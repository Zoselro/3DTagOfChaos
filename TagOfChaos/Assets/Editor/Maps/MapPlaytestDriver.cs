using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 맵 축소 Play Mode 검증 도구(Plan.md/MapReplacePlan.md v2 §5). 실제 쿠키·괴물 프리팹을 통행 격자 경로(MapPassabilityCheck.FindPath)를 따라
// 걷게 한다. 입력은 레거시 Input Manager라 밖에서 넣을 수 없으므로, 캐릭터 조정자를 잠시 끄고 조정자와 같은 방식(수평 속도 대입,
// 세로 속도는 물리)으로 Rigidbody를 민다. 가는 길의 닫힌 문은 InteractableDoor.Interact로 연다. 멈추거나 떨어지면 실패로 적는다.
public static class MapPlaytestDriver
{
    private const float ReachDistance = 0.6f;
    private const float StuckSeconds = 4f;
    private const float DoorRange = 5f; // 문 틈 가운데(두 문짝 사이)에서 — InteractionPoint는 왼쪽 문짝 가운데라 틈 반대쪽 끝에서는 멀다

    public sealed class Walk
    {
        public string Name;
        public Rigidbody Body;
        public GameObject Character;
        public List<Vector3> Path;
        public float Speed;
        public int Index;
        public float StartTime, LastProgressTime;
        public Vector3 LastProgressPos, Start;
        public int DoorsOpened, Replans;
        public bool Cookie;
        public string Result; // null이면 진행 중
        public readonly List<Behaviour> Disabled = new List<Behaviour>();
        public readonly Dictionary<Behaviour, bool> WasEnabled = new Dictionary<Behaviour, bool>();
        public readonly Dictionary<Collider, PhysicsMaterial> Materials = new Dictionary<Collider, PhysicsMaterial>();
        public float Damping;

        public override string ToString() =>
            $"{Name}: {Result ?? "running"} — waypoint {Index}/{Path?.Count ?? 0}, pos {Body?.position:F1}, " +
            $"moved {(Body != null ? Horizontal(Body.position - Start) : 0f):F1} m, doors opened {DoorsOpened}, replans {Replans}, t {Time.time - StartTime:F1}s";
    }

    private static readonly List<Walk> walks = new List<Walk>();
    // 경로 격자 반 크기 — 캔디숲 실제 맵은 둥근 울타리(80 m) + 탈출구 받침까지(§28 C11)
    public static float GridHalf = MapCompactor.NewHalf;
    public static IReadOnlyList<Walk> Walks => walks;

    public static string Status => string.Join("\n", walks.Select(w => w.ToString()));

    // character를 destination까지 걷게 한다. controllerTypes: 잠시 끌 조정자 컴포넌트 이름(입력으로 속도를 덮어쓰지 않게).
    public static Walk Begin(string name, GameObject character, Vector3 destination, bool cookie, float speed, params string[] controllerTypes)
    {
        var walk = new Walk { Name = name, Character = character, Body = character.GetComponent<Rigidbody>(), Speed = speed, Cookie = cookie };
        walk.Path = MapPassabilityCheck.FindPath(character.transform.position, destination, cookie, GridHalf);
        walk.Start = character.transform.position;
        walk.StartTime = walk.LastProgressTime = Time.time;
        walk.LastProgressPos = walk.Start;
        if (walk.Body == null) walk.Result = "no Rigidbody";
        else if (walk.Path == null) walk.Result = "no path on grid";

        foreach (MonoBehaviour b in character.GetComponents<MonoBehaviour>())
            if (controllerTypes.Contains(b.GetType().Name)) { walk.WasEnabled[b] = b.enabled; b.enabled = false; walk.Disabled.Add(b); }

        // 조정자는 물리 스텝마다 속도를 넣지만 이 드라이버는 에디터 프레임마다 넣는다(창이 뒤에 있으면 초당 몇 번). 그 사이 지면 마찰이
        // 속도를 깎지 않게 검증 동안만 마찰 0 재질을 쓴다(벽·지형 충돌은 그대로). 끝나면 되돌린다.
        if (walk.Body != null)
        {
            var slick = new PhysicsMaterial("PlaytestNoFriction")
            {
                dynamicFriction = 0f, staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            foreach (Collider c in character.GetComponentsInChildren<Collider>())
                if (!c.isTrigger) { walk.Materials[c] = c.sharedMaterial; c.sharedMaterial = slick; }
            walk.Damping = walk.Body.linearDamping;
            walk.Body.linearDamping = 0f;
        }

        if (walk.Result != null) Restore(walk); // 길이 없으면 바로 되돌린다(조정자·마찰)
        walks.Add(walk);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        return walk;
    }

    // 맵 씬을 열고 오프라인 방을 만드는 임시 부트스트랩을 붙인다(DontSave라 씬 파일에 저장되지 않는다). 그다음 Play Mode로 들어간다.
    // 맵 씬을 연다. 오프라인 방은 Play Mode에 들어간 뒤 StartWalks가 만든다(부트스트랩을 실행 중에만 만들어 씬 파일에 남지 않게).
    public static string Prepare(string map)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), UnityEditor.SceneManagement.OpenSceneMode.Single);
        return scene.path;
    }

    private static void EnsureOfflineRoom()
    {
        if (Photon.Pun.PhotonNetwork.InRoom || Object.FindFirstObjectByType<OfflineModeBootstrap>() != null) return;
        var go = new GameObject("PlaytestBootstrap");
        go.SetActive(false);
        var boot = go.AddComponent<OfflineModeBootstrap>();
        typeof(OfflineModeBootstrap).GetField("autoCreateRoom", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(boot, true);
        go.SetActive(true);
    }

    // Play Mode에서: 괴물을 괴물 스폰에 만들고, 괴물은 monsterTo(기본: 쿠키 스폰)로, 쿠키는 cookieTo(기본: 괴물 스폰)로 걷게 한다.
    public static string StartWalks(Vector3? monsterTo = null, Vector3? cookieTo = null)
    {
        EnsureOfflineRoom();
        GameObject cookie = GameObject.Find("HideOrSeekPlayer(Clone)");
        if (cookie == null) return "cookie not spawned yet (offline room is being created, call again)";
        Clear();
        Vector3 monsterSpawn = GameObject.Find(SceneSpawnPoints.Monster).transform.position;
        Vector3 cookieSpawn = GameObject.Find(SceneSpawnPoints.Cookie).transform.position;
        GameObject monster = GameObject.Find("MonsterPlayer(Clone)")
                             ?? Photon.Pun.PhotonNetwork.Instantiate("MonsterPlayer", monsterSpawn + Vector3.up * 0.2f, Quaternion.identity, 0);
        // 잡기 판정은 게임 규칙이라 지형 검증 동안만 끈다(두 캐릭터 길이 엇갈리면 괴물이 쿠키를 잡아 쿠키가 키네마틱이 된다)
        Begin("monster", monster, monsterTo ?? cookieSpawn, false, 6f, "MonsterController", "MonsterGrabKillTrigger");
        Begin("cookie", cookie, cookieTo ?? monsterSpawn, true, 5f, "HideOrSeekPlayer");
        return Status;
    }

    public static void Clear()
    {
        foreach (Walk w in walks) Restore(w);
        walks.Clear();
        EditorApplication.update -= Tick;
    }

    private static void Tick()
    {
        if (!Application.isPlaying)
        {
            foreach (Walk w in walks.Where(w => w.Result == null)) w.Result = "play mode ended";
            EditorApplication.update -= Tick;
            return;
        }

        float killY = KillZoneTop();
        foreach (Walk w in walks)
        {
            if (w.Result != null) continue;
            if (w.Body == null) { w.Result = "character destroyed"; continue; }
            if (w.Body.isKinematic) { Finish(w, "became kinematic (grabbed/carried by game logic)"); continue; }

            // 게임 단계 처리가 조정자를 다시 켤 수 있어 매 틱 끈다(켜져 있으면 입력 0으로 속도를 덮어쓴다)
            foreach (Behaviour b in w.Disabled) if (b != null && b.enabled) b.enabled = false;

            Vector3 pos = w.Body.position;
            // 격자 경로를 한 점씩 따라간다(멀리 있는 점을 향하면 모서리를 질러 열린 문짝 옆 틈에 끼었다)
            while (w.Index < w.Path.Count && Horizontal(w.Path[w.Index] - pos) < ReachDistance) w.Index++;
            if (w.Index >= w.Path.Count) { Finish(w, "arrived"); continue; }

            Vector3 dir = w.Path[w.Index] - pos;
            dir.y = 0f;
            dir.Normalize();

            w.Body.linearVelocity = new Vector3(dir.x * w.Speed, w.Body.linearVelocity.y, dir.z * w.Speed);
            if (dir != Vector3.zero) w.Body.MoveRotation(Quaternion.LookRotation(dir));
            OpenDoorsAhead(w, pos);

            if (Horizontal(pos - w.LastProgressPos) > 1f) { w.LastProgressPos = pos; w.LastProgressTime = Time.time; }
            else if (Time.time - w.LastProgressTime > StuckSeconds)
            {
                // 열린 문짝처럼 계획 뒤에 생긴 장애물이면 지금 자리에서 다시 찾는다(최대 3번)
                List<Vector3> again = w.Replans < 3 ? MapPassabilityCheck.FindPath(pos, w.Path[w.Path.Count - 1], w.Cookie, GridHalf) : null;
                if (again != null && again.Count > 1) { w.Path = again; w.Index = 0; w.Replans++; w.LastProgressTime = Time.time; }
                else Finish(w, $"stuck near {pos:F1}");
            }
            if (pos.y < killY + 1f) Finish(w, $"fell to {pos:F1}");
        }
    }

    // (경로 점은 0.5 m 간격 격자 칸 중심)

    private static void OpenDoorsAhead(Walk w, Vector3 pos)
    {
        var character = w.Character.GetComponent<IGameCharacter>();
        if (character == null) return;
        foreach (InteractableDoor door in Object.FindObjectsByType<InteractableDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (door.State != InteractableDoor.DoorState.Closed) continue;
            if (Horizontal(DoorCenter(door) - pos) > DoorRange) continue;
            if (!door.CanInteract(character)) continue;
            door.Interact(character);
            w.DoorsOpened++;
        }
    }

    private static Vector3 DoorCenter(InteractableDoor door)
    {
        Renderer[] rs = door.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return door.InteractionPoint;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b.center;
    }

    private static void Finish(Walk w, string result)
    {
        w.Result = result;
        if (w.Body != null && !w.Body.isKinematic) w.Body.linearVelocity = new Vector3(0f, w.Body.linearVelocity.y, 0f);
        Restore(w);
    }

    private static void Restore(Walk w)
    {
        foreach (Behaviour b in w.Disabled) if (b != null) b.enabled = !w.WasEnabled.TryGetValue(b, out bool was) || was;
        w.Disabled.Clear();
        w.WasEnabled.Clear();
        foreach (var pair in w.Materials) if (pair.Key != null) pair.Key.sharedMaterial = pair.Value;
        w.Materials.Clear();
        if (w.Body != null) w.Body.linearDamping = w.Damping;
    }

    private static float KillZoneTop()
    {
        var kill = GameObject.Find("VoidKillZone");
        if (kill == null) return -100f;
        var box = kill.GetComponent<BoxCollider>();
        return box != null ? box.bounds.max.y : kill.transform.position.y;
    }

    private static float Horizontal(Vector3 v) => new Vector2(v.x, v.z).magnitude;
}
