using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Test_CursedCandyCarnival에 움직이는 놀이기구 두 개를 놓는다(Request1009Plan.md §31, 단계 Q4). 여러 번 실행해도 같은 결과.
// - 롤러코스터(남서 Q3 구석): 트랙(레일 + 지지대)을 서쪽 벽 띠에 세로로, 열차 2편성(CoasterRide·CoasterTrain·CoasterTrainAudio)이 판 내내 돈다.
//   트랙 둘레는 쇠창살 울타리(나머지 두 변은 맵 벽)로 막아 달리는 열차·낮은 레일 밑으로 들어가지 못하게 한다.
//   입구(괴물 입 성)는 남쪽 띠에 앞(계단)이 동쪽을 보게 — 탈출구가 아니라 장식이라 두 계단 아래 창살 문은 늘 닫혀 있다
//   (괴물은 32° 계단을 못 올라 통로 위가 쿠키만의 피난처가 되지 않게). 계단 밑은 속이 찬 쐐기, 계단 옆은 난간 벽.
// - 회전목마(북동 Q2, 건물과 북쪽 칸막이 사이): 배율 0.65, Game_ 씬과 같은 회전(Animator Carousel_Top_Spin)·키네마틱 Rigidbody·볼록 충돌.
//   회전목마 소리 AMB_Carousel·조명 LGT_Carousel_Top을 그 자리로.
// - 걸리는 상자·장식·표시(무지개·로켓·쿠키 출발)는 가까운 빈 곳으로 옮긴다(출발 자리·술래방 둘레는 비움).
public static class CarnivalRidesBuilder
{
    public const string ScenePath = "Assets/Scenes/Maps/Test/Test_CursedCandyCarnival.unity";
    public const string RootName = "Rides";
    public const string CoasterName = "Coaster", EntranceName = "CoasterEntrance", CarouselName = "Carousel";
    public const string TrainPath = CoasterExitBuilder.Folder + "/CUR_CoasterTrain.fbx";
    private const string MetaPath = CoasterExitBuilder.Folder + "/coaster.json";
    private const string TrainMetaPath = CoasterExitBuilder.Folder + "/coaster_train.json";
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";

    // 롤러코스터: 트랙 길이(로컬 x, 48 m)가 월드 z를 따라가게 90° 돌린다(로컬 z → 월드 +x)
    public static readonly Vector3 TrackCenter = new Vector3(-62f, 0f, -42.5f);
    public const float TrackYaw = 90f;
    public const float WallInner = 69.5f;                 // 맵 벽 안쪽 면(벽 ±70, 두께 1)
    public const float FenceEastX = -56.6f, FenceNorthZ = -16f, FenceHeight = 3.6f;
    // 입구: 앞(로컬 −Z)이 월드 +X를 본다. 남쪽 면이 벽에 붙는다(벽과 사이에 쿠키만 지나는 틈이 없게)
    public static readonly Vector3 EntranceCenter = new Vector3(-38f, 0f, -61.9f);
    public const float EntranceYaw = 270f;
    // 회전목마
    public static readonly Vector3 CarouselCenter = new Vector3(17.5f, 0f, 45f);
    public const float CarouselScale = 0.65f;
    public const float CarouselRadius = 14.8f * CarouselScale; // 받침 반지름(carousel.json 지름 29.6)

    private const float BodyHeight = 14f, FacadeDepth = 2.4f, StairWallHeight = 7f, GateHeight = 3.6f;
    private const float SpawnClearance = 4f, MonsterRoomHalf = 15f, MapLimit = 66f;
    private const float PassWidth = 4f;
    private const float SearchRadius = 45f;   // 옮긴 것과 놀이기구·다른 장애물 사이를 괴물(몸 지름 3.6 m)이 지나갈 수 있게

    [Serializable] private class Meta { public EntranceMeta entrance; }
    [Serializable] private class EntranceMeta { public float floor, pass_half, pass_height, stair_half, front_foot, mouth, back_wall, back_foot, half_x; }
    [Serializable] private class TrainMeta { public CarMeta[] cars; }
    [Serializable] private class CarMeta { public string name; public float behind_head, wheel_half; }

    // 놀이기구가 차지하는 곳(옮기기·빈 자리 검사)
    private struct Zone
    {
        public Vector3 Center;
        public Vector2 Half;  // 네모(x, z 반 길이) — Radius가 0일 때
        public float Radius;  // 원

        public bool Contains(Vector3 p, float margin)
        {
            Vector3 d = p - Center;
            if (Radius > 0f) return new Vector2(d.x, d.z).magnitude < Radius + margin;
            return Mathf.Abs(d.x) < Half.x + margin && Mathf.Abs(d.z) < Half.y + margin;
        }
    }

    [MenuItem(MenuRoot + "Build Carnival Rides (Test Cursed Candy Carnival)")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[CarnivalRidesBuilder] " + Apply(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static string Apply(Scene scene)
    {
        CoasterPathBaker.Report baked = CoasterPathBaker.Bake();
        if (!baked.Ok) return "coaster path: " + baked.Message;
        var path = AssetDatabase.LoadAssetAtPath<CoasterPathSO>(CoasterPathBaker.AssetPath);
        var e = JsonUtility.FromJson<Meta>(File.ReadAllText(MetaPath)).entrance;
        var trainMeta = JsonUtility.FromJson<TrainMeta>(File.ReadAllText(TrainMetaPath));
        GameObject trackModel = CoasterExitBuilder.Import(CoasterExitBuilder.TrackPath);
        GameObject entranceModel = CoasterExitBuilder.Import(CoasterExitBuilder.EntrancePath);
        GameObject trainModel = CoasterExitBuilder.Import(TrainPath);
        if (trackModel == null || entranceModel == null || trainModel == null) return "models missing";

        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        Transform old = map.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform root = Group(map, RootName);
        var report = new List<string> { baked.Message };

        Transform coaster = BuildCoaster(root, path, trackModel, trainModel, trainMeta, report);
        BuildFence(Group(coaster, "Fence"));
        BuildEntrance(root, entranceModel, e);
        BuildCarousel(root, scene, report);
        Physics.SyncTransforms();

        var zones = Zones(e);
        report.Add(Relocate(scene, map, zones));
        return string.Join(" | ", report);
    }

    // ---------------- coaster ----------------

    private static Transform BuildCoaster(Transform root, CoasterPathSO path, GameObject trackModel, GameObject trainModel, TrainMeta meta, List<string> report)
    {
        Transform coaster = Group(root, CoasterName);
        coaster.SetPositionAndRotation(TrackCenter, Quaternion.Euler(0f, TrackYaw, 0f));
        var track = (GameObject)PrefabUtility.InstantiatePrefab(trackModel, coaster);
        track.name = "CoasterTrack";
        track.transform.localPosition = Vector3.zero;
        track.transform.localRotation = Quaternion.identity;
        foreach (MeshRenderer r in track.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool rail = r.name.Contains("Rail");
            r.sharedMaterial = CoasterExitBuilder.TrackMaterial(rail ? "CoasterRail" : "CoasterSupports", rail ? new Color(0.86f, 0.07f, 0.12f) : new Color(0.33f, 0.18f, 0.82f));
            // 레일·지지대 = 고정 충돌(볼록 아님)
            var mc = r.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
        }

        Material trainMat = TrainMaterial();
        var meshes = trainModel.GetComponentsInChildren<MeshFilter>(true).ToDictionary(m => m.name, m => m.sharedMesh);
        var trains = new CoasterTrain[2];
        for (int t = 0; t < trains.Length; t++)
        {
            var train = new GameObject(t == 0 ? "Train_A" : "Train_B").transform;
            train.SetParent(coaster, false);
            var cars = new CoasterTrain.Car[meta.cars.Length];
            Transform head = null;
            for (int c = 0; c < meta.cars.Length; c++)
            {
                CarMeta cm = meta.cars[c];
                var car = new GameObject(cm.name);
                car.transform.SetParent(train, false);
                Mesh mesh = meshes[cm.name];
                car.AddComponent<MeshFilter>().sharedMesh = mesh;
                car.AddComponent<MeshRenderer>().sharedMaterial = trainMat;
                var box = car.AddComponent<BoxCollider>();
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
                var body = car.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                cars[c] = new CoasterTrain.Car { body = body, behindHead = cm.behind_head, wheelHalf = cm.wheel_half };
                if (c == 0) head = car.transform;
            }
            trains[t] = train.gameObject.AddComponent<CoasterTrain>();
            trains[t].EditorSetup(cars);
            train.gameObject.AddComponent<CoasterTrainAudio>().EditorSetup(head);
        }
        var ride = coaster.gameObject.AddComponent<CoasterRide>();
        ride.EditorSetup(path, trains);
        ride.EditorPlaceAt(0d);
        report.Add($"coaster: 2 trains x {meta.cars.Length} cars");
        return coaster;
    }

    // 트랙 둘레(동쪽·북쪽 두 변, 서쪽·남쪽은 맵 벽까지)
    private static void BuildFence(Transform parent)
    {
        Material iron = CoasterExitBuilder.PlainMaterial("M_CUR_CoasterGate", new Color(0.12f, 0.08f, 0.14f), Color.black, 0.6f);
        FenceLine(parent, "Fence_E", new Vector3(FenceEastX, 0f, -WallInner), new Vector3(FenceEastX, 0f, FenceNorthZ), iron);
        FenceLine(parent, "Fence_N", new Vector3(-WallInner, 0f, FenceNorthZ), new Vector3(FenceEastX, 0f, FenceNorthZ), iron);
    }

    private static void FenceLine(Transform parent, string name, Vector3 a, Vector3 b, Material iron)
    {
        var fence = new GameObject(name).transform;
        fence.SetParent(parent, true);
        float len = (b - a).magnitude;
        fence.SetPositionAndRotation((a + b) / 2f, Quaternion.LookRotation(b - a));
        var box = fence.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, FenceHeight / 2f, 0f);
        box.size = new Vector3(0.35f, FenceHeight, len + 0.35f);
        int posts = Mathf.CeilToInt(len / 1.2f);
        for (int i = 0; i <= posts; i++)
            Bar(fence, $"Post_{i}", new Vector3(0f, FenceHeight / 2f, -len / 2f + i * len / posts), new Vector3(0.12f, FenceHeight, 0.12f), iron);
        Bar(fence, "Rail_Top", new Vector3(0f, FenceHeight - 0.3f, 0f), new Vector3(0.1f, 0.12f, len), iron);
        Bar(fence, "Rail_Low", new Vector3(0f, 0.5f, 0f), new Vector3(0.1f, 0.12f, len), iron);
    }

    // ---------------- entrance (decoration) ----------------

    // 입구 로컬(앞 = −Z, Blender 재기 coaster.json)에서 충돌을 만든다
    private static void BuildEntrance(Transform root, GameObject model, EntranceMeta e)
    {
        var entrance = new GameObject(EntranceName).transform;
        entrance.SetParent(root, false);
        entrance.SetPositionAndRotation(EntranceCenter, Quaternion.Euler(0f, EntranceYaw, 0f));
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, entrance);
        visual.name = "Model";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        foreach (MeshRenderer r in visual.GetComponentsInChildren<MeshRenderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select((m, i) => i == 0 ? CoasterExitBuilder.EntranceMaterial() : CoasterExitBuilder.TunnelMaterial()).ToArray();

        Transform cols = Group(entrance, "Colliders");
        float facade = -FacadeDepth, back = e.back_wall;
        // 건물 덩어리(통로 포함 — 두 문이 늘 닫혀 통로에 들어갈 수 없다)
        Box(cols, "COL_Body", new Vector3(0f, BodyHeight / 2f, (facade + back) / 2f), new Vector3(e.half_x * 2f, BodyHeight, back - facade));
        Ramp(cols, "COL_StairsFront", new Vector3(0f, 0f, e.front_foot), 0f, e.stair_half * 2f, e.floor, e.mouth - e.front_foot);
        Ramp(cols, "COL_StairsBack", new Vector3(0f, 0f, e.back_foot), 180f, e.stair_half * 2f, e.floor, e.back_foot - e.back_wall);
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (e.stair_half + 0.15f);
            Box(cols, side < 0 ? "COL_StairRailFrontL" : "COL_StairRailFrontR", new Vector3(x, StairWallHeight / 2f, (e.front_foot + facade) / 2f), new Vector3(0.3f, StairWallHeight, facade - e.front_foot));
            Box(cols, side < 0 ? "COL_StairRailBackL" : "COL_StairRailBackR", new Vector3(x, StairWallHeight / 2f, (back + e.back_foot) / 2f), new Vector3(0.3f, StairWallHeight, e.back_foot - back));
        }
        Material iron = CoasterExitBuilder.PlainMaterial("M_CUR_CoasterGate", new Color(0.12f, 0.08f, 0.14f), Color.black, 0.6f);
        Gate(entrance, "Gate_Front", e.front_foot - 0.4f, e.stair_half * 2f + 0.6f, iron);
        Gate(entrance, "Gate_Back", e.back_foot + 0.4f, e.stair_half * 2f + 0.6f, iron);
    }

    // 늘 닫힌 창살 문(쿠키 점프 1.8 m로 넘지 못하는 높이)
    private static void Gate(Transform entrance, string name, float z, float width, Material iron)
    {
        var gate = new GameObject(name).transform;
        gate.SetParent(entrance, false);
        gate.localPosition = new Vector3(0f, 0f, z);
        var col = gate.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, GateHeight / 2f, 0f);
        col.size = new Vector3(width, GateHeight, 0.4f);
        const int Bars = 13;
        for (int i = 0; i < Bars; i++)
            Bar(gate, $"Bar_{i}", new Vector3(-width / 2f + 0.2f + i * (width - 0.4f) / (Bars - 1), GateHeight / 2f, 0f), new Vector3(0.14f, GateHeight, 0.14f), iron);
        Bar(gate, "Rail_Top", new Vector3(0f, GateHeight - 0.25f, 0f), new Vector3(width, 0.16f, 0.18f), iron);
        Bar(gate, "Rail_Mid", new Vector3(0f, GateHeight * 0.45f, 0f), new Vector3(width, 0.16f, 0.18f), iron);
    }

    // ---------------- carousel ----------------

    private static void BuildCarousel(Transform root, Scene scene, List<string> report)
    {
        var carousel = Group(root, CarouselName);
        carousel.position = CarouselCenter;
        carousel.localScale = Vector3.one * CarouselScale;
        var top = new GameObject(CarouselModelSwap.TopName).transform;
        top.SetParent(carousel, false);
        top.gameObject.AddComponent<MeshCollider>(); // Apply가 충돌 메시를 넣는다
        report.Add(CarouselModelSwap.Apply(top));

        string guid = AssetDatabase.FindAssets("Carousel_Top_Spin t:AnimatorController").FirstOrDefault();
        var controller = guid != null ? AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        var animator = top.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Fixed; // 움직이는 충돌체 — 물리 스텝에 맞춰 돈다
        var body = top.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        if (controller == null) report.Add("carousel controller missing");

        foreach (GameObject g in scene.GetRootGameObjects())
            foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
                if (t.name == "AMB_Carousel" || t.name == "LGT_Carousel_Top")
                    t.position = new Vector3(CarouselCenter.x, t.position.y, CarouselCenter.z);
    }

    // ---------------- relocation ----------------

    private static List<Zone> Zones(EntranceMeta e)
    {
        float cx = (FenceEastX - WallInner) / 2f, cz = (FenceNorthZ - WallInner) / 2f;
        var coaster = new Zone { Center = new Vector3(cx, 0f, cz), Half = new Vector2((FenceEastX + WallInner) / 2f, (FenceNorthZ + WallInner) / 2f) };
        // 입구: 로컬 z(front_foot ~ back_foot)가 월드 −x로, 로컬 x(±half_x)가 월드 z로 — 앞 문 앞 3 m 비움
        float front = EntranceCenter.x - e.front_foot + 3f, back = EntranceCenter.x - e.back_foot - 0.8f;
        var entrance = new Zone { Center = new Vector3((front + back) / 2f, 0f, EntranceCenter.z), Half = new Vector2((front - back) / 2f, e.half_x + 0.3f) };
        var carousel = new Zone { Center = CarouselCenter, Radius = CarouselRadius + 0.5f };
        return new List<Zone> { coaster, entrance, carousel };
    }

    private static string Relocate(Scene scene, Transform map, List<Zone> zones)
    {
        var spawns = scene.GetRootGameObjects().Where(g => g.name == "MonsterSpawnPos" || g.name == "PlayerSpawnPos").Select(g => g.transform.position).ToList();
        var items = new List<(Transform t, float clearance)>();
        GameObject escape = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EscapeMapSetup.RootName);
        if (escape != null) items.AddRange(escape.GetComponentsInChildren<MaterialChest>(true).Select(c => (c.transform, 1.6f)));
        Transform deco = map.Find("Decoration"), markers = map.Find("Markers");
        if (deco != null) items.AddRange(deco.Cast<Transform>().Select(t => (t, 2.5f)));
        if (markers != null) items.AddRange(markers.Cast<Transform>().Select(t => (t, 2f)));

        // 사분면 건물(속이 빈 방)도 빈 자리에서 뺀다
        var avoid = new List<Zone>(zones);
        foreach (QuadrantBuilding b in map.GetComponentsInChildren<QuadrantBuilding>(true))
        {
            Bounds bb = b.GetComponentsInChildren<Collider>(true).Select(c => c.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
            avoid.Add(new Zone { Center = new Vector3(bb.center.x, 0f, bb.center.z), Half = new Vector2(bb.extents.x, bb.extents.z) });
        }
        var moved = new List<string>();
        foreach ((Transform t, float clearance) in items)
        {
            Vector3 from = t.position;
            if (!zones.Any(z => z.Contains(from, clearance + PassWidth))) continue;
            var own = t.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToList();
            foreach (Collider c in own) c.enabled = false;
            bool found = FreeSpot(from, clearance, avoid, spawns, out Vector3 spot);
            foreach (Collider c in own) c.enabled = true;
            if (!found) { moved.Add(t.name + " (no free spot)"); continue; }
            t.position = new Vector3(spot.x, from.y, spot.z);
            avoid.Add(new Zone { Center = spot, Radius = clearance }); // 옮긴 것끼리(충돌체 없는 표시 포함) 겹치지 않게
            Physics.SyncTransforms();
            moved.Add($"{t.name} ({from.x:F0},{from.z:F0})->({spot.x:F0},{spot.z:F0})");
        }
        return moved.Count > 0 ? "moved: " + string.Join(", ", moved) : "nothing moved";
    }

    // origin에서 가까운 순으로 맵 안·놀이기구 밖(PassWidth 더)·출발 자리와 술래방에서 떨어진, clearance + PassWidth/2 반지름 안에 충돌체(땅 제외)가 없는 곳
    private static bool FreeSpot(Vector3 origin, float clearance, List<Zone> zones, List<Vector3> spawns, out Vector3 spot)
    {
        var cands = new List<Vector3>();
        for (float x = -SearchRadius; x <= SearchRadius; x += 1f)
            for (float z = -SearchRadius; z <= SearchRadius; z += 1f)
            {
                var p = new Vector3(Mathf.Round(origin.x) + x, 0f, Mathf.Round(origin.z) + z);
                if (Mathf.Abs(p.x) > MapLimit || Mathf.Abs(p.z) > MapLimit) continue;
                if (p.x * origin.x < 9f || p.z * origin.z < 9f) continue; // 같은 사분면에(축 칸막이 길 3 m 밖)
                if (Mathf.Abs(p.x) < MonsterRoomHalf && Mathf.Abs(p.z) < MonsterRoomHalf) continue;
                if (zones.Any(zn => zn.Contains(p, clearance + PassWidth))) continue;
                if (spawns.Any(s => new Vector2(s.x - p.x, s.z - p.z).magnitude < SpawnClearance + clearance)) continue;
                cands.Add(p);
            }
        foreach (Vector3 p in cands.OrderBy(c => (c - new Vector3(origin.x, 0f, origin.z)).sqrMagnitude))
        {
            float r = clearance + PassWidth / 2f;
            bool blocked = Physics.OverlapCapsule(p + Vector3.up * (r + 0.2f), p + Vector3.up * Mathf.Max(3f, r + 0.3f), r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Any(c => c.transform.parent == null || c.transform.parent.name != "Ground");
            if (!blocked) { spot = p; return true; }
        }
        spot = origin;
        return false;
    }

    // ---------------- passability ----------------

    public const string PassabilityReport = "Temp/TestCarnival_Passability.txt";
    private const float MonsterRoomWall = 12.6f;

    // 몸 5가지 통행 검사(MapPassabilityCheck). 울타리 안 트랙 자리는 놀이 공간이 아니라 주머니로 보지 않는다.
    public static MapPassabilityCheck.Result[] CheckPassability(Scene scene)
    {
        var options = new MapPassabilityCheck.Options
        {
            Half = WallInner + 2f,
            Bodies = MapPassabilityCheck.DesignRange,
            CookieForbidden = p => Mathf.Abs(p.x) < MonsterRoomWall && Mathf.Abs(p.z) < MonsterRoomWall,
            IgnorePocket = p => Mathf.Abs(p.x) > WallInner || Mathf.Abs(p.z) > WallInner || (p.x < FenceEastX && p.z < FenceNorthZ),
            Targets = Targets(scene).ToArray(),
            MonsterSeeds = Enumerable.Range(0, 4).Select(i => new Vector3(i % 2 == 0 ? -1f : 1f, 0f, i < 2 ? 1f : -1f) * 12.5f).ToArray(),
            MapImage = "Temp/TestCarnival_PassabilityMap.png",
        };
        MapPassabilityCheck.Result[] results = MapPassabilityCheck.Run(scene, options);
        var sb = new System.Text.StringBuilder();
        foreach (MapPassabilityCheck.Result r in results) sb.Append(r.Clean ? "CLEAN " : "ISSUES ").Append(r).Append('\n');
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PassabilityReport, sb.ToString());
        return results;
    }

    private static IEnumerable<(string, Vector3)> Targets(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("CHEST_Slot_") || n.StartsWith("RocketSpawn_") || n.StartsWith("RainbowSpawn_") || n.StartsWith("Spawn_Cookie_")
                    || n.StartsWith("Repair_Point") || n.StartsWith("ExitTrigger_"))
                    yield return (n + (n.StartsWith("Repair_Point") && t.parent != null ? "@" + t.parent.name : ""), t.position);
            }
    }

    // ---------------- helpers ----------------

    private static Material TrainMaterial()
    {
        Material m = CoasterExitBuilder.LoadOrCreate("M_CUR_CoasterTrain");
        m.mainTexture = CoasterExitBuilder.Texture("CoasterTrain_albedo", false);
        m.SetTexture("_BumpMap", CoasterExitBuilder.Texture("CoasterTrain_normal", true));
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", CoasterExitBuilder.Texture("CoasterTrain_metal", false));
        m.EnableKeyword("_METALLICGLOSSMAP");
        m.SetFloat("_GlossMapScale", 0.8f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void Box(Transform parent, string name, Vector3 localCenter, Vector3 size)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localCenter;
        t.gameObject.AddComponent<BoxCollider>().size = size;
        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
    }

    // 단위 쐐기(CoasterExitBuilder.RampMesh: 폭 x −0.5~0.5, 길이 z 0~1에서 높이 0 → 1)
    private static void Ramp(Transform parent, string name, Vector3 localFoot, float yaw, float width, float height, float run)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localFoot;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        t.localScale = new Vector3(width, height, run);
        var mc = t.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = CoasterExitBuilder.RampMesh();
        mc.convex = true;
    }

    private static void Bar(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Transform Group(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }
}
