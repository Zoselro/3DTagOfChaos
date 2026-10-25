using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

// 초콜릿 공장 건물 8종 프리팹(Request1009Plan.md §41, 단계 U1). Blender(chb_build.py)가 내보낸 CHO_Bld_<이름>.fbx + .json으로:
// - 겉(Exterior, 땅 아래 받침판은 잘라 냄)·방(Interior)은 보이기만, 충돌은 COL_Interior(방 벽·소품 상자) + 건물 자리 채움 상자(BuildingFill)
// - 출입구마다 두 장 여닫이 문(Door_<앞/뒤>_L·R → Leaf_L·Leaf_R, InteractableDoor + 공용 MapDoor 컨트롤러, E키) — 옛 초콜릿 공장 문과 같다
// - 움직이는 부분: Spin_*(SpinPart 회전), Flow_*(TextureScroll 흐름), Item_* + Path_*(ConveyorItems), FX_*(ChocolateFactoryFx 파티클)
// - 공장 소리: AMB_* 자리에 AmbientEmitter(SoundId.AmbFactory — 옛 Game_ChocolateFactory 기계실 소리 그대로)
// - 재료 건물: Socket_<재료>(QuadrantBuilding이 색을 칠함)·Repair_Point(쿠키 손 높이)·ESC_Glow(가동 빛기둥)·방 빛
// 여러 번 실행해도 같은 결과(프리팹을 덮어쓴다).
public static class ChocolateFactoryAssets
{
    public const string ModelFolder = "Assets/Maps/ChocolateFactory/Models/Buildings";
    public const string PropFolder = "Assets/Maps/ChocolateFactory/Models/Props";    // §40 꾸미기 소품 10종
    public const string GeneratedFolder = "Assets/Maps/ChocolateFactory/Generated";
    public const string PrefabFolder = "Assets/04. Prefabs/ChocolateFactory";
    public const string DoorControllerPath = "Assets/Maps/Common/Doors/MapDoor.controller";
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";

    public static readonly string[] MaterialBuildings = { "Gear", "MacaronWheel", "ChocolateOil", "CookieWheel" };
    public static readonly string[] GeneralBuildings = { "Cauldron", "Factory", "ChocoBar", "Cogworks" };
    public static IEnumerable<string> AllBuildings => MaterialBuildings.Concat(GeneralBuildings);

    public const string DoorLeafLeft = "Leaf_L", DoorLeafRight = "Leaf_R";
    public const float DoorOpenAngle = 90f;
    private const float LeafThickness = 0.2f;
    private const float CorridorHalf = 2.5f + 0.4f;   // 문 구멍 반폭 + 통로 벽 두께(Blender CUT_W / 2 + T)
    private const float FillReach = 26f;
    private const float GlowTop = 60f;

    public static string PrefabName(string key) => "CHO_Bld_" + key;
    public static string PrefabPath(string key) => $"{PrefabFolder}/{PrefabName(key)}.prefab";

    // ---------------------------------------------------------------- json (chb_build.py가 쓴다, Unity 축)

    [Serializable] public class Meta
    {
        public string name, kind, material, socket;
        public float sunk;
        public Room room;
        public DoorMeta[] doors;
        public SpinMeta[] spin;
        public FlowMeta[] flows;
        public FxMeta[] fx;
        public PathMeta[] paths;
        public string[] items;
    }
    [Serializable] public class Room { public float[] min, max; public float ceiling; }
    [Serializable] public class DoorMeta { public string side; public float[] root, centre; public float yaw, leaf_width, height; public string[] leaves; }
    [Serializable] public class SpinMeta { public string name; public float[] axis; public float speed; }
    [Serializable] public class FlowMeta { public string name; public float[] speed; }
    [Serializable] public class FxMeta { public string name, kind; public float radius; }
    [Serializable] public class PathMeta { public string name, item; public string[] points; public int count; public float speed; }

    public static Meta LoadMeta(string key)
    {
        string path = $"{ModelFolder}/{PrefabName(key)}.json";
        return File.Exists(path) ? JsonUtility.FromJson<Meta>(File.ReadAllText(path)) : null;
    }

    private static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

    // ---------------------------------------------------------------- menu

    [MenuItem(MenuRoot + "Build Chocolate Factory Buildings (import, prefabs)")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        TwistedPropBuilder.EnsureFolder(PrefabFolder);
        TwistedPropBuilder.EnsureFolder(GeneratedFolder);
        var report = new List<string> { CarnivalRideModelsImporter.ImportFolder(ModelFolder), CarnivalRideModelsImporter.ImportFolder(PropFolder) };
        foreach (string key in AllBuildings) report.Add(BuildBuilding(key));
        report.Add(BuildProps());
        AssetDatabase.SaveAssets();
        return "[ChocolateFactoryAssets] " + string.Join(" | ", report);
    }

    // ---------------------------------------------------------------- one building

    public static string BuildBuilding(string key)
    {
        string name = PrefabName(key);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{name}.fbx");
        Meta meta = LoadMeta(key);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DoorControllerPath);
        if (model == null || meta == null) return $"{name}: missing model/json";
        if (controller == null) return $"{name}: missing {DoorControllerPath}";

        var root = new GameObject(name);
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Model";
            Transform m = instance.transform;
            // 가져온 FBX(축 변환 굽기)는 Blender 앞(−Y)이 −Z를 본다 — json(chb_build.py, 앞 = +Z)과 맞게 모델을 180° 돌린다
            m.localRotation = Quaternion.Euler(0f, 180f, 0f);

            MeshCollider exterior = SetupMeshes(m, name);
            int doors = BuildDoors(root.transform, m, meta, controller, name);
            string moving = BuildMoving(root.transform, m, meta);
            int sounds = AddSounds(m);
            int boxes = exterior != null ? BuildingFill.Build(root.transform, exterior, Opening(meta), FillReach) : 0;
            if (exterior != null) Object.DestroyImmediate(exterior.gameObject);
            string socket = meta.kind == "material_building" ? SetupSocket(root.transform, m) : "general";
            AddLights(root.transform, meta);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (IsStatic(t)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(key));
            return $"{name}: doors {doors}, {moving}, sounds {sounds}, fill {boxes}, {socket}";
        }
        finally { Object.DestroyImmediate(root); }
    }

    // 움직이거나 켜졌다 꺼지는 것(문·회전·흐름·컨베이어·파티클·빛·빛기둥)은 정적 배칭에서 뺀다.
    private static bool IsStatic(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            string n = p.name;
            if (n.StartsWith("Door_") || n.StartsWith("Spin_") || n.StartsWith("Flow_") || n.StartsWith("Conveyor_") || n.StartsWith("Item_")
                || n.StartsWith("FX_") || n == "ESC_Glow" || n.StartsWith("Socket_")) return false;
        }
        return t.GetComponent<Light>() == null;
    }

    // 겉: 땅 아래 받침판을 잘라 낸 사본, 충돌 사본은 채움 상자를 재는 데만(다 쓰면 지운다), 방 충돌 상자는 그대로.
    private static MeshCollider SetupMeshes(Transform model, string name)
    {
        MeshCollider exterior = null;
        foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.name.StartsWith("Exterior")) mf.sharedMesh = CarnivalMapAssets.TrimGroundPlate(mf.sharedMesh, name, GeneratedFolder);
            if (!mf.name.StartsWith("COL_")) continue;
            var r = mf.GetComponent<Renderer>();
            if (r != null) Object.DestroyImmediate(r);
            var col = mf.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;
            if (mf.name.StartsWith("COL_Exterior")) exterior = col;
        }
        return exterior;
    }

    // 채움 상자에서 뺄 곳: 방 바닥(방 벽 안쪽) + 앞·뒤 문 통로(통로 폭 그대로 방 벽에서 건물 바깥까지).
    private static Func<float, float, bool> Opening(Meta meta)
    {
        Vector3 lo = V(meta.room.min), hi = V(meta.room.max);
        float cz = (lo.z + hi.z) / 2f;
        var bands = meta.doors.Select(d => (x: d.centre[0], outward: d.centre[2] > cz)).ToArray();
        return (x, z) =>
        {
            if (x > lo.x - 0.2f && x < hi.x + 0.2f && z > lo.z - 0.2f && z < hi.z + 0.2f) return true;
            foreach (var b in bands)
                if (Mathf.Abs(x - b.x) < CorridorHalf && (b.outward ? z > hi.z - 0.5f : z < lo.z + 0.5f)) return true;
            return false;
        };
    }

    // ---------------------------------------------------------------- doors

    // 문 루트(경첩 L 자리, +Z = 방 안쪽) 아래 Leaf_L(+X로 뻗음)·Leaf_R(2 m 옆, −X로 뻗음). 받은 문짝 메시는 월드 자세 그대로 문짝 아래에 둔다
    // (Blender에서 원점 = 경첩). 공용 컨트롤러가 Leaf_L −90°/Leaf_R +90°로 안쪽, 반대로 바깥쪽을 연다(MapSceneBuilder와 같은 구조).
    private static int BuildDoors(Transform root, Transform model, Meta meta, AnimatorController controller, string name)
    {
        Transform parent = new GameObject("Doors").transform;
        parent.SetParent(root, false);
        int built = 0;
        foreach (DoorMeta d in meta.doors)
        {
            Transform left = Find(model, d.leaves[0]), right = Find(model, d.leaves[1]);
            if (left == null || right == null) { Debug.LogError($"[ChocolateFactoryAssets] {name} {d.side}: leaf mesh missing"); continue; }
            var door = new GameObject($"Door_{d.side}");
            door.transform.SetParent(parent, false);
            door.transform.SetPositionAndRotation(V(d.root), Quaternion.Euler(0f, d.yaw, 0f));
            AddLeaf(door.transform, DoorLeafLeft, left, Vector3.zero, d, +1f);
            AddLeaf(door.transform, DoorLeafRight, right, Vector3.right * (d.leaf_width * 2f), d, -1f);

            var animator = door.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // 화면 밖에서도 문 충돌체가 움직여야 한다
            animator.updateMode = AnimatorUpdateMode.Fixed;           // kinematic 문짝을 물리 스텝에 맞춰 움직인다
            var body = door.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var interactable = door.AddComponent<InteractableDoor>();
            var so = new SerializedObject(interactable);
            so.FindProperty("doorId").stringValue = $"{name}_{d.side}";   // 건물마다 한 번씩 놓는다 — 씬 안에서 겹치지 않는다
            so.FindProperty("inwardOpenAngle").floatValue = -DoorOpenAngle; // Leaf_L(+X로 뻗음)이 −90°로 돌면 +Z(안쪽)로 간다
            so.FindProperty("leafCollider").objectReferenceValue = door.transform.Find(DoorLeafLeft).GetComponent<BoxCollider>();
            so.FindProperty("interactionRange").floatValue = d.leaf_width + 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            built++;
        }
        return built;
    }

    private static void AddLeaf(Transform door, string leafName, Transform mesh, Vector3 localPos, DoorMeta d, float extend)
    {
        var leaf = new GameObject(leafName).transform;
        leaf.SetParent(door, false);
        leaf.localPosition = localPos;
        mesh.SetParent(leaf, true);
        // 판 부분만 덮는 상자(손잡이·초콜릿 조각 돌출은 제외) — 규칙상 convex(상자)
        var box = leaf.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(extend * d.leaf_width / 2f, d.height / 2f, 0f);
        box.size = new Vector3(d.leaf_width - 0.04f, d.height, LeafThickness);
    }

    // ---------------------------------------------------------------- moving parts

    private static string BuildMoving(Transform root, Transform model, Meta meta)
    {
        int spins = 0, flows = 0, fx = 0, conveyors = 0;
        foreach (SpinMeta s in meta.spin ?? new SpinMeta[0])
        {
            Transform t = Find(model, s.name);
            if (t == null) continue;
            t.gameObject.AddComponent<SpinPart>().EditorSetup(t.InverseTransformDirection(V(s.axis)), s.speed); // json 축(건물 축) → 부품 로컬 축
            spins++;
        }
        foreach (FlowMeta f in meta.flows ?? new FlowMeta[0])
        {
            Transform t = Find(model, f.name);
            Renderer r = t != null ? t.GetComponent<Renderer>() : null;
            if (r == null) continue;
            t.gameObject.AddComponent<TextureScroll>().EditorSetup(r, new Vector2(f.speed[0], f.speed[1]));
            flows++;
        }
        foreach (FxMeta f in meta.fx ?? new FxMeta[0])
        {
            Transform t = Find(model, f.name);
            if (t != null && ChocolateFactoryFx.Attach(t, f.kind, f.radius)) fx++;
        }
        foreach (PathMeta p in meta.paths ?? new PathMeta[0])
        {
            Transform item = Find(model, p.item), a = Find(model, p.points[0]), b = Find(model, p.points[1]);
            if (item == null || a == null || b == null) continue;
            var go = new GameObject("Conveyor_" + p.name);
            go.transform.SetParent(root, false);
            go.AddComponent<ConveyorItems>().EditorSetup(item, a, b, p.count, p.speed);
            conveyors++;
        }
        return $"spin {spins}, flow {flows}, fx {fx}, conveyor {conveyors}";
    }

    // 공장 소리: 옛 Game_ChocolateFactory 기계실과 같은 AmbFactory 3D 반복음(AMB_Room·AMB_Gears·AMB_Pour·AMB_Chimney)
    private static int AddSounds(Transform model)
    {
        int n = 0;
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("AMB_")))
        {
            var emitter = t.gameObject.AddComponent<AmbientEmitter>();
            var so = new SerializedObject(emitter);
            so.FindProperty("sound").intValue = (int)SoundId.AmbFactory;
            so.ApplyModifiedPropertiesWithoutUndo();
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- material buildings

    // 꽂는 곳(Socket_<재료>, Blender가 받침 위에 둔 흰 모양 — QuadrantBuilding이 필요한 재료 색으로 칠한다)을 받침 높이까지 상자로 막고,
    // 받침 위로 가동 빛기둥(ESC_Glow)을 세운다. Repair_Point(쿠키 손 높이 1.3 m)는 Blender가 둔 그대로 쓴다.
    private static string SetupSocket(Transform root, Transform model)
    {
        Transform socket = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Socket_"));
        Transform repair = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Repair_Point"));
        if (socket == null || repair == null) return "socket missing";
        Bounds b = socket.GetComponent<Renderer>().bounds;
        var box = socket.gameObject.AddComponent<BoxCollider>();
        Vector3 c = socket.InverseTransformPoint(new Vector3(b.center.x, b.max.y / 2f, b.center.z));
        box.center = c;
        box.size = new Vector3(b.size.x, b.max.y, b.size.z);

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(glow.GetComponent<Collider>());
        glow.name = "ESC_Glow";
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = new Vector3(b.center.x, GlowTop / 2f, b.center.z);
        glow.transform.localScale = new Vector3(0.9f, GlowTop / 2f, 0.9f);
        var r = glow.GetComponent<Renderer>();
        r.sharedMaterial = CandyForestAssets.LoadMaterial(CandyForestAssets.GlowMaterial);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glow.SetActive(false); // 가동되면 QuadrantBuilding이 켠다(맵에 놓기 전·확인 씬에서는 꺼져 있다)
        return $"{socket.name} at {b.center:F1}, repair y {repair.position.y:F2}";
    }

    // 방 빛 하나(방 크기에 맞춘 범위) + 문 통로마다 하나. 램프 모양은 Blender가 만들었다(빛나는 재질).
    private static void AddLights(Transform root, Meta meta)
    {
        Vector3 lo = V(meta.room.min), hi = V(meta.room.max);
        Vector3 centre = (lo + hi) / 2f;
        float size = Mathf.Max(hi.x - lo.x, hi.z - lo.z);
        NewLight(root, "Light_Inside", new Vector3(centre.x, meta.room.ceiling - 1.5f, centre.z), size * 0.8f + 4f, 1.8f);
        foreach (DoorMeta d in meta.doors)
        {
            Vector3 door = V(d.centre);
            float wallZ = door.z > centre.z ? hi.z : lo.z;
            NewLight(root, $"Light_{d.side}Corridor", new Vector3(door.x, 4.6f, (door.z + wallZ) / 2f), Mathf.Abs(door.z - wallZ) * 0.6f + 4f, 1.3f);
        }
    }

    private static void NewLight(Transform root, string name, Vector3 pos, float range, float intensity)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(root, false);
        light.transform.localPosition = pos;
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.color = new Color(1f, 0.78f, 0.55f);
        light.shadows = LightShadows.None;
    }

    // ---------------------------------------------------------------- §40 props

    // 기둥 모양(가로등·깃발·배너)은 기둥만 막는 캡슐, 나머지는 겉모습 범위 상자(최소 1 m — 쿠키가 올라서지 못할 만큼은 아니어도 걸리게).
    private static readonly HashSet<string> PoleProps = new HashSet<string> { "CHO_StreetLamp", "CHO_FlagStand", "CHO_Banner" };

    public static string PropPath(string name) => $"{PrefabFolder}/Props/{name}.prefab";

    public static string BuildProps()
    {
        TwistedPropBuilder.EnsureFolder(PrefabFolder + "/Props");
        int n = 0;
        foreach (string fbx in Directory.GetFiles(PropFolder, "*.fbx"))
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx.Replace('\\', '/'));
            if (model == null) continue;
            var root = new GameObject(model.name);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Model";
                Bounds b = root.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                if (PoleProps.Contains(model.name))
                {
                    var cap = root.AddComponent<CapsuleCollider>();
                    cap.radius = 0.3f;
                    cap.height = b.size.y;
                    cap.center = new Vector3(0f, b.size.y / 2f, 0f);
                }
                else
                {
                    var box = root.AddComponent<BoxCollider>();
                    float h = Mathf.Max(1f, b.size.y);
                    box.center = new Vector3(b.center.x, h / 2f, b.center.z);
                    box.size = new Vector3(b.size.x * 0.92f, h, b.size.z * 0.92f);
                }
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                PrefabUtility.SaveAsPrefabAsset(root, PropPath(model.name));
                n++;
            }
            finally { Object.DestroyImmediate(root); }
        }
        return $"props {n}";
    }

    private static Transform Find(Transform model, string name) =>
        model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
}
