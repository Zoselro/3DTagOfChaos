using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 탈출 모드의 방장 전용 규칙(EscapePlan.md §5.2~§5.6, Request1009Plan.md §25). 모든 요청을 검증해 EscapeState를 바꾸고 Room Prop으로 쓴다.
// 방장이 바뀌면 새 방장이 Room Prop의 상태를 이어받는다(Adopt). EscapeManager가 소유하는 순수 C# 클래스다.
// 개편(§25): 사분면 건물 4곳(재료는 판마다 섞음, 활성 = 쿠키 수 + 1, 최대 4) → 모두 가동되면 동서남북 탈출구 중 2개가 열린다.
// 첫 탈출(탈출구·로켓)부터 마녀 타이머, 타이머가 흐르기 시작하면 무지개 상자(헬리콥터 모자). 괴물 소음폭탄, 맵에 깔린 AI.
public sealed class EscapeAuthority
{
    public const int InventorySlots = 4;
    private const float PickUpReach = 3.5f;       // 바닥 아이템을 줍는 최대 거리(m, 요청 검증)
    private const float InteractReach = 6f;       // 상자·로켓·무지개 상자 요청 검증 거리(m, 여유 포함)
    private const float RepairReach = 5.5f;       // 건물 수리 자리까지(건물 반 크기 7 m 안쪽)
    private const float ExitReach = 7f;           // 탈출구 트리거까지
    private const float DropSpread = 0.6f;
    private const float RepairFinishTolerance = 0.1f; // 누르는 사람 화면과 방장 계산의 차이(요청 왕복 지연)
    private const float HoldTolerance = 0.5f;          // 해체 20초 검사 여유
    public const int DecoysPerPlayer = 10;             // 쿠키·스파이 1인당 맵에 깔리는 AI(§25.5 L)
    public const int BombMaxCharges = 1;               // 소음폭탄은 한 번에 하나(§26 R4) — 치우거나 터진 뒤 BombCooldown
    public const float BombCooldown = 20f;
    public const float BombBuildingRadius = 12f;       // 폭탄이 이 거리 안 건물에 붙는다(수리 시작 때 터짐)
    public const float BombBlastRadius = 4f;
    public const float HatRiseSeconds = 20f;           // 모자: 40 m까지 떠오르는 시간(§25.3.1 R6)
    private const float DecoyPlaceAttempts = 40;
    public const float DecoyCatchSeconds = 2.63f;      // 괴물 잡는 동작(GrabKill 클립 길이) 뒤 AI를 지운다(§26 R10)
    private const float MaterialCheckSeconds = 2f;      // D 안전장치 검사 간격(§26 R1)

    private readonly EscapeManager manager;
    private readonly EscapeCatalogSO catalog;
    private readonly EscapeRecipeSO recipe;
    private readonly System.Random rng = new System.Random();
    private readonly Dictionary<int, float> toolReadyAt = new Dictionary<int, float>();
    private readonly Dictionary<int, Vector3> lastPositions = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, HoldStart> dismantleStarts = new Dictionary<int, HoldStart>();
    private readonly Dictionary<int, int> repairSlots = new Dictionary<int, int>(); // 수리 중인 사람이 든 인벤토리 칸
    private readonly Dictionary<int, Vector3> takeoffPositions = new Dictionary<int, Vector3>(); // 모자 이륙 자리(탈출하면 그 자리에 아이템을 둔다, §26 A)
    private float nextMaterialCheck;
    private bool launchRequested; // 로켓 발사는 한 번만 쓴다 — 응답 전에 다시 불려 두 번 쓰이던 문제(F-05)
    private bool witchTimerRequested;
    private readonly Dictionary<int, float> lastPositionAt = new Dictionary<int, float>();
    private readonly Dictionary<int, float> suspectUntil = new Dictionary<int, float>();
    private readonly Dictionary<int, float> moveExemptUntil = new Dictionary<int, float>(); // 호박마차 순간이동 직후(이동 검사 제외)
    private const float SuspectSeconds = 1f;          // 불가능한 이동을 본 뒤 그 사람의 위치가 중요한 요청을 거부하는 시간(S-15, §26 H2: 3초 → 1초)
    private const float PlausibleSpeedFactor = 1.5f;

    private struct HoldStart
    {
        public int Target;
        public float At; // 방장 시계(Time.realtimeSinceStartup)
    }

    public EscapeState State { get; private set; }

    public EscapeAuthority(EscapeManager manager, EscapeCatalogSO catalog, EscapeRecipeSO recipe)
    {
        this.manager = manager;
        this.catalog = catalog;
        this.recipe = recipe;
    }

    // ---------------- setup ----------------

    // 판이 시작될 때 한 번(방장). 건물 재료·활성 건물·로켓 자리·상자·아이템·AI를 정한다.
    public void Initialize(int chestAnchorCount)
    {
        var s = new EscapeState();
        int players = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
        int monsters = Mathf.Max(0, RoomState.MonsterCount());
        int spies = RoomState.TryGetIntArray(NetKeys.SpyActorNumbers, out int[] spyActors) ? spyActors.Length : 0;
        int cookies = Mathf.Max(1, players - monsters - spies);

        // 건물: 재료 4종을 판마다 섞어 4곳에 하나씩, 활성 = 쿠키 수 + 1(최대 4, §25.3 Q3)
        var materials = new List<ItemSO>();
        foreach (ItemSO m in recipe.BuildingMaterials) if (m != null) materials.Add(m);
        Shuffle(materials);
        int activeCount = ActiveBuildingsFor(cookies, Mathf.Min(EscapeState.BuildingCount, materials.Count));
        var activeOrder = new List<int>();
        for (int i = 0; i < materials.Count && i < EscapeState.BuildingCount; i++) activeOrder.Add(i);
        Shuffle(activeOrder);
        var active = new HashSet<int>(activeOrder.GetRange(0, Mathf.Min(activeCount, activeOrder.Count)));
        for (int i = 0; i < EscapeState.BuildingCount; i++)
        {
            string id = i < materials.Count ? materials[i].ItemId : string.Empty;
            s.Buildings.Add(new EscapeState.Building { MaterialId = id, Active = active.Contains(i) && id.Length > 0, ItemIndex = -1 });
        }

        // 스파이 로켓(§26.3.3): 칸마다 맵별로 정한 재료(레시피 rocketSlots). 수리키트는 칸을 50%씩 채운다. 자리는 후보 중 무작위.
        var activeIds = new HashSet<string>();
        foreach (EscapeState.Building b in s.Buildings) if (b.Active) activeIds.Add(b.MaterialId);
        var spyExtras = new List<ItemSO>(); // 이번 판 수리 대상이 아니라 상자에 없는 로켓 재료 → 스파이 상자에 1개씩(Q5·Q20)
        foreach (EscapeRecipeSO.RocketSlot r in recipe.RocketSlots)
        {
            var accepts = new List<string>();
            bool present = false;
            if (r.accepts != null)
                foreach (ItemSO a in r.accepts)
                {
                    if (a == null) continue;
                    accepts.Add(a.ItemId);
                    if (activeIds.Contains(a.ItemId)) present = true;
                }
            if (accepts.Count == 0) continue;
            s.RocketSlots.Add(new EscapeState.Slot { Label = string.Empty, Accepts = accepts.ToArray(), ItemIndex = -1, Anchor = s.RocketSlots.Count });
            if (!present && spies > 0) spyExtras.Add(catalog.Find(accepts[rng.Next(accepts.Count)]));
        }
        int kitCount = spies > 0 ? Mathf.Max(1, recipe.RepairKitCount > 0 ? recipe.RepairKitCount : s.RocketSlots.Count) : 0; // 키트 수 = 로켓 칸 수(§26)
        s.RocketAnchor = manager.RocketAnchorCount > 0 ? rng.Next(manager.RocketAnchorCount) : -1;

        // 상자: 일반 = 쿠키 × 8, 스파이 = 스파이 × 5(§25 요청). 상자 자리 중에서 무작위.
        var anchors = new List<int>();
        for (int i = 0; i < chestAnchorCount; i++) anchors.Add(i);
        Shuffle(anchors);
        int spyChests = Mathf.Min(spies * 5 + spyExtras.Count, anchors.Count);
        int normalChests = Mathf.Min(cookies * 8, anchors.Count - spyChests);
        for (int i = 0; i < normalChests; i++) s.Chests.Add(new EscapeState.Chest { Anchor = anchors[i], Spy = false });
        for (int i = 0; i < spyChests; i++) s.Chests.Add(new EscapeState.Chest { Anchor = anchors[normalChests + i], Spy = true });

        // 일반 상자: 활성 건물 재료 각 2개 + (스파이가 있으면) 수리키트(칸 수 − 1개) + 남은 상자는 쿠키 아이템(비율 5:3:5, §25.3.2 S1)
        var normalItems = new List<string>();
        foreach (EscapeState.Building b in s.Buildings)
            if (b.Active) { normalItems.Add(b.MaterialId); normalItems.Add(b.MaterialId); }
        if (catalog.RepairKit != null) for (int k = 1; k < kitCount; k++) normalItems.Add(catalog.RepairKit.ItemId);
        while (normalItems.Count < normalChests)
        {
            ItemSO extra = PickWeighted(catalog.CookieItems, catalog.CookieItemWeights);
            if (extra == null) break;
            normalItems.Add(extra.ItemId);
        }
        var normalIndices = new List<int>();
        for (int i = 0; i < s.Chests.Count; i++) if (!s.Chests[i].Spy) normalIndices.Add(i);
        Shuffle(normalIndices);
        for (int i = 0; i < normalItems.Count; i++)
        {
            ItemSO item = catalog.Find(normalItems[i]);
            if (item == null) continue;
            var entry = NewItem(item);
            if (i < normalIndices.Count) { entry.Loc = ItemLocation.Chest; entry.A = normalIndices[i]; }
            else { entry.Loc = ItemLocation.Ground; entry.Pos = manager.DropPointNear(manager.BuildingRepairPoint(i % EscapeState.BuildingCount), rng.Next()); } // 상자가 모자라면 건물 옆 바닥
            s.Items.Add(entry);
        }

        // 스파이 상자: 스파이 1명당 수리키트 1 + 해체 도구 1 + 나머지 3개는 겹치지 않게 무작위(§25.3.2 R8)
        var spyIndices = new List<int>();
        for (int i = 0; i < s.Chests.Count; i++) if (s.Chests[i].Spy) spyIndices.Add(i);
        for (int spy = 0; spy < spies; spy++)
        {
            var kit = new List<ItemSO> { catalog.RepairKit, catalog.DismantleTool };
            var pool = new List<ItemSO>(catalog.SpyRandomTools);
            Shuffle(pool);
            for (int k = 0; k < 3 && k < pool.Count; k++) kit.Add(pool[k]);
            if (spy == 0) kit.AddRange(spyExtras); // 상자에 없는 로켓 재료(스파이 한 명 몫)
            int first = spy * 5 + (spy > 0 ? spyExtras.Count : 0);
            for (int k = 0; k < kit.Count && first + k < spyIndices.Count; k++)
            {
                if (kit[k] == null) continue;
                var entry = NewItem(kit[k]);
                entry.Loc = ItemLocation.Chest;
                entry.A = spyIndices[first + k];
                s.Items.Add(entry);
            }
        }

        // AI: 쿠키·스파이 1인당 10마리를 맵에 흩어 놓는다(§25.5 L)
        foreach (Player p in RoomState.Players)
        {
            if (RoomState.IsMonster(p.ActorNumber)) continue;
            for (int k = 0; k < DecoysPerPlayer; k++)
            {
                if (!TryDecoySpot(out Vector3 at)) break;
                s.Decoys.Add(new EscapeState.Decoy { Id = s.NextId++, Owner = p.ActorNumber, Pos = at, Yaw = (float)(rng.NextDouble() * 360.0) });
            }
        }

        State = s;
        Commit();
        Debug.Log($"[EscapeAuthority] Initialized {recipe.MapName}: cookies={cookies} spies={spies} buildings={Summary(s)} chests={s.Chests.Count} " +
                  $"items={s.Items.Count} rocketSpot={s.RocketAnchor} decoys={s.Decoys.Count}");
    }

    // 활성 건물 수(순수 계산, §25.3 Q3): 쿠키 수 + 1, 최대 buildings.
    public static int ActiveBuildingsFor(int cookies, int buildings) => Mathf.Clamp(cookies + 1, 1, Mathf.Max(1, buildings));

    private bool TryDecoySpot(out Vector3 at)
    {
        for (int i = 0; i < DecoyPlaceAttempts; i++)
            if (manager.TryRandomMapPoint(rng, out at)) return true;
        at = default;
        return false;
    }

    private static string Summary(EscapeState s)
    {
        var parts = new List<string>();
        foreach (EscapeState.Building b in s.Buildings) parts.Add((b.Active ? "" : "-") + b.MaterialId);
        return string.Join(",", parts);
    }

    private EscapeState.Item NewItem(ItemSO item) => new EscapeState.Item { Id = item.ItemId, Charges = item.IsTool ? item.Tool.Charges : 0 };

    private ItemSO PickWeighted(IReadOnlyList<ItemSO> items, IReadOnlyList<int> weights)
    {
        if (items == null || items.Count == 0) return null;
        int total = 0;
        for (int i = 0; i < items.Count; i++) total += WeightAt(weights, i);
        if (total <= 0) return items[rng.Next(items.Count)];
        int roll = rng.Next(total);
        for (int i = 0; i < items.Count; i++)
        {
            roll -= WeightAt(weights, i);
            if (roll < 0) return items[i];
        }
        return items[items.Count - 1];
    }

    private static int WeightAt(IReadOnlyList<int> weights, int i) => weights != null && i < weights.Count ? Mathf.Max(0, weights[i]) : 1;

    // 방장이 바뀌었을 때 Room Prop의 상태를 이어받는다.
    public void Adopt(EscapeState current)
    {
        State = current?.Clone();
        launchRequested = RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out _);
        witchTimerRequested = RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out _);
    }

    // 모든 클라이언트가 일정 간격으로(EscapeManager): 요청 거리 검사와 나간 사람의 아이템을 떨어뜨릴 위치를 기억해 둔다.
    // 방장이 아닐 때도 기록해 두어, 방장이 된 첫 순간에 "위치를 모르면 통과"하는 틈이 없다(F-10).
    // 방장은 쿠키의 이동이 가능한 속도인지도 본다(S-15) — 호박마차 순간이동·모자 비행은 제외.
    public void TrackPositions()
    {
        float now = Time.realtimeSinceStartup;
        bool judge = PhotonNetwork.IsMasterClient;
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.View == null || c.View.Owner == null) continue;
            int actor = c.View.Owner.ActorNumber;
            Vector3 pos = c.gameObject.transform.position;
            bool exempt = (moveExemptUntil.TryGetValue(actor, out float until) && now < until) || (State != null && State.FlightIndex(actor) >= 0);
            if (judge && !exempt && lastPositions.TryGetValue(actor, out Vector3 prev) && lastPositionAt.TryGetValue(actor, out float prevAt))
            {
                var cookie = c as HideOrSeekPlayer;
                if (cookie != null && cookie.CurrentCarrierViewId < 0
                    && !NetSecurityRules.IsPlausibleMove(prev, pos, now - prevAt, cookie.MaxMoveSpeed * PlausibleSpeedFactor)
                    && !manager.IsNearCookieSpawn(pos) // 맵 밖으로 떨어져 스폰 자리로 돌아온 것은 정상(§26 H2)
                    && !manager.IsRoomEjectMove(prev, pos)) // 술래방에서 문 밖으로 내보내진 것도 정상(§28.15 E1)
                {
                    suspectUntil[actor] = now + SuspectSeconds;
                    Debug.LogWarning($"[NetGuard] Actor {actor} moved {Vector3.Distance(prev, pos):F1} m in {now - prevAt:F2}s — escape requests ignored for {SuspectSeconds}s.");
                }
            }
            lastPositions[actor] = pos;
            lastPositionAt[actor] = now;
        }
    }

    private bool IsSuspect(int actor) => suspectUntil.TryGetValue(actor, out float until) && Time.realtimeSinceStartup < until;

    // ---------------- master tick ----------------

    // 방장 화면에서 매 프레임(EscapeManager): 다 찬 수리 마무리, 수리하던 사람이 사라진 건물 정리, 무지개 상자 등장, 지난 폭탄 정리, 녹음기 AI 걷기 끝.
    public void Tick()
    {
        if (State == null) return;
        double now = PhotonNetwork.Time;
        bool changed = false;

        for (int b = 0; b < State.Buildings.Count; b++)
        {
            EscapeState.Building building = State.Buildings[b];
            if (building.Repairer == 0 || building.Repaired) continue;
            int actor = building.Repairer;
            bool stillThere = IsActiveEscaper(actor) && HeldMaterialFor(actor, b) >= 0;
            if (!stillThere) { StopRepair(b, now); changed = true; continue; }
            if (building.ProgressAtTime(now) >= 1f) { CompleteRepair(b, now); changed = true; }
        }

        if (State.RainbowAnchor < 0 && manager.RainbowAnchorCount > 0 && WitchTimerFlowing(now))
        {
            State.RainbowAnchor = rng.Next(manager.RainbowAnchorCount);
            State.Rainbow = RainbowState.Closed;
            Notice(EscapeNoticeKind.RainbowAppeared, null);
            changed = true;
        }

        for (int i = State.Bombs.Count - 1; i >= 0; i--)
            if (State.Bombs[i].Spent && now >= State.Bombs[i].SpentAt + BombCooldown) { State.Bombs.RemoveAt(i); changed = true; }

        // 괴물에게 잡힌 AI: 잡는 동작이 끝나면 지운다(§26 R10)
        for (int i = State.Decoys.Count - 1; i >= 0; i--)
            if (State.Decoys[i].Caught && now >= State.Decoys[i].CaughtAt + DecoyCatchSeconds) { State.Decoys.RemoveAt(i); changed = true; }

        if (Time.realtimeSinceStartup >= nextMaterialCheck)
        {
            nextMaterialCheck = Time.realtimeSinceStartup + MaterialCheckSeconds;
            changed |= RestoreMissingMaterials();
        }

        if (changed) Commit();
    }

    // 마녀 타이머가 흐르기 시작했는지(등장 연출이 끝난 뒤, §25 결정 1).
    private static bool WitchTimerFlowing(double now) =>
        WitchTimeline.Mode == WitchMode.TimeAttack && WitchTimeline.TryGetAppearAt(out double appearAt)
        && now >= WitchTimeline.WalkStart(WitchMode.TimeAttack, appearAt);

    // ---------------- D: 재료 안전장치(§26 R1·§26.3.3) ----------------

    // 수리 안 된 건물의 재료가 쿠키 손이 닿는 곳(일반 상자·바닥·쿠키 손)에 하나도 없으면, 그리고 로켓에 아직 필요한 재료가
    // 스파이 손이 닿는 곳(상자·바닥·스파이 손)에 하나도 없으면 빈 상자에 새로 만든다. 건물·로켓에 들어간 것, 없어진 것은 세지 않는다.
    private bool RestoreMissingMaterials()
    {
        bool changed = false;
        foreach (EscapeState.Building b in State.Buildings)
        {
            if (!b.Active || b.Repaired || string.IsNullOrEmpty(b.MaterialId)) continue;
            if (CountReachable(b.MaterialId, forSpy: false) > 0) continue;
            changed |= CreateMaterial(b.MaterialId, spyChest: false);
        }
        if (SpyStillPlaying())
            foreach (EscapeState.Slot slot in State.RocketSlots)
            {
                if (slot.Filled || slot.Accepts == null || slot.Accepts.Length == 0) continue;
                int found = 0;
                foreach (string id in slot.Accepts) found += CountReachable(id, forSpy: true);
                if (found == 0) changed |= CreateMaterial(slot.Accepts[0], spyChest: true);
            }
        return changed;
    }

    private int CountReachable(string itemId, bool forSpy)
    {
        int n = 0;
        foreach (EscapeState.Item it in State.Items)
        {
            if (it.Id != itemId) continue;
            switch (it.Loc)
            {
                case ItemLocation.Chest:
                    if (it.A >= 0 && it.A < State.Chests.Count && (forSpy || !State.Chests[it.A].Spy)) n++;
                    break;
                case ItemLocation.Ground:
                    if (manager.IsReachableGround(it.Pos)) n++;
                    break;
                case ItemLocation.Held:
                    if (IsActiveEscaper(it.A) && RoomState.IsSpy(it.A) == forSpy) n++; // 상대 편이 든 것은 세지 않는다(§26.3.3)
                    break;
            }
        }
        return n;
    }

    private bool SpyStillPlaying()
    {
        if (!RoomState.TryGetIntArray(NetKeys.SpyActorNumbers, out int[] spies)) return false;
        foreach (int spy in spies) if (IsActiveEscaper(spy)) return true;
        return false;
    }

    private bool CreateMaterial(string itemId, bool spyChest)
    {
        ItemSO def = catalog.Find(itemId);
        if (def == null) return false;
        State.Items.Add(NewItem(def));
        int item = State.Items.Count - 1;
        int chest = spyChest ? PickEmptyChest(spy: true) : -1;
        if (chest >= 0)
        {
            EscapeState.Chest ch = State.Chests[chest];
            ch.Opened = false;
            State.Chests[chest] = ch;
            EscapeState.Item it = State.Items[item];
            it.Loc = ItemLocation.Chest;
            it.A = chest;
            State.Items[item] = it;
        }
        else
        {
            RespawnInEmptyChest(item);
        }
        if (!spyChest) SendDelayedNoticeToCookies(new object[] { (byte)EscapeNoticeKind.MaterialRestored, itemId, item, PhotonNetwork.Time });
        Debug.Log($"[EscapeAuthority] Material safety net: {itemId} was nowhere reachable — created again ({(spyChest ? "spy" : "cookie")}).");
        return true;
    }

    private int PickEmptyChest(bool spy)
    {
        var pool = new List<int>();
        for (int c = 0; c < State.Chests.Count; c++)
            if (State.Chests[c].Spy == spy && State.ItemInChest(c) < 0) pool.Add(c);
        return pool.Count > 0 ? pool[rng.Next(pool.Count)] : -1;
    }

    // ---------------- requests ----------------

    public void Handle(EscapeRequest r)
    {
        if (State == null) return;
        if (!EscapeManager.ActionsAllowed) return; // 결과가 나온 뒤 늦게 도착한 요청은 받지 않는다
        if (IsSuspect(r.Sender) && NeedsTrustedPosition(r.Op)) // 방금 불가능한 이동을 했다(S-15) — 위치가 중요한 요청만 막는다(§26 H2)
        {
            Debug.Log($"[EscapeAuthority] {r.Op} from actor {r.Sender} ignored: suspicious move just now.");
            return;
        }
        bool changed;
        switch (r.Op)
        {
            case EscapeOp.OpenChest: changed = OpenChest(r.Sender, r.A); break;
            case EscapeOp.PickUp: changed = PickUp(r.Sender, r.A); break;
            case EscapeOp.Drop: changed = Drop(r.Sender, r.A, r.Pos); break;
            case EscapeOp.DropAll: changed = DropAll(r.Sender, r.Pos); break;
            case EscapeOp.RocketInsert: changed = RocketInsert(r.Sender, r.A); break;
            case EscapeOp.RocketBoard: changed = RocketBoard(r.Sender); break;
            case EscapeOp.Exit: changed = Exit(r.Sender, r.A); break;
            case EscapeOp.ToolUse: changed = ToolUse(r); break;
            case EscapeOp.RepairBegin: changed = RepairBegin(r.Sender, r.A, r.B); break;
            case EscapeOp.RepairStop: changed = RepairStop(r.Sender, r.A, finish: false); break;
            case EscapeOp.RepairFinish: changed = RepairStop(r.Sender, r.A, finish: true); break;
            case EscapeOp.DismantleBegin: DismantleBegin(r.Sender, r.A); changed = false; break;
            case EscapeOp.Dismantle: changed = Dismantle(r.Sender, r.A); break;
            case EscapeOp.RainbowOpen: changed = RainbowOpen(r.Sender); break;
            case EscapeOp.RainbowBreak: changed = RainbowBreak(r.Sender); break;
            case EscapeOp.HatFly: changed = HatFly(r.Sender, r.A); break;
            case EscapeOp.HatEscape: changed = HatEscape(r.Sender); break;
            case EscapeOp.PlaceBomb: changed = PlaceBomb(r.Sender, r.Pos); break;
            case EscapeOp.RemoveBomb: changed = RemoveBomb(r.Sender, r.A); break;
            case EscapeOp.DecoyCatch: changed = DecoyCatch(r.Sender, r.A); break;
            default: changed = false; break;
        }
        if (changed) Commit();
    }

    // 끝내는 요청(떼기·떨어뜨리기·폭탄 치우기)은 이동이 의심스러워도 받는다 — 받지 않으면 수리가 끝나지 않는 등 화면과 방장이 어긋난다.
    private static bool NeedsTrustedPosition(EscapeOp op)
    {
        switch (op)
        {
            case EscapeOp.Drop:
            case EscapeOp.DropAll:
            case EscapeOp.RepairStop:
            case EscapeOp.RepairFinish:
            case EscapeOp.RemoveBomb:
            case EscapeOp.HatFly:
                return false;
            default:
                return true;
        }
    }

    private bool OpenChest(int actor, int chest)
    {
        if (!IsActiveEscaper(actor) || chest < 0 || chest >= State.Chests.Count) return false;
        if (State.FlightIndex(actor) >= 0) return false;                 // 모자로 나는 중에는 줍지 못한다(§26 A)
        if (GamePhaseState.Current == GamePhase.Prepare) return false;   // 준비 시간에는 상자가 없다(§26 R2)
        EscapeState.Chest c = State.Chests[chest];
        if (c.Opened) return false;
        if (c.Spy && !RoomState.IsSpy(actor)) return false; // 쿠키는 스파이 상자를 열 수 없다(D7)
        if (!IsNear(actor, manager.ChestPosition(c.Anchor), InteractReach)) return false;

        c.Opened = true;
        State.Chests[chest] = c;
        int item = State.ItemInChest(chest);
        if (item >= 0) Give(actor, item, manager.ChestFront(c.Anchor));
        return true;
    }

    private bool PickUp(int actor, int item)
    {
        if (!IsActiveEscaper(actor) || item < 0 || item >= State.Items.Count) return false;
        EscapeState.Item it = State.Items[item];
        if (it.Loc != ItemLocation.Ground || !IsNear(actor, it.Pos, PickUpReach) || State.FlightIndex(actor) >= 0) return false;
        Give(actor, item, it.Pos);
        return true;
    }

    private bool Drop(int actor, int slot, Vector3 pos)
    {
        int item = State.HeldItemAt(actor, slot);
        if (item < 0) return false;
        PutOnGround(item, ValidDropPosition(actor, pos));
        return true;
    }

    private bool DropAll(int actor, Vector3 pos, bool trusted = false)
    {
        bool any = false;
        int n = 0;
        Vector3 origin = trusted ? pos : ValidDropPosition(actor, pos);
        for (int i = 0; i < State.Items.Count; i++)
        {
            if (State.Items[i].Loc != ItemLocation.Held || State.Items[i].A != actor) continue;
            float angle = n++ * 90f;
            PutOnGround(i, origin + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * DropSpread);
            any = true;
        }
        return any;
    }

    // ---------------- buildings (§25.5 E) ----------------

    // 그 건물 재료(또는 수리키트를 받는 건물이면 수리키트)를 지금 들고 있는 칸 → 아이템 번호. 없으면 -1.
    private int HeldMaterialFor(int actor, int building)
    {
        string want = State.Buildings[building].MaterialId;
        if (repairSlots.TryGetValue(actor, out int slot))
        {
            int item = State.HeldItemAt(actor, slot);
            return item >= 0 && State.Items[item].Id == want ? item : -1;
        }
        // 방장이 바뀌어 칸 기록이 없으면 그 사람이 든 맞는 재료를 찾는다(§26 H2)
        for (int s = 0; s < InventorySlots; s++)
        {
            int item = State.HeldItemAt(actor, s);
            if (item >= 0 && State.Items[item].Id == want) { repairSlots[actor] = s; return item; }
        }
        return -1;
    }

    private bool RepairBegin(int actor, int b, int invSlot)
    {
        if (!IsActiveEscaper(actor) || b < 0 || b >= State.Buildings.Count) return RejectRepair(actor, b, "not playing");
        EscapeState.Building building = State.Buildings[b];
        if (!building.Active || building.Repaired) return RejectRepair(actor, b, "already running");
        if (building.Repairer != 0 && building.Repairer != actor) return RejectRepair(actor, b, "busy"); // 한 건물에 한 사람
        if (!IsNear(actor, manager.BuildingRepairPoint(b), RepairReach)) return RejectRepair(actor, b, "too far");
        int item = State.HeldItemAt(actor, invSlot);
        if (item < 0 || State.Items[item].Id != building.MaterialId) return RejectRepair(actor, b, "wrong item"); // 맞지 않는 재료는 넣을 수 없다(스파이 포함, §25 결정 2)

        double now = PhotonNetwork.Time;
        building.Progress = building.ProgressAtTime(now);
        building.ProgressAt = now;
        building.Repairer = actor;
        State.Buildings[b] = building;
        repairSlots[actor] = invSlot;
        DetonateBombsAt(b, actor, now);
        return true;
    }

    // 수리 시작을 받지 않은 이유를 남긴다(§26 H2 — "게이지가 안 찬다" 원인 확인용).
    private static bool RejectRepair(int actor, int b, string reason)
    {
        Debug.Log($"[EscapeAuthority] RepairBegin from actor {actor} on building {b} rejected: {reason}.");
        return false;
    }

    private bool RepairStop(int actor, int b, bool finish)
    {
        if (b < 0 || b >= State.Buildings.Count) return false;
        EscapeState.Building building = State.Buildings[b];
        if (building.Repairer != actor || building.Repaired) return false;
        double now = PhotonNetwork.Time;
        float progress = building.ProgressAtTime(now);
        // 누르는 사람 화면에서 다 찼다(finish) — 방장 계산이 왕복 지연만큼 늦을 수 있어 여유를 둔다. 떼기만 했으면 지금 값에서 멈춘다.
        if (progress >= 1f - (finish ? RepairFinishTolerance : 0f) && HeldMaterialFor(actor, b) >= 0)
        {
            CompleteRepair(b, now);
            return true;
        }
        StopRepair(b, now);
        return true;
    }

    private void StopRepair(int b, double now)
    {
        EscapeState.Building building = State.Buildings[b];
        building.Progress = building.ProgressAtTime(now);
        building.ProgressAt = now;
        repairSlots.Remove(building.Repairer);
        building.Repairer = 0;
        State.Buildings[b] = building;
    }

    private void CompleteRepair(int b, double now)
    {
        EscapeState.Building building = State.Buildings[b];
        int actor = building.Repairer;
        int item = HeldMaterialFor(actor, b);
        repairSlots.Remove(actor);
        building.Repairer = 0;
        building.Progress = 1f;
        building.ProgressAt = now;
        if (item >= 0)
        {
            EscapeState.Item it = State.Items[item];
            it.Loc = ItemLocation.Device;
            it.A = b;
            State.Items[item] = it;
            building.ItemIndex = item;
        }
        State.Buildings[b] = building;
        if (item < 0) { StopRepair(b, now); return; }
        Notice(EscapeNoticeKind.BuildingRepaired, building.MaterialId);
        if (State.AllBuildingsRepaired && !State.AnyExitOpen) OpenExits(now);
    }

    // 모든 건물이 가동됐다: 탈출구 4개 중 2개를 무작위로 연다. 맵의 AI는 이 순간 모두 사라진다(§25.3 Q1).
    private void OpenExits(double now)
    {
        var exits = new List<int>();
        for (int i = 0; i < Mathf.Min(EscapeState.ExitCount, manager.ExitCount); i++) exits.Add(i);
        Shuffle(exits);
        byte mask = 0;
        for (int i = 0; i < 2 && i < exits.Count; i++) mask |= (byte)(1 << exits[i]);
        State.OpenExits = mask;
        State.ExitsOpenedAt = now;
        State.Decoys.Clear();
        Notice(EscapeNoticeKind.ExitsOpened, null);
        Debug.Log($"[EscapeAuthority] All buildings running: exits {mask} open, decoys cleared.");
    }

    // 스파이 해체 도구(§25.3 Q8): 누르기 시작한 건물·시각을 기억해 둔다.
    private void DismantleBegin(int actor, int b)
    {
        if (!CanDismantle(actor, b)) return;
        dismantleStarts[actor] = new HoldStart { Target = b, At = Time.realtimeSinceStartup };
    }

    private bool CanDismantle(int actor, int b)
    {
        if (!RoomState.IsSpy(actor) || !IsActiveEscaper(actor) || b < 0 || b >= State.Buildings.Count) return false;
        EscapeState.Building building = State.Buildings[b];
        if (!building.Active || building.ItemIndex < 0 || State.AnyExitOpen) return false; // 탈출구가 열린 뒤에는 뺄 수 없다
        if (HoldsTwoHanded(actor)) return false; // 두 손으로 무언가를 들고 있으면 해체할 수 없다(뺀 재료를 받을 손이 없다, §26 R15)
        return HeldTool(actor, ToolKind.Dismantle) >= 0 && IsNear(actor, manager.BuildingRepairPoint(b), RepairReach);
    }

    // 같은 건물을 DismantleSeconds(여유 포함) 이상 누르고 있었는지(순수 계산, 시험용).
    public static bool HeldLongEnough(float startedAt, float now, float seconds) => now - startedAt >= seconds - HoldTolerance;

    private bool Dismantle(int actor, int b)
    {
        if (!CanDismantle(actor, b)) return false;
        if (!dismantleStarts.TryGetValue(actor, out HoldStart started) || started.Target != b
            || !HeldLongEnough(started.At, Time.realtimeSinceStartup, EscapeState.DismantleSeconds)) return false;
        dismantleStarts.Remove(actor);

        EscapeState.Building building = State.Buildings[b];
        int item = building.ItemIndex;
        building.ItemIndex = -1;
        building.Progress = 0f;
        building.ProgressAt = PhotonNetwork.Time;
        State.Buildings[b] = building;
        UseUpTool(HeldTool(actor, ToolKind.Dismantle)); // 먼저 도구를 써서 칸을 비운다
        Give(actor, item, manager.DropPointNear(manager.BuildingRepairPoint(b), rng.Next())); // 뺀 재료는 스파이 손으로(§26 R15)
        AnnounceStolen(item);
        return true;
    }

    // ---------------- exits & rocket ----------------

    // 쿠키가 열린 탈출구로 걸어 나갔다(§25.5 F). 스파이는 탈출구로 나갈 수 없다(§25.3.1 R10).
    private bool Exit(int actor, int exit)
    {
        if (RoomState.IsSpy(actor) || RoomState.IsMonster(actor) || !IsActiveEscaper(actor)) return false;
        if (!State.ExitOpen(exit) || exit >= manager.ExitCount) return false;
        Vector3 at = manager.ExitPosition(exit);
        if (!IsNear(actor, at, ExitReach)) return false;
        DropAll(actor, at);
        State.Escaped.Add(actor);
        Debug.Log($"[EscapeAuthority] Actor {actor} escaped through exit {exit}.");
        StartWitchTimerIfOthersLeft(at);
        return true;
    }

    private bool RocketInsert(int actor, int invSlot)
    {
        if (!RoomState.IsSpy(actor) || !IsActiveEscaper(actor) || !IsNear(actor, manager.RocketPosition, InteractReach)) return false;
        int item = State.HeldItemAt(actor, invSlot);
        if (item < 0) return false;
        EscapeState.Item it = State.Items[item];
        if (catalog.RepairKit != null && it.Id == catalog.RepairKit.ItemId)
        {
            // 수리키트: 칸을 50%씩 채우고 없어진다(다시 생기지 않음, §26.3.3)
            if (!EscapeState.ApplyRepairKit(State.RocketSlots, slot => SpyHoldsAny(actor, slot.Accepts))) return false;
            it.Loc = ItemLocation.Gone;
            State.Items[item] = it;
            return true;
        }
        int rocketSlot = FindRocketSlotFor(it.Id);
        if (rocketSlot < 0) return false;

        EscapeState.Slot filled = State.RocketSlots[rocketSlot];
        filled.RocketFilled = true;
        State.RocketSlots[rocketSlot] = filled;

        // 로켓에 넣은 재료는 다른 상자에 다시 생긴다(§25.3.1 R4 — 쿠키 재료가 모자라지 않게). 같은 지연 뒤 알린다.
        bool onGround = RespawnInEmptyChest(item);
        AnnounceRespawn(item, onGround);
        return true;
    }

    private bool SpyHoldsAny(int actor, string[] ids)
    {
        if (ids == null) return false;
        for (int s = 0; s < InventorySlots; s++)
        {
            int item = State.HeldItemAt(actor, s);
            if (item >= 0 && System.Array.IndexOf(ids, State.Items[item].Id) >= 0) return true;
        }
        return false;
    }

    private bool RocketBoard(int actor)
    {
        if (!RoomState.IsSpy(actor) || !IsActiveEscaper(actor) || !State.RocketComplete) return false;
        if (!IsNear(actor, manager.RocketPosition, InteractReach) || HoldsTwoHanded(actor)) return false;
        DropAll(actor, manager.RocketPosition);
        State.Boarded.Add(actor);
        TryLaunchRocket();
        return true;
    }

    // 남아 있는 스파이가 모두 탔으면 로켓이 떠난다(D21). 남은 사람이 있으면 마녀 타이머가 시작된다(첫 탈출, §25.5 G).
    public void TryLaunchRocket()
    {
        if (launchRequested || State == null || State.Boarded.Count == 0 || RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out _)) return;
        if (!RoomState.TryGetIntArray(NetKeys.SpyActorNumbers, out int[] spies)) return;
        foreach (int spy in spies)
        {
            Player p = FindPlayer(spy);
            if (p == null || RoomState.IsBroken(p)) continue; // 나갔거나 잡힌 스파이는 뺀다
            if (!State.Boarded.Contains(spy)) return;
        }
        launchRequested = true;
        AuthorityProps.SetRoom(new Hashtable { { NetKeys.SpyEscapedAt, PhotonNetwork.Time } }); // 로켓 발사(연출 기준) — 늘 기록한다
        StartWitchTimerIfOthersLeft(manager.RocketPosition);
        Debug.Log("[EscapeAuthority] Rocket launched.");
    }

    private void StartWitchTimerIfOthersLeft(Vector3 point)
    {
        if (AnyoneStillPlaying()) StartWitchTimer(point);
    }

    // 마녀 타이머 시작(방장, 한 번만, §25.5 G): 앞 샷(첫 탈출 지점) 3초 → 마녀 등장 연출 10초 → 그때부터 타이머가 흐른다.
    // 안전 상한(GameRuleController)도 이것을 부른다.
    public void StartWitchTimer(Vector3 point)
    {
        if (witchTimerRequested || RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out _) || RoomState.TryGetInt(NetKeys.GameResult, out _)) return;
        witchTimerRequested = true;
        double now = PhotonNetwork.Time;
        double appearAt = WitchTimeline.RocketAppearAt(now);
        AuthorityProps.SetRoom(new Hashtable
        {
            { NetKeys.FirstEscapeAt, now },
            { NetKeys.FirstEscapePoint, point },
            { NetKeys.WitchMode, (int)WitchMode.TimeAttack },
            { NetKeys.WitchAppearAt, appearAt },
            { NetKeys.TimeAttackEndTime, WitchTimeline.TimeAttackEnd(appearAt, RoomState.TimeAttackSeconds()) },
        });
        // 마녀 시간에는 AI가 모두 사라진다(등장 연출 전, §26 R13·Q16)
        if (State != null && State.Decoys.Count > 0)
        {
            State.Decoys.Clear();
            Notice(EscapeNoticeKind.AllDecoysGone, null);
            Commit();
        }
        Debug.Log($"[EscapeAuthority] Witch timer started at {point}.");
    }

    // ---------------- rainbow chest & hat (§25.5 H) ----------------

    private bool RainbowOpen(int actor)
    {
        if (State.Rainbow != RainbowState.Closed || !IsActiveEscaper(actor) || State.FlightIndex(actor) >= 0) return false;
        if (!IsNear(actor, manager.RainbowPosition(State.RainbowAnchor), InteractReach) || catalog.HeliHat == null) return false;
        State.Rainbow = RainbowState.Opened;
        var hat = NewItem(catalog.HeliHat);
        hat.Loc = ItemLocation.Ground;
        hat.Pos = manager.RainbowPosition(State.RainbowAnchor);
        State.Items.Add(hat);
        Give(actor, State.Items.Count - 1, hat.Pos);
        return true;
    }

    private bool RainbowBreak(int actor)
    {
        if (State.Rainbow != RainbowState.Closed || !RoomState.IsMonster(actor)) return false;
        if (!IsNear(actor, manager.RainbowPosition(State.RainbowAnchor), InteractReach)) return false;
        State.Rainbow = RainbowState.Broken;
        Notice(EscapeNoticeKind.RainbowBroken, null);
        return true;
    }

    private bool HatFly(int actor, int invSlot)
    {
        if (!IsActiveEscaper(actor) || State.FlightIndex(actor) >= 0) return false;
        int item = State.HeldItemAt(actor, invSlot);
        ItemSO def = item >= 0 ? catalog.Find(State.Items[item].Id) : null;
        if (def == null || !def.IsTool || def.Tool.Kind != ToolKind.HeliHat) return false;
        UseUpTool(item);
        if (lastPositions.TryGetValue(actor, out Vector3 takeoff)) takeoffPositions[actor] = takeoff;
        State.Flights.Add(new EscapeState.Flight { Actor = actor, StartAt = PhotonNetwork.Time });
        return true;
    }

    // 모자로 맵 밖에 나갔다: 떠오르는 시간이 지났고, 방장 화면에서도 그 사람이 맵 경계 밖에 있어야 한다.
    private bool HatEscape(int actor)
    {
        int f = State.FlightIndex(actor);
        if (f < 0 || !IsActiveEscaper(actor)) return false;
        if (PhotonNetwork.Time - State.Flights[f].StartAt < HatRiseSeconds - 1f) return false;
        if (lastPositions.TryGetValue(actor, out Vector3 at) && !manager.IsOutsideMap(at, HatFlight.EscapeMargin * 0.5f)) return false;
        State.Flights.RemoveAt(f);
        State.Escaped.Add(actor);
        // 들고 있던 것은 맵 밖(40 m 위)이 아니라 이륙한 자리에 둔다(§26 A)
        DropAll(actor, takeoffPositions.TryGetValue(actor, out Vector3 takeoff) ? takeoff : manager.FallbackDropPoint(rng.Next()), trusted: true);
        takeoffPositions.Remove(actor);
        StartWitchTimerIfOthersLeft(at);
        return true;
    }

    // ---------------- monster: noise bombs, decoys (§25.5 K·L) ----------------

    private bool PlaceBomb(int actor, Vector3 pos)
    {
        if (!RoomState.IsMonster(actor) || !IsNear(actor, pos, 4f)) return false;
        if (State.BombChargesLeft(actor, BombMaxCharges, PhotonNetwork.Time, BombCooldown) <= 0) return false;
        State.Bombs.Add(new EscapeState.Bomb { Id = State.NextId++, Owner = actor, Pos = manager.SnapToGround(pos) });
        return true;
    }

    private bool RemoveBomb(int actor, int id)
    {
        int i = State.BombIndex(id);
        if (i < 0 || State.Bombs[i].Owner != actor || State.Bombs[i].Spent || !IsNear(actor, State.Bombs[i].Pos, InteractReach)) return false;
        EscapeState.Bomb bomb = State.Bombs[i];
        bomb.Spent = true;
        bomb.SpentAt = PhotonNetwork.Time; // 치워도 같은 쿨타임(§25.3.1 R7)
        State.Bombs[i] = bomb;
        return true;
    }

    // 누군가 이 건물 수리를 시작했다: 근처에 놓인 소음폭탄이 터진다 — 폭탄 둘레·수리하는 사람이 기절, 비상벨, 괴물 화면에 위치 표시.
    private void DetonateBombsAt(int building, int repairer, double now)
    {
        Vector3 point = manager.BuildingRepairPoint(building);
        for (int i = 0; i < State.Bombs.Count; i++)
        {
            EscapeState.Bomb bomb = State.Bombs[i];
            if (bomb.Spent || Flat(bomb.Pos - point) > BombBuildingRadius) continue;
            bomb.Spent = true;
            bomb.SpentAt = now;
            State.Bombs[i] = bomb;
            var targets = new List<int>();
            foreach (IGameCharacter c in CharacterRegistry.All)
            {
                if (c.Role != CharacterRole.Cookie || c.View == null || c.View.Owner == null) continue;
                int owner = c.View.Owner.ActorNumber;
                if (owner == repairer || Vector3.Distance(c.gameObject.transform.position, bomb.Pos) <= BombBlastRadius) targets.Add(c.View.ViewID);
            }
            if (catalog.NoiseBomb != null)
                PhotonNetwork.RaiseEvent(NetEventCodes.ToolHit, new object[] { catalog.NoiseBomb.ItemId, bomb.Owner, targets.ToArray(), bomb.Pos, 0 },
                    new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
            Notice(EscapeNoticeKind.NoiseBomb, building.ToString());
        }
    }

    private bool DecoyCatch(int actor, int id)
    {
        int i = State.DecoyIndex(id);
        if (i < 0 || !RoomState.IsMonster(actor)) return false;
        EscapeState.Decoy d = State.Decoys[i];
        if (d.Caught || !IsNear(actor, d.Pos, InteractReach)) return false;
        // 바로 지우지 않고 잡는 동작(GrabKill) 동안 괴물 손에 들려 있게 한다 — 끝나면 Tick이 지운다(§26 R10)
        d.CaughtBy = actor;
        d.CaughtAt = PhotonNetwork.Time;
        State.Decoys[i] = d;
        return true;
    }

    // ---------------- tools ----------------

    private bool ToolUse(EscapeRequest r)
    {
        int item = State.HeldItemAt(r.Sender, r.A);
        if (item < 0 || !IsActiveEscaper(r.Sender)) return false;
        EscapeState.Item it = State.Items[item];
        ItemSO def = catalog.Find(it.Id);
        if (def == null || !def.IsTool || it.Charges <= 0 || !def.Tool.UsedByClick) return false;
        if (toolReadyAt.TryGetValue(r.Sender, out float ready) && Time.time < ready) return false;

        int[] targets;
        switch (def.Tool.Kind)
        {
            case ToolKind.Skates:
                targets = SelfView(r.Sender);
                break;
            case ToolKind.Pumpkin:
                if (!manager.IsValidTeleport(r.Pos)) return false; // 이동할 곳이 맵 안·술래방 밖·빈 곳이어야 한다(§25.3 Q7)
                moveExemptUntil[r.Sender] = Time.realtimeSinceStartup + 3f;
                targets = SelfView(r.Sender);
                break;
            case ToolKind.Recorder:
                if (!AttachRecorder(r.Sender)) return false; // 내 AI가 없으면 쓸 수 없다(§25.3.2 S2)
                targets = new int[0];
                break;
            default:
                targets = ValidTargets(r.Sender, def.Tool, r.Pos, r.Targets);
                break;
        }

        toolReadyAt[r.Sender] = Time.time + def.Tool.CooldownSeconds;
        it.Charges--;
        if (it.Charges <= 0) it.Loc = ItemLocation.Gone;
        State.Items[item] = it;

        var payload = new object[] { it.Id, r.Sender, targets, r.Pos, r.B };
        PhotonNetwork.RaiseEvent(NetEventCodes.ToolHit, payload, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
        return true;
    }

    // 녹음기: 그 사람의 AI 중 무작위 하나가 목소리를 내며 걷기 시작한다.
    private bool AttachRecorder(int actor)
    {
        Player owner = FindPlayer(actor);
        if (owner == null || !RoomState.TryGetPlayerBool(owner, NetKeys.HasVoice, out bool hasVoice) || !hasVoice) return false; // 녹음하지 않은 사람은 쓸 수 없다(§26 Q11)
        var mine = new List<int>();
        for (int i = 0; i < State.Decoys.Count; i++)
            if (State.Decoys[i].Owner == actor && State.Decoys[i].VoiceAt <= 0 && !State.Decoys[i].Caught) mine.Add(i);
        if (mine.Count == 0) return false;
        int pick = mine[rng.Next(mine.Count)];
        EscapeState.Decoy d = State.Decoys[pick];
        d.VoiceAt = PhotonNetwork.Time;
        d.Seed = rng.Next();
        State.Decoys[pick] = d;
        return true;
    }

    private static int[] SelfView(int actor)
    {
        IGameCharacter c = CharacterRegistry.FindByOwner(actor);
        return c != null && c.View != null ? new[] { c.View.ViewID } : new int[0];
    }

    // 쓴 사람이 고른 명중 대상 중 실제로 닿을 수 있는 것만 남긴다(research.md R4.10-8). 거리는 방장 화면의 위치로 재고,
    // 동기화 지연·몸 크기(괴물 몸 앞면 약 1.9 m)를 여유로 둔다. 물풍선·감옥 풍선은 떨어진 지점이 사거리 안이고 대상이 그 둘레에 있어야 한다.
    private int[] ValidTargets(int sender, ToolSO tool, Vector3 point, int[] targets)
    {
        if (targets == null || targets.Length == 0) return new int[0];
        if (!lastPositions.TryGetValue(sender, out Vector3 from)) return targets; // 위치를 아직 모르면 막지 않는다(입장 직후)

        bool area = tool.Kind == ToolKind.WaterBalloon || tool.Kind == ToolKind.JailBalloon;
        if (area && Flat(point - from) > tool.Range + ToolReachSlack) return new int[0];

        int max = tool.Kind == ToolKind.JailBalloon ? 1 : targets.Length; // 감옥 풍선은 한 명만 가둔다
        var kept = new List<int>(targets.Length);
        foreach (int viewId in targets)
        {
            if (kept.Count >= max) break;
            if (kept.Contains(viewId)) continue;
            PhotonView view = PhotonView.Find(viewId);
            if (view == null) continue;
            Vector3 at = view.transform.position;
            float distance = area ? Vector3.Distance(at, point) : Vector3.Distance(at, from);
            float limit = (area ? tool.HitRadius : tool.Range) + ToolReachSlack;
            if (distance <= limit) kept.Add(viewId);
            else Debug.LogWarning($"[EscapeAuthority] Tool {tool.Kind} from actor {sender}: target view {viewId} is out of reach ({distance:F1} m > {limit:F1} m).");
        }
        return kept.ToArray();
    }

    private const float ToolReachSlack = 3f;

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    private int HeldTool(int actor, ToolKind kind)
    {
        for (int i = 0; i < State.Items.Count; i++)
        {
            EscapeState.Item it = State.Items[i];
            if (it.Loc != ItemLocation.Held || it.A != actor) continue;
            ItemSO def = catalog.Find(it.Id);
            if (def != null && def.IsTool && def.Tool.Kind == kind) return i;
        }
        return -1;
    }

    private void UseUpTool(int item)
    {
        if (item < 0) return;
        EscapeState.Item it = State.Items[item];
        it.Charges = Mathf.Max(0, it.Charges - 1);
        if (it.Charges <= 0) it.Loc = ItemLocation.Gone;
        State.Items[item] = it;
    }

    // ---------------- room events ----------------

    public void OnPlayerLeft(Player player)
    {
        if (State == null || player == null) return;
        Vector3 pos = lastPositions.TryGetValue(player.ActorNumber, out Vector3 p) ? p : manager.FallbackDropPoint(rng.Next()); // 원점 = 술래방이라 쓰지 않는다(§26 B)
        bool changed = DropAll(player.ActorNumber, pos); // 나간 쿠키·스파이의 모든 아이템을 그 자리에(D28)
        changed |= ClearPersonal(player.ActorNumber);
        if (changed) Commit();
        TryLaunchRocket();
    }

    // 잡히면(괴물·마녀) 그 사람의 아이템을 마지막 위치에 떨어뜨리고(D11), 수리·비행을 멈추고, 스파이였으면 모두에게 알린다(D34).
    public void OnPlayerBroken(Player player)
    {
        if (player == null) return;
        if (State != null)
        {
            Vector3 at = lastPositions.TryGetValue(player.ActorNumber, out Vector3 p) ? p : manager.FallbackDropPoint(rng.Next());
            bool changed = DropAll(player.ActorNumber, at);
            changed |= ClearPersonal(player.ActorNumber);
            changed |= RemoveDecoysOf(player.ActorNumber); // 부서진 쿠키의 AI는 모두 사라진다(§26 후속)
            if (changed) Commit();
        }
        // 괴물에게 잡혔으면 모두에게 "'닉네임'이(가) 잡혔습니다!"(마녀 처단은 판 끝 연출이라 알리지 않는다)
        if (RoomState.TryGetPlayerInt(player, NetKeys.DeathCause, out int cause) && cause == (int)DeathCause.Monster)
            Notice(EscapeNoticeKind.CookieCaught, player.ActorNumber.ToString());
        if (RoomState.IsSpy(player.ActorNumber)) Notice(EscapeNoticeKind.SpyCaught, null);
        Reevaluate();
    }

    // 그 사람의 AI 중 아직 잡히지 않은 것을 모두 지운다(잡히는 중인 AI는 동작을 끝까지 보여 준 뒤 Tick이 지운다).
    private bool RemoveDecoysOf(int actor)
    {
        int removed = State.Decoys.RemoveAll(d => d.Owner == actor && !d.Caught);
        if (removed > 0) Debug.Log($"[EscapeAuthority] Actor {actor} broken: {removed} AIs removed.");
        return removed > 0;
    }

    // 그 사람의 수리·비행을 멈춘다(잡힘·나감).
    private bool ClearPersonal(int actor)
    {
        bool changed = false;
        double now = PhotonNetwork.Time;
        for (int b = 0; b < State.Buildings.Count; b++)
            if (State.Buildings[b].Repairer == actor) { StopRepair(b, now); changed = true; }
        int f = State.FlightIndex(actor);
        if (f >= 0) { State.Flights.RemoveAt(f); changed = true; }
        dismantleStarts.Remove(actor);
        takeoffPositions.Remove(actor);
        return changed;
    }

    // 로켓 조건을 다시 본다(알림 없음). 잡힘 처리와 방장 교체 직후에 쓴다.
    public void Reevaluate() => TryLaunchRocket();

    // ---------------- helpers ----------------

    private bool AnyoneStillPlaying()
    {
        foreach (Player p in RoomState.Players)
            if (IsActiveEscaper(p.ActorNumber)) return true;
        return false;
    }

    // 괴물이 아니고, 탈출·탑승하지 않았고, 잡히지 않은 참가자.
    private bool IsActiveEscaper(int actor)
    {
        if (RoomState.IsMonster(actor) || State.HasLeftMap(actor)) return false;
        Player p = FindPlayer(actor);
        return p != null && !RoomState.IsBroken(p);
    }

    // 받을 수 없으면(칸이 가득 참 등) 그 자리 바닥에 놓는다 — 실패는 없다(F-19).
    private void Give(int actor, int item, Vector3 fallbackGround)
    {
        if (!CanPickUp(actor, item))
        {
            PutOnGround(item, fallbackGround);
            return;
        }
        int slot = ChooseSlot(actor);
        EscapeState.Item it = State.Items[item];
        it.Loc = ItemLocation.Held;
        it.A = actor;
        it.B = slot;
        State.Items[item] = it;
    }

    // 두 손 아이템을 든 동안은 줍지 못하고, 칸이 가득 차면 줍지 못한다(§1.9).
    public bool CanPickUp(int actor, int item)
    {
        if (State.HeldCount(actor) >= InventorySlots || HoldsTwoHanded(actor)) return false;
        ItemSO def = catalog.Find(State.Items[item].Id);
        return def != null;
    }

    private bool HoldsTwoHanded(int actor)
    {
        foreach (EscapeState.Item it in State.Items)
        {
            if (it.Loc != ItemLocation.Held || it.A != actor) continue;
            ItemSO def = catalog.Find(it.Id);
            if (def != null && def.LocksSlotSwitch) return true;
        }
        return false;
    }

    // 지금 고른 칸이 비었으면 그 칸, 아니면 첫 빈 칸.
    private int ChooseSlot(int actor)
    {
        Player p = FindPlayer(actor);
        int selected = p != null && RoomState.TryGetPlayerInt(p, NetKeys.SelectedSlot, out int sel) ? sel : 0;
        if (selected >= 0 && selected < InventorySlots && State.HeldItemAt(actor, selected) < 0) return selected;
        for (int i = 0; i < InventorySlots; i++) if (State.HeldItemAt(actor, i) < 0) return i;
        return 0;
    }

    private int FindRocketSlotFor(string itemId)
    {
        ItemSO def = catalog.Find(itemId);
        for (int i = 0; i < State.RocketSlots.Count; i++)
        {
            EscapeState.Slot slot = State.RocketSlots[i];
            if (slot.Filled) continue;
            if (slot.Allows(itemId)) return i; // 수리키트는 RocketInsert에서 따로(50%) 처리한다
        }
        return -1;
    }

    // 로켓에 넣거나 해체로 뺀 재료는 열려 있던 빈 일반 상자 하나에 닫힌 모습으로 다시 생긴다(§1.4, Request1009Plan.md §18.3).
    // 빈 상자가 없으면 건물 옆 바닥에 놓는다. 바닥이면 true.
    private bool RespawnInEmptyChest(int item)
    {
        int chest = PickRespawnChest(State, rng.Next());
        EscapeState.Item it = State.Items[item];
        if (chest < 0)
        {
            it.Loc = ItemLocation.Ground;
            it.Pos = manager.DropPointNear(manager.BuildingRepairPoint(rng.Next(EscapeState.BuildingCount)), rng.Next());
            State.Items[item] = it;
            return true;
        }
        EscapeState.Chest ch = State.Chests[chest];
        ch.Opened = false;
        State.Chests[chest] = ch;
        it.Loc = ItemLocation.Chest;
        it.A = chest;
        State.Items[item] = it;
        return false;
    }

    // 다시 생길 상자(순수 계산): 열려 있고 빈 일반 상자 중 하나 → 없으면 빈 일반 상자(처음부터 비어 닫힌 것) 중 하나 → 없으면 -1(바닥).
    // 스파이 상자는 빼고, roll로 후보 중 하나를 고른다.
    public static int PickRespawnChest(EscapeState state, int roll)
    {
        var opened = new List<int>();
        var empty = new List<int>();
        for (int c = 0; c < state.Chests.Count; c++)
        {
            if (state.Chests[c].Spy || state.ItemInChest(c) >= 0) continue;
            empty.Add(c);
            if (state.Chests[c].Opened) opened.Add(c);
        }
        List<int> pool = opened.Count > 0 ? opened : empty;
        return pool.Count > 0 ? pool[(roll & int.MaxValue) % pool.Count] : -1;
    }

    private void PutOnGround(int item, Vector3 pos)
    {
        EscapeState.Item it = State.Items[item];
        it.Loc = ItemLocation.Ground;
        it.Pos = manager.SafeGroundPoint(pos, rng.Next()); // 술래방 안·맵 밖·닿지 않는 높이면 가까운 건물 옆으로(§26 C)
        State.Items[item] = it;
    }

    private Vector3 ValidDropPosition(int actor, Vector3 requested)
    {
        // 요청 위치가 그 사람에게서 너무 멀면(조작) 그 사람 발밑으로.
        if (lastPositions.TryGetValue(actor, out Vector3 at) && Vector3.Distance(at, requested) > PickUpReach) return at;
        return requested;
    }

    private const float MaxReachHeight = 4f; // 다른 층(지붕 위 등)에서는 닿지 않는다

    private bool IsNear(int actor, Vector3 target, float reach)
    {
        if (!lastPositions.TryGetValue(actor, out Vector3 at)) return true; // 위치를 아직 모르면 막지 않는다(입장 직후)
        Vector3 d = at - target;
        if (Mathf.Abs(d.y) > MaxReachHeight) return false;
        d.y = 0f;
        return d.magnitude <= reach;
    }

    private static Player FindPlayer(int actor)
    {
        if (!RoomState.IsInRoom()) return null;
        return PhotonNetwork.CurrentRoom.GetPlayer(actor);
    }

    private void Notice(EscapeNoticeKind kind, string itemId)
    {
        PhotonNetwork.RaiseEvent(NetEventCodes.EscapeNotice, new object[] { (byte)kind, itemId ?? string.Empty },
            new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    }

    // 스파이가 건물에서 재료를 뺐다는 알림(Request1003bPlan.md §2): 괴물이 아닌 사람에게만, 설정한 초 뒤에 띄우도록 "보여 줄 시각"을 담아
    // 지금 보낸다. 그사이 재료가 다시 건물에 들어갔으면 받은 쪽이 띄우지 않는다.
    private void AnnounceStolen(int item) =>
        SendDelayedNoticeToCookies(new object[] { (byte)EscapeNoticeKind.StolenFromDevice, State.Items[item].Id, item, StealNoticeTime() });

    private void AnnounceRespawn(int item, bool onGround) =>
        SendDelayedNoticeToCookies(new object[] { (byte)EscapeNoticeKind.Respawned, State.Items[item].Id, item, StealNoticeTime(), onGround });

    private static double StealNoticeTime() => PhotonNetwork.Time + GameSettings.Current.StealNoticeDelaySeconds;

    // 괴물이 아닌 사람(쿠키·스파이)에게만 보낸다.
    private static void SendDelayedNoticeToCookies(object[] content)
    {
        var targets = new List<int>();
        foreach (Player p in RoomState.Players) if (!RoomState.IsMonster(p.ActorNumber)) targets.Add(p.ActorNumber);
        if (targets.Count == 0) return;
        // 오프라인 방은 대상 지정(TargetActors)을 무시하고 아무에게도 보내지 않으므로 모두에게(혼자뿐)
        var options = PhotonNetwork.OfflineMode ? new RaiseEventOptions { Receivers = ReceiverGroup.All } : new RaiseEventOptions { TargetActors = targets.ToArray() };
        PhotonNetwork.RaiseEvent(NetEventCodes.EscapeNotice, content, options, SendOptions.SendReliable);
    }

    public void Commit()
    {
        if (State == null || !RoomState.IsInRoom()) return;
        State.Revision++;
        AuthorityProps.SetRoom(new Hashtable { { NetKeys.EscapeState, State.Encode() } });
        manager.ApplyLocalState(State.Clone()); // 방장 화면은 서버 응답을 기다리지 않고 바로 맞춘다
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
