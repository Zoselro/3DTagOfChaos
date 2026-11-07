using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;

// 거대 괴물 두뇌(방장 전용, Request1009Plan.md §64.3·64.4). EscapeManager.Start가 붙인다.
// - 대상: 가장 가까운 산 사람(쿠키·스파이·술래). 한 번 정하면 TargetHoldSeconds 동안 유지하고, 다른 사람이 절반 거리보다 가까울 때만 바꾼다.
// - 길: 거대 괴물 NavMesh(GiantNavMesh — 건물은 돌아가고 소품은 통과). 대상 둘레의 거대 괴물 길 위 점(목표점)까지 걷는다.
//   대상이 건물 안이면 목표점 = 대상에게 가장 가까운 문 앞(쿠키 길로 잰다) → 문 쪽을 보고 기다린다(사용자 결정 A).
//   목표점까지 걸어갈 길이 없으면(다른 층·떨어진 구역) 마법진으로 가라앉았다가 목표점 둘레에서 솟아오른다(WarpCooldown마다 한 번).
// - 밟기: 대상이 앞발 자리에서 StompTriggerRange 안이면 빠르게 내리찍고, 발이 닿는 순간(StompImpactSeconds) 판정 원 안의 사람을 죽인다.
// 상태는 Room Prop NetKeys.Giant(GiantState) 하나 — 방장이 바뀌면 새 방장이 거기서 이어 받는다.
public class GiantBrain : MonoBehaviour
{
    private const float ThinkSeconds = 0.25f;
    private const float RetargetDistance = 2.5f;   // 목표점이 이만큼 바뀌면 새 길
    private const float TargetHoldSeconds = 4f;
    private const float SwitchRatio = 0.5f;
    private const float WarpCooldown = 8f;
    private const float GoalReach = 10f;           // 대상에서 거대 괴물 길까지 이 거리 안이면 바깥에 있다고 본다
    private const float IndoorGap = 4f;            // 목표점이 대상에서 이만큼 넘게 떨어져 있으면 건물 안
    private const float DoorSearch = 30f;
    private const int SpawnTries = 40, WarpTries = 16;

    private EscapeManager manager;
    private GiantState state;
    private bool hasState;
    private double judgedStompAt = -1;
    private float nextThink;
    private int targetActor = -1;
    private float targetSince;
    private readonly List<IGameCharacter> targets = new List<IGameCharacter>();
    private NavMeshPath path;
    private InteractableDoor[] doors;

    public void Init(EscapeManager owner) => manager = owner;

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || manager == null)
        {
            hasState = false; // 방장이 되면 방 속성에서 다시 읽는다
            return;
        }
        if (!hasState)
        {
            if (!GiantState.TryGetCurrent(out state)) return;
            hasState = true;
        }
        if (!WitchTimeline.TryGetAppearAt(out double appearAt) || RoomState.TryGetInt(NetKeys.GameResult, out _)) return;

        double now = PhotonNetwork.Time;
        double walkStart = WitchTimeline.WalkStart(appearAt);
        if (now >= WitchTimeline.WalkEnd(appearAt)) return; // 마녀가 도착했다 — 거대 괴물은 사라진다(GiantPresenter)

        // 발이 닿는 순간 판정(매 프레임 — 생각 주기에 묶이지 않게)
        if (state.StompAt > 0 && state.StompAt != judgedStompAt && now >= state.StompAt + GiantRules.StompImpactSeconds)
        {
            judgedStompAt = state.StompAt;
            Judge(state.StompCenter);
        }

        if (now < walkStart || Time.time < nextThink) return;
        nextThink = Time.time + ThinkSeconds;
        if (state.WarpAt > 0 && now < state.WarpAt + GiantRules.WarpSeconds) return;
        if (state.StompAt > 0 && now < state.StompAt + GiantRules.StompSeconds + GiantRules.StompCooldown) return;
        Think(now, walkStart);
    }

    private void Think(double now, double walkStart)
    {
        GiantState.Pose pose = state.Sample(now, walkStart, RoomState.TimeAttackSeconds());
        IGameCharacter target = PickTarget(pose.Position);
        if (target == null)
        {
            if (pose.Speed > 0f) Stand(now, pose, pose.Yaw); // 쫓을 사람이 없으면 선다
            return;
        }
        Vector3 t = target.gameObject.transform.position;

        // 밟기: 같은 높이(층)에서 앞발 자리 가까이
        if (GiantRules.ShouldStomp(pose.Position, t))
        {
            float yaw = GiantRules.YawTo(pose.Position, t, pose.Yaw);
            Stand(now, pose, yaw, commit: false);
            state.StompAt = now;
            state.StompCenter = GiantRules.StompCenter(pose.Position, yaw); // 높이 = 거대 괴물이 선 바닥(판정 ±StompHeight)
            Commit();
            return;
        }

        if (!TryGoal(t, out Vector3 goal, out float faceYaw)) return;
        if (!ReachableOnFoot(pose.Position, goal))
        {
            if (state.WarpAt <= 0 || now >= state.WarpAt + GiantRules.WarpSeconds + WarpCooldown) StartWarp(now, pose, goal, t);
            return;
        }
        bool idle = !state.Moves || pose.Speed <= 0f;
        if (idle && GiantRules.FlatDistance(pose.Position, goal) < 1f)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(state.Yaw, faceYaw)) > 10f) Stand(now, pose, faceYaw); // 문 앞: 문 쪽을 본다
            return;
        }
        if (idle || GiantRules.FlatDistance(state.End, goal) > RetargetDistance) Walk(now, pose, goal, faceYaw);
    }

    // 대상 고르기(같은 사람 TargetHoldSeconds 유지, 절반 거리보다 가까운 사람이 생기면 바꿈).
    private IGameCharacter PickTarget(Vector3 from)
    {
        CollectTargets(targets);
        IGameCharacter nearest = null, current = null;
        float nearestD = float.MaxValue, currentD = float.MaxValue;
        foreach (IGameCharacter c in targets)
        {
            Vector3 p = c.gameObject.transform.position;
            float d = GiantRules.FlatDistance(from, p) + (GiantRules.OtherFloor(from.y, p.y) ? 1000f : 0f); // 같은 층 먼저
            if (d < nearestD) { nearestD = d; nearest = c; }
            if (c.View.Owner.ActorNumber == targetActor) { current = c; currentD = d; }
        }
        bool keep = current != null && (Time.time - targetSince < TargetHoldSeconds || nearestD > currentD * SwitchRatio);
        IGameCharacter chosen = keep ? current : nearest;
        if (chosen != null && chosen.View.Owner.ActorNumber != targetActor)
        {
            targetActor = chosen.View.Owner.ActorNumber;
            targetSince = Time.time;
        }
        return chosen;
    }

    // 대상 둘레의 거대 괴물 길 위 목표점. 건물 안이면 대상에게 가장 가까운 문 앞(문 쪽을 바라봄).
    private bool TryGoal(Vector3 target, out Vector3 goal, out float faceYaw)
    {
        faceYaw = state.Yaw;
        if (GiantNavMesh.Sample(target, GoalReach, out goal) && !GiantRules.OtherFloor(goal.y, target.y)
            && GiantRules.FlatDistance(goal, target) <= IndoorGap)
        {
            faceYaw = GiantRules.YawTo(goal, target, state.Yaw);
            return true;
        }
        if (TryDoorFront(target, out goal, out faceYaw)) return true;
        return GiantNavMesh.Sample(target, GoalReach * 2f, out goal) && !GiantRules.OtherFloor(goal.y, target.y);
    }

    private bool TryDoorFront(Vector3 target, out Vector3 front, out float faceYaw)
    {
        front = default;
        faceYaw = state.Yaw;
        if (doors == null) doors = FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None);
        if (path == null) path = new NavMeshPath();
        var cookie = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(target, out NavMeshHit from, 3f, cookie)) return false;
        float best = float.MaxValue;
        foreach (InteractableDoor door in doors)
        {
            if (door == null) continue;
            Vector3 d = door.transform.position;
            if (GiantRules.FlatDistance(d, target) > DoorSearch || GiantRules.OtherFloor(d.y, target.y)) continue;
            if (!GiantNavMesh.Sample(d, 9f, out Vector3 outside) || GiantRules.OtherFloor(outside.y, d.y)) continue;
            if (!NavMesh.SamplePosition(d, out NavMeshHit dh, 3f, cookie)) continue;
            if (!NavMesh.CalculatePath(from.position, dh.position, cookie, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float len = PathLength(path);
            if (len >= best) continue;
            best = len;
            front = outside;
            faceYaw = GiantRules.YawTo(outside, d, state.Yaw);
        }
        return best < float.MaxValue;
    }

    private bool ReachableOnFoot(Vector3 from, Vector3 goal)
    {
        if (!GiantNavMesh.Sample(from, 4f, out Vector3 start)) return false;
        if (path == null) path = new NavMeshPath();
        return NavMesh.CalculatePath(start, goal, GiantNavMesh.Filter, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private void Walk(double now, GiantState.Pose pose, Vector3 goal, float faceYaw)
    {
        // ReachableOnFoot이 방금 계산한 path를 쓴다
        Vector3[] corners = path.corners;
        int n = Mathf.Min(corners.Length, GiantState.MaxPoints);
        var points = new Vector3[Mathf.Max(2, n)];
        points[0] = pose.Position;
        for (int i = 1; i < n; i++) points[i] = corners[i];
        if (n < 2) points[1] = goal;
        state.Points = points;
        state.MoveAt = now;
        state.Yaw = faceYaw;
        Commit();
    }

    private void Stand(double now, GiantState.Pose pose, float yaw, bool commit = true)
    {
        state.Points = new[] { pose.Position };
        state.MoveAt = now;
        state.Yaw = yaw;
        if (commit) Commit();
    }

    // 걸어서 못 가는 목표점으로: 지금 자리에서 가라앉고, 목표점 둘레 빈 거대 괴물 길에서 솟아오른다.
    private void StartWarp(double now, GiantState.Pose pose, Vector3 goal, Vector3 target)
    {
        Vector3 to = WarpPoint(goal, target, (int)(now * 1000));
        float yaw = GiantRules.YawTo(to, target, pose.Yaw);
        state.WarpAt = now;
        state.WarpFrom = pose.Position;
        state.WarpTo = to;
        state.WarpYaw = yaw;
        state.Points = new[] { to };
        state.MoveAt = now + GiantRules.WarpSeconds;
        state.Yaw = yaw;
        Commit();
        Debug.Log($"[Giant] Warp {pose.Position} -> {to} (target {target}).");
    }

    // 목표점 둘레(대상에서 WarpMin~Max m, 같은 층)의 거대 괴물 길. 못 찾으면 목표점.
    private Vector3 WarpPoint(Vector3 goal, Vector3 target, int seed)
    {
        var rng = new System.Random(seed);
        for (int i = 0; i < WarpTries; i++)
        {
            float a = (float)rng.NextDouble() * 360f;
            float r = Mathf.Lerp(GiantRules.WarpMinDistance, GiantRules.WarpMaxDistance, (float)rng.NextDouble());
            Vector3 c = target + Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
            if (!GiantNavMesh.Sample(c, 3f, out Vector3 p) || GiantRules.OtherFloor(target.y, p.y) || manager.IsOutsideMap(p, -3f)) continue;
            if (!ReachableOnFoot(p, goal)) continue; // 내려선 곳에서 목표점까지 걸어갈 수 있어야 한다
            return p;
        }
        return goal;
    }

    // 밟힌 사람: 쿠키·스파이는 부서짐(HitCount 2), 술래는 사망 원인만(DeathCause.Giant — MonsterEscapeState가 숨기고 관전으로).
    private void Judge(Vector3 center)
    {
        CollectTargets(targets);
        int killed = 0;
        foreach (IGameCharacter c in targets)
        {
            Vector3 p = c.gameObject.transform.position;
            if (!GiantRules.InStompRange(center, p)) continue;
            Player owner = c.View.Owner;
            if (c.Role == CharacterRole.Monster)
                AuthorityProps.SetPlayer(owner, new Hashtable { { NetKeys.DeathCause, (int)DeathCause.Giant } });
            else
                AuthorityProps.SetPlayer(owner, new Hashtable { { NetKeys.HitCount, 2 }, { NetKeys.DeathCause, (int)DeathCause.Giant } });
            killed++;
            Debug.Log($"[Giant] Stomped actor {owner.ActorNumber} ({c.Role}) at {GiantRules.FlatDistance(center, p):F1} m from {center}.");
        }
        if (killed == 0) Debug.Log($"[Giant] Stomp at {center} hit nobody.");
    }

    private void Commit()
    {
        state.Seq++;
        AuthorityProps.SetRoom(new Hashtable { { NetKeys.Giant, state.Encode() } });
    }

    private static float PathLength(NavMeshPath p)
    {
        float l = 0f;
        Vector3[] c = p.corners;
        for (int i = 1; i < c.Length; i++) l += Vector3.Distance(c[i - 1], c[i]);
        return l;
    }

    // 쫓을 수 있는 사람: 맵에 남아 있고 살아 있는 쿠키·스파이, 살아 있는 술래.
    public static void CollectTargets(List<IGameCharacter> into)
    {
        into.Clear();
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.View == null || c.View.Owner == null || !c.gameObject.activeInHierarchy) continue;
            Player owner = c.View.Owner;
            if (EscapeManager.HasLeftMap(owner.ActorNumber)) continue;
            if (RoomState.TryGetPlayerInt(owner, NetKeys.DeathCause, out int cause) && cause != (int)DeathCause.None) continue;
            if (c.Role == CharacterRole.Monster)
            {
                if (c is Behaviour b && !b.enabled) continue;
            }
            else if (RoomState.IsBroken(owner)) continue;
            into.Add(c);
        }
    }

    // ---------------- 등장(마녀 타이머 시작 때 방장, EscapeAuthority.StartWitchTimer) ----------------

    // 나올지 정한다: 오프라인 개발 방은 OfflineModeBootstrap 체크박스, 그 밖에는 확률(GiantSettingsSO.SpawnChance).
    public static bool RollSpawn(System.Random rng)
    {
        if (PhotonNetwork.OfflineMode && OfflineModeBootstrap.GiantOverride.HasValue) return OfflineModeBootstrap.GiantOverride.Value;
        return rng.NextDouble() < GiantSettingsSO.Current.SpawnChance;
    }

    // 나오는 자리: 지상(쿠키 스폰 높이) 거대 괴물 길 중 모두에게서 가장 먼 곳. 맵 가운데를 바라본다.
    public static bool TryPickSpawn(EscapeManager manager, System.Random rng, out GiantState s)
    {
        s = default;
        if (manager == null || !GiantNavMesh.Ready) return false;
        GameObject spawn = GameObject.Find(SceneSpawnPoints.Cookie);
        float groundY = spawn != null ? spawn.transform.position.y : 0f;
        var people = new List<IGameCharacter>();
        CollectTargets(people);

        bool found = false;
        Vector3 bestPoint = default;
        float bestScore = -1f;
        for (int i = 0; i < SpawnTries; i++)
        {
            Vector2 xz = manager.Area.RandomPoint(rng, 15f);
            if (!GiantNavMesh.Sample(new Vector3(xz.x, groundY, xz.y), 4f, out Vector3 p)) continue;
            if (Mathf.Abs(p.y - groundY) > 4f || manager.InMonsterRoom(p)) continue;
            float nearest = float.MaxValue;
            foreach (IGameCharacter c in people) nearest = Mathf.Min(nearest, GiantRules.FlatDistance(p, c.gameObject.transform.position));
            float score = Mathf.Min(nearest, GiantRules.SpawnMinDistance * 2f);
            if (score > bestScore) { bestScore = score; bestPoint = p; found = true; }
        }
        if (!found) return false;
        Vector3 center = new Vector3(manager.Area.Center.x, bestPoint.y, manager.Area.Center.y);
        float yaw = GiantRules.YawTo(bestPoint, center, 0f);
        s = new GiantState { Spawn = bestPoint, SpawnYaw = yaw, Points = new[] { bestPoint }, Yaw = yaw };
        return true;
    }
}
