using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 날아가는 투척물 전체(Request1009Plan.md §70.3.3). 모든 화면이 같은 목록을 갖고 같은 계산(BallisticSim)으로 그린다.
// - 던진 사람: 손을 떠나는 순간 예측 투사체를 바로 띄우고 방장에게 EscapeOp.Throw를 보낸다.
// - 방장(NetAuthority): 검증 뒤 ProjectileSpawn을 모두에게 보내고, 스텝마다 진행하며 닿음·시한을 판정해 ToolHit(투사체 번호 포함)으로 끝낸다.
// - 모두: ToolHit이 오면 그 자리로 옮겨 지우고 효과는 ToolHitReceiver.Apply가 그대로 처리한다.
// 진행 중인 목록은 모든 화면이 가지므로 방장이 바뀌어도 새 방장이 이어서 판정한다.
public class ProjectileSystem : MonoBehaviour
{
    public static ProjectileSystem Instance { get; private set; }

    public const float PredictedTimeout = 2f;   // 예측 투사체: 이 안에 Spawn이 안 오면 지운다(거절은 ThrowRejected로 바로 온다 — §71 T4, 느린 연결 대비 1 → 2초)
    public const float FinishTimeout = 1.5f;    // 끝날 때가 지났는데 ToolHit이 안 오면(방장 교체 등) 지운다
    public const float ContactHold = 0.5f;      // 물풍선이 내 화면에서 사람에게 닿았는데 결과가 안 오면 이만큼 기다렸다가 계속 날아간다

    private sealed class Live
    {
        public int Id;            // 방장이 준 번호(예측 중이면 0)
        public int LocalKey;      // 던진 사람이 붙인 번호(예측 투사체 맞추기)
        public ItemSO Item;
        public ProjectileConfigSO Config;
        public int Owner;
        public int Color;
        public Vector3 Origin, Velocity;
        public double T0;
        public ProjectileState State;
        public bool Done;         // 방장: 끝냈다 / 다른 화면: 끝날 자리에서 결과를 기다린다
        public float DoneAt;      // Time.time
        public float HoldUntil;   // 물풍선 접촉 대기
        public float SpawnedAt;   // 예측 투사체 생성 시각(Time.time)
        public Transform View;
        public Transform Thrower;
    }

    private readonly List<Live> live = new List<Live>();
    private readonly List<int> targetBuffer = new List<int>();
    private EscapeManager manager;
    private int nextLocalKey = 1;
    private int nextId = 1;

    public int Count => live.Count;

    public static ProjectileSystem Create(EscapeManager owner)
    {
        var go = new GameObject(nameof(ProjectileSystem));
        go.transform.SetParent(owner.transform, false);
        var s = go.AddComponent<ProjectileSystem>();
        s.manager = owner;
        Instance = s;
        return s;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (Live p in live) if (p.View != null) Destroy(p.View.gameObject);
        live.Clear();
    }

    // ---------------- 던지는 사람 ----------------

    // 손을 떠나는 순간: 예측 투사체를 띄우고 방장에게 요청한다. slot = 인벤토리 칸(술래 수색 폭탄은 -1).
    public void Throw(ItemSO item, int slot, int color, ThrowAim aim, Transform thrower)
    {
        if (item == null || !item.IsTool || item.Tool.Projectile == null || PhotonNetwork.LocalPlayer == null) return;
        int key = nextLocalKey++;
        double t0 = NetAuthority.Time;
        var p = new Live
        {
            LocalKey = key, Item = item, Config = item.Tool.Projectile, Owner = PhotonNetwork.LocalPlayer.ActorNumber, Color = color,
            Origin = aim.Origin, Velocity = aim.Velocity, T0 = t0, State = ProjectileState.Launch(aim.Origin, aim.Velocity),
            SpawnedAt = Time.time, Thrower = thrower,
        };
        p.View = CreateView(p);
        live.Add(p);
        manager.RequestThrow(slot, color, aim.Origin, aim.Velocity, NetAuthority.Stamp, key);
    }

    // ---------------- 방장 ----------------

    // 검증을 마친 던지기(EscapeAuthority.Throw)를 모두에게 알린다.
    public void AuthorizeSpawn(ItemSO item, int owner, Vector3 origin, Vector3 velocity, double t0, int color, int localKey)
    {
        int id = nextId++;
        var content = new object[] { id, item.ItemId, owner, origin, velocity, t0, color, localKey };
        PhotonNetwork.RaiseEvent(NetEventCodes.ProjectileSpawn, content, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    }

    // ---------------- 모든 화면 ----------------

    public void OnSpawn(object data)
    {
        if (!(data is object[] d) || d.Length < 8 || !(d[0] is int id) || !(d[1] is string itemId) || !(d[2] is int owner)
            || !(d[3] is Vector3 origin) || !(d[4] is Vector3 velocity) || !(d[5] is double t0) || !(d[6] is int color) || !(d[7] is int key)) return;
        ItemSO item = manager != null ? manager.Catalog.Find(itemId) : null;
        if (item == null || !item.IsTool || item.Tool.Projectile == null) return;
        if (id >= nextId) nextId = id + 1; // 방장이 바뀌어도 번호가 겹치지 않게
        if (Find(id) != null) return;

        bool mine = PhotonNetwork.LocalPlayer != null && owner == PhotonNetwork.LocalPlayer.ActorNumber;
        Live p = mine ? live.Find(x => x.Id == 0 && x.LocalKey == key) : null;
        if (p == null)
        {
            p = new Live { Item = item, Config = item.Tool.Projectile, Owner = owner, Thrower = ThrowerOf(owner) };
            p.View = CreateView(p);
            live.Add(p);
            if (!mine) PlayThrowPresentation(item, p.Thrower, origin);
        }
        // 방장이 정한 출발(시작점·속도·시각)로 다시 계산한다 — 예측과 거의 같으므로 눈에 띄게 튀지 않는다.
        p.Id = id;
        p.Color = color;
        p.Origin = origin;
        p.Velocity = velocity;
        p.T0 = t0;
        p.State = ProjectileState.Launch(origin, velocity);
        p.Done = false;
        p.HoldUntil = 0f;
    }

    // 방장이 던지기를 받지 않았다(§71 T1, 던진 사람에게만): 예측 투사체를 바로 지우고, 쓴 쿨타임을 돌려준다.
    public static event System.Action<ItemSO, ThrowReject> ThrowRejected;

    public void OnThrowRejected(int localKey, ThrowReject why)
    {
        Live p = live.Find(x => x.Id == 0 && x.LocalKey == localKey);
        if (p == null) return;
        ItemSO item = p.Item;
        Remove(p);
        Debug.Log($"[Projectile] My {item.ItemId} throw was rejected by the master: {why}.");
        if (why != ThrowReject.Cooldown) ScreenToast.Show(EscapeTextsSO.Current.throwRejected, 1.5f); // 결과 알림(§72 A3) — 쿨타임은 돌려주기만
        ThrowRejected?.Invoke(item, why);
    }

    // ToolHit에 투사체 번호가 있으면: 그 자리로 옮겨 지운다. 효과는 ToolHitReceiver가 이어서 처리한다.
    public void OnFinished(int id, Vector3 at)
    {
        Live p = Find(id);
        if (p == null) return;
        Remove(p);
    }

    private void Update()
    {
        double now = NetAuthority.Time;
        bool authority = NetAuthority.IsAuthority;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            Live p = live[i];
            if (p.Id == 0)
            {
                // 예측 투사체: 방장이 받아들이지 않으면 지운다
                if (Time.time - p.SpawnedAt > PredictedTimeout) { Remove(p); continue; }
            }
            else if (p.Done)
            {
                if (Time.time - p.DoneAt > FinishTimeout) Remove(p);
                else if (p.View != null) p.View.position = p.State.Position;
                continue;
            }
            Step(p, now, authority && p.Id != 0);
            if (p.View != null) Draw(p);
        }
    }

    // 출발 뒤 지금까지 고정 간격으로 진행한다. 판정은 방장만.
    private void Step(Live p, double now, bool judge)
    {
        if (Time.time < p.HoldUntil) return;
        float target = (float)(now - p.T0);
        ProjectileWorld.Ignore = p.Thrower;
        int guard = 0;
        while (p.State.Age < target && guard++ < 600)
        {
            bool hit = BallisticSim.Advance(ref p.State, p.Config, ProjectileWorld.Sweep, out Collider touched);
            if (p.Config.burstOnImpact)
            {
                if (!hit) { if (p.State.Age >= p.Config.maxLifeSeconds) { Conclude(p, judge, null); break; } continue; }
                Conclude(p, judge, touched);
                break;
            }
            if (BallisticSim.FuseDone(p.State, p.Config)) { Conclude(p, judge, null); break; }
        }
        ProjectileWorld.Ignore = null;
    }

    // 끝날 때: 방장은 대상을 정해 ToolHit을 보내고, 다른 화면은 결과를 기다린다(물풍선이 사람에게 닿았으면 잠깐 멈춰 기다린다).
    private void Conclude(Live p, bool judge, Collider touched)
    {
        if (!judge)
        {
            if (p.Id == 0 && p.Config.burstOnImpact && ProjectileWorld.CharacterOf(touched) != null)
            {
                p.HoldUntil = Time.time + ContactHold; // 예측 중: 결과를 기다리며 그 자리에
                return;
            }
            p.Done = true;
            p.DoneAt = Time.time;
            return;
        }
        p.Done = true;
        p.DoneAt = Time.time;
        int[] targets = p.Config.burstOnImpact ? DirectTarget(p, touched) : AreaTargets(p);
        Vector3 at = p.State.Position;
        // ToolHit 형식(EscapeAuthority.ToolUse와 같음) 뒤에 투사체 번호를 덧붙인다.
        var payload = new object[] { p.Item.ItemId, p.Owner, targets, at, p.Color, NetAuthority.Time, false, p.Id };
        PhotonNetwork.RaiseEvent(NetEventCodes.ToolHit, payload, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
        Debug.Log($"[Projectile] {p.Item.ItemId} #{p.Id} by actor {p.Owner} ended at {at} after {p.State.Age:F2}s ({p.State.Bounces} bounce(s)): {targets.Length} target(s).");
    }

    // 물풍선: 몸에 직접 닿은 사람 한 명(Q1 A — 벽·바닥에 먼저 닿으면 아무도 안 갇힌다).
    private int[] DirectTarget(Live p, Collider touched)
    {
        IGameCharacter c = ProjectileWorld.CharacterOf(touched);
        return c != null && IsValidTarget(c, p, includeMonsters: p.Item.Tool.CanHitMonster) ? new[] { c.View.ViewID } : new int[0];
    }

    // 섬광탄·수색 폭탄: 터진 자리 둘레 HitRadius. 섬광탄은 술래 포함(던진 사람 제외), 수색 폭탄은 진짜 사람만(AI는 캐릭터가 아니라 빠진다).
    private int[] AreaTargets(Live p)
    {
        targetBuffer.Clear();
        ToolSO tool = p.Item.Tool;
        bool search = tool.Kind == ToolKind.SearchBomb;
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (search ? c.Role == CharacterRole.Monster : !IsValidTarget(c, p, tool.CanHitMonster)) continue;
            if (search && !IsValidTarget(c, p, false)) continue;
            if (Vector3.Distance(c.gameObject.transform.position, p.State.Position) <= tool.HitRadius) targetBuffer.Add(c.View.ViewID);
        }
        return targetBuffer.ToArray();
    }

    private static bool IsValidTarget(IGameCharacter c, Live p, bool includeMonsters)
    {
        if (c == null || !CharacterRegistry.IsAlive(c) || c.View == null || c.View.Owner == null) return false;
        int actor = c.View.Owner.ActorNumber;
        if (actor == p.Owner && p.Item.Tool.Kind != ToolKind.SearchBomb) return false; // 던진 사람 본인은 맞지 않는다
        if (!includeMonsters && c.Role == CharacterRole.Monster) return false;
        if (EscapeManager.HasLeftMap(actor) || EscapeManager.IsVehicleProtected(actor) || RoomState.IsBroken(c.View.Owner)) return false;
        return !(c is HideOrSeekPlayer cookie && (cookie.IsBroken || cookie.IsDodgeInvulnerable)); // 구르는 중 무적(§69 C3)
    }

    // ---------------- 모습 ----------------

    private Transform CreateView(Live p)
    {
        var go = new GameObject("Projectile_" + p.Item.ItemId);
        go.transform.SetParent(transform, false);
        go.transform.position = p.State.Position;
        ProjectileConfigSO c = p.Config;
        GameObject model = c.model != null ? c.model : null;
        if (p.Item.Tool.Kind == ToolKind.WaterBalloon && model == null)
            WaterBubbleView.CreateSmall(go.transform, c.radius * 2.8f);
        else if (model != null || p.Item.ModelPrefab != null)
        {
            GameObject m = Instantiate(model != null ? model : p.Item.ModelPrefab, go.transform, false);
            m.transform.localScale *= c.modelScale;
            foreach (Collider col in m.GetComponentsInChildren<Collider>()) Destroy(col);
        }
        else
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localScale = Vector3.one * c.radius * 2f;
            EscapeVisuals.Tint(sphere, p.Item.Tint, 0.5f);
        }
        return go.transform;
    }

    private static void Draw(Live p)
    {
        p.View.position = p.State.Position;
        if (!p.State.Resting && p.Config.spinSpeed != 0f)
        {
            Vector3 v = p.State.Velocity;
            Vector3 axis = Vector3.Cross(Vector3.up, new Vector3(v.x, 0f, v.z));
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;
            p.View.Rotate(axis.normalized, p.Config.spinSpeed * Time.deltaTime, Space.World);
        }
    }

    private Live Find(int id) => id == 0 ? null : live.Find(x => x.Id == id);

    private void Remove(Live p)
    {
        if (p.View != null) Destroy(p.View.gameObject);
        live.Remove(p);
    }

    private static Transform ThrowerOf(int actor)
    {
        IGameCharacter c = CharacterRegistry.FindByOwner(actor);
        return c != null ? c.gameObject.transform : null;
    }

    // 다른 사람이 던졌다: 그 사람의 던지는 동작과 소리(쓴 사람은 누를 때 이미 들었다).
    private static void PlayThrowPresentation(ItemSO item, Transform thrower, Vector3 origin)
    {
        GameAudio.PlayAt(ToolUser.UseSound(item.Tool.Kind), origin);
        if (thrower != null && thrower.TryGetComponent(out PlayerActionPose pose)) pose.Play(PlayerActionPose.OneShot.Throw);
    }

    // 시험용: 지금 날고 있는 투사체(번호, 위치).
    public IEnumerable<(int id, string item, Vector3 position, bool done)> DebugLive()
    {
        foreach (Live p in live) yield return (p.Id, p.Item.ItemId, p.State.Position, p.Done);
    }
}
