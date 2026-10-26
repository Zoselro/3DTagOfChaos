using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 축소한 맵의 통행 검사(Plan.md/MapReplacePlan.md v2 §5). 1 m 격자, 문은 열린 것으로 본다.
// - 쿠키: 캡슐 r 0.46 h 2.0, 한 칸에 1.8 m까지 오른다(점프).
// - 괴물: 통로 폭 4 m 기준(반경 1.9 m, 높이 3 m), 한 칸에 0.9 m까지 오른다.
// 보고: 쿠키 스폰에서 닿지 않는 바닥(갇힌 주머니), 괴물이 2.5 m 안으로 다가갈 수 없는 쿠키 자리(쿠키 전용), 스폰 연결 여부.
// 몸 크기(Body)를 여러 개 주면 한 번의 격자로 몸마다 따로 검사한다 — 앞으로 만들 캐릭터 크기 범위(Request1009Plan.md §28.10).
public static class MapPassabilityCheck
{
    // 플레이어 캐릭터 몸(캡슐 반경·키·한 번에 오르는 높이)
    public readonly struct Body
    {
        public readonly string Name;
        public readonly float Radius, Height, Rise;
        public Body(string name, float radius, float height, float rise) { Name = name; Radius = radius; Height = height; Rise = rise; }
        public override string ToString() => $"{Name}(r {Radius:F2} h {Height:F1} jump {Rise:F1})";
    }

    public static readonly Body CookieBody = new Body("Cookie", 0.46f, 2f, 1.8f);
    // 새 캐릭터 설계 범위(§28.10): 반경 0.35~0.6 m, 키 2.4 m 이하, 점프 1.8 m 이하 — 네 모서리 + 지금 쿠키
    public static readonly Body[] DesignRange =
    {
        CookieBody,
        new Body("Thin", 0.35f, 1.6f, 1.8f), new Body("ThinTall", 0.35f, 2.4f, 1.8f),
        new Body("Wide", 0.6f, 1.6f, 1.8f), new Body("WideTall", 0.6f, 2.4f, 1.8f),
    };

    public const string ExitZonePrefix = "ExitZone_";

    public sealed class Options
    {
        public float Half = MapCompactor.NewHalf;
        public float Cell = MapPassabilityCheck.Cell;
        public Body[] Bodies = { CookieBody };
        public System.Func<Vector3, bool> CookieForbidden; // 쿠키가 들어갈 수 없는 곳(술래방, S11) — 주머니·쿠키 전용 판정에서도 뺀다
        public System.Func<Vector3, bool> IgnorePocket;    // 주머니로 보지 않는 곳(울타리 밖처럼 놀이 공간이 아닌 바닥)
        public (string name, Vector3 pos)[] Targets;       // 쿠키·괴물이 모두 TargetReach m 안까지 와야 하는 자리(상자·수리 자리·탈출구 …)
        // 괴물 출발점을 더한다 — 술래방 문(폭 3 m)은 실제 괴물 충돌체(반경 0.31 m)는 지나가지만 4 m 통로 기준 격자로는 막힌다
        public Vector3[] MonsterSeeds;
        // 실제 괴물이 지나가는 것을 Play Mode로 확인한 곳 — 격자 괴물(반경 1.8 m 구)이 못 서는 가파른 계단 등. 쿠키 전용 판정에서 뺀다.
        public System.Func<Vector3, bool> MonsterVerified;
        // 괴물 몸 반경(기본 1.8 m = 4 m 통로 규칙). 소품이 많은 방 안처럼 규칙 몸이 못 도는 곳은 더 작은 몸(실제 0.31 m보다 크게)으로 따로 잰다(§42).
        public float MonsterRadius = MapPassabilityCheck.MonsterRadius;
        public string MapImage; // 첫 몸 기준 위에서 본 통행 지도 PNG(초록 = 괴물·쿠키 모두, 노랑 = 괴물 손 닿는 쿠키 자리, 빨강 = 쿠키 전용, 보라 = 주머니)
    }

    public const float TargetReach = 3f;
    private const string LogTag = "[Passability]";
    private const float Cell = 0.5f;   // 4 m 통로(규칙 R3)를 격자가 놓치지 않을 만큼 촘촘하게
    public const float MonsterRadius = 1.8f;
    private const float MonsterHeight = 3f, MonsterRise = 0.9f; // 통로 폭 3.6 m 이상(4 m 규칙 안에서 격자 오차 몫을 뺐다)
    // 괴물은 Rigidbody 속도 이동이고 계단 처리가 없으며 캡슐이 작다(r 0.31 m) — 수직 턱은 이 높이까지만 넘는다. 이어진 경사는 MonsterRise까지.
    private const float MonsterStep = 0.15f;
    private const float MonsterReach = 2.5f;       // 괴물 잡기 거리(잡기 구 1.57 m + 여유)
    private const float MinWalkNormal = 0.7f;
    private const int MaxLayers = 4;
    private const float ReportClusterArea = 3f; // m², 이보다 작은 외딴 칸 무리는 보고만 하지 않는다
    private static int ReportClusterMin => Mathf.CeilToInt(ReportClusterArea / (gridCell * gridCell));

    public sealed class Result
    {
        public string Scene;
        public string BodyName = CookieBody.ToString();
        public int Cells, CookieNodes, CookieReached, MonsterReached;
        public int PocketCells, CookieOnlyCells, SealedCells;
        public bool CookieSpawnOnGraph, MonsterSpawnOnGraph, SpawnsConnected;
        public readonly List<string> Pockets = new List<string>();
        public readonly List<string> CookieOnly = new List<string>();
        public readonly List<string> UnreachedTargets = new List<string>();
        public int Targets;
        public bool Clean => CookieSpawnOnGraph && MonsterSpawnOnGraph && SpawnsConnected && Pockets.Count == 0 && CookieOnly.Count == 0 && UnreachedTargets.Count == 0;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Scene} [{BodyName}]: cells {Cells}, cookie nodes {CookieNodes}, cookie reached {CookieReached}, monster reached {MonsterReached}");
            sb.AppendLine($"  spawns on graph cookie {CookieSpawnOnGraph} monster {MonsterSpawnOnGraph}, connected {SpawnsConnected}");
            sb.AppendLine($"  sealed interiors (covered, nobody can enter) {SealedCells} cells");
            sb.AppendLine($"  pockets (unreached open ground) {PocketCells} cells, clusters >= {ReportClusterMin}: {Pockets.Count}");
            foreach (string p in Pockets) sb.AppendLine("    " + p);
            sb.AppendLine($"  cookie-only (monster cannot come within {MonsterReach} m) {CookieOnlyCells} cells, clusters >= {ReportClusterMin}: {CookieOnly.Count}");
            foreach (string p in CookieOnly) sb.AppendLine("    " + p);
            if (Targets > 0)
            {
                sb.AppendLine($"  targets {Targets}, not reached by both within {TargetReach} m: {UnreachedTargets.Count}");
                foreach (string p in UnreachedTargets) sb.AppendLine("    " + p);
            }
            return sb.ToString();
        }
    }

    [MenuItem("Tools/TagOfChaos/Maps/Check Passability (Active Scene)")]
    private static void CheckActive() => Report(Run(SceneManager.GetActiveScene()));

    [MenuItem("Tools/TagOfChaos/Maps/Check Passability (All Map Scenes)")]
    private static void CheckAll()
    {
        foreach (string map in MapSceneBuilder.MapNames)
        {
            Scene scene = EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);
            Report(Run(scene));
        }
    }

    public static void Report(Result r)
    {
        string text = r.ToString();
        Directory.CreateDirectory("Temp");
        File.WriteAllText($"Temp/Passability_{r.Scene}{(r.BodyName == CookieBody.ToString() ? "" : "_" + r.BodyName.Split('(')[0])}.txt", text);
        if (r.Clean) Debug.Log($"{LogTag} {text}");
        else Debug.LogWarning($"{LogTag} {text}");
    }

    // Ground: 그 칸의 가장 낮은 걷는 면, Covered: 그 위를 다른 물체가 덮고 있음(닫힌 천막·건물 안).
    // Fits: 몸마다(비트) 그 자리에 설 수 있는지. 0번 비트는 FindPath가 쓰는 지금 쿠키.
    private struct Node { public float Y; public int Fits; public bool Monster, Ground, Covered; public bool Cookie => (Fits & 1) != 0; }

    public static Result Run(Scene scene) => Run(scene, MapCompactor.NewHalf, Cell);

    // half·cell을 바꾸면 원본 크기 씬(±172 m)도 비교용으로 검사할 수 있다.
    public static Result Run(Scene scene, float half, float cell) => Run(scene, new Options { Half = half, Cell = cell })[0];

    // 몸마다 결과 하나. 문(InteractableDoor)·탈출구 문(EscapeGate)은 열린 것으로, 술래방 문은 괴물만 지나가는 것으로 본다.
    public static Result[] Run(Scene scene, Options options)
    {
        float half = options.Half;
        int n = Mathf.RoundToInt(half * 2f / options.Cell) + 1;
        gridCell = options.Cell;
        monsterRadius = options.MonsterRadius;
        Body[] bodies = options.Bodies;
        var results = bodies.Select(b => new Result { Scene = scene.name, BodyName = b.ToString(), Cells = n * n }).ToArray();

        var disabled = new List<Collider>();
        void Open(Component root)
        {
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
                if (c.enabled) { c.enabled = false; disabled.Add(c); }
        }
        foreach (InteractableDoor door in Object.FindObjectsByType<InteractableDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) Open(door);
        foreach (MonsterRoomDoor door in Object.FindObjectsByType<MonsterRoomDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) Open(door);
        // 쿠키 구멍은 쿠키에게 열려 있다(괴물 몸 격자는 1.4 m 구멍에 들어가지 못한다)
        foreach (CookieHole hole in Object.FindObjectsByType<CookieHole>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) Open(hole);
        // 탈출구 구역(ExitZone_ 표시 — 위치·크기만): 계단·통로·나가는 마당은 쿠키만 가도 된다(캔디숲 울타리 밖 받침과 같은 기준, §30)
        var exitZones = new List<Bounds>();
        foreach (EscapeGate gate in Object.FindObjectsByType<EscapeGate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            foreach (Transform t in gate.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("Door_")) Open(t);
                else if (t.name.StartsWith(ExitZonePrefix)) exitZones.Add(new Bounds(t.position, t.lossyScale));
            }
        bool InExitZone(Vector3 p) => exitZones.Any(z => p.x >= z.min.x && p.x <= z.max.x && p.z >= z.min.z && p.z <= z.max.z);
        Physics.SyncTransforms();

        try
        {
            var nodes = new Node[n, n][];
            for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                {
                    Vector3 p = CellPos(ix, iz, half);
                    nodes[ix, iz] = FindNodes(p, bodies);
                    if (options.CookieForbidden != null && options.CookieForbidden(p))
                        for (int k = 0; k < nodes[ix, iz].Length; k++) nodes[ix, iz][k].Fits = 0;
                    for (int b = 0; b < bodies.Length; b++) results[b].CookieNodes += nodes[ix, iz].Count(x => (x.Fits & (1 << b)) != 0);
                }

            bool[,][] monster = Flood(nodes, n, half, SceneSpawnPoints.Monster, -1, default, out bool monsterOnGraph, options.MonsterSeeds);
            int reach = Mathf.CeilToInt(MonsterReach / options.Cell);
            for (int b = 0; b < bodies.Length; b++)
            {
                Result result = results[b];
                int bit = 1 << b;
                bool[,][] cookie = Flood(nodes, n, half, SceneSpawnPoints.Cookie, b, bodies[b], out bool cookieOnGraph);
                result.CookieSpawnOnGraph = cookieOnGraph;
                result.MonsterSpawnOnGraph = monsterOnGraph;
                // 술래방이 쿠키 금지면 괴물 스폰 대신 괴물이 닿는 바닥과 쿠키가 닿는 바닥이 이어지는지로 본다
                result.SpawnsConnected = cookieOnGraph && monsterOnGraph &&
                    (options.CookieForbidden != null ? Overlaps(nodes, cookie, monster, n) : MonsterSpawnReachedByCookie(nodes, cookie, n, half));

                var pocket = new bool[n, n];
                var cookieOnly = new bool[n, n];
                for (int ix = 0; ix < n; ix++)
                    for (int iz = 0; iz < n; iz++)
                    {
                        Node[] list = nodes[ix, iz];
                        for (int k = 0; k < list.Length; k++)
                        {
                            if (cookie[ix, iz][k]) result.CookieReached++;
                            if (monster[ix, iz][k]) result.MonsterReached++;
                        }
                        int ground = System.Array.FindIndex(list, x => (x.Fits & bit) != 0 && x.Ground);
                        if (ground >= 0 && !cookie[ix, iz][ground])
                        {
                            if (list[ground].Covered) result.SealedCells++; // 아무도 들어갈 수 없는 닫힌 내부 — 문제 아님
                            else if ((options.IgnorePocket == null || !options.IgnorePocket(CellPos(ix, iz, half))) && !InExitZone(CellPos(ix, iz, half))) { pocket[ix, iz] = true; result.PocketCells++; }
                        }

                        bool verified = options.MonsterVerified != null && options.MonsterVerified(CellPos(ix, iz, half));
                        for (int k = 0; k < list.Length && !verified && !InExitZone(CellPos(ix, iz, half)); k++)
                        {
                            if (!cookie[ix, iz][k]) continue;
                            if (!MonsterNear(nodes, monster, n, ix, iz, list[k].Y, reach)) { cookieOnly[ix, iz] = true; result.CookieOnlyCells++; break; }
                        }
                    }

                Clusters(pocket, n, half, result.Pockets);
                Clusters(cookieOnly, n, half, result.CookieOnly);
                if (b == 0 && !string.IsNullOrEmpty(options.MapImage)) WriteMap(options.MapImage, nodes, cookie, monster, pocket, cookieOnly, n);
                if (options.Targets != null)
                    foreach ((string name, Vector3 pos) in options.Targets)
                    {
                        result.Targets++;
                        bool c = Near(nodes, cookie, n, half, pos), m = Near(nodes, monster, n, half, pos);
                        if (!c || !m) result.UnreachedTargets.Add($"{name} {pos:F1} cookie {c} monster {m}");
                    }
            }
        }
        finally
        {
            foreach (Collider c in disabled) c.enabled = true;
            Physics.SyncTransforms();
        }
        return results;
    }

    private static void WriteMap(string path, Node[,][] nodes, bool[,][] cookie, bool[,][] monster, bool[,] pocket, bool[,] cookieOnly, int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
        var pixels = new Color32[n * n];
        for (int ix = 0; ix < n; ix++)
            for (int iz = 0; iz < n; iz++)
            {
                bool c = cookie[ix, iz].Any(x => x), m = monster[ix, iz].Any(x => x);
                Color32 col = pocket[ix, iz] ? new Color32(170, 60, 200, 255)
                    : cookieOnly[ix, iz] ? new Color32(230, 40, 40, 255)
                    : c && m ? new Color32(70, 170, 80, 255)
                    : c ? new Color32(220, 200, 60, 255)
                    : m ? new Color32(60, 110, 200, 255)
                    : nodes[ix, iz].Length > 0 ? new Color32(45, 45, 50, 255) : new Color32(15, 15, 18, 255);
                pixels[iz * n + ix] = col;
            }
        tex.SetPixels32(pixels);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // 자리(pos)에서 수평 TargetReach m 안, 높이 차 2.5 m 안에 닿은 칸이 있는지
    private static bool Near(Node[,][] nodes, bool[,][] reached, int n, float half, Vector3 pos)
    {
        int cx = Mathf.RoundToInt((pos.x + half) / gridCell), cz = Mathf.RoundToInt((pos.z + half) / gridCell);
        int r = Mathf.CeilToInt(TargetReach / gridCell);
        for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(n - 1, cx + r); x++)
            for (int z = Mathf.Max(0, cz - r); z <= Mathf.Min(n - 1, cz + r); z++)
            {
                if ((x - cx) * (x - cx) + (z - cz) * (z - cz) > r * r) continue;
                for (int k = 0; k < nodes[x, z].Length; k++)
                    if (reached[x, z][k] && Mathf.Abs(nodes[x, z][k].Y - pos.y) <= MonsterReach) return true;
            }
        return false;
    }

    private static bool Overlaps(Node[,][] nodes, bool[,][] a, bool[,][] b, int n)
    {
        for (int ix = 0; ix < n; ix++)
            for (int iz = 0; iz < n; iz++)
                for (int k = 0; k < nodes[ix, iz].Length; k++)
                    if (a[ix, iz][k] && b[ix, iz][k]) return true;
        return false;
    }

    private static float gridCell = Cell;

    // Play Mode 검증용: from에서 to까지 쿠키(cookie=true) 또는 괴물이 갈 수 있는 격자 경로(칸 중심, 바닥 높이). 없으면 null.
    // 닫힌 문은 지나갈 수 있는 것으로 본다(가는 길에 여는 것은 MapPlaytestDriver가 한다). 이미 열린 문짝은 장애물로 둔다.
    public static List<Vector3> FindPath(Vector3 from, Vector3 to, bool cookie) => FindPath(from, to, cookie, MapCompactor.NewHalf);

    public static List<Vector3> FindPath(Vector3 from, Vector3 to, bool cookie, float half)
    {
        gridCell = Cell;
        monsterRadius = MonsterRadius;
        int n = Mathf.RoundToInt(half * 2f / Cell) + 1;
        var disabled = new List<Collider>();
        foreach (InteractableDoor door in Object.FindObjectsByType<InteractableDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (door.State == InteractableDoor.DoorState.Closed)
            foreach (Collider c in door.GetComponentsInChildren<Collider>())
                if (c.enabled) { c.enabled = false; disabled.Add(c); }
        // 캐릭터 자신은 장애물이 아니다
        foreach (Rigidbody rb in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (!rb.isKinematic)
                foreach (Collider c in rb.GetComponentsInChildren<Collider>())
                    if (c.enabled) { c.enabled = false; disabled.Add(c); }
        Physics.SyncTransforms();
        try
        {
            var nodes = new Node[n, n][];
            for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                    nodes[ix, iz] = FindNodes(CellPos(ix, iz, half), CookieOnlyBody);

            (int x, int z) Index(Vector3 p) => (Mathf.Clamp(Mathf.RoundToInt((p.x + half) / Cell), 0, n - 1), Mathf.Clamp(Mathf.RoundToInt((p.z + half) / Cell), 0, n - 1));
            var (sx, sz) = Index(from);
            var (tx, tz) = Index(to);
            // 실제 캐릭터 충돌체는 작아 격자 기준(괴물 통로 반경)으로는 서 있을 수 없는 칸에 있을 수 있다 → 2 m 안에서 가장 가까운 칸부터
            int start = -1;
            float startD = float.MaxValue;
            int r = Mathf.CeilToInt(2f / Cell);
            int ox = sx, oz = sz;
            for (int ix = Mathf.Max(0, ox - r); ix <= Mathf.Min(n - 1, ox + r); ix++)
                for (int iz = Mathf.Max(0, oz - r); iz <= Mathf.Min(n - 1, oz + r); iz++)
                {
                    int k = Nearest(nodes[ix, iz], from.y, cookie);
                    float d = (ix - ox) * (ix - ox) + (iz - oz) * (iz - oz);
                    // 시작 칸은 지금 자리에서 곧장 갈 수 있어야 한다(열린 문짝 너머의 칸을 고르면 끼어 버린다)
                    if (k >= 0 && d < startD && (d == 0 || Sweep(from, from.y, CellPos(ix, iz, half), nodes[ix, iz][k].Y, CookieBody))) { startD = d; start = k; (sx, sz) = (ix, iz); }
                }
            if (start < 0) return null;

            var parent = new Dictionary<(int, int, int), (int, int, int)>();
            var queue = new Queue<(int x, int z, int k)>();
            var first = (sx, sz, start);
            parent[first] = first;
            queue.Enqueue(first);
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            float rise = MonsterRise * Cell; // 검증 드라이버는 점프하지 않으므로 둘 다 걸어서 갈 수 있는 길만
            (int, int, int)? goal = null;
            (int, int, int)? closest = null;   // 목표 칸이 물체 안이면 닿을 수 있는 가장 가까운 칸으로(6 m 안)
            float closestD = 6f;
            while (queue.Count > 0 && goal == null)
            {
                var (x, z, k) = queue.Dequeue();
                Node here = nodes[x, z][k];
                float toTarget = new Vector2(x - tx, z - tz).magnitude * Cell;
                if (toTarget < closestD) { closestD = toTarget; closest = (x, z, k); }
                if (Mathf.Abs(x - tx) <= 1 && Mathf.Abs(z - tz) <= 1) { goal = (x, z, k); break; }
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dx[d], nz = z + dz[d];
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    Node[] list = nodes[nx, nz];
                    for (int j = 0; j < list.Length; j++)
                    {
                        if (parent.ContainsKey((nx, nz, j)) || !(cookie ? list[j].Cookie : list[j].Monster)) continue;
                        float dy = list[j].Y - here.Y;
                        if (dy > rise) continue;
                        if (cookie && !Sweep(CellPos(x, z, half), here.Y, CellPos(nx, nz, half), list[j].Y, CookieBody)) continue;
                        if (dy > MonsterStep && !IsSlope(CellPos(x, z, half), here.Y, CellPos(nx, nz, half), list[j].Y)) continue;
                        parent[(nx, nz, j)] = (x, z, k);
                        queue.Enqueue((nx, nz, j));
                    }
                }
            }
            if (goal == null) goal = closest;
            if (goal == null) return null;

            var path = new List<Vector3>();
            for (var cur = goal.Value; ; cur = parent[cur])
            {
                Vector3 p = CellPos(cur.Item1, cur.Item2, half);
                path.Add(new Vector3(p.x, nodes[cur.Item1, cur.Item2][cur.Item3].Y, p.z));
                if (parent[cur].Equals(cur)) break;
            }
            path.Reverse();
            return path;
        }
        finally
        {
            foreach (Collider c in disabled) c.enabled = true;
            Physics.SyncTransforms();
        }
    }

    private static float monsterRadius = MonsterRadius;

    private static Vector3 CellPos(int ix, int iz, float half) => new Vector3(-half + ix * gridCell, 0f, -half + iz * gridCell);

    private static readonly Body[] CookieOnlyBody = { CookieBody };

    // 위에서 아래로 훑어 걸을 수 있는 면(법선 y ≥ 0.7)마다 쿠키·괴물이 설 수 있는지 본다. 아래층부터.
    private static Node[] FindNodes(Vector3 p, Body[] bodies)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, 300f, p.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        var list = new List<Node>();
        bool first = true;
        foreach (RaycastHit h in hits.OrderBy(h => h.point.y))
        {
            if (h.normal.y < MinWalkNormal) continue;
            bool ground = first;
            if (list.Count > 0 && h.point.y - list[list.Count - 1].Y < 0.5f)
            {
                // 0.5 m 안에 겹친 두 면에서는 위의 면에 선다(얇은 경사 발판 끝이 아래 바닥에 가려 길이 끊기지 않게)
                ground = list[list.Count - 1].Ground;
                list.RemoveAt(list.Count - 1);
            }
            Vector3 f = h.point;
            var node = new Node { Y = f.y, Monster = Free(f, monsterRadius, MonsterHeight) };
            for (int b = 0; b < bodies.Length; b++)
                if (Free(f, bodies[b].Radius * 0.9f, bodies[b].Height)) node.Fits |= 1 << b;
            node.Ground = ground;
            if (ground) node.Covered = hits.Any(o => o.point.y > f.y + 0.5f && !IsWalkableGround(o.collider));
            first = false;
            if ((node.Fits != 0 || node.Monster) && !InsideSolid(f)) list.Add(node);
            if (list.Count >= MaxLayers) break;
        }
        return list.ToArray();
    }

    // 속이 빈 메시 충돌체(나무 줄기·버섯 등) 안쪽 바닥은 CheckCapsule로 걸리지 않는다. 수평 네 방향으로 쏜 첫 충돌이
    // 모두 뒷면이면 닫힌 물체 안으로 본다.
    private static readonly Vector3[] Around = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

    private static bool InsideSolid(Vector3 feet)
    {
        bool saved = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            Vector3 o = feet + Vector3.up * 1f;
            foreach (Vector3 dir in Around)
            {
                if (!Physics.Raycast(o, dir, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (Vector3.Dot(hit.normal, dir) <= 0f) return false;
            }
            return true;
        }
        finally { Physics.queriesHitBackfaces = saved; }
    }

    // 걷는 지형 자체는 장애물로 보지 않는다(경사면에서 반경 1.9 m 구가 비탈에 닿아 막힌 것으로 잘못 나왔다).
    private static readonly Collider[] overlapBuffer = new Collider[64];

    private static bool Free(Vector3 feet, float radius, float height)
    {
        Vector3 a = feet + Vector3.up * (radius + 0.15f);
        Vector3 b = feet + Vector3.up * Mathf.Max(radius + 0.15f, height - radius);
        // 길이 0인 캡슐은 PhysX가 메시 경사면에 닿는다고 잘못 보는 일이 있어(5 cm 이상 떨어져도) 구로 잰다
        int count = (b - a).sqrMagnitude < 1e-6f
            ? Physics.OverlapSphereNonAlloc(a, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore)
            : Physics.OverlapCapsuleNonAlloc(a, b, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!IsWalkableGround(overlapBuffer[i])) return false;
        return true;
    }

    private static bool IsWalkableGround(Collider c)
    {
        if (c.name.Contains("Terrain_Walkable") || c.name.Contains("Terrain_Decor")) return true;
        return c.transform.parent != null && c.transform.parent.name == "MapColliders";
    }

    // body < 0이면 괴물
    private static bool[,][] Flood(Node[,][] nodes, int n, float half, string spawnName, int body, Body shape, out bool onGraph, Vector3[] extraSeeds = null)
    {
        bool cookie = body >= 0;
        int bit = cookie ? 1 << body : 0;
        var seen = new bool[n, n][];
        for (int ix = 0; ix < n; ix++)
            for (int iz = 0; iz < n; iz++)
                seen[ix, iz] = new bool[nodes[ix, iz].Length];
        onGraph = false;

        GameObject spawn = GameObject.Find(spawnName);
        if (spawn == null) return seen;
        Vector3 s = spawn.transform.position;
        int sx = Mathf.Clamp(Mathf.RoundToInt((s.x + half) / Cell), 0, n - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt((s.z + half) / Cell), 0, n - 1);
        int start = cookie ? NearestFit(nodes[sx, sz], s.y, bit) : Nearest(nodes[sx, sz], s.y, false);
        if (start < 0) return seen;
        onGraph = true;

        float rise = cookie ? shape.Rise : MonsterRise * gridCell; // 괴물은 비탈 기울기(1 m당 0.9 m), 쿠키는 한 번 뛰는 높이
        var queue = new Queue<(int x, int z, int k)>();
        seen[sx, sz][start] = true;
        queue.Enqueue((sx, sz, start));
        if (extraSeeds != null)
            foreach (Vector3 e in extraSeeds)
            {
                int ex = Mathf.Clamp(Mathf.RoundToInt((e.x + half) / gridCell), 0, n - 1), ez = Mathf.Clamp(Mathf.RoundToInt((e.z + half) / gridCell), 0, n - 1);
                int k = cookie ? NearestFit(nodes[ex, ez], e.y, bit) : Nearest(nodes[ex, ez], e.y, false);
                if (k < 0 || seen[ex, ez][k]) continue;
                seen[ex, ez][k] = true;
                queue.Enqueue((ex, ez, k));
            }
        int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
        while (queue.Count > 0)
        {
            var (x, z, k) = queue.Dequeue();
            Node from = nodes[x, z][k];
            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d], nz = z + dz[d];
                if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                Node[] list = nodes[nx, nz];
                for (int j = 0; j < list.Length; j++)
                {
                    if (seen[nx, nz][j] || !(cookie ? (list[j].Fits & bit) != 0 : list[j].Monster)) continue;
                    float dy = list[j].Y - from.Y;
                    if (dy > rise) continue;                       // 오르기는 한계까지, 내려가기는 떨어져도 된다
                    if (!cookie && dy > MonsterStep && !IsSlope(CellPos(x, z, half), from.Y, CellPos(nx, nz, half), list[j].Y)) continue;
                    if (cookie && !Sweep(CellPos(x, z, half), from.Y, CellPos(nx, nz, half), list[j].Y, shape)) continue;
                    seen[nx, nz][j] = true;
                    queue.Enqueue((nx, nz, j));
                }
            }
        }
        return seen;
    }

    // 쿠키가 이웃 칸으로 옮겨 갈 때 얇은 벽·울타리에 막히는지(두 칸 모두 비어 있어도 사이에 벽이 있을 수 있다)
    private static bool Sweep(Vector3 a, float ya, Vector3 b, float yb, Body body)
    {
        float y = Mathf.Max(ya, yb) + 0.05f;
        float r = body.Radius * 0.6f;
        Vector3 p1 = new Vector3(a.x, y + r + 0.1f, a.z), p2 = new Vector3(a.x, y + body.Height - r, a.z);
        Vector3 dir = b - a;
        dir.y = 0f;
        foreach (RaycastHit hit in Physics.CapsuleCastAll(p1, p2, r, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!IsWalkableGround(hit.collider)) return false;
        return true;
    }

    // 두 칸 사이가 이어진 비탈인지(가운데 높이가 두 칸 높이의 중간쯤) — 턱이면 가운데가 한쪽 높이와 같다.
    private static bool IsSlope(Vector3 a, float ya, Vector3 b, float yb)
    {
        Vector3 mid = (a + b) * 0.5f;
        float top = Mathf.Max(ya, yb) + 0.5f;
        if (!Physics.Raycast(new Vector3(mid.x, top, mid.z), Vector3.down, out RaycastHit hit, Mathf.Abs(ya - yb) + 1.5f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        float expected = (ya + yb) * 0.5f;
        return Mathf.Abs(hit.point.y - expected) <= 0.25f * Mathf.Abs(ya - yb) + 0.05f;
    }

    private static int Nearest(Node[] list, float y, bool cookie)
    {
        int best = -1;
        float bestD = 3f;
        for (int k = 0; k < list.Length; k++)
        {
            if (!(cookie ? list[k].Cookie : list[k].Monster)) continue;
            float d = Mathf.Abs(list[k].Y - y);
            if (d < bestD) { bestD = d; best = k; }
        }
        return best;
    }

    private static int NearestFit(Node[] list, float y, int bit)
    {
        int best = -1;
        float bestD = 3f;
        for (int k = 0; k < list.Length; k++)
        {
            if ((list[k].Fits & bit) == 0) continue;
            float d = Mathf.Abs(list[k].Y - y);
            if (d < bestD) { bestD = d; best = k; }
        }
        return best;
    }

    private static bool MonsterSpawnReachedByCookie(Node[,][] nodes, bool[,][] cookie, int n, float half)
    {
        GameObject spawn = GameObject.Find(SceneSpawnPoints.Monster);
        if (spawn == null) return false;
        Vector3 s = spawn.transform.position;
        int sx = Mathf.Clamp(Mathf.RoundToInt((s.x + half) / Cell), 0, n - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt((s.z + half) / Cell), 0, n - 1);
        for (int x = Mathf.Max(0, sx - 2); x <= Mathf.Min(n - 1, sx + 2); x++)
            for (int z = Mathf.Max(0, sz - 2); z <= Mathf.Min(n - 1, sz + 2); z++)
                for (int k = 0; k < nodes[x, z].Length; k++)
                    if (cookie[x, z][k] && Mathf.Abs(nodes[x, z][k].Y - s.y) < 2f) return true;
        return false;
    }

    private static bool MonsterNear(Node[,][] nodes, bool[,][] monster, int n, int ix, int iz, float y, int reach)
    {
        for (int x = Mathf.Max(0, ix - reach); x <= Mathf.Min(n - 1, ix + reach); x++)
            for (int z = Mathf.Max(0, iz - reach); z <= Mathf.Min(n - 1, iz + reach); z++)
            {
                if ((x - ix) * (x - ix) + (z - iz) * (z - iz) > reach * reach) continue;
                Node[] list = nodes[x, z];
                for (int k = 0; k < list.Length; k++)
                    if (monster[x, z][k] && Mathf.Abs(list[k].Y - y) <= MonsterReach) return true;
            }
        return false;
    }

    private static void Clusters(bool[,] mask, int n, float half, List<string> output)
    {
        var seen = new bool[n, n];
        int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
        var found = new List<(int count, string text)>();
        for (int ix = 0; ix < n; ix++)
            for (int iz = 0; iz < n; iz++)
            {
                if (!mask[ix, iz] || seen[ix, iz]) continue;
                var queue = new Queue<(int, int)>();
                queue.Enqueue((ix, iz));
                seen[ix, iz] = true;
                int count = 0;
                float sx = 0f, sz = 0f, minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                while (queue.Count > 0)
                {
                    var (x, z) = queue.Dequeue();
                    Vector3 p = CellPos(x, z, half);
                    count++;
                    sx += p.x; sz += p.z;
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + dx[d], nz = z + dz[d];
                        if (nx < 0 || nz < 0 || nx >= n || nz >= n || seen[nx, nz] || !mask[nx, nz]) continue;
                        seen[nx, nz] = true;
                        queue.Enqueue((nx, nz));
                    }
                }
                if (count >= ReportClusterMin)
                    found.Add((count, $"{count} cells around ({sx / count:F0}, {sz / count:F0}) x[{minX:F0},{maxX:F0}] z[{minZ:F0},{maxZ:F0}]"));
            }
        output.AddRange(found.OrderByDescending(f => f.count).Select(f => f.text));
    }
}
