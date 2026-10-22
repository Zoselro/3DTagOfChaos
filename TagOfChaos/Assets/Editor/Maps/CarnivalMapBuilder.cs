using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 저주받은 놀이공원 실제 모델 맵 씬(Request1009Plan.md §36, 단계 CM6). Test_CursedCandyCarnival을 복사한 CursedCandyCarnival 씬에서
// 놀이 공간을 새 모델로 바꾼다(캔디숲 CandyForestMapBuilder와 같은 순서·규칙, 반지름 155 m):
//   바닥판 + 사분면별 바닥 타일(겉모습만) · 둥근 술래방(문 12·3·6·9시) · 재료 건물 4(Building_Qn) · 탈출구 4 · 둘레 울타리 ·
//   큰길·둥근 산책길 꾸미기 · 사분면 분수 광장·텐트 4·노점·소품 · 가장자리 놀이기구 4(바이킹·회전목마·롤러코스터·자이로드롭) ·
//   상자/로켓/무지개/스폰 자리 · 조명. 카메라·관리자·마녀·환경음은 테스트 씬 그대로. 여러 번 실행해도 같은 결과(고정 난수).
public static class CarnivalMapBuilder
{
    public const string MapName = "CursedCandyCarnival";
    public const string ScenePath = "Assets/Scenes/Maps/" + MapName + ".unity";
    public const string GeneratedFolder = CarnivalMapAssets.GeneratedFolder;
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";

    public const float MapRadius = 155f;
    public const float RoadHalf = 5f;          // 큰길 반 폭(타일)
    public const float RoadRow = 7.5f;         // 큰길 양옆 가로등·깃발 줄
    public const float RingRadius = 80f, RingHalf = 3f; // 둥근 산책길
    public const float BuildingRadius = 100f;
    public const float RoomRadius = 12.55f;    // 술래방 벽 바깥(MO_MonsterRoom 지름 25.05 m)
    public const float RoomInner = 11.9f;
    public const float RoomDoorWidth = 4f;
    public const float GuardInnerHalf = 8f;    // 둥근 방 안 정사각(MonsterRoomGuard) — 방 바깥 모서리를 안으로 보지 않게
    private const float WallHeight = 9f;
    private const float FenceHeight = 6f, FenceThickness = 0.8f;
    private const float ExitOpening = 5.6f, ExitPostWidth = 1.75f, ExitHeight = 5.6f, PadDepth = 10f;
    private const float FloorSize = 2f * (MapRadius + 80f);
    private const float TileChunk = 32f;
    private const float TileY = 0.06f;           // 재료 건물 방 바닥(0.02~0.05 m)보다 위 — 겹치면 바닥에 검은 조각이 깜빡인다
    private const float FarBackdropClear = MapRadius + 45f;
    private const float RideFenceHeight = 3.6f;

    // 조명(§37): 가로등마다 점광원, 열린 탈출구 빛기둥(안개 무시), 술래방 둘레·문 빛. 주변광·안개는 테스트 씬 값보다 조금 밝게.
    private const float LampLightHeight = 4.7f, LampRange = 16f, LampIntensity = 2.6f;
    private const float LampGap = 3f;             // 새 가로등 둘레 빈 폭(괴물 지름 3.6 m 통로 — 가로등 기둥 0.4 m 포함)
    private const float ExitBeamHeight = 120f;
    private const float RoomLampRadius = 16f;
    private static readonly Color LampColor = new Color(1f, 0.76f, 0.52f);
    private static readonly Color ExitColor = new Color(0.1f, 1f, 0.3f);
    private static readonly Color RoomColor = new Color(1f, 0.28f, 0.22f);
    private static readonly Color SceneAmbient = new Color(0.17f, 0.37f, 0.56f);
    private const float SceneFogDensity = 0.013f;
    private const string BeaconShader = "TagOfChaos/Beacon";

    // 사분면(Q1 북서 안전벨트, Q2 북동 배터리, Q3 남서 수리 도구, Q4 남동 빨간 버튼) — 각도는 +x에서 +z 쪽으로
    public static readonly float[] QuadDeg = { 135f, 45f, 225f, 315f };
    public static readonly float[] RoadDeg = { 90f, 0f, 270f, 180f };          // N, E, S, W
    public static readonly string[] ExitNames = { "Exit_N", "Exit_E", "Exit_S", "Exit_W" };
    private static readonly string[] KeepInMap = { "Background", WitchPath.FarBackdropName, "Ground", "Rides" };

    // 텐트(사분면마다 4): (반지름, 사분면 방향에서 각도 차이)
    private static readonly Vector2[] Tents = { new Vector2(46f, -22f), new Vector2(46f, 22f), new Vector2(72f, -30f), new Vector2(72f, 30f) };
    private static readonly Vector2[] Stalls = { new Vector2(30f, -18f), new Vector2(36f, -10f), new Vector2(30f, 18f), new Vector2(36f, 10f) };
    private static readonly string[] StallNames = { "MO_Popcorn", "MO_CottonCandy", "MO_CherryJuice", "MO_Candy" };
    // 놀이기구: 바이킹 북(Q1 뒤), 회전목마 동(Q2 아래), 롤러코스터 서(Q3 위), 자이로드롭 남(Q4 아래)
    private static readonly Vector2 VikingAt = new Vector2(116f, 108f), CarouselAt = new Vector2(125f, 20f), CoasterAt = new Vector2(124f, 197f), GyroAt = new Vector2(132f, 288f);

    private static readonly Dictionary<string, (string[] mix, float[] weight, string pattern)> QuadFloor = new Dictionary<string, (string[], float[], string)>
    {
        { "Q1", (new[] { "FL_Cobble", "FL_Basic" }, new[] { 0.6f, 0.4f }, "FL_PatternCompass") },
        { "Q2", (new[] { "FL_Basic", "FL_Wet" }, new[] { 0.55f, 0.45f }, "FL_PatternWheel") },
        { "Q3", (new[] { "FL_Cobble", "FL_Wet", "FL_Sewer" }, new[] { 0.65f, 0.27f, 0.08f }, "FL_PatternDiamond") },
        { "Q4", (new[] { "FL_Basic", "FL_Cobble" }, new[] { 0.5f, 0.5f }, "FL_PatternFlower") },
    };

    // 놓은 것(수평 원) — 흩어 놓기·표시 자리 검사
    private static readonly List<(Vector2 c, float r)> taken = new List<(Vector2, float)>();
    private static readonly List<Vector3> monsterSeeds = new List<Vector3>();

    [MenuItem(MenuRoot + "Build Cursed Candy Carnival Map Scene")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        if (CarnivalMapAssets.LoadPrefab("CUR_Bld_SafetyBelt") == null) return "[CarnivalMap] prefabs missing — run Build Carnival Map Prefabs first";
        taken.Clear(); monsterSeeds.Clear(); reachCache.Clear();
        string source = TestMapBuilder.ScenePath(MapName);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(source, ScenePath)) return "[CarnivalMap] copy failed";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TwistedPropBuilder.EnsureFolder(GeneratedFolder);

        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        foreach (Transform child in map.Cast<Transform>().ToList())
            if (!KeepInMap.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
        foreach (GameObject root in scene.GetRootGameObjects().Where(g => g.name == "MapDoors").ToList()) Object.DestroyImmediate(root);
        GameObject oldEscape = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EscapeMapSetup.RootName);
        if (oldEscape != null)
            foreach (Transform child in oldEscape.transform.Cast<Transform>().ToList())
                if (child.name != "WITCH_Anchor") Object.DestroyImmediate(child.gameObject);

        var rng = new System.Random(36);
        BuildForest(map, rng);
        SpreadFarBackdrop(map);
        BuildGround(map);
        BuildMonsterRoom(map);
        BuildBuildings(map);
        BuildExits(map);
        BuildFence(map);
        int rides = BuildRides(scene, map);
        int deco = BuildRoads(map) + BuildQuadrants(map, rng) + BuildExtraLamps(map);
        int tiles = BuildTiles(map, rng);
        Physics.SyncTransforms();
        int markers = BuildMarkers(map);
        int lights = BuildLights(scene, map);

        string recipe = SetRecipe();
        string escape = EscapeMapSetup.Apply(scene, MapName);
        Transform room = map.Find("MonsterRoom");
        var guard = room != null ? room.GetComponent<MonsterRoomGuard>() : null;
        if (guard != null)
        {
            guard.EditorBind(GuardInnerHalf, room.GetComponentsInChildren<MonsterRoomDoor>(true).Select(d => d.transform).OrderBy(t => t.name).ToArray());
            EditorUtility.SetDirty(guard);
        }
        var manager = Object.FindFirstObjectByType<EscapeManager>();
        if (manager != null)
        {
            manager.EditorSetMapRadius(MapRadius);
            EditorUtility.SetDirty(manager);
        }
        MoveSpawn(scene, SceneSpawnPoints.Cookie, map, "Spawn_Cookie_Q1");
        MoveWitchAnchor(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings();
        return $"[CarnivalMap] {ScenePath}: rides {rides}, decoration {deco}, tiles {tiles}, markers {markers}, lights {lights} | {recipe} | {escape}";
    }

    // ---------------------------------------------------------------- helpers

    public static Vector3 Polar(float radius, float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, Mathf.Sin(deg * Mathf.Deg2Rad)) * radius;
    private static Vector3 Polar(Vector2 radiusDeg, float baseDeg = 0f) => Polar(radiusDeg.x, baseDeg + radiusDeg.y);
    // 로컬 +Z가 맵 가운데를 본다(재료 건물·탈출구 루트 — 건물 FBX는 앞이 +Z).
    private static Quaternion FaceCenter(Vector3 p) => new Vector2(p.x, p.z).sqrMagnitude < 0.01f ? Quaternion.identity : Quaternion.LookRotation(new Vector3(-p.x, 0f, -p.z));
    // 소품·놀이기구 FBX(ride_common 내보내기)는 앞(Blender −Y)이 Unity −Z — 앞이 맵 가운데를 보게 +Z를 바깥으로.
    private static Quaternion FrontToCenter(Vector3 p) => new Vector2(p.x, p.z).sqrMagnitude < 0.01f ? Quaternion.identity : Quaternion.LookRotation(new Vector3(p.x, 0f, p.z));

    private static Transform Group(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static GameObject Spawn(string prefab, Transform parent, Vector3 pos, Quaternion rot, bool colliders = true)
    {
        GameObject asset = CarnivalMapAssets.LoadPrefab(prefab);
        if (asset == null) { Debug.LogWarning($"[CarnivalMap] prefab {prefab} missing"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.SetPositionAndRotation(pos, rot);
        if (!colliders) foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return go;
    }

    private static GameObject Model(string fbx, Transform parent, Vector3 localPos, Quaternion localRot, string name = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarnivalMapAssets.MapObjectFolder}/{fbx}.fbx");
        if (asset == null) { Debug.LogWarning($"[CarnivalMap] model {fbx} missing"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        if (name != null) go.name = name;
        return go;
    }

    private static void LocalBox(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = center;
        t.gameObject.AddComponent<BoxCollider>().size = size;
    }

    private static readonly Dictionary<string, float> reachCache = new Dictionary<string, float>();

    // 프리팹 원점에서 겉모습 끝까지(수평). 프리팹 에셋의 bounds는 비어 있어 잠깐 꺼내 잰다.
    private static float Reach(string prefab)
    {
        if (reachCache.TryGetValue(prefab, out float cached)) return cached;
        GameObject asset = CarnivalMapAssets.LoadPrefab(prefab);
        if (asset == null) return 2f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        float r = 0f;
        try
        {
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Renderer x in go.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = x.bounds;
                r = Mathf.Max(r, new Vector2(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z))).magnitude);
            }
        }
        finally { Object.DestroyImmediate(go); }
        return reachCache[prefab] = r;
    }

    private static void Take(Vector3 p, float r) => taken.Add((new Vector2(p.x, p.z), r));
    private static bool IsFree(Vector3 p, float r) => taken.All(t => (t.c - new Vector2(p.x, p.z)).magnitude >= t.r + r);

    // ---------------------------------------------------------------- background

    // 테스트 맵 소나무(80 m 맵 둘레)는 넓어진 울타리 안이라 지우고, 울타리 밖에 숲 고리를 새로 심는다(충돌 없음). 옛 지형 고리도 울타리 안이라 지운다.
    private static void BuildForest(Transform map, System.Random rng)
    {
        Transform background = map.Find("Background");
        if (background == null) return;
        List<Transform> pines = background.Cast<Transform>().Where(t => t.name.StartsWith("CUR_Pine_")).ToList();
        Transform pineA = pines.FirstOrDefault(t => t.name.StartsWith("CUR_Pine_A")), pineB = pines.FirstOrDefault(t => t.name.StartsWith("CUR_Pine_B"));
        var forest = Group(background, "CUR_Forest");
        if (pineA != null && pineB != null)
            for (int i = 0; i < 84; i++)
            {
                float deg = i * 360f / 84f + (float)rng.NextDouble() * 3f;
                float radius = MapRadius + 16f + (float)rng.NextDouble() * 38f;
                bool nearExit = RoadDeg.Any(e => Mathf.Abs(Mathf.DeltaAngle(deg, e)) < 7f);
                if (nearExit) radius += 14f;
                Transform template = i % 3 == 0 ? pineB : pineA;
                Transform tree = Object.Instantiate(template.gameObject, forest).transform;
                tree.name = $"CUR_Pine_{i:00}";
                tree.position = Polar(radius, deg);
                tree.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * template.rotation;
                tree.localScale = template.localScale * (0.85f + (float)rng.NextDouble() * 0.4f);
                foreach (Collider c in tree.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }
        foreach (Transform t in pines) Object.DestroyImmediate(t.gameObject);
        Transform terrain = background.Find("CUR_Terrain_Decor");
        if (terrain != null) Object.DestroyImmediate(terrain.gameObject);
    }

    // 마녀가 걸어오는 먼 배경 중 넓어진 맵에 걸리는 것은 그만큼 바깥으로(캔디숲 §32와 같음).
    private static void SpreadFarBackdrop(Transform map)
    {
        Transform far = map.Find(WitchPath.FarBackdropName);
        if (far == null) return;
        float push = MapRadius - 80f;
        foreach (Transform t in far.Cast<Transform>().ToList())
        {
            Renderer r = t.GetComponentInChildren<Renderer>(true);
            if (r == null || r.bounds.size.x > FarBackdropClear) continue;
            var flat = new Vector2(r.bounds.center.x, r.bounds.center.z);
            if (flat.magnitude >= FarBackdropClear || flat.sqrMagnitude < 1f) continue;
            Vector2 move = flat.normalized * push;
            t.position += new Vector3(move.x, 0f, move.y);
        }
    }

    // 걷는 면은 평평한 바닥판 하나(타일은 겉모습만). 사각 벽은 지운다(둥근 울타리).
    private static void BuildGround(Transform map)
    {
        Transform ground = map.Find("Ground");
        foreach (Transform child in ground.Cast<Transform>().ToList())
            if (child.name.StartsWith("Wall_")) Object.DestroyImmediate(child.gameObject);
        Transform floor = ground.Find("Floor");
        if (floor == null) return;
        Vector3 scale = Vector3.one;
        for (int i = 0; i < 3; i++)
        {
            Vector3 axis = Vector3.zero;
            axis[i] = 1f;
            if (Mathf.Abs((floor.rotation * axis).y) < 0.5f) scale[i] = FloorSize / 140f;
        }
        floor.localScale = scale;
    }

    // ---------------------------------------------------------------- monster room

    // 둥근 술래방(MO_MonsterRoom, 반지름 12 m, 문 12·3·6·9시 폭 4 m). 벽은 겹친 상자 고리(RoomRing_), 문(MonsterDoor_0~3 = 북·동·남·서)은 괴물만 지나간다.
    private static void BuildMonsterRoom(Transform map)
    {
        var room = new GameObject("MonsterRoom").transform;
        room.SetParent(map, false);
        Model("MO_MonsterRoom", room, Vector3.zero, Quaternion.identity, "Model");
        Transform ring = Group(room, "RoomRings");
        const int segments = 72;
        float step = 360f / segments, doorHalfDeg = (RoomDoorWidth / 2f) / RoomInner * Mathf.Rad2Deg;
        for (int i = 0; i < segments; i++)
        {
            float a = (i + 0.5f) * step;
            if (RoadDeg.Any(d => Mathf.Abs(Mathf.DeltaAngle(a, d)) < doorHalfDeg + step / 2f)) continue;
            Vector3 dir = Polar(1f, a);
            var t = new GameObject($"RoomRing_{i:00}").transform;
            t.SetParent(ring, false);
            t.localPosition = dir * ((RoomInner + RoomRadius) / 2f) + Vector3.up * WallHeight / 2f;
            t.localRotation = Quaternion.LookRotation(dir);
            float chord = 2f * RoomRadius * Mathf.Sin(step * Mathf.Deg2Rad / 2f) + 0.2f;
            t.gameObject.AddComponent<BoxCollider>().size = new Vector3(chord, WallHeight, RoomRadius - RoomInner + 0.1f);
        }
        // 문 옆 기둥: 문 폭 바깥 끝을 정확히 막는다(고리 조각 사이 틈 없음)
        var doorMesh = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarnivalMapAssets.MapObjectFolder}/MO_MonsterRoomDoor.fbx");
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Polar(1f, RoadDeg[i]);
            Quaternion rot = Quaternion.LookRotation(dir);
            foreach (int s in new[] { -1, 1 })
            {
                var post = new GameObject($"RoomRing_Post{i}{(s < 0 ? "L" : "R")}").transform;
                post.SetParent(ring, false);
                post.localPosition = dir * ((RoomInner + RoomRadius) / 2f) + rot * Vector3.right * (s * (RoomDoorWidth / 2f + 0.5f)) + Vector3.up * WallHeight / 2f;
                post.localRotation = rot;
                post.gameObject.AddComponent<BoxCollider>().size = new Vector3(1f, WallHeight, RoomRadius - RoomInner + 0.3f);
            }
            var door = new GameObject("MonsterDoor_" + i);
            door.transform.SetParent(room, false);
            door.transform.localPosition = dir * ((RoomInner + RoomRadius) / 2f);
            door.transform.localRotation = rot;
            MeshFilter source = doorMesh != null ? doorMesh.GetComponentInChildren<MeshFilter>() : null;
            if (source != null)
            {
                door.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                door.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            }
            var box = door.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, WallHeight / 2f, 0f);
            box.size = new Vector3(RoomDoorWidth + 0.2f, WallHeight, RoomRadius - RoomInner + 0.3f);
            // 문 앞 붉은 바닥 빛(§37 L3, 겉모습만) — 문 자리가 멀리서 보이게
            BeaconPart(room, "DoorGlow_" + i, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), BeaconMaterial("CUR_RoomDoorGlow", RoomColor * 1.2f, 1f, 1.5f, 1f),
                dir * RoomRadius + Vector3.up * 0.1f, Quaternion.Euler(90f, 0f, 0f), new Vector3(7f, 7f, 1f));
        }
        var spawn = new GameObject("Spawn_Monster").transform;
        spawn.SetParent(room, false);
        Take(Vector3.zero, RoomRadius + 2f);
    }

    // ---------------------------------------------------------------- buildings

    private static void BuildBuildings(Transform map)
    {
        Transform group = Group(map, "Buildings");
        for (int q = 0; q < 4; q++)
        {
            string key = CarnivalMapAssets.BuildingKeys[q];
            Vector3 p = Polar(BuildingRadius, QuadDeg[q]);
            var root = new GameObject($"Building_Q{q + 1}").transform;   // EscapeMapSetup 이름 규칙(QuadrantBuilding)
            root.SetParent(group, false);
            root.SetPositionAndRotation(p, FaceCenter(p));
            GameObject bld = Spawn("CUR_Bld_" + key, root, p, root.rotation);
            if (bld == null) continue;
            Bounds local = LocalBounds(bld.transform);
            Take(p, new Vector2(local.extents.x, local.extents.z).magnitude * 0.85f); // 바닥 타일은 건물 밑에도 깐다(방 바닥 0 m 위 2 cm — 겉 건물 밑은 가려진다)
            monsterSeeds.Add(p + Vector3.up * 0.1f); // 방 가운데(문 폭이 넓어 실제로도 들어간다 — §35.2)
        }
    }

    // t 기준 범위: 메시 자기 범위의 꼭짓점을 옮겨 잰다(돌아간 물체의 월드 범위로 재면 부풀려진다).
    private static Bounds LocalBounds(Transform t)
    {
        var b = new Bounds();
        bool first = true;
        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.name == "ESC_Glow" || mf.sharedMesh == null || mf.GetComponent<Renderer>() == null) continue;
            Bounds w = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = t.InverseTransformPoint(mf.transform.TransformPoint(new Vector3(i % 2 == 0 ? w.min.x : w.max.x, i / 2 % 2 == 0 ? w.min.y : w.max.y, i / 4 == 0 ? w.min.z : w.max.z)));
                if (first) { b = new Bounds(corner, Vector3.zero); first = false; }
                else b.Encapsulate(corner);
            }
        }
        return b;
    }

    // ---------------------------------------------------------------- exits & fence

    // 탈출구: 문틀(MO_ExitGate) + 닫힌 문(Door_ — 문짝 두 장, 열리면 땅으로 내려감) + 기둥·문 위 충돌 + 울타리 밖 받침(양옆·뒤 막음) + 트리거 자리.
    private static void BuildExits(Transform map)
    {
        Transform exits = Group(map, "Exits");
        for (int i = 0; i < 4; i++)
        {
            Vector3 d = Polar(1f, RoadDeg[i]);
            string key = ExitNames[i].Substring(5);
            var root = new GameObject(ExitNames[i]).transform;
            root.SetParent(exits, false);
            root.SetPositionAndRotation(d * MapRadius, Quaternion.LookRotation(-d));
            Model("MO_ExitGate", root, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), "Model"); // 문틀 앞(모델 −Z)이 맵 안쪽
            var door = new GameObject("Door_" + key).transform;
            door.SetParent(root, false);
            var doorBox = door.gameObject.AddComponent<BoxCollider>();
            doorBox.center = new Vector3(0f, ExitHeight / 2f, 0f);
            doorBox.size = new Vector3(ExitOpening, ExitHeight, 0.8f);
            Model("MO_ExitGateDoorL", door, new Vector3(-ExitOpening / 2f, 0f, 0f), Quaternion.identity);
            Model("MO_ExitGateDoorR", door, new Vector3(ExitOpening / 2f, 0f, 0f), Quaternion.identity);
            // 레버(겉모습): 울타리 밖 받침 옆 — 안쪽에 두면 울타리와 사이에 좁은 틈이 생긴다
            GameObject lever = Model("MO_ExitLever", root, new Vector3(ExitOpening / 2f + ExitPostWidth + 1.4f, 0f, -2.6f), Quaternion.identity);
            if (lever != null) Model("MO_ExitLeverArm", lever.transform, new Vector3(0f, 1.15f, 0f), Quaternion.Euler(-35f, 0f, 0f));

            float postX = ExitOpening / 2f + ExitPostWidth / 2f;
            LocalBox(root, "COL_PostL", new Vector3(-postX, 5f, 0f), new Vector3(ExitPostWidth, 10f, 2.2f));
            LocalBox(root, "COL_PostR", new Vector3(postX, 5f, 0f), new Vector3(ExitPostWidth, 10f, 2.2f));
            LocalBox(root, "COL_Lintel", new Vector3(0f, (ExitHeight + 10f) / 2f, 0f), new Vector3(ExitOpening, 10f - ExitHeight, 2.2f));
            float wallX = ExitOpening / 2f + ExitPostWidth + 0.4f;
            LocalBox(root, "COL_PadL", new Vector3(-wallX, 3f, -PadDepth / 2f - 0.5f), new Vector3(0.8f, 6f, PadDepth));
            LocalBox(root, "COL_PadR", new Vector3(wallX, 3f, -PadDepth / 2f - 0.5f), new Vector3(0.8f, 6f, PadDepth));
            LocalBox(root, "COL_PadBack", new Vector3(0f, 3f, -PadDepth - 0.5f), new Vector3(wallX * 2f + 0.8f, 6f, 0.8f));
            for (int k = 0; k < 3; k++)   // 받침 양옆 울타리 겉모습
                foreach (int s in new[] { -1, 1 })
                    Spawn("MO_Fence", root, root.TransformPoint(new Vector3(s * wallX, 0f, -1.9f - k * 3.36f)), root.rotation * Quaternion.Euler(0f, 90f, 0f), false);
            Spawn("MO_Fence", root, root.TransformPoint(new Vector3(-1.7f, 0f, -PadDepth - 0.5f)), root.rotation, false);
            Spawn("MO_Fence", root, root.TransformPoint(new Vector3(1.7f, 0f, -PadDepth - 0.5f)), root.rotation, false);
            var trigger = new GameObject("ExitTrigger_" + key).transform;
            trigger.SetParent(root, false);
            trigger.localPosition = new Vector3(0f, 1f, -4f);
            BuildExitOpenLook(root, key);
            Take(d * MapRadius, 7f);
            // 닫혀 있어도 자리가 보이게 안쪽 양옆 가로등(탈출구 루트 밖 — 열린 탈출구 투시 테두리에 들지 않게)
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 p = root.TransformPoint(new Vector3(s * (ExitOpening / 2f + ExitPostWidth + 2.2f), 0f, 4f));
                if (Spawn("MO_StreetLamp", Group(map, "Roads"), p, Quaternion.identity) != null) Take(p, 0.8f);
            }
        }
    }

    // 열린 탈출구 표시(ExitOpenLook_ — EscapeGate가 열릴 때 켠다, §37 L1): 받침 위 하늘까지 초록 빛기둥(안개 무시 — 맵 반대쪽에서도 보인다),
    // 문틀 안쪽 빛 띠, 바닥 빛 원, 초록 점광원. 충돌 없음.
    private static void BuildExitOpenLook(Transform root, string key)
    {
        var look = new GameObject("ExitOpenLook_" + key).transform;
        look.SetParent(root, false);
        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"), quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        Material core = BeaconMaterial("CUR_ExitBeamCore", ExitColor * 1.6f, 0f, 1.2f, 1.2f);
        Material haze = BeaconMaterial("CUR_ExitBeamHaze", ExitColor * 0.55f, 0f, 0.8f, 2.5f);
        Material frame = BeaconMaterial("CUR_ExitFrame", ExitColor * 1.8f, 0f, 0.1f, 0.1f);
        Material ground = BeaconMaterial("CUR_ExitGround", ExitColor * 1.1f, 1f, 1.5f, 1f);
        BeaconPart(look, "Beam_Core", cylinder, core, new Vector3(0f, ExitBeamHeight / 2f, -4f), Quaternion.identity, new Vector3(1.8f, ExitBeamHeight / 2f, 1.8f));
        BeaconPart(look, "Beam_Haze", cylinder, haze, new Vector3(0f, ExitBeamHeight / 2f, -4f), Quaternion.identity, new Vector3(6f, ExitBeamHeight / 2f, 6f));
        BeaconPart(look, "Ground_Glow", quad, ground, new Vector3(0f, 0.1f, -1f), Quaternion.Euler(90f, 0f, 0f), new Vector3(14f, 14f, 1f));
        foreach (int s in new[] { -1, 1 })
            BeaconPart(look, s < 0 ? "Frame_L" : "Frame_R", quad, frame, new Vector3(s * (ExitOpening / 2f + 0.2f), ExitHeight / 2f, 1.2f), Quaternion.identity, new Vector3(0.4f, ExitHeight, 1f));
        BeaconPart(look, "Frame_Top", quad, frame, new Vector3(0f, ExitHeight + 0.2f, 1.2f), Quaternion.identity, new Vector3(ExitOpening + 0.8f, 0.4f, 1f));
        var light = new GameObject("Light_Exit").AddComponent<Light>();
        light.transform.SetParent(look, false);
        light.transform.localPosition = new Vector3(0f, 4f, 2.5f);
        light.type = LightType.Point;
        light.range = 26f;
        light.intensity = 4.5f;
        light.color = ExitColor;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
    }

    private static void BeaconPart(Transform parent, string name, Mesh mesh, Material material, Vector3 localPos, Quaternion localRot, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // Beacon 재질(Generated/에 두고 다시 만들 때 값만 고친다)
    private static Material BeaconMaterial(string name, Color color, float radial, float fadePower, float edgePower)
    {
        string path = $"{GeneratedFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(BeaconShader));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find(BeaconShader);
        m.SetColor("_Color", color);
        m.SetFloat("_Radial", radial);
        m.SetFloat("_FadePower", fadePower);
        m.SetFloat("_EdgePower", edgePower);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 둥근 울타리: 겉모습은 MO_Fence를 이어 세우고(충돌 없음), 충돌은 겹친 상자 고리(탈출구 자리만 비움 — 문 기둥이 막는다).
    private static void BuildFence(Transform map)
    {
        Transform fence = Group(map, "Fence");
        Transform cols = Group(fence, "Colliders");
        float r = MapRadius;
        float exitHalf = ExitOpening / 2f + ExitPostWidth;
        int segments = Mathf.RoundToInt(2f * Mathf.PI * (r + 0.4f) / 3.36f);
        for (int i = 0; i < segments; i++)
        {
            float a = (i + 0.5f) * 360f / segments;
            if (RoadDeg.Any(e => Mathf.Abs(Mathf.DeltaAngle(a, e)) * Mathf.Deg2Rad * r < exitHalf + 2.6f)) continue;
            Vector3 p = Polar(r + 0.4f, a);
            Spawn("MO_Fence", fence, p, FaceCenter(p), false);
        }
        int colSegments = 240;
        float step = 360f / colSegments;
        for (int i = 0; i < colSegments; i++)
        {
            float a0 = i * step, a1 = a0 + step;
            float lo = a0, hi = a1;
            bool skip = false;
            foreach (float center in RoadDeg)
            {
                float half = exitHalf / r * Mathf.Rad2Deg;
                float d0 = Mathf.DeltaAngle(center, a0), d1 = Mathf.DeltaAngle(center, a1);
                if (d0 >= -half && d1 <= half) skip = true;
                else if (d0 < -half && d1 > -half && d1 <= half) hi = a1 - (d1 + half);
                else if (d0 >= -half && d0 < half && d1 > half) lo = a0 + (half - d0);
            }
            if (skip) continue;
            float mid = (lo + hi) / 2f, span = Mathf.DeltaAngle(lo, hi);
            Vector3 p = Polar(r + FenceThickness / 2f, mid);
            var t = new GameObject($"COL_Fence_{i:000}").transform;
            t.SetParent(cols, false);
            t.SetPositionAndRotation(p + Vector3.up * FenceHeight / 2f, Quaternion.LookRotation(p));
            float chord = 2f * r * Mathf.Sin(span * Mathf.Deg2Rad / 2f) + 0.2f;
            t.gameObject.AddComponent<BoxCollider>().size = new Vector3(chord, FenceHeight, FenceThickness);
        }
    }

    // ---------------------------------------------------------------- rides

    private static int BuildRides(Scene scene, Transform map)
    {
        Transform rides = Group(map, "Rides");
        int n = 0;
        Vector3 v = Polar(VikingAt.x, VikingAt.y);
        GameObject viking = Spawn("CUR_Viking", rides, v, FrontToCenter(v)); // 계단이 맵 안쪽
        if (viking != null) { n++; Take(v, 19f); }
        Vector3 g = Polar(GyroAt.x, GyroAt.y);
        GameObject gyro = Spawn("CUR_GyroDrop", rides, g, FrontToCenter(g));
        if (gyro != null) { n++; Take(g, 16f); }

        Transform carousel = rides.Find(CarnivalRidesBuilder.CarouselName);
        if (carousel != null)
        {
            Vector3 c = Polar(CarouselAt.x, CarouselAt.y);
            carousel.position = c;
            Take(c, CarnivalRidesBuilder.CarouselRadius + 2f);
            Transform amb = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "AMB_Carousel");
            if (amb != null) amb.position = c + Vector3.up * 4f;
            n++;
        }
        if (PlaceCoaster(rides)) n++;
        return n;
    }

    // 롤러코스터(트랙 + 입구): 테스트 씬의 서로 위치를 그대로 두고, 트랙 길이가 둘레 방향, 입구가 맵 안쪽이 되게 돌려 서쪽 가장자리로 옮긴다.
    // 트랙 둘레는 네 변 모두 울타리 + 속이 찬 막음(레일 밑 바닥이 아무도 못 가는 빈터가 되지 않게).
    private static bool PlaceCoaster(Transform rides)
    {
        Transform coaster = rides.Find(CarnivalRidesBuilder.CoasterName), entrance = rides.Find(CarnivalRidesBuilder.EntranceName);
        if (coaster == null || entrance == null) return false;
        Transform oldFence = coaster.Find("Fence");
        if (oldFence != null) Object.DestroyImmediate(oldFence.gameObject);
        Vector3 entranceLocal = coaster.InverseTransformPoint(entrance.position);
        Quaternion entranceRel = Quaternion.Inverse(coaster.rotation) * entrance.rotation;
        float a = CoasterAt.y;
        Vector3 radial = Polar(1f, a), tangent = new Vector3(-radial.z, 0f, radial.x);
        Quaternion best = Quaternion.identity;
        float bestDot = float.MaxValue;
        foreach (int s in new[] { -1, 1 })
        {
            Quaternion rot = Quaternion.LookRotation(Vector3.Cross(tangent * s, Vector3.up)); // 로컬 +X(트랙 길이) = s × 둘레 방향
            float dot = Vector3.Dot(rot * entranceLocal, radial);
            if (dot < bestDot) { bestDot = dot; best = rot; }
        }
        Vector3 offset = best * entranceLocal;
        float radialShift = Vector3.Dot(offset, radial) / 2f;
        Vector3 trackPos = Polar(CoasterAt.x - radialShift, a);
        coaster.SetPositionAndRotation(trackPos, best);
        entrance.SetPositionAndRotation(trackPos + offset, best * entranceRel);
        Physics.SyncTransforms();

        // 울타리: 트랙 범위(로컬) 둘레 1.5 m
        Bounds track = LocalBounds(coaster.Find("CoasterTrack") ?? coaster);
        Vector2 half = new Vector2(track.extents.x + 1.5f, track.extents.z + 1.5f);
        Vector3 center = new Vector3(track.center.x, 0f, track.center.z);
        Transform fence = Group(coaster, "Fence");
        foreach (int s in new[] { -1, 1 })
        {
            LocalBox(fence, $"COL_FenceX{(s < 0 ? "A" : "B")}", center + new Vector3(0f, RideFenceHeight / 2f, s * half.y), new Vector3(half.x * 2f + 0.4f, RideFenceHeight, 0.4f));
            LocalBox(fence, $"COL_FenceZ{(s < 0 ? "A" : "B")}", center + new Vector3(s * half.x, RideFenceHeight / 2f, 0f), new Vector3(0.4f, RideFenceHeight, half.y * 2f + 0.4f));
            for (float x = -half.x + 1.68f; x <= half.x - 1.6f; x += 3.36f)
                Spawn("MO_Fence", fence, coaster.TransformPoint(center + new Vector3(x, 0f, s * half.y)), coaster.rotation, false);
            for (float z = -half.y + 1.68f; z <= half.y - 1.6f; z += 3.36f)
                Spawn("MO_Fence", fence, coaster.TransformPoint(center + new Vector3(s * half.x, 0f, z)), coaster.rotation * Quaternion.Euler(0f, 90f, 0f), false);
        }
        LocalBox(fence, "COL_RideBlock", center + new Vector3(0f, RideFenceHeight / 2f, 0f), new Vector3(half.x * 2f, RideFenceHeight, half.y * 2f));
        Vector3 worldCenter = coaster.TransformPoint(center);
        Take(worldCenter, new Vector2(half.x, half.y).magnitude + 1f);
        Bounds ent = LocalBounds(entrance);
        Take(entrance.TransformPoint(ent.center), new Vector2(ent.extents.x, ent.extents.z).magnitude + 1f);
        return true;
    }

    // ---------------------------------------------------------------- roads & quadrants

    // 큰길 양옆: 가로등과 깃발 줄을 번갈아, 둥근 산책길: 가로등. 사분면마다 이정표(술래방 모서리 바깥).
    private static int BuildRoads(Transform map)
    {
        Transform roads = Group(map, "Roads");
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Polar(1f, RoadDeg[i]), perp = new Vector3(dir.z, 0f, -dir.x);
            Quaternion along = Quaternion.FromToRotation(Vector3.right, dir);
            int k = 0;
            for (float t = 20f; t < MapRadius - 14f; t += 9f, k++)
            {
                if (Mathf.Abs(t - RingRadius) < RingHalf + 3f) continue;
                foreach (int s in new[] { -1, 1 })
                {
                    Vector3 p = dir * t + perp * (s * RoadRow);
                    bool lamp = k % 2 == 0;
                    if (Spawn(lamp ? "MO_StreetLamp" : "MO_Bunting", roads, p, lamp ? Quaternion.identity : along) != null) n++;
                    Take(p, 0.8f);
                    if (!lamp) { Take(p + dir * 4f, 0.6f); Take(p - dir * 4f, 0.6f); }
                }
            }
        }
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f + 7.5f;
            if (RoadDeg.Any(e => Mathf.Abs(Mathf.DeltaAngle(a, e)) < 6f)) continue;
            Vector3 p = Polar(RingRadius + RingHalf + 1.5f, a);
            if (!IsFree(p, 1f)) continue;
            if (Spawn("MO_StreetLamp", roads, p, Quaternion.identity) != null) { n++; Take(p, 0.8f); }
        }
        for (int q = 0; q < 4; q++)
        {
            Vector3 p = Polar(19f, QuadDeg[q]);
            if (Spawn("MO_Signpost", roads, p, FrontToCenter(p)) != null) { n++; Take(p, 1.5f); }
        }
        return n;
    }

    private static int BuildQuadrants(Transform map, System.Random rng)
    {
        int n = 0;
        for (int q = 0; q < 4; q++)
        {
            float b = QuadDeg[q];
            Transform group = Group(map, $"Quadrant_Q{q + 1}");
            // 숨을 곳: 텐트 4(앞·뒤 문이 둘레 방향)
            foreach (Vector2 t in Tents)
            {
                Vector3 p = Polar(t, b);
                if (Spawn("MO_Tent", group, p, FaceCenter(p) * Quaternion.Euler(0f, 90f, 0f)) != null) n++;
                Take(p, 5.4f);
                monsterSeeds.Add(p + Vector3.up * 0.1f);
            }
            // 분수 광장: 분수 + 벤치 3 + 가로등 2 + 화단 2
            Vector3 f = Polar(52f, b);
            if (Spawn("MO_Fountain", group, f, Quaternion.identity) != null) n++;
            Take(f, 3f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = f + Polar(7.5f, b + 90f + k * 90f);
                if (Spawn("MO_Bench", group, p, Quaternion.LookRotation(new Vector3(p.x - f.x, 0f, p.z - f.z))) != null) n++; // 앞(−Z)이 분수
                Take(p, 1.3f);
            }
            foreach (float d in new[] { -9f, 9f })
            {
                Vector3 p = Polar(52f, b + d);
                if (Spawn("MO_StreetLamp", group, p, Quaternion.identity) != null) n++;
                Take(p, 0.8f);
            }
            foreach (float r in new[] { 39f, 65f })
            {
                Vector3 p = Polar(r, b);
                if (Spawn("MO_Planter", group, p, Quaternion.Euler(0f, (float)rng.NextDouble() * 45f, 0f)) != null) n++;
                Take(p, 1.6f);
            }
            // 노점 4(맛 수레들)
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = Polar(Stalls[k], b);
                if (Spawn(StallNames[k], group, p, FrontToCenter(p)) != null) n++;
                Take(p, 2f);
            }
            // 매표소: 산책길 안쪽, 건물 양옆
            foreach (float d in new[] { -14f, 14f })
            {
                Vector3 p = Polar(73.5f, b + d);
                if (!IsFree(p, 1.8f)) continue;
                if (Spawn("MO_TicketBooth", group, p, FrontToCenter(p)) != null) n++;
                Take(p, 1.8f);
            }
            n += Scatter(group, b, rng);
        }
        return n;
    }

    // 가로등 더하기(§37 L2·L3) — 사분면을 다 놓은 뒤 빈자리에만:
    //   둥근 산책길 안쪽 줄(바깥 줄과 엇갈려 7.5°마다 한쪽에 가로등) · 술래방 둘레 8(문·이정표를 피해 사분면 방향 ±20°).
    private static int BuildExtraLamps(Transform map)
    {
        Transform roads = Group(map, "Roads"), room = map.Find("MonsterRoom");
        int n = 0;
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f;
            if (RoadDeg.Any(e => Mathf.Abs(Mathf.DeltaAngle(a, e)) < 6f)) continue;
            Vector3 p = Polar(RingRadius - RingHalf - 1.5f, a);
            if (!IsFree(p, LampGap)) continue;
            if (Spawn("MO_StreetLamp", roads, p, Quaternion.identity) != null) { n++; Take(p, 0.8f); }
        }
        if (room != null)
            foreach (float b in QuadDeg)
                foreach (float d in new[] { -20f, 20f })
                {
                    Vector3 p = Polar(RoomLampRadius, b + d);
                    if (Spawn("MO_StreetLamp", room, p, Quaternion.identity) != null) { n++; Take(p, 0.8f); }
                }
        return n;
    }

    // 사분면 안 빈자리에 흩어 놓기(큰길·산책길·건물·텐트·놀이기구와 떨어지게, 서로 4 m 넘게 — 괴물 통로).
    private static int Scatter(Transform group, float baseDeg, System.Random rng)
    {
        (string name, int count)[] list =
        {
            ("MO_Bench", 3), ("MO_StreetLamp", 8), ("MO_CrateBarrel", 4), ("MO_BalloonCart", 2), ("MO_Planter", 1),
            ("MO_Popcorn", 1), ("MO_CottonCandy", 1), ("MO_CherryJuice", 1), ("MO_Candy", 1), ("MO_Bunting", 1),
        };
        int n = 0;
        foreach ((string name, int count) in list)
        {
            float reach = Reach(name);
            int got = 0;
            for (int tries = 0; tries < 4000 && got < count; tries++)
            {
                float r = Mathf.Lerp(24f, MapRadius - 10f - reach, (float)rng.NextDouble());
                float a = baseDeg + Mathf.Lerp(-40f, 40f, (float)rng.NextDouble());
                Vector3 p = Polar(r, a);
                if (Mathf.Abs(p.x) < RoadRow + reach + 4f || Mathf.Abs(p.z) < RoadRow + reach + 4f) continue;
                if (Mathf.Abs(r - RingRadius) < RingHalf + reach + 1f) continue;
                if (!IsFree(p, reach + 4f)) continue;
                Quaternion rot = FrontToCenter(p) * Quaternion.Euler(0f, rng.Next(3) * 30f - 30f, 0f);
                if (Spawn(name, group, p, rot) == null) break;
                Take(p, reach);
                got++;
                n++;
            }
        }
        return n;
    }

    // ---------------------------------------------------------------- floor tiles (visual only)

    // 타일 종류마다 메시 하나(사각 4 m 칸 = 2 m 타일 네 장 또는 4 m 무늬 한 장). 큰길·산책길·탈출구 받침은 기본 타일,
    // 사분면은 바닥 예시처럼 섞는다. 술래방 안은 비운다(방 모델이 바닥을 가진다).
    private static int BuildTiles(Transform map, System.Random rng)
    {
        Transform tiles = Group(map, "Tiles");
        var quads = new Dictionary<string, List<(Vector2 p, float s)>>();
        int skipped = 0;
        void Add(string kind, Vector2 p, float s)
        {
            if (Covered(p, s)) { skipped++; return; }
            // 32 m 조각마다 메시 하나 — 맵 전체 한 덩어리면 물체당 점광원 수 제한으로 가로등·방 빛이 바닥에 닿지 않는다
            string key = $"{kind}_{Mathf.FloorToInt(p.x / TileChunk)}_{Mathf.FloorToInt(p.y / TileChunk)}";
            if (!quads.TryGetValue(key, out var l)) quads[key] = l = new List<(Vector2, float)>();
            l.Add((p, s));
        }
        string Pick((string[] mix, float[] weight, string pattern) mix)
        {
            float t = (float)rng.NextDouble();
            for (int i = 0; i < mix.mix.Length; i++) { t -= mix.weight[i]; if (t <= 0f) return mix.mix[i]; }
            return mix.mix[mix.mix.Length - 1];
        }
        Physics.SyncTransforms();
        // 재료 건물 방 바닥이 다 덮는 칸도 비운다(방 바닥 0.02~0.05 m가 타일과 겹쳐 깜빡이지 않게, 방은 제 바닥이 보인다)
        bool RoomFloor(float x, float z) => Physics.Raycast(new Vector3(x, 5f, z), Vector3.down, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                                            && hit.collider.name.StartsWith("Interior") && hit.point.y < 0.1f;
        bool Covered(Vector2 p, float size)
        {
            for (int a = 0; a <= 2; a++)
                for (int b = 0; b <= 2; b++)
                    if (!RoomFloor(p.x + size * a / 2f, p.y + size * b / 2f)) return false;
            return true;
        }
        bool Hole(Vector3 w) => new Vector2(w.x, w.z).magnitude < RoomInner - 2.9f; // 4 m 칸 전체가 술래방 바닥 안
        int count = Mathf.CeilToInt((MapRadius + PadDepth + 2f) / 4f);
        for (int i = -count; i < count; i++)
            for (int j = -count; j < count; j++)
            {
                var c = new Vector2(i * 4f + 2f, j * 4f + 2f);
                float r = c.magnitude;
                bool road = Mathf.Abs(c.x) < RoadHalf + 1f || Mathf.Abs(c.y) < RoadHalf + 1f;
                if (r > MapRadius + 1f && !(road && r < MapRadius + PadDepth + 1.5f && (Mathf.Abs(c.x) < 4f || Mathf.Abs(c.y) < 4f))) continue;
                if (Hole(new Vector3(c.x, 0f, c.y))) continue;
                bool ring = Mathf.Abs(r - RingRadius) < RingHalf + 0.5f;
                if (road || ring)
                {
                    for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++) Add("FL_Basic", new Vector2(i * 4f + a * 2f, j * 4f + b * 2f), 2f);
                    continue;
                }
                string quad = c.x < 0f ? (c.y > 0f ? "Q1" : "Q3") : (c.y > 0f ? "Q2" : "Q4");
                var mix = QuadFloor[quad];
                if (rng.NextDouble() < 0.035) { Add(mix.pattern, new Vector2(i * 4f, j * 4f), 4f); continue; }
                for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++) Add(Pick(mix), new Vector2(i * 4f + a * 2f, j * 4f + b * 2f), 2f);
            }
        int total = 0;
        foreach (string old in AssetDatabase.FindAssets("Floor_ t:Mesh", new[] { GeneratedFolder })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(old));
        foreach (var kv in quads)
        {
            string kind = kv.Key.Substring(0, kv.Key.IndexOf('_', 3));
            Mesh mesh = TileMesh(kv.Key, kv.Value, rng);
            var go = new GameObject("Tiles_" + kv.Key);
            go.transform.SetParent(tiles, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{CarnivalMapAssets.ModelRoot}/Floor/Materials/M_{kind}.mat");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            total += kv.Value.Count;
        }
        Debug.Log($"[CarnivalMap] tiles {total} in {quads.Count} chunks, {skipped} under room floors");
        return total;
    }

    private static Mesh TileMesh(string kind, List<(Vector2 p, float s)> cells, System.Random rng)
    {
        var v = new List<Vector3>(cells.Count * 4);
        var uv = new List<Vector2>(cells.Count * 4);
        var tri = new List<int>(cells.Count * 6);
        Vector2[] corners = { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        foreach ((Vector2 p, float s) in cells)
        {
            int rot = rng.Next(4), i0 = v.Count;
            for (int k = 0; k < 4; k++)
            {
                Vector2 c = corners[k];
                v.Add(new Vector3(p.x + c.x * s, TileY, p.y + c.y * s));
                uv.Add(corners[(k + rot) % 4]);
            }
            tri.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
        }
        var mesh = new Mesh { name = "Floor_" + kind, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        string path = $"{GeneratedFolder}/Floor_{kind}.asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) AssetDatabase.CreateAsset(mesh, path);
        else
        {
            EditorUtility.CopySerialized(mesh, saved);
            Object.DestroyImmediate(mesh);
            mesh = saved;
        }
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // ---------------------------------------------------------------- markers

    private static int BuildMarkers(Transform map)
    {
        Transform markers = Group(map, "Markers");
        bool Clear(Vector3 p, float radius)
        {
            if (!IsFree(p, radius)) return false;
            if (Mathf.Abs(new Vector2(p.x, p.z).magnitude - RingRadius) < RingHalf) return false;
            if (Physics.CheckCapsule(p + Vector3.up * (radius + 0.1f), p + Vector3.up * (radius + 2.4f), radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            return Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.point.y < 0.3f;
        }
        int n = 0;
        foreach ((string name, Vector3 pos) in CandyForestLayout.PlaceMarkers(Clear))
        {
            var t = new GameObject(name).transform;
            t.SetParent(markers, false);
            t.position = pos;
            n++;
        }
        return n;
    }

    private const float WitchBeyondFence = 75f;

    private static void MoveWitchAnchor(Scene scene)
    {
        Transform anchor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "WITCH_Anchor");
        if (anchor == null) return;
        var flat = new Vector2(anchor.position.x, anchor.position.z);
        if (flat.sqrMagnitude < 1f) flat = new Vector2(-1f, -1f);
        Vector2 at = flat.normalized * (MapRadius + WitchBeyondFence);
        anchor.position = new Vector3(at.x, anchor.position.y, at.y);
    }

    private static void MoveSpawn(Scene scene, string spawnName, Transform map, string markerName)
    {
        GameObject spawn = scene.GetRootGameObjects().FirstOrDefault(g => g.name == spawnName);
        Transform marker = map.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == markerName);
        if (spawn != null && marker != null) spawn.transform.position = marker.position + Vector3.up * 0.5f;
    }

    // ---------------------------------------------------------------- lights

    // 테스트 맵 점광원은 끄고(옛 자리), 모든 가로등·회전목마·놀이기구·술래방(안쪽·문 위)에 점광원. 건물·텐트 안 빛은 프리팹에 있다.
    // 열린 탈출구 빛은 ExitOpenLook_ 안에 있다(열릴 때만 켜짐). 주변광·안개는 이 씬만 조금 밝게(§37 L2).
    private static int BuildLights(Scene scene, Transform map)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = SceneAmbient;
        RenderSettings.ambientSkyColor = SceneAmbient;
        RenderSettings.fogDensity = SceneFogDensity;
        GameObject lighting = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MapLighting");
        Transform old = lighting != null ? lighting.transform.Find("PointLights") : null;
        if (old != null) old.gameObject.SetActive(false);
        if (lighting != null)
            foreach (ReflectionProbe probe in lighting.GetComponentsInChildren<ReflectionProbe>(true))
                if (probe.name.Contains("Water")) probe.gameObject.SetActive(false); // 옛 연못 자리
        Transform parent = lighting != null ? lighting.transform : map;
        Transform lights = parent.Find("CUR_Lights");
        if (lights != null) Object.DestroyImmediate(lights.gameObject);
        lights = new GameObject("CUR_Lights").transform;
        lights.SetParent(parent, false);
        int n = 0;
        foreach (Transform t in map.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            if (source == null) continue;
            if (source.name == "MO_StreetLamp")
            { Point(lights, t.position + Vector3.up * LampLightHeight, LampRange, LampIntensity, LampColor); n++; }
            else if (source.name == "CUR_Viking" || source.name == "CUR_GyroDrop")
            { Point(lights, t.position + Vector3.up * 9f, 26f, 1.4f, new Color(1f, 0.45f, 0.6f)); n++; }
        }
        Transform carousel = map.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == CarnivalRidesBuilder.CarouselName);
        if (carousel != null) { Point(lights, carousel.position + Vector3.up * 8f, 22f, 1.6f, new Color(1f, 0.75f, 0.55f)); n++; }
        // 술래방: 안쪽 빛 + 문마다 문 위 붉은 빛(문 자리가 멀리서 보이게)
        Point(lights, Vector3.up * 6f, 15f, 2.2f, RoomColor); n++;
        for (int i = 0; i < 4; i++) { Point(lights, Polar(RoomRadius + 1.2f, RoadDeg[i]) + Vector3.up * 4.5f, 9f, 3.2f, RoomColor); n++; }
        return n;
    }

    private static void Point(Transform parent, Vector3 pos, float range, float intensity, Color color)
    {
        var go = new GameObject("Light_" + parent.childCount.ToString("000"));
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = intensity;
        l.color = color;
        l.shadows = LightShadows.None;
    }

    // ---------------------------------------------------------------- recipe & build settings

    // 건물 간판이 재료를 말하므로 Q1~Q4 재료를 고정한다(안전벨트·배터리·수리 도구·빨간 버튼).
    private static string SetRecipe()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<EscapeRecipeSO>("Assets/03. SO/Escape/Recipes/Recipe_CursedCandyCarnival.asset");
        if (recipe == null) return "recipe missing";
        string[] items = { "Item_Seatbelt", "Item_Battery", "Item_Toolbox", "Item_RedButton" };
        ItemSO[] materials = items.Select(n => AssetDatabase.FindAssets(n + " t:ItemSO").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemSO>).FirstOrDefault(i => i != null && i.name == n)).ToArray();
        if (materials.Any(m => m == null)) return "recipe items missing";
        recipe.EditorSetBuildingMaterials(materials, true);
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return "recipe fixed " + string.Join(",", materials.Select(m => m.ItemId));
    }

    public static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        foreach (EditorBuildSettingsScene s in scenes)
            if (s.path == TestMapBuilder.ScenePath(MapName)) s.enabled = false;
        EditorBuildSettings.scenes = scenes.ToArray();

        var settings = AssetDatabase.LoadAssetAtPath<GameSettingsSO>("Assets/Resources/GameSettings.asset");
        if (settings == null) return;
        var so = new SerializedObject(settings);
        SerializedProperty list = so.FindProperty("gameMapScenes");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).stringValue == TestMapBuilder.SceneName(MapName)) list.GetArrayElementAtIndex(i).stringValue = MapName;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- passability (CM7)

    public const string PassabilityReport = "Temp/CursedCandyCarnival_Passability.txt";

    [MenuItem(MenuRoot + "Check Cursed Candy Carnival Passability (5 body sizes)")]
    public static void CheckPassabilityMenu() => Debug.Log(CheckPassability());

    // 몸 크기 5가지로 통행 검사(캔디숲과 같은 기준). 술래방은 괴물 전용, 울타리 밖(탈출구 받침 제외)은 놀이 공간이 아님.
    // 괴물 출발점: 술래방 문 바깥 + 텐트·건물 방 가운데(실제 괴물 몸(반경 0.31 m)은 텐트 문 2.8 m를 지나간다 — §34.1·§35.2 Play Mode,
    // 격자 기준 괴물(반경 1.8 m)은 못 지나가므로 안에서 출발시킨다).
    public static string CheckPassability()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var seeds = RoadDeg.Select(d => Polar(RoomRadius + 1.5f, d)).ToList();
        foreach (Transform t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
        {
            if (t.name.StartsWith("Building_Q")) seeds.Add(t.position + Vector3.up * 0.1f);
            if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == "MO_Tent")
                seeds.Add(t.position + Vector3.up * 0.1f);
        }
        var options = new MapPassabilityCheck.Options
        {
            Half = MapRadius + 12f,
            Bodies = MapPassabilityCheck.DesignRange,
            CookieForbidden = p => new Vector2(p.x, p.z).magnitude < RoomRadius,
            IgnorePocket = p => new Vector2(p.x, p.z).magnitude > MapRadius + 0.5f,
            Targets = Targets(scene).ToArray(),
            MonsterSeeds = seeds.ToArray(),
            MapImage = "Temp/CursedCandyCarnival_PassabilityMap.png",
        };
        MapPassabilityCheck.Result[] results = MapPassabilityCheck.Run(scene, options);
        var sb = new System.Text.StringBuilder();
        foreach (MapPassabilityCheck.Result r in results) sb.Append(r.Clean ? "CLEAN " : "ISSUES ").Append(r);
        System.IO.Directory.CreateDirectory("Temp");
        System.IO.File.WriteAllText(PassabilityReport, sb.ToString());
        return $"[CarnivalMap] passability: {results.Count(r => r.Clean)}/{results.Length} clean — {PassabilityReport}";
    }

    private static IEnumerable<(string, Vector3)> Targets(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("CHEST_Slot_") || n.StartsWith("RocketSpawn_") || n.StartsWith("RainbowSpawn_") || n.StartsWith("Spawn_Cookie_")
                    || n.StartsWith("Repair_Point") || n.StartsWith("ExitTrigger_") || n == "Zone_Inside")
                    yield return (n + (n.StartsWith("Repair_Point") || n == "Zone_Inside" ? "@" + t.parent.parent.name + "/" + t.parent.name : ""), t.position);
            }
    }
}
