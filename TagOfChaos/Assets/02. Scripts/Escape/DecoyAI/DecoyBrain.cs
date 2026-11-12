using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

// 방장만 돌리는 AI(미끼) 판단(Request1009Plan.md §44.4). AI마다 할 일을 정해 경로(DecoyRoute)를 Room Prop "DR_<id>"로 쓴다.
// 배회·쉬기·재료 건물(수리하는 척)·숨기·도망(술래가 추격 BGM 거리 안)·춤(한 행동을 끝낼 때 아주 가끔).
// - 부하: 한 프레임에 몇 마리만 판단(0.3초에 한 바퀴), 길 계산은 프레임당 최대 4번. 술래 거리는 위치만 비교한다.
// - 자리 나눠 쓰기: 건물 방은 3마리, 숨을 곳은 2마리까지.
// - 방장 교체: 새 방장이 "DR_*"를 읽어 각 AI의 지금 경로를 이어받고, 경로가 끝나면 새로 판단한다.
// AI는 실제로 재료를 넣거나 게이지를 채우지 않는다(겉모습만, §44 사용자 결정).
public class DecoyBrain : MonoBehaviour
{
    private const float ThinkCycle = 0.3f;
    private const int PathBudget = 4;
    private const float LocalRange = 45f;
    private const float DoorOpenDistance = 2.5f;
    private const float StartSpread = 3f;      // 사냥 시작 때 한꺼번에 움직이지 않게
    private const float WriteInterval = 0.1f;  // 바뀐 경로를 모아 보내는 간격
    // 새 경로를 이만큼 뒤에 시작하도록 미리 보낸다(§71 A1, Q3) — 모아 보내기 0.1초 + 왕복 지연 여유. 다른 화면이 시작 전에 받아 튀지 않는다.
    public const float RouteLead = 0.25f;
    private const int FleeCandidates = 10;

    private class Agent
    {
        public int Id;
        public DecoyRules.Personality P;
        public DecoyRules.Activity Act;
        public DecoyRoute Route;
        public bool HasRoute;
        public double DecideAt;     // 이 시각에 다음 행동을 고른다
        public double SafeSince = -1;
        public double NextZigzag;
        public int Claim = -1;      // 건물 번호(0~) 또는 숨을 곳 번호(1000~)
        public System.Random Rng;
        public StaminaModel Stamina; // 진짜 쿠키와 같은 규칙(§46.4) — 방장만 계산한다
        public double StaminaAt;     // Stamina를 이 시각까지 진행했다
        // 길이 경로 한 개에 담을 수 있는 점(DecoyRoute.MaxPoints)보다 길어 중간에서 잘렸다 — 그 끝에서 쉬지 않고 같은 목표로 이어 간다(§69 E2).
        // 진저브레드 시계탑 나선 내리막처럼 꺾임이 많은 길에서 잘린 자리에 멈춰 쉬던 문제.
        public bool Continue;
        public Vector3 ContinueTo;
        public DecoyGait ContinueGait;
        public float ContinueRest, ContinueYaw;
    }

    public const float AdoptedStamina = 50f; // 방장이 바뀌면 남은 값을 모르므로 50에서 시작(순간 회복 꼼수 방지, §46.4 ST7)

    private EscapeManager manager;
    private readonly Dictionary<int, Agent> agents = new Dictionary<int, Agent>();
    private readonly List<int> order = new List<int>();
    private readonly List<int> gone = new List<int>();
    private readonly Dictionary<int, int> claims = new Dictionary<int, int>();
    private readonly Hashtable pending = new Hashtable();
    private readonly List<Vector3> monsters = new List<Vector3>();
    private InteractableDoor[] doors = new InteractableDoor[0];
    private NavMeshPath path;
    private int cursor;
    private int pathsThisFrame;
    private float thinkCarry;
    private float nextWrite;
    private float walkSpeed = 6f;
    private bool wasMaster;

    public static DecoyBrain Instance { get; private set; }
    public int AgentCount => agents.Count;

    public void Init(EscapeManager owner)
    {
        manager = owner;
        Instance = this;
        path = new NavMeshPath();
        doors = FindObjectsByType<InteractableDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var prefab = Resources.Load<GameObject>("HideOrSeekPlayer");
        var cookie = prefab != null ? prefab.GetComponent<HideOrSeekPlayer>() : null;
        if (cookie != null && cookie.WalkSpeed > 0.1f) walkSpeed = cookie.WalkSpeed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        bool master = RoomState.IsInRoom() && PhotonNetwork.IsMasterClient;
        if (!master) { wasMaster = false; return; }
        EscapeState s = manager.State;
        if (s == null) { if (agents.Count > 0) { agents.Clear(); claims.Clear(); } return; }
        if (!wasMaster) { wasMaster = true; AdoptRoutes(s); }
        if (!DecoyNavMesh.Ready) return;

        GamePhase phase = GamePhaseState.Current;
        bool active = phase == GamePhase.Prepare || phase == GamePhase.Hunt || phase == GamePhase.TimeAttack;
        double now = PhotonNetwork.Time;
        SyncAgents(s, now);
        if (active)
        {
            CollectMonsters();
            Think(s, now, phase == GamePhase.Prepare);
            OpenDoors(s, now);
        }
        Flush();
    }

    // ---------------- 목록 맞추기 ----------------

    private void SyncAgents(EscapeState s, double now)
    {
        gone.Clear();
        foreach (int id in agents.Keys)
        {
            int i = s.DecoyIndex(id);
            if (i < 0 || s.Decoys[i].Caught) gone.Add(id);
        }
        foreach (int id in gone) Drop(id);
        foreach (EscapeState.Decoy d in s.Decoys)
        {
            if (d.Caught || agents.ContainsKey(d.Id)) continue;
            var a = new Agent
            {
                Id = d.Id,
                P = DecoyRules.FromSeed(d.Id * 7919 + d.Owner),
                Rng = new System.Random(d.Id * 7919 + d.Owner),
            };
            a.DecideAt = now + a.Rng.NextDouble() * StartSpread;
            a.Stamina = new StaminaModel(StaminaSettings(), float.MaxValue);
            a.StaminaAt = now;
            agents[d.Id] = a;
            order.Add(d.Id);
        }
    }

    private static StaminaSettings StaminaSettings() => GameSettings.Current != null ? GameSettings.Current.Stamina : null;

    // 지금 경로대로 [StaminaAt, now] 동안 달린·걸은·쉰 시간만큼 스태미나를 진행한다(경로 순서: 달리기 → 걷기 → 멈춤).
    private static void AdvanceStamina(Agent a, double now)
    {
        if (a.Stamina == null || now <= a.StaminaAt) return;
        double t0 = a.StaminaAt;
        a.StaminaAt = now;
        if (!a.HasRoute || !a.Route.Moves) { a.Stamina.Tick((float)(now - t0), false, false); return; }
        DecoyRoute r = a.Route;
        double runEnd = r.StartAt + r.RunSeconds;
        double moveEnd = r.StartAt + r.TravelSeconds;
        float run = Overlap(t0, now, r.StartAt, runEnd);
        float walk = Overlap(t0, now, System.Math.Max(r.StartAt, runEnd), moveEnd);
        float rest = Mathf.Max(0f, (float)(now - t0) - run - walk);
        if (run > 0f) a.Stamina.Tick(run, true, true);
        if (walk > 0f) a.Stamina.Tick(walk, false, true);
        if (rest > 0f) a.Stamina.Tick(rest, false, false);
    }

    private static float Overlap(double a0, double a1, double b0, double b1) => (float)System.Math.Max(0.0, System.Math.Min(a1, b1) - System.Math.Max(a0, b0));

    // 방장이 된 순간: 남아 있는 경로를 이어받는다(§44.6).
    private void AdoptRoutes(EscapeState s)
    {
        agents.Clear();
        order.Clear();
        claims.Clear();
        double now = PhotonNetwork.Time;
        foreach (EscapeState.Decoy d in s.Decoys)
        {
            if (d.Caught) continue;
            var a = new Agent { Id = d.Id, P = DecoyRules.FromSeed(d.Id * 7919 + d.Owner), Rng = new System.Random(d.Id * 7919 + d.Owner + 17),
                                Stamina = new StaminaModel(StaminaSettings(), AdoptedStamina), StaminaAt = now };
            if (manager.Routes.TryGet(d.Id, out DecoyRoute r))
            {
                a.Route = r;
                a.HasRoute = true;
                a.Act = r.Gait == DecoyGait.Run ? DecoyRules.Activity.Flee : r.Gait == DecoyGait.Dance ? DecoyRules.Activity.Dance : DecoyRules.Activity.Wander;
                a.DecideAt = r.EndsAt;
            }
            else a.DecideAt = now + a.Rng.NextDouble() * StartSpread;
            agents[d.Id] = a;
            order.Add(d.Id);
        }
        if (agents.Count > 0) Debug.Log($"[DecoyBrain] Became master — adopted {manager.Routes.Count} route(s) for {agents.Count} decoy(s).");
    }

    private void Drop(int id)
    {
        if (agents.TryGetValue(id, out Agent a)) Release(a);
        agents.Remove(id);
        order.Remove(id);
        if (manager.Routes.TryGet(id, out _))
        {
            manager.Routes.RemoveLocal(id);
            pending[DecoyRouteBook.KeyOf(id)] = null;
        }
    }

    // ---------------- 판단 ----------------

    private void Think(EscapeState s, double now, bool prepare)
    {
        pathsThisFrame = 0;
        if (order.Count == 0) return;
        thinkCarry += order.Count * Time.deltaTime / ThinkCycle;
        int n = Mathf.Min(order.Count, Mathf.FloorToInt(thinkCarry));
        thinkCarry -= n;
        float audible = DecoyRules.FleeRadius; // 추격음 반경(45 m)이 아니라 예전 30 m(§59 CS2)
        for (int k = 0; k < n; k++)
        {
            if (cursor >= order.Count) cursor = 0;
            Agent a = agents[order[cursor++]];
            int i = s.DecoyIndex(a.Id);
            if (i < 0) continue;
            Vector3 pos = manager.Routes.PositionOf(s.Decoys[i], now);

            if (!prepare && a.Act != DecoyRules.Activity.Dance && monsters.Count > 0)
            {
                Vector3 monster = Nearest(pos);
                if (a.Act == DecoyRules.Activity.Flee)
                {
                    if (DecoyRules.IsSafe(pos, monster, audible)) { if (a.SafeSince < 0) a.SafeSince = now; }
                    else a.SafeSince = -1;
                    if (DecoyRules.CanStopFleeing(a.SafeSince, now))
                    {
                        a.SafeSince = -1;
                        Begin(a, pos, now, a.Rng.NextDouble() < 0.5 ? DecoyRules.Activity.Hide : DecoyRules.Activity.Wander, false);
                        continue;
                    }
                    bool close = (pos - monster).sqrMagnitude < DecoyRules.ZigzagDistance * DecoyRules.ZigzagDistance;
                    if (now >= a.DecideAt || (close && now >= a.NextZigzag)) Flee(a, pos, monster, now);
                    continue;
                }
                if (DecoyRules.ShouldFlee(pos, monster, DecoyRules.FleeStartDistance(audible, a.P)))
                {
                    Flee(a, pos, monster, now);
                    continue;
                }
            }
            if (now < a.DecideAt) continue;
            if (a.Continue)
            {
                a.Continue = false;
                if (TryRoute(a, pos, a.ContinueTo, a.ContinueGait, now, a.ContinueRest, a.ContinueYaw)) continue;
            }
            if (a.Act == DecoyRules.Activity.Flee) a.Act = DecoyRules.Activity.Wander; // 준비 시간·술래 없음
            Next(a, pos, now, prepare);
        }
    }

    // 하던 일을 마쳤다: 아주 가끔 춤, 아니면 가중치로 다음 일.
    private void Next(Agent a, Vector3 pos, double now, bool prepare)
    {
        DanceCatalogSO dances = DanceCatalogSO.Current;
        bool justDanced = a.Act == DecoyRules.Activity.Dance;
        if (!justDanced && a.Act != DecoyRules.Activity.None && dances != null && DecoyRules.RollDance(a.Rng.NextDouble())
            && dances.TryPickForAi(a.Rng.NextDouble(), out byte danceId))
        {
            Release(a);
            a.Act = DecoyRules.Activity.Dance;
            Write(a, new DecoyRoute
            {
                StartAt = now + RouteLead, Gait = DecoyGait.Dance, DanceId = danceId, Speed = 0f,
                HoldUntil = (float)DecoyRules.Range(a.Rng.NextDouble(), 3, 8), FaceYaw = float.NaN, Points = new[] { LeadPoint(a, pos, now) },
            });
            return;
        }
        DecoyRules.Activity next = DecoyRules.PickActivity(a.P, a.Rng.NextDouble(), prepare, manager.BuildingCount > 0, DecoyHideSpot.All.Count > 0);
        Begin(a, pos, now, next, prepare);
    }

    private void Begin(Agent a, Vector3 pos, double now, DecoyRules.Activity act, bool prepare)
    {
        Release(a);
        bool ok = false;
        switch (act)
        {
            case DecoyRules.Activity.Visit: ok = Visit(a, pos, now); break;
            case DecoyRules.Activity.Hide: ok = Hide(a, pos, now); break;
            case DecoyRules.Activity.Rest: ok = Rest(a, pos, now); break;
        }
        if (!ok && act != DecoyRules.Activity.Rest) ok = Wander(a, pos, now);
        if (!ok) Rest(a, pos, now);
    }

    private bool Rest(Agent a, Vector3 pos, double now)
    {
        a.Act = DecoyRules.Activity.Rest;
        Write(a, new DecoyRoute
        {
            StartAt = now + RouteLead, Gait = DecoyGait.Idle, Speed = 0f, HoldUntil = (float)DecoyRules.Range(a.Rng.NextDouble(), 2, 6),
            FaceYaw = (float)(a.Rng.NextDouble() * 360.0), Points = new[] { LeadPoint(a, pos, now) },
        });
        return true;
    }

    private bool Wander(Agent a, Vector3 pos, double now)
    {
        for (int tries = 0; tries < 3; tries++)
        {
            if (!PickMapPoint(a, pos, out Vector3 target)) continue;
            DecoyGait gait = a.Rng.NextDouble() < 0.2 ? DecoyGait.Run : DecoyGait.Walk;
            if (!TryRoute(a, pos, target, gait, now, (float)DecoyRules.Range(a.Rng.NextDouble(), 1, 4), float.NaN)) continue;
            a.Act = DecoyRules.Activity.Wander;
            return true;
        }
        return false;
    }

    // 재료 건물 방 안 수리 자리 옆으로 걸어가 5~12초 수리하는 척(건물 쪽을 보고 선다).
    private bool Visit(Agent a, Vector3 pos, double now)
    {
        int count = manager.BuildingCount;
        int start = a.Rng.Next(count);
        for (int k = 0; k < count; k++)
        {
            int b = (start + k) % count;
            if (Claimed(b) >= DecoyRules.BuildingCapacity) continue;
            Vector3 repair = manager.BuildingRepairPoint(b);
            Vector2 jitter = Random2(a, 1.6f);
            if (!NavMesh.SamplePosition(repair + new Vector3(jitter.x, 0f, jitter.y), out NavMeshHit hit, 2.5f, DecoyNavMesh.Filter)) continue;
            Vector3 face = repair - hit.position;
            float yaw = face.sqrMagnitude > 0.01f ? Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg : float.NaN;
            if (!TryRoute(a, pos, hit.position, DecoyGait.Walk, now, (float)DecoyRules.Range(a.Rng.NextDouble(), 5, 12), yaw)) continue;
            a.Act = DecoyRules.Activity.Visit;
            Claim(a, b);
            return true;
        }
        return false;
    }

    // 가까운 창고·텐트 안 자리로 가서 6~15초 가만히(걷기 20 m/s라 이동이 짧아 오래 숨으면 서 있는 AI가 많아 보였다 — Play Mode 확인).
    private bool Hide(Agent a, Vector3 pos, double now)
    {
        IReadOnlyList<DecoyHideSpot> spots = DecoyHideSpot.All;
        int best = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < spots.Count; i++)
        {
            if (Claimed(1000 + i) >= DecoyHideSpot.Capacity) continue;
            float d = (spots[i].Spot(0) - pos).sqrMagnitude * (0.7f + 0.6f * (float)a.Rng.NextDouble());
            if (d < bestScore) { bestScore = d; best = i; }
        }
        if (best < 0) return false;
        DecoyHideSpot spot = spots[best];
        Vector3 target = spot.Spot(a.Rng.Next(spot.SpotCount));
        DecoyGait gait = a.Act == DecoyRules.Activity.Flee ? DecoyGait.Run : DecoyGait.Walk;
        if (!TryRoute(a, pos, target, gait, now, (float)DecoyRules.Range(a.Rng.NextDouble(), 6, 15), (float)(a.Rng.NextDouble() * 360.0))) return false;
        a.Act = DecoyRules.Activity.Hide;
        Claim(a, 1000 + best);
        return true;
    }

    // 술래 반대쪽 15~25 m를 몇 군데 골라 점수가 가장 좋은 곳으로 뛴다(§44.4). 가까우면 1~2초마다 다시 골라 지그재그.
    private void Flee(Agent a, Vector3 pos, Vector3 monster, double now)
    {
        Release(a);
        Vector3 away = new Vector3(pos.x - monster.x, 0f, pos.z - monster.z);
        if (away.sqrMagnitude < 0.01f) away = Quaternion.Euler(0f, (float)(a.Rng.NextDouble() * 360.0), 0f) * Vector3.forward;
        away.Normalize();
        Vector3 bestPoint = Vector3.zero;
        float bestScore = float.MinValue;
        for (int k = 0; k < FleeCandidates; k++)
        {
            float angle = (float)DecoyRules.Range(a.Rng.NextDouble(), -100, 100);
            float dist = (float)DecoyRules.Range(a.Rng.NextDouble(), 15, 25);
            Vector3 c = pos + Quaternion.Euler(0f, angle, 0f) * away * dist;
            if (manager.IsOutsideMap(c, -3f) || manager.InMonsterRoom(c)) continue;
            if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 4f, DecoyNavMesh.Filter)) continue;
            float score = DecoyRules.FleeScore(pos, monster, hit.position, Openness(hit.position), Crowd(hit.position));
            if (score > bestScore) { bestScore = score; bestPoint = hit.position; }
        }
        a.Act = DecoyRules.Activity.Flee;
        a.NextZigzag = now + DecoyRules.Range(a.Rng.NextDouble(), 1, 2);
        if (bestScore > float.MinValue && TryRoute(a, pos, bestPoint, DecoyGait.Run, now, 0f, float.NaN)) return;
        a.DecideAt = now + 0.5; // 길이 없으면 잠시 뒤 다시
    }

    // ---------------- 길 ----------------

    private bool PickMapPoint(Agent a, Vector3 pos, out Vector3 point)
    {
        point = default;
        Vector3 c;
        if (a.P.Roams)
        {
            if (!manager.TryRandomMapPoint(a.Rng, out c)) return false;
        }
        else
        {
            Vector2 r = Random2(a, LocalRange);
            c = pos + new Vector3(r.x, 0f, r.y);
        }
        if (manager.IsOutsideMap(c, -4f) || manager.InMonsterRoom(c)) return false;
        if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 4f, DecoyNavMesh.Filter)) return false;
        point = hit.position;
        return (point - pos).sqrMagnitude > 16f;
    }

    // 지금 자리에서 목표까지 NavMesh 길을 찾아 경로로 쓴다. 길이 끊겼거나(지붕 위 등) 계산 한도를 넘으면 false.
    private bool TryRoute(Agent a, Vector3 from, Vector3 to, DecoyGait gait, double now, float rest, float faceYaw)
    {
        if (pathsThisFrame >= PathBudget) { a.DecideAt = now + 0.1; return false; }
        pathsThisFrame++;
        from = LeadPoint(a, from, now); // 새 경로는 RouteLead 뒤 그 자리(옛 경로를 따라간 곳)에서 출발한다(§71 A1)
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, 3f, DecoyNavMesh.Filter)) return false;
        if (!NavMesh.CalculatePath(start.position, to, DecoyNavMesh.Filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        Vector3[] corners = path.corners;
        if (corners.Length < 2) return false;
        int n = Mathf.Min(corners.Length, DecoyRoute.MaxPoints);
        bool truncated = corners.Length > DecoyRoute.MaxPoints;
        a.Continue = truncated;
        if (truncated) { a.ContinueTo = to; a.ContinueGait = gait; a.ContinueRest = rest; a.ContinueYaw = faceYaw; rest = 0f; }
        var points = new Vector3[n];
        points[0] = from; // 옛 경로가 RouteLead 뒤 닿는 자리에서 이어서(순간이동 없음)
        for (int i = 1; i < n; i++) points[i] = DecoyRoute.Quantize(corners[i]);
        // 달리기는 지금 스태미나로 달릴 수 있는 거리까지만, 그 뒤는 걷는다. 지쳤으면(0 → 35) 처음부터 걷는다(§46.4).
        AdvanceStamina(a, now);
        if (gait == DecoyGait.Run && a.Stamina != null && !a.Stamina.CanRun) gait = DecoyGait.Walk;
        var route = new DecoyRoute { StartAt = now + RouteLead, Gait = gait, Speed = DecoyRules.SpeedFor(gait, walkSpeed), FaceYaw = faceYaw, Points = points };
        if (gait == DecoyGait.Run && a.Stamina != null)
        {
            float runDistance = a.Stamina.RunSecondsLeft * route.Speed;
            if (runDistance < route.Length) route.RunUntil = Mathf.Max(0.05f, Mathf.HalfToFloat(Mathf.FloatToHalf(runDistance)));
        }
        route.HoldUntil = route.TravelSeconds + rest;
        Write(a, route);
        return true;
    }

    // 새 경로가 시작할 자리: RouteLead 뒤 지금 따라가는 경로가 닿는 곳(경로가 없으면 지금 자리).
    private Vector3 LeadPoint(Agent a, Vector3 pos, double now) =>
        manager.Routes.TryActive(a.Id, now + RouteLead, out DecoyRoute r) ? DecoyRoute.Sample(r, now + RouteLead).Position : pos;

    private void Write(Agent a, DecoyRoute route)
    {
        AdvanceStamina(a, route.StartAt); // 바뀌기 전 경로로 지금까지 진행
        a.Route = route;
        a.HasRoute = true;
        a.DecideAt = route.EndsAt;
        if (a.Continue) a.DecideAt -= RouteLead; // 잘린 경로(§69 E2): 끝나기 RouteLead 전에 다음 경로를 정해 끝나는 순간 이어지게
        manager.Routes.SetLocal(a.Id, route);
        pending[DecoyRouteBook.KeyOf(a.Id)] = DecoyRoute.Encode(route);
    }

    private void Flush()
    {
        if (pending.Count == 0 || Time.unscaledTime < nextWrite) return;
        nextWrite = Time.unscaledTime + WriteInterval;
        var send = new Hashtable();
        foreach (System.Collections.DictionaryEntry e in pending) send[e.Key] = e.Value;
        pending.Clear();
        AuthorityProps.SetRoom(send);
    }

    // 지나가는 AI 2.5 m 안의 닫힌 문을 방장이 연다(사람이 여는 것과 같은 DoorStates 경로, §44.4).
    private void OpenDoors(EscapeState s, double now)
    {
        if (doors.Length == 0 || Time.frameCount % 6 != 0) return;
        foreach (EscapeState.Decoy d in s.Decoys)
        {
            if (d.Caught || !manager.Routes.TryGet(d.Id, out DecoyRoute r) || !r.Moves || now > r.StartAt + r.TravelSeconds) continue;
            Vector3 p = DecoyRoute.Sample(r, now).Position;
            foreach (InteractableDoor door in doors)
            {
                if (door == null || door.State != InteractableDoor.DoorState.Closed) continue;
                if ((door.InteractionPoint - p).sqrMagnitude < DoorOpenDistance * DoorOpenDistance) door.OpenByAuthority(p);
            }
        }
    }

    // ---------------- 도움 ----------------

    private void CollectMonsters()
    {
        monsters.Clear();
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is MonsterController m && CharacterRegistry.IsAlive(c) && !manager.InMonsterRoom(m.transform.position)) monsters.Add(m.transform.position);
#if UNITY_EDITOR
        if (EditorThreat.HasValue) monsters.Add(EditorThreat.Value);
#endif
    }

    private Vector3 Nearest(Vector3 p)
    {
        Vector3 best = monsters[0];
        float bestD = (best - p).sqrMagnitude;
        for (int i = 1; i < monsters.Count; i++)
        {
            float d = (monsters[i] - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = monsters[i]; }
        }
        return best;
    }

    // 8방향으로 6 m 트인 정도(0~1) — 막다른 곳일수록 낮다.
    private static float Openness(Vector3 p)
    {
        int open = 0;
        for (int k = 0; k < 8; k++)
        {
            Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
            if (!NavMesh.Raycast(p, p + dir * 6f, out _, DecoyNavMesh.Filter)) open++;
        }
        return open / 8f;
    }

    private int Crowd(Vector3 p)
    {
        int n = 0;
        foreach (Agent a in agents.Values)
            if (a.HasRoute && a.Route.IsValid && (a.Route.Points[a.Route.Points.Length - 1] - p).sqrMagnitude < 36f) n++;
        return n;
    }

    private Vector2 Random2(Agent a, float radius)
    {
        double angle = a.Rng.NextDouble() * System.Math.PI * 2.0;
        double r = radius * System.Math.Sqrt(a.Rng.NextDouble());
        return new Vector2((float)(System.Math.Cos(angle) * r), (float)(System.Math.Sin(angle) * r));
    }

    private int Claimed(int key) => claims.TryGetValue(key, out int n) ? n : 0;

    private void Claim(Agent a, int key)
    {
        a.Claim = key;
        claims[key] = Claimed(key) + 1;
    }

    private void Release(Agent a)
    {
        if (a.Claim < 0) return;
        int n = Claimed(a.Claim) - 1;
        if (n <= 0) claims.Remove(a.Claim); else claims[a.Claim] = n;
        a.Claim = -1;
    }

#if UNITY_EDITOR
    // 시험용: 혼자 하는 오프라인 Play Mode에서 술래가 이 자리에 있는 것처럼(도망 확인).
    public static Vector3? EditorThreat;

    // 시험용: 지금 행동 이름.
    public string EditorActivityOf(int id) => agents.TryGetValue(id, out Agent a) ? a.Act.ToString() : "-";
#endif
}
