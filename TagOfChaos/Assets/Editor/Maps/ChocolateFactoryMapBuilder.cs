using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 초콜릿 공장 실제 모델 맵 씬(Request1009Plan.md §42). Test_ChocolateFactory를 복사한 ChocolateFactory 씬에서 놀이 공간을
// 새 모델로 바꾼다(놀이공원 CarnivalMapBuilder와 같은 순서·규칙, 반지름 155 m):
//   사분면별 바닥 타일(§40 1 m 타일, 바닥 예시 광장의 섞는 법) · 초콜릿 술래방(문 12·3·6·9시) · 재료 건물 4(Building_Qn, §41) ·
//   일반 건물 4(가장자리 큰 볼거리 — 가마솥·공장·톱니바퀴와 분쇄기·초콜릿 바) · 출구 게이트 4 · 둘레 펜스 ·
//   큰길·둥근 산책길 꾸미기 · 사분면 케이크 광장·숨는 창고 4·작업장·소품 · 상자/로켓/무지개/스폰 자리 · 조명.
// 카메라·관리자·마녀·환경음은 테스트 씬 그대로. 여러 번 실행해도 같은 결과(고정 난수).
public static class ChocolateFactoryMapBuilder
{
    public const string MapName = "ChocolateFactory";
    public const string ScenePath = "Assets/Scenes/Maps/" + MapName + ".unity";
    public const string GeneratedFolder = ChocolateFactoryAssets.GeneratedFolder;
    public const string RecipePath = "Assets/03. SO/Escape/Recipes/Recipe_ChocolateFactory.asset";
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";

    public const float MapRadius = 155f;
    public const float RoadHalf = 5f;          // 큰길 반 폭(타일)
    public const float RoadRow = 7.5f;         // 큰길 양옆 가로등·배너 줄
    public const float RingRadius = 80f, RingHalf = 3f; // 둥근 산책길
    public const float BuildingRadius = 100f;  // 재료 건물(놀이공원과 같음)
    public const float LandmarkRadius = 124f;  // 일반 건물(가장자리)
    public const float RoomRadius = 12.55f;    // 술래방 벽 바깥(CHO_MonsterRoom — MO_MonsterRoom과 같은 치수)
    public const float RoomInner = 11.9f;
    public const float RoomDoorWidth = 4f;
    public const float GuardInnerHalf = 8f;
    private const float WallHeight = 9f;
    private const float FenceHeight = 6f, FenceThickness = 0.8f;
    private const float FenceStep = 3.3f;      // 펜스 한 칸(CHO_Fence 2.06 m × 1.6)
    private static readonly Vector3 FenceScale = new Vector3(1.6f, 1.4f, 1.4f);
    private const float ExitOpening = 5.6f, ExitPostWidth = 1.6f, ExitHeight = 5.6f, PadDepth = 10f;
    private static readonly RoundMapKit.ExitSize Exit = new RoundMapKit.ExitSize { Opening = ExitOpening, PostWidth = ExitPostWidth, Height = ExitHeight, PadDepth = PadDepth };
    private const float FloorSize = 2f * (MapRadius + 80f);
    private const float TileChunk = 32f;
    private const float TileY = 0.06f;          // 창고 바닥(0.05)·술래방 판(0.04)보다 위, 겹치는 칸은 깔지 않는다
    private const float GroundUnderY = -0.3f;   // 보이는 바닥 아래판(창살 구덩이 0.22 m 아래가 맨땅에 가리지 않게 — 걷는 판은 0 m 그대로)
    private const float FarBackdropClear = MapRadius + 45f;
    private const float WitchBeyondFence = 75f;

    // 조명: 가로등마다 점광원, 열린 탈출구 빛기둥, 술래방 둘레·문 빛. 주변광은 따뜻한 갈색, 안개는 테스트 씬보다 조금 옅게.
    private const float LampLightHeight = 3.5f, LampRange = 15f, LampIntensity = 2.4f;
    private const float LampGap = 3f;
    private const float RoomLampRadius = 16f;
    private static readonly Color LampColor = new Color(1f, 0.74f, 0.48f);
    private static readonly Color ExitColor = new Color(0.1f, 1f, 0.3f);
    private static readonly Color RoomColor = new Color(1f, 0.28f, 0.22f);
    private static readonly Color SceneAmbient = new Color(0.36f, 0.27f, 0.24f);
    private const float SceneFogDensity = 0.012f;

    // 사분면(Q1 북서 기어, Q2 북동 마카롱 바퀴, Q3 남서 초콜릿 원유, Q4 남동 쿠키 바퀴) — 각도는 +x에서 +z 쪽으로
    public static readonly float[] QuadDeg = { 135f, 45f, 225f, 315f };
    public static readonly float[] RoadDeg = { 90f, 0f, 270f, 180f };          // N, E, S, W
    public static readonly string[] ExitNames = { "Exit_N", "Exit_E", "Exit_S", "Exit_W" };
    public static readonly string[] RecipeItems = { "Item_Gear", "Item_MacaronWheel", "Item_ChocolateOil", "Item_CookieWheel" };
    // 일반 건물(사분면 가장자리, 시계 방향 10·1·7·4시 — 2026-10-27 사용자 요청. 각도는 +x에서 +z 쪽, 12시 = 90°, 한 시간 = 30°)
    public static readonly (string key, float deg)[] Landmarks = { ("Cauldron", 150f), ("Factory", 60f), ("Cogworks", 240f), ("ChocoBar", 330f) };
    private static readonly string[] KeepInMap = { "Background", WitchPath.FarBackdropName, "Ground" };   // GroundUnder는 다시 만든다

    // 숨는 창고(사분면마다 4): (반지름, 사분면 방향에서 각도 차이) — 놀이공원 텐트 자리
    public static readonly Vector2[] Sheds = { new Vector2(46f, -22f), new Vector2(46f, 22f), new Vector2(68f, -28f), new Vector2(68f, 28f) };
    public const float ShedReach = 6.2f;       // 창고 지붕 끝까지(9.7 m 정사각 반 대각선 + 여유)
    // 건물 옆 창고(2026-10-28 요청, 사용자 그림 기준): 사분면마다 두 구역에 4개씩. 각도는 사분면 방향(QuadDeg)에서의 차이, 반지름은 맵 가운데에서.
    // A = 재료 건물 바깥쪽(일반 건물 반대편, 큰길 쪽으로 건물을 감싸는 띠), B = 일반 건물 바깥쪽(재료 건물 반대편, 반대쪽 큰길 쪽).
    // 일반 건물은 재료 건물에서 +15°에 있으므로(Landmarks) 네 사분면 모두 같은 규칙으로 놓인다.
    private static readonly (Vector2 deg, Vector2 radius)[] ShedZones = { (new Vector2(-26f, -8f), new Vector2(92f, 135f)), (new Vector2(24f, 38f), new Vector2(100f, 136f)) };
    public const int ShedsPerZone = 4;
    // 작업장(놀이공원 노점 자리)
    private static readonly Vector2[] Works = { new Vector2(30f, -18f), new Vector2(36f, -10f), new Vector2(30f, 18f), new Vector2(36f, 10f) };
    private static readonly string[] WorkNames = { "CHO_FactoryMachine", "CHO_PackingMachine", "CHO_ChocoShelf", "CHO_Forklift" };

    // 바닥(§40 바닥 예시 광장): 기본 판 위에 4 m 칸마다 무늬 하나 — 젤리 2×2, 초콜릿 조각 한 줄, 분쇄기 창살 한 줄(가장자리), 격자 둘(모서리).
    // 사분면마다 섞는 비율이 달라 걸어 보면 어느 사분면인지 바닥으로도 안다.
    public enum Accent { Plain, Jelly, Choco, Grinder, Grid }
    private static readonly Dictionary<string, (Accent a, float w)[]> QuadFloor = new Dictionary<string, (Accent, float)[]>
    {
        { "Q1", new[] { (Accent.Plain, 0.5f), (Accent.Grinder, 0.14f), (Accent.Grid, 0.12f), (Accent.Choco, 0.18f), (Accent.Jelly, 0.06f) } }, // 기어 — 쇠 창살
        { "Q2", new[] { (Accent.Plain, 0.48f), (Accent.Jelly, 0.3f), (Accent.Choco, 0.14f), (Accent.Grid, 0.05f), (Accent.Grinder, 0.03f) } }, // 마카롱 — 분홍 젤리
        { "Q3", new[] { (Accent.Plain, 0.45f), (Accent.Choco, 0.3f), (Accent.Grid, 0.15f), (Accent.Jelly, 0.06f), (Accent.Grinder, 0.04f) } }, // 원유 — 초콜릿 길·녹은 초콜릿 격자
        { "Q4", new[] { (Accent.Plain, 0.5f), (Accent.Choco, 0.16f), (Accent.Jelly, 0.14f), (Accent.Grinder, 0.1f), (Accent.Grid, 0.1f) } },  // 쿠키 — 고루
    };
    public static readonly string[] TileKinds = { "CHO_FL_Basic", "CHO_FL_ChocoPiece", "CHO_FL_Jelly", "CHO_FL_Grinder", "CHO_FL_Grid" };
    private const int Basic = 0, ChocoPiece = 1, Jelly = 2, Grinder = 3, Grid = 4;

    private static readonly RoundMapKit.Placement placed = new RoundMapKit.Placement();
    private static readonly Dictionary<string, float> reachCache = new Dictionary<string, float>();
    // 타일을 깔지 않는 곳(제 바닥이 있는 방·창고): 로컬 직사각형(루트 기준 x·z 범위)
    private static readonly List<(Transform root, Rect rect)> ownFloors = new List<(Transform, Rect)>();

    [MenuItem(MenuRoot + "Build Chocolate Factory Map Scene")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ChocolateFactoryAssets.PrefabPath("Gear")) == null || ChocolateFactoryAssets.LoadProp(ChocolateFactoryAssets.ShedName) == null)
            return "[ChocolateFactoryMap] prefabs missing — run Build Chocolate Factory Buildings first";
        placed.Clear(); reachCache.Clear(); ownFloors.Clear();
        string source = TestMapBuilder.ScenePath(MapName);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(source, ScenePath)) return "[ChocolateFactoryMap] copy failed";
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

        var rng = new System.Random(42);
        int backdrop = BuildBackdrop(map, rng);
        RoundMapKit.SpreadFarBackdrop(map, MapRadius, FarBackdropClear);
        BuildGround(map);
        BuildMonsterRoom(map);
        BuildBuildings(map);
        int landmarks = BuildLandmarks(map);
        int buildingSheds = BuildBuildingSheds(map);
        BuildExits(map);
        BuildFence(map);
        int deco = BuildRoads(map) + BuildQuadrants(map, rng) + BuildExtraLamps(map);
        Physics.SyncTransforms();
        int tiles = BuildTiles(map, rng);
        int markers = RoundMapKit.Markers(map, placed, RingRadius, RingHalf);
        int lights = BuildLights(scene, map);

        string recipe = RoundMapKit.FixRecipe(RecipePath, RecipeItems);
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
        RoundMapKit.MoveSpawn(scene, SceneSpawnPoints.Cookie, map, "Spawn_Cookie_Q1");
        RoundMapKit.MoveWitchAnchor(scene, MapRadius, WitchBeyondFence);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RoundMapKit.AddToBuildSettings(ScenePath, MapName);
        return $"[ChocolateFactoryMap] {ScenePath}: landmarks {landmarks}, decoration {deco}, backdrop {backdrop}, sheds beside buildings {buildingSheds}, tiles {tiles}, markers {markers}, lights {lights} | {recipe} | {escape}";
    }

    // ---------------------------------------------------------------- helpers

    public static Vector3 Polar(float radius, float deg) => RoundMapKit.Polar(radius, deg);
    private static Vector3 Polar(Vector2 radiusDeg, float baseDeg = 0f) => RoundMapKit.Polar(radiusDeg, baseDeg);
    private static Quaternion FaceCenter(Vector3 p) => RoundMapKit.FaceCenter(p);
    private static Quaternion FrontToCenter(Vector3 p) => RoundMapKit.FrontToCenter(p);
    private static Transform Group(Transform parent, string name) => RoundMapKit.Group(parent, name);

    private static GameObject Spawn(string prop, Transform parent, Vector3 pos, Quaternion rot, bool colliders = true)
    {
        GameObject asset = ChocolateFactoryAssets.LoadProp(prop);
        if (asset == null) { Debug.LogWarning($"[ChocolateFactoryMap] prefab {prop} missing"); return null; }
        return RoundMapKit.Spawn(asset, parent, pos, rot, colliders);
    }

    private static GameObject Model(string fbx, Transform parent, Vector3 localPos, Quaternion localRot, string name = null) =>
        RoundMapKit.Model($"{ChocolateFactoryAssets.MapObjectFolder}/{fbx}.fbx", parent, localPos, localRot, name);

    private static float Reach(string prop)
    {
        if (reachCache.TryGetValue(prop, out float cached)) return cached;
        return reachCache[prop] = RoundMapKit.Reach(ChocolateFactoryAssets.LoadProp(prop));
    }

    private static void Take(Vector3 p, float r) => placed.Take(p, r);
    private static bool IsFree(Vector3 p, float r) => placed.IsFree(p, r);

    // ---------------------------------------------------------------- background

    // 옛 공장 배경 중 넓어진 울타리 안이나 바로 밖(20 m)에 걸리는 것은 지우고, 울타리 밖에 큰 공장 기계·배관·상자 더미 고리를 둔다(충돌 없음).
    private static int BuildBackdrop(Transform map, System.Random rng)
    {
        Transform background = map.Find("Background");
        if (background == null) return 0;
        foreach (Transform t in background.Cast<Transform>().ToList())
        {
            Renderer r = t.GetComponentInChildren<Renderer>(true);
            if (r == null) continue;
            var flat = new Vector2(r.bounds.center.x, r.bounds.center.z);
            if (flat.magnitude - new Vector2(r.bounds.extents.x, r.bounds.extents.z).magnitude < MapRadius + 20f) Object.DestroyImmediate(t.gameObject);
        }
        Transform ring = Group(background, "CHO_Backdrop");
        if (ring.GetComponent<WitchBackdropToggle>() == null) ring.gameObject.AddComponent<WitchBackdropToggle>(); // 마녀 연출 때만 보인다
        (string name, float scale)[] pieces = { ("CHO_FactoryMachine", 3.2f), ("CHO_Pipe", 3.5f), ("CHO_PackingMachine", 2.6f), ("CHO_CacaoCrate", 3f), ("CHO_IngredientSack", 3f), ("CHO_Crate", 3.4f) };
        int n = 0;
        for (int i = 0; i < 60; i++)
        {
            float deg = i * 6f + (float)rng.NextDouble() * 2.5f;
            if (RoundMapKit.AngleTo(deg, RoadDeg) < 6f) continue; // 출구 앞은 트인 채로
            var piece = pieces[(i * 7 + rng.Next(3)) % pieces.Length];
            Vector3 p = Polar(MapRadius + 14f + (float)rng.NextDouble() * 22f, deg);
            GameObject go = Spawn(piece.name, ring, p, FaceCenter(p) * Quaternion.Euler(0f, rng.Next(-30, 30), 0f), false);
            if (go == null) continue;
            go.transform.localScale = Vector3.one * piece.scale * (0.85f + (float)rng.NextDouble() * 0.3f);
            n++;
        }
        return n;
    }

    // 걷는 면은 평평한 바닥판 하나(0 m, 충돌). 사각 벽은 지우고(둥근 울타리), 판은 보이지 않게 하고 0.3 m 아래에 보이는 판을 둔다 —
    // 놀이 공간은 타일이 덮고, 창살 타일 구덩이(0.22 m 아래)가 맨땅에 가려지지 않는다.
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
        foreach (Renderer r in floor.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

        // Ground 묶음은 테스트 맵 FBX(축 변환으로 돌아가 있다) — 아래판은 맵 바로 아래에 반듯하게 둔다
        Transform old = map.Find("GroundUnder");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var under = new GameObject("GroundUnder");
        under.transform.SetParent(map, false);
        under.transform.SetPositionAndRotation(new Vector3(0f, GroundUnderY, 0f), Quaternion.identity);
        under.AddComponent<MeshFilter>().sharedMesh = SquareMesh("Floor_GroundUnder", FloorSize, FloorSize);
        var mr = under.AddComponent<MeshRenderer>();
        mr.sharedMaterial = TileMaterial("CHO_FL_Basic");
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(under, StaticEditorFlags.BatchingStatic);
    }

    // 가운데가 원점인 평판(UV 1 m = 1) — 아래판 한 장
    private static Mesh SquareMesh(string name, float sx, float sz)
    {
        var mesh = new Mesh { name = name };
        float hx = sx / 2f, hz = sz / 2f;
        mesh.SetVertices(new[] { new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz) });
        mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(0f, sz), new Vector2(sx, sz), new Vector2(sx, 0f) });
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh, $"{GeneratedFolder}/{name}.asset");
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
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

    private static Material TileMaterial(string kind) => AssetDatabase.LoadAssetAtPath<Material>($"{ChocolateFactoryAssets.FloorFolder}/Materials/M_{kind}.mat");

    // ---------------------------------------------------------------- monster room

    // 초콜릿 술래방(CHO_MonsterRoom, 반지름 12 m, 문 12·3·6·9시 폭 4 m). 벽은 겹친 상자 고리, 문(MonsterDoor_0~3 = 북·동·남·서)은 괴물만 지나간다.
    private static void BuildMonsterRoom(Transform map)
    {
        var room = new GameObject("MonsterRoom").transform;
        room.SetParent(map, false);
        Model("CHO_MonsterRoom", room, Vector3.zero, Quaternion.identity, "Model");
        RoundMapKit.RoomRing(room, RoomInner, RoomRadius, WallHeight, RoomDoorWidth, RoadDeg);
        var doorModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{ChocolateFactoryAssets.MapObjectFolder}/CHO_MonsterRoomDoor.fbx");
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Polar(1f, RoadDeg[i]);
            RoundMapKit.RoomDoor(room, i, dir, RoomInner, RoomRadius, WallHeight, RoomDoorWidth, doorModel);
            RoundMapKit.BeaconPart(room, "DoorGlow_" + i, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), RoundMapKit.BeaconMaterial(GeneratedFolder, "CHO_RoomDoorGlow", RoomColor * 1.2f, 1f, 1.5f, 1f),
                dir * RoomRadius + Vector3.up * 0.1f, Quaternion.Euler(90f, 0f, 0f), new Vector3(7f, 7f, 1f));
        }
        var spawn = new GameObject("Spawn_Monster").transform;
        spawn.SetParent(room, false);
        Take(Vector3.zero, RoomRadius + 2f);
    }

    // ---------------------------------------------------------------- buildings

    // 재료 건물: Building_Q1~Q4 루트(EscapeMapSetup → QuadrantBuilding) 아래 §41 프리팹, 정문(+Z)이 술래방을 본다. 꽂는 곳·수리 자리는 프리팹 안.
    private static void BuildBuildings(Transform map)
    {
        Transform group = Group(map, "Buildings");
        for (int q = 0; q < 4; q++)
        {
            string key = ChocolateFactoryAssets.MaterialBuildings[q];
            Vector3 p = Polar(BuildingRadius, QuadDeg[q]);
            var root = new GameObject($"Building_Q{q + 1}").transform;
            root.SetParent(group, false);
            root.SetPositionAndRotation(p, FaceCenter(p));
            PlaceBuilding(key, root, p);
        }
    }

    // 건물 옆 창고: 사분면마다 ShedZones 두 구역에 ShedsPerZone개씩. 구역 안 후보(3° × 4 m 격자) 중 큰길·산책길·울타리·다른 오브젝트와 겹치지 않는 자리에서
    // 이미 고른 창고와 가장 먼 곳부터 고른다(구역 안에 고루 퍼진다). 재료 건물·일반 건물을 놓은 뒤, 꾸미기보다 먼저 자리를 잡아 소품이 피해 간다.
    private static int BuildBuildingSheds(Transform map)
    {
        int n = 0;
        for (int q = 0; q < 4; q++)
        {
            Transform group = Group(map, $"Quadrant_Q{q + 1}");
            for (int z = 0; z < ShedZones.Length; z++)
            {
                (Vector2 deg, Vector2 radius) = ShedZones[z];
                var candidates = new List<Vector3>();
                for (float d = deg.x; d <= deg.y + 0.01f; d += 3f)
                    for (float r = radius.x; r <= radius.y + 0.01f; r += 4f)
                    {
                        Vector3 p = Polar(r, QuadDeg[q] + d);
                        if (r > MapRadius - 6f - ShedReach) continue;                                                           // 울타리 안
                        if (Mathf.Abs(p.x) < RoadRow + ShedReach + 2f || Mathf.Abs(p.z) < RoadRow + ShedReach + 2f) continue;   // 큰길·가로등 줄
                        if (Mathf.Abs(r - RingRadius) < RingHalf + ShedReach + 1f) continue;                                   // 둥근 산책길
                        candidates.Add(p);
                    }
                Vector3 centre = Polar((radius.x + radius.y) / 2f, QuadDeg[q] + (deg.x + deg.y) / 2f);
                var chosen = new List<Vector3>();
                while (chosen.Count < ShedsPerZone)
                {
                    float best = float.NegativeInfinity;
                    Vector3 pick = default;
                    foreach (Vector3 p in candidates)
                    {
                        if (!IsFree(p, ShedReach + 1f)) continue;
                        float score = chosen.Count == 0 ? -Vector3.Distance(p, centre) : chosen.Min(c => Vector3.Distance(p, c));
                        if (score > best) { best = score; pick = p; }
                    }
                    if (float.IsNegativeInfinity(best)) break;
                    GameObject shed = Spawn(ChocolateFactoryAssets.ShedName, group, pick, FaceCenter(pick) * Quaternion.Euler(0f, 90f, 0f));
                    Take(pick, ShedReach);
                    chosen.Add(pick);
                    if (shed == null) continue;
                    ownFloors.Add((shed.transform, Rect.MinMaxRect(-4.3f, -4.3f, 4.3f, 4.3f)));
                    placed.MonsterSeeds.Add(pick + Vector3.up * 0.1f);
                    n++;
                }
                if (chosen.Count < ShedsPerZone) Debug.LogWarning($"[ChocolateFactoryMap] Q{q + 1} zone {(char)('A' + z)}: only {chosen.Count} sheds fit");
            }
        }
        return n;
    }

    // 일반 건물 4: 사분면 가장자리의 큰 볼거리(문으로 드나들고 안에 숨을 수도 있다). 정문이 맵 가운데를 본다.
    private static int BuildLandmarks(Transform map)
    {
        Transform group = Group(map, "Landmarks");
        int n = 0;
        foreach ((string key, float deg) in Landmarks)
        {
            Vector3 p = Polar(LandmarkRadius, deg);
            var root = new GameObject("Landmark_" + key).transform;
            root.SetParent(group, false);
            root.SetPositionAndRotation(p, FaceCenter(p));
            if (PlaceBuilding(key, root, p)) n++;
        }
        return n;
    }

    private static bool PlaceBuilding(string key, Transform root, Vector3 p)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ChocolateFactoryAssets.PrefabPath(key));
        if (asset == null) { Debug.LogWarning($"[ChocolateFactoryMap] building {key} missing"); return false; }
        GameObject bld = RoundMapKit.Spawn(asset, root, p, root.rotation);
        Bounds local = RoundMapKit.LocalBounds(bld.transform);
        Take(p, new Vector2(local.extents.x, local.extents.z).magnitude * 0.85f);
        ChocolateFactoryAssets.Meta meta = ChocolateFactoryAssets.LoadMeta(key);
        if (meta != null)
        {
            Vector3 lo = new Vector3(meta.room.min[0], 0f, meta.room.min[2]), hi = new Vector3(meta.room.max[0], 0f, meta.room.max[2]);
            ownFloors.Add((bld.transform, Rect.MinMaxRect(lo.x, lo.z, hi.x, hi.z)));
            placed.MonsterSeeds.Add(bld.transform.TransformPoint((lo + hi) / 2f) + Vector3.up * 0.1f); // 방 가운데
        }
        return true;
    }

    // ---------------------------------------------------------------- exits & fence

    // 출구 게이트: 문틀(CHO_ExitGate) + 닫힌 문(Door_ — 문짝 두 장, 열리면 땅으로 내려감) + 기둥·문 위 충돌 + 울타리 밖 받침(양옆·뒤 막음) + 트리거.
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
            Model("CHO_ExitGate", root, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), "Model"); // 문틀 앞(모델 −Z)이 맵 안쪽
            RoundMapKit.ExitDoor(root, key, Exit, $"{ChocolateFactoryAssets.MapObjectFolder}/CHO_ExitGateDoorL.fbx", $"{ChocolateFactoryAssets.MapObjectFolder}/CHO_ExitGateDoorR.fbx");
            // 레버(겉모습): 울타리 밖 받침 옆 — 안쪽에 두면 울타리와 사이에 좁은 틈이 생긴다
            GameObject lever = Model("CHO_ExitLever", root, new Vector3(ExitOpening / 2f + ExitPostWidth + 1.4f, 0f, -2.6f), Quaternion.identity);
            if (lever != null) Model("CHO_ExitLeverArm", lever.transform, new Vector3(0f, 1.15f, 0f), Quaternion.Euler(-35f, 0f, 0f));
            RoundMapKit.ExitColliders(root, key, Exit);
            float wallX = Exit.WallX;
            for (int k = 0; k < 3; k++)   // 받침 양옆 펜스 겉모습
                foreach (int s in new[] { -1, 1 })
                    FencePiece(root, root.TransformPoint(new Vector3(s * wallX, 0f, -1.9f - k * FenceStep)), root.rotation * Quaternion.Euler(0f, 90f, 0f));
            FencePiece(root, root.TransformPoint(new Vector3(-1.7f, 0f, -PadDepth - 0.5f)), root.rotation);
            FencePiece(root, root.TransformPoint(new Vector3(1.7f, 0f, -PadDepth - 0.5f)), root.rotation);
            RoundMapKit.ExitOpenLook(root, key, Exit, ExitColor, GeneratedFolder, "CHO_");
            Take(d * MapRadius, 7f);
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 p = root.TransformPoint(new Vector3(s * (ExitOpening / 2f + ExitPostWidth + 2.2f), 0f, 4f));
                if (Spawn("CHO_StreetLamp", Group(map, "Roads"), p, Quaternion.identity) != null) Take(p, 0.8f);
            }
        }
    }

    private static void FencePiece(Transform parent, Vector3 pos, Quaternion rot)
    {
        GameObject go = Spawn("CHO_Fence", parent, pos, rot, false);
        if (go != null) go.transform.localScale = FenceScale;
    }

    // 둥근 울타리: 겉모습은 CHO_Fence(1.6배)를 이어 세우고(충돌 없음), 충돌은 겹친 상자 고리(탈출구 자리만 비움 — 문 기둥이 막는다).
    private static void BuildFence(Transform map)
    {
        Transform fence = Group(map, "Fence");
        float r = MapRadius;
        float exitHalf = ExitOpening / 2f + ExitPostWidth;
        int segments = Mathf.RoundToInt(2f * Mathf.PI * (r + 0.4f) / FenceStep);
        for (int i = 0; i < segments; i++)
        {
            float a = (i + 0.5f) * 360f / segments;
            if (RoundMapKit.AngleTo(a, RoadDeg) * Mathf.Deg2Rad * r < exitHalf + 2.6f) continue;
            Vector3 p = Polar(r + 0.4f, a);
            FencePiece(fence, p, FaceCenter(p));
        }
        RoundMapKit.FenceColliders(Group(fence, "Colliders"), r, RoadDeg, exitHalf, FenceHeight, FenceThickness);
    }

    // ---------------------------------------------------------------- roads & quadrants

    // 큰길 양옆: 가로등과 배너를 번갈아, 둥근 산책길: 가로등. 사분면마다 안내판(술래방 모서리 바깥).
    private static int BuildRoads(Transform map)
    {
        Transform roads = Group(map, "Roads");
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Polar(1f, RoadDeg[i]), perp = new Vector3(dir.z, 0f, -dir.x);
            int k = 0;
            for (float t = 20f; t < MapRadius - 14f; t += 9f, k++)
            {
                if (Mathf.Abs(t - RingRadius) < RingHalf + 3f) continue;
                foreach (int s in new[] { -1, 1 })
                {
                    Vector3 p = dir * t + perp * (s * RoadRow);
                    bool lamp = k % 2 == 0;
                    // 배너 앞(−Z)이 큰길을 본다
                    if (Spawn(lamp ? "CHO_StreetLamp" : "CHO_Banner", roads, p, lamp ? Quaternion.identity : Quaternion.LookRotation(perp * s)) != null) n++;
                    Take(p, 0.8f);
                }
            }
        }
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f + 7.5f;
            if (RoundMapKit.AngleTo(a, RoadDeg) < 6f) continue;
            Vector3 p = Polar(RingRadius + RingHalf + 1.5f, a);
            if (!IsFree(p, 1f)) continue;
            if (Spawn("CHO_StreetLamp", roads, p, Quaternion.identity) != null) { n++; Take(p, 0.8f); }
        }
        for (int q = 0; q < 4; q++)
        {
            Vector3 p = Polar(19f, QuadDeg[q]);
            if (Spawn("CHO_Signboard", roads, p, FrontToCenter(p)) != null) { n++; Take(p, 1.5f); }
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
            // 숨는 창고 4(앞·뒤 문이 둘레 방향)
            foreach (Vector2 s in Sheds)
            {
                Vector3 p = Polar(s, b);
                GameObject shed = Spawn(ChocolateFactoryAssets.ShedName, group, p, FaceCenter(p) * Quaternion.Euler(0f, 90f, 0f));
                if (shed != null)
                {
                    n++;
                    ownFloors.Add((shed.transform, Rect.MinMaxRect(-4.3f, -4.3f, 4.3f, 4.3f)));
                }
                Take(p, ShedReach);
                placed.MonsterSeeds.Add(p + Vector3.up * 0.1f);
            }
            // 케이크 광장: 케이크 장식 + 벤치 3 + 가로등 2 + 깃발 2
            Vector3 f = Polar(52f, b);
            if (Spawn("CHO_CakeDeco", group, f, Quaternion.Euler(0f, b, 0f)) != null) n++;
            Take(f, 1.6f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = f + Polar(6.5f, b + 90f + k * 90f);
                if (Spawn("CHO_Bench", group, p, Quaternion.LookRotation(new Vector3(p.x - f.x, 0f, p.z - f.z))) != null) n++; // 앞(−Z)이 케이크
                Take(p, 1.3f);
            }
            foreach (float d in new[] { -9f, 9f })
            {
                Vector3 p = Polar(52f, b + d);
                if (Spawn("CHO_StreetLamp", group, p, Quaternion.identity) != null) n++;
                Take(p, 0.8f);
            }
            foreach (float r in new[] { 39f, 65f })
            {
                Vector3 p = Polar(r, b);
                if (Spawn("CHO_FlagStand", group, p, FrontToCenter(p)) != null) n++;
                Take(p, 1f);
            }
            // 작업장 4(공장 기계·포장 기계·초콜릿 진열대·지게차)
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = Polar(Works[k], b);
                if (Spawn(WorkNames[k], group, p, FrontToCenter(p)) != null) n++;
                Take(p, Reach(WorkNames[k]));
            }
            // CCTV: 산책길 안쪽, 건물 양옆
            foreach (float d in new[] { -14f, 14f })
            {
                Vector3 p = Polar(73.5f, b + d);
                if (!IsFree(p, 1.2f)) continue;
                if (Spawn("CHO_CCTV", group, p, FrontToCenter(p)) != null) n++;
                Take(p, 1.2f);
            }
            n += Scatter(group, b, rng);
        }
        return n;
    }

    // 가로등 더하기(§37 L2·L3와 같음) — 사분면을 다 놓은 뒤 빈자리에만: 둥근 산책길 안쪽 줄 · 술래방 둘레 8.
    private static int BuildExtraLamps(Transform map)
    {
        Transform roads = Group(map, "Roads"), room = map.Find("MonsterRoom");
        int n = 0;
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f;
            if (RoundMapKit.AngleTo(a, RoadDeg) < 6f) continue;
            Vector3 p = Polar(RingRadius - RingHalf - 1.5f, a);
            if (!IsFree(p, LampGap)) continue;
            if (Spawn("CHO_StreetLamp", roads, p, Quaternion.identity) != null) { n++; Take(p, 0.8f); }
        }
        if (room != null)
            foreach (float b in QuadDeg)
                foreach (float d in new[] { -20f, 20f })
                {
                    Vector3 p = Polar(RoomLampRadius, b + d);
                    if (Spawn("CHO_StreetLamp", room, p, Quaternion.identity) != null) { n++; Take(p, 0.8f); }
                }
        return n;
    }

    // 사분면 안 빈자리에 흩어 놓기(큰길·산책길·건물·창고와 떨어지게, 서로 4 m 넘게 — 괴물 통로).
    private static int Scatter(Transform group, float baseDeg, System.Random rng)
    {
        (string name, int count)[] list =
        {
            ("CHO_Bench", 3), ("CHO_StreetLamp", 8), ("CHO_Crate", 3), ("CHO_CacaoCrate", 2), ("CHO_IngredientSack", 3), ("CHO_Cart", 2),
            ("CHO_Pipe", 2), ("CHO_Banner", 1), ("CHO_CCTV", 1), ("CHO_FactoryMachine", 1), ("CHO_ChocoShelf", 1), ("CHO_PackingMachine", 1),
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

    // 1 m 타일을 32 m 조각·종류마다 메시 하나로 묶는다(물체당 점광원 수 제한 때문에 맵 한 덩어리로 만들지 않는다).
    // 큰길: 기본 판 + 가운데 초콜릿 조각 두 줄, 양 가장자리 12 m마다 격자 배수구 둘. 둥근 산책길: 가운데 초콜릿 조각 두 줄.
    // 사분면: 4 m 칸마다 무늬(QuadFloor). 술래방 판·재료 건물 방·창고 안처럼 제 바닥이 있는 곳은 비운다.
    private static int BuildTiles(Transform map, System.Random rng)
    {
        Transform tiles = Group(map, "Tiles");
        var cells = new Dictionary<string, List<(Vector2 p, int rot)>>();
        int skipped = 0;
        void Add(int kind, int ix, int iz)
        {
            var c = new Vector2(ix + 0.5f, iz + 0.5f);
            if (OwnFloor(c)) { skipped++; return; }
            string key = $"{TileKinds[kind]}_{Mathf.FloorToInt(c.x / TileChunk)}_{Mathf.FloorToInt(c.y / TileChunk)}";
            if (!cells.TryGetValue(key, out var l)) cells[key] = l = new List<(Vector2, int)>();
            l.Add((c, rng.Next(4)));
        }
        int count = Mathf.CeilToInt(MapRadius + PadDepth + 2f);
        var blockStyle = new Dictionary<(int, int), (Accent a, int side)>();
        for (int ix = -count; ix < count; ix++)
            for (int iz = -count; iz < count; iz++)
            {
                var c = new Vector2(ix + 0.5f, iz + 0.5f);
                float r = c.magnitude;
                float ax = Mathf.Abs(c.x), az = Mathf.Abs(c.y);
                bool road = ax < RoadHalf + 1f || az < RoadHalf + 1f;
                if (r > MapRadius + 1f && !(road && r < MapRadius + PadDepth + 1.5f && (ax < 4f || az < 4f))) continue;
                if (r < RoomInner - 0.4f) continue; // 술래방 판(반지름 11.6 m) 안
                if (road)
                {
                    float perp = ax < RoadHalf + 1f && az < RoadHalf + 1f ? Mathf.Min(ax, az) : (ax < RoadHalf + 1f ? ax : az);
                    float along = ax < RoadHalf + 1f ? az : ax;
                    int kind = perp < 1f ? ChocoPiece : (perp > RoadHalf && Mathf.Repeat(along, 12f) < 2f && r < MapRadius - 2f ? Grid : Basic);
                    Add(kind, ix, iz);
                    continue;
                }
                if (Mathf.Abs(r - RingRadius) < RingHalf + 0.5f) { Add(Mathf.Abs(r - RingRadius) < 1f ? ChocoPiece : Basic, ix, iz); continue; }
                string quad = c.x < 0f ? (c.y > 0f ? "Q1" : "Q3") : (c.y > 0f ? "Q2" : "Q4");
                int bx = Mathf.FloorToInt(ix / 4f), bz = Mathf.FloorToInt(iz / 4f);
                if (!blockStyle.TryGetValue((bx, bz), out var style)) blockStyle[(bx, bz)] = style = (PickAccent(QuadFloor[quad], rng), rng.Next(4));
                Add(AccentTile(style.a, style.side, ix - bx * 4, iz - bz * 4), ix, iz);
            }

        foreach (string old in AssetDatabase.FindAssets("Floor_CHO_FL_ t:Mesh", new[] { GeneratedFolder })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(old));
        var sources = TileKinds.ToDictionary(k => k, TileSource);
        int total = 0;
        foreach (var kv in cells)
        {
            string kind = kv.Key.Substring(0, kv.Key.IndexOf('_', "CHO_FL_".Length));
            (Mesh mesh, Material[] mats) = sources[kind];
            if (mesh == null) continue;
            var go = new GameObject("Tiles_" + kv.Key);
            go.transform.SetParent(tiles, false);
            go.AddComponent<MeshFilter>().sharedMesh = TileChunkMesh(kv.Key, mesh, kv.Value);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            total += kv.Value.Count;
        }
        Debug.Log($"[ChocolateFactoryMap] tiles {total} in {cells.Count} chunks, {skipped} under own floors");
        return total;
    }

    private static Accent PickAccent((Accent a, float w)[] mix, System.Random rng)
    {
        float t = (float)rng.NextDouble();
        foreach ((Accent a, float w) in mix) { t -= w; if (t <= 0f) return a; }
        return mix[mix.Length - 1].a;
    }

    // 4 m 칸 안 (u, v) 자리의 타일 — side(0~3)로 무늬 방향을 돌린다.
    public static int AccentTile(Accent accent, int side, int u, int v)
    {
        for (int k = 0; k < side; k++) { int t = u; u = 3 - v; v = t; }
        switch (accent)
        {
            case Accent.Jelly: return u >= 1 && u <= 2 && v >= 1 && v <= 2 ? Jelly : Basic;
            case Accent.Choco: return v == 1 ? ChocoPiece : Basic;
            case Accent.Grinder: return v == 0 ? Grinder : Basic;
            case Accent.Grid: return u == 0 && v <= 1 ? Grid : Basic;
            default: return Basic;
        }
    }

    // 제 바닥이 있는 곳(재료·일반 건물 방, 창고 안) — 타일이 바닥과 겹쳐 깜빡이지 않게 비운다.
    private static bool OwnFloor(Vector2 c)
    {
        foreach ((Transform root, Rect rect) in ownFloors)
        {
            Vector3 local = root.InverseTransformPoint(new Vector3(c.x, 0f, c.y));
            if (rect.Contains(new Vector2(local.x, local.z))) return true;
        }
        return false;
    }

    // 타일 종류의 원본 메시(FBX, 원점 = 타일 가운데 바닥, 1 m)와 재질(부분 메시 순서).
    private static (Mesh, Material[]) TileSource(string kind)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ChocolateFactoryAssets.FloorFolder}/{kind}.fbx");
        MeshFilter mf = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
        if (mf == null) { Debug.LogWarning($"[ChocolateFactoryMap] tile {kind} missing"); return (null, null); }
        return (mf.sharedMesh, mf.GetComponent<MeshRenderer>().sharedMaterials);
    }

    // 원본 타일을 칸마다 옮겨(90° 단위로 돌려) 부분 메시별로 이어 붙인다 — 창살 타일은 구멍·구덩이까지 그대로.
    private static Mesh TileChunkMesh(string key, Mesh source, List<(Vector2 p, int rot)> cells)
    {
        int subs = source.subMeshCount;
        var parts = new CombineInstance[subs];
        var temps = new List<Mesh>();
        for (int s = 0; s < subs; s++)
        {
            var list = new CombineInstance[cells.Count];
            for (int i = 0; i < cells.Count; i++)
                list[i] = new CombineInstance
                {
                    mesh = source, subMeshIndex = s,
                    transform = Matrix4x4.TRS(new Vector3(cells[i].p.x, TileY, cells[i].p.y), Quaternion.Euler(0f, cells[i].rot * 90f, 0f), Vector3.one),
                };
            var part = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            part.CombineMeshes(list, true, true);
            temps.Add(part);
            parts[s] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
        }
        var mesh = new Mesh { name = "Floor_" + key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.CombineMeshes(parts, false, false);
        foreach (Mesh t in temps) Object.DestroyImmediate(t);
        mesh.RecalculateBounds();
        return SaveMesh(mesh, $"{GeneratedFolder}/Floor_{key}.asset");
    }

    // ---------------------------------------------------------------- lights

    // 테스트 맵 점광원은 끄고, 모든 가로등·술래방(안쪽·문 위)에 점광원. 건물·창고 안 빛은 프리팹에 있다.
    private static int BuildLights(Scene scene, Transform map)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = SceneAmbient;
        RenderSettings.ambientSkyColor = SceneAmbient;
        RenderSettings.fogDensity = SceneFogDensity;
        GameObject lighting = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MapLighting");
        Transform old = lighting != null ? lighting.transform.Find("PointLights") : null;
        if (old != null) old.gameObject.SetActive(false);
        Transform parent = lighting != null ? lighting.transform : map;
        Transform lights = parent.Find("CHO_Lights");
        if (lights != null) Object.DestroyImmediate(lights.gameObject);
        lights = new GameObject("CHO_Lights").transform;
        lights.SetParent(parent, false);
        int n = 0;
        foreach (Transform t in map.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            if (source == null || source.name != "CHO_StreetLamp" || t.IsChildOf(map.Find("Background"))) continue;
            RoundMapKit.Point(lights, t.position + Vector3.up * LampLightHeight, LampRange, LampIntensity, LampColor);
            n++;
        }
        RoundMapKit.Point(lights, Vector3.up * 6f, 15f, 2.2f, RoomColor); n++;
        for (int i = 0; i < 4; i++) { RoundMapKit.Point(lights, Polar(RoomRadius + 1.2f, RoadDeg[i]) + Vector3.up * 4.5f, 9f, 3.2f, RoomColor); n++; }
        return n;
    }

    // ---------------------------------------------------------------- passability

    public const string PassabilityReport = "Temp/ChocolateFactory_Passability.txt";
    private const float RepairStandOff = 1.5f;

    [MenuItem(MenuRoot + "Check Chocolate Factory Passability (5 body sizes)")]
    public static void CheckPassabilityMenu() => Debug.Log(CheckPassability());

    // 두 번 잰다(몸 크기 5가지씩):
    //   ① 맵 전체 — 기준 괴물(반경 1.8 m 구, 4 m 통로 규칙). 술래방은 괴물 전용, 울타리 밖(탈출구 받침 제외)은 놀이 공간이 아님.
    //      건물 방·문 복도 안은 소품(마카롱 탑·가마솥·원유 탱크·생산 줄)이 많아 기준 몸이 돌지 못하므로 ②에서 잰다.
    //   ② 건물 안 — 괴물 반경 0.9 m(§41 문 통로 시험과 같은 값, 실제 0.31 m의 세 배): 방 가운데·꽂는 곳 앞까지 오고, 괴물 손이 안 닿는 쿠키 자리 0.
    // 괴물 출발점: 술래방 문 바깥 + 건물 방 가운데 + 창고 안(창고 문 2.8 m는 실제 괴물은 지나가지만 격자 괴물은 못 지나간다 — 놀이공원 텐트와 같은 처리).
    public static string CheckPassability()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var seeds = RoadDeg.Select(d => Polar(RoomRadius + 1.5f, d)).ToList();
        var targets = RoundMapKit.PassTargets(scene).ToList();
        var interiorTargets = new List<(string, Vector3)>();
        var buildings = new List<(Transform t, ChocolateFactoryAssets.Meta meta)>();
        foreach (Transform t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            string source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name;
            if (source == ChocolateFactoryAssets.ShedName)
            {
                seeds.Add(t.position + Vector3.up * 0.1f);
                targets.Add(("Shed@" + t.parent.name + "/" + t.name, t.position));
            }
            else if (source != null && source.StartsWith("CHO_Bld_"))
            {
                ChocolateFactoryAssets.Meta meta = ChocolateFactoryAssets.LoadMeta(source.Substring("CHO_Bld_".Length));
                if (meta == null) continue;
                buildings.Add((t, meta));
                Vector3 centre = t.TransformPoint(new Vector3((meta.room.min[0] + meta.room.max[0]) / 2f, 0.1f, (meta.room.min[2] + meta.room.max[2]) / 2f));
                seeds.Add(centre);
                interiorTargets.Add(("Room@" + source, centre));
                // 꽂는 곳: 쿠키가 재료를 꽂으며 서는 자리(받침 앞, 방 가운데 쪽 RepairStandOff m)
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    if (!targets[i].Item1.StartsWith("Repair_Point@" + source)) continue;
                    Vector3 p = targets[i].Item2, toCentre = Vector3.ProjectOnPlane(centre - p, Vector3.up);
                    interiorTargets.Add(("Stand@" + source, p + toCentre.normalized * Mathf.Min(RepairStandOff, toCentre.magnitude)));
                    targets.RemoveAt(i);
                }
            }
        }
        bool Inside(Vector3 p) => buildings.Any(b => InsideBuilding(b.t, b.meta, p));
        var common = new MapPassabilityCheck.Options
        {
            Half = MapRadius + 12f,
            Bodies = MapPassabilityCheck.DesignRange,
            CookieForbidden = p => new Vector2(p.x, p.z).magnitude < RoomRadius,
            IgnorePocket = p => new Vector2(p.x, p.z).magnitude > MapRadius + 0.5f,
            MonsterVerified = Inside,
            Targets = targets.ToArray(),
            MonsterSeeds = seeds.ToArray(),
            MapImage = "Temp/ChocolateFactory_PassabilityMap.png",
        };
        var interior = new MapPassabilityCheck.Options
        {
            Half = common.Half,
            Bodies = common.Bodies,
            CookieForbidden = common.CookieForbidden,
            IgnorePocket = p => common.IgnorePocket(p) || !Inside(p),   // 건물 밖은 ①에서 잰다
            MonsterVerified = p => !Inside(p),
            MonsterRadius = InteriorMonsterRadius,
            Targets = interiorTargets.ToArray(),
            MonsterSeeds = common.MonsterSeeds,
            MapImage = "Temp/ChocolateFactory_PassabilityInterior.png",
        };
        MapPassabilityCheck.Result[] results = MapPassabilityCheck.Run(scene, common).Concat(MapPassabilityCheck.Run(scene, interior)).ToArray();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < results.Length; i++)
            sb.Append(results[i].Clean ? "CLEAN " : "ISSUES ").Append(i < results.Length / 2 ? "[map, monster r 1.8] " : $"[building interiors, monster r {InteriorMonsterRadius}] ").Append(results[i]);
        System.IO.Directory.CreateDirectory("Temp");
        System.IO.File.WriteAllText(PassabilityReport, sb.ToString());
        return $"[ChocolateFactoryMap] passability: {results.Count(r => r.Clean)}/{results.Length} clean — {PassabilityReport}";
    }

    public const float InteriorMonsterRadius = 0.9f;

    // 건물 방(벽 안쪽 + 0.5 m)과 앞·뒤 문 복도(문 가운데 ± 복도 반폭, 방 벽에서 문까지 + 1 m) — 건물 루트 기준.
    private static bool InsideBuilding(Transform root, ChocolateFactoryAssets.Meta meta, Vector3 p)
    {
        Vector3 l = root.InverseTransformPoint(p);
        float x0 = meta.room.min[0], x1 = meta.room.max[0], z0 = meta.room.min[2], z1 = meta.room.max[2];
        if (l.x > x0 - 0.5f && l.x < x1 + 0.5f && l.z > z0 - 0.5f && l.z < z1 + 0.5f) return true;
        foreach (ChocolateFactoryAssets.DoorMeta d in meta.doors)
        {
            float dz = d.centre[2], wall = dz > (z0 + z1) / 2f ? z1 : z0;
            if (Mathf.Abs(l.x - d.centre[0]) < 2.9f && l.z > Mathf.Min(dz, wall) - 1f && l.z < Mathf.Max(dz, wall) + 1f) return true;
        }
        return false;
    }
}
