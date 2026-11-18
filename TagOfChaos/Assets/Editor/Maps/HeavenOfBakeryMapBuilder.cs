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
    public const float FieldRadius = 100f, FenceRadius = 96f;
    public const float PlazaScale = 1.67f, PlazaTop = 0.82f;
    public const float MapRadius = 130f;                    // 고원이 섬 밖으로 나간다(r ≈ 124)
    private const float FenceHeight = 6f, FenceThickness = 0.8f;

    // ---- 술래방(정자, 문 N·E·S·W)
    public const float RoomInner = 5.8f, RoomOuter = 6.6f, RoomWallHeight = 4.5f, RoomDoorWidth = 3.0f;
    public const float GuardInnerHalf = 3.8f;                // 둥근 방 안쪽 정사각형 반 폭(5.8 · 0.707 ≈ 4.1 − 여유)
    public const float RoomHalfForAi = 6.6f;                 // EscapeManager.monsterRoomHalfSize(AI 길 굽기 못 걷는 상자)
    public static readonly float[] AxisDeg = { 90f, 0f, 270f, 180f };   // N, E, S, W

    // ---- 서브 맵 슬롯(앞 −Z = 맵 가운데). 고원 모델 · 윗면 높이 · 평평한 윗면 반지름 · 바닥 반지름
    public static readonly (string name, Vector3 pos, string plateau, float top, float topR, float footR)[] Slots =
    {
        ("SubSlot_NW", new Vector3(-66f, 0f, 64f), "HOB_Plateau_Almond", 12f, 30.5f, 33.5f),
        ("SubSlot_N", new Vector3(0f, 0f, 92f), "HOB_Plateau_Choco", 15f, 32.5f, 35.5f),
        ("SubSlot_NE", new Vector3(66f, 0f, 64f), "HOB_Plateau_Almond", 12f, 30.5f, 33.5f),
    };
    public static readonly string[] SubMapNames = { "SubMap_Almond", "SubMap_Chocolate", "SubMap_Flour" };

    // ---- 경사로·다리·계단
    private static readonly Vector3 RampDirNW = new Vector3(0.087f, 0f, -0.996f).normalized;   // 고원 → 경사로 아래(남쪽)
    private static readonly Vector3 RampDirNE = new Vector3(-0.087f, 0f, -0.996f).normalized;
    private const float RampLength = 32f, RampTopInset = 0.5f;
    public const float BridgeY = 12f, BridgeZ = 48.5f, PadHalf = 3.5f;   // 가운데 발판(7 × 7 m)
    private const int SpanSegments = 4;

    // ---- 탈출구
    public static readonly (string name, string key, Vector3 pos)[] Exits =
    {
        ("Exit_N", "N", new Vector3(0f, 0f, 46f)), ("Exit_E", "E", new Vector3(93f, 0f, 0f)),
        ("Exit_S", "S", new Vector3(0f, 0f, -93f)), ("Exit_W", "W", new Vector3(-93f, 0f, 0f)),
    };
    private static readonly RoundMapKit.ExitSize Exit = new RoundMapKit.ExitSize { Opening = 4f, PostWidth = 1.5f, Height = 5f, PadDepth = 6f };
    private static readonly Color ExitColor = new Color(1f, 0.82f, 0.4f);
    private static readonly Color RoomColor = new Color(1f, 0.45f, 0.7f);

    // ---- 과일 밭 자리(재료 건물 1 + 일반 건물 3이 판마다 나뉘어 들어간다)
    public static readonly float[] LotDeg = { 200f, 250f, 290f, 340f };
    public const float LotRadius = 55f, LotHalf = 8f;
    public static readonly float[] WaterfallDeg = { 205f, 235f, 305f, 335f };
    public static readonly float[] CookieSpawnDeg = { 215f, 245f, 295f, 325f };

    private static readonly RoundMapKit.Placement placed = new RoundMapKit.Placement();
    private static readonly List<(Vector3 a, Vector3 b, float half)> pathSegs = new List<(Vector3, Vector3, float)>();
    private static readonly List<(Vector3 c, Quaternion r, Vector2 half)> keepOut = new List<(Vector3, Quaternion, Vector2)>();
    private static readonly Transform[] slotRoots = new Transform[3];
    private static readonly Transform[] contentRoots = new Transform[3];
    private static readonly Transform[] lotRoots = new Transform[4];
    private static readonly Transform[] generals = new Transform[3];
    private static readonly Transform[] placeholders = new Transform[4];
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
        BuildBridge(ground);
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
        RoundMapKit.MoveWitchAnchor(scene, MapRadius, 70f);
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
        Put("HOB_Island_Main", ground, Vector3.zero, Quaternion.identity);
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

    private static Vector3 RampDir(int slot) => slot == 0 ? RampDirNW : RampDirNE;
    private static Vector3 RampTop(int slot) => Flat(Slots[slot].pos) + RampDir(slot) * (Slots[slot].topR - RampTopInset);
    private static Vector3 RampBase(int slot) => RampTop(slot) + RampDir(slot) * RampLength;

    // 고원 가장자리에서 길이 이어지는 곳(울타리를 비운다): (방향, 반 폭 m)
    private static IEnumerable<(Vector3 dir, float half)> SlotGaps(int s)
    {
        Vector3 c = Flat(Slots[s].pos);
        if (s == 1) { yield return (Vector3.back, 3f); yield break; }   // N: 계단
        yield return (RampDir(s), 3f);
        yield return ((BridgeEnd(s) - c).normalized, 2.6f);
    }

    private static void BuildSlots(Transform ground)
    {
        Transform group = Group(ground, "Slots");
        for (int s = 0; s < Slots.Length; s++)
        {
            var slot = Slots[s];
            Vector3 c = Flat(slot.pos);
            GameObject plateau = Put(slot.plateau, group, c, Outward(c));
            if (plateau != null) plateau.name = $"Plateau_{slot.name.Substring(8)}";
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
        for (int s = 0; s <= 2; s += 2)
        {
            Vector3 b = RampBase(s), u = RampDir(s);
            GameObject ramp = Put("HOB_SpiralRamp_12m", group, b, Quaternion.LookRotation(u));
            if (ramp != null) ramp.name = s == 0 ? "Ramp_W" : "Ramp_E";
            keepOut.Add((b - u * (RampLength / 2f), Quaternion.LookRotation(u), new Vector2(8.5f, RampLength / 2f + 1f)));
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
        Put("HOB_ChocoPillar", group, new Vector3(0f, 0f, BridgeZ + PadHalf - 0.8f), Quaternion.identity, true, new Vector3(1.4f, (BridgeY - 1f) / 3.5f, 1.4f));

        for (int s = 0; s <= 2; s += 2)
        {
            Vector3 a = new Vector3(s == 0 ? -PadHalf + 0.3f : PadHalf - 0.3f, BridgeY, BridgeZ);
            Vector3 b = BridgeEnd(s) + Vector3.up * BridgeY;
            Vector3 dir = (b - a).normalized;
            float seg = (b - a).magnitude / SpanSegments;
            for (int k = 0; k < SpanSegments; k++)
            {
                Vector3 mid = a + dir * (seg * (k + 0.5f));
                GameObject span = Put("HOB_Bridge_10m", group, mid, Quaternion.LookRotation(dir), true, new Vector3(1f, 1f, seg / 10f + 0.03f));
                if (span != null) span.name = $"Span_{(s == 0 ? "W" : "E")}{k}";
                if (k == 1 || k == 2)   // 받침 기둥(Exit_N 앞은 비운다)
                    Put("HOB_ChocoPillar", group, Flat(mid), Quaternion.identity, true, new Vector3(1.3f, (BridgeY - 2.9f) / 3.5f, 1.3f));
            }
        }
        // 발판 → N 고원(3 m 오름): 계단은 로컬 −Z로 오른다 → 북쪽(+Z)을 보게 180°
        GameObject stairs = Put("HOB_Stairs_3m", group, new Vector3(0f, BridgeY, BridgeZ + PadHalf), Quaternion.Euler(0f, 180f, 0f));
        if (stairs != null) stairs.name = "Stairs_N";
        // 계단 위 보이지 않는 경사판(발판 12 m → 고원 15 m): 계단 턱마다 걸리지 않고 AI 길·통행 검사가 이어지게
        var slope = new GameObject("COL_StairsSlope").transform;
        slope.SetParent(group, false);
        Vector3 low = new Vector3(0f, BridgeY, BridgeZ + PadHalf - 1f), high = new Vector3(0f, BridgeY + 3.2f, BridgeZ + PadHalf + 5.5f);   // 계단 턱 모서리보다 살짝 위
        Vector3 along = (high - low).normalized;
        Quaternion tilt = Quaternion.LookRotation(along, Vector3.Cross(along, Vector3.right));
        slope.SetPositionAndRotation((low + high) / 2f - tilt * Vector3.up * 0.1f, tilt);
        slope.gameObject.AddComponent<BoxCollider>().size = new Vector3(4.6f, 0.2f, (high - low).magnitude + 0.4f);
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
            q4.SetPositionAndRotation(lotRoots[0].position, lotRoots[0].rotation);
        }
        for (int g = 0; g < 3; g++)
        {
            generals[g] = Empty(buildings, $"General_{g + 1}", lotRoots[g + 1].position, lotRoots[g + 1].rotation);
            PutLocal("HOB_Signpost", generals[g], new Vector3(0f, 0f, 0f), 0f);
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

    private static readonly Rect BuildingRect = Rect.MinMaxRect(-8.5f, 0.5f, 8.5f, 17.5f);   // 재료 건물 자리(로컬 x, z)
    private static readonly List<(Vector3 c, float r)>[] localUsed = { new List<(Vector3, float)>(), new List<(Vector3, float)>(), new List<(Vector3, float)>() };

    private static int Scatter(Transform content, int s, System.Random rng, (string prefab, int count, float r)[] list)
    {
        int n = 0;
        foreach ((string prefab, int count, float r) in list)
            for (int k = 0; k < count; k++)
            {
                Vector3 l = LocalSpot(rng, r, localUsed[s], BuildingRect);
                if (float.IsNaN(l.x)) continue;
                if (PutLocal(prefab, Group(content, "Deco"), l, (float)(rng.NextDouble() * 360.0)) != null) n++;
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
        n += Scatter(almond, 0, rng, new[]
        {
            ("HOB_AlmondTree", 8, 1.8f), ("HOB_Crate", 3, 0.9f), ("HOB_Sack", 4, 0.9f), ("HOB_Barrel", 3, 0.6f), ("HOB_Urn", 2, 0.6f),
            ("HOB_Bench", 2, 1.0f), ("HOB_LampPost_Short", 6, 0.4f), ("HOB_Bush_Round", 4, 0.8f),
        });

        // 초콜릿: 재료 건물 = 초콜릿 분수(D6, 수리 자리는 분수 앞) + 초콜릿 기둥 고리·과자
        Transform choco = contentRoots[1];
        Transform q2 = placeholders[1];
        if (q2 != null)
        {
            foreach (Transform c in q2.Cast<Transform>().ToList())
                if (!(c.name.StartsWith("Repair_Point") || c.name.StartsWith("Socket_") || c.name.StartsWith("ESC_Glow"))) Object.DestroyImmediate(c.gameObject);
            PlaceBuilding(q2, choco, new Vector3(0f, 0f, 9f));
            GameObject fountain = Put("HOB_ChocoFountain", q2, q2.position, q2.rotation);
            if (fountain != null) fountain.name = "Model";
            foreach (Transform c in q2.Cast<Transform>())
            {
                if (c.name.StartsWith("Repair_Point")) c.localPosition = new Vector3(0f, 1.2f, -6.8f);
                else if (c.name.StartsWith("Socket_")) c.localPosition = new Vector3(0f, 0.5f, -5.9f);
                else if (c.name.StartsWith("ESC_Glow")) c.localPosition = new Vector3(0f, 7.2f, 0f);
            }
            for (int k = 0; k < 6; k++)
            {
                float a = 30f + k * 60f;   // 앞(−Z, 수리 자리)은 비운다
                PutLocal("HOB_ChocoPillar", choco, new Vector3(0f, 0f, 9f) + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * 7.5f, 0f);
            }
        }
        n += Scatter(choco, 1, rng, new[]
        {
            ("HOB_ChocolateBar", 4, 1.1f), ("HOB_Truffles", 3, 1.2f), ("HOB_StrawberryChocoCake", 3, 1.2f), ("HOB_CandyCanes", 3, 1.0f),
            ("HOB_CandyCaneLamp", 4, 0.5f), ("HOB_Cupcake_Cream", 2, 0.9f),
        });

        // 밀가루: 임시 재료 건물 + 밀밭 2 · 작은 절벽 폭포 · 자루 무리 · 표지판
        Transform flour = contentRoots[2];
        PlaceBuilding(placeholders[2], flour, new Vector3(0f, 0f, 9f));
        n += BuildWheatField(flour, 2, new Vector3(-15f, 0f, 6f), new Vector2(8f, 7f), rng);
        n += BuildWheatField(flour, 2, new Vector3(15f, 0f, 6f), new Vector2(8f, 7f), rng);
        GameObject cliff = PutLocal("HOB_WaterfallCliff", Group(flour, "Deco"), new Vector3(0f, 0f, 22f), 180f);
        localUsed[2].Add((new Vector3(0f, 0f, 22f), 7.5f));
        if (cliff != null)
        {
            Bounds b = RoundMapKit.LocalBounds(cliff.transform);
            Vector3 lip = cliff.transform.TransformPoint(new Vector3(0f, b.max.y - 0.3f, b.min.z + 0.6f));
            GameObject fall = Put("HOB_Waterfall_Small", Group(flour, "Deco"), lip, flour.rotation * Quaternion.Euler(0f, 180f, 0f), false);
            if (fall != null) fall.name = "SmallWaterfall";
        }
        n += Scatter(flour, 2, rng, new[] { ("HOB_Sack", 6, 0.9f), ("HOB_Signpost", 1, 1.0f), ("HOB_WoodFootbridge", 1, 3.2f), ("HOB_Bench", 2, 1.0f), ("HOB_FlowerBed", 3, 1.1f) });
        return n;
    }

    private static void PlaceBuilding(Transform building, Transform content, Vector3 local)
    {
        if (building == null) return;
        building.SetParent(content, true);
        building.SetPositionAndRotation(content.TransformPoint(local), content.rotation);
    }

    // 밀밭: 포기(LOD 프리팹)를 0.75 m 격자 + 흔들기로 깐다.
    private static int BuildWheatField(Transform content, int s, Vector3 center, Vector2 size, System.Random rng)
    {
        Transform field = Group(content, "WheatFields");
        localUsed[s].Add((center, Mathf.Max(size.x, size.y) * 0.6f));
        int n = 0;
        const float step = 0.75f;
        for (float x = -size.x / 2f; x <= size.x / 2f; x += step)
            for (float z = -size.y / 2f; z <= size.y / 2f; z += step)
            {
                var l = center + new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.35f, 0f, z + (float)(rng.NextDouble() - 0.5) * 0.35f);
                GameObject w = PutLocal("HOB_Wheat", field, l, (float)(rng.NextDouble() * 360.0), false, 0.9f + (float)rng.NextDouble() * 0.25f);
                if (w != null) n++;
            }
        return n;
    }

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
        n += TileRun(group, "HOB_Tile_Diamond", Polar(26f, 0f), Exits[1].pos + Polar(-8f, 0f), 4f);
        n += TileRun(group, "HOB_Tile_Diamond", Polar(26f, 270f), Exits[2].pos + Polar(8f, 270f) * -1f, 4f);
        n += TileRun(group, "HOB_Tile_Diamond", Polar(26f, 180f), Exits[3].pos + Polar(8f, 0f), 4f);
        n += TileRun(group, "HOB_Tile_Star", Polar(26f, 90f), Exits[0].pos + Vector3.back * 6f, 4f);
        for (int s = 0; s <= 2; s += 2)
        {
            Vector3 entry = RampBase(s) + RampDir(s) * 3f;
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
        float[] exitDeg = { 0f, 270f, 180f };
        float half = Exit.Opening / 2f + Exit.PostWidth;
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
        foreach (float e in exitDeg)
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

    private static int BuildClouds(Transform far, System.Random rng)
    {
        Transform group = Group(far, "Clouds");
        int n = 0;
        for (int k = 0; k < 40; k++)
        {
            float a = (float)(rng.NextDouble() * 360.0), r = 115f + (float)rng.NextDouble() * 60f;
            float y = -30f + (float)rng.NextDouble() * 42f;
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
        if (d < 30f + r || d > 90f - r) return false;
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
                    Vector3 p = Polar(30f + (float)rng.NextDouble() * 60f, (float)(rng.NextDouble() * 360.0));
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
                Vector3 p = Polar(30f + (float)rng.NextDouble() * 58f, (float)(rng.NextDouble() * 360.0));
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
        RenderSettings.fogStartDistance = 140f;
        RenderSettings.fogEndDistance = 460f;
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
            else { p.transform.position = new Vector3(0f, 15f, 0f); p.size = new Vector3(260f, 80f, 260f); }
    }

    private static void SetAmbience(Scene scene)
    {
        foreach (AmbientZone z in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AmbientZone>(true)))
        {
            var so = new SerializedObject(z);
            SerializedProperty boxes = so.FindProperty("boxes");
            if (boxes == null) continue;
            boxes.arraySize = 1;
            boxes.GetArrayElementAtIndex(0).boundsValue = new Bounds(new Vector3(0f, 25f, 20f), new Vector3(2f * MapRadius + 40f, 80f, 2f * MapRadius + 40f));
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
    }
}
