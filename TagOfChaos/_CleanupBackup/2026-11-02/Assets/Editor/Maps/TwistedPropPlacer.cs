using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 망가진 소품 배치(TwistedCandyPlan.md §2.2·V3). 씬마다 표의 소품을 빈 평지에 자동으로 놓는다 — 손으로 고친 좌표가 없어
// 맵을 다시 만들어도(MapSceneBuilder) 이 메뉴만 다시 실행하면 된다. 같은 씬에 다시 실행하면 이전 배치(TwistedProps 루트)를 지우고 같은 자리에 다시 놓는다.
// 자리 조건: 위에서 쏜 광선이 처음 닿는 곳이 걸을 수 있는 평평한 바닥, 소품 둘레 + 여유(Clearance)에 다른 충돌체가 없음
// (맵 여유 2.5 m → 벽과 소품 사이 틈이 5 m 이상이라 괴물 몸(지름 3.6 m)도 지나간다 — 쿠키만 들어가는 틈을 만들지 않는다),
// 탈출 장치·로켓·상자 자리·출발점에서 떨어짐, 소품끼리 떨어짐. 앞(+Z)이 맵 가운데를 본다.
public static class TwistedPropPlacer
{
    public const string RootName = "TwistedProps";
    private const float MapClearance = 2.5f;
    private const float PropSpacing = 11f;
    private const float Grid = 3f;

    // Shelter: 숨을 곳(V4) — 크게 비어 있는 땅이 필요해 먼저 놓고, 방향은 90° 단위(문이 맵 가운데 쪽/반대쪽), 바닥 기울기 0.6 m까지.
    // Replace: 숨을 곳이 들어갈 큰 빈 땅이 맵에 없어서, 맵의 장식 건물(같은 크기의 텐트·집) 자리를 차례로 시도해 바꾼다 — 원래 건물은 끄고(지우지 않음)
    // 그 자리·방향(90° 단위)에 들어갈 수 있는 숨을 곳을 놓는다. 다시 실행하면 원래 건물을 먼저 다시 켠다.
    public sealed class Spot { public string Prefab; public int Count; public float Scale = 1f; public bool Shelter; public string[] Replace; }

    public sealed class SceneProps
    {
        public string ScenePath;
        public string GroundToken;      // 바닥 충돌체 이름에 들어가는 글자
        public float RayTop;            // 광선 시작 높이(지붕 아래)
        public float MinRadius, MaxRadius;
        public int Seed;
        public float Clearance = MapClearance; // 대기실은 술래잡기 판이 아니라 좁게(1.2 m) 둔다
        public Spot[] Spots;
    }

    private static Spot S(string prefab, int count, float scale = 1f) => new Spot { Prefab = prefab, Count = count, Scale = scale };
    private static Spot Shelter(string prefab, int count, params string[] replace) => new Spot { Prefab = prefab, Count = count, Shelter = true, Replace = replace };

    public static readonly SceneProps[] Scenes =
    {
        new SceneProps { ScenePath = TwistedAtmosphere.MapScenePath("CandyForest"), GroundToken = "Terrain_Walkable", RayTop = 40f, MinRadius = 6f, MaxRadius = 66f, Seed = 1,
            Spots = new[] { S("TW_HeadlessUnicorn", 2), S("TW_EyeLollipop", 3), S("TW_MeltingTree", 3), S("TW_MoldyCake", 2) } },
        new SceneProps { ScenePath = TwistedAtmosphere.MapScenePath("GingerbreadVillage"), GroundToken = "Terrain_Walkable", RayTop = 40f, MinRadius = 6f, MaxRadius = 66f, Seed = 2,
            Spots = new[] { Shelter("TW_Shelter_GingerHouse", 2, "GIN_GingerHouse_Cottage_28", "GIN_GingerHouse_Cottage_40", "GIN_GingerHouse_Cottage_02", "GIN_GingerHouse_Cottage_26", "GIN_GingerHouse_Cottage_14"), S("TW_BrokenGingerDoll", 2), S("TW_EyelessGuard", 3), S("TW_CrackedFence", 3), S("TW_EyeLollipop_Warm", 2) } },
        new SceneProps { ScenePath = TwistedAtmosphere.MapScenePath("ChocolateFactory"), GroundToken = "Terrain_Walkable", RayTop = 40f, MinRadius = 6f, MaxRadius = 66f, Seed = 3,
            Spots = new[] { S("TW_MeltedDollMold", 2), S("TW_FacelessDoughRow", 2), S("TW_BrokenPacker", 2), S("TW_EyeLollipop", 1) } },
        new SceneProps { ScenePath = TwistedAtmosphere.MapScenePath("CursedCandyCarnival"), GroundToken = "Terrain_Walkable", RayTop = 40f, MinRadius = 6f, MaxRadius = 66f, Seed = 4,
            Spots = new[] { Shelter("TW_Shelter_CircusTent", 2, "CUR_CircusTent_Small_11", "CUR_CircusTent_Small_01", "CUR_CircusTent_Small_07", "CUR_CircusTent_Small_14",
                "CUR_CircusTent_Small_06", "CUR_CircusTent_Small_13", "CUR_CircusTent_Small_08", "CUR_CircusTent_Small_09", "CUR_CircusTent_Small_03",
                "CUR_CircusTent_Small_17", "CUR_CircusTent_Small_21", "CUR_CircusTent_Small_23", "CUR_CircusTent_Small_26"), S("TW_HeadlessHorse_Carnival", 3), S("TW_ClownSign", 2), S("TW_TicketDoll", 2), S("TW_DeflatedBalloons", 3) } },
        new SceneProps { ScenePath = TwistedAtmosphere.MapScenePath("HauntedBakery"), GroundToken = "Terrain_Walkable", RayTop = 20f, MinRadius = 4f, MaxRadius = 66f, Seed = 5,
            Spots = new[] { S("TW_CrackedWeddingCake", 2), S("TW_JarDoll", 3), S("TW_FlourSack", 3) } },
        new SceneProps { ScenePath = TwistedAtmosphere.GameLobbyScene, GroundToken = "LobbyGround", RayTop = 30f, MinRadius = 7f, MaxRadius = 30f, Seed = 6, Clearance = 1.2f,
            Spots = new[] { S("TW_HangingJellyBears", 2), S("TW_EyeLollipop_Warm", 2, 0.7f), S("TW_CrackedFence", 1, 0.6f) } },
    };

    [MenuItem("Tools/TagOfChaos/Maps/Place Twisted Props (All)")]
    public static void PlaceAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var report = new List<string>();
        foreach (SceneProps sp in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(sp.ScenePath, OpenSceneMode.Single);
            report.Add($"{System.IO.Path.GetFileNameWithoutExtension(sp.ScenePath)}: {PlaceInOpenScene(sp)}");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("[TwistedProps]\n" + string.Join("\n", report));
    }

    public static string PlaceInOpenScene(SceneProps sp)
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        foreach (Spot spot in sp.Spots)
            if (spot.Replace != null)
                foreach (string n in spot.Replace) { Transform t = FindMapObject(n); if (t != null) t.gameObject.SetActive(true); }
        Physics.SyncTransforms();

        var avoid = AvoidPoints();
        var rnd = new System.Random(sp.Seed);
        var candidates = new List<Vector3>();
        for (float x = -sp.MaxRadius; x <= sp.MaxRadius; x += Grid)
        for (float z = -sp.MaxRadius; z <= sp.MaxRadius; z += Grid)
        {
            float r = new Vector2(x, z).magnitude;
            if (r >= sp.MinRadius && r <= sp.MaxRadius) candidates.Add(new Vector3(x, 0f, z));
        }
        candidates = candidates.OrderBy(_ => rnd.Next()).ToList();

        var root = new GameObject(RootName);
        root.isStatic = true;
        var placed = new List<Vector3>();
        var parts = new List<string>();
        int missing = 0;
        foreach (Spot spot in sp.Spots)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TwistedPropBuilder.PrefabFolder}/{spot.Prefab}.prefab");
            if (prefab == null) { parts.Add($"{spot.Prefab} missing"); continue; }
            int done = 0;
            if (spot.Replace != null)
            {
                foreach (string n in spot.Replace)
                {
                    if (done >= spot.Count) break;
                    Transform original = FindMapObject(n);
                    if (original == null) continue;
                    Vector3 at = original.position;
                    if (avoid.Any(a => a.w > 6f && (new Vector2(a.x - at.x, a.z - at.z)).magnitude < a.w)) continue;
                    original.gameObject.SetActive(false);
                    Physics.SyncTransforms();
                    if (!TryGround(sp, at, out Vector3 floor)) { original.gameObject.SetActive(true); continue; }
                    var shelter = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    float yaw0 = Mathf.Round(original.eulerAngles.y / 90f) * 90f;
                    bool fits = false;
                    for (int turn = 0; turn < 4 && !fits; turn++) // 원래 방향부터 90°씩 — 벽이 상자 자리와 겹치면 돌려 본다
                    {
                        shelter.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw0 + turn * 90f, 0f));
                        Physics.SyncTransforms();
                        fits = IsClear(shelter, sp.Clearance, 0.8f, allowChests: true) && DoorsOpen(shelter);
                    }
                    if (!fits)
                    {
                        Object.DestroyImmediate(shelter);
                        original.gameObject.SetActive(true);
                        Physics.SyncTransforms();
                        continue;
                    }
                    shelter.name = $"{spot.Prefab}_{done:00} ({n})";
                    foreach (Light exit in shelter.GetComponentsInChildren<Light>().Where(l => l.name.StartsWith("Light_Exit")))
                    {
                        Vector3 dir = exit.transform.position - shelter.transform.position;
                        dir.y = 0f;
                        Vector3 front = exit.transform.position + dir.normalized * 4f;
                        avoid.Add(new Vector4(front.x, 0f, front.z, 7f)); // 뒤에 놓는 소품이 문 앞을 막지 않게
                    }
                    placed.Add(new Vector3(at.x, 0f, at.z));
                    done++;
                }
                missing += spot.Count - done;
                parts.Add($"{spot.Prefab} {done}/{spot.Count}");
                continue;
            }
            foreach (Vector3 c in candidates)
            {
                if (done >= spot.Count) break;
                if (placed.Any(p => (p - c).sqrMagnitude < PropSpacing * PropSpacing)) continue;
                if (avoid.Any(a => (new Vector2(a.x - c.x, a.z - c.z)).magnitude < a.w)) continue;
                if (!TryGround(sp, c, out Vector3 ground)) continue;
                Quaternion rot = Quaternion.LookRotation(new Vector3(-c.x, 0f, -c.z).normalized + Vector3.right * 0.001f);
                if (spot.Shelter) rot = Quaternion.Euler(0f, Mathf.Round(rot.eulerAngles.y / 90f) * 90f, 0f);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                go.transform.SetPositionAndRotation(ground, rot);
                go.transform.localScale = Vector3.one * spot.Scale;
                Physics.SyncTransforms();
                if (!IsClear(go, sp.Clearance, spot.Shelter ? 0.6f : 0.4f))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }
                go.name = $"{spot.Prefab}_{done:00}";
                placed.Add(c);
                done++;
            }
            missing += spot.Count - done;
            parts.Add($"{spot.Prefab} {done}/{spot.Count}");
        }
        return string.Join(", ", parts) + (missing > 0 ? $" — {missing} not placed" : "");
    }

    // (x, y 무시, z, 반경) — 탈출 장치·로켓·상자 자리·출발점·가마솥 둘레는 비운다.
    private static List<Vector4> AvoidPoints()
    {
        var list = new List<Vector4>();
        GameObject escape = GameObject.Find("EscapeSetup");
        if (escape != null)
            foreach (Transform t in escape.transform)
            {
                float r = t.name.StartsWith("ESC_") ? 18f : t.name.StartsWith("SPY_") ? 10f : 6f;
                list.Add(new Vector4(t.position.x, 0f, t.position.z, r));
            }
        foreach (string n in new[] { SceneSpawnPoints.Cookie, SceneSpawnPoints.Monster, "Cauldron" })
        {
            GameObject g = GameObject.Find(n);
            if (g != null) list.Add(new Vector4(g.transform.position.x, 0f, g.transform.position.z, n == "Cauldron" ? 8f : 10f));
        }
        return list;
    }

    private static bool TryGround(SceneProps sp, Vector3 c, out Vector3 ground)
    {
        ground = default;
        if (!Physics.Raycast(new Vector3(c.x, sp.RayTop, c.z), Vector3.down, out RaycastHit hit, sp.RayTop + 30f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (!hit.collider.name.Contains(sp.GroundToken) || hit.normal.y < 0.97f) return false;
        ground = hit.point;
        return true;
    }

    private static Transform FindMapObject(string name)
    {
        GameObject map = GameObject.Find("Map");
        if (map == null) return null;
        foreach (Transform group in map.transform)
        {
            Transform t = group.Find(name);
            if (t != null) return t;
        }
        return null;
    }

    // 숨을 곳 문(출구 등 Light_Exit_*)마다 바깥 6 m에서 실내까지 괴물 격자 경로(MapPassabilityCheck.FindPath)가 있고, 건물을 돌아가지 않을 만큼
    // 짧아야 한다 — 문이 맵 경계·장식에 막히면 출입구가 아니다(TwistedTests.Shelters_MonsterEntersThroughEveryDoor와 같은 기준). 한 번 0.5초쯤.
    public static bool DoorsOpen(GameObject shelter)
    {
        float limit = MapCompactor.NewHalf - 3f;
        Light[] lights = shelter.GetComponentsInChildren<Light>();
        Vector3[] insides = lights.Where(l => l.name.StartsWith("Light_Inside")).Select(l => FloorBelow(l.transform.position)).ToArray();
        if (insides.Length == 0) return true;
        foreach (Light exit in lights.Where(l => l.name.StartsWith("Light_Exit")))
        {
            Vector3 dir = exit.transform.position - shelter.transform.position;
            dir.y = 0f;
            Vector3 outside = FloorBelow(exit.transform.position + dir.normalized * 6f);
            if (Mathf.Abs(outside.x) > limit || Mathf.Abs(outside.z) > limit) return false;
            Vector3 inside = insides.OrderBy(p => (p - exit.transform.position).sqrMagnitude).First(); // 이 문에서 가까운 방
            List<Vector3> path = MapPassabilityCheck.FindPath(outside, inside, false);
            if (path == null) return false;
            float length = 0f;
            for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            if (length > Vector3.Distance(outside, inside) + 14f) return false;
        }
        return true;
    }

    public static Vector3 FloorBelow(Vector3 p)
    {
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (IsGround(h.collider)) return h.point;
        return new Vector3(p.x, 0f, p.z);
    }

    private static bool IsChest(Transform t)
    {
        for (; t != null; t = t.parent) if (t.name.StartsWith("CHEST_Slot")) return true;
        return false;
    }

    // 실제 충돌체 모양으로(회전한 벽의 AABB가 아니라) b 둘레 margin 안에 own 충돌체가 있는지.
    private static bool TouchesAny(Bounds b, HashSet<Collider> own, float margin) =>
        Physics.OverlapBox(b.center, b.extents + Vector3.one * margin, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Any(own.Contains);

    private static bool IsGround(Collider c) => c.name.Contains("Terrain_Walkable") || c.name.Contains("Ground");

    // 소품 충돌체 경계 + 여유 안에 바닥 말고 다른 충돌체가 없어야 한다. 바닥 경사가 심하면(네 모서리 높이 차 > maxSlope) 거른다.
    private static bool IsClear(GameObject go, float clearance, float maxSlope, bool allowChests = false)
    {
        var own = go.GetComponentsInChildren<Collider>();
        if (own.Length == 0) return true;
        Bounds b = own[0].bounds;
        foreach (Collider c in own) b.Encapsulate(c.bounds);
        var half = new Vector3(b.extents.x + clearance, b.extents.y, b.extents.z + clearance);
        var center = new Vector3(b.center.x, b.center.y + 0.3f, b.center.z);
        var ownSet = new HashSet<Collider>(own);
        foreach (Collider hit in Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (ownSet.Contains(hit)) continue;
            if (IsGround(hit)) continue;
            // 숨을 곳 안·옆의 상자 자리는 그대로 둔다(숨을 곳 안 상자 = 좋은 자리) — 벽·가구에 닿을 때만 막는다(0.25 m — 그보다 좁은 틈은 쿠키도 못 들어간다)
            if (allowChests && hit.transform.root.name == "EscapeSetup" && IsChest(hit.transform) && !TouchesAny(hit.bounds, ownSet, 0.25f)) continue;
            return false;
        }
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 corner in new[] { new Vector3(b.min.x, 0, b.min.z), new Vector3(b.max.x, 0, b.min.z), new Vector3(b.min.x, 0, b.max.z), new Vector3(b.max.x, 0, b.max.z) })
        {
            RaycastHit[] hits = Physics.RaycastAll(corner + Vector3.up * (b.max.y + 2f), Vector3.down, b.max.y + 8f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit[] floor = hits.Where(h => !ownSet.Contains(h.collider) && IsGround(h.collider)).ToArray();
            if (floor.Length == 0) return false;
            float y = floor.Max(h => h.point.y);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }
        return maxY - minY <= maxSlope;
    }
}
