using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 저주받은 놀이공원 실제 맵 프리팹(Request1009Plan.md §36, 단계 CM3). CarnivalRideModelsImporter가 재질을 이은 FBX로:
// - 재료 건물 CUR_Bld_<재료>: 겉(충돌 사본 COL_Exterior) + 방(Interior)은 Mesh Collider, 받침대 앞(방 가운데 쪽)에 꽂는 곳(CUR_Socket_<재료>)과
//   수리 자리(Repair_Point, 쿠키 손 높이), 가동 빛기둥(ESC_Glow), 방 빛(Light_Inside). 이름 규칙은 QuadrantBuilding(Repair_Point·Socket_·ESC_Glow).
// - 소품(MO_*): 사람 높이 아래를 막는 단순 충돌체(캡슐·상자). 깃발 줄·풍선은 머리 위라 막지 않는다.
// - 놀이기구 바이킹·자이로드롭: 받침 전체를 막는 충돌체(받침·계단 밑에 숨는 틈이 없게), 바이킹 배는 VikingRide가 흔든다.
// 여러 번 실행해도 같은 결과(프리팹을 덮어쓴다).
public static class CarnivalMapAssets
{
    public const string ModelRoot = CarnivalRideModelsImporter.Root;
    public const string PrefabFolder = CarnivalRideModelsImporter.PrefabFolder;
    public const string BuildingFolder = ModelRoot + "/MaterialBuildings";
    public const string MapObjectFolder = ModelRoot + "/MapObjects";
    public const float RepairHeight = 1.3f;     // 쿠키 손 높이(캔디숲과 같음) — 재료는 이보다 0.2 m 위(받침 홈 위)에 놓인다
    private const float SocketGap = 0.9f;        // 받침대 둘레에서 꽂는 곳 가운데까지
    private const float GlowTop = 60f;
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";

    // 사분면 → 재료 건물(간판). 레시피 buildingMaterials 순서와 같다(EscapeRecipeSO.FixedBuildingOrder).
    public static readonly string[] BuildingKeys = { "SafetyBelt", "Battery", "RepairKit", "RedButton" };

    [Serializable]
    private class BuildingMeta
    {
        public float[] pedestal;   // Unity 축 [x, z, 꼭대기, 반지름]
        public Doors doors;
        public Room room;
        public Ceiling ceiling;
    }
    [Serializable] private class Doors { public Door front, back; }
    [Serializable] private class Door { public float width, height; }
    [Serializable] private class Room { public float width, depth; }
    [Serializable] private class Ceiling { public float centre, edge_min; }

    // 소품 충돌체: 캡슐(반경, 높이) 또는 상자(겉모습 범위 × 배율, 최소 높이). 없으면 충돌 없음.
    private struct Shape
    {
        public float Radius, Height, Scale, MinHeight;
        public bool Box;
        public Vector2 Offset; // 캡슐 가운데(x, z)
        public static Shape Capsule(float r, float h, float x = 0f, float z = 0f) => new Shape { Radius = r, Height = h, Offset = new Vector2(x, z) };
        public static Shape Bounds(float scale = 0.92f, float minHeight = 2.6f) => new Shape { Box = true, Scale = scale, MinHeight = minHeight };
    }

    private static readonly Dictionary<string, Shape[]> PropShapes = new Dictionary<string, Shape[]>
    {
        { "MO_Bench", new[] { Shape.Bounds(1f, 1.1f) } },
        { "MO_StreetLamp", new[] { Shape.Capsule(0.4f, 5.6f) } },
        { "MO_Fountain", new[] { Shape.Capsule(2.45f, 4.4f) } },
        { "MO_Fence", new[] { Shape.Bounds(1f, 2.4f) } },
        { "MO_CrateBarrel", new[] { Shape.Bounds(1f, 2.6f) } },
        { "MO_CherryJuice", new[] { Shape.Capsule(1.05f, 4f) } },
        { "MO_CottonCandy", new[] { Shape.Bounds(0.95f, 3f) } },
        { "MO_Popcorn", new[] { Shape.Bounds(0.95f, 3f) } },
        { "MO_Candy", new[] { Shape.Bounds(0.95f, 3f) } },
        { "MO_Bunting", new[] { Shape.Capsule(0.35f, 4.8f, -4f), Shape.Capsule(0.35f, 4.8f, 4f) } },
        { "MO_Planter", new[] { Shape.Capsule(1.3f, 3f) } },
        { "MO_Signpost", new[] { Shape.Capsule(0.35f, 3.8f) } },
        { "MO_BalloonCart", new[] { Shape.Bounds(1f, 2.6f) } },
        { "MO_TicketBooth", new[] { Shape.Capsule(1.5f, 6f) } },
    };

    [MenuItem(MenuRoot + "Build Carnival Map Prefabs (buildings, sockets, props, rides)")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        TwistedPropBuilder.EnsureFolder(PrefabFolder);
        var report = new List<string>();
        foreach (string key in BuildingKeys) report.Add(BuildBuilding(key));
        int props = PropShapes.Keys.Count(name => BuildProp(name) != null);
        report.Add($"props {props}/{PropShapes.Count}");
        report.Add(BuildViking());
        report.Add(BuildGyroDrop());
        AssetDatabase.SaveAssets();
        return "[CarnivalMapAssets] " + string.Join(" | ", report);
    }

    public static string PrefabPath(string name) => $"{PrefabFolder}/{name}.prefab";
    public static GameObject LoadPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));

    // ---------------------------------------------------------------- material buildings

    private static string BuildBuilding(string key)
    {
        string name = "CUR_Bld_" + key;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildingFolder}/{name}.fbx");
        var socketModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapObjectFolder}/CUR_Socket_{key}.fbx");
        string metaPath = $"{BuildingFolder}/{name}.json";
        if (model == null || socketModel == null || !File.Exists(metaPath)) return $"{name}: missing model/socket/json";
        BuildingMeta meta = JsonUtility.FromJson<BuildingMeta>(File.ReadAllText(metaPath));

        var root = new GameObject(name);
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Model";
            foreach (MeshFilter mf in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name.StartsWith("Exterior")) mf.sharedMesh = TrimGroundPlate(mf.sharedMesh, name);
                bool collision = mf.name.StartsWith("COL_");
                if (collision)
                {
                    var r = mf.GetComponent<Renderer>();
                    if (r != null) Object.DestroyImmediate(r);
                }
                if (collision || mf.name.StartsWith("Interior")) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
            string walls = BuildingRing(root.transform, instance.transform, key);
            // Blender 수리 자리(받침대 꼭대기)는 지운다 — 쿠키 손 높이의 수리 자리를 꽂는 곳 위에 새로 둔다
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Repair_Point")).ToList())
                Object.DestroyImmediate(t.gameObject);

            // json 받침대 x는 Blender 축 그대로(Unity x = −x) — 방 단면을 재어 확인했다(§36 CM7)
            float px = -meta.pedestal[0], pz = meta.pedestal[1], radius = meta.pedestal[3];
            float side = Mathf.Sign(px == 0f ? 1f : px);
            var socketPos = new Vector3(px - side * (radius + SocketGap), 0f, pz);
            var socket = (GameObject)PrefabUtility.InstantiatePrefab(socketModel, root.transform);
            socket.name = "CUR_Socket_" + key;
            socket.transform.localPosition = socketPos;
            socket.transform.localRotation = Quaternion.LookRotation(new Vector3(side, 0f, 0f)); // 앞(홈, 모델 −Z)이 방 가운데를 본다
            AddSocketCollider(socket);

            BlockBehindPedestal(root.transform, meta, px, radius);

            var repair = new GameObject("Repair_Point").transform;
            repair.SetParent(root.transform, false);
            repair.localPosition = socketPos + Vector3.up * RepairHeight;

            Transform glow = Primitive(root.transform, "ESC_Glow", new Vector3(px, GlowTop / 2f, pz), new Vector3(0.9f, GlowTop / 2f, 0.9f),
                CandyForestAssets.LoadMaterial(CandyForestAssets.GlowMaterial));
            glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var light = new GameObject("Light_Inside").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            float ceiling = meta.ceiling != null ? meta.ceiling.edge_min : 6f;
            light.transform.localPosition = new Vector3(0f, Mathf.Max(3f, ceiling - 1f), 0f);
            light.type = LightType.Point;
            float roomSize = meta.room != null ? Mathf.Max(meta.room.width, meta.room.depth) : 18f;
            light.range = roomSize * 0.75f + 4f;
            light.intensity = 1.8f;
            light.color = new Color(1f, 0.8f, 0.68f);
            light.shadows = LightShadows.None;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<Light>() == null && t.name != "ESC_Glow")
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
            return $"{name}: socket {socketPos:F1}, repair y {RepairHeight}, light r {light.range:F0}, {walls}";
        }
        finally { Object.DestroyImmediate(root); }
    }

    public const string GeneratedFolder = "Assets/Maps/CursedCandyCarnival/Generated";
    private const float WallHeight = BuildingFill.WallHeight;
    private const float FillReach = 26f;

    // 겉 건물(받은 모델)을 메시 충돌로 두지 않고 방과 문 통로를 뺀 건물 바닥 자리를 높은 상자로 채운다(BuildingFill, §36 CM7):
    //   방 = 위에서 쏜 광선이 방 바닥(Interior, 0.3 m 아래)에 닿는 곳, 문 통로 = 앞(+Z)·뒤(−Z) 문 폭 그대로 방 벽에서 바깥까지.
    // 겉 충돌 메시는 지운다(재는 데만 쓴다).
    // 문 띠(Unity 축, 앞 +Z / 뒤 −Z): mb_build.py OPENINGS(22 m 틀 가운데 x, 폭) × 1.7, Blender x → Unity −x. 폭은 json doors와 같다.
    private static readonly Dictionary<string, (float cx, float half)[]> DoorBands = new Dictionary<string, (float, float)[]>
    {
        { "RedButton", new[] { (0.17f, 13.26f / 2f), (1.87f, 10.2f / 2f) } },
        { "Battery", new[] { (0.34f, 8.84f / 2f), (0f, 8.16f / 2f) } },
        { "RepairKit", new[] { (-0.17f, 13.94f / 2f), (0f, 10.2f / 2f) } },
        { "SafetyBelt", new[] { (-0.17f, 13.94f / 2f), (0f, 10.2f / 2f) } },
    };

    private static string BuildingRing(Transform root, Transform model, string key)
    {
        MeshCollider interior = model.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault(c => c.name.StartsWith("Interior"));
        MeshCollider exterior = model.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault(c => c.name.StartsWith("COL_"));
        if (interior == null || exterior == null) return "fill: missing colliders";
        bool RoomFloor(float x, float z) => interior.Raycast(new Ray(new Vector3(x, 4f, z), Vector3.down), out RaycastHit hit, 5f) && hit.point.y < 0.3f;
        (float cx, float half)[] door = DoorBands[key];
        bool Corridor(float x, float z) => z > 0f ? Mathf.Abs(x - door[0].cx) < door[0].half : Mathf.Abs(x - door[1].cx) < door[1].half;
        int boxes = BuildingFill.Build(root, exterior, (x, z) => RoomFloor(x, z) || Corridor(x, z), FillReach);
        Object.DestroyImmediate(exterior);
        return $"fill {boxes} boxes";
    }
    private const float GroundPlateTop = 0.15f;

    // 받은 건물 모델은 바닥 받침판(땅 높이의 위를 보는 넓은 면, 건물 둘레 ±19 m)을 들고 있어 맵 바닥 타일을 덮는다(연보라 맨바닥으로 보임).
    // 땅 높이(0.15 m 아래)에 다 들어가는 면을 뺀 사본을 Generated/에 저장해 쓴다 — 그 위 벽·계단은 그대로.
    public static Mesh TrimGroundPlate(Mesh source, string name) => TrimGroundPlate(source, name, GeneratedFolder);

    public static Mesh TrimGroundPlate(Mesh source, string name, string folder)
    {
        Vector3[] v = source.vertices;
        var mesh = Object.Instantiate(source);
        mesh.name = name + "_Exterior";
        int removed = 0;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] tris = source.GetTriangles(sub);
            var kept = new List<int>(tris.Length);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                bool plate = Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < GroundPlateTop; // 기울어진 조각도(바닥 타일 위로 검은 조각처럼 보였다)
                if (plate) { removed++; continue; }
                kept.Add(tris[i]); kept.Add(tris[i + 1]); kept.Add(tris[i + 2]);
            }
            mesh.SetTriangles(kept, sub);
        }
        mesh.RecalculateBounds();
        TwistedPropBuilder.EnsureFolder(folder);
        string path = $"{folder}/{mesh.name}.asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) AssetDatabase.CreateAsset(mesh, path);
        else
        {
            EditorUtility.CopySerialized(mesh, saved);
            Object.DestroyImmediate(mesh);
            mesh = saved;
        }
        Debug.Log($"[TrimGroundPlate] {name}: removed {removed} ground-plate faces");
        return mesh;
    }

    private const float MonsterPassage = 3.6f;   // 받침대 옆 통로가 이보다 좁으면(괴물 통로 규칙, 통행 검사 괴물 몸 지름) 받침대 뒤 막다른 곳을 막는다

    // 좁고 긴 방(수리도구 7.8 m·빨간 버튼 9 m)은 받침대 옆 통로가 좁아 받침대 뒤가 쿠키만 숨는 막다른 곳이 된다 —
    // 받침대 가운데부터 방 끝 벽까지를 높은 상자로 막는다(꽂는 곳은 받침대 앞, 방 가운데 쪽이라 그대로).
    private static void BlockBehindPedestal(Transform root, BuildingMeta meta, float px, float radius)
    {
        if (meta.room == null) return;
        float gap = meta.room.depth / 2f - radius;
        if (gap >= MonsterPassage) return;
        float side = Mathf.Sign(px == 0f ? 1f : px);
        float length = Mathf.Max(1f, meta.room.width / 2f + 2f - Mathf.Abs(px));
        var box = new GameObject("COL_BehindPedestal").AddComponent<BoxCollider>();
        box.transform.SetParent(root, false);
        box.transform.localPosition = new Vector3(px + side * length / 2f, WallHeight / 2f, 0f);
        box.size = new Vector3(length, WallHeight, meta.room.depth + 2f);
    }

    // 꽂는 곳 몸통(받침 판 제외)을 상자로 막는다 — 쿠키가 홈 위로 올라서지 못하게 받침 판 높이까지 포함.
    private static void AddSocketCollider(GameObject socket)
    {
        Bounds? b = null;
        foreach (Renderer r in socket.GetComponentsInChildren<Renderer>(true))
        {
            Bounds rb = r.bounds;
            if (b == null) b = rb;
            else { Bounds v = b.Value; v.Encapsulate(rb); b = v; }
        }
        if (b == null) return;
        Bounds w = b.Value;
        var box = socket.AddComponent<BoxCollider>();
        Vector3 localCenter = socket.transform.InverseTransformPoint(w.center);
        Vector3 localSize = socket.transform.InverseTransformVector(w.size);
        box.center = new Vector3(localCenter.x, w.size.y / 2f, localCenter.z);
        box.size = new Vector3(Mathf.Abs(localSize.x) * 0.9f, w.size.y, Mathf.Abs(localSize.z) * 0.9f);
    }

    // ---------------------------------------------------------------- props

    private static GameObject BuildProp(string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapObjectFolder}/{name}.fbx");
        if (model == null) return null;
        var root = new GameObject(name);
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = "Model";
            Bounds bounds = RendererBounds(root);
            int i = 0;
            foreach (Shape s in PropShapes[name])
            {
                var col = new GameObject($"COL_{i++}");
                col.transform.SetParent(root.transform, false);
                if (s.Box)
                {
                    var box = col.AddComponent<BoxCollider>();
                    float h = Mathf.Max(bounds.size.y, s.MinHeight);
                    box.center = new Vector3(bounds.center.x, h / 2f, bounds.center.z);
                    box.size = new Vector3(bounds.size.x * s.Scale, h, bounds.size.z * s.Scale);
                }
                else
                {
                    var cap = col.AddComponent<CapsuleCollider>();
                    cap.radius = s.Radius;
                    cap.height = Mathf.Max(s.Height, s.Radius * 2f);
                    cap.center = new Vector3(s.Offset.x, cap.height / 2f, s.Offset.y);
                }
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            if (name == FountainWaterBuilder.PrefabName) FountainWaterBuilder.Attach(root); // 움직이는 물(파티클·물 표면·물소리, §39)
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Bounds RendererBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------------------------------------------------------------- rides

    // 바이킹: 받침·틀·축은 고정, 배(VK_Ship, 원점 = 축)는 VikingRide가 로컬 Z축으로 흔든다. 받침 전체는 속이 찬 상자(계단 밑·배 아래 틈 없음).
    private static string BuildViking()
    {
        string folder = ModelRoot + "/Viking";
        var root = new GameObject("CUR_Viking");
        try
        {
            foreach (string part in new[] { "VK_Platform", "VK_Frame", "VK_Axle", "VK_Ship" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{part}.fbx");
                if (model == null) return "viking: missing " + part;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                go.name = part;
            }
            Bounds platform = RendererBounds(root.transform.Find("VK_Platform").gameObject);
            var box = new GameObject("COL_Platform").AddComponent<BoxCollider>();
            box.transform.SetParent(root.transform, false);
            box.center = new Vector3(platform.center.x, 3f, platform.center.z);
            box.size = new Vector3(platform.size.x, 6f, platform.size.z);
            Transform ship = root.transform.Find("VK_Ship");
            var ride = root.AddComponent<VikingRide>();
            ride.EditorSetup(ship);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (!t.IsChildOf(ship)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("CUR_Viking"));
            return $"viking: platform {platform.size:F1}";
        }
        finally { Object.DestroyImmediate(root); }
    }

    // 자이로드롭: 받침(높이 3 m 위 승강장)과 탑을 막는다. 링은 겉모습만.
    private static string BuildGyroDrop()
    {
        string folder = ModelRoot + "/GyroDrop";
        var root = new GameObject("CUR_GyroDrop");
        try
        {
            foreach (string part in new[] { "GD_Platform", "GD_Tower", "GD_Ring", "GD_Crown" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{part}.fbx");
                if (model == null) return "gyro: missing " + part;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                go.name = part;
            }
            Bounds platform = RendererBounds(root.transform.Find("GD_Platform").gameObject);
            var box = new GameObject("COL_Platform").AddComponent<BoxCollider>();
            box.transform.SetParent(root.transform, false);
            box.center = new Vector3(platform.center.x, 3.5f, platform.center.z);
            box.size = new Vector3(platform.size.x, 7f, platform.size.z);
            var tower = new GameObject("COL_Tower").AddComponent<CapsuleCollider>();
            tower.transform.SetParent(root.transform, false);
            tower.radius = 2.6f;
            tower.height = 40f;
            tower.center = new Vector3(0f, 20f, 0f);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("CUR_GyroDrop"));
            return $"gyro: platform {platform.size:F1}";
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Transform Primitive(Transform parent, string name, Vector3 pos, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }
}
