using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Heaven Of Bakery 실제 모델 맵 씬(Request1009Plan.md §89 HB6). Test_HeavenOfBakery를 복사한 HeavenOfBakery 씬의 놀이 공간을
// Blender로 만든 HOB_ 모델(§89 HB1~HB3)로 바꾼다:
//   떠 있는 섬(r 100) · 가운데 둥근 광장 + 술래방 정자 · 북쪽 고원 슬롯 3(NW·NE 12 m, N 15 m — 높이는 슬롯에 고정, 결정 D1) ·
//   고원 위 서브 맵 묶음 3(아몬드·초콜릿·밀가루 — 판마다 슬롯이 바뀐다, MapLayoutRandomizer) · 나선 경사로 2(W→NW, E→NE, 입구 건물 없음 D5) ·
//   NW ↔ 가운데 발판 ↔ NE 다리 + 발판 → N 계단 · 탈출구 4(N은 다리 밑 초콜릿 절벽 앞, E·S·W는 섬 둘레) · 과일 밭 자리 4(재료 건물 1 + 일반 건물 3) ·
//   길 타일 · 꾸미기 · 둘레 울타리 · 섬 둘레 폭포 4 · 구름.
// 재료 건물 모델이 아직 없어 Building_Q1·Q3·Q4는 테스트 씬의 임시 정자(수리 자리·소켓·빛 포함)를 옮겨 쓰고, Q2는 초콜릿 분수(결정 D6)다.
// 일반 건물 General_1~3은 빈 자리(표지판만)다. 여러 번 실행해도 같은 결과(고정 난수).
public static class HeavenOfBakeryMapBuilder
{
    public const string MapName = "HeavenOfBakery";
    public const string ScenePath = "Assets/Scenes/Maps/" + MapName + ".unity";
    public const string GeneratedFolder = "Assets/09. Environment/HeavenOfBakery/Generated";
    private const string PrefabDir = HobPrefabBuilder.PrefabDir;

    // ---- 섬
    // public const float FieldRadius = 100f, FenceRadius = 96f;
    public const float FieldRadius = 85f, FenceRadius = 81f;          // §89.24 큰 섬 r 85(하늘 섬 68 m의 약 1.25배)
    public const float PlazaScale = 1.67f, PlazaTop = 0.82f;
    // public const float MapRadius = 190f;                 // §89.13 떠 있는 섬 3개가 큰 섬 밖 r 150~187
    // public const float MapRadius = 245f;
    public const float MapRadius = 270f;   // 2026-11-19 하늘 섬 고도 48 m: 섬 중심 r 192 + 반지름 68 + 여유                    // §89.24 하늘 섬 중심 r 168 + 반지름 68 + 여유
    // public const float MapRadius = 130f;                 // 예전: 고원이 큰 섬 가장자리에 걸침(r ≈ 124)
    private const float FenceHeight = 6f, FenceThickness = 0.8f;

    // ---- 술래방(정자, 문 N·E·S·W)
    public const float RoomInner = 5.8f, RoomOuter = 6.6f, RoomWallHeight = 4.5f, RoomDoorWidth = 3.0f;
    public const float GuardInnerHalf = 3.8f;                // 둥근 방 안쪽 정사각형 반 폭(5.8 · 0.707 ≈ 4.1 − 여유)
    public const float RoomHalfForAi = 6.6f;                 // EscapeManager.monsterRoomHalfSize(AI 길 굽기 못 걷는 상자)
    public static readonly float[] AxisDeg = { 90f, 0f, 270f, 180f };   // N, E, S, W

    // ---- 서브 맵 슬롯(앞 −Z = 맵 가운데). 고원 모델 · 윗면 높이 · 평평한 윗면 반지름 · 바닥 반지름
    public static readonly (string name, Vector3 pos, string plateau, float top, float topR, float footR)[] Slots =
    {
        // §89.13 하늘에 떠 있는 섬: 큰 섬(r ≈ 103) 밖으로 약 15 m 틈, 윗면 높이 2배(12 → 24, 15 → 30). 모델 원점 = 윗면.
        // ("SubSlot_NW", new Vector3(-88.17f, 0f, 121.35f), "HOB_Plateau_Almond", 24f, 30.5f, 33f),   // r 150, 126°
        // ("SubSlot_N", new Vector3(0f, 0f, 152f), "HOB_Plateau_Choco", 30f, 32.5f, 35f),
        // ("SubSlot_NE", new Vector3(88.17f, 0f, 121.35f), "HOB_Plateau_Almond", 24f, 30.5f, 33f),    // r 150, 54°
        // §89.24 하늘 섬 3개 같은 모델(r 68)·같은 높이(24 m), 섬 중심 r 168, 120° 간격 — 슬롯 0 = 8시(210°), 1 = 12시(90°), 2 = 4시(330°)
        ("SubSlot_SW", Polar(SkyDist, 210f), "HOB_Plateau_Sky68", SkyTop, SkyR, SkyR + 2f),
        ("SubSlot_N", Polar(SkyDist, 90f), "HOB_Plateau_Sky68", SkyTop, SkyR, SkyR + 2f),
        ("SubSlot_SE", Polar(SkyDist, 330f), "HOB_Plateau_Sky68", SkyTop, SkyR, SkyR + 2f),
        // 예전(큰 섬에 붙은 고원): NW (−66, 64) 12 m · N (0, 92) 15 m · NE (66, 64) 12 m
    };
    public static readonly string[] SubMapNames = { "SubMap_Almond", "SubMap_Chocolate", "SubMap_Flour" };
    // public const float SkyR = 68f, SkyTop = 24f, SkyDist = 168f;
    public const float SkyR = 68f, SkyTop = 48f, SkyDist = 192f;   // 2026-11-19 고도 24 → 48 m(하늘에 뜬 섬으로 보이게), 경사로가 길어진 만큼 섬도 24 m 바깥으로     // §89.24 하늘 섬 반지름·윗면 높이·섬 중심 거리(경사로 꼭대기가 섬 가장자리에 바로 닿게)

    // ---- 경사로·다리·계단
    // private static readonly Vector3 RampDirNW = new Vector3(0.087f, 0f, -0.996f).normalized;   // 예전 나선 경사로(고원 → 아래)
    // private static readonly Vector3 RampDirNE = new Vector3(-0.087f, 0f, -0.996f).normalized;
    // private const float RampLength = 32f, RampTopInset = 0.5f;
    // §89.13 큰 섬 가장자리 지그재그 경사로(HOB_SwitchRamp_24m, 다리 3줄·간격 5 m) → 꼭대기 끝(r 93, 24 m)에서 하늘 다리로 NW·NE 섬
    // private const float RampTopR = 93f, RampLane = 5f, RampEndY = 32f, RampBackY = 4.2f;   // 모델: 1줄 시작 (+5, 0), 꼭대기 끝 (−5, 32), 뒤로 4.2 m
    // §89.24 HOB_SwitchRamp_Wide_24m: 폭 9 m·줄 간격 10.5 m·한 줄 12.2 m(길 72.6 m = 예전의 75%). 모델 원점(가운데 줄 아래) = 큰 섬 r 83.5,
    // 1줄 시작 (+10.5, 0) = 큰 섬 위 땅, 꼭대기 끝 (−10.5, 18.2) = 하늘 섬 가장자리(높이 24 m). 뒤 머리핀은 y −5.25(큰 섬 위 13.5 m 높이).
    // private const float RampBaseR = 83.5f, RampLane = 10.5f, RampEndY = 18.2f, RampBackY = 5.6f, RampWidth = 9f;
    private const float RampBaseR = 83.5f, RampLane = 10.5f, RampEndY = 42.4f, RampBackY = 5.6f, RampWidth = 9f;   // 48 m 경사로(한 줄 36.4 m, 길 145 m, 기울기 그대로)
    public const float BridgeY = 24f, BridgeZ = 103.5f, PadHalf = 3.5f;   // 가운데 발판(7 × 7 m, 작은 떠 있는 바위 위) — 예전 12 m · z 48.5
    private const float SpanMax = 10f;                                  // 다리 한 토막 최대 길이(모델 10 m)
    // private const int SpanSegments = 4;

    // ---- 탈출구
    public static readonly (string name, string key, Vector3 pos)[] Exits =
    {
        // ("Exit_N", "N", new Vector3(0f, 0f, 93f)), ("Exit_E", "E", new Vector3(93f, 0f, 0f)),   // N: 예전 (0, 46) 다리 밑 절벽 앞 → 다른 셋처럼 가장자리
        // ("Exit_S", "S", new Vector3(0f, 0f, -93f)), ("Exit_W", "W", new Vector3(-93f, 0f, 0f)),
        // §89.24 큰 섬 r 85: 가장자리 r 77, 경사로 입구(90·210·330° 둘레)와 재료 건물 자리(270°) 사이
        ("Exit_N", "N", Polar(77f, 30f)), ("Exit_E", "E", Polar(77f, 150f)),
        ("Exit_S", "S", Polar(77f, 240f)), ("Exit_W", "W", Polar(77f, 300f)),
    };
    private static readonly RoundMapKit.ExitSize Exit = new RoundMapKit.ExitSize { Opening = 4f, PostWidth = 1.5f, Height = 5f, PadDepth = 6f };
    private static readonly Color ExitColor = new Color(1f, 0.82f, 0.4f);
    private static readonly Color RoomColor = new Color(1f, 0.45f, 0.7f);

    // ---- 과일 밭 자리(재료 건물 1 + 일반 건물 3이 판마다 나뉘어 들어간다)
    // public static readonly float[] LotDeg = { 200f, 250f, 290f, 340f };
    public static readonly float[] LotDeg = { 270f };   // §89.24 1층은 재료 건물(딸기잼 공장)만 — 일반 건물 3채는 맵에 두지 않는다
    // public const float LotRadius = 55f, LotHalf = 8f;
    // public const float LotRadius = 55f, LotHalf = 12.5f;   // §89.22 베이커리 건물(폭 16~24 m)이 들어가게 자리 16 → 25 m
    public const float LotRadius = 50f, LotHalf = 12.5f;   // §89.24 큰 섬 r 85 — 둥근 길(r 25) 밖·섬 가장자리 안(자리 크기는 B안 건물 크기에서 다시 정한다)
    // public static readonly float[] WaterfallDeg = { 205f, 235f, 305f, 335f };
    public static readonly float[] WaterfallDeg = { 0f, 60f, 120f, 180f };   // §89.24 경사로(90·210·330°)·탈출구 피해
    // public static readonly float[] CookieSpawnDeg = { 215f, 245f, 295f, 325f };
    public static readonly float[] CookieSpawnDeg = { 45f, 135f, 200f, 340f };   // §89.24 재료 건물 자리(270°) 피해
    // public const float WitchDeg = 315f;
    public const float WitchDeg = 270f;   // §89.24 아몬드(210°)·밀(330°) 섬 사이 빈 하늘

    private static readonly RoundMapKit.Placement placed = new RoundMapKit.Placement();
    private static readonly List<(Vector3 a, Vector3 b, float half)> pathSegs = new List<(Vector3, Vector3, float)>();
    private static readonly List<(Vector3 c, Quaternion r, Vector2 half)> keepOut = new List<(Vector3, Quaternion, Vector2)>();
    private static readonly Transform[] slotRoots = new Transform[3];
    private static readonly Transform[] contentRoots = new Transform[3];
    // private static readonly Transform[] lotRoots = new Transform[4];
    // private static readonly Transform[] generals = new Transform[3];
    private static readonly Transform[] lotRoots = new Transform[LotDeg.Length];
    private static readonly Transform[] generals = new Transform[0];   // §89.24 1층 일반 건물 없음
    private static readonly Transform[] placeholders = new Transform[4];
    // §89.20 테스트 맵 정자는 부품이 Z-위(−Z = 위)로 놓여 뿌리에 X 90°가 있어야 선다. 자리에 놓을 때 Y 회전만 주면 옆으로 누워
    // 절반이 땅(고원 윗면) 속에 묻혔고, 튀어나온 벽·기둥 충돌체에 술래가 끼었다 → 원래 "위" 축을 위로 돌리는 보정을 기억해 곱한다.
    private static readonly Quaternion[] placeholderTilt = new Quaternion[4];
    public static string LastReport { get; private set; } = "";

    [MenuItem("Tools/TagOfChaos/Maps/Heaven Of Bakery/Build Map Scene")]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[HeavenOfBakeryMap] stop Play Mode first"); return; }
        Debug.Log(Build());
    }

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "[HeavenOfBakeryMap] stop Play Mode first";
        if (P("HOB_Island_Main") == null || P("HOB_Wheat") == null || P("HOB_Waterfall") == null)
            return "[HeavenOfBakeryMap] prefabs missing — run Heaven Of Bakery/Build Prefabs and the waterfall builder first";
        placed.Clear(); pathSegs.Clear(); keepOut.Clear();
        string source = TestMapBuilder.ScenePath(MapName);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(source, ScenePath)) return "[HeavenOfBakeryMap] copy failed";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        HobPrefabBuilder.EnsureFolder(GeneratedFolder);

        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        // 테스트 맵의 임시 건물(수리 자리·소켓·빛이 붙은 정자)은 재료 건물 모델이 올 때까지 쓴다
        for (int q = 0; q < 4; q++)
        {
            placeholders[q] = map.Find($"Building_Q{q + 1}");
            placeholderTilt[q] = placeholders[q] != null ? UprightTilt(placeholders[q]) : Quaternion.identity;
            if (placeholders[q] != null) placeholders[q].SetParent(null, true);
        }
        foreach (Transform child in map.Cast<Transform>().ToList())
            if (child.name != WitchPath.FarBackdropName) Object.DestroyImmediate(child.gameObject);
        Transform far = Group(map, WitchPath.FarBackdropName);
        foreach (Transform child in far.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
        foreach (GameObject g in scene.GetRootGameObjects().Where(g => g.name == "MapDoors").ToList()) Object.DestroyImmediate(g);
        GameObject oldEscape = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EscapeMapSetup.RootName);
        if (oldEscape != null)
            foreach (Transform child in oldEscape.transform.Cast<Transform>().ToList())
                if (child.name != "WITCH_Anchor") Object.DestroyImmediate(child.gameObject);

        var rng = new System.Random(1117);
        Transform ground = Group(map, WitchPath.GroundName);   // 마녀가 부수지 않는 땅(섬·고원·경사로·다리)
        BuildIsland(ground);
        BuildMonsterRoom(map);
        BuildSlots(ground);
        BuildRamps(ground);
        // BuildBridge(ground);   // §89.24 다리 모두 없앰(N 발판·6 m 계단·하늘 다리)
        BuildSkyCannons(ground);   // §89.24 섬마다 대포 2개(다른 두 섬으로) + 착지 자리
        int exits = BuildExits(map);
        BuildLots(map);
        Physics.SyncTransforms();
        int subDeco = BuildSubMaps(map, rng);
        int paths = BuildPaths(map);
        int rim = BuildRim(map);
        int falls = BuildWaterfalls(Group(map, "Background"));
        int clouds = BuildClouds(far, rng);
        int fieldDeco = BuildFieldDeco(map, rng);
        Physics.SyncTransforms();
        int markers = BuildFieldMarkers(map, rng) + BuildSubMapMarkers(rng);
        subDeco += FillWheat(contentRoots[2], new System.Random(1719));
        subDeco += BuildPlazaDecor(map);   // §89.19 큰 섬 중앙 광장 둘레(Map/Environment)
        SetLook(scene);
        SetAmbience(scene);
        MoveKillZone(scene);
        foreach (Transform t in placeholders) if (t != null && t.parent == null) Object.DestroyImmediate(t.gameObject);

        string escape = EscapeMapSetup.Apply(scene, MapName);
        int moved = ReparentSubMapChests(scene);
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
            var so = new SerializedObject(manager);
            so.FindProperty("monsterRoomHalfSize").floatValue = RoomHalfForAi;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }
        BindLayout(scene);
        ClearBakedNavMesh(scene);
        RoundMapKit.MoveSpawn(scene, SceneSpawnPoints.Cookie, map, "Spawn_Cookie_Q1");
        RoundMapKit.MoveSpawn(scene, SceneSpawnPoints.Monster, map, "Spawn_Monster");
        // §89.13 마녀는 떠 있는 섬이 없는 남동쪽 하늘(315°)에서 걸어온다 — 북쪽 섬을 뚫고 오지 않게
        Transform witchAnchor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "WITCH_Anchor");
        if (witchAnchor != null) witchAnchor.position = Polar(10f, WitchDeg) + Vector3.up * witchAnchor.position.y;
        RoundMapKit.MoveWitchAnchor(scene, MapRadius, 70f);
        int floor = BuildCloudFloor(scene, map.Find(WitchPath.FarBackdropName), new System.Random(915));
        MarkStatic(map);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RoundMapKit.AddToBuildSettings(ScenePath, MapName);
        LastReport = $"[HeavenOfBakeryMap] {ScenePath}: exits {exits}, sub-map deco {subDeco}, field deco {fieldDeco}, path tiles {paths}, rim {rim}, " +
                     $"waterfalls {falls}, clouds {clouds}, markers {markers}, sub-map chests {moved} | {escape}";
        return LastReport;
    }

    // ---------------------------------------------------------------- helpers

    private static Vector3 Polar(float r, float deg) => RoundMapKit.Polar(r, deg);
    private static Quaternion FaceCenter(Vector3 p) => RoundMapKit.FaceCenter(p);
    private static Quaternion Outward(Vector3 p) => RoundMapKit.FrontToCenter(p);
    private static Transform Group(Transform parent, string name) => RoundMapKit.Group(parent, name);
    private static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);
    private static GameObject P(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");

    private static GameObject Put(string prefab, Transform parent, Vector3 pos, Quaternion rot, bool colliders = true, Vector3? scale = null)
    {
        GameObject asset = P(prefab);
        if (asset == null) { Debug.LogWarning($"[HeavenOfBakeryMap] prefab {prefab} missing"); return null; }
        GameObject go = RoundMapKit.Spawn(asset, parent, pos, rot, colliders);
        if (scale.HasValue) go.transform.localScale = scale.Value;
        return go;
    }

    // 묶음 로컬 자세로 놓는다(서브 맵 내용·과일 밭 건물은 판마다 옮겨진다).
    private static GameObject PutLocal(string prefab, Transform parent, Vector3 local, float yaw, bool colliders = true, float scale = 1f)
    {
        GameObject go = Put(prefab, parent, parent.TransformPoint(local), parent.rotation * Quaternion.Euler(0f, yaw, 0f), colliders);
        if (go != null && !Mathf.Approximately(scale, 1f)) go.transform.localScale = Vector3.one * scale;
        return go;
    }

    private static Transform Empty(Transform parent, string name, Vector3 pos, Quaternion rot)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(pos, rot);
        return t;
    }

    private static float SegDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P2 = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
        Vector2 ab = B - A;
        float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(P2 - A, ab) / ab.sqrMagnitude);
        return (P2 - (A + ab * t)).magnitude;
    }

    private static bool OnPath(Vector3 p, float r) => pathSegs.Any(s => SegDistance(p, s.a, s.b) < s.half + r)
                                                      || Mathf.Abs(Flat(p).magnitude - 24f) < 4f + r;

    private static bool InKeepOut(Vector3 p, float r)
    {
        foreach ((Vector3 c, Quaternion rot, Vector2 half) in keepOut)
        {
            Vector3 l = Quaternion.Inverse(rot) * (p - c);
            if (Mathf.Abs(l.x) < half.x + r && Mathf.Abs(l.z) < half.y + r) return true;
        }
        return false;
    }

    private static bool UnderPlateau(Vector3 p, float r) => Slots.Any(s => (Flat(p) - Flat(s.pos)).magnitude < s.footR + 2f + r);

    // ---------------------------------------------------------------- island, plaza, monster room

    private static void BuildIsland(Transform ground)
    {
        // Put("HOB_Island_Main", ground, Vector3.zero, Quaternion.identity);
        Put("HOB_Island_Main85", ground, Vector3.zero, Quaternion.identity);   // §89.24 r 85
        Put("HOB_RoundPlaza", ground, Vector3.zero, Quaternion.identity, true, new Vector3(PlazaScale, 1f, PlazaScale));
        placed.Take(Vector3.zero, 9.6f * PlazaScale + 1f);
    }

    private static void BuildMonsterRoom(Transform map)
    {
        var room = Empty(map, "MonsterRoom", new Vector3(0f, PlazaTop, 0f), Quaternion.identity);
        GameObject model = Put("HOB_TaggerGazebo", room, room.position, Quaternion.identity);
        if (model != null) model.name = "Model";
        RoundMapKit.RoomRing(room, RoomInner, RoomOuter, RoomWallHeight, RoomDoorWidth, AxisDeg);
        Material glow = RoundMapKit.BeaconMaterial(GeneratedFolder, "HOB_RoomDoorGlow", RoomColor * 1.2f, 1f, 1.5f, 1f);
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Polar(1f, AxisDeg[i]);
            Transform door = RoundMapKit.RoomDoor(room, i, dir, RoomInner, RoomOuter, RoomWallHeight, RoomDoorWidth, null);
            // 문 겉모습: 사탕 대문(충돌은 문 상자가 맡는다 — 괴물만 지나간다)
            GameObject gate = Put("HOB_Gate", door, door.position, door.rotation, false, new Vector3(RoomDoorWidth / 3.2f, 1.1f, 1f));
            if (gate != null) gate.name = "DoorModel";
            RoundMapKit.BeaconPart(room, "DoorGlow_" + i, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), glow,
                dir * (RoomOuter + 1.5f) + Vector3.up * 0.12f, Quaternion.Euler(90f, 0f, 0f), new Vector3(4.5f, 4.5f, 1f));
        }
        // 보이는 낮은 왕관 울타리(보이지 않는 벽이 어디인지 알려 준다), 문 자리는 비움
        Transform fence = Group(room, "RoomFence");
        int n = Mathf.RoundToInt(2f * Mathf.PI * RoomOuter / 2.1f);
        for (int k = 0; k < n; k++)
        {
            float a = (k + 0.5f) * 360f / n;
            if (AxisDeg.Any(d => Mathf.Abs(Mathf.DeltaAngle(a, d)) < (RoomDoorWidth / 2f + 1.1f) / RoomOuter * Mathf.Rad2Deg)) continue;
            Vector3 p = room.position + Polar(RoomOuter - 0.2f, a);
            Put("HOB_CrownFence_2m", fence, p, Quaternion.LookRotation(Polar(1f, a)), false);
        }
        Empty(room, "Spawn_Monster", room.position + new Vector3(0f, 0.6f, 0f), Quaternion.identity);
    }

    // ---------------------------------------------------------------- slots (plateaus + rim fences)

    private static Vector3 BridgeEnd(int slot)
    {
        Vector3 c = Flat(Slots[slot].pos);
        Vector3 start = new Vector3(slot == 0 ? -PadHalf : PadHalf, 0f, BridgeZ);
        return c + (start - c).normalized * (Slots[slot].topR - 1f);
    }

    // private static Vector3 RampDir(int slot) => slot == 0 ? RampDirNW : RampDirNE;
    // private static Vector3 RampTop(int slot) => Flat(Slots[slot].pos) + RampDir(slot) * (Slots[slot].topR - RampTopInset);
    // private static Vector3 RampBase(int slot) => RampTop(slot) + RampDir(slot) * RampLength;
    // §89.13 경사로: 큰 섬 가운데 → 떠 있는 섬 방향(out). 모델 로컬 +Y(Blender) = out, +X(Blender) = 월드 −right.
    private static Vector3 RampOut(int slot) => Flat(Slots[slot].pos).normalized;
    private static Quaternion RampRot(int slot) => Quaternion.LookRotation(-RampOut(slot));
    private static Vector3 RampRight(int slot) => RampRot(slot) * Vector3.right;
    private static Vector3 RampLocal(int slot, float x, float y) => RampOrigin(slot) - RampRight(slot) * x + RampOut(slot) * y;
    // public static Vector3 RampTopEnd(int slot) => RampOut(slot) * RampTopR;                                   // 꼭대기 끝(24 m) — 하늘 다리가 시작
    // private static Vector3 RampOrigin(int slot) => RampTopEnd(slot) - RampOut(slot) * RampEndY - RampRight(slot) * RampLane;
    // §89.24 모델 원점(가운데 줄) = 큰 섬 r 83.5. 꼭대기 끝은 3줄 쪽(−x) — 하늘 섬 가장자리에 바로 닿는다(다리 없음)
    private static Vector3 RampOrigin(int slot) => RampOut(slot) * RampBaseR;
    public static Vector3 RampTopEnd(int slot) => RampLocal(slot, -RampLane, RampEndY);
    public static Vector3 RampEntry(int slot) => RampLocal(slot, RampLane, -3f);                              // 1줄 시작 앞(땅)
    private static Vector3 SkyBridgeEnd(int slot) => Flat(Slots[slot].pos) - RampOut(slot) * (Slots[slot].topR - 1f);

    // 고원 가장자리에서 길이 이어지는 곳(울타리를 비운다): (방향, 반 폭 m)
    private static IEnumerable<(Vector3 dir, float half)> SlotGaps(int s)
    {
        Vector3 c = Flat(Slots[s].pos);
        // if (s == 1) { yield return (Vector3.back, 3f); yield break; }   // N: 계단
        // yield return (-RampOut(s), 3f);                          // 큰 섬 경사로에서 오는 하늘 다리
        // yield return ((BridgeEnd(s) - c).normalized, 2.6f);
        yield return ((Flat(RampTopEnd(s)) - c).normalized, RampWidth / 2f + 1f);   // §89.24 나선 경사로 꼭대기만(다리 없음)
    }

    private static void BuildSlots(Transform ground)
    {
        Transform group = Group(ground, "Slots");
        for (int s = 0; s < Slots.Length; s++)
        {
            var slot = Slots[s];
            Vector3 c = Flat(slot.pos);
            GameObject plateau = Put(slot.plateau, group, c + Vector3.up * slot.top, Outward(c));   // §89.13 모델 원점 = 윗면(떠 있는 섬)
            if (plateau != null) plateau.name = $"Plateau_{slot.name.Substring(8)}";
            if (plateau != null) NoGrassTop(plateau, "Cream");   // §89.17 서브 맵 고원 윗면에 초록 잔디 없음(서브 맵 바닥이 덮지 못하는 가장자리는 크림 돌)
            slotRoots[s] = Empty(group, slot.name, c + Vector3.up * slot.top, Outward(c));
            // 윗면 둘레 울타리(떨어지지 않게): 겉은 왕관 울타리, 충돌은 겹친 상자 고리. 길이 이어지는 곳은 비운다.
            Transform fence = Group(slotRoots[s], "RimFence");
            float r = slot.topR - 0.7f;
            var gaps = SlotGaps(s).Select(g => (deg: Mathf.Atan2(g.dir.z, g.dir.x) * Mathf.Rad2Deg, g.half)).ToList();
            bool InGap(float a, float extra) => gaps.Any(g => Mathf.Abs(Mathf.DeltaAngle(a, g.deg)) < (g.half + extra) / r * Mathf.Rad2Deg);
            int n = Mathf.RoundToInt(2f * Mathf.PI * r / 2.1f);
            for (int k = 0; k < n; k++)
            {
                float a = (k + 0.5f) * 360f / n;
                if (InGap(a, 1.1f)) continue;
                Vector3 p = slotRoots[s].position + Polar(r, a);
                Put("HOB_CrownFence_2m", fence, p, Quaternion.LookRotation(Polar(1f, a)), false);
            }
            const int cols = 120;
            for (int k = 0; k < cols; k++)
            {
                float a = (k + 0.5f) * 360f / cols;
                if (InGap(a, 0f)) continue;
                Vector3 d = Polar(1f, a);
                var t = new GameObject($"COL_Rim_{k:000}").transform;
                t.SetParent(fence, false);
                t.SetPositionAndRotation(slotRoots[s].position + d * (r + 0.4f) + Vector3.up * 1.5f, Quaternion.LookRotation(d));
                t.gameObject.AddComponent<BoxCollider>().size = new Vector3(2f * r * Mathf.Sin(Mathf.PI / cols) + 0.3f, 3f, 0.6f);
            }
            placed.Take(c, slot.footR + 2f);
        }
    }

    // ---------------------------------------------------------------- ramps, bridge, stairs

    private static void BuildRamps(Transform ground)
    {
        Transform group = Group(ground, "Ramps");
        // for (int s = 0; s <= 2; s += 2)   // 예전: NW·NE 두 곳(N은 다리 발판 + 계단)
        for (int s = 0; s < Slots.Length; s++)   // §89.24 세 섬 모두 나선 경사로(다리 없음)
        {
            // GameObject ramp = Put("HOB_SwitchRamp_24m", group, RampOrigin(s), RampRot(s));
            // GameObject ramp = Put("HOB_SwitchRamp_Wide_24m", group, RampOrigin(s), RampRot(s));
            GameObject ramp = Put("HOB_SwitchRamp_Wide_48m", group, RampOrigin(s), RampRot(s));
            // if (ramp != null) ramp.name = s == 0 ? "Ramp_W" : "Ramp_E";
            if (ramp != null) ramp.name = $"Ramp_{Slots[s].name.Substring(8)}";
            float mid = (RampEndY - RampBackY) / 2f;
            keepOut.Add((RampLocal(s, 0f, mid), RampRot(s), new Vector2(RampLane + RampWidth / 2f + 2f, (RampEndY + RampBackY) / 2f + 2f)));
            // 꼭대기 끝 → 떠 있는 섬 가장자리: 하늘 다리
            // Spans(group, RampTopEnd(s) + Vector3.up * BridgeY, SkyBridgeEnd(s) + Vector3.up * BridgeY, s == 0 ? "SkyW" : "SkyE");
        }
    }

    // §89.24 섬마다 대포 2개: 슬롯 s → 다른 두 슬롯 t. 대포는 가장자리 12 m 안쪽·옆으로 7 m(같은 쌍의 두 방향이 겹치지 않게), 포구 = 목표 섬.
    // 착지 자리는 목표 섬 가장자리 18 m 안쪽. 슬롯 아래에 둔다(섬 내용은 판마다 옮겨지지만 섬 자리·방향은 그대로).
    public const float CannonInset = 12f, LandingInset = 18f, CannonSide = 7f;
    private static void BuildSkyCannons(Transform ground)
    {
        Transform group = Group(ground, "SkyCannons");
        int id = 0;
        for (int s = 0; s < Slots.Length; s++)
            for (int t = 0; t < Slots.Length; t++)
            {
                if (t == s) continue;
                Vector3 cs = Flat(Slots[s].pos), ct = Flat(Slots[t].pos), d = (ct - cs).normalized, side = Vector3.Cross(Vector3.up, d) * CannonSide;
                Vector3 at = cs + d * (SkyR - CannonInset) + side + Vector3.up * SkyTop;
                Vector3 land = ct - d * (SkyR - LandingInset) + side + Vector3.up * SkyTop;
                GameObject gun = Put("HOB_SkyCannon", group, at, Quaternion.LookRotation(d), false);
                // if (gun != null) gun.name = $"SkyCannon_{id}_{Slots[s].name.Substring(8)}_to_{Slots[t].name.Substring(8)}";
                // Empty(group, $"SkyLanding_{id}_{Slots[t].name.Substring(8)}", land, Quaternion.LookRotation(-d));
                Transform landing = Empty(group, $"SkyLanding_{id}_{Slots[t].name.Substring(8)}", land, Quaternion.LookRotation(-d));
                if (gun != null)
                {
                    gun.name = $"SkyCannon_{id}_{Slots[s].name.Substring(8)}_to_{Slots[t].name.Substring(8)}";
                    // 몸통만 막는 상자(계단 쪽 z −2.9 뒤는 비움 — 쿠키는 계단 앞에서 E, 오르는 모습은 SkyCannonView가 그린다)
                    var box = gun.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 1.6f, 0f);
                    box.size = new Vector3(3.6f, 3.2f, 5.8f);
                    gun.AddComponent<SkyCannonView>().EditorBind(id, landing);
                }
                id++;
            }
    }

    private static void BuildBridge(Transform ground)
    {
        Transform group = Group(ground, "Bridge");
        // 가운데 발판(다리 두 쪽 + 계단이 만난다) — 다리 갑판 재질의 상자
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "BridgePad";
        pad.transform.SetParent(group, false);
        pad.transform.position = new Vector3(0f, BridgeY - 0.5f, BridgeZ);
        pad.transform.localScale = new Vector3(PadHalf * 2f, 1f, PadHalf * 2f);
        GameObject bridgeAsset = P("HOB_Bridge_10m");
        Material deck = bridgeAsset != null ? bridgeAsset.GetComponentInChildren<Renderer>().sharedMaterial : null;
        if (deck != null) pad.GetComponent<Renderer>().sharedMaterial = deck;
        // Put("HOB_ChocoPillar", group, new Vector3(0f, 0f, BridgeZ + PadHalf - 0.8f), ...);   // 예전 발판 받침 기둥 — 떠 있는 발판은 작은 바위 위
        GameObject islet = Put("HOB_SkyIslet", group, new Vector3(0f, BridgeY - 1.02f, BridgeZ), Quaternion.Euler(0f, 30f, 0f));
        if (islet != null) islet.name = "PadIslet";

        for (int s = 0; s <= 2; s += 2)
            Spans(group, new Vector3(s == 0 ? -PadHalf + 0.3f : PadHalf - 0.3f, BridgeY, BridgeZ), BridgeEnd(s) + Vector3.up * BridgeY, s == 0 ? "W" : "E");
        // 발판 → N 섬(6 m 오름): 계단은 로컬 −Z로 오른다 → 북쪽(+Z)을 보게 180°
        GameObject stairs = Put("HOB_Stairs_6m", group, new Vector3(0f, BridgeY, BridgeZ + PadHalf), Quaternion.Euler(0f, 180f, 0f));
        if (stairs != null) stairs.name = "Stairs_N";
        // 계단 위 보이지 않는 경사판(발판 24 m → 섬 30 m): 계단 턱마다 걸리지 않고 AI 길·통행 검사가 이어지게
        // 경사판은 계단 턱 모서리(30턱: 앞 0.38 m·높이 0.2 m)보다 0.15 m 이상 위 — 가까우면 통행 검사 몸이 모서리에 걸려 칸이 비었다.
        // 위 끝(층계참 위 0.5 m)에서 짧은 판이 섬 윗면 아래로 내려가 묻힌다 — 판 끝이 위로 튀어나와 계단 꼭대기에서 길이 끊기지 않게.
        float baseZ = BridgeZ + PadHalf;
        SlopeBox(group, "COL_StairsSlope", new Vector3(0f, BridgeY, baseZ - 1.2f), new Vector3(0f, BridgeY + 6.43f, baseZ + 11.4f));
        SlopeBox(group, "COL_StairsTop", new Vector3(0f, BridgeY + 6.43f, baseZ + 11.4f), new Vector3(0f, BridgeY + 5.9f, baseZ + 15f));
    }

    // 보이지 않는 걷는 판: low → high를 윗면으로 하는 4.6 m 폭 상자(두께 0.2 m)
    private static void SlopeBox(Transform group, string name, Vector3 low, Vector3 high)
    {
        var slope = new GameObject(name).transform;
        slope.SetParent(group, false);
        Vector3 along = (high - low).normalized;
        Quaternion tilt = Quaternion.LookRotation(along, Vector3.Cross(along, Vector3.right));
        slope.SetPositionAndRotation((low + high) / 2f - tilt * Vector3.up * 0.1f, tilt);
        slope.gameObject.AddComponent<BoxCollider>().size = new Vector3(4.6f, 0.2f, (high - low).magnitude);
    }

    // 다리 토막을 a → b로 잇는다(10 m 이하 토막, 기둥 없음 — 아래는 하늘). §89.13
    private static void Spans(Transform group, Vector3 a, Vector3 b, string tag)
    {
        Vector3 dir = (b - a).normalized;
        int count = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / SpanMax));
        float seg = (b - a).magnitude / count;
        for (int k = 0; k < count; k++)
        {
            Vector3 mid = a + dir * (seg * (k + 0.5f));
            GameObject span = Put("HOB_Bridge_10m", group, mid, Quaternion.LookRotation(dir), true, new Vector3(1f, 1f, seg / 10f + 0.03f));
            if (span != null) span.name = $"Span_{tag}{k}";
        }
    }

    // ---------------------------------------------------------------- exits

    private static int BuildExits(Transform map)
    {
        Transform group = Group(map, "Exits");
        int n = 0;
        foreach ((string name, string key, Vector3 pos) in Exits)
        {
            Transform root = Empty(group, name, pos, FaceCenter(pos));
            GameObject portal = Put("HOB_ExitPortal", root, root.position, root.rotation, false);
            if (portal != null) portal.name = "Portal";
            RoundMapKit.ExitColliders(root, key, Exit);
            var door = new GameObject("Door_" + key).transform;
            door.SetParent(root, false);
            var box = door.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, Exit.Height / 2f, 0f);
            box.size = new Vector3(Exit.Opening, Exit.Height, 0.8f);
            GameObject leaf = Put("HOB_Gate", door, door.position, door.rotation, false, new Vector3(Exit.Opening / 3.2f, Exit.Height / 3.2f, 1f));
            if (leaf != null) leaf.name = "DoorModel";
            RoundMapKit.ExitOpenLook(root, key, Exit, ExitColor, GeneratedFolder, "HOB_");
            placed.Take(pos, 9f);
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- lots (material building + general buildings)

    private static void BuildLots(Transform map)
    {
        Transform group = Group(map, "Lots");
        Transform buildings = Group(map, "FieldBuildings");
        for (int i = 0; i < LotDeg.Length; i++)
        {
            Vector3 p = Polar(LotRadius, LotDeg[i]);
            lotRoots[i] = Empty(group, $"Lot_{i + 1}", p, FaceCenter(p));
            Transform border = Group(lotRoots[i], "Border");
            for (int k = 0; k < 8; k++)
                foreach (int side in new[] { -1, 1 })
                {
                    float t = -LotHalf + 1f + k * 2f;
                    PutLocal("HOB_Tile_Checker", border, new Vector3(t, 0.01f, side * (LotHalf + 1f)), 0f, false, 2f);
                    PutLocal("HOB_Tile_Checker", border, new Vector3(side * (LotHalf + 1f), 0.01f, t), 0f, false, 2f);
                }
            placed.Take(p, LotHalf * 1.42f + 2f);
        }
        // 재료 건물(과일 밭 몫): 임시 정자. 일반 건물 3: 빈 자리 + 표지판(모델 대기). 기본 배치 = MaterialLot 0.
        Transform q4 = placeholders[3];
        if (q4 != null)
        {
            q4.SetParent(buildings, true);
            q4.SetPositionAndRotation(lotRoots[0].position, lotRoots[0].rotation * TiltOf(q4));   // §89.20 서 있게
            InstallBakery(q4, "HOB_BLD_JamFactory", LotYaw, true);   // §89.22 재료 건물 = 딸기잼 공장
        }
        string[] generalModels = { "HOB_BLD_FruitWarehouse", "HOB_BLD_OrangeWorkshop", "HOB_BLD_AppleWorkshop" };   // §89.22 일반 건물 3채
        // for (int g = 0; g < 3; g++)
        for (int g = 0; g < generals.Length; g++)   // §89.24 1층 일반 건물 없음(generals 0개) — 프리팹은 보관
        {
            generals[g] = Empty(buildings, $"General_{g + 1}", lotRoots[g + 1].position, lotRoots[g + 1].rotation);
            // PutLocal("HOB_Signpost", generals[g], new Vector3(0f, 0f, 0f), 0f);   // 예전 빈 자리 표지판
            GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{HobBakeryBuildings.PrefabDir}/{generalModels[g]}.prefab");
            if (pf != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, generals[g]);
                inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.Euler(0f, LotYaw, 0f);
            }
        }
    }

    // ---------------------------------------------------------------- sub-maps (content moved between slots each round)

    // 묶음 로컬: 원점 = 고원 윗면 가운데, −Z = 맵 가운데(길이 이어지는 앞쪽). 앞 ±55°(r > 12)는 비워 둔다(경사로·다리·계단이 닿는 곳).
    private static bool LocalFree(Vector3 l, float r, List<(Vector3 c, float r)> used, Rect building)
    {
        float d = new Vector2(l.x, l.z).magnitude;
        if (d + r > 26f) return false;
        if (d > 12f - r && l.z < 0f && Mathf.Abs(Mathf.Atan2(l.x, -l.z) * Mathf.Rad2Deg) < 55f) return false;
        if (l.x + r > building.xMin && l.x - r < building.xMax && l.z + r > building.yMin && l.z - r < building.yMax) return false;
        return used.All(u => (new Vector2(u.c.x - l.x, u.c.z - l.z)).magnitude >= u.r + r);
    }

    private static Vector3 LocalSpot(System.Random rng, float r, List<(Vector3 c, float r)> used, Rect building)
    {
        for (int t = 0; t < 400; t++)
        {
            float a = (float)(rng.NextDouble() * 360.0), d = 4f + (float)rng.NextDouble() * 22f;
            var l = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * d, 0f, Mathf.Cos(a * Mathf.Deg2Rad) * d);
            if (LocalFree(l, r, used, building)) { used.Add((l, r)); return l; }
        }
        return new Vector3(float.NaN, 0f, 0f);
    }

    // private static readonly Rect BuildingRect = Rect.MinMaxRect(-8.5f, 0.5f, 8.5f, 17.5f);   // 재료 건물 자리(로컬 x, z)
    private static readonly Rect BuildingRect = Rect.MinMaxRect(-11.8f, 0.0f, 11.8f, 18.2f);   // §89.22 베이커리 건물(폭 최대 22.7 m·깊이 16.7 m)
    private static readonly List<(Vector3 c, float r)>[] localUsed = { new List<(Vector3, float)>(), new List<(Vector3, float)>(), new List<(Vector3, float)>() };

    private static int Scatter(Transform content, int s, System.Random rng, (string prefab, int count, float r)[] list, Transform target = null)
    {
        if (target == null) target = Group(content, "Deco");   // §89.19 서브 맵은 Environment/<테마>_Decor로 넘긴다
        int n = 0;
        foreach ((string prefab, int count, float r) in list)
            for (int k = 0; k < count; k++)
            {
                Vector3 l = LocalSpot(rng, r, localUsed[s], BuildingRect);
                if (float.IsNaN(l.x)) continue;
                if (PutLocal(prefab, target, l, (float)(rng.NextDouble() * 360.0)) != null) n++;
            }
        return n;
    }

    private static int BuildSubMaps(Transform map, System.Random rng)
    {
        Transform group = Group(map, "SubMaps");
        int n = 0;
        for (int s = 0; s < 3; s++)
        {
            localUsed[s].Clear();
            contentRoots[s] = Empty(group, SubMapNames[s], slotRoots[s].position, slotRoots[s].rotation);   // 기본 배치 = 순서 0(아몬드 NW·초콜릿 N·밀가루 NE)
        }

        // 아몬드: 임시 재료 건물 + 아몬드 나무·자루·통·상자 무리·벤치·등
        Transform almond = contentRoots[0];
        PlaceBuilding(placeholders[0], almond, new Vector3(0f, 0f, 9f));
        InstallBakery(placeholders[0], "HOB_BLD_AlmondFactory", SubYaw, true);   // §89.22
        // §89.17 건물 자리 밖은 아몬드 타일 바닥 + 아몬드 나무만(§89.16 길 타일·울타리·소품 대신)
        // n += BuildThemeGround(almond, 0, "Almond", null);
        // n += Scatter(almond, 0, rng, new[] { ("HOB_AlmondTree", 7, 1.8f), ("HOB_AlmondCrate", 3, 0.9f), ("HOB_AlmondSackDeco", 4, 0.9f), ("HOB_AlmondBig", 2, 0.9f), ("HOB_AlmondHalf", 2, 0.8f),
        //     ("HOB_Barrel", 2, 0.6f), ("HOB_Bench", 2, 1.0f), ("HOB_PinkFlowerBush", 3, 1.1f), ("HOB_Bush_Round", 2, 0.8f) });
        SubGround(almond, "Almond", "Cream", 0f);
        n += BuildTileFloor(almond, "Almond");
        // n += Scatter(almond, 0, rng, new[] { ("HOB_AlmondTree", 14, 1.9f) });   // §89.19 무작위 14그루 → 과수원 격자 16그루(BuildAlmondDecor)
        n += BuildAlmondDecor(almond, rng);

        // 초콜릿: 재료 건물 = 초콜릿 분수(D6, 수리 자리는 분수 앞) + 초콜릿 기둥 고리·과자
        Transform choco = contentRoots[1];
        Transform q2 = placeholders[1];
        if (q2 != null)
        {
            foreach (Transform c in q2.Cast<Transform>().ToList())
                if (!(c.name.StartsWith("Repair_Point") || c.name.StartsWith("Socket_") || c.name.StartsWith("ESC_Glow"))) Object.DestroyImmediate(c.gameObject);
            PlaceBuilding(q2, choco, new Vector3(0f, 0f, 9f), false);   // 정자 부품을 지우고 분수를 Y-위로 붙이므로 기울이지 않는다
            // §89.22 초콜릿 분수 + 초콜릿 기둥 고리 → 초콜릿 공장 건물(꼭대기 퐁듀 단에서 초콜릿이 흐름)
            // GameObject fountain = Put("HOB_ChocoFountain", q2, q2.position, q2.rotation);
            // if (fountain != null) fountain.name = "Model";
            // foreach (Transform c in q2.Cast<Transform>())
            // {
            //     if (c.name.StartsWith("Repair_Point")) c.localPosition = new Vector3(0f, 1.2f, -6.8f);
            //     else if (c.name.StartsWith("Socket_")) c.localPosition = new Vector3(0f, 0.5f, -5.9f);
            //     else if (c.name.StartsWith("ESC_Glow")) c.localPosition = new Vector3(0f, 7.2f, 0f);
            // }
            // for (int k = 0; k < 6; k++)
            // {
            //     float a = 30f + k * 60f;   // 앞(−Z, 수리 자리)은 비운다
            //     PutLocal("HOB_ChocoPillar", choco, new Vector3(0f, 0f, 9f) + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * 7.5f, 0f);
            // }
            InstallBakery(q2, "HOB_BLD_ChocoFactory", SubYaw, false);
        }
        // §89.17 건물 자리 밖은 녹은 초콜릿 바닥 + 초코 퐁듀(냄비·큰 꼬치·웅덩이)와 초콜릿 소품만(§89.16 길 타일·케이크·꽃 대신)
        // n += BuildThemeGround(choco, 1, "Choco", null);
        // n += Scatter(choco, 1, rng, new[] { ("HOB_ChocoBlock", 3, 1.0f), ("HOB_Truffle", 3, 0.6f), ("HOB_HeartBonbon", 3, 0.5f), ("HOB_ChocoCube", 4, 0.5f), ("HOB_Truffles", 2, 1.2f),
        //     ("HOB_StrawberryChocoCake", 2, 1.2f), ("HOB_CandyCanes", 2, 1.0f), ("HOB_PinkFlowerBush", 3, 1.1f), ("HOB_Cupcake_Cream", 1, 0.9f) });
        SubGround(choco, "Choco", "ChocoFondue", 0.03f);
        n += BuildChocoDecor(choco, rng);   // §89.19 분수 둘레 고리·가로등·진입 타일·퐁듀 군집 다음에 웅덩이·나머지 소품
        n += ChocoPuddles(choco, 1, rng);
        // n += Scatter(choco, 1, rng, new[] { ("HOB_FonduePot", 5, 1.5f), ("HOB_FondueSkewer", 9, 0.7f), ("HOB_ChocoBlock", 4, 1.0f), ("HOB_Truffle", 4, 0.6f), ("HOB_HeartBonbon", 4, 0.5f),
        //     ("HOB_ChocoCube", 5, 0.5f), ("HOB_Truffles", 2, 1.2f) });   // §89.19 퐁듀 2묶음은 BuildChocoDecor, 나머지만 흩뿌림
        n += Scatter(choco, 1, rng, new[] { ("HOB_FonduePot", 3, 1.5f), ("HOB_FondueSkewer", 3, 0.7f), ("HOB_ChocoBlock", 4, 1.0f), ("HOB_Truffle", 4, 0.6f), ("HOB_ChocoCube", 5, 0.5f) },
                     Env(choco, "ChocolateFountain_Decor"));

        // 밀가루: 임시 재료 건물 + 밀밭 2 · 작은 절벽 폭포 · 자루 무리 · 표지판
        Transform flour = contentRoots[2];
        PlaceBuilding(placeholders[2], flour, new Vector3(0f, 0f, 9f));
        InstallBakery(placeholders[2], "HOB_BLD_WheatMill", SubYaw, true);   // §89.22
        // §89.17 건물 자리 밖 전부 밀밭(흙 바닥 + 2 m 밀밭 덩어리) — 상자 표시를 놓은 뒤 FillWheat가 깐다(예전: 8 × 7 m 밀밭 둘)
        // n += BuildWheatField(flour, 2, new Vector3(-15f, 0f, 6f), new Vector2(8f, 7f), rng);
        // n += BuildWheatField(flour, 2, new Vector3(15f, 0f, 6f), new Vector2(8f, 7f), rng);
        SubGround(flour, "Flour", "Soil", 0f);
        // §89.16 작은 절벽 폭포(가까이서 종이처럼 얇았다) 대신 섬 뒤 가장자리에서 구름으로 떨어지는 큰 폭포(HOB_Waterfall) — 안 부서지는 "Waterfalls" 묶음
        // GameObject cliff = PutLocal("HOB_WaterfallCliff", Group(flour, "Deco"), new Vector3(0f, 0f, 22f), 180f);   // 예전 절벽 + HOB_Waterfall_Small
        {
            Transform falls = Group(flour, "Waterfalls");
            Vector3 lipLocal = new Vector3(0f, -0.2f, Slots[2].topR + 1.2f);   // 절벽 기둥 앞(-0.6이면 기둥 뒤로 숨었다)
            Vector3 lip = flour.TransformPoint(lipLocal);
            GameObject fall = Put("HOB_Waterfall", falls, lip, Outward(lip - flour.position), false);
            if (fall != null) fall.name = "IslandWaterfall";
            localUsed[2].Add((new Vector3(0f, 0f, 24f), 5f));
        }
        n += BuildFlourDecor(flour, rng);   // §89.19 건물 앞 밀가루 타일 마당·자루 군집·흙길 울타리/깃발/가로등(밀밭은 FillWheat가 이들 둘레를 비우고 깖)
        // n += BuildThemeGround(flour, 2, "Flour", "HOB_WheatBanner");
        // n += Scatter(flour, 2, rng, new[] { ("HOB_WheatBundle", 5, 0.6f), ("HOB_FlourSack", 4, 0.7f), ("HOB_FlourScoop", 2, 0.8f), ("HOB_Bench", 2, 1.0f),
        //                                     ("HOB_PinkFlowerBush", 2, 1.1f), ("HOB_FlowerBed", 2, 1.1f), ("HOB_WoodFootbridge", 1, 3.2f) });   // 예전: 자루 6·표지판·다리·벤치·꽃밭
        return n;
    }

    // §89.17 쓰지 않음(서브 맵 바닥을 BuildTileFloor·SubGround·FillWheat로 바꿈)
    // // §89.16 서브 맵 바닥(구상 시트): 입구(−Z 가장자리)에서 건물 앞까지 3 m 테마 타일 길(가운데 줄에 무늬 타일) + 건물 앞 광장,
    // // 길 양옆 금 울타리 토막(사이사이 비움)과 금 가로등, 밀가루는 깃발. 타일은 부서지지 않는 "Tiles" 묶음(충돌체 없음), 나머지는 Deco(부서짐).
    // private static int BuildThemeGround(Transform content, int s, string theme, string banner)
    // {
    //     Transform tiles = Group(content, "Tiles");
    //     Transform deco = Group(content, "Deco");
    //     int n = 0;
    //     const float T = 3f;
    //     for (int row = 0; row < 9; row++)                                           // 길: z −24 … 0
    //     {
    //         float z = -24f + row * T;
    //         for (int col = -1; col <= 1; col++)
    //         {
    //             bool emblem = col == 0 && (row == 1 || row == 4 || row == 7);
    //             if (PutLocal($"HOB_Tile_{theme}{(emblem ? "" : "Plain")}", tiles, new Vector3(col * T, 0.01f, z), 0f, false) != null) n++;
    //         }
    //     }
    //     foreach (float x in new[] { -9f, -6f, 6f, 9f })                            // 건물 앞 광장 양옆
    //         for (int row = 0; row < 2; row++)
    //             if (PutLocal($"HOB_Tile_{theme}Plain", tiles, new Vector3(x, 0.01f, -3f + row * T), 0f, false) != null) n++;
    //     for (float z = -22f; z <= 0f; z += 6f) localUsed[s].Add((new Vector3(0f, 0f, z), 5f));
    //     localUsed[s].Add((new Vector3(-7.5f, 0f, -1.5f), 3.2f)); localUsed[s].Add((new Vector3(7.5f, 0f, -1.5f), 3.2f));
    //     // 길 양옆: 울타리 토막(2.2 m) 두 개 + 틈 반복, 토막 사이 기둥, 가로등·깃발
    //     foreach (int side in new[] { -1, 1 })
    //     {
    //         float x = side * 5.2f;
    //         foreach (float zc in new[] { -19.4f, -10.4f })
    //         {
    //             for (int k = 0; k < 2; k++)
    //                 if (PutLocal("HOB_GoldFence", deco, new Vector3(x, 0f, zc + k * 2.4f), 90f) != null) n++;
    //             if (PutLocal("HOB_GoldPost", deco, new Vector3(x, 0f, zc + 1.2f), 0f) != null) n++;
    //         }
    //         foreach (float z in new[] { -22.5f, -13.5f, -4.5f })
    //             if (PutLocal("HOB_GoldLamp", deco, new Vector3(x, 0f, z), 0f) != null) n++;
    //         if (banner != null)
    //             foreach (float z in new[] { -16.5f, -7.5f })
    //                 if (PutLocal(banner, deco, new Vector3(x + side * 0.4f, 0f, z), side > 0 ? -90f : 90f) != null) n++;
    //         localUsed[s].Add((new Vector3(x, 0f, -14f), 1.2f));
    //     }
    //     return n;
    // }

    // ---------------------------------------------------------------- §89.19 Environment decor (approved plan)
    // 서브 맵은 판마다 자리를 바꾸므로 테마 장식은 각 서브 맵의 Environment 아래(<테마>_Decor · Shared_Decor · Lighting_Decor · GroundTiles),
    // 큰 섬 공통 장식은 Map/Environment 아래. 타일(GroundTiles)은 부서지지 않고, 나머지는 부서지기 규칙의 묶음(containers)이라 거대 괴물이 부순다.
    // 건물 자리(BuildingRect) + 2 m와 입구 쪽 ±55°·12 m 밖(경사로·다리가 닿는 곳)은 비운다 — 자리는 모두 localUsed로 겹침을 막는다.

    private const string ChoPropDir = "Assets/04. Prefabs/ChocolateFactory/Props";
    // private static readonly Rect BuildingKeepOut = Rect.MinMaxRect(-10.5f, -1.5f, 10.5f, 19.5f);
    private static readonly Rect BuildingKeepOut = Rect.MinMaxRect(-12.8f, -2.0f, 12.8f, 20.2f);   // §89.22

    private static Transform Env(Transform content, string group) => Group(Group(content, "Environment"), group);

    // 다른 맵(초콜릿 공장) 소품 프리팹(배관·공장 기계)
    private static GameObject PutCho(string prefab, Transform parent, Vector3 local, float yaw)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ChoPropDir}/{prefab}.prefab");
        if (asset == null) { Debug.LogWarning($"[HeavenOfBakeryMap] prefab {prefab} missing"); return null; }
        return RoundMapKit.Spawn(asset, parent, parent.TransformPoint(local), parent.rotation * Quaternion.Euler(0f, yaw, 0f), true);
    }

    // 정한 자리에 놓되 이미 쓴 자리와 겹치면 건너뛴다(반지름 r로 자리 등록). keepOut=false면 건물 자리 검사를 하지 않는다(분수 둘레 고리).
    private static GameObject PlaceFixed(string prefab, Transform group, Transform content, int s, Vector3 local, float yaw, float r, bool keepOut = true, bool cho = false)
    {
        if (keepOut && local.x + r > BuildingKeepOut.xMin && local.x - r < BuildingKeepOut.xMax && local.z + r > BuildingKeepOut.yMin && local.z - r < BuildingKeepOut.yMax) return null;
        if (new Vector2(local.x, local.z).magnitude + r > SubGroundR - 1f) return null;
        if (localUsed[s].Any(u => new Vector2(u.c.x - local.x, u.c.z - local.z).magnitude < u.r + r)) return null;
        GameObject go = cho ? PutCho(prefab, group, local, yaw) : PutLocal(prefab, group, local, yaw);
        if (go != null) localUsed[s].Add((local, r));
        return go;
    }

    // 군집: 사각 영역 안에서 겹치지 않게 무작위로(최대 80번 시도), 회전은 boxy면 0/90° ± 10°, 아니면 무작위.
    private static int Cluster(Transform group, Transform content, int s, System.Random rng, Rect area, (string prefab, int count, float r, bool boxy, bool cho)[] list)
    {
        int n = 0;
        foreach (var it in list)
            for (int k = 0; k < it.count; k++)
                for (int t = 0; t < 80; t++)
                {
                    var l = new Vector3(Mathf.Lerp(area.xMin, area.xMax, (float)rng.NextDouble()), 0f, Mathf.Lerp(area.yMin, area.yMax, (float)rng.NextDouble()));
                    float yaw = it.boxy ? rng.Next(4) * 90f + (float)(rng.NextDouble() - 0.5) * 20f : (float)(rng.NextDouble() * 360.0);
                    if (PlaceFixed(it.prefab, group, content, s, l, yaw, it.r, true, it.cho) != null) { n++; break; }
                }
        return n;
    }

    private static Vector3 Jit(System.Random rng, float x, float z, float j) =>
        new Vector3(x + (float)(rng.NextDouble() - 0.5) * 2f * j, 0f, z + (float)(rng.NextDouble() - 0.5) * 2f * j);

    // 아몬드 공장: 과수원 16그루(격자 + 흔들기, 밑동 잔디 → 흙) · 서쪽 견과 창고 군집 · 동쪽 배관/공장 기계 · 입구 아몬드 조형물 · 과수원 앞 금 울타리 · 입구 길 금 가로등
    private static int BuildAlmondDecor(Transform almond, System.Random rng)
    {
        const int s = 0;
        Transform theme = Env(almond, "AlmondFactory_Decor"), shared = Env(almond, "Shared_Decor"), light = Env(almond, "Lighting_Decor");
        int n = 0;
        // 울타리·가로등·조형물·군집을 먼저(정한 자리), 나무는 그 둘레를 피해서
        foreach (int side in new[] { -1, 1 })
        {
            float x = side * 14.6f;
            foreach (float zc in new[] { -4.95f, 8.0f })
            {
                foreach (float dz in new[] { -1.25f, 1.25f })
                    if (PlaceFixed("HOB_GoldFence", shared, almond, s, new Vector3(x, 0f, zc + dz), 90f, 0.2f) != null) n++;
                foreach (float dz in new[] { -2.6f, 2.6f })
                    if (PlaceFixed("HOB_GoldPost", shared, almond, s, new Vector3(x, 0f, zc + dz), 0f, 0.2f) != null) n++;
            }
            foreach (float z in new[] { -5f, -10.5f })
                if (PlaceFixed("HOB_GoldLamp", light, almond, s, new Vector3(side * 3f, 0f, z), 0f, 0.35f) != null) n++;
        }
        if (PlaceFixed("HOB_AlmondBig", theme, almond, s, new Vector3(-4.6f, 0f, -4.2f), 28f, 0.75f) != null) n++;
        if (PlaceFixed("HOB_AlmondHalf", theme, almond, s, new Vector3(4.6f, 0f, -4.4f), -36f, 0.65f) != null) n++;
        if (PlaceFixed("HOB_AlmondHalf", theme, almond, s, new Vector3(5.7f, 0f, -2.7f), 61f, 0.65f) != null) n++;
        n += Cluster(theme, almond, s, rng, Rect.MinMaxRect(-13.9f, 3f, -11.2f, 13f), new[]
        {
            ("HOB_AlmondCrate", 3, 0.8f, true, false), ("HOB_AlmondSackDeco", 3, 0.75f, false, false),
        });
        n += Cluster(shared, almond, s, rng, Rect.MinMaxRect(-13.9f, 3f, -11.2f, 13f), new[] { ("HOB_Barrel", 2, 0.5f, false, false) });
        if (PlaceFixed("CHO_Pipe", shared, almond, s, new Vector3(12.6f, 0f, 4.8f), -90f, 0.9f, true, true) != null) n++;
        if (PlaceFixed("CHO_Pipe", shared, almond, s, new Vector3(12.8f, 0f, 7.6f), -76f, 0.9f, true, true) != null) n++;
        if (PlaceFixed("CHO_FactoryMachine", shared, almond, s, new Vector3(12.9f, 0f, 11.6f), -90f, 1.5f, true, true) != null) n++;
        // 과수원: x ±17.6(z −7…15, 5.5 m) · ±22.5(z −3.5…7.5) · 건물 뒤 (±5, 22.5) — 흔들기 ±0.5 m, 회전 무작위
        var spots = new List<Vector3>();
        foreach (int side in new[] { -1, 1 })
        {
            foreach (float z in new[] { -7f, -1.5f, 4f, 9.5f, 15f }) spots.Add(new Vector3(side * 17.6f, 0f, z));
            foreach (float z in new[] { -3.5f, 2f, 7.5f }) spots.Add(new Vector3(side * 22.5f, 0f, z));
        }
        foreach (Vector3 p in spots)
        {
            Vector3 l = Jit(rng, p.x, p.z, 0.5f);
            GameObject tree = PlaceFixed("HOB_AlmondTree", theme, almond, s, l, (float)(rng.NextDouble() * 360.0), 1.6f);
            if (tree != null) { NoGrassTop(tree, "Soil"); n++; }
        }
        return n;
    }

    // 초콜릿 분수: 분수(0, 9) 둘레 반지름 11 m 장식 고리(앞 수리 자리 ±30° 비움, 분수를 바라봄) · 대각선 금 가로등 4 · 분수 앞 초콜릿 타일 마당 · 뒤쪽 퐁듀 묶음 2
    private static int BuildChocoDecor(Transform choco, System.Random rng)
    {
        const int s = 1;
        Transform theme = Env(choco, "ChocolateFountain_Decor"), light = Env(choco, "Lighting_Decor"), tiles = Env(choco, "GroundTiles");
        var c = new Vector3(0f, 0f, 9f);
        int n = 0;
        localUsed[s].Add((c, 9.2f));   // 분수 + 기둥 고리(7.5 m)
        var ring = new (float a, string prefab, float r)[]
        {
            (-125f, "HOB_Truffles", 1.1f), (-90f, "HOB_ChocolateBar", 1.0f), (-55f, "HOB_HeartBonbon", 0.45f), (-20f, "HOB_ChocolateBar", 1.0f),
            (20f, "HOB_ChocolateBar", 1.0f), (55f, "HOB_HeartBonbon", 0.45f), (90f, "HOB_ChocolateBar", 1.0f), (125f, "HOB_Truffles", 1.1f),
        };
        foreach (var it in ring)
        {
            Vector3 l = c + new Vector3(Mathf.Sin(it.a * Mathf.Deg2Rad), 0f, Mathf.Cos(it.a * Mathf.Deg2Rad)) * 11f;
            if (PlaceFixed(it.prefab, theme, choco, s, l, it.a + 180f + (float)(rng.NextDouble() - 0.5) * 16f, it.r, false) != null) n++;
        }
        foreach (float a in new[] { -135f, -45f, 45f, 135f })
            if (PlaceFixed("HOB_GoldLamp", light, choco, s, c + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * 12.5f, 0f, 0.35f, false) != null) n++;
        foreach (float x in new[] { -3f, 0f, 3f })
            foreach (float z in new[] { -4.5f, -1.5f })
                if (PutLocal($"HOB_Tile_Choco{(x == 0f && z == -1.5f ? "" : "Plain")}", tiles, new Vector3(x, 0.05f, z), 0f, false) != null) n++;
        localUsed[s].Add((new Vector3(0f, 0f, -3f), 4.2f));
        // 퐁듀 묶음: 냄비 1 + 꼬치 3(냄비 안쪽·옆으로 2.2 m)
        foreach (int side in new[] { -1, 1 })
        {
            var pc = new Vector3(side * 14f, 0f, 16f);
            if (PlaceFixed("HOB_FonduePot", theme, choco, s, pc, (float)(rng.NextDouble() * 360.0), 1.3f, false) != null) n++;
            Vector3 inward = (-pc).normalized;
            foreach (float da in new[] { -70f, 0f, 70f })
            {
                Vector3 d = Quaternion.Euler(0f, da, 0f) * inward;
                if (PlaceFixed("HOB_FondueSkewer", theme, choco, s, pc + d * 2.7f, (float)(rng.NextDouble() * 360.0), 0.55f, false) != null) n++;
            }
        }
        return n;
    }

    // 밀가루 공장: 건물 앞 밀가루 타일 마당 · 서쪽 밀가루 창고 군집 · 흙길 울타리(입구 12 m 안) · 밀 깃발 · 낮은 가로등
    private static int BuildFlourDecor(Transform flour, System.Random rng)
    {
        const int s = 2;
        Transform theme = Env(flour, "FlourFactory_Decor"), shared = Env(flour, "Shared_Decor"), light = Env(flour, "Lighting_Decor"), tiles = Env(flour, "GroundTiles");
        int n = 0;
        foreach (float x in new[] { -3f, 0f, 3f })
            foreach (float z in new[] { -4.5f, -1.5f })
                if (PutLocal($"HOB_Tile_Flour{(x == 0f && z == -1.5f ? "" : "Plain")}", tiles, new Vector3(x, 0.03f, z), 0f, false) != null) n++;
        localUsed[s].Add((new Vector3(0f, 0f, -3f), 4.2f));
        foreach (int side in new[] { -1, 1 })
        {
            foreach (float z in new[] { -8.2f, -10.4f })
                if (PlaceFixed("HOB_Fence_2m", shared, flour, s, new Vector3(side * 2.6f, 0f, z), 90f, 0.2f) != null) n++;
            if (PlaceFixed("HOB_WheatBanner", theme, flour, s, new Vector3(side * 3.4f, 0f, -11.2f), side > 0 ? -90f : 90f, 0.5f) != null) n++;
            if (PlaceFixed("HOB_WheatBanner", theme, flour, s, new Vector3(side * 6.4f, 0f, -2.9f), side > 0 ? -90f : 90f, 0.5f) != null) n++;
            if (PlaceFixed("HOB_LampPost_Short", light, flour, s, new Vector3(side * 3.4f, 0f, -6.6f), 0f, 0.3f) != null) n++;
            if (PlaceFixed("HOB_LampPost_Short", light, flour, s, new Vector3(side * 8.2f, 0f, -3.6f), 0f, 0.3f) != null) n++;
        }
        n += Cluster(theme, flour, s, rng, Rect.MinMaxRect(-14.5f, 2f, -11f, 12f), new[]
        {
            ("HOB_FlourSack", 4, 0.65f, false, false), ("HOB_FlourScoop", 1, 0.8f, false, false), ("HOB_WheatBundle", 3, 0.5f, false, false),
        });
        n += Cluster(shared, flour, s, rng, Rect.MinMaxRect(-14.5f, 2f, -11f, 12f), new[] { ("HOB_Barrel", 2, 0.5f, false, false), ("HOB_Crate", 2, 0.75f, true, false) });
        return n;
    }

    // 큰 섬 중앙 광장 둘레(Map/Environment): 돌길 사이 8방향(22.5° + 45° k), 광장 바깥 돌 고리(반지름 16 m) 밖 잔디 18 m에 벤치 4 · 꽃밭 4, 벤치 옆 높은 가로등 4.
    // 돌길 타일·기존 장식과 1.6 m 안이면 건너뛴다.
    private static int BuildPlazaDecor(Transform map)
    {
        Transform shared = Env(map, "Shared_Decor"), light = Env(map, "Lighting_Decor");
        Transform paths = map.Find("Paths");
        var blocked = new List<Vector3>();
        if (paths != null) foreach (Transform t in paths.GetComponentsInChildren<Transform>()) if (t != paths) blocked.Add(Flat(t.position));
        Transform field = map.Find("FieldDeco");
        if (field != null) foreach (Transform t in field) blocked.Add(Flat(t.position));
        bool Free(Vector3 p, float r) => blocked.All(b => (b - p).magnitude >= r + 1.6f);
        int n = 0;
        for (int k = 0; k < 8; k++)
        {
            float a = 22.5f + k * 45f;
            Vector3 p = Polar(18f, a);   // 13 m는 광장 둘레 돌 고리 위였다(걷는 길)
            bool bench = k % 2 == 0;
            Quaternion face = Quaternion.LookRotation(-Polar(1f, a));
            if (Free(p, bench ? 1.0f : 1.1f) && Put(bench ? "HOB_Bench" : "HOB_FlowerBed", shared, p, face) != null) { blocked.Add(p); n++; }
            if (bench)
            {
                Vector3 lp = Polar(19.4f, a + 6f);
                if (Free(lp, 0.6f) && Put("HOB_LampPost_Tall", light, lp, Quaternion.identity) != null) { blocked.Add(lp); n++; }
            }
        }
        return n;
    }

    // ---------------------------------------------------------------- §89.17 sub-map floors (no green grass)

    public const float SubGroundR = 27.6f;   // 아몬드 고원 가장자리 덤불·꽃(r 28 m~)과 둘레 울타리(29.8 m) 안쪽 — 세 고원 모두에 맞는다

    // 잔디 재질 슬롯만 바꾼다(씬 사본): 고원 윗면 → 크림 돌, 아몬드 나무 밑동 잔디 둔덕 → 흙
    private static void NoGrassTop(GameObject go, string material)
    {
        Material to = HobHQBuilder.MaterialFor(material);
        if (to == null) return;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name == "M_HQ_Grass" ? to : m).ToArray();
    }

    // 서브 맵 바닥 원판(묶음과 함께 옮겨진다): 충돌체 없음(밑의 고원 메시가 밟힌다), 부서지지 않는 "Floor" 묶음. amp > 0 이면 녹은 초콜릿 물결.
    private static void SubGround(Transform content, string theme, string material, float amp)
    {
        const int Seg = 96, Rings = 24;
        var verts = new List<Vector3> { new Vector3(0f, 0.015f + amp, 0f) };
        var tris = new List<int>();
        for (int ring = 1; ring <= Rings; ring++)
        {
            // float r = SubGroundR * ring / Rings;
            float r = (SkyR - 1f) * ring / Rings;   // §89.24 바닥 원판은 섬 전체(r 67), 장식·타일·밀밭은 건물 둘레(SubGroundR)만
            for (int k = 0; k < Seg; k++)
            {
                float a = k * Mathf.PI * 2f / Seg;
                float h = ring == Rings ? 0f : amp * (Mathf.Sin(r * 1.7f + a * 3f) * 0.6f + (Mathf.PerlinNoise(Mathf.Cos(a) * r * 0.25f + 50f, Mathf.Sin(a) * r * 0.25f + 50f) - 0.5f) * 1.6f);
                verts.Add(new Vector3(Mathf.Cos(a) * r, 0.015f + Mathf.Max(0f, amp + h), Mathf.Sin(a) * r));
            }
        }
        for (int k = 0; k < Seg; k++) tris.AddRange(new[] { 0, 1 + (k + 1) % Seg, 1 + k });
        for (int ring = 0; ring < Rings - 1; ring++)
            for (int k = 0; k < Seg; k++)
            {
                int a = 1 + ring * Seg + k, b = 1 + ring * Seg + (k + 1) % Seg, c = a + Seg, d = b + Seg;
                tris.AddRange(new[] { a, b, c, b, d, c });
            }
        var mesh = new Mesh { name = "HOB_SubGround_" + theme };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
        mesh.SetColors(Enumerable.Repeat(Color.white, verts.Count).ToList());
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = $"{GeneratedFolder}/HOB_SubGround_{theme}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        var go = new GameObject("SubGround", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(Group(content, "Floor"), false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material == "ChocoFondue" ? FondueMaterial() : HobHQBuilder.MaterialFor(material);
    }

    // 녹은 초콜릿 바닥: 밝은 초콜릿 무늬를 크게, 반들반들하게(흙처럼 보이지 않게). 너무 반들거리면 하늘이 비쳐 보라색으로 보인다(1.25).
    private static Material FondueMaterial()
    {
        string path = $"{HobHQBuilder.MaterialDir}/M_HQ_ChocoFondue.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(HobHQBuilder.MaterialFor("ChocoFlow")); AssetDatabase.CreateAsset(m, path); }
        m.CopyPropertiesFromMaterial(HobHQBuilder.MaterialFor("ChocoFlow"));
        m.SetColor("_Color", new Color(0.8f, 0.66f, 0.55f));
        m.SetFloat("_Tile", 0.3f); m.SetFloat("_SmoothScale", 1.25f); m.SetFloat("_BumpScale", 0.5f); m.SetFloat("_MacroStrength", 0.35f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 아몬드: 건물 자리 밖을 3 m 아몬드 타일로 모두 덮는다(네 모서리가 바닥 원판 안인 칸만, 4칸마다 무늬 타일). 부서지지 않는 "Tiles" 묶음.
    private static int BuildTileFloor(Transform content, string theme)
    {
        Transform tiles = Env(content, "GroundTiles");   // §89.19 예전 "Tiles"
        const float T = 3f;
        int n = 0;
        for (int i = -10; i <= 10; i++)
            for (int j = -10; j <= 10; j++)
            {
                float x = i * T, z = j * T;
                if (new Vector2(Mathf.Abs(x) + T / 2f, Mathf.Abs(z) + T / 2f).magnitude > SubGroundR) continue;
                if (x + T / 2f > BuildingRect.xMin && x - T / 2f < BuildingRect.xMax && z + T / 2f > BuildingRect.yMin && z - T / 2f < BuildingRect.yMax) continue;
                bool emblem = ((i % 2) + 2) % 2 == 0 && ((j % 2) + 2) % 2 == 0 && ((i + j) / 2 % 2 + 2) % 2 == 0;
                if (PutLocal($"HOB_Tile_{theme}{(emblem ? "" : "Plain")}", tiles, new Vector3(x, 0.03f, z), 0f, false) != null) n++;
            }
        return n;
    }

    // 초콜릿: 녹은 초콜릿 웅덩이(밟고 지나감, 부서지지 않는 "Tiles" 묶음, 충돌체 없음) — 소품을 비워 두는 앞쪽(경사로·다리가 닿는 곳)에도 깐다
    private static int ChocoPuddles(Transform content, int s, System.Random rng)
    {
        Transform floor = Env(content, "GroundTiles");   // §89.19 예전 "Tiles"
        var placed = new List<(Vector3 c, float r)>();
        int n = 0;
        for (int t = 0; t < 600 && n < 14; t++)
        {
            float rad = n % 2 == 0 ? 2.6f : 1.8f;
            float a = (float)(rng.NextDouble() * 360.0), d = 3f + (float)rng.NextDouble() * (SubGroundR - 4.5f - rad);
            var l = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * d, 0f, Mathf.Cos(a * Mathf.Deg2Rad) * d);
            if (l.x + rad > BuildingRect.xMin && l.x - rad < BuildingRect.xMax && l.z + rad > BuildingRect.yMin && l.z - rad < BuildingRect.yMax) continue;
            if (placed.Any(p => (p.c - l).magnitude < p.r + rad + 1.5f)) continue;
            if (l.x + rad > -5f && l.x - rad < 5f && l.z + rad > -6.5f && l.z - rad < 0.5f) continue;   // §89.19 분수 앞 초콜릿 타일 마당
            placed.Add((l, rad));
            if (PutLocal(n % 2 == 0 ? "HOB_ChocoPuddle" : "HOB_ChocoPuddleB", floor, l + Vector3.up * 0.02f, (float)(rng.NextDouble() * 360.0), false) != null) n++;
        }
        return n;
    }

    // 밀가루: 건물 자리·건물 앞 흙길·상자 등 표시 둘레를 빼고 섬 전체를 2 m 밀밭 덩어리로 덮는다(충돌체 없음 — 밀 사이로 걸어 다닌다).
    private static int FillWheat(Transform content, System.Random rng)
    {
        if (content == null) return 0;
        Transform field = Group(Env(content, "FlourFactory_Decor"), "WheatFields");   // §89.19 예전 content/WheatFields
        var keep = new List<Vector3>();
        foreach (string g in new[] { "Markers", "Chests", "Deco" })
        {
            Transform t = content.Find(g);
            if (t != null) foreach (Transform c in t) keep.Add(content.InverseTransformPoint(c.position));
        }
        Transform env = content.Find("Environment");                                    // §89.19 장식 군집·울타리·깃발·가로등 둘레도 비운다
        if (env != null)
            foreach (Transform g in env)
                if (g.name != "GroundTiles")
                    foreach (Transform c in g) if (c.name != "WheatFields") keep.Add(content.InverseTransformPoint(c.position));
        const float S = 2f;
        int n = 0;
        for (int i = -14; i <= 14; i++)
            for (int j = -14; j <= 14; j++)
            {
                float x = i * S, z = j * S;
                if (new Vector2(x, z).magnitude + 1.1f > SubGroundR) continue;
                if (x + 1.5f > BuildingRect.xMin && x - 1.5f < BuildingRect.xMax && z + 1.5f > BuildingRect.yMin && z - 1.5f < BuildingRect.yMax) continue;
                if (Mathf.Abs(x) < 2.2f && z < BuildingRect.yMin + 1f) continue;                  // 입구 → 건물 앞 흙길
                if (Mathf.Abs(x) < 5.5f && z > -7.5f && z < 1f) continue;                        // §89.19 건물 앞 밀가루 타일 마당
                if (keep.Any(k => new Vector2(k.x - x, k.z - z).magnitude < 2.1f)) continue;     // 상자·로켓·무지개 자리 둘레 빈터
                float yaw = rng.Next(4) * 90f;
                if (PutLocal("HOB_WheatPatch", field, new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.3f, 0f, z + (float)(rng.NextDouble() - 0.5) * 0.3f), yaw, false,
                             0.95f + (float)rng.NextDouble() * 0.12f) != null) n++;
            }
        return n;
    }

    private static void PlaceBuilding(Transform building, Transform content, Vector3 local, bool keepTilt = true)
    {
        if (building == null) return;
        // §89.14 서브 맵 안 "Buildings" 묶음 — 부서지기 규칙이 건물 등급(4번)으로 본다
        building.SetParent(Group(content, "Buildings"), true);
        building.SetPositionAndRotation(content.TransformPoint(local), content.rotation * (keepTilt ? TiltOf(building) : Quaternion.identity));
    }

    // §89.22 베이커리 건물: 프리팹 앞(문) = 로컬 +Z. 서브 맵 묶음은 −Z가 맵 가운데라 180°, 과일 밭 자리는 +Z가 가운데라 0°.
    private const float SubYaw = 180f, LotYaw = 0f;

    // §89.22 재료 건물 자리(QuadrantBuilding)에 베이커리 건물을 넣는다: 임시 정자 부품(기둥·벽·지붕 고리)은 지우고 프리팹을 자식으로,
    // 수리 자리·소켓·빛기둥은 남겨 건물 안 재료 꽂는 자리(프리팹의 SocketPoint)로 옮긴다. 뿌리의 정자 기울기(tilted)는 풀고 자식의 세계 회전은 유지.
    private static GameObject InstallBakery(Transform building, string prefab, float yaw, bool tilted)
    {
        GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{HobBakeryBuildings.PrefabDir}/{prefab}.prefab");
        if (building == null || pf == null) { Debug.LogWarning($"[HeavenOfBakeryMap] bakery building missing: {prefab}"); return null; }
        Quaternion upright = tilted ? building.rotation * Quaternion.Inverse(TiltOf(building)) : building.rotation;
        var keep = new Dictionary<Transform, Quaternion>();
        foreach (Transform c in building.Cast<Transform>().ToList())
        {
            if (c.name.StartsWith("Repair_Point") || c.name.StartsWith("Socket_") || c.name.StartsWith("ESC_Glow")) keep[c] = Quaternion.Inverse(upright) * c.rotation;
            else Object.DestroyImmediate(c.gameObject);
        }
        building.rotation = upright;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, building);
        inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Transform sp = inst.transform.Find("SocketPoint");
        Vector3 s = sp != null ? sp.position : building.position;
        foreach (KeyValuePair<Transform, Quaternion> kv in keep)
        {
            Transform c = kv.Key; c.rotation = upright * kv.Value;
            if (c.name.StartsWith("Repair_Point")) c.position = s + Vector3.up * 1.2f;
            else if (c.name.StartsWith("Socket_")) { c.position = s + Vector3.up * 0.35f; c.localScale = Vector3.one * 0.7f; }
            else c.position = s + Vector3.up * 4.5f;
        }
        return inst;
    }

    // 뿌리의 어느 축이 세계 위를 보고 있는지(테스트 맵 원래 자세) → 그 축을 위로 돌리는 회전(Y 회전과 곱해 쓴다)
    private static Quaternion UprightTilt(Transform t)
    {
        Vector3[] axes = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        Vector3 best = axes.OrderByDescending(a => Vector3.Dot(t.rotation * a, Vector3.up)).First();
        return Quaternion.FromToRotation(best, Vector3.up);
    }

    private static Quaternion TiltOf(Transform building)
    {
        int q = System.Array.IndexOf(placeholders, building);
        return q >= 0 ? placeholderTilt[q] : Quaternion.identity;
    }

    // §89.17 쓰지 않음(밀가루 섬 전체 밀밭 FillWheat로 바꿈)
    // // 밀밭: 포기(LOD 프리팹)를 0.75 m 격자 + 흔들기로 깐다.
    // private static int BuildWheatField(Transform content, int s, Vector3 center, Vector2 size, System.Random rng)
    // {
    //     Transform field = Group(content, "WheatFields");
    //     localUsed[s].Add((center, Mathf.Max(size.x, size.y) * 0.6f));
    //     int n = 0;
    //     const float step = 0.75f;
    //     for (float x = -size.x / 2f; x <= size.x / 2f; x += step)
    //         for (float z = -size.y / 2f; z <= size.y / 2f; z += step)
    //         {
    //             var l = center + new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.35f, 0f, z + (float)(rng.NextDouble() - 0.5) * 0.35f);
    //             GameObject w = PutLocal("HOB_Wheat", field, l, (float)(rng.NextDouble() * 360.0), false, 0.9f + (float)rng.NextDouble() * 0.25f);
    //             if (w != null) n++;
    //         }
    //     return n;
    // }

    // ---------------------------------------------------------------- paths

    private static int TileRun(Transform parent, string tile, Vector3 a, Vector3 b, float width)
    {
        Vector3 d = Flat(b - a);
        float len = d.magnitude;
        if (len < 0.5f) return 0;
        d /= len;
        Vector3 side = new Vector3(d.z, 0f, -d.x);
        Quaternion rot = Quaternion.LookRotation(d);
        int n = 0;
        for (float t = 1f; t < len; t += 2f)
            for (float w = -width / 2f + 1f; w < width / 2f; w += 2f)
            {
                Vector3 p = a + d * t + side * w + Vector3.up * 0.01f;
                if (InKeepOut(p, 0f) || UnderPlateau(p, -2f)) continue;
                if (Put(tile, parent, p, rot, false, new Vector3(2f, 1f, 2f)) != null) n++;
            }
        pathSegs.Add((a, b, width / 2f));
        return n;
    }

    private static int BuildPaths(Transform map)
    {
        Transform group = Group(map, "Paths");
        int n = 0;
        // 둥근 길 r 22~26
        int ring = Mathf.RoundToInt(2f * Mathf.PI * 24f / 2f);
        for (int k = 0; k < ring; k++)
            foreach (float r in new[] { 23f, 25f })
            {
                float a = (k + 0.5f) * 360f / ring;
                if (Put("HOB_Tile_Checker", group, Polar(r, a) + Vector3.up * 0.01f, Quaternion.Euler(0f, -a, 0f), false, new Vector3(2f, 1f, 2f)) != null) n++;
            }
        // 방사 길: 동·남·서 탈출구, 북쪽 다리 밑 탈출구, 두 경사로 입구
        // n += TileRun(group, "HOB_Tile_Diamond", Polar(26f, 0f), Exits[1].pos + Polar(-8f, 0f), 4f);
        // n += TileRun(group, "HOB_Tile_Diamond", Polar(26f, 270f), Exits[2].pos + Polar(8f, 270f) * -1f, 4f);
        // n += TileRun(group, "HOB_Tile_Diamond", Polar(26f, 180f), Exits[3].pos + Polar(8f, 0f), 4f);
        // n += TileRun(group, "HOB_Tile_Star", Polar(26f, 90f), Exits[0].pos + Vector3.back * 6f, 4f);
        foreach ((string name, string key, Vector3 pos) in Exits)   // §89.24 탈출구 4곳까지(가장자리 r 77)
        {
            float ea = Mathf.Atan2(pos.z, pos.x) * Mathf.Rad2Deg;
            n += TileRun(group, "HOB_Tile_Diamond", Polar(26f, ea), Polar(Flat(pos).magnitude - 7f, ea), 4f);
        }
        // for (int s = 0; s <= 2; s += 2)
        for (int s = 0; s < Slots.Length; s++)   // §89.24 나선 경사로 입구 3곳
        {
            Vector3 entry = RampEntry(s);
            Vector3 from = Polar(26f, Mathf.Atan2(entry.z, entry.x) * Mathf.Rad2Deg);
            n += TileRun(group, "HOB_Tile_Circle", from, entry, 4f);
        }
        // 과일 밭 자리 앞까지
        foreach (Transform lot in lotRoots)
        {
            Vector3 front = lot.position + lot.forward * (LotHalf + 2f);
            Vector3 from = Polar(26f, Mathf.Atan2(lot.position.z, lot.position.x) * Mathf.Rad2Deg);
            n += TileRun(group, "HOB_Tile_Red", from, front, 2f);
        }
        // 방사 길 가로등
        Transform lamps = Group(group, "Lamps");
        foreach ((Vector3 a, Vector3 b, float half) in pathSegs.ToList())
        {
            Vector3 d = Flat(b - a);
            float len = d.magnitude;
            if (half < 1.5f || len < 8f) continue;
            d /= len;
            Vector3 side = new Vector3(d.z, 0f, -d.x);
            for (float t = 6f; t < len - 2f; t += 14f)
                foreach (int sgn in new[] { -1, 1 })
                {
                    Vector3 p = a + d * t + side * (sgn * (half + 1.2f));
                    if (InKeepOut(p, 0.5f) || UnderPlateau(p, 0.5f)) continue;
                    if (Put("HOB_LampPost_Tall", lamps, p, Quaternion.identity) != null) placed.Take(p, 0.8f);
                }
        }
        return n;
    }

    // ---------------------------------------------------------------- rim, waterfalls, sky

    private static int BuildRim(Transform map)
    {
        Transform rim = Group(map, "Rim");
        // float[] exitDeg = { 0f, 90f, 270f, 180f };   // §89.13 Exit_N도 가장자리
        // §89.24 탈출구 4곳 + 나선 경사로 1줄 입구 3곳(경사로가 울타리 선을 낮게 지나가는 곳)
        float[] exitDeg = Exits.Select(e => Mathf.Atan2(e.pos.z, e.pos.x) * Mathf.Rad2Deg)
            .Concat(Enumerable.Range(0, Slots.Length).Select(sl => { Vector3 en = RampLocal(sl, RampLane, 4f); return Mathf.Atan2(en.z, en.x) * Mathf.Rad2Deg; })).ToArray();
        // float half = Exit.Opening / 2f + Exit.PostWidth;
        float half = Mathf.Max(Exit.Opening / 2f + Exit.PostWidth, RampWidth / 2f + 1f);   // §89.24 경사로 폭 9 m가 지나가게
        int n = 0;
        float r = FenceRadius + 0.4f;
        int segments = Mathf.RoundToInt(2f * Mathf.PI * r / 2.15f);
        for (int i = 0; i < segments; i++)
        {
            float a = (i + 0.5f) * 360f / segments;
            Vector3 p = Polar(r, a);
            if (exitDeg.Any(e => Mathf.Abs(Mathf.DeltaAngle(a, e)) < (half + 1.1f) / r * Mathf.Rad2Deg) || UnderPlateau(p, -2f)) continue;
            if (Put("HOB_CrownFence_2m", rim, p, Quaternion.LookRotation(Polar(1f, a)), false) != null) n++;
        }
        for (int k = 0; k < 24; k++)
        {
            float a = k * 15f + 7.5f;
            Vector3 p = Polar(FenceRadius - 1.2f, a);
            if (exitDeg.Any(e => Mathf.Abs(Mathf.DeltaAngle(a, e)) < 6f) || UnderPlateau(p, 0f)) continue;
            if (Put("HOB_BannerLamp", rim, p, FaceCenter(p)) != null) { placed.Take(p, 1f); n++; }
        }
        // foreach (float e in exitDeg)
        foreach (float e in exitDeg.Take(Exits.Length))   // §89.24 사탕 아치는 탈출구만(경사로 입구에는 두지 않음)
        {
            Vector3 p = Polar(FenceRadius - 10f, e);
            if (Put("HOB_CandyArch", rim, p, FaceCenter(p)) != null) { placed.Take(p, 3f); n++; }
        }
        RoundMapKit.FenceColliders(Group(rim, "Colliders"), FenceRadius, exitDeg, half, FenceHeight, FenceThickness);
        return n;
    }

    private static int BuildWaterfalls(Transform background)
    {
        Transform group = Group(background, "Waterfalls");
        int n = 0;
        foreach (float a in WaterfallDeg)
        {
            Vector3 p = Polar(FieldRadius - 0.5f, a) + Vector3.down * 0.2f;
            if (Put("HOB_Waterfall", group, p, Outward(p), false) != null) n++;
        }
        return n;
    }

    // §89.15 마녀가 걸어오는 구름 바닥: 떠 있는 섬들 밖(r 195)부터 900 m까지 구름 바다(높이 −1.5 m, 마녀 발은 0 m — 발목이 구름에 잠긴다).
    // 마녀 길(앵커 방향 ±45 m)은 평평하게, 나머지는 살짝 울퉁불퉁 + 큰 뭉게구름. 충돌체 없음. 길 위의 구름은 지운다(WitchBackdropBuilder.OnWitchPath).
    // public const float CloudFloorInner = 195f, CloudFloorOuter = 900f, CloudFloorY = -1.5f;
    // public const float CloudFloorInner = 260f, CloudFloorOuter = 950f, CloudFloorY = -1.5f;
    public const float CloudFloorInner = 285f, CloudFloorOuter = 980f, CloudFloorY = -1.5f;   // 하늘 섬(r 260까지) 밖   // §89.24 하늘 섬(r 236까지) 밖

    private static int BuildCloudFloor(Scene scene, Transform far, System.Random rng)
    {
        if (far == null) return 0;
        Transform anchor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "WITCH_Anchor");
        if (anchor == null || !WitchPath.TryGetGroundRect(scene, out Rect ground)) return 0;
        Vector3 center = new Vector3(ground.center.x, 0f, ground.center.y);
        Vector3 outward = WitchPath.OutwardDirection(center, anchor.position);
        float Height(Vector3 p)
        {
            Vector3 d = p - center; d.y = 0f;
            float along = Vector3.Dot(d, outward), side = (d - outward * Mathf.Max(0f, along)).magnitude;
            float flat = along > 0f ? Mathf.Clamp01((side - 45f) / 40f) : 1f;              // 길 둘레는 평평
            float bump = Mathf.PerlinNoise(p.x * 0.011f + 13f, p.z * 0.011f + 7f) - 0.5f;
            float far01 = Mathf.InverseLerp(CloudFloorInner, CloudFloorOuter, d.magnitude);
            return CloudFloorY + bump * 3.2f * flat - far01 * 6f * flat;                  // 멀수록 조금 내려가 지평선이 부드럽다
        }
        const int Seg = 160, Rings = 28;
        var verts = new List<Vector3>(); var tris = new List<int>();
        for (int ring = 0; ring <= Rings; ring++)
        {
            float r = Mathf.Lerp(CloudFloorInner, CloudFloorOuter, Mathf.Pow(ring / (float)Rings, 1.6f));
            for (int k = 0; k < Seg; k++)
            {
                float a = k * Mathf.PI * 2f / Seg;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                verts.Add(new Vector3(p.x, Height(p), p.z));
            }
        }
        for (int ring = 0; ring < Rings; ring++)
            for (int k = 0; k < Seg; k++)
            {
                int a = ring * Seg + k, b = ring * Seg + (k + 1) % Seg, c = a + Seg, d = b + Seg;
                tris.AddRange(new[] { a, b, c, b, d, c });
            }
        var mesh = new Mesh { name = "HOB_CloudFloor", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
        mesh.SetColors(Enumerable.Repeat(Color.white, verts.Count).ToList());   // HQ 셰이더: 정점 색 = 접촉 그림자 → 흰색(그림자 없음)
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = GeneratedFolder + "/HOB_CloudFloor.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        Transform old = far.Find("FarGround");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var floorGo = new GameObject("FarGround", typeof(MeshFilter), typeof(MeshRenderer));
        floorGo.transform.SetParent(far, false);
        floorGo.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = floorGo.GetComponent<MeshRenderer>();
        mr.sharedMaterial = HobHQBuilder.MaterialFor("CloudPink");
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 섬들 아래(r < 안쪽 반지름)는 한 층 낮은 구름 바다(−45 m) — 위에서 보면 구멍 대신 구름이 보이고 섬은 그 위에 떠 있다.
        // FarGround의 자식으로 둔다(FarBackdrop 바로 아래 물체는 마녀 길 위에 있으면 안 된다 — 원점은 길 위로 잡힌다).
        var low = new List<Vector3>(); var lowTris = new List<int>();
        const int LowRings = 10;
        low.Add(new Vector3(center.x, -45f, center.z));
        for (int ring = 1; ring <= LowRings; ring++)
        {
            float r = (CloudFloorInner + 25f) * ring / LowRings;
            for (int k = 0; k < Seg; k++)
            {
                float a = k * Mathf.PI * 2f / Seg;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CloudFloorInner - 40f, CloudFloorInner + 25f, r));   // 바깥 가장자리는 위 구름 바닥 아래로 올라가 이어진다
                low.Add(new Vector3(p.x, Mathf.Lerp(-45f, CloudFloorY - 6f, edge) + (Mathf.PerlinNoise(p.x * 0.02f + 3f, p.z * 0.02f + 9f) - 0.5f) * 5f, p.z));
            }
        }
        for (int k = 0; k < Seg; k++) lowTris.AddRange(new[] { 0, 1 + (k + 1) % Seg, 1 + k });
        for (int ring = 1; ring < LowRings; ring++)
            for (int k = 0; k < Seg; k++)
            {
                int a = 1 + (ring - 1) * Seg + k, b = 1 + (ring - 1) * Seg + (k + 1) % Seg, c = a + Seg, d = b + Seg;
                lowTris.AddRange(new[] { a, b, c, b, d, c });
            }
        var lowMesh = new Mesh { name = "HOB_CloudFloorLow" };
        lowMesh.SetVertices(low); lowMesh.SetTriangles(lowTris, 0);
        lowMesh.SetColors(Enumerable.Repeat(Color.white, low.Count).ToList());
        lowMesh.RecalculateNormals(); lowMesh.RecalculateBounds();
        string lowPath = GeneratedFolder + "/HOB_CloudFloorLow.asset";
        AssetDatabase.DeleteAsset(lowPath);
        AssetDatabase.CreateAsset(lowMesh, lowPath);
        var lowGo = new GameObject("LowerClouds", typeof(MeshFilter), typeof(MeshRenderer));
        lowGo.transform.SetParent(floorGo.transform, false);
        lowGo.GetComponent<MeshFilter>().sharedMesh = lowMesh;
        var lmr = lowGo.GetComponent<MeshRenderer>();
        lmr.sharedMaterial = mr.sharedMaterial;
        lmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 마녀 길 위(또는 걸친) 구름 지우기
        int removed = 0;
        foreach (Transform t in far.Cast<Transform>().ToList())
        {
            if (t.name == "FarGround") continue;
            float rad = t.GetComponentsInChildren<Renderer>().Select(r => r.bounds.extents.magnitude).DefaultIfEmpty(0f).Max();
            if (WitchBackdropBuilder.OnWitchPath(t.position, center, outward, WitchBackdropBuilder.PathHalfWidth + rad + 4f)) { Object.DestroyImmediate(t.gameObject); removed++; }
        }
        // 바닥 위 뭉게구름(가까운 곳이 촘촘, 길은 비움)
        int n = 0, tries = 0;
        while (n < 110 && tries++ < 2000)
        {
            float u = (float)rng.NextDouble();
            float r = Mathf.Lerp(CloudFloorInner + 15f, CloudFloorOuter - 60f, u * u);
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            float sc = Mathf.Lerp(4f, 11f, (float)rng.NextDouble()) * Mathf.Lerp(1f, 1.8f, (r - CloudFloorInner) / (CloudFloorOuter - CloudFloorInner));
            if (WitchBackdropBuilder.OnWitchPath(p, center, outward, WitchBackdropBuilder.PathHalfWidth + sc * 3f + 6f)) continue;
            p.y = Height(p) - sc * 0.6f;
            if (Put("HOB_Cloud_L", far, p, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), false, new Vector3(sc, sc * 0.75f, sc)) != null) n++;
        }
        return n + removed * 0;
    }

    private static int BuildClouds(Transform far, System.Random rng)
    {
        // Transform group = Group(far, "Clouds");   // 예전: 묶음 하나(원점 = 마녀 길 위로 잡혀 시험이 막았다)
        Transform group = far;                          // §89.15 구름은 FarBackdrop 바로 아래(마녀 길 위의 것은 BuildCloudFloor가 지운다)
        int n = 0;
        for (int k = 0; k < 40; k++)
        {
            float a = (float)(rng.NextDouble() * 360.0), r = 115f + (float)rng.NextDouble() * 60f;
            float y = -30f + (float)rng.NextDouble() * 42f;
            if (Slots.Any(sl => (Polar(r, a) - Flat(sl.pos)).magnitude < sl.footR + 12f)) continue;   // §89.13 떠 있는 섬과 겹치지 않게
            float sc = 1.2f + (float)rng.NextDouble() * 1.8f;
            if (Put(k % 3 == 0 ? "HOB_Cloud_S" : "HOB_Cloud_L", group, Polar(r, a) + Vector3.up * y, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), false, Vector3.one * sc) != null) n++;
        }
        // 섬 아래 구름 바다
        for (int k = 0; k < 14; k++)
        {
            float a = k * (360f / 14f), r = 40f + (k % 3) * 22f;
            if (Put("HOB_Cloud_L", group, Polar(r, a) + Vector3.up * -58f, Quaternion.Euler(0f, a, 0f), false, Vector3.one * 4f) != null) n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- field decoration

    private static bool FieldFree(Vector3 p, float r)
    {
        float d = Flat(p).magnitude;
        // if (d < 30f + r || d > 90f - r) return false;
        if (d < 30f + r || d > FieldRadius - 10f - r) return false;   // §89.24 r 85
        if (UnderPlateau(p, r) || InKeepOut(p, r + 1.5f) || OnPath(p, r + 1f)) return false;
        if (Exits.Any(e => (Flat(p) - Flat(e.pos)).magnitude < 11f + r)) return false;
        if (CookieSpawnDeg.Any(a => (Flat(p) - Polar(36f, a)).magnitude < 4f + r)) return false;
        return placed.IsFree(p, r);
    }

    private static int BuildFieldDeco(Transform map, System.Random rng)
    {
        Transform group = Group(map, "FieldDeco");
        (string prefab, int count, float r)[] list =
        {
            ("HOB_Fruit_Apple", 3, 1.3f), ("HOB_Fruit_Cherries", 3, 0.8f), ("HOB_Fruit_Grapes", 2, 1.0f), ("HOB_Fruit_Orange", 3, 1.0f),
            ("HOB_Fruit_Pumpkin", 3, 1.3f), ("HOB_Fruit_Strawberry", 3, 1.2f), ("HOB_Cupcake_Cream", 4, 1.0f), ("HOB_Cupcake_Pink", 4, 1.0f),
            ("HOB_StrawberryCupcake", 4, 0.9f), ("HOB_MarketStall", 3, 1.5f), ("HOB_FruitCart", 3, 1.5f), ("HOB_House_Cake", 1, 5f),
            ("HOB_House_Strawberry", 1, 5f), ("HOB_House_TierCake", 1, 5f), ("HOB_Tree_Fruit", 10, 1.3f), ("HOB_Tree_Round", 10, 1.2f),
            ("HOB_Tree_Pine_L", 5, 1.3f), ("HOB_Tree_Pine_S", 5, 1.3f), ("HOB_Bush_Berry", 8, 0.8f), ("HOB_Bush_Round", 8, 0.8f),
            ("HOB_FlowerBush", 8, 0.9f), ("HOB_FlowerBed", 6, 1.1f), ("HOB_RockBush", 4, 1.3f), ("HOB_RockMushroom", 4, 1.3f), ("HOB_Bench", 4, 1.0f),
        };
        int n = 0;
        foreach ((string prefab, int count, float r) in list)
            for (int k = 0; k < count; k++)
                for (int t = 0; t < 200; t++)
                {
                    // Vector3 p = Polar(30f + (float)rng.NextDouble() * 60f, (float)(rng.NextDouble() * 360.0));
                    Vector3 p = Polar(30f + (float)rng.NextDouble() * (FieldRadius - 40f), (float)(rng.NextDouble() * 360.0));   // §89.24
                    if (!FieldFree(p, r)) continue;
                    Quaternion rot = prefab.StartsWith("HOB_House_") || prefab == "HOB_MarketStall" || prefab == "HOB_Bench"
                        ? FaceCenter(p) : Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                    if (Put(prefab, group, p, rot) != null) { placed.Take(p, r); n++; }
                    break;
                }
        return n;
    }

    // ---------------------------------------------------------------- markers (chests 40, rockets 6, rainbows 8, cookie spawns 4)

    private static bool MarkerClear(Vector3 p, float radius, float floorY)
    {
        if (Physics.CheckCapsule(p + Vector3.up * (radius + 0.15f), p + Vector3.up * (radius + 2.2f), radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        return Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
               && Mathf.Abs(hit.point.y - floorY) < 0.35f;
    }

    private static Transform Marker(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        return t;
    }

    private static int BuildFieldMarkers(Transform map, System.Random rng)
    {
        Transform group = Group(map, "Markers");
        int n = 0;
        for (int q = 0; q < 4; q++) { Marker(group, $"Spawn_Cookie_Q{q + 1}", Polar(36f, CookieSpawnDeg[q]) + Vector3.up * 0.1f); n++; }
        var spots = new List<Vector3>();
        Vector3 Pick(float radius, float minGap)
        {
            for (int t = 0; t < 600; t++)
            {
                // Vector3 p = Polar(30f + (float)rng.NextDouble() * 58f, (float)(rng.NextDouble() * 360.0));
                Vector3 p = Polar(30f + (float)rng.NextDouble() * (FieldRadius - 42f), (float)(rng.NextDouble() * 360.0));   // §89.24
                if (UnderPlateau(p, 1f) || InKeepOut(p, radius + 1f) || !placed.IsFree(p, radius) || spots.Any(s => (s - p).magnitude < minGap)) continue;
                if (Exits.Any(e => (Flat(p) - Flat(e.pos)).magnitude < 9f)) continue;
                if (!MarkerClear(p, radius, 0f)) continue;
                spots.Add(p);
                return p;
            }
            return new Vector3(float.NaN, 0f, 0f);
        }
        for (int i = 0; i < 16; i++) { Vector3 p = Pick(0.6f, 9f); if (!float.IsNaN(p.x)) { Marker(group, $"CHEST_Slot_{i:00}", p); n++; } }
        for (int i = 0; i < 3; i++) { Vector3 p = Pick(1.6f, 20f); if (!float.IsNaN(p.x)) { Marker(group, $"RocketSpawn_{i:00}", p); placed.Take(p, 2f); n++; } }
        for (int i = 0; i < 2; i++) { Vector3 p = Pick(0.8f, 20f); if (!float.IsNaN(p.x)) { Marker(group, $"RainbowSpawn_{i:00}", p); n++; } }
        return n;
    }

    // 서브 맵 묶음 안 표시(묶음과 함께 옮겨진다): 상자 8 · 로켓 1 · 무지개 2.
    private static int BuildSubMapMarkers(System.Random rng)
    {
        int n = 0;
        for (int s = 0; s < 3; s++)
        {
            Transform content = contentRoots[s];
            Transform group = Group(content, "Markers");
            float floorY = content.position.y;
            Vector3 Pick(float radius)
            {
                for (int t = 0; t < 600; t++)
                {
                    Vector3 l = LocalSpot(rng, radius + 0.4f, localUsed[s], BuildingRect);
                    if (float.IsNaN(l.x)) break;
                    Vector3 w = content.TransformPoint(l);
                    if (MarkerClear(w, radius, floorY)) return w;
                    localUsed[s].RemoveAt(localUsed[s].Count - 1);
                }
                return new Vector3(float.NaN, 0f, 0f);
            }
            for (int i = 0; i < 8; i++) { Vector3 p = Pick(0.6f); if (!float.IsNaN(p.x)) { Marker(group, $"CHEST_Slot_{16 + s * 8 + i:00}", p); n++; } }
            { Vector3 p = Pick(1.6f); if (!float.IsNaN(p.x)) { Marker(group, $"RocketSpawn_{3 + s:00}", p); n++; } }
            for (int i = 0; i < 2; i++) { Vector3 p = Pick(0.8f); if (!float.IsNaN(p.x)) { Marker(group, $"RainbowSpawn_{2 + s * 2 + i:00}", p); n++; } }
        }
        return n;
    }

    // EscapeMapSetup은 상자를 EscapeSetup 아래에 만든다(같은 월드 자리) — 서브 맵 상자는 묶음 아래로 옮겨 판 배치에 따라가게 한다.
    private static int ReparentSubMapChests(Scene scene)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EscapeMapSetup.RootName);
        if (root == null) return 0;
        int n = 0;
        foreach (Transform chest in root.transform.Cast<Transform>().Where(t => t.name.StartsWith("CHEST_Slot_")).ToList())
        {
            if (!int.TryParse(chest.name.Substring("CHEST_Slot_".Length), out int index) || index < 16) continue;
            int s = Mathf.Clamp((index - 16) / 8, 0, 2);
            chest.SetParent(Group(contentRoots[s], "Chests"), true);
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- layout, navmesh, look, static

    private static void BindLayout(Scene scene)
    {
        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == "MapLayout").ToList()) Object.DestroyImmediate(old);
        var go = new GameObject("MapLayout");
        SceneManager.MoveGameObjectToScene(go, scene);
        var randomizer = go.AddComponent<MapLayoutRandomizer>();
        Transform q4 = placeholders[3];
        randomizer.EditorBind(slotRoots.ToArray(), contentRoots.ToArray(), lotRoots.ToArray(), q4, generals.ToArray());
        go.AddComponent<RuntimeNavMeshBaker>();
        EditorUtility.SetDirty(randomizer);
    }

    // 에디터에서 구운 길(테스트 맵 것)은 맞지 않는다 — 판 시작 때 RuntimeNavMeshBaker가 굽는다.
    private static void ClearBakedNavMesh(Scene scene)
    {
        foreach (GameObject g in scene.GetRootGameObjects())
            foreach (MonoBehaviour mb in g.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(mb is DecoyNavMesh) && !(mb is GiantNavMesh)) continue;
                var so = new SerializedObject(mb);
                SerializedProperty data = so.FindProperty("data");
                if (data != null) { data.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); }
                EditorUtility.SetDirty(mb);
            }
    }

    // 하늘 섬 낮 분위기: 하늘색 안개·밝은 해·3색 주변광·절차 하늘.
    private static void SetLook(Scene scene)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.78f, 0.87f, 0.98f);
        // RenderSettings.fogStartDistance = 220f;   // §89.13 맵 반경 190 — 예전 140
        // RenderSettings.fogStartDistance = 280f;
        RenderSettings.fogStartDistance = 310f;   // §89.24 맵 반경 245
        RenderSettings.fogEndDistance = 750f;     // 예전 460
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.8f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.86f, 0.8f, 0.74f);
        RenderSettings.ambientGroundColor = new Color(0.5f, 0.46f, 0.42f);
        string skyPath = GeneratedFolder + "/HOB_Sky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
        sky.SetColor("_SkyTint", new Color(0.55f, 0.72f, 1f));
        sky.SetColor("_GroundColor", new Color(0.8f, 0.86f, 0.95f));
        sky.SetFloat("_AtmosphereThickness", 0.8f);
        sky.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        foreach (Camera c in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Where(c => c.CompareTag("MainCamera")))
            c.clearFlags = CameraClearFlags.Skybox;                                    // 테스트 맵 카메라는 단색 배경이었다(하늘이 검게 보임)
        GameObject lighting = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MapLighting");
        if (lighting == null) return;
        Light sun = lighting.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;
        }
        foreach (ReflectionProbe p in lighting.GetComponentsInChildren<ReflectionProbe>(true).ToList())
            if (p.name.StartsWith("Probe_HAU")) Object.DestroyImmediate(p.gameObject);
            // else { p.transform.position = new Vector3(0f, 15f, 30f); p.size = new Vector3(2f * MapRadius + 20f, 100f, 2f * MapRadius + 20f); }
            else { p.transform.position = new Vector3(0f, 35f, 30f); p.size = new Vector3(2f * MapRadius + 20f, 150f, 2f * MapRadius + 20f); }   // 하늘 섬 48 m + 건물
    }

    private static void SetAmbience(Scene scene)
    {
        foreach (AmbientZone z in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AmbientZone>(true)))
        {
            var so = new SerializedObject(z);
            SerializedProperty boxes = so.FindProperty("boxes");
            if (boxes == null) continue;
            boxes.arraySize = 1;
            // boxes.GetArrayElementAtIndex(0).boundsValue = new Bounds(new Vector3(0f, 25f, 20f), new Vector3(2f * MapRadius + 40f, 80f, 2f * MapRadius + 40f));
            boxes.GetArrayElementAtIndex(0).boundsValue = new Bounds(new Vector3(0f, 40f, 20f), new Vector3(2f * MapRadius + 40f, 130f, 2f * MapRadius + 40f));   // 하늘 섬 48 m + 건물
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void MoveKillZone(Scene scene)
    {
        GameObject kill = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<VoidKillZone>() != null);
        if (kill == null) return;
        kill.transform.position = new Vector3(0f, -80f, 0f);
        var box = kill.GetComponent<BoxCollider>();
        if (box != null) box.size = new Vector3(700f, 4f, 700f);
    }

    // 고정된 부분만 정적 일괄 그리기(서브 맵 묶음·과일 밭 건물은 판 시작 때 옮겨지므로 넣지 않는다).
    private static void MarkStatic(Transform map)
    {
        const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;
        foreach (string g in new[] { WitchPath.GroundName, "Paths", "Rim", "FieldDeco", "Lots" })
        {
            Transform t = map.Find(g);
            if (t == null) continue;
            foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(c.gameObject, flags);
        }
        // §89.20 판마다 옮겨지는 묶음(서브 맵·과일 밭 건물)은 정적이면 안 된다 — 테스트 맵 정자 부품이 정적 일괄 그리기에 묶여
        // 충돌체만 새 자리로 가고 모습은 처음 자리에 남았다(맵이 안 바뀌는 것처럼 보이고, 보이지 않는 벽이 생김).
        foreach (string g in new[] { "SubMaps", "FieldBuildings" })
        {
            Transform t = map.Find(g);
            if (t == null) continue;
            foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(c.gameObject, 0);
        }
    }
}
