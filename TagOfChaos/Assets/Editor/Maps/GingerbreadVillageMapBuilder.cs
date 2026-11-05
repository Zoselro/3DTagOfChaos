using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 진저브레드 마을 실제 모델 맵 씬(Request1009Plan.md §51). Test_GingerbreadVillage를 복사한 GingerbreadVillage 씬에서 놀이 공간을
// Blender로 만든 건물·소품(§48~§50)으로 바꾼다. 두 층:
//   지상(반지름 155 m): 가운데 술래방(테스트 맵 것 그대로) · 술래방 뒤(12시) 시계탑 · 사분면마다 재료 건물(룬 건물, Building_Qn) 1 +
//     지상 건물 2 · 큰길 4 · 둥근 산책길 · 둘레 울타리 · 꾸미기 소품 · 상자/로켓/무지개/스폰 자리.
//   지하(반지름 108 m ≈ 지상의 2/3, 바닥 −36 m 동굴): 시계탑 계단 → 뒷벽 통로 → 돌기둥을 도는 나선 내리막(18°) → 동굴 바닥.
//     사분면마다 지하 건물 3 · 출구 4(12·3·6·9시, 지하에만) · 지하 길 · 소품 · 등불. 지하에는 술래방이 없다.
// 걷는 바닥: 지상은 보이지 않는 평판(시계탑 계단 자리만 비움) + 1 m 타일(보이기만), 지하도 같은 방식.
// 카메라·관리자·마녀·환경음은 테스트 씬 그대로(환경음 상자만 두 층에 맞춘다). 여러 번 실행해도 같은 결과(고정 난수).
public static class GingerbreadVillageMapBuilder
{
    public const string MapName = "GingerbreadVillage";
    public const string ScenePath = "Assets/Scenes/Maps/" + MapName + ".unity";
    public const string GeneratedFolder = "Assets/09. Environment/GingerbreadVillage/Generated";
    private const string PropPrefabs = "Assets/04. Prefabs/Environment/GingerbreadVillage";
    private const string BuildingPrefabs = "Assets/04. Prefabs/Environment/GingerbreadBuildings";
    private const string MaterialDir = "Assets/09. Environment/GingerbreadBuildings/Materials";
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";

    // ---- 지상
    public const float MapRadius = 100f;   // 155 → 100 m(§58, 약 0.65배)
    public const float RoadHalf = 5f, RoadRow = 7.5f;
    public const float RingRadius = 22f, RingHalf = 3f;
    // 시계탑 2(11시·5시, 2026-11-04 사용자 요청 — 처음 12시 술래방 뒤에서 옮김). 정문이 맵 가운데를 보고, 뒷벽 통로 뒤(바깥쪽)에 나선 내리막.
    public const float TowerRadius = 40f;
    public static readonly float[] TowerDeg = { 105f, 285f };   // §58: 지하 1·4사분면에 나선과 룬 건물이 함께 들어가게 큰길 쪽으로
    private const float FenceHeight = 6f, FenceThickness = 0.8f, FenceStep = 3.1f;
    private const float FloorHalf = MapRadius + 80f;
    private const float GroundUnderY = -0.3f;
    private const float WitchBeyondFence = 75f;

    // ---- 지하
    public const float UgRadius = 105f;   // 134 → 105 m(§58: 지하 건물 5종 + 룬 2 + 숨는 건물 1)
    public const float UgY = -36f;
    public const float CeilingY = -0.35f;
    public const float UgRoadHalf = 3.5f;
    private const float UgFloorHalf = UgRadius + 30f;

    // ---- 시계탑 계단·나선 내리막(시계탑 로컬: 앞 +Z, 계단 구멍 x ±2.3 · z −3.0~1.65, 뒷벽 통로 바깥 z −3.61 · 바닥 −3.6 m)
    private static readonly Rect TowerStairLocal = Rect.MinMaxRect(-2.3f, -3.0f, 2.3f, 1.65f);
    private static readonly Rect TowerFootLocal = Rect.MinMaxRect(-8.7f, -3.7f, 8.7f, 4.5f);
    private const float TowerBackLocal = -3.61f, TowerTunnelY = -3.6f;
    public const float HelixRadius = 10f, HelixWidth = 5f, HelixSlopeDeg = 18f;
    private const float HelixLandingDeg = 25f, HelixCurb = 1.3f, HelixThickness = 0.6f, BridgeGapDeg = 12f;

    // 사분면(Q1 북서, Q2 북동, Q3 남서, Q4 남동) — 각도는 +x에서 +z 쪽
    public static readonly float[] QuadDeg = { 135f, 45f, 225f, 315f };
    public static readonly float[] AxisDeg = { 90f, 0f, 270f, 180f };          // N, E, S, W
    public static readonly string[] ExitNames = { "Exit_N", "Exit_E", "Exit_S", "Exit_W" };
    // 건물 배치(§58, 2026-11-05 사용자 요청): 지상 사분면마다 3채(룬 건물은 Q2·Q3), 지하 사분면마다 2채(룬 건물은 UQ1·UQ4).
    // 자리는 큰길·산책길·시계탑·나선·출구 앞과 겹치지 않게(옆 1 m·문 앞뒤 4 m 여유) 찾은 값(scratchpad/lay/lay57.py·pair57.py). (이름, 반지름, 각도)
    // 룬 건물은 Building_Q{사분면}(탈출 설정이 이 이름으로 찾는다).
    private static readonly (int q, float r, float deg)[] RuneGround = { (2, 52f, 43f), (3, 50f, 225f) };
    private static readonly (int q, float r, float deg)[] RuneUnderground = { (1, 84f, 149f), (4, 84f, 331f) };
    // §60(사용자 사진): 사분면마다 안쪽 고리 1채 + 바깥 고리(지상 2·지하 1)로 고르게(scratchpad/lay/even60.py).
    private static readonly (string name, float r, float deg)[][] GroundPlan =
    {
        new[] { ("GIN_HD_HolidayHideaway", 56f, 140f), ("GIN_BLD_CandyCaneCottage", 82f, 111f), ("GIN_BLD_GingerbreadMill", 82f, 162f) },
        new[] { ("GIN_BLD_ChocoCottage", 82f, 16f), ("GIN_HD_CandyMughouse", 84f, 72f) },
        new[] { ("GIN_BLD_SkullCauldron", 82f, 198f), ("GIN_HD_GingerbreadWindmill", 84f, 253f) },
        new[] { ("GIN_HD_CandyMughouse", 60f, 321f), ("GIN_BLD_CandyCogCottage", 82f, 291f), ("GIN_BLD_ChocoCottage", 88f, 343f) },
    };
    private static readonly (string name, float r, float deg)[][] UgPlan =
    {
        new[] { ("GIN_UG_MacaronCottage", 48f, 144f) },
        new[] { ("GIN_UG_CrookedChocoCottage", 62f, 31f), ("GIN_UG_GloomgumFortress", 82f, 66f) },
        new[] { ("GIN_UG_CandyCaneCottage", 53f, 224f), ("GIN_UG_GearworksGate", 88f, 224f) },
        new[] { ("GIN_HD_WitchTeapot", 46f, 323f) },
    };
    private static IEnumerable<string> PlanNames => GroundPlan.Concat(UgPlan).SelectMany(x => x).Select(x => x.name).Distinct();
    private static readonly string[] KeepInMap = { "Background", WitchPath.FarBackdropName, "Ground", "Exits" };   // 술래방은 왕좌 방으로 새로 만든다(§52)

    // 조명
    private static readonly Color LampColor = new Color(1f, 0.78f, 0.55f);
    private static readonly Color UgLampColor = new Color(0.78f, 0.55f, 1f);
    private static readonly Color UgWarmColor = new Color(1f, 0.62f, 0.32f);

    private static readonly RoundMapKit.Placement placed = new RoundMapKit.Placement();   // 지상
    private static readonly RoundMapKit.Placement placedUg = new RoundMapKit.Placement(); // 지하
    private static readonly Dictionary<string, float> reachCache = new Dictionary<string, float>();
    private static readonly List<(Transform root, Rect rect)> noTiles = new List<(Transform, Rect)>();
    private static readonly List<Transform> towers = new List<Transform>();
    private static readonly List<Vector3> helixCentres = new List<Vector3>();
    private const float TowerHoleHalf = 10f;   // 시계탑 둘레 정사각 구멍(반 폭) — 그 안은 탑 방향으로 돌린 띠 판이 채운다
    public static string LastReport { get; private set; } = "";

    [MenuItem(MenuRoot + "Build Gingerbread Village Map Scene")]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GingerbreadMap] stop Play Mode first"); return; }
        Debug.Log(Build());
    }

    public static string Build()
    {
        if (Prop("GIN_ClockTower") == null || Building("GIN_RuneBuilding") == null)
            return "[GingerbreadMap] prefabs missing — run Gingerbread 2·3·5 menus first";
        placed.Clear(); placedUg.Clear(); reachCache.Clear(); noTiles.Clear(); towers.Clear(); helixCentres.Clear();
        string source = TestMapBuilder.ScenePath(MapName);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(source, ScenePath)) return "[GingerbreadMap] copy failed";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TwistedPropBuilder.EnsureFolder(GeneratedFolder);

        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        foreach (Transform child in map.Cast<Transform>().ToList())
            if (!KeepInMap.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
        GameObject oldEscape = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EscapeMapSetup.RootName);
        if (oldEscape != null)
            foreach (Transform child in oldEscape.transform.Cast<Transform>().ToList())
                if (child.name != "WITCH_Anchor") Object.DestroyImmediate(child.gameObject);

        var rng = new System.Random(1104);
        int backdrop = BuildBackdrop(map, rng);
        RoundMapKit.SpreadFarBackdrop(map, MapRadius, MapRadius + 45f);
        BuildThroneRoom(map);

        BuildClockTowers(map);
        BuildGround(map);
        Transform ug = Group(map, "Underground");
        BuildRuneBuildings(map, ug);
        int groundBuildings = BuildPlan(GroundPlan, map, 0f, "Q", placed);
        BuildFence(map);
        int groundDeco = BuildGroundRoads(map) + BuildGroundQuadrants(map, rng);

        BuildCave(ug);
        for (int k = 0; k < towers.Count; k++) BuildHelix(ug, k);
        int ugBuildings = BuildPlan(UgPlan, ug, UgY, "UQ", placedUg);
        BuildElevator(map, ug);
        int exits = BuildExits(map, ug);
        int ugDeco = BuildUgDeco(ug, rng);

        Physics.SyncTransforms();
        int tiles = BuildTiles(map, ug, rng);
        int markers = RoundMapKit.Markers(map, placed, RingRadius, RingHalf, MapRadius / 80f);
        int lights = BuildLights(scene, map, ug);
        SetAmbience(scene);
        MoveKillZone(scene);

        string escape = EscapeMapSetup.Apply(scene, MapName);
        Transform room = map.Find("MonsterRoom");
        var guard = room != null ? room.GetComponent<MonsterRoomGuard>() : null;
        if (guard != null)   // 둥근 방이라 RoomWall_ 면이 없다 — 안쪽 정사각형 반 폭을 직접 준다(초콜릿 공장과 같음)
        {
            guard.EditorBind(GuardInnerHalf, room.GetComponentsInChildren<MonsterRoomDoor>(true).Select(d => d.transform).OrderBy(t => t.name).ToArray());
            EditorUtility.SetDirty(guard);
        }
        var manager = Object.FindFirstObjectByType<EscapeManager>();
        if (manager != null) { manager.EditorSetMapRadius(Mathf.Max(MapRadius, UgRadius)); EditorUtility.SetDirty(manager); }   // 지하 동굴(105 m)이 지상(100 m)보다 넓다
        RoundMapKit.MoveSpawn(scene, SceneSpawnPoints.Cookie, map, "Spawn_Cookie_Q1");
        RoundMapKit.MoveWitchAnchor(scene, MapRadius, WitchBeyondFence);

        string overlaps = CheckOverlaps(map);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RoundMapKit.AddToBuildSettings(ScenePath, MapName);
        LastReport = $"[GingerbreadMap] {ScenePath}: ground buildings {groundBuildings}, underground buildings {ugBuildings}, rune buildings {RuneGround.Length + RuneUnderground.Length}, exits {exits}, " +
                     $"ground deco {groundDeco}, underground deco {ugDeco}, backdrop {backdrop}, tiles {tiles}, markers {markers}, lights {lights}, " +
                     $"towers {string.Join(" ", towers.Select(t => t.position.ToString("F0")))} | {overlaps} | {escape}";
        return LastReport;
    }

    // ---------------------------------------------------------------- helpers

    private static Vector3 Polar(float r, float deg) => RoundMapKit.Polar(r, deg);
    private static Quaternion FaceCenter(Vector3 p) => RoundMapKit.FaceCenter(p);
    private static Transform Group(Transform parent, string name) => RoundMapKit.Group(parent, name);
    private static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);

    private static GameObject Prop(string name)
    {
        foreach (string g in new[] { "Ground", "Underground", "Floor", "ClockTower" })
        {
            var a = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabs}/{g}/{name}.prefab");
            if (a != null) return a;
        }
        return null;
    }

    private static GameObject Building(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildingPrefabs}/{name}.prefab");
    private static Material Mat(string key) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/M_GIN_{key}.mat");

    private static float Reach(string prop)
    {
        if (reachCache.TryGetValue(prop, out float r)) return r;
        return reachCache[prop] = RoundMapKit.Reach(Prop(prop));
    }

    private static GameObject SpawnProp(string prop, Transform parent, Vector3 pos, Quaternion rot, bool colliders = true)
    {
        GameObject asset = Prop(prop);
        if (asset == null) { Debug.LogWarning($"[GingerbreadMap] prefab {prop} missing"); return null; }
        return RoundMapKit.Spawn(asset, parent, pos, rot, colliders);
    }

    private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat, bool collider, Vector3 pos = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        if (mat != null) go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    private static Mesh SaveMesh(Mesh mesh)
    {
        string path = $"{GeneratedFolder}/{mesh.name}.asset";
        mesh.RecalculateBounds();
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, saved);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(saved);
        return saved;
    }

    // 메시를 만드는 작은 도구(사각형·띠). UV는 1 m = uvScale.
    private sealed class MeshBuild
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector2> U = new List<Vector2>();
        public readonly List<int> T = new List<int>();

        // a→b→c→d 시계 방향(위에서 보면) = 위를 보는 면
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = V.Count;
            V.AddRange(new[] { a, b, c, d });
            U.AddRange(new[] { ua, ub, uc, ud });
            T.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        public void FlatRect(float x0, float z0, float x1, float z1, float y, bool down, float uvScale)
        {
            Vector3 a = new Vector3(x0, y, z0), b = new Vector3(x0, y, z1), c = new Vector3(x1, y, z1), d = new Vector3(x1, y, z0);
            Vector2 Uv(Vector3 p) => new Vector2(p.x, p.z) * uvScale;
            if (down) Quad(a, d, c, b, Uv(a), Uv(d), Uv(c), Uv(b));
            else Quad(a, b, c, d, Uv(a), Uv(b), Uv(c), Uv(d));
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(V); m.SetUVs(0, U); m.SetTriangles(T, 0);
            m.RecalculateNormals(); m.RecalculateTangents();
            return SaveMesh(m);
        }
    }

    // 가운데가 원점인 정사각형에서 hole(월드 x·z)만 비운 판 — 지상 걷는 판·아래판·동굴 천장
    private static Mesh SquareWithHole(string name, float half, Rect hole, float y, bool down, float uvScale)
    {
        var mb = new MeshBuild();
        mb.FlatRect(-half, -half, hole.xMin, half, y, down, uvScale);
        mb.FlatRect(hole.xMax, -half, half, half, y, down, uvScale);
        mb.FlatRect(hole.xMin, -half, hole.xMax, hole.yMin, y, down, uvScale);
        mb.FlatRect(hole.xMin, hole.yMax, hole.xMax, half, y, down, uvScale);
        return mb.ToMesh(name);
    }

    // ---------------------------------------------------------------- monster room (throne room, §52)

    // 술래방 = 왕좌 방(GIN_ThroneRoom, Blender throneroom.py — 벽 안쪽 11.9 m·바깥 12.55 m, 문 12·3·6·9시 폭 4 m).
    // 왕좌가 북쪽(시계탑 쪽)에서 남쪽을 보게 180° 돌린다. 벽은 겹친 상자 고리(9 m), 문(MonsterDoor_0~3 = 북·동·남·서)은 괴물만 지나간다.
    public const float RoomRadius = 12.55f, RoomInner = 11.9f, RoomDoorWidth = 4f, GuardInnerHalf = 8f;
    private const float RoomWallHeight = 9f;
    private static readonly Color RoomColor = new Color(0.72f, 0.3f, 1f);

    private static void BuildThroneRoom(Transform map)
    {
        var room = new GameObject("MonsterRoom").transform;
        room.SetParent(map, false);
        GameObject model = SpawnProp("GIN_ThroneRoom", room, Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
        if (model != null) model.name = "Model";
        RoundMapKit.RoomRing(room, RoomInner, RoomRadius, RoomWallHeight, RoomDoorWidth, AxisDeg);
        GameObject doorModel = Prop("GIN_ThroneRoomDoor");
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Polar(1f, AxisDeg[i]);
            RoundMapKit.RoomDoor(room, i, dir, RoomInner, RoomRadius, RoomWallHeight, RoomDoorWidth, doorModel);
            RoundMapKit.BeaconPart(room, "DoorGlow_" + i, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), RoundMapKit.BeaconMaterial(GeneratedFolder, "GIN_RoomDoorGlow", RoomColor * 1.2f, 1f, 1.5f, 1f),
                dir * RoomRadius + Vector3.up * 0.15f, Quaternion.Euler(90f, 0f, 0f), new Vector3(7f, 7f, 1f));
        }
        var spawn = new GameObject("Spawn_Monster").transform;
        spawn.SetParent(room, false);
        spawn.localPosition = new Vector3(0f, 0.1f, -5.5f);   // 가운데는 엘리베이터(§58)
        placed.Take(Vector3.zero, RoomRadius + 4f);
        // 방 둘레 가로등 8(문 사이)
        foreach (float b in QuadDeg)
            foreach (float d in new[] { -18f, 18f })
            {
                Vector3 p = Polar(RoomRadius + 3.5f, b + d);
                if (SpawnProp("GIN_StreetLamp", room, p, Quaternion.identity) != null) placed.Take(p, 0.8f);
            }
    }

    // 괴물 전용 엘리베이터(§58): 술래방 가운데(지상)와 지하 동굴 가운데의 발판 한 쌍. 발판 위에서 E → 빛기둥 연출 → 반대쪽 발판.
    private const float ElevatorRadius = 2.2f, ElevatorBeamHeight = 6f;

    private static void BuildElevator(Transform map, Transform ug)
    {
        Transform room = map.Find("MonsterRoom");
        MonsterElevator top = MakeElevatorPad(room != null ? room : map, "Elevator_Top", Vector3.zero);
        MonsterElevator bottom = MakeElevatorPad(Group(ug, "Elevator"), "Elevator_Bottom", Vector3.up * UgY);
        top.EditorBind(bottom, top.transform.Find("Beam"));
        bottom.EditorBind(top, bottom.transform.Find("Beam"));
        EditorUtility.SetDirty(top); EditorUtility.SetDirty(bottom);
        placedUg.Take(Vector3.zero, ElevatorRadius + 4f);
    }

    private static MonsterElevator MakeElevatorPad(Transform parent, string name, Vector3 pos)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var pad = new GameObject("Pad");
        pad.transform.SetParent(root.transform, false);
        pad.transform.localPosition = Vector3.up * 0.04f;
        pad.transform.localScale = new Vector3(ElevatorRadius * 2f, 0.04f, ElevatorRadius * 2f);   // 8 cm 원판(걸을 수 있는 높이 — 충돌은 바닥이 맡는다)
        pad.AddComponent<MeshFilter>().sharedMesh = cylinder;
        pad.AddComponent<MeshRenderer>().sharedMaterial = Mat("stone_lilac");
        RoundMapKit.BeaconPart(root.transform, "Glow", Resources.GetBuiltinResource<Mesh>("Quad.fbx"), RoundMapKit.BeaconMaterial(GeneratedFolder, "GIN_ElevatorGlow", RoomColor * 1.4f, 1f, 1.5f, 1f),
            Vector3.up * 0.1f, Quaternion.Euler(90f, 0f, 0f), new Vector3(ElevatorRadius * 2.6f, ElevatorRadius * 2.6f, 1f));
        RoundMapKit.BeaconPart(root.transform, "Beam", cylinder, RoundMapKit.BeaconMaterial(GeneratedFolder, "GIN_ElevatorBeam", RoomColor * 1.6f, 0f, 1.2f, 1.5f),
            Vector3.up * (ElevatorBeamHeight / 2f), Quaternion.identity, new Vector3(ElevatorRadius * 1.8f, ElevatorBeamHeight / 2f, ElevatorRadius * 1.8f));
        RoundMapKit.Point(root.transform, pos + Vector3.up * 3f, 8f, 2f, RoomColor);
        return root.AddComponent<MonsterElevator>();
    }

    // ---------------------------------------------------------------- background

    // 테스트 맵 배경(크림 퍼프 나무·컵케이크·막대 사탕)은 지우지 않고 울타리 밖 고리(172~205 m)로 옮긴다. 땅 덩어리(Terrain_Decor)는 지운다.
    private static int BuildBackdrop(Transform map, System.Random rng)
    {
        Transform background = map.Find("Background");
        if (background == null) return 0;
        int n = 0;
        foreach (Transform t in background.Cast<Transform>().ToList())
        {
            if (t.name.StartsWith("GIN_Terrain")) { Object.DestroyImmediate(t.gameObject); continue; }
            var flat = new Vector2(t.position.x, t.position.z);
            float deg = flat.sqrMagnitude > 1f ? Mathf.Atan2(flat.y, flat.x) * Mathf.Rad2Deg : rng.Next(360);
            Vector3 p = Polar(MapRadius + 17f + (float)rng.NextDouble() * 33f, deg);
            t.position = new Vector3(p.x, 0f, p.z);
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- ground

    // 시계탑 2(11시·5시, 반지름 70 m): 정문(+Z)이 맵 가운데를 본다. 계단 구멍은 걷는 판·아래판·동굴 천장에서 비운다(RingFill).
    private static void BuildClockTowers(Transform map)
    {
        Transform group = Group(map, "Landmarks");
        for (int k = 0; k < TowerDeg.Length; k++)
        {
            Vector3 p = Polar(TowerRadius, TowerDeg[k]);
            GameObject tower = SpawnProp("GIN_ClockTower", group, p, FaceCenter(p));
            if (tower == null) continue;
            tower.name = $"GIN_ClockTower_{(k == 0 ? "11" : "05")}";
            var d = tower.GetComponentInChildren<InteractableDoor>(true);
            if (d != null) { var so = new SerializedObject(d); so.FindProperty("doorId").stringValue = tower.name + "_Front"; so.ApplyModifiedPropertiesWithoutUndo(); }
            noTiles.Add((tower.transform, TowerFootLocal));
            placed.Take(p, 11f);
            placed.MonsterSeeds.Add(tower.transform.TransformPoint(new Vector3(0f, 0.1f, 2.4f)));
            towers.Add(tower.transform);
        }
    }

    private static List<Rect> TowerHoles() => towers.Select(t => Rect.MinMaxRect(t.position.x - TowerHoleHalf, t.position.z - TowerHoleHalf, t.position.x + TowerHoleHalf, t.position.z + TowerHoleHalf)).ToList();

    // 탑 둘레 정사각 구멍(월드 축)과 계단 구멍(탑 방향으로 돈 직사각형) 사이를 채우는 띠 판. 탑 가운데에서 각도별로 두 경계를 잇는다
    // (두 모양 모두 탑 가운데를 품은 볼록 다각형 — 꼭짓점 각도는 빠짐없이 넣는다).
    private static Mesh RingFill(string name, Transform tower, float y, bool down, float uvScale)
    {
        Vector2 c = new Vector2(tower.position.x, tower.position.z);
        Vector2 W(float lx, float lz) { Vector3 w = tower.TransformPoint(new Vector3(lx, 0f, lz)); return new Vector2(w.x, w.z); }
        Vector2[] inner = { W(TowerStairLocal.xMin, TowerStairLocal.yMin), W(TowerStairLocal.xMax, TowerStairLocal.yMin), W(TowerStairLocal.xMax, TowerStairLocal.yMax), W(TowerStairLocal.xMin, TowerStairLocal.yMax) };
        float h = TowerHoleHalf;
        Vector2[] outer = { c + new Vector2(-h, -h), c + new Vector2(h, -h), c + new Vector2(h, h), c + new Vector2(-h, h) };
        var angles = new List<float>();
        for (int i = 0; i < 256; i++) angles.Add(i * 360f / 256f);
        foreach (Vector2 v in inner.Concat(outer)) angles.Add(Mathf.Repeat(Mathf.Atan2(v.y - c.y, v.x - c.x) * Mathf.Rad2Deg, 360f));
        angles.Sort();
        float Hit(Vector2[] poly, Vector2 d)
        {
            float best = float.MaxValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], e = poly[(i + 1) % poly.Length] - a;
                float den = d.x * e.y - d.y * e.x;
                if (Mathf.Abs(den) < 1e-7f) continue;
                Vector2 ac = a - c;
                float t = (ac.x * e.y - ac.y * e.x) / den, u = (ac.x * d.y - ac.y * d.x) / den;
                if (t > 0f && u >= -1e-4f && u <= 1f + 1e-4f) best = Mathf.Min(best, t);
            }
            return best;
        }
        var mb = new MeshBuild();
        Vector3 P3(Vector2 v) => new Vector3(v.x, y, v.y);
        Vector2 Uv(Vector2 v) => v * uvScale;
        for (int i = 0; i < angles.Count; i++)
        {
            float a0 = angles[i] * Mathf.Deg2Rad, a1 = angles[(i + 1) % angles.Count] * Mathf.Deg2Rad;
            if (Mathf.Abs(a1 - a0) < 1e-6f) continue;
            Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            Vector2 i0 = c + d0 * Hit(inner, d0), i1 = c + d1 * Hit(inner, d1), o0 = c + d0 * Hit(outer, d0), o1 = c + d1 * Hit(outer, d1);
            if (down) mb.Quad(P3(i0), P3(o0), P3(o1), P3(i1), Uv(i0), Uv(o0), Uv(o1), Uv(i1));
            else mb.Quad(P3(i0), P3(i1), P3(o1), P3(o0), Uv(i0), Uv(i1), Uv(o1), Uv(o0));
        }
        return mb.ToMesh(name);
    }

    // 가운데가 원점인 정사각형에서 여러 직사각형 구멍을 비운 판(구멍 모서리로 나눈 격자 칸 중 구멍 밖만)
    private static Mesh SquareWithHoles(string name, float half, List<Rect> holes, float y, bool down, float uvScale)
    {
        var xs = new SortedSet<float> { -half, half }; var zs = new SortedSet<float> { -half, half };
        foreach (Rect r in holes) { xs.Add(r.xMin); xs.Add(r.xMax); zs.Add(r.yMin); zs.Add(r.yMax); }
        float[] X = xs.ToArray(), Z = zs.ToArray();
        var mb = new MeshBuild();
        for (int i = 0; i + 1 < X.Length; i++)
            for (int j = 0; j + 1 < Z.Length; j++)
            {
                var m = new Vector2((X[i] + X[i + 1]) / 2f, (Z[j] + Z[j + 1]) / 2f);
                if (holes.Any(r => r.Contains(m))) continue;
                mb.FlatRect(X[i], Z[j], X[i + 1], Z[j + 1], y, down, uvScale);
            }
        return mb.ToMesh(name);
    }

    // 걷는 면 = 보이지 않는 평판(0 m, 계단 구멍만 비움). 보이는 면은 타일 + 0.3 m 아래 아래판(울타리 밖).
    private static void BuildGround(Transform map)
    {
        Transform ground = map.Find("Ground");
        foreach (Transform child in ground.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
        ground.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ground.localScale = Vector3.one;
        List<Rect> holes = TowerHoles();
        GameObject walk = MeshObject(ground, "Floor", SquareWithHoles("GIN_Floor_Walk", FloorHalf, holes, 0f, false, 1f), null, true);
        walk.isStatic = true;
        GameObject under = MeshObject(ground, "GroundUnder", SquareWithHoles("GIN_Floor_GroundUnder", FloorHalf, holes, GroundUnderY, false, 0.5f), Mat("GIN_FL_GrassStones"), false);
        under.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int k = 0; k < towers.Count; k++)
        {
            MeshObject(ground, "Floor_Tower" + k, RingFill($"GIN_Floor_WalkTower{k}", towers[k], 0f, false, 1f), null, true);
            GameObject u = MeshObject(ground, "GroundUnder_Tower" + k, RingFill($"GIN_Floor_UnderTower{k}", towers[k], GroundUnderY, false, 0.5f), Mat("GIN_FL_GrassStones"), false);
            u.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // 재료 건물(룬 건물, B안): Building_Q1~Q4 루트(EscapeMapSetup → QuadrantBuilding) 아래 GIN_RuneBuilding, 정문이 술래방을 본다.
    // 룬(재료) 건물 4: 지상 Q2·Q3, 지하 UQ1·UQ4(§58). 정문이 맵 가운데를 본다.
    private static void BuildRuneBuildings(Transform map, Transform ug)
    {
        GameObject asset = Building("GIN_RuneBuilding");
        foreach ((int q, float r, float deg, bool under) in RuneGround.Select(x => (x.q, x.r, x.deg, false)).Concat(RuneUnderground.Select(x => (x.q, x.r, x.deg, true))))
        {
            Transform group = Group(under ? ug : map, "Buildings");
            Vector3 p = Polar(r, deg);
            var root = new GameObject($"Building_Q{q}").transform;
            root.SetParent(group, false);
            root.SetPositionAndRotation(p + Vector3.up * (under ? UgY : 0f), FaceCenter(p));
            GameObject bld = RoundMapKit.Spawn(asset, root, root.position, root.rotation);
            foreach (InteractableDoor d in bld.GetComponentsInChildren<InteractableDoor>(true))
            {
                var so = new SerializedObject(d);
                so.FindProperty("doorId").stringValue = $"{root.name}_{d.name.Replace("Door_", "")}";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (under) TrimRoofBeam(bld.transform);
            RoundMapKit.Placement pl = under ? placedUg : placed;
            Take(pl, bld.transform, 0.85f);
            pl.MonsterSeeds.Add(root.position + Vector3.up * 0.2f);
        }
    }

    // 지하 룬 건물의 지붕 빛기둥(110 m)은 동굴 천장을 뚫고 지상에 섰다 — 천장 바로 아래에서 끝나게 줄인다(바닥 쪽 끝은 그대로).
    private static void TrimRoofBeam(Transform building)
    {
        foreach (Transform beam in building.GetComponentsInChildren<Transform>(true).Where(t => t.name == "RoofBeam").ToList())
        {
            Renderer[] parts = beam.GetComponentsInChildren<Renderer>(true);
            if (parts.Length == 0) continue;
            float Min() => parts.Min(r => r.bounds.min.y);
            float Max() => parts.Max(r => r.bounds.max.y);
            float bottom = Min(), top = Max(), want = CeilingY - 0.3f;
            if (top <= want || want <= bottom) continue;
            Vector3 sc = beam.localScale;
            beam.localScale = new Vector3(sc.x, sc.y * (want - bottom) / (top - bottom), sc.z);
            beam.position += Vector3.up * (bottom - Min());
        }
    }

    private static void Take(RoundMapKit.Placement pl, Transform t, float k)
    {
        Bounds b = RoundMapKit.LocalBounds(t);
        pl.Take(t.TransformPoint(new Vector3(b.center.x, 0f, b.center.z)), new Vector2(b.extents.x, b.extents.z).magnitude * k);
    }

    // 사분면별 건물(일반·숨는 건물). 숨는 건물(GIN_HD_)은 HideBuildings, 나머지는 지상 Landmarks·지하 Buildings에 둔다.
    private static int BuildPlan((string name, float r, float deg)[][] plan, Transform parent, float y, string tag, RoundMapKit.Placement pl)
    {
        int n = 0;
        for (int q = 0; q < plan.Length; q++)
            for (int k = 0; k < plan[q].Length; k++)
            {
                (string name, float r, float deg) = plan[q][k];
                Transform group = Group(parent, name.StartsWith("GIN_HD_") ? "HideBuildings" : y < 0f ? "Buildings" : "Landmarks");
                if (PlaceBuilding(name, group, Polar(r, deg) + Vector3.up * y, $"{tag}{q + 1}_{k}", pl)) n++;
            }
        return n;
    }

    private static bool PlaceBuilding(string name, Transform group, Vector3 p, string tag, RoundMapKit.Placement pl)
    {
        GameObject asset = Building(name);
        if (asset == null) { Debug.LogWarning($"[GingerbreadMap] building {name} missing"); return false; }
        GameObject bld = RoundMapKit.Spawn(asset, group, p, FaceCenter(Flat(p)));
        bld.name = $"{name}_{tag}";
        foreach (InteractableDoor d in bld.GetComponentsInChildren<InteractableDoor>(true))
        {
            var so = new SerializedObject(d);
            so.FindProperty("doorId").stringValue = $"{bld.name}_{d.name.Replace("Door_", "")}";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Take(pl, bld.transform, 0.85f);
        pl.MonsterSeeds.Add(p + Vector3.up * 0.2f);
        return true;
    }

    // 둥근 울타리(지상, 출구 없음): 겉모습 GIN_PillarFence를 이어 세우고, 충돌은 겹친 상자 고리.
    private static void BuildFence(Transform map)
    {
        Transform fence = Group(map, "Fence");
        float r = MapRadius;
        int segments = Mathf.RoundToInt(2f * Mathf.PI * (r + 0.4f) / FenceStep);
        for (int i = 0; i < segments; i++)
        {
            Vector3 p = Polar(r + 0.4f, (i + 0.5f) * 360f / segments);
            SpawnProp("GIN_PillarFence", fence, p, FaceCenter(p), false);
        }
        RoundMapKit.FenceColliders(Group(fence, "Colliders"), r, new float[0], 0f, FenceHeight, FenceThickness);
    }

    // 큰길(술래방 → 울타리) 양옆 가로등·사탕 지팡이, 둥근 산책길 가로등·울타리, 사분면 입구 아치.
    private static int BuildGroundRoads(Transform map)
    {
        Transform roads = Group(map, "Roads");
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Polar(1f, AxisDeg[i]), perp = new Vector3(dir.z, 0f, -dir.x);
            int k = 0;
            for (float t = 20f; t < MapRadius - 8f; t += 10f, k++)
            {
                if (Mathf.Abs(t - RingRadius) < RingHalf + 3f) continue;
                foreach (int s in new[] { -1, 1 })
                {
                    Vector3 p = dir * t + perp * (s * RoadRow);
                    if (!placed.IsFree(p, 0.8f)) continue;
                    string prop = k % 2 == 0 ? "GIN_StreetLamp" : "GIN_CandyCane";
                    if (SpawnProp(prop, roads, p, Quaternion.LookRotation(-perp * s)) != null) { n++; placed.Take(p, 0.8f); }
                }
            }
        }
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f + 7.5f;
            if (RoundMapKit.AngleTo(a, AxisDeg) < 8f) continue;
            Vector3 p = Polar(RingRadius + RingHalf + 1.2f, a);
            if (placed.IsFree(p, 0.8f) && SpawnProp("GIN_StreetLamp", roads, p, Quaternion.identity) != null) { n++; placed.Take(p, 0.8f); }
            Vector3 f = Polar(RingRadius - RingHalf - 0.8f, a);   // 안쪽 낮은 울타리(사이사이 틈 — 지나갈 수 있다)
            if (i % 3 != 0 && placed.IsFree(f, 1.4f) && SpawnProp("GIN_CandyFence", roads, f, FaceCenter(f) * Quaternion.Euler(0f, 90f, 0f)) != null) { n++; placed.Take(f, 1.4f); }
        }
        for (int q = 0; q < 4; q++)
        {
            Vector3 p = Polar(RingRadius + RingHalf + 5f, QuadDeg[q]);   // 산책길에서 사분면으로 들어가는 아치
            if (SpawnProp("GIN_CandyArch", roads, p, FaceCenter(p)) != null) { n++; placed.Take(p + Polar(1.7f, QuadDeg[q] + 90f), 0.6f); placed.Take(p - Polar(1.7f, QuadDeg[q] + 90f), 0.6f); }
            Vector3 s = Polar(RingRadius + RingHalf + 9f, QuadDeg[q]);
            if (placed.IsFree(s, 2f) && SpawnProp("GIN_SignPost", roads, s, FaceCenter(s)) != null) { n++; placed.Take(s, 1f); }   // 안쪽 건물 문 앞이면 세우지 않는다(§60)
        }
        return n;
    }

    // 사분면 꾸미기: 재료 건물 앞 작은 광장(벤치·사탕 기둥) + 빈자리에 흩어 놓기(큰길·산책길·건물과 떨어지고 서로 4 m 넘게 — 괴물 통로).
    private static int BuildGroundQuadrants(Transform map, System.Random rng)
    {
        (string name, int count)[] list =
        {
            ("GIN_CandyTree", 5), ("GIN_FrostedPineTree", 6), ("GIN_Lollipop", 4), ("GIN_Cupcake", 3), ("GIN_GiantCookie", 2), ("GIN_Macaron", 2),
            ("GIN_MeringueSwirl", 2), ("GIN_GiftBoxes", 3), ("GIN_GingerbreadMan", 2), ("GIN_HeartCookie", 2), ("GIN_ChocolateBar", 2),
            ("GIN_CherryPillar", 2), ("GIN_CakeBench", 3), ("GIN_CandyCart", 1), ("GIN_BannerStand", 2), ("GIN_WoodFence", 2), ("GIN_StreetLamp", 4),
        };
        int n = 0;
        for (int q = 0; q < 4; q++)
        {
            float b = QuadDeg[q];
            Transform group = Group(map, $"Quadrant_Q{q + 1}");
            foreach ((int rq, float rr, float rb) in RuneGround.Where(x => x.q == q + 1))
            {
                Vector3 front = Polar(rr - 22f, rb);   // 재료 건물 앞 광장
                foreach (float d in new[] { -1f, 1f })
                {
                    Vector3 p = front + Polar(7f, rb + 90f * d);
                    if (placed.IsFree(p, 1.5f) && SpawnProp("GIN_CakeBench", group, p, FaceCenter(p)) != null) { n++; placed.Take(p, 1.3f); }
                    Vector3 c = front + Polar(10f, rb + 90f * d) + Polar(4f, rb);
                    if (placed.IsFree(c, 1f) && SpawnProp("GIN_CherryPillar", group, c, Quaternion.identity) != null) { n++; placed.Take(c, 0.6f); }
                }
            }
            n += Scatter(group, b, list, rng, placed, RingRadius + RingHalf + 3f, MapRadius - 8f, RoadRow, RingRadius, RingHalf);
        }
        return n;
    }

    private static int Scatter(Transform group, float baseDeg, (string name, int count)[] list, System.Random rng, RoundMapKit.Placement pl,
                               float rMin, float rMax, float roadRow, float ring, float ringHalf, float y = 0f)
    {
        int n = 0;
        foreach ((string name, int count) in list)
        {
            float reach = Reach(name);
            int got = 0;
            for (int tries = 0; tries < 4000 && got < count; tries++)
            {
                float r = Mathf.Lerp(rMin, rMax - reach, (float)rng.NextDouble());
                float a = baseDeg + Mathf.Lerp(-42f, 42f, (float)rng.NextDouble());
                Vector3 p = Polar(r, a);
                if (Mathf.Abs(p.x) < roadRow + reach + 2f || Mathf.Abs(p.z) < roadRow + reach + 2f) continue;
                if (ring > 0f && Mathf.Abs(r - ring) < ringHalf + reach + 1f) continue;
                if (!pl.IsFree(p, reach + 4f)) continue;
                Quaternion rot = FaceCenter(p) * Quaternion.Euler(0f, rng.Next(5) * 20f - 40f, 0f);
                if (SpawnProp(name, group, p + Vector3.up * y, rot) == null) break;
                pl.Take(p, reach);
                got++; n++;
            }
        }
        return n;
    }

    // ---------------------------------------------------------------- underground

    // 동굴: 바닥판(보이지 않음, 충돌) · 천장(아래를 보는 판, 계단 구멍 비움) · 둥근 돌벽(출구 자리 비움, 겉 + 충돌 고리) · 출구 뒤 굴(양옆·뒤 벽).
    private static void BuildCave(Transform ug)
    {
        Transform cave = Group(ug, "Cave");
        MeshObject(cave, "Floor", SquareWithHole("GIN_UG_Floor_Walk", UgFloorHalf, Rect.MinMaxRect(0f, 0f, 0f, 0f), UgY, false, 1f), null, true);
        GameObject ceiling = MeshObject(cave, "Ceiling", SquareWithHoles("GIN_UG_Ceiling", UgFloorHalf, TowerHoles(), CeilingY, true, 0.25f), Mat("rock_dark"), false);
        ceiling.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        for (int k = 0; k < towers.Count; k++)
        {
            GameObject c = MeshObject(cave, "Ceiling_Tower" + k, RingFill($"GIN_UG_CeilingTower{k}", towers[k], CeilingY, true, 0.25f), Mat("rock_dark"), false);
            c.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        }

        // 벽 겉모습(안쪽을 보는 원통, 위로 갈수록 조금 안쪽으로 기운다)
        const int seg = 240;
        float gapHalfDeg = (ExitGapHalf + 0.4f) / UgRadius * Mathf.Rad2Deg;
        var mb = new MeshBuild();
        float h = CeilingY - UgY;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * 360f / seg, a1 = (i + 1) * 360f / seg;
            if (AxisDeg.Any(e => Mathf.Abs(Mathf.DeltaAngle((a0 + a1) / 2f, e)) < gapHalfDeg)) continue;
            Vector3 b0 = Polar(UgRadius + 0.5f, a0) + Vector3.up * UgY, b1 = Polar(UgRadius + 0.5f, a1) + Vector3.up * UgY;
            Vector3 t0 = Polar(UgRadius - 3f, a0) + Vector3.up * CeilingY, t1 = Polar(UgRadius - 3f, a1) + Vector3.up * CeilingY;
            float u0 = a0 * Mathf.Deg2Rad * UgRadius / 4f, u1 = a1 * Mathf.Deg2Rad * UgRadius / 4f;
            mb.Quad(b0, b1, t1, t0, new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, h / 4f), new Vector2(u0, h / 4f));   // 가운데에서 보이는 면
        }
        MeshObject(cave, "Wall", mb.ToMesh("GIN_UG_CaveWall"), Mat("rock_purple"), false);

        // 벽 충돌 고리(출구 자리만 비움)
        Transform cols = Group(cave, "Colliders");
        const int colSeg = 200;
        for (int i = 0; i < colSeg; i++)
        {
            float a = (i + 0.5f) * 360f / colSeg;
            if (AxisDeg.Any(e => Mathf.Abs(Mathf.DeltaAngle(a, e)) * Mathf.Deg2Rad * UgRadius < ExitGapHalf + 1.2f)) continue;
            Vector3 p = Polar(UgRadius + 0.5f, a);
            var t = new GameObject($"COL_CaveWall_{i:000}").transform;
            t.SetParent(cols, false);
            t.SetPositionAndRotation(p + Vector3.up * (UgY + h / 2f), Quaternion.LookRotation(p));
            t.gameObject.AddComponent<BoxCollider>().size = new Vector3(2f * UgRadius * Mathf.Sin(Mathf.PI / colSeg) + 0.3f, h, 1f);
        }
        // 출구 자리 문틀 옆 돌기둥(벽 틈 끝을 막는다)
        foreach (float e in AxisDeg)
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 d = Polar(1f, e), perp = new Vector3(d.z, 0f, -d.x);
                Vector3 p = d * (UgRadius + 0.3f) + perp * (s * (ExitGapHalf + 1.1f));
                Box(cols.parent, $"ExitJamb_{e:000}_{(s < 0 ? "L" : "R")}", p + Vector3.up * (UgY + h / 2f), Quaternion.LookRotation(d), new Vector3(2.2f, h, 2.4f), Mat("rock_purple"));
            }
    }

    private const float ExitGapHalf = 4.2f;    // 출구 자리 벽 틈 반 폭 = 테스트 맵 탈출구 기둥 바깥 끝(문틀 8.4 m) — 굴 옆벽이 기둥에 붙는다

    private static void Box(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;   // 부모 기준(나선 묶음은 탑 방향으로 돌아 있다)
        go.transform.localRotation = rot;
        go.transform.localScale = size;
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<BoxCollider>();
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    // 시계탑 뒷벽 통로 → 다리(평평) → 돌기둥을 도는 나선 내리막(폭 5 m, 18°) → 동굴 바닥.
    // 나선 처음 25°는 평평한 층계참(통로 쪽 끝은 막는다). 바깥 가장자리 낮은 담(1.3 m), 안쪽은 가운데 돌기둥.
    // 나선 묶음 로컬 좌표: +Z = 맵 가운데 → 시계탑 바깥쪽, 탑은 (0, 0, TowerRadius)에서 −Z(가운데)를 본다.
    private static void BuildHelix(Transform ug, int k)
    {
        Transform helix = Group(ug, "Helix_" + k);
        helix.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Polar(1f, TowerDeg[k])));
        string tag = k.ToString();
        float zBack = TowerRadius - TowerBackLocal;             // 통로 바깥(로컬 z)
        const float bridge = 1.5f;
        Vector3 helixCentre = new Vector3(0f, 0f, zBack + bridge + HelixWidth / 2f + HelixRadius);
        float rIn = HelixRadius - HelixWidth / 2f, rOut = HelixRadius + HelixWidth / 2f;
        float tan = Mathf.Tan(HelixSlopeDeg * Mathf.Deg2Rad);
        float drop = TowerTunnelY - UgY;
        float runDeg = drop / tan / HelixRadius * Mathf.Rad2Deg;
        float startDeg = -90f - HelixLandingDeg * 0.5f, flatEnd = -90f + HelixLandingDeg * 0.5f, endDeg = flatEnd + runDeg;
        float Y(float deg) => TowerTunnelY - Mathf.Max(0f, (deg - flatEnd) * Mathf.Deg2Rad * HelixRadius * tan);
        Vector3 P(float r, float deg, float y) => helixCentre + Polar(r, deg) + Vector3.up * y;

        var top = new MeshBuild();      // 걷는 면(충돌)
        var side = new MeshBuild();     // 밑면·옆면·담(겉), 담은 충돌도
        var curb = new MeshBuild();
        int steps = Mathf.CeilToInt((endDeg - startDeg) / 3f);
        for (int i = 0; i < steps; i++)
        {
            float a0 = startDeg + (endDeg - startDeg) * i / steps, a1 = startDeg + (endDeg - startDeg) * (i + 1) / steps;
            float y0 = Y(a0), y1 = Y(a1);
            float s0 = (a0 - startDeg) * Mathf.Deg2Rad * HelixRadius / 3f, s1 = (a1 - startDeg) * Mathf.Deg2Rad * HelixRadius / 3f;
            Vector3 i0 = P(rIn, a0, y0), o0 = P(rOut, a0, y0), i1 = P(rIn, a1, y1), o1 = P(rOut, a1, y1);
            top.Quad(i0, i1, o1, o0, new Vector2(0f, s0), new Vector2(0f, s1), new Vector2(HelixWidth / 3f, s1), new Vector2(HelixWidth / 3f, s0));
            Vector3 d = Vector3.down * HelixThickness;
            side.Quad(i0 + d, o0 + d, o1 + d, i1 + d, new Vector2(0f, s0), new Vector2(1.6f, s0), new Vector2(1.6f, s1), new Vector2(0f, s1));   // 밑면(아래를 본다)
            side.Quad(o0 + d, o0, o1, o1 + d, new Vector2(s0, 0f), new Vector2(s0, 0.2f), new Vector2(s1, 0.2f), new Vector2(s1, 0f));            // 바깥 옆면
            if (Mathf.Abs((a0 + a1) / 2f + 90f) < BridgeGapDeg) continue;   // 다리가 붙는 자리는 담을 비운다
            Vector3 u = Vector3.up * HelixCurb;
            Vector3 c0 = P(rOut - 0.5f, a0, y0), c1 = P(rOut - 0.5f, a1, y1);
            curb.Quad(c0, c1, c1 + u, c0 + u, new Vector2(s0, 0f), new Vector2(s1, 0f), new Vector2(s1, 0.4f), new Vector2(s0, 0.4f));            // 담 안쪽
            curb.Quad(o0, o0 + u, o1 + u, o1, new Vector2(s0, 0f), new Vector2(s0, 0.4f), new Vector2(s1, 0.4f), new Vector2(s1, 0f));            // 담 바깥
            curb.Quad(c0 + u, c1 + u, o1 + u, o0 + u, new Vector2(s0, 0f), new Vector2(s1, 0f), new Vector2(s1, 0.15f), new Vector2(s0, 0.15f));  // 담 위
        }
        // 통로 쪽 끝막이(층계참 서쪽 끝)
        {
            float y = TowerTunnelY;
            Vector3 i0 = P(rIn, startDeg, y), o0 = P(rOut, startDeg, y), u = Vector3.up * 2.4f;
            curb.Quad(i0, i0 + u, o0 + u, o0, Vector2.zero, new Vector2(0f, 0.8f), new Vector2(1.6f, 0.8f), new Vector2(1.6f, 0f));
            curb.Quad(o0, o0 + u, i0 + u, i0, Vector2.zero, new Vector2(0f, 0.8f), new Vector2(1.6f, 0.8f), new Vector2(1.6f, 0f));
        }
        // 다리: 통로(시계탑 안 0.6 m 겹침) → 층계참 바깥 가장자리
        {
            var bm = new MeshBuild();
            bm.FlatRect(-1.6f, zBack - 0.6f, 1.6f, helixCentre.z - rOut + 0.6f, TowerTunnelY, false, 1f / 3f);
            MeshObject(helix, "Bridge", bm.ToMesh("GIN_UG_HelixBridge" + tag), Mat("stone_lilac_rough"), true);
            foreach (int s in new[] { -1, 1 })
                Box(helix, s < 0 ? "BridgeWall_L" : "BridgeWall_R", new Vector3(s * 1.85f, TowerTunnelY + 1.6f, (zBack + helixCentre.z - rOut) / 2f), Quaternion.identity,
                    new Vector3(0.5f, 3.2f, helixCentre.z - rOut - zBack + 1.2f), Mat("stone_lilac_rough"));
        }
        MeshObject(helix, "Ramp", top.ToMesh("GIN_UG_HelixRamp" + tag), Mat("stone_lilac_rough"), true);
        MeshObject(helix, "RampUnder", side.ToMesh("GIN_UG_HelixUnder" + tag), Mat("stone_purple"), false);
        MeshObject(helix, "Curb", curb.ToMesh("GIN_UG_HelixCurb" + tag), Mat("stone_lilac"), true);
        // 가운데 돌기둥(바닥 → 천장)
        {
            var cm = new MeshBuild();
            const int n = 48;
            float r = rIn + 0.05f, y0 = UgY, y1 = CeilingY;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * 360f / n, a1 = (i + 1) * 360f / n;
                float u0 = a0 * Mathf.Deg2Rad * r / 3f, u1 = a1 * Mathf.Deg2Rad * r / 3f;
                Vector3 b0 = helixCentre + Polar(r, a0) + Vector3.up * y0, b1 = helixCentre + Polar(r, a1) + Vector3.up * y0;
                cm.Quad(b0, b0 + Vector3.up * (y1 - y0), b1 + Vector3.up * (y1 - y0), b1, new Vector2(u0, 0f), new Vector2(u0, (y1 - y0) / 3f), new Vector2(u1, (y1 - y0) / 3f), new Vector2(u1, 0f));
            }
            MeshObject(helix, "Column", cm.ToMesh("GIN_UG_HelixColumn" + tag), Mat("ginger_rough"), true);
        }
        Vector3 world = helix.TransformPoint(helixCentre);
        helixCentres.Add(new Vector3(world.x, 0f, world.z));
        placedUg.Take(new Vector3(world.x, 0f, world.z), rOut + 1.5f);
        // 나선 바닥 끝 앞자리(괴물 출발점)
        placedUg.MonsterSeeds.Add(helix.TransformPoint(P(HelixRadius, endDeg + 20f, UgY + 0.1f) + Polar(HelixWidth + 2f, endDeg + 20f)));
    }

    public static IReadOnlyList<Vector3> HelixCentres => helixCentres;
    public static Vector3 HelixCentre => helixCentres.Count > 0 ? helixCentres[0] : Vector3.zero;

    // 출구 4(지하 12·3·6·9시): 테스트 맵 탈출구(문틀·문·받침·트리거)를 동굴 벽 틈으로 옮기고, 받침 뒤를 굴(양옆·뒤 벽·천장)로 막는다.
    private static int BuildExits(Transform map, Transform ug)
    {
        Transform exits = map.Find("Exits");
        if (exits == null) return 0;
        exits.SetParent(ug, true);
        Transform alcoves = Group(ug, "ExitAlcoves");
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            Transform e = exits.Find(ExitNames[i]);
            if (e == null) continue;
            Vector3 d = Polar(1f, AxisDeg[i]);
            Vector3 old = Flat(e.position);
            float yaw = Vector3.SignedAngle(old.normalized, d, Vector3.up);
            e.SetPositionAndRotation(d * UgRadius + Vector3.up * UgY, Quaternion.Euler(0f, yaw, 0f) * e.rotation);
            Vector3 perp = new Vector3(d.z, 0f, -d.x);
            float depth = 12f, hgt = 9f;
            Material rock = Mat("rock_purple");
            foreach (int s in new[] { -1, 1 })
                Box(alcoves, $"Alcove_{ExitNames[i]}_{(s < 0 ? "L" : "R")}", d * (UgRadius + depth / 2f) + perp * (s * (ExitGapHalf + 0.6f)) + Vector3.up * (UgY + hgt / 2f), Quaternion.LookRotation(d), new Vector3(1.2f, hgt, depth), rock);
            Box(alcoves, $"Alcove_{ExitNames[i]}_Back", d * (UgRadius + depth + 0.6f) + Vector3.up * (UgY + hgt / 2f), Quaternion.LookRotation(d), new Vector3(ExitGapHalf * 2f + 2.4f, hgt, 1.2f), rock);
            Box(alcoves, $"Alcove_{ExitNames[i]}_Roof", d * (UgRadius + depth / 2f) + Vector3.up * (UgY + hgt + 0.5f), Quaternion.LookRotation(d), new Vector3(ExitGapHalf * 2f + 2.4f, 1f, depth + 1.2f), rock);
            Box(alcoves, $"Alcove_{ExitNames[i]}_Top", d * (UgRadius + 0.3f) + Vector3.up * ((UgY + hgt + CeilingY) / 2f + 0.5f), Quaternion.LookRotation(d), new Vector3(ExitGapHalf * 2f + 2.4f, CeilingY - UgY - hgt, 2.4f), rock);
            placedUg.Take(d * UgRadius, 9f);
            n++;
        }
        return n;
    }

    // 지하 꾸미기: 길 양옆 등불 기둥(돌 아치)·화살표 안내판, 출구 앞 해골 문, 벽 쪽 수정·바위·뾰족 바위, 건물 옆 수레·통·상자, 흩어 놓기.
    private static int BuildUgDeco(Transform ug, System.Random rng)
    {
        Transform deco = Group(ug, "Deco");
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Polar(1f, AxisDeg[i]), perp = new Vector3(dir.z, 0f, -dir.x);
            for (float t = 16f; t < UgRadius - 10f; t += 14f)
                foreach (int s in new[] { -1, 1 })
                {
                    Vector3 p = dir * t + perp * (s * (UgRoadHalf + 2.5f));
                    if (!placedUg.IsFree(p, 1.2f)) continue;
                    string prop = Mathf.RoundToInt(t / 14f) % 2 == 0 ? "GIN_UG_Banner" : "GIN_UG_Crystals";
                    if (SpawnProp(prop, deco, p + Vector3.up * UgY, Quaternion.LookRotation(-perp * s)) != null) { n++; placedUg.Take(p, Reach(prop)); }
                }
            Vector3 sign = dir * 24f + perp * (UgRoadHalf + 3f);
            if (placedUg.IsFree(sign, 1.5f) && SpawnProp("GIN_UG_ArrowSign", deco, sign + Vector3.up * UgY, Quaternion.LookRotation(-perp)) != null) { n++; placedUg.Take(sign, 1.3f); }
            Vector3 arch = dir * (UgRadius - 14f);   // 출구 앞 돌 아치(길 위, 아치 아래로 지나간다)
            if (SpawnProp("GIN_UG_CrownArch", deco, arch + Vector3.up * UgY, Quaternion.LookRotation(-dir)) != null)
            {
                n++;
                placedUg.Take(arch + perp * 1.6f, 0.6f); placedUg.Take(arch - perp * 1.6f, 0.6f);
            }
        }
        (string name, int count)[] list =
        {
            ("GIN_UG_Crystals", 4), ("GIN_UG_RockSpire", 3), ("GIN_UG_RockPile", 3), ("GIN_UG_RuinWall", 2), ("GIN_UG_StoneFence", 2), ("GIN_UG_Barrels", 2),
            ("GIN_UG_CrateStack", 2), ("GIN_UG_MineCart", 1), ("GIN_UG_StoneArch", 1), ("GIN_UG_SkullPortal", 1), ("GIN_UG_Banner", 1),
        };
        for (int q = 0; q < 4; q++)
            n += Scatter(Group(deco, $"Quadrant_UQ{q + 1}"), QuadDeg[q], list, rng, placedUg, 16f, UgRadius - 4f, UgRoadHalf, 0f, 0f, UgY);
        return n;
    }

    // ---------------------------------------------------------------- tiles (visual only)

    // 1 m 칸 사각형(타일 재질 한 장 = 1 m, 칸마다 90° 돌림)을 32 m 조각·종류마다 메시 하나로 묶는다.
    // 지상: 큰길 체크, 산책길·술래방 둘레 왕관 돌, 사분면 풀밭 돌(쿠키 무늬 섞음). 지하: 돌 벽돌, 길·가운데 광장 보라 왕관, 빛나는 금 간 돌 섞음.
    private const float TileChunk = 32f, TileY = 0.06f;
    private static int BuildTiles(Transform map, Transform ug, System.Random rng)
    {
        foreach (string old in AssetDatabase.FindAssets("GIN_Tiles_ t:Mesh", new[] { GeneratedFolder })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(old));
        var cells = new Dictionary<string, List<(Vector2 p, int rot)>>();
        void Add(string kind, string level, Vector2 c)
        {
            string key = $"{level}_{kind}_{Mathf.FloorToInt(c.x / TileChunk)}_{Mathf.FloorToInt(c.y / TileChunk)}";
            if (!cells.TryGetValue(key, out var l)) cells[key] = l = new List<(Vector2, int)>();
            l.Add((c, rng.Next(4)));
        }
        int count = Mathf.CeilToInt(MapRadius + 1f);
        for (int ix = -count; ix < count; ix++)
            for (int iz = -count; iz < count; iz++)
            {
                var c = new Vector2(ix + 0.5f, iz + 0.5f);
                float r = c.magnitude, ax = Mathf.Abs(c.x), az = Mathf.Abs(c.y);
                if (r <= MapRadius + 1f && r > RoomRadius - 0.3f && !UnderOwnFloor(c))
                {
                    string kind = ax < RoadHalf || az < RoadHalf ? "Checker" : Mathf.Abs(r - RingRadius) < RingHalf || r < 18f ? "CrownTan"
                        : rng.NextDouble() < 0.05 ? "Cookie" : "GrassStones";
                    Add(kind, "G", c);
                }
                if (r <= UgRadius + 1f || (ax < ExitGapHalf && r < UgRadius + 12f) || (az < ExitGapHalf && r < UgRadius + 12f))
                {
                    string kind = ax < UgRoadHalf || az < UgRoadHalf || r < 14f ? "CrownPurple" : rng.NextDouble() < 0.06 ? "CrackedGlow" : "StoneBrick";
                    Add(kind, "U", c);
                }
            }
        Transform gTiles = Group(map, "Tiles"), uTiles = Group(ug, "Tiles");
        int total = 0;
        foreach (var kv in cells)
        {
            string[] k = kv.Key.Split('_');
            bool under = k[0] == "U";
            Material mat = Mat("GIN_FL_" + k[1]);
            var mb = new MeshBuild();
            float y = (under ? UgY : 0f) + TileY;
            Vector2[] uv = { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            foreach ((Vector2 p, int rot) in kv.Value)
            {
                Vector3 a = new Vector3(p.x - 0.5f, y, p.y - 0.5f), b = new Vector3(p.x - 0.5f, y, p.y + 0.5f), c = new Vector3(p.x + 0.5f, y, p.y + 0.5f), d = new Vector3(p.x + 0.5f, y, p.y - 0.5f);
                mb.Quad(a, b, c, d, uv[rot % 4], uv[(rot + 1) % 4], uv[(rot + 2) % 4], uv[(rot + 3) % 4]);
            }
            var go = MeshObject(under ? uTiles : gTiles, "Tiles_" + kv.Key, mb.ToMesh("GIN_Tiles_" + kv.Key), mat, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            total += kv.Value.Count;
        }
        return total;
    }

    private static bool UnderOwnFloor(Vector2 c)
    {
        foreach ((Transform root, Rect rect) in noTiles)
        {
            Vector3 l = root.InverseTransformPoint(new Vector3(c.x, 0f, c.y));
            if (rect.Contains(new Vector2(l.x, l.z))) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- lights, ambience, kill zone

    // 지상: 가로등마다 점광원. 지하: 길 등불·나선 기둥 둘레·수정·아치·출구 앞. 건물 안 빛은 프리팹에 있다.
    private static int BuildLights(Scene scene, Transform map, Transform ug)
    {
        GameObject lighting = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MapLighting");
        Transform parent = lighting != null ? lighting.transform : map;
        Transform old = parent.Find("GIN_Lights");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform lights = new GameObject("GIN_Lights").transform;
        lights.SetParent(parent, false);
        int n = 0;
        foreach (Transform t in map.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            string src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name;
            if (src == "GIN_StreetLamp" && !t.IsChildOf(map.Find("Background"))) { RoundMapKit.Point(lights, t.position + Vector3.up * 3.5f, 15f, 2.2f, LampColor); n++; }
            else if (src == "GIN_UG_Crystals") { RoundMapKit.Point(lights, t.position + Vector3.up * 2f, 9f, 1.6f, UgLampColor); n++; }
            else if (src == "GIN_UG_CrownArch" || src == "GIN_UG_StoneArch" || src == "GIN_UG_SkullPortal") { RoundMapKit.Point(lights, t.position + Vector3.up * 3.5f, 14f, 2.2f, UgWarmColor); n++; }
        }
        // 왕좌 방: 가운데 보라 빛 + 왕좌 앞 + 문 위
        RoundMapKit.Point(lights, Vector3.up * 7f, 18f, 2.2f, RoomColor); n++;
        RoundMapKit.Point(lights, new Vector3(0f, 4f, 4f), 9f, 2.4f, LampColor); n++;
        for (int i = 0; i < 4; i++) { RoundMapKit.Point(lights, Polar(RoomRadius + 1.2f, AxisDeg[i]) + Vector3.up * 4.5f, 9f, 2.6f, RoomColor); n++; }
        // 나선 기둥 둘레(높이마다 4방향)
        for (float y = UgY + 3f; y < TowerTunnelY + 1f; y += 8f)
            foreach (Vector3 hc in helixCentres)
                for (int k = 0; k < 4; k++) { RoundMapKit.Point(lights, hc + Polar(HelixRadius + HelixWidth + 1f, k * 90f + 45f) + Vector3.up * y, 14f, 1.8f, UgWarmColor); n++; }
        // 동굴 길·가운데 광장
        for (int i = 0; i < 4; i++)
            for (float t = 10f; t < UgRadius; t += 18f) { RoundMapKit.Point(lights, Polar(t, AxisDeg[i]) + Vector3.up * (UgY + 6f), 18f, 1.6f, UgLampColor); n++; }
        RoundMapKit.Point(lights, Vector3.up * (UgY + 10f), 26f, 2.4f, UgLampColor); n++;
        // 지하 건물 정문 앞
        Transform ugBld = ug.Find("Buildings");
        if (ugBld != null)
            foreach (Transform b in ugBld)
            { RoundMapKit.Point(lights, b.TransformPoint(new Vector3(0f, 6f, 18f)), 16f, 2f, UgWarmColor); n++; }
        return n;
    }

    // 환경음: 땅 위 바탕(새·바람)은 땅 위만, 동굴 소리는 동굴·시계탑 계단.
    private static void SetAmbience(Scene scene)
    {
        foreach (AmbientZone z in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AmbientZone>(true)))
        {
            var so = new SerializedObject(z);
            SerializedProperty boxes = so.FindProperty("boxes");
            if (z.name == "AMB_Bed")
            {
                boxes.arraySize = 1;
                boxes.GetArrayElementAtIndex(0).boundsValue = new Bounds(new Vector3(0f, 49.5f, 0f), new Vector3(2f * (MapRadius + 60f), 101f, 2f * (MapRadius + 60f)));
            }
            else if (z.name == "AMB_Underground")
            {
                boxes.arraySize = 1 + towers.Count;
                boxes.GetArrayElementAtIndex(0).boundsValue = new Bounds(new Vector3(0f, (UgY + CeilingY) / 2f, 0f), new Vector3(2f * (UgRadius + 16f), CeilingY - UgY + 2f, 2f * (UgRadius + 16f)));
                for (int k = 0; k < towers.Count; k++)   // 시계탑 계단
                    boxes.GetArrayElementAtIndex(1 + k).boundsValue = new Bounds(towers[k].position + Vector3.down * 2f, new Vector3(9f, 4.4f, 9f));
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void MoveKillZone(Scene scene)
    {
        GameObject kill = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<VoidKillZone>() != null);
        if (kill == null) return;
        kill.transform.position = new Vector3(0f, UgY - 40f, 0f);
        var box = kill.GetComponent<BoxCollider>();
        if (box != null) box.size = new Vector3(2f * FloorHalf + 200f, 4f, 2f * FloorHalf + 200f);
    }

    // ---------------------------------------------------------------- checks

    // 건물끼리(같은 층)·지하 건물과 나선이 겹치지 않는지 — 겉모습 범위(로컬 상자)를 바닥에 그린 사각형끼리 잰다.
    private static string CheckOverlaps(Transform map)
    {
        var names = new HashSet<string>(PlanNames) { "GIN_RuneBuilding", "GIN_ClockTower", "GIN_ThroneRoom" };
        var blds = map.GetComponentsInChildren<Transform>(true)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && names.Contains(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name ?? ""))
            .Select(t => (t, poly: Footprint(t), y: t.position.y)).ToList();
        var issues = new List<string>();
        for (int i = 0; i < blds.Count; i++)
        {
            for (int j = i + 1; j < blds.Count; j++)
                if (Mathf.Abs(blds[i].y - blds[j].y) < 5f && PolyOverlap(blds[i].poly, blds[j].poly)) issues.Add(blds[i].t.name + "x" + blds[j].t.name);
            foreach (Vector3 hc in helixCentres)
                if (blds[i].y < UgY + 5f && PolyDistance(blds[i].poly, new Vector2(hc.x, hc.z)) < HelixRadius + HelixWidth / 2f + 1f) issues.Add(blds[i].t.name + "xHelix");
            float far = blds[i].poly.Max(p => p.magnitude);
            float limit = blds[i].y < UgY + 5f ? UgRadius - 3f : MapRadius - 3f;
            if (far > limit) issues.Add($"{blds[i].t.name} reaches {far:F0} m");
        }
        return issues.Count == 0 ? $"overlaps 0 ({blds.Count} buildings)" : "OVERLAPS " + string.Join(", ", issues);
    }

    private static Vector2[] Footprint(Transform t)
    {
        Bounds b = RoundMapKit.LocalBounds(t);
        var c = new Vector2[4];
        float[,] xz = { { b.min.x, b.min.z }, { b.max.x, b.min.z }, { b.max.x, b.max.z }, { b.min.x, b.max.z } };
        for (int i = 0; i < 4; i++)
        {
            Vector3 w = t.TransformPoint(new Vector3(xz[i, 0], 0f, xz[i, 1]));
            c[i] = new Vector2(w.x, w.z);
        }
        return c;
    }

    // 볼록 사각형 두 개(분리축)
    private static bool PolyOverlap(Vector2[] a, Vector2[] b)
    {
        foreach (Vector2[] poly in new[] { a, b })
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 e = poly[(i + 1) % poly.Length] - poly[i], n = new Vector2(-e.y, e.x);
                float aMin = a.Min(p => Vector2.Dot(p, n)), aMax = a.Max(p => Vector2.Dot(p, n));
                float bMin = b.Min(p => Vector2.Dot(p, n)), bMax = b.Max(p => Vector2.Dot(p, n));
                if (aMax < bMin || bMax < aMin) return false;
            }
        return true;
    }

    private static float PolyDistance(Vector2[] poly, Vector2 p)
    {
        int pos = 0, neg = 0;
        float best = float.MaxValue;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Length], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, (a + ab * t - p).magnitude);
            if (ab.x * (p.y - a.y) - ab.y * (p.x - a.x) < 0f) neg++; else pos++;
        }
        return pos == 0 || neg == 0 ? 0f : best;   // 모든 변의 같은 쪽 = 안
    }
}
