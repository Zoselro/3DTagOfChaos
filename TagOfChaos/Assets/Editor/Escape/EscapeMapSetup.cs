using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 맵 씬에 탈출 모드 배치를 만든다(Request1009Plan.md §25.5 O). 사분면 맵(테스트 맵 Test_<Map>)의 이름 규칙을 읽어
// EscapeSetup 루트(EscapeManager)를 만들고 씬 오브젝트를 연결한다:
//   Building_Q1~Q4 → QuadrantBuilding, Exit_N/E/S/W → EscapeGate, MonsterDoor_0~3 → MonsterRoomDoor,
//   CHEST_Slot_nn 표시 → 상자(지금 상자 모델), RocketSpawn_nn → 로켓 후보(지금 맵 로켓 모델 하나를 판마다 옮긴다),
//   RainbowSpawn_nn → 무지개 상자 후보, WITCH_Anchor(마녀 위치)는 그대로 둔다.
// 이름 규칙이 없는 옛 맵(Game_<Map> — 탈출 장치·탈것)은 건너뛴다. 옛 배치 도구는 Editor/Escape/Legacy~에 있다.
public static class EscapeMapSetup
{
    public const string RootName = "EscapeSetup";
    public const int ChestSlotCount = 40;
    private const string MaterialFolder = "Assets/09. Environment/Escape/Materials";
    private static readonly string[] ExitOrder = { "Exit_N", "Exit_E", "Exit_S", "Exit_W" };

    [MenuItem("Tools/TagOfChaos/Escape/Setup Open Map Scene")]
    public static void SetupOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log(Apply(scene, SceneNames.MapOf(scene.name)));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/TagOfChaos/Escape/Setup All Map Scenes")]
    public static void SetupAll()
    {
        foreach (string map in MapSceneBuilder.MapNames)
        {
            string path = TestMapBuilder.ScenePath(map);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log(Apply(scene, map));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    // 술래방 지킴이만 붙인다(§28.15 E1) — 상자·로켓 배치는 그대로 두고 맵 씬마다 MonsterRoomGuard만 맞춘다.
    [MenuItem("Tools/TagOfChaos/Escape/Setup Monster Room Guard (All Map Scenes)")]
    public static void SetupRoomGuardAll() => Debug.Log(SetupRoomGuards());

    public static string SetupRoomGuards()
    {
        var report = new List<string>();
        foreach (string map in MapSceneBuilder.MapNames)
        {
            string path = TestMapBuilder.ScenePath(map);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Transform room = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "MonsterRoom");
            if (room == null) { report.Add($"{map}: no MonsterRoom"); continue; }
            MonsterRoomGuard guard = BindRoomGuard(room);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add($"{map}: inner {guard.InnerHalf:F2}");
        }
        return "[EscapeMapSetup] room guards — " + string.Join(", ", report);
    }

    public static string Apply(Scene scene, string map)
    {
        List<Transform> all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Transform Find(string name) => all.FirstOrDefault(t => t != null && t.name == name);
        Transform mapRoot = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Map");
        if (mapRoot == null || Find("Building_Q1") == null) return $"[EscapeMapSetup] {map}: skipped (no quadrant-map names — Request1009Plan.md §25.5 O)";

        // 마녀 위치는 지키고 예전 배치를 지운다
        GameObject oldRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        Transform witchAnchor = oldRoot != null ? oldRoot.transform.Find("WITCH_Anchor") : null;
        if (witchAnchor != null) witchAnchor.SetParent(null, true);
        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToList()) Object.DestroyImmediate(old);

        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        var manager = root.AddComponent<EscapeManager>();

        var buildings = new QuadrantBuilding[4];
        for (int q = 0; q < 4; q++)
        {
            Transform b = mapRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == $"Building_Q{q + 1}");
            if (b == null) continue;
            buildings[q] = b.GetComponent<QuadrantBuilding>() ?? b.gameObject.AddComponent<QuadrantBuilding>();
        }

        var exits = new List<EscapeGate>();
        foreach (string name in ExitOrder)
        {
            Transform e = mapRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (e != null) exits.Add(e.GetComponent<EscapeGate>() ?? e.gameObject.AddComponent<EscapeGate>());
        }

        foreach (Transform door in mapRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("MonsterDoor_")))
            if (door.GetComponent<MonsterRoomDoor>() == null) door.gameObject.AddComponent<MonsterRoomDoor>();

        Vector3 center = Vector3.zero;
        var chestMarkers = mapRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("CHEST_Slot_")).OrderBy(t => t.name).ToList();
        var chests = new List<Transform>();
        foreach (Transform marker in chestMarkers)
        {
            chests.Add(BuildChest(root.transform, chests.Count, marker.position, center));
            Object.DestroyImmediate(marker.gameObject); // 표시는 상자로 바뀐다(이름이 겹치지 않게)
        }

        Transform[] rocketSpots = mapRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("RocketSpawn_")).OrderBy(t => t.name).ToArray();
        foreach (Transform spot in rocketSpots) spot.rotation = Quaternion.LookRotation(Flat(center - spot.position).normalized); // 칸(앞면)이 맵 안쪽을 본다
        SpyRocket rocket = rocketSpots.Length > 0 ? BuildRocket(root.transform, map, rocketSpots[0].position, center) : null;
        Transform[] rainbowSpots = mapRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("RainbowSpawn_")).OrderBy(t => t.name).ToArray();
        var rainbowGo = new GameObject("RAINBOW_Chest");
        rainbowGo.transform.SetParent(root.transform, false);
        RainbowChest rainbow = rainbowGo.AddComponent<RainbowChest>();

        if (witchAnchor == null)
        {
            witchAnchor = new GameObject("WITCH_Anchor").transform;
            witchAnchor.position = Vector3.forward * 155f;
        }
        witchAnchor.SetParent(root.transform, true);
        WitchPresenter witch = witchAnchor.GetComponent<WitchPresenter>() ?? witchAnchor.gameObject.AddComponent<WitchPresenter>();

        Transform room = mapRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MonsterRoom");
        if (room != null) BindRoomGuard(room);
        manager.EditorBind(chests.ToArray(), buildings, exits.ToArray(), rocket, rocketSpots, rainbowSpots, rainbow, room, witch);
        var so = new SerializedObject(manager);
        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        so.FindProperty("recipeOverride").objectReferenceValue = catalog != null ? catalog.RecipeFor(map) : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        return $"[EscapeMapSetup] {map}: buildings {buildings.Count(b => b != null)}, exits {exits.Count}, chests {chests.Count}, " +
               $"rocket spots {rocketSpots.Length}, rainbow spots {rainbowSpots.Length}, witch {witchAnchor.position}";
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // 술래방 안 쿠키 내보내기(§28.15 E1): 벽 안쪽 면 = RoomWall_ 충돌체 중 가운데에서 가장 가까운 면.
    private static MonsterRoomGuard BindRoomGuard(Transform room)
    {
        Physics.SyncTransforms();
        Vector3 c = room.position;
        float inner = float.MaxValue;
        foreach (Collider wall in room.GetComponentsInChildren<Collider>(true).Where(w => w.name.StartsWith("RoomWall_")))
        {
            Bounds b = wall.bounds;
            bool alongX = b.size.z > b.size.x; // 동·서 벽(긴 쪽이 z)
            float near = alongX ? Mathf.Min(Mathf.Abs(b.min.x - c.x), Mathf.Abs(b.max.x - c.x)) : Mathf.Min(Mathf.Abs(b.min.z - c.z), Mathf.Abs(b.max.z - c.z));
            inner = Mathf.Min(inner, near);
        }
        if (inner == float.MaxValue) inner = 11.7f;
        Transform[] doors = room.GetComponentsInChildren<MonsterRoomDoor>(true).Select(d => d.transform).OrderBy(t => t.name).ToArray();
        MonsterRoomGuard guard = room.GetComponent<MonsterRoomGuard>() ?? room.gameObject.AddComponent<MonsterRoomGuard>();
        guard.EditorBind(inner, doors);
        EditorUtility.SetDirty(guard);
        return guard;
    }

    // 지금 맵 로켓 모델(SPY_Rocket_<Map>)을 그대로 쓴다(§25 결정 — 지금 것 그대로). 위치는 판마다 RocketSpawn 중 하나로 옮긴다.
    private static SpyRocket BuildRocket(Transform parent, string map, Vector3 pos, Vector3 center)
    {
        GameObject model = InstantiateModel($"SPY_Rocket_{map}", parent, $"SPY_Rocket_{map}_Root");
        if (model != null)
        {
            model.transform.position = pos;
            Vector3 toCenter = Flat(center - pos);
            if (toCenter.sqrMagnitude > 0.01f) model.transform.rotation = Quaternion.LookRotation(toCenter.normalized);
            var capsule = model.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 2.5f, 0f);
            capsule.radius = 1f;
            capsule.height = 5f;
            return model.AddComponent<SpyRocket>();
        }

        var root = new GameObject($"SPY_Rocket_{map}_Root");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        Shape(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 2.2f, 0f), new Vector3(1.4f, 2f, 1.4f), Mat("SPY_Rocket", new Color(0.25f, 0.25f, 0.3f)), true);
        for (int i = 0; i < 2; i++)
        {
            var slot = new GameObject($"Slot_{i:00}").transform;
            slot.SetParent(root.transform, false);
            slot.localPosition = new Vector3(i == 0 ? -0.6f : 0.6f, 1.6f, 0.75f);
        }
        return root.AddComponent<SpyRocket>();
    }

    // 지금 상자 모델(CHEST) 그대로. 맵 가운데를 바라본다.
    private static Transform BuildChest(Transform parent, int index, Vector3 pos, Vector3 center)
    {
        Vector3 face = Flat(center - pos);
        Quaternion facing = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face.normalized) : Quaternion.identity;
        GameObject model = InstantiateModel("CHEST", parent, $"CHEST_Slot_{index:00}");
        if (model != null)
        {
            model.transform.SetPositionAndRotation(pos, facing);
            var collider = model.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.size = new Vector3(1f, 0.7f, 0.7f);
            model.AddComponent<MaterialChest>();
            return model.transform;
        }

        var root = new GameObject($"CHEST_Slot_{index:00}");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, facing);
        Shape(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 0.7f), Mat("CHEST_Wood", new Color(0.55f, 0.33f, 0.18f)), false);
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.35f, 0f);
        box.size = new Vector3(1f, 0.7f, 0.7f);
        var lid = new GameObject("Lid").transform;
        lid.SetParent(root.transform, false);
        lid.localPosition = new Vector3(0f, 0.6f, -0.35f);
        Shape(lid, "LidMesh", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0.35f), new Vector3(1.04f, 0.12f, 0.74f), Mat("CHEST_Lid", new Color(0.45f, 0.26f, 0.14f)), false);
        root.AddComponent<MaterialChest>();
        return root.transform;
    }

    private static GameObject InstantiateModel(string unit, Transform parent, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{EscapeModelImportPostprocessor.ModelFolder}/{unit}.fbx");
        if (asset == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        go.name = name;
        return go;
    }

    private static void Shape(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material material, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    private static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/09. Environment/Escape")) AssetDatabase.CreateFolder("Assets/09. Environment", "Escape");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/09. Environment/Escape", "Materials");
        string path = $"{MaterialFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
