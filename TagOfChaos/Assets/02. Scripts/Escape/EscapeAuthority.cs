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
    public const float ChestShowHeight = 1.1f; // 연 상자 위로 아이템이 떠 있는 높이(§69 B2)
    private const float PickUpReach = 3.5f;       // 바닥 아이템을 줍는 최대 거리(m, 요청 검증)
    private const float InteractReach = 6f;       // 상자·로켓·무지개 상자 요청 검증 거리(m, 여유 포함)
    private const float RepairReach = 5.5f;       // 건물 수리 자리까지(건물 반 크기 7 m 안쪽)
    private const float ExitReach = 7f;           // 탈출구 트리거까지
    private const float DropSpread = 0.6f;
    private const float RepairFinishTolerance = 0.1f; // 누르는 사람 화면과 방장 계산의 차이(요청 왕복 지연)
    private const float HoldTolerance = 0.5f;          // 해체 20초 검사 여유
    public const int DecoysPerPlayer = 10;             // 쿠키·스파이 1인당 맵에 깔리는 AI(§25.5 L)
    public const int BombMaxCharges = 3;               // 사이렌 함정은 세 개까지(§69 F5 — 예전 하나) — 하나씩 치우거나 풀린 뒤 BombCooldown
    public const float BombCooldown = 20f;
    public const float BombBuildingRadius = 12f;       // 폭탄이 이 거리 안 건물에 붙는다(수리 시작 때 터짐)
    public const float BombBlastRadius = 4f;
    public const float SearchCooldown = 3f;            // 술래 수색 폭탄(§68 GP5, §69 F6 — 예전 25초)
    public const float SearchThrowRange = 10f;
    public const float HatRiseSeconds = HatFlight.CruiseHeight / HatFlight.RiseSpeed; // 모자: 40 m까지 떠오르는 시간(§25.3.1 R6, §69 B7 — 속도와 같은 값에서)
    private const float DecoyPlaceAttempts = 40;
    public const float DecoyCatchSeconds = 2.63f;      // 괴물 잡는 동작(GrabKill 클립 길이) 뒤 AI를 지운다(§26 R10)
    private const float MaterialCheckSeconds = 2f;      // D 안전장치 검사 간격(§26 R1)

    private readonly EscapeManager manager;
    private readonly EscapeCatalogSO catalog;
    private readonly EscapeRecipeSO recipe;
    private readonly System.Random rng = new System.Random();
    private readonly Dictionary<int, float> toolReadyAt = new Dictionary<int, float>();
    private readonly GloveCombo gloveCombo = new GloveCombo();                  // 권투 글러브 연타(§68 GP3)
    private readonly Dictionary<int, float> searchReadyAt = new Dictionary<int, float>(); // 수색 폭탄 쿨타임(§68 GP5)
    private readonly Dictionary<int, float> mashAt = new Dictionary<int, float>();       // 사이렌 철창 연타 간격(§68 GP5)
    private readonly Dictionary<int, Vector3> lastPositions = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, HoldStart> dismantleStarts = new Dictionary<int, HoldStart>();
    private readonly Dictionary<int, int> repairSlots = new Dictionary<int, int>(); // 수리 중인 사람이 든 인벤토리 칸
    private readonly Dictionary<int, Vector3> takeoffPositions = new Dictionary<int, Vector3>(); // 모자 이륙 자리(탈출하면 그 자리에 아이템을 둔다, §26 A)
    private float nextMaterialCheck;
    private bool launchRequested; // 로켓 발사는 한 번만 쓴다 — 응답 전에 다시 불려 두 번 쓰이던 문제(F-05)
    private bool witchTimerRequested;
    private readonly Dictionary<int, float> lastPositionAt = new Dictionary<int, float>();
    private readonly Dictionary<int, float> suspectUntil = new Dictionary<int, float>();
    private readonly Dictionary<int, StaminaWatch> staminaWatch = new Dictionary<int, StaminaWatch>(); // §46.3 ST4
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

        // 건물: 재료 4종을 판마다 섞어 4곳에 하나씩(레시피가 고정이면 순서대로), 활성 = 쿠키 수 + 1(최대 4, §25.3 Q3)
        var materials = new List<ItemSO>();
        foreach (ItemSO m in recipe.BuildingMaterials) if (m != null) materials.Add(m);
        if (!recipe.FixedBuildingOrder) Shuffle(materials); // 간판이 재료를 말하는 건물(놀이공원)은 순서 그대로(§36)
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
        // §74 P3: 칸마다 재료 하나로 확정한다 — 예전에는 칸이 레시피의 후보(사탕 숲·진저브레드 4색)를 모두 받아 다른 색이 끼워졌다.
        var takenRocket = new HashSet<string>();
        foreach (EscapeRecipeSO.RocketSlot r in recipe.RocketSlots)
        {
            var options = new List<string>();
            if (r.accepts != null)
                foreach (ItemSO a in r.accepts)
                    if (a != null && !options.Contains(a.ItemId)) options.Add(a.ItemId);
            if (options.Count == 0) continue;
            string pick = EscapeRules.PickRocketMaterial(options, takenRocket, activeIds, rng.Next());
            takenRocket.Add(pick);
            s.RocketSlots.Add(new EscapeState.Slot { Label = string.Empty, Accepts = new[] { pick }, ItemIndex = -1, Anchor = s.RocketSlots.Count });
            if (!activeIds.Contains(pick) && spies > 0) spyExtras.Add(catalog.Find(pick)); // 상자에 없는 로켓 재료 → 스파이 상자에 1개
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
            if (judge && c is HideOrSeekPlayer runner && lastPositions.TryGetValue(actor, out Vector3 before) && lastPositionAt.TryGetValue(actor, out float beforeAt))
                WatchStamina(actor, runner, before, pos, now - beforeAt, exempt);
            lastPositions[actor] = pos;
            lastPositionAt[actor] = now;
        }
    }

    // 방장 추정 검사(§46.3 ST4): 수평 속도로 달렸는지 추정해 같은 스태미나 규칙을 돌리고, 지쳤어야 할 때 계속 달리면 경고 로그만 남긴다.
    private void WatchStamina(int actor, HideOrSeekPlayer cookie, Vector3 from, Vector3 to, float dt, bool exempt)
    {
        if (dt <= 0.05f || dt > 1f) return;
        if (!staminaWatch.TryGetValue(actor, out StaminaWatch watch)) staminaWatch[actor] = watch = new StaminaWatch(GameSettings.Current.Stamina);
        Vector3 d = to - from;
        d.y = 0f;
        float speed = d.magnitude / dt;
        bool skip = exempt || cookie.CurrentCarrierViewId >= 0 || cookie.IsMovementLocked;
        if (watch.Observe(dt, skip ? 0f : speed, cookie.WalkSpeed * cookie.SpeedBoost, Time.realtimeSinceStartup))
            Debug.LogWarning($"[NetGuard] Actor {actor} kept running {watch.OverrunSeconds:F1}s past the stamina limit (speed {speed:F1} m/s).");
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
        {
            EscapeState.Bomb bomb = State.Bombs[i];
            if (bomb.Caged && SirenCage.ShouldRelease(bomb.Mash, bomb.CagedAt, now)) // 사이렌 철창: 시간이 다 되면 저절로 풀림(§68 GP5)
            {
                bomb.Spent = true;
                bomb.SpentAt = now;
                State.Bombs[i] = bomb;
                changed = true;
                continue;
            }
            if (bomb.Spent && now >= bomb.SpentAt + BombCooldown) { State.Bombs.RemoveAt(i); changed = true; }
        }

        // 호박 마차(§68.7): 타지 않고 IdleSeconds가 지났거나, 탄 뒤 도착이 끝났거나, 주인이 더는 뛰지 않으면 지운다
        for (int i = State.Carriages.Count - 1; i >= 0; i--)
        {
            EscapeState.Carriage c = State.Carriages[i];
            bool done = c.Boarded ? now >= c.BoardAt + PumpkinCarriageRules.RemoveAt : now >= c.SummonedAt + PumpkinCarriageRules.IdleSeconds;
            if (done || (!IsActiveEscaper(c.Owner) && !(c.Boarded && now < c.BoardAt + PumpkinCarriageRules.TeleportAt))) { State.Carriages.RemoveAt(i); changed = true; }
        }

        // 콜라 대포(§68.9): 타지 않고 IdleSeconds가 지났거나, 착지가 끝났거나, 주인이 타기 전에 더는 뛰지 않으면 지운다
        for (int i = State.Cannons.Count - 1; i >= 0; i--)
        {
            EscapeState.Cannon c = State.Cannons[i];
            bool done = c.Boarded ? now >= c.BoardAt + ColaCannonRules.LandAt(c.Flight) + ColaCannonRules.LandGrace : now >= c.SummonedAt + ColaCannonRules.IdleSeconds;
            if (done || (!IsActiveEscaper(c.Owner) && !(c.Boarded && now < c.BoardAt + ColaCannonRules.LandAt(c.Flight)))) { State.Cannons.RemoveAt(i); changed = true; }
        }

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
        && now >= WitchTimeline.WalkStart(appearAt);

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
            case EscapeOp.DecoyCatch: changed = DecoyCatch(r.Sender, r.A, r.Pos, r.B); break;
            case EscapeOp.SearchBomb: changed = false; break; // 옛 즉시 수색 폭탄 — 이제 던진다(EscapeOp.Throw, §70.3)
            case EscapeOp.Throw: changed = Throw(r); break;
            case EscapeOp.CageMash: changed = CageMash(r.Sender, r.A); break;
            case EscapeOp.CarriageBoard: changed = CarriageBoard(r.Sender, r.A); break;
            case EscapeOp.CannonBoard: changed = CannonBoard(r.Sender, r.A, r.B); break;
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
        if (item < 0) return true;
        if (c.Spy) { Give(actor, item, manager.ChestFront(c.Anchor)); return true; } // 스파이 상자 재료는 스파이 손에(쿠키가 주워 가지 못하게)
        // 일반 상자: 열린 상자 위에 떠서 무엇인지 보이고, 원하면 E로 줍고 아니면 그대로 둔다 — 누구나 주울 수 있다(§69 B2, 사용자 확정)
        EscapeState.Item shown = State.Items[item];
        shown.Loc = ItemLocation.Ground;
        shown.Pos = manager.ChestPosition(c.Anchor) + Vector3.up * ChestShowHeight;
        State.Items[item] = shown;
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
        // 거대 괴물(§64): 이때 한 번 정한다(오프라인 개발 방은 OfflineModeBootstrap 체크박스, 그 밖에는 30 %). 나오면 시작 연출이 5초 길다.
        var rng = new System.Random(unchecked((int)(now * 1000)));
        GiantState giantState = default;
        bool giant = GiantBrain.RollSpawn(rng) && GiantBrain.TryPickSpawn(manager, rng, out giantState);
        var props = new Hashtable
        {
            { NetKeys.FirstEscapeAt, now },
            { NetKeys.FirstEscapePoint, point },
            { NetKeys.WitchMode, (int)WitchMode.TimeAttack },
            { NetKeys.WitchAppearAt, appearAt },
            { NetKeys.TimeAttackEndTime, WitchTimeline.TimeAttackEnd(appearAt, RoomState.TimeAttackSeconds(), giant) },
        };
        if (giant)
        {
            props[NetKeys.Giant] = giantState.Encode();
            Debug.Log($"[EscapeAuthority] Giant monster will appear at {giantState.Spawn}.");
        }
        AuthorityProps.SetRoom(props);
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
        // 재료 건물 둘레에서만(§69 F5), 이번 판에 쓰는 건물에, 재료가 꽂히지 않은 건물에, 건물당 하나(§74 P12)
        int site = manager.MaterialBuildingAt(pos, BombBuildingRadius);
        EscapeRules.BombPlace verdict = EscapeRules.CanPlaceBomb(State, site);
        if (verdict != EscapeRules.BombPlace.Ok)
        {
            NoticeTo(actor, EscapeNoticeKind.BombRejected, ((byte)verdict).ToString());
            return false;
        }
        State.Bombs.Add(new EscapeState.Bomb { Id = State.NextId++, Owner = actor, Pos = manager.SnapToGround(pos), Building = -1, Site = site });
        return true;
    }

    private bool RemoveBomb(int actor, int id)
    {
        int i = State.BombIndex(id);
        if (i < 0 || State.Bombs[i].Owner != actor || State.Bombs[i].Spent || State.Bombs[i].Caged || !IsNear(actor, State.Bombs[i].Pos, InteractReach)) return false;
        EscapeState.Bomb bomb = State.Bombs[i];
        bomb.Spent = true;
        bomb.SpentAt = PhotonNetwork.Time; // 치워도 같은 쿨타임(§25.3.1 R7)
        State.Bombs[i] = bomb;
        return true;
    }

    // 누군가 이 건물 수리를 시작했다: 근처에 놓인 사이렌 함정이 걸린다(§68 GP5 — 옛 소음폭탄의 기절 대신) — 사이렌·비상벨,
    // 건물 둘레가 빼빼로 철창에 갇히고(안의 사람은 연타로 부숨, SirenCage), 괴물 화면에 위치 표시.
    private void DetonateBombsAt(int building, int repairer, double now)
    {
        Vector3 point = manager.BuildingRepairPoint(building);
        for (int i = 0; i < State.Bombs.Count; i++)
        {
            EscapeState.Bomb bomb = State.Bombs[i];
            if (bomb.Spent || bomb.Caged) continue;
            if (bomb.Site >= 0 ? bomb.Site != building : Flat(bomb.Pos - point) > BombBuildingRadius) continue; // 놓은 건물로(§74 P12)
            bomb.Building = building;
            bomb.CagedAt = now;
            bomb.Mash = 0;
            State.Bombs[i] = bomb;
            Notice(EscapeNoticeKind.NoiseBomb, building.ToString());
            Debug.Log($"[EscapeAuthority] Siren trap {bomb.Id} caged building {building} (repair by actor {repairer}).");
            break; // 한 건물에 철창 하나
        }
    }

    // 사이렌 철창 안의 사람이 점프 키를 눌렀다(§68 GP5): 철창 안(방장 기록 위치)이고 너무 빠르지 않으면 한 번 센다. 다 차면 부서진다.
    private bool CageMash(int actor, int id)
    {
        int i = State.BombIndex(id);
        if (i < 0 || !State.Bombs[i].Caged || RoomState.IsMonster(actor) || !IsActiveEscaper(actor)) return false;
        if (!lastPositions.TryGetValue(actor, out Vector3 at)) return false;
        EscapeState.Bomb bomb = State.Bombs[i];
        if (!SirenCage.Ring(manager.BuildingTransform(bomb.Building), out Vector3 c, out float r) || !SirenCage.Inside(at, c, r + 1f)) return false;
        if (mashAt.TryGetValue(actor, out float last) && Time.time - last < SirenCage.MashMinInterval) return false;
        mashAt[actor] = Time.time;
        bomb.Mash++;
        bool broken = SirenCage.ShouldRelease(bomb.Mash, bomb.CagedAt, PhotonNetwork.Time);
        if (broken) { bomb.Spent = true; bomb.SpentAt = PhotonNetwork.Time; }
        State.Bombs[i] = bomb;
        return broken || bomb.Mash % 3 == 0; // 게이지는 3번마다 보낸다(상태 전송 줄이기)
    }

    // ---------------- 호박 마차(§68.7) ----------------

    // 소환할 수 있는지: 뛰는 중인 쿠키·스파이, 이미 소환한 마차가 없고, 쓴 사람 가까이, 맵 안·술래방 밖·빈 바닥.
    private bool CanSummonCarriage(int actor, Vector3 pos)
    {
        if (!IsActiveEscaper(actor) || RoomState.IsMonster(actor)) return false;
        foreach (EscapeState.Carriage c in State.Carriages) if (c.Owner == actor) return false;
        return IsNear(actor, pos, PumpkinCarriageRules.SummonReach) && manager.IsValidTeleport(pos);
    }

    // 주인이 문 앞에서 E: 문이 열리고 들어가 닫힌 뒤 도착 지점(방장이 정함, 지금 호박마차 규칙 — 맵 안 무작위·걸어 갈 수 있는 곳)으로 간다.
    private bool CarriageBoard(int actor, int id)
    {
        int i = State.CarriageIndex(id);
        if (i < 0) return false;
        EscapeState.Carriage c = State.Carriages[i];
        double now = PhotonNetwork.Time;
        if (c.Owner != actor || c.Boarded || !IsActiveEscaper(actor)) return false;
        if (PumpkinCarriageRules.StageAt(c.SummonedAt, 0, now) != PumpkinCarriageRules.Stage.Waiting) return false;
        if (!IsNear(actor, PumpkinCarriageRules.DoorPoint(c.Pos, c.Yaw), PumpkinCarriageRules.BoardReach + 1f)) return false;
        if (!manager.TryRandomMapPoint(new System.Random(rng.Next()), out Vector3 dest)) return false;
        c.BoardAt = now;
        c.Dest = dest;
        State.Carriages[i] = c;
        moveExemptUntil[actor] = Time.realtimeSinceStartup + PumpkinCarriageRules.TeleportAt + 3f; // 들어감·순간이동은 이동 검사에서 뺀다
        Debug.Log($"[EscapeAuthority] Actor {actor} boarded pumpkin carriage {id} -> {dest}.");
        return true;
    }

    // ---------------- 개발용: 오프라인 아이템 시험(OfflineModeBootstrap) ----------------
    // EscapeManager.OfflineAuthority로만 닿는다(오프라인 방 방장 = 혼자). 온라인 판에서는 쓰이지 않는다.

    // 그 칸에 아이템을 새로 쥐여 준다(칸에 있던 것은 없앤다). 도구면 횟수를 가득, 쿨타임은 초기화.
    public bool DevGive(int actor, string itemId, int slot)
    {
        ItemSO def = catalog.Find(itemId);
        if (State == null || def == null || slot < 0 || slot >= InventorySlots) return false;
        int old = State.HeldItemAt(actor, slot);
        if (old >= 0) { EscapeState.Item gone = State.Items[old]; gone.Loc = ItemLocation.Gone; State.Items[old] = gone; }
        State.Items.Add(new EscapeState.Item { Id = def.ItemId, Loc = ItemLocation.Held, A = actor, B = slot, Charges = def.IsTool ? def.Tool.Charges : 0 });
        toolReadyAt.Remove(actor);
        Commit();
        return true;
    }

    // 들고 있는 도구의 횟수를 가득 채우고 쿨타임을 없앤다.
    public bool DevRefill(int actor)
    {
        if (State == null) return false;
        for (int i = 0; i < State.Items.Count; i++)
        {
            EscapeState.Item it = State.Items[i];
            if (it.Loc != ItemLocation.Held || it.A != actor) continue;
            ItemSO def = catalog.Find(it.Id);
            if (def != null && def.IsTool) { it.Charges = def.Tool.Charges; State.Items[i] = it; }
        }
        toolReadyAt.Remove(actor);
        Commit();
        return true;
    }

    // 들고 있는 것을 모두 없앤다.
    public bool DevClear(int actor)
    {
        if (State == null) return false;
        for (int i = 0; i < State.Items.Count; i++)
        {
            EscapeState.Item it = State.Items[i];
            if (it.Loc != ItemLocation.Held || it.A != actor) continue;
            it.Loc = ItemLocation.Gone;
            State.Items[i] = it;
        }
        Commit();
        return true;
    }

    // ---------------- 콜라 대포(§68.9) ----------------

    private bool CanSummonCannon(int actor, Vector3 pos)
    {
        if (!IsActiveEscaper(actor) || RoomState.IsMonster(actor)) return false;
        foreach (EscapeState.Cannon c in State.Cannons) if (c.Owner == actor) return false;
        return IsNear(actor, pos, ColaCannonRules.SummonReach) && manager.IsValidTeleport(pos);
    }

    // 주인이 뒤 계단에서 E: 그 순간 보던 방향(aimYaw)으로 맵 끝까지 — 끝에서 가장 가까운 내릴 수 있는 곳을 방장이 정한다(도망용 — 맵 밖으로는 안 감).
    private bool CannonBoard(int actor, int id, int aimYaw)
    {
        int i = State.CannonIndex(id);
        if (i < 0) return false;
        EscapeState.Cannon c = State.Cannons[i];
        double now = PhotonNetwork.Time;
        if (c.Owner != actor || c.Boarded || !IsActiveEscaper(actor)) return false;
        if (ColaCannonRules.StageAt(c.SummonedAt, 0, 0f, now) != ColaCannonRules.Stage.Waiting) return false;
        if (!IsNear(actor, ColaCannonRules.StepsPoint(c.Pos, c.Yaw), ColaCannonRules.BoardReach + 1f)) return false;
        if (!TryCannonLanding(c.Pos, aimYaw, out Vector3 dest, out float flight, out float ceiling)) return false;
        c.BoardAt = now;
        c.AimYaw = aimYaw;
        c.Dest = dest;
        c.Flight = flight;
        c.Ceiling = ceiling;
        State.Cannons[i] = c;
        moveExemptUntil[actor] = Time.realtimeSinceStartup + ColaCannonRules.LandAt(flight) + 3f; // 들어감·비행은 이동 검사에서 뺀다
        Debug.Log($"[EscapeAuthority] Actor {actor} fired cola cannon {id} toward {aimYaw}° -> {dest} ({flight:F1}s).");
        return true;
    }

    // 입구에서 보는 방향으로 맵 끝(경계에서 EdgeMargin 안쪽)을 찾고, 그 끝에서부터 RangeStep씩 안쪽으로:
    // 바닥이 있고, 맵 안·술래방 밖·빈 곳이고, 걸어서 이어진 첫 곳.
    // 위가 막힌 곳(진저브레드 지하 동굴)에서는 입구 위 천장을 재어 그 아래에서만 바닥을 찾는다 — 예전에는 40 m 위(= 지상층)에서 찾아
    // 지상 바닥이 잡혀 모두 걸러지고 탑승이 거절됐다(§69 E3).
    private bool TryCannonLanding(Vector3 pos, float aimYaw, out Vector3 dest, out float flight, out float ceiling)
    {
        Vector3 mouth = ColaCannonRules.Mouth(pos, aimYaw, ColaCannonRules.FirePitch);
        Vector3 dir = ColaCannonRules.Rotation(aimYaw) * Vector3.forward;
        ceiling = manager.CeilingAbove(mouth, 80f);
        float edge = ColaCannonRules.MinRange;
        for (float d = ColaCannonRules.MinRange; d <= ColaCannonRules.MaxRange; d += ColaCannonRules.EdgeStep)
        {
            if (manager.IsOutsideMap(mouth + dir * d, -ColaCannonRules.EdgeMargin)) break;
            edge = d;
        }
        for (float d = edge; d >= ColaCannonRules.MinRange; d -= ColaCannonRules.RangeStep)
        {
            Vector3 probe = mouth + dir * d;
            probe.y = ceiling > 0f ? Mathf.Min(mouth.y + 40f, ceiling - 0.6f) : mouth.y + 40f;
            if (!manager.TryGroundBelow(probe, 100f, out Vector3 ground) || ground.y > mouth.y + 20f) continue; // 바닥 없음·너무 높은 곳
            if (!manager.IsValidTeleport(ground) || !manager.IsWalkableFromSpawn(ground)) continue;
            dest = ground;
            flight = ColaCannonRules.FlightSeconds(Flat(ground - mouth));
            return true;
        }
        dest = default;
        flight = 0f;
        return false;
    }

    // 술래 수색 폭탄(§68 GP5): 던진 자리 둘레의 진짜 사람(쿠키·스파이 — AI는 캐릭터가 아니라 빠진다)만 기침한다. 상태는 바꾸지 않는다.
    private bool SearchBomb(int actor, Vector3 pos)
    {
        ItemSO def = catalog.SearchBomb;
        if (def == null || !def.IsTool || !RoomState.IsMonster(actor) || !IsNear(actor, pos, SearchThrowRange + 4f)) return false;
        if (searchReadyAt.TryGetValue(actor, out float ready) && Time.time < ready) return false;
        searchReadyAt[actor] = Time.time + SearchCooldown;
        var targets = new List<int>();
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (c.Role == CharacterRole.Monster || !CharacterRegistry.IsAlive(c) || c.View == null || c.View.Owner == null) continue;
            int owner = c.View.Owner.ActorNumber;
            if (EscapeManager.HasLeftMap(owner) || RoomState.IsBroken(c.View.Owner)) continue;
            if (Vector3.Distance(c.gameObject.transform.position, pos) <= def.Tool.HitRadius) targets.Add(c.View.ViewID);
        }
        PhotonNetwork.RaiseEvent(NetEventCodes.ToolHit, new object[] { def.ItemId, actor, targets.ToArray(), pos, 0, PhotonNetwork.Time },
            new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
        Debug.Log($"[EscapeAuthority] Search bomb by actor {actor} at {pos}: {targets.Count} real player(s) cough.");
        return false;
    }

    // seen·seenStamp: 괴물 화면이 잡은 순간의 AI 위치와 PhotonNetwork.ServerTimestamp(§46.2 GK4, 0이면 옛 클라이언트).
    private bool DecoyCatch(int actor, int id, Vector3 seen, int seenStamp)
    {
        int i = State.DecoyIndex(id);
        bool testSameActor = GrabKillAuthority.DebugJudgeSameActor && !RoomState.IsMonster(actor); // 재현 도구(§70.2.6): 위치 판정만 로그로
        if (i < 0 || (!RoomState.IsMonster(actor) && !testSameActor)) return false;
        EscapeState.Decoy d = State.Decoys[i];
        double now = PhotonNetwork.Time;
        if (testSameActor) { DecoyWithinReach(actor, d, manager.Routes.PoseOf(d, now), seen, seenStamp, now); return false; }
        // 움직이는 AI는 경로 위치로 잰다(모든 화면이 같은 경로로 그린다, Request1009Plan.md §44.6).
        DecoyRoute.Pose pose = manager.Routes.PoseOf(d, now);
        if (d.Caught) return false; // 이미 잡히는 중(같은 요청이 겹침) — 진행 중인 잡기를 끊지 않게 거절 알림도 보내지 않는다
        if (decoyJailedUntil.TryGetValue(id, out double jailed) && now < jailed)
        {
            NoticeTo(actor, EscapeNoticeKind.DecoyCatchRejected, id.ToString()); // 물풍선에 갇힌 AI는 진짜 쿠키처럼 잡지 못한다(§76 A8)
            return false;
        }
        if (!DecoyWithinReach(actor, d, pose, seen, seenStamp, now))
        {
            NoticeTo(actor, EscapeNoticeKind.DecoyCatchRejected, id.ToString()); // 술래 화면이 빈손 잡기 동작을 바로 끊는다(§70.2.5 D5)
            return false;
        }
        // 바로 지우지 않고 잡는 동작(GrabKill) 동안 괴물 손에 들려 있게 한다 — 끝나면 Tick이 지운다(§26 R10)
        d.Pos = pose.Position; // 마지막으로 확정한 자리(방장이 바뀌어도 여기서 시작)
        d.Yaw = pose.Yaw;
        d.CaughtBy = actor;
        d.CaughtAt = now;
        State.Decoys[i] = d;
        return true;
    }

    // 괴물 화면이 본 순간으로 되돌려 잰다(§46.2 GK4): 그 화면이 본 AI 위치가 [본 시각 − 0.5초, 지금] 동안의 경로에서 2 m 안이고,
    // 괴물이 그 위치에 손이 닿으면 잡힌다. 경로가 바뀐 직후에는 직전 경로로도 재고, 서 있는 AI(경로 없음)는 그 자리로 잰다(§70.2.5 D5).
    private bool DecoyWithinReach(int actor, EscapeState.Decoy d, DecoyRoute.Pose pose, Vector3 seen, int seenStamp, double now)
    {
        if (seenStamp == 0) return IsNear(actor, pose.Position, InteractReach); // 옛 클라이언트
        float age = NetAuthority.AgeOf(seenStamp);
        double seenAt = now - Mathf.Max(0f, age);
        double from = seenAt - NetSecurityRules.GrabRewindWindow;
        float error = manager.Routes.TryGet(d.Id, out _) ? manager.Routes.MinDistance(d.Id, seen, from, now) : Vector3.Distance(seen, pose.Position);
        bool reach = MonsterCouldReach(actor, seen, from, now, seenAt, out float monsterGap, out string dashNote);
        bool ok = age <= NetSecurityRules.GrabClaimMaxAge && age >= -0.1f && error <= NetSecurityRules.GrabClaimPathTolerance && reach;
        Debug.Log($"[GrabKill] Decoy {d.Id} {(ok ? "caught" : "rejected")}: path error {error:F1} m, now {Vector3.Distance(seen, pose.Position):F1} m apart, " +
                  $"monster gap {monsterGap:F1} m{dashNote}, age {age * 1000f:F0} ms.");
        return ok;
    }

    // 괴물이 본 AI 자리에 손이 닿았는지: 방장 기록의 괴물 길(마지막 패킷에서 앞으로 늘림)이 그 자리에서 InteractReach 안을 지났는지(§46 후속),
    // 또는 예고된 돌진 길이 그 안을 지났는지(§70.2.5 D2 — 돌진은 위치 패킷으로 따라갈 수 없다).
    private bool MonsterCouldReach(int actor, Vector3 seen, double from, double now, double seenAt, out float gap, out string dashNote)
    {
        gap = -1f;
        dashNote = string.Empty;
        IGameCharacter monster = MonsterOf(actor);
        if (monster == null || monster.View == null) return IsNear(actor, seen, InteractReach);
        bool known = false;
        if (PositionHistory.TryGet(monster.View.ViewID, out PositionTrack track))
        {
            bool remote = PositionHistory.IsRemote(monster.View);
            Vector3 current = remote ? track.NewestPoint : monster.gameObject.transform.position;
            float lead = remote ? NetSecurityRules.PacketLead(seenAt, track.NewestTime) : 0f;
            known = track.TryMinDistanceLeading(seen, from, now, current, lead, out gap);
        }
        if (MonsterDashLedger.TryGet(monster.View.ViewID, seenAt, out DashWindow dash))
        {
            float dashGap = dash.PathError(seen, seenAt, MonsterDashLedger.ClaimTimeSlack);
            dashNote = $" (dash path {dashGap:F1} m)";
            gap = known ? Mathf.Min(gap, dashGap) : dashGap;
            known = true;
        }
        else dashNote = $" ({MonsterDashLedger.Describe(monster.View.ViewID, seenAt)})";
        if (!known) return IsNear(actor, seen, InteractReach);
        return gap <= InteractReach;
    }

    // 그 사람의 술래 캐릭터(없으면 그 사람의 아무 캐릭터).
    private static IGameCharacter MonsterOf(int actor)
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.Role == CharacterRole.Monster && c.View != null && c.View.Owner != null && c.View.Owner.ActorNumber == actor) return c;
        return CharacterRegistry.FindByOwner(actor);
    }

    // ---------------- tools ----------------

    private bool ToolUse(EscapeRequest r)
    {
        int item = State.HeldItemAt(r.Sender, r.A);
        if (item < 0 || !IsActiveEscaper(r.Sender)) return false;
        EscapeState.Item it = State.Items[item];
        ItemSO def = catalog.Find(it.Id);
        if (def == null || !def.IsTool || it.Charges <= 0 || !def.Tool.UsedByClick) return false;
        if (def.Tool.Projectile != null) return false; // 던지는 도구는 EscapeOp.Throw로만(자동 조준 요청은 받지 않는다, §70.3)
        if (toolReadyAt.TryGetValue(r.Sender, out float ready) && Time.time < ready) return false;

        int[] targets;
        switch (def.Tool.Kind)
        {
            case ToolKind.Skates:
                targets = SelfView(r.Sender);
                break;
            case ToolKind.Pumpkin:
                // 호박 마차 소환(§68.7): 쓴 사람 앞 자리(Pos)에 Yaw(B) 방향으로. 순간이동은 E로 탄 뒤(CarriageBoard)
                if (!CanSummonCarriage(r.Sender, r.Pos)) return false;
                State.Carriages.Add(new EscapeState.Carriage { Id = State.NextId++, Owner = r.Sender, Pos = r.Pos, Yaw = r.B, SummonedAt = PhotonNetwork.Time });
                targets = new int[0];
                break;
            case ToolKind.ColaCannon:
                // 콜라 대포 설치(§68.9): 쓴 사람 앞 자리(Pos)에 Yaw(B) 방향으로. 발사는 E로 탄 뒤(CannonBoard)
                if (!CanSummonCannon(r.Sender, r.Pos)) return false;
                State.Cannons.Add(new EscapeState.Cannon { Id = State.NextId++, Owner = r.Sender, Pos = r.Pos, Yaw = r.B, SummonedAt = PhotonNetwork.Time });
                targets = new int[0];
                break;
            case ToolKind.Recorder:
                // 좌클릭으로 고른 내 AI(Targets[0] = AI 번호, §69 B3 — 사용자: 내 AI만)
                if (!AttachRecorder(r.Sender, r.Targets != null && r.Targets.Length > 0 ? r.Targets[0] : -1)) return false;
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

        double hitAt = PhotonNetwork.Time; // 던지는 도구(물풍선·섬광탄)는 위에서 막았다 — 투사체가 닿을 때 따로 알린다(§70.3)
        ApplyDecoyHits(r.Sender, def.Tool, targets); // AI도 맞는다(§76 A3) — 스턴건
        bool knock = def.Tool.Kind == ToolKind.BoxingGlove && targets.Length > 0 && gloveCombo.Register(targets[0], PhotonNetwork.Time); // 3초 안 3대 = 쓰러짐(§68 GP3)
        var payload = new object[] { it.Id, r.Sender, targets, r.Pos, r.B, hitAt, knock };
        PhotonNetwork.RaiseEvent(NetEventCodes.ToolHit, payload, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
        return true;
    }

    // ---------------- 던지기(§70.3) ----------------

    public const float ThrowOriginReach = 4f; // 손(시작점)이 그 사람 기록 위치에서 벗어나도 되는 거리(달리기 + 동기화 여유)
    public const float ThrowMaxAge = 0.5f;    // 손을 떠난 시각이 이보다 오래됐거나 앞서면 지금으로 본다

    // 물풍선·섬광탄(쿠키·스파이 인벤토리 칸) 또는 수색 폭탄(술래, A = -1). 횟수·쿨타임·시작점·속도를 확인하고 투사체를 출발시킨다.
    // 누가 맞는지는 날아가 닿거나 터질 때 방장이 정한다(ProjectileSystem) — 던질 때 고른 대상은 받지 않는다.
    private bool Throw(EscapeRequest r)
    {
        ProjectileSystem projectiles = ProjectileSystem.Instance;
        if (projectiles == null) return false;
        ItemSO def;
        int item = -1;
        if (r.A < 0)
        {
            def = catalog.SearchBomb;
            if (def == null || !def.IsTool || !RoomState.IsMonster(r.Sender)) return RejectThrow(r, ThrowReject.NotAllowed);
            if (searchReadyAt.TryGetValue(r.Sender, out float searchReady) && Time.time < searchReady - CooldownSlack) return RejectThrow(r, ThrowReject.Cooldown);
        }
        else
        {
            item = State.HeldItemAt(r.Sender, r.A);
            if (item < 0) return RejectThrow(r, ThrowReject.NotHolding);
            if (!IsActiveEscaper(r.Sender)) return RejectThrow(r, ThrowReject.NotAllowed);
            def = catalog.Find(State.Items[item].Id);
            if (def == null || !def.IsTool) return RejectThrow(r, ThrowReject.NotHolding);
            if (State.Items[item].Charges <= 0) return RejectThrow(r, ThrowReject.NoCharges);
            if (toolReadyAt.TryGetValue(r.Sender, out float ready) && Time.time < ready - CooldownSlack) return RejectThrow(r, ThrowReject.Cooldown);
        }
        ProjectileConfigSO cfg = def.Tool.Projectile;
        if (cfg == null) return RejectThrow(r, ThrowReject.NotAllowed);
        if (!ThrowOriginOk(r.Sender, r.Pos, r.Stamp)) return RejectThrow(r, ThrowReject.FarFromHand); // 손이 그 사람 근처
        Vector3 velocity = Vector3.ClampMagnitude(r.Dir, ThrowSolver.MaxSpeed(cfg));
        if (velocity.sqrMagnitude < 0.01f) return RejectThrow(r, ThrowReject.BadVelocity);
        float age = NetAuthority.AgeOf(r.Stamp);
        if (r.Stamp == 0 || age < -0.1f || age > ThrowMaxAge) age = 0f;

        if (item >= 0)
        {
            toolReadyAt[r.Sender] = Time.time + def.Tool.CooldownSeconds;
            EscapeState.Item it = State.Items[item];
            it.Charges--;
            if (it.Charges <= 0) it.Loc = ItemLocation.Gone;
            State.Items[item] = it;
        }
        else searchReadyAt[r.Sender] = Time.time + SearchCooldown;

        int localKey = r.Targets != null && r.Targets.Length > 0 ? r.Targets[0] : 0;
        projectiles.AuthorizeSpawn(def, r.Sender, r.Pos, velocity, NetAuthority.Time - Mathf.Max(0f, age), Mathf.Clamp(r.B, 0, 9), localKey);
        return item >= 0; // 횟수가 바뀌었을 때만 상태를 다시 쓴다
    }

    // 쿨타임 판정 여유(§71 T3): 던진 사람 화면과 방장의 시계·프레임 차이로 딱 맞춰 누른 던지기가 거절되지 않게.
    public const float CooldownSlack = 0.25f;

    // 던지기를 받지 않았다(§71 T1): 던진 사람에게만 이유를 알린다 — 그 화면은 예측 투사체를 바로 지우고 쿨타임을 돌려준다.
    private bool RejectThrow(EscapeRequest r, ThrowReject why)
    {
        int key = r.Targets != null && r.Targets.Length > 0 ? r.Targets[0] : 0;
        NoticeTo(r.Sender, EscapeNoticeKind.ThrowRejected, key + "|" + (byte)why);
        Debug.Log($"[Projectile] Throw from actor {r.Sender} rejected: {why}.");
        return false;
    }

    // 손(시작점)이 그 사람 근처인지(§71 T2): 받은 위치 기록(최근 0.5초 + 던진 시각) 또는 예고된 돌진 길에서 수평으로 ThrowOriginReach 안.
    // 예전에는 0.2초마다 잰 방장 화면의 보간된 위치(lastPositions)로만 재서 돌진·엘리베이터 직후 거절됐다. 순간이동 예외(탈것·엘리베이터)는 막지 않는다.
    private bool ThrowOriginOk(int actor, Vector3 origin, int stamp)
    {
        IGameCharacter c = MonsterOf(actor);
        if (c == null || c.View == null) return IsNear(actor, origin, ThrowOriginReach);
        if (moveExemptUntil.TryGetValue(actor, out float until) && Time.realtimeSinceStartup < until) return true;
        double now = NetAuthority.Time;
        double at = now - Mathf.Clamp(NetAuthority.AgeOf(stamp), 0f, ThrowMaxAge);
        Vector3 flat = new Vector3(origin.x, c.gameObject.transform.position.y, origin.z);
        if (PositionHistory.TryGet(c.View.ViewID, out PositionTrack track))
        {
            Vector3 newest = track.NewestPoint;
            Vector3 probe = new Vector3(origin.x, newest.y, origin.z);
            if (track.TryMinDistance(probe, at - 0.5, now, newest, out float off) && off <= ThrowOriginReach) return true;
        }
        if (MonsterDashLedger.TryGet(c.View.ViewID, at, out DashWindow dash) && dash.PathError(origin, at, MonsterDashLedger.ClaimTimeSlack) <= ThrowOriginReach) return true;
        Vector3 d = c.gameObject.transform.position - flat;
        d.y = 0f;
        return d.magnitude <= ThrowOriginReach || IsNear(actor, origin, ThrowOriginReach);
    }

    // 녹음기: 그 사람의 AI 중 무작위 하나가 목소리를 내며 걷기 시작한다.
    public const float RecorderReach = 25f; // 녹음기를 붙일 AI까지 거리(쓴 사람 화면 조준 20 m + 동기화 여유)

    private bool AttachRecorder(int actor, int decoyId)
    {
        Player owner = FindPlayer(actor);
        if (owner == null || !RoomState.TryGetPlayerBool(owner, NetKeys.HasVoice, out bool hasVoice) || !hasVoice) return false; // 녹음하지 않은 사람은 쓸 수 없다(§26 Q11)
        int pick = State.DecoyIndex(decoyId);
        if (pick < 0) return false;
        EscapeState.Decoy target = State.Decoys[pick];
        if (target.Owner != actor || target.VoiceAt > 0 || target.Caught) return false; // 내 AI이고, 아직 녹음기가 없고, 안 잡힘
        if (lastPositions.TryGetValue(actor, out Vector3 at) && Flat(manager.Routes.PoseOf(target, PhotonNetwork.Time).Position - at) > RecorderReach) return false;
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
        if (!tool.CanHitMonster) targets = WithoutMonsters(targets); // 스턴건만 괴물을 맞힌다(§59 AT3 — 조작된 요청도 여기서 막힌다)
        if (!lastPositions.TryGetValue(sender, out Vector3 from)) return targets; // 위치를 아직 모르면 막지 않는다(입장 직후)

        bool area = tool.IsThrown; // 물풍선·섬광탄: 떨어진 자리 둘레
        if (area && Flat(point - from) > tool.Range + ToolReachSlack) return new int[0];

        int max = tool.HitsArea ? targets.Length : area || tool.Kind == ToolKind.Hammer || tool.Kind == ToolKind.BoxingGlove ? 1 : targets.Length; // 물풍선·뿅망치·글러브는 한 명, 섬광탄은 둘레 모두
        var kept = new List<int>(targets.Length);
        foreach (int viewId in targets)
        {
            if (kept.Count >= max) break;
            if (kept.Contains(viewId)) continue;
            if (DecoyHit.IsDecoy(viewId))
            {
                // AI(§76 A3): 도구가 AI를 맞히는 종류이고, 아직 있고 잡히는 중이 아니며, 경로 위치가 사거리 안
                IGameCharacter shooter = CharacterRegistry.FindByOwner(sender); // 쓴 사람의 지금 몸 위치(기록이 늦을 수 있다)
                Vector3 origin = area ? point : shooter != null ? shooter.gameObject.transform.position : from;
                if (!DecoyHit.CanHit(tool.Kind) || !DecoyReachable(DecoyHit.DecoyId(viewId), origin, (area ? tool.HitRadius : tool.Range) + ToolReachSlack)) continue;
                kept.Add(viewId);
                continue;
            }
            PhotonView view = PhotonView.Find(viewId);
            if (view == null) continue;
            if (view.TryGetComponent(out HideOrSeekPlayer dodger) && dodger.IsDodgeInvulnerable) continue; // 구르는 중(§69 C3)
            Vector3 at = view.transform.position;
            float distance = area ? Vector3.Distance(at, point) : Vector3.Distance(at, from);
            float limit = (area ? tool.HitRadius : tool.Range) + ToolReachSlack;
            if (distance <= limit) kept.Add(viewId);
            else Debug.LogWarning($"[EscapeAuthority] Tool {tool.Kind} from actor {sender}: target view {viewId} is out of reach ({distance:F1} m > {limit:F1} m).");
        }
        return kept.ToArray();
    }

    private const float ToolReachSlack = 3f;

    // ---------------- AI도 맞음(§76 A3·A6) ----------------

    private readonly Dictionary<int, double> decoyJailedUntil = new Dictionary<int, double>(); // AI 번호 → 갇힘이 풀리는 시각(PhotonNetwork.Time)

    private bool DecoyReachable(int decoyId, Vector3 center, float limit)
    {
        int i = State.DecoyIndex(decoyId);
        if (i < 0 || State.Decoys[i].Caught) return false;
        Vector3 at = manager.Routes.PositionOf(State.Decoys[i], PhotonNetwork.Time);
        return Vector3.Distance(at + Vector3.up * 0.95f, center) <= limit + 1f || Flat(at - center) <= limit;
    }

    // 방장이 보낼 ToolHit 대상 중 AI(음수 번호)에게: 맞은 동안 제자리 → 끝나면 쓴 사람 반대쪽으로 도망(DecoyBrain.OnHit), 물풍선이면 갇힘 기록.
    // 시간은 진짜 쿠키와 같다(스턴건 2초·물풍선 5초 + 떨어짐·섬광탄 3초).
    public void ApplyDecoyHits(int sender, ToolSO tool, int[] targets)
    {
        if (targets == null || tool == null || !DecoyHit.CanHit(tool.Kind)) return;
        DecoyBrain brain = manager.GetComponent<DecoyBrain>();
        Vector3 from = lastPositions.TryGetValue(sender, out Vector3 p) ? p : Vector3.zero;
        IGameCharacter user = CharacterRegistry.FindByOwner(sender);
        if (user != null) from = user.gameObject.transform.position;
        double now = PhotonNetwork.Time;
        float seconds = DecoyHit.HoldSeconds(tool);
        foreach (int t in targets)
        {
            if (!DecoyHit.IsDecoy(t)) continue;
            int id = DecoyHit.DecoyId(t);
            if (tool.Kind == ToolKind.WaterBalloon) decoyJailedUntil[id] = now + tool.StunSecondsCookie;
            if (brain != null) brain.OnHit(id, from, seconds);
        }
    }

    private static int[] WithoutMonsters(int[] targets)
    {
        var kept = new List<int>(targets.Length);
        foreach (int viewId in targets)
        {
            PhotonView view = PhotonView.Find(viewId);
            IGameCharacter c = view != null ? view.GetComponent<IGameCharacter>() : null;
            if (c == null || c.Role != CharacterRole.Monster) kept.Add(viewId);
        }
        return kept.ToArray();
    }

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
        changed |= RemoveDecoysOf(player.ActorNumber); // 나간 사람(끊김·강제 종료·나가기)의 AI도 지운다(§74 P17 — 잡혔을 때와 같게)
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

    // 방 안에 없는 주인의 AI를 지운다(§74 P17) — 방장이 나가며 그 사람의 AI를 지우지 못했을 때 새 방장이 이어받으며 정리한다.
    public bool RemoveDecoysOfAbsentPlayers()
    {
        if (State == null || !RoomState.IsInRoom()) return false;
        var owners = new HashSet<int>();
        foreach (EscapeState.Decoy d in State.Decoys) owners.Add(d.Owner);
        bool changed = false;
        foreach (int owner in owners)
            if (PhotonNetwork.CurrentRoom.GetPlayer(owner) == null) changed |= RemoveDecoysOf(owner);
        if (changed) Commit();
        return changed;
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

    // §72 E5: 방장이 아닌 화면도 방장이 승인한 던지기·도구 사용(모두에게 오는 ProjectileSpawn·ToolHit)을 보고 쿨타임을 적어 둔다 —
    // 예전에는 방장만 들고 있어, 방장이 바뀌면 쿨타임이 사라져 바로 다시 던질 수 있었다. 방장 자신은 승인할 때 이미 적었다.
    public void NoteApprovedUse(int actor, ItemSO def)
    {
        if (PhotonNetwork.IsMasterClient || def == null || !def.IsTool || actor <= 0) return;
        if (def == catalog.SearchBomb) searchReadyAt[actor] = Time.time + SearchCooldown;
        else toolReadyAt[actor] = Time.time + def.Tool.CooldownSeconds;
    }

    // 한 사람에게만 보내는 알림.
    public static void NoticeTo(int actor, EscapeNoticeKind kind, string text)
    {
        // 오프라인 방은 대상 지정(TargetActors)을 무시하고 아무에게도 보내지 않으므로 모두에게(혼자뿐)
        var options = PhotonNetwork.OfflineMode ? new RaiseEventOptions { Receivers = ReceiverGroup.All } : new RaiseEventOptions { TargetActors = new[] { actor } };
        PhotonNetwork.RaiseEvent(NetEventCodes.EscapeNotice, new object[] { (byte)kind, text ?? string.Empty }, options, SendOptions.SendReliable);
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
