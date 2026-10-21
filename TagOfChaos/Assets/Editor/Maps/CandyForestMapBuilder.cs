using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 캔디숲 실제 모델 맵 씬(Request1009Plan.md §28, 단계 C6~C8·C10).
// Test_CandyForest를 복사한 CandyForest 씬에서 놀이 공간을 실제 모델로 바꾼다:
//   둥근 바닥·울타리(반지름 80 m) · 바닥 타일(겉모습만) · 술래방 꾸미기 · 재료 건물 4 · 탈출구 4 · 큰길 소품 줄 ·
//   가장자리 괴물집·천막 무리 · 사분면 꾸미기 · 울타리 밖 숲 · 상자/로켓/무지개/스폰 자리 · 조명.
// 배치는 위치표(Assets/03. SO/Maps/CandyForestLayout.asset)를 따른다. 카메라·관리자·마녀·환경음은 테스트 씬 그대로.
// 여러 번 실행해도 같은 결과(위치표가 locked면 위치표를 다시 만들지 않는다).
public static class CandyForestMapBuilder
{
    public const string MapName = "CandyForest";
    public const string ScenePath = "Assets/Scenes/Maps/" + MapName + ".unity";
    public const string LayoutPath = "Assets/03. SO/Maps/CandyForestLayout.asset";
    private const string SourceScene = "Assets/Scenes/Maps/Test/Test_CandyForest.unity";
    private const string MenuRoot = "Tools/TagOfChaos/CandyForest/";

    private static readonly string[] KeepInMap = { "Background", WitchPath.FarBackdropName, "Ground", "MonsterRoom" };
    private const float ExitOpening = 5.6f, ExitPostWidth = 1.8f, ExitHeight = 7.5f, PadDepth = 10f;
    private const float FenceHeight = 6f, FenceThickness = 0.8f;
    private const float ForestClearRadius = 95f;
    // 옛 캔디숲 지형 고리(흰 땅, §28.15 E2): 원본에서 이 맵에 맞춘 메시를 만든다
    private const string TerrainSource = "Assets/Maps/CandyForest/Compact/CAN_Terrain_Decor.asset";
    private const string TerrainMeshPath = CandyForestAssets.Root + "/Models/CF_TerrainRing.asset";
    private const float TerrainFlatY = 0.015f;      // 바닥판(0)·초록 원판 위, 초록 타일 풀 면(0.04) 아래
    private const float TerrainInsideY = -0.05f;    // 울타리 안에 걸친 면의 안쪽 꼭짓점은 바닥판 밑으로
    private const string GrassDiscPath = CandyForestAssets.Root + "/Models/CF_GrassDisc.asset";
    private const float GrassDiscY = 0.008f;
    private const float TerrainDropRadius = 79.5f;  // 이 안쪽에만 있는 면은 지운다(울타리 80 m 안)
    private const float TerrainFlatRadius = 86f;    // 여기까지 평평
    private const float TerrainExitFlatRadius = 93f;// 탈출구 받침(울타리 밖 10 m) 앞은 더 멀리까지 평평
    private const float TerrainRiseLength = 12f;    // 평평한 곳 끝에서 원래 언덕 높이까지

    [MenuItem(MenuRoot + "2 Build Map Scene")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        if (CandyForestAssets.LoadPrefab("CF_Bld_Q1") == null) return "[CandyForestMap] prefabs missing — run 1 Import Models first";
        CandyForestLayoutSO layout = LoadLayout(out string layoutReport);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(SourceScene, ScenePath)) return "[CandyForestMap] copy failed";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        foreach (Transform child in map.Cast<Transform>().ToList())
            if (!KeepInMap.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
        GameObject oldEscape = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EscapeMapSetup.RootName);
        if (oldEscape != null)
            foreach (Transform child in oldEscape.transform.Cast<Transform>().ToList())
                if (child.name != "WITCH_Anchor") Object.DestroyImmediate(child.gameObject);

        ClearBackground(map);
        BuildGround(map);
        BuildMonsterRoom(map);
        int placed = PlaceLayout(map, layout);
        BuildExits(map);
        BuildFence(map);
        int tiles = BuildTiles(map, layout);
        int markers = BuildMarkers(map, layout);
        int lights = BuildLights(scene, map);

        string escape = EscapeMapSetup.Apply(scene, MapName);
        var manager = Object.FindFirstObjectByType<EscapeManager>();
        if (manager != null)
        {
            manager.EditorSetMapRadius(CandyForestLayout.MapRadius);
            EditorUtility.SetDirty(manager);
        }
        MoveSpawn(scene, SceneSpawnPoints.Cookie, map, "Spawn_Cookie_Q1");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings();
        return $"[CandyForestMap] {ScenePath}: {layoutReport}, placed {placed}, tiles {tiles}, markers {markers}, lights {lights} ({DarkReport}) | {escape}";
    }

    // ---------------------------------------------------------------- layout SO

    private static CandyForestLayoutSO LoadLayout(out string report)
    {
        var layout = AssetDatabase.LoadAssetAtPath<CandyForestLayoutSO>(LayoutPath);
        if (layout == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/03. SO/Maps")) AssetDatabase.CreateFolder("Assets/03. SO", "Maps");
            layout = ScriptableObject.CreateInstance<CandyForestLayoutSO>();
            AssetDatabase.CreateAsset(layout, LayoutPath);
        }
        if (layout.locked && layout.placements.Count > 0)
        {
            report = $"layout locked ({layout.placements.Count})";
            return layout;
        }
        layout.placements = CandyForestLayout.Generate(Footprint, out List<string> warnings);
        EditorUtility.SetDirty(layout);
        AssetDatabase.SaveAssets();
        foreach (string w in warnings) Debug.LogWarning("[CandyForestMap] " + w);
        report = $"layout {layout.placements.Count} (warnings {warnings.Count})";
        return layout;
    }

    // 프리팹의 충돌체·겉모습 반지름(수평, 원점 기준)
    public static CandyForestLayout.Footprint Footprint(string asset)
    {
        GameObject prefab = CandyForestAssets.LoadPrefab(asset);
        var f = new CandyForestLayout.Footprint();
        if (prefab == null) return f;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) f.Collider = Mathf.Max(f.Collider, ColliderReach(c));
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) f.Visual = Mathf.Max(f.Visual, FlatReach(r.bounds));
        }
        finally { Object.DestroyImmediate(go); }
        return f;
    }

    private const float WalkZoneHeight = 3f; // 사람이 지나가는 높이 — 나무 머리처럼 그 위에 있는 충돌은 틈 계산에서 뺀다

    // 메시 충돌체는 실제 꼭짓점(사람 높이 아래)으로, 상자·캡슐은 범위로 잰다
    private static float ColliderReach(Collider c)
    {
        if (c is MeshCollider mc && mc.sharedMesh != null)
        {
            float reach = 0f;
            Matrix4x4 m = mc.transform.localToWorldMatrix;
            foreach (Vector3 v in mc.sharedMesh.vertices)
            {
                Vector3 w = m.MultiplyPoint3x4(v);
                if (w.y <= WalkZoneHeight) reach = Mathf.Max(reach, new Vector2(w.x, w.z).magnitude);
            }
            return reach;
        }
        return FlatReach(c.bounds);
    }

    private static float FlatReach(Bounds b)
    {
        float x = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), z = Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
        return Mathf.Sqrt(x * x + z * z);
    }

    // ---------------------------------------------------------------- ground, room, background

    private static Transform Group(Transform map, string name)
    {
        Transform t = map.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(map, false);
        return t;
    }

    private static void ClearBackground(Transform map)
    {
        Transform background = map.Find("Background");
        if (background == null) return;
        foreach (Renderer r in background.GetComponentsInChildren<Renderer>(true).ToList())
        {
            if (r == null) continue;
            Bounds b = r.bounds;
            bool huge = b.size.x > ForestClearRadius * 2f || b.size.z > ForestClearRadius * 2f;
            if (!huge && new Vector2(b.center.x, b.center.z).magnitude < ForestClearRadius) Object.DestroyImmediate(r.gameObject);
        }
    }

    // 걷는 면은 평평한 바닥 하나(타일은 겉모습만, §28.8). 사각 벽은 지우고 둥근 울타리로 바꾼다.
    private static void BuildGround(Transform map)
    {
        Transform ground = map.Find("Ground");
        foreach (Transform child in ground.Cast<Transform>().ToList())
            if (child.name.StartsWith("Wall_")) Object.DestroyImmediate(child.gameObject);
        Transform floor = ground.Find("Floor");
        if (floor == null) return;
        // 바닥 모델은 Blender 축 회전을 들고 있다 — 수평으로 놓인 축만 늘린다(140 m → 220 m)
        Vector3 scale = Vector3.one;
        for (int i = 0; i < 3; i++)
        {
            Vector3 axis = Vector3.zero;
            axis[i] = 1f;
            if (Mathf.Abs((floor.rotation * axis).y) < 0.5f) scale[i] = 220f / 140f;
        }
        floor.localScale = scale;
        // 바닥판은 흰 땅과 같은 재질(지형 고리 틈·모서리가 같은 색), 울타리 안은 초록 원판(풀밭 타일 틈으로 보여도 어색하지 않은 색)
        var r = floor.GetComponent<Renderer>();
        Transform terrain = map.Find("Background/CAN_Terrain_Decor");
        Renderer terrainLook = terrain != null ? terrain.GetComponent<Renderer>() : null;
        if (r != null) r.sharedMaterial = terrainLook != null ? terrainLook.sharedMaterial : PlainMaterial("CF_Ground", new Color(0.3f, 0.42f, 0.2f));
        BuildGrassDisc(ground);
        BuildTerrainRing(map);
    }

    private static void BuildGrassDisc(Transform ground)
    {
        const int segments = 128;
        float radius = CandyForestLayout.MapRadius + 0.6f;
        var vertices = new Vector3[segments + 1];
        var tris = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            tris[i * 3] = 0;
            tris[i * 3 + 1] = (i + 1) % segments + 1;
            tris[i * 3 + 2] = i + 1;
        }
        var mesh = new Mesh { name = "CF_GrassDisc", vertices = vertices, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh = SaveMesh(mesh, GrassDiscPath);

        var disc = new GameObject("GrassDisc");
        disc.transform.SetParent(ground, false);
        disc.transform.SetPositionAndRotation(new Vector3(0f, GrassDiscY, 0f), Quaternion.identity); // Ground는 Blender 축 회전을 들고 있다 — 월드 기준으로 눕힌다
        disc.AddComponent<MeshFilter>().sharedMesh = mesh;
        disc.AddComponent<MeshRenderer>().sharedMaterial = PlainMaterial("CF_Ground", new Color(0.3f, 0.42f, 0.2f));
        GameObjectUtility.SetStaticEditorFlags(disc, StaticEditorFlags.BatchingStatic);
    }

    // 만든 메시를 에셋으로 저장(이미 있으면 내용만 바꿔 씬 참조를 지킨다).
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
        AssetDatabase.SaveAssets();
        return mesh;
    }

    // 흰 땅(옛 지형 고리)을 이 맵에 맞춰 다시 깐다(§28.15 E2): 울타리 안 면은 지우고, 울타리 둘레는 바닥 높이로 평평하게,
    // 그 바깥에서 원래 언덕 높이로 부드럽게 올라간다. 충돌체도 같은 메시를 쓴다.
    private static void BuildTerrainRing(Transform map)
    {
        Transform terrain = map.Find("Background/CAN_Terrain_Decor");
        var source = AssetDatabase.LoadAssetAtPath<Mesh>(TerrainSource);
        if (terrain == null || source == null) { Debug.LogWarning("[CandyForestMap] old terrain ring missing — skipped"); return; }

        Matrix4x4 toWorld = terrain.localToWorldMatrix, toLocal = terrain.worldToLocalMatrix;
        Vector3[] vertices = source.vertices;
        var inside = new bool[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 w = toWorld.MultiplyPoint3x4(vertices[i]);
            float radius = new Vector2(w.x, w.z).magnitude;
            inside[i] = radius < TerrainDropRadius;
            w.y = TerrainHeight(w.x, w.z, w.y);
            vertices[i] = toLocal.MultiplyPoint3x4(w);
        }

        var mesh = new Mesh { name = "CF_TerrainRing", indexFormat = source.indexFormat };
        mesh.vertices = vertices;
        mesh.uv = source.uv;
        if (source.colors.Length == vertices.Length) mesh.colors = source.colors;
        mesh.subMeshCount = source.subMeshCount;
        int dropped = 0;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] tris = source.GetTriangles(sub);
            var kept = new List<int>(tris.Length);
            for (int t = 0; t < tris.Length; t += 3)
            {
                if (inside[tris[t]] && inside[tris[t + 1]] && inside[tris[t + 2]]) { dropped++; continue; }
                kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
            }
            mesh.SetTriangles(kept, sub);
        }
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        mesh = SaveMesh(mesh, TerrainMeshPath);
        terrain.GetComponent<MeshFilter>().sharedMesh = mesh;
        var col = terrain.GetComponent<MeshCollider>();
        if (col != null) col.sharedMesh = mesh;
        Physics.SyncTransforms();
        Debug.Log($"[CandyForestMap] terrain ring: dropped {dropped} faces inside the fence, flattened to {TerrainFlatRadius} m ({TerrainExitFlatRadius} m at exits)");
    }

    // 지형 고리 높이(월드): 평평한 곳 → 원래 높이로 부드럽게. 바닥판(220 m 사각) 위로는 늘 바닥판보다 위.
    public static float TerrainHeight(float x, float z, float original)
    {
        var p = new Vector2(x, z);
        float radius = p.magnitude, flat = TerrainFlatRadius;
        foreach (Vector2 d in CandyForestLayout.RoadDir)
        {
            float along = Vector2.Dot(p, d), side = Mathf.Abs(d.x * p.y - d.y * p.x);
            if (along > 0f) flat = Mathf.Max(flat, Mathf.Lerp(TerrainExitFlatRadius, TerrainFlatRadius, Mathf.InverseLerp(6f, 10f, side)));
        }
        float high = Mathf.Max(original, TerrainFlatY);
        if (radius < CandyForestLayout.MapRadius + 0.5f) return TerrainInsideY;
        if (radius <= flat) return TerrainFlatY;
        return Mathf.Lerp(TerrainFlatY, high, Mathf.SmoothStep(0f, 1f, (radius - flat) / TerrainRiseLength));
    }

    // 울타리 밖 꾸미기를 세울 지형 높이(지형 충돌체 위, 없으면 0).
    private static float GroundAt(Transform map, float x, float z)
    {
        Transform terrain = map.Find("Background/CAN_Terrain_Decor");
        var col = terrain != null ? terrain.GetComponent<MeshCollider>() : null;
        if (col != null && col.Raycast(new Ray(new Vector3(x, 60f, z), Vector3.down), out RaycastHit hit, 120f)) return Mathf.Max(0f, hit.point.y);
        return 0f;
    }

    // 술래방: 벽·문은 그대로(괴물만 지나감, S11), 바닥은 용암 타일, 벽 바깥 가운데에 빨간 상자, 안쪽에 제단.
    private static void BuildMonsterRoom(Transform map)
    {
        Transform room = map.Find("MonsterRoom");
        Material wall = PlainMaterial("CF_RoomWall", new Color(0.32f, 0.08f, 0.1f));
        foreach (Renderer r in room.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.StartsWith("RoomWall_") || r.name.StartsWith("RoomCorner_")) r.sharedMaterial = wall;
            if (r.name == "RoomFloorMark") r.enabled = false;
        }
        Transform deco = Group(room, "CF_RoomDeco");
        for (int i = 0; i < 4; i++)
        {
            Vector2 dir = CandyForestLayout.RoadDir[i];
            Vector3 p = new Vector3(dir.x, 0f, dir.y) * (CandyForestLayout.RoomHalf + 0.3f + 2.8f);
            Spawn("CF_MonsterBox", deco, p, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 90f, false);
        }
        Spawn("CF_MonsterAltar", deco, new Vector3(0f, 0f, 6.5f), 180f, true);
    }

    // ---------------------------------------------------------------- placements

    private static int PlaceLayout(Transform map, CandyForestLayoutSO layout)
    {
        int n = 0;
        foreach (CandyForestLayoutSO.Placement p in layout.placements)
        {
            if (p.asset.StartsWith("CF_Bld_"))
            {
                // 재료 건물: Building_Qn 루트(QuadrantBuilding이 붙는다, 이름은 EscapeMapSetup 규칙)
                string q = p.asset.Substring("CF_Bld_".Length);
                var root = new GameObject("Building_" + q).transform;
                root.SetParent(Group(map, p.group), false);
                root.SetPositionAndRotation(p.position, Quaternion.Euler(0f, p.yaw, 0f));
                Spawn(p.asset, root, Vector3.zero, 0f, false, local: true);
                n++;
                continue;
            }
            Vector3 at = p.position;
            if (new Vector2(at.x, at.z).magnitude > CandyForestLayout.MapRadius + 1f) at.y = GroundAt(map, at.x, at.z); // 울타리 밖은 흰 땅 언덕 위에(§28.15 E2)
            if (Spawn(p.asset, Group(map, p.group), at, p.yaw, p.noColliders) != null) n++;
        }
        return n;
    }

    private static GameObject Spawn(string asset, Transform parent, Vector3 pos, float yaw, bool noColliders, bool local = false)
    {
        GameObject prefab = CandyForestAssets.LoadPrefab(asset);
        if (prefab == null) { Debug.LogWarning($"[CandyForestMap] prefab {asset} missing"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        if (local) { go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); }
        else go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        if (noColliders)
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c); // 프리팹 인스턴스 안 오브젝트는 지우지 않고 충돌체만
        return go;
    }

    // ---------------------------------------------------------------- exits & fence

    // 탈출구(F2): 닫힘 = 회색 돌 문(Door_ — 열리면 땅으로 내려감), 열림 = 초록 문틀(ExitOpenLook_). 문 바깥은 받침 + 막힌 울.
    private static void BuildExits(Transform map)
    {
        Transform exits = Group(map, "Exits");
        for (int i = 0; i < 4; i++)
        {
            Vector2 d = CandyForestLayout.RoadDir[i];
            string key = CandyForestLayout.ExitNames[i].Substring(5);
            var root = new GameObject(CandyForestLayout.ExitNames[i]).transform;
            root.SetParent(exits, false);
            root.SetPositionAndRotation(new Vector3(d.x, 0f, d.y) * CandyForestLayout.MapRadius, Quaternion.LookRotation(new Vector3(-d.x, 0f, -d.y)));

            var door = new GameObject("Door_" + key).transform;
            door.SetParent(root, false);
            var doorBox = door.gameObject.AddComponent<BoxCollider>();
            doorBox.center = new Vector3(0f, ExitHeight / 2f, 0f);
            doorBox.size = new Vector3(ExitOpening, ExitHeight, 1f);
            Spawn("CF_ExitClosed", door, Vector3.zero, 0f, true, local: true);
            var open = Spawn("CF_ExitOpen", root, Vector3.zero, 0f, true, local: true);
            if (open != null) open.name = "ExitOpenLook_" + key;

            float postX = ExitOpening / 2f + ExitPostWidth / 2f;
            LocalBox(root, "COL_PostL", new Vector3(-postX, 4.5f, 0f), new Vector3(ExitPostWidth, 9f, 2.2f));
            LocalBox(root, "COL_PostR", new Vector3(postX, 4.5f, 0f), new Vector3(ExitPostWidth, 9f, 2.2f));
            LocalBox(root, "COL_Lintel", new Vector3(0f, (ExitHeight + 9f) / 2f, 0f), new Vector3(ExitOpening, 9f - ExitHeight, 2.2f));
            // 문 바깥 받침(울타리 밖 -Z): 쿠키가 걸어 들어가 탈출하는 자리. 양옆·뒤는 막는다(괴물이 맵 밖으로 나가지 않게).
            float wallX = ExitOpening / 2f + ExitPostWidth + 0.4f;
            LocalBox(root, "COL_PadL", new Vector3(-wallX, 3f, -PadDepth / 2f - 0.5f), new Vector3(0.8f, 6f, PadDepth));
            LocalBox(root, "COL_PadR", new Vector3(wallX, 3f, -PadDepth / 2f - 0.5f), new Vector3(0.8f, 6f, PadDepth));
            LocalBox(root, "COL_PadBack", new Vector3(0f, 3f, -PadDepth - 0.5f), new Vector3(wallX * 2f + 0.8f, 6f, 0.8f));
            var pad = Spawn("CF_Tile_Path", root, new Vector3(0f, 0f, -PadDepth / 2f - 0.5f), 90f, true, local: true);
            if (pad != null) pad.name = "ExitPad_" + key;
            var trigger = new GameObject("ExitTrigger_" + key).transform;
            trigger.SetParent(root, false);
            trigger.localPosition = new Vector3(0f, 1f, -4f);
        }
    }

    private static void LocalBox(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = center;
        t.gameObject.AddComponent<BoxCollider>().size = size;
    }

    // 둥근 울타리(S10): 겉모습은 울타리 문 모델을 이어 세우고, 충돌은 겹친 상자 고리(탈출구 자리만 비움 — 문 기둥이 막는다).
    private static void BuildFence(Transform map)
    {
        Transform fence = Group(map, "Fence");
        Transform cols = Group(fence, "Colliders");
        float r = CandyForestLayout.MapRadius;
        float exitHalf = ExitOpening / 2f + ExitPostWidth;
        int segments = 80;
        for (int i = 0; i < segments; i++)
        {
            float a = (i + 0.5f) * 360f / segments;
            if (NearExit(a, r, exitHalf + 3.2f)) continue;
            Vector2 p = CandyForestLayout.Polar(r + 0.4f, a);
            Spawn("CF_FenceGate", fence, new Vector3(p.x, 0f, p.y), CandyForestLayout.YawToCenter(p), true);
        }
        int colSegments = 120;
        float step = 360f / colSegments;
        for (int i = 0; i < colSegments; i++)
        {
            float a0 = i * step, a1 = a0 + step, am = a0 + step / 2f;
            // 탈출구 기둥 바깥 끝(exitHalf)까지 이어서 막는다 — 조각을 잘라 맞춘다
            float lo = a0, hi = a1;
            bool skip = false;
            for (int e = 0; e < 4; e++)
            {
                float center = e * 90f;
                float half = exitHalf / r * Mathf.Rad2Deg;
                float d0 = Mathf.DeltaAngle(center, a0), d1 = Mathf.DeltaAngle(center, a1);
                if (d0 >= -half && d1 <= half) skip = true;            // 다 들어감
                else if (d0 < -half && d1 > -half && d1 <= half) hi = a1 - (d1 + half);   // 0°/360° 넘김 없이 조각 안에서 자른다
                else if (d0 >= -half && d0 < half && d1 > half) lo = a0 + (half - d0);
            }
            if (skip) continue;
            float mid = (lo + hi) / 2f, span = Mathf.DeltaAngle(lo, hi);
            Vector2 p = CandyForestLayout.Polar(r, mid);
            var t = new GameObject($"COL_Fence_{i:000}").transform;
            t.SetParent(cols, false);
            t.SetPositionAndRotation(new Vector3(p.x, FenceHeight / 2f, p.y), Quaternion.LookRotation(new Vector3(p.x, 0f, p.y)));
            float chord = 2f * r * Mathf.Sin(span * Mathf.Deg2Rad / 2f) + 0.2f;
            t.gameObject.AddComponent<BoxCollider>().size = new Vector3(chord, FenceHeight, FenceThickness);
        }
    }

    private static bool NearExit(float deg, float radius, float halfWidth)
    {
        for (int e = 0; e < 4; e++)
            if (Mathf.Abs(Mathf.DeltaAngle(deg, e * 90f)) * Mathf.Deg2Rad * radius < halfWidth) return true;
        return false;
    }

    // ---------------------------------------------------------------- floor tiles (visual only, §28.8)

    private static int BuildTiles(Transform map, CandyForestLayoutSO layout)
    {
        Transform tiles = Group(map, "Tiles");
        int n = 0;
        float half = CandyForestLayout.RoadHalf, room = CandyForestLayout.RoomHalf;
        var buildings = layout.placements.Where(p => p.asset.StartsWith("CF_Bld_")).Select(p => new Vector2(p.position.x, p.position.z)).ToList();
        var rng = new System.Random(7);
        // 술래방: 붉은 용암 바닥판 + 용암 타일(타일의 붉은 면이 바닥판보다 위에 오게 올림 — 초록 바닥이 비치지 않게)
        var lavaFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(lavaFloor.GetComponent<Collider>());
        lavaFloor.name = "RoomLavaFloor";
        lavaFloor.transform.SetParent(tiles, false);
        lavaFloor.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        lavaFloor.transform.localScale = new Vector3(room * 2f + 0.6f, 0.02f, room * 2f + 0.6f);
        Material lava = PlainMaterial("CF_RoomLava", new Color(0.45f, 0.05f, 0.03f));
        lava.EnableKeyword("_EMISSION");
        lava.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        lava.SetColor("_EmissionColor", new Color(0.55f, 0.08f, 0.02f));
        lavaFloor.GetComponent<Renderer>().sharedMaterial = lava;
        GameObjectUtility.SetStaticEditorFlags(lavaFloor, StaticEditorFlags.BatchingStatic);
        for (int ix = 0; ix < 4; ix++)
            for (int iz = 0; iz < 3; iz++)
            { Spawn("CF_Tile_Lava", tiles, new Vector3(-8.7f + ix * 5.8f, 0.13f, -9.5f + iz * 9.5f), rng.Next(2) * 180f, true); n++; } // 타일 5.9 × 10 m(원본 비율, §28.16)
        // 큰길 포장(10 m가 길 너비, 길이 5.48 m 타일을 5.2 m씩 — 원본 비율, §28.16)
        for (int d = 0; d < 4; d++)
        {
            Vector2 dir = CandyForestLayout.RoadDir[d];
            float yaw = Mathf.Abs(dir.y) > 0.5f ? 90f : 0f;
            for (float t = room + 3f; t < CandyForestLayout.MapRadius + 1f; t += 5.2f)
            { Spawn("CF_Tile_Path", tiles, new Vector3(dir.x, 0f, dir.y) * t + Vector3.up * 0.012f, yaw + rng.Next(2) * 180f, true); n++; } // 풀밭과 겹치는 곳에서 길이 위
        }
        // 풀밭: 새 초록 바닥 모델(약 10 × 9 m, 2026-10-20) — 10% 넘게 겹침, 반쯤 180° 돌려 이음매를 숨김.
        // 큰길 밑으로 조금 들어가 길가에 틈이 없게(길 타일이 1 cm 위)
        for (float x = -84f; x <= 84f; x += 8.8f)
            for (float z = -84f; z <= 84f; z += 7.9f)
            {
                var c = new Vector2(x, z);
                if (c.magnitude > 84f) continue;
                if (Mathf.Abs(x) < half || Mathf.Abs(z) < half + 1f) continue;
                if (Mathf.Abs(x) < room + 3f && Mathf.Abs(z) < room + 5f) continue;
                if (buildings.Any(b => (b - c).magnitude < 8f)) continue;
                Spawn("CF_Tile_Moss", tiles, new Vector3(x, 0f, z), rng.Next(2) * 180f, true);
                n++;
            }
        return n;
    }

    // ---------------------------------------------------------------- markers

    private static int BuildMarkers(Transform map, CandyForestLayoutSO layout)
    {
        Transform markers = Group(map, "Markers");
        Physics.SyncTransforms();
        // 충돌 없는 꾸미기(연못·버섯·벤치 …)도 피한다
        var decor = layout.placements.Where(p => p.group != "Forest" && p.group != "Roads")
            .Select(p => (c: new Vector2(p.position.x, p.position.z), r: Footprint(p.asset).Visual * 0.8f)).ToList();
        bool Clear(Vector3 p, float radius)
        {
            var c = new Vector2(p.x, p.z);
            if (decor.Any(d => (d.c - c).magnitude < d.r + radius)) return false;
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

    private static void MoveSpawn(Scene scene, string spawnName, Transform map, string markerName)
    {
        GameObject spawn = scene.GetRootGameObjects().FirstOrDefault(g => g.name == spawnName);
        Transform marker = map.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == markerName);
        if (spawn != null && marker != null) spawn.transform.position = marker.position + Vector3.up * 0.5f;
    }

    // ---------------------------------------------------------------- lights (C10)

    // 예전 맵 점광원은 꺼 둔 채(테스트 맵과 같음), 가로등·괴물집·천막 안에 새 점광원. 재료 건물 안 빛은 프리팹에 있다.
    private static int BuildLights(Scene scene, Transform map)
    {
        GameObject lighting = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MapLighting");
        Transform old = lighting != null ? lighting.transform.Find("PointLights") : null;
        if (old != null) old.gameObject.SetActive(false);
        Transform parent = lighting != null ? lighting.transform : map;
        Transform lights = parent.Find("CF_Lights");
        if (lights != null) Object.DestroyImmediate(lights.gameObject);
        lights = new GameObject("CF_Lights").transform;
        lights.SetParent(parent, false);
        int n = 0;
        foreach (Transform t in map.GetComponentsInChildren<Transform>(true))
        {
            string asset = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) != null && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
                ? PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject).name : null;
            if (asset == null || IsUnder(t, "Forest")) continue;
            if (asset == "CF_Lamp") { Point(lights, t.position + Vector3.up * 4.6f, 12f, 1.7f, new Color(1f, 0.74f, 0.55f)); n++; }
            else if (asset == "CF_MonsterHouse" || asset == "CF_Tent") { Point(lights, t.position + Vector3.up * 5f, 15f, 1.4f, new Color(1f, 0.55f, 0.65f)); n++; }
        }
        DarkReport = CheckDarkInteriors(map, parent);
        return n;
    }

    public static string DarkReport { get; private set; }

    // 어두운 곳 검사(§28 C10): 지붕이 덮인 곳(재료 건물·괴물집·천막 안)의 바닥 2 m 격자마다 점광원 범위의 60% 안인지.
    // 바깥은 해(방향광)와 주변광이 비춘다.
    private const float LitCoverage = 0.6f;

    private static string CheckDarkInteriors(Transform map, Transform lightRoot)
    {
        var lights = lightRoot.GetComponentsInChildren<Light>(true).Concat(map.GetComponentsInChildren<Light>(true))
            .Where(l => l.type == LightType.Point && l.gameObject.activeInHierarchy).Distinct().ToList();
        int checkedCells = 0, dark = 0;
        var where = new List<string>();
        foreach (Transform t in map.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            if (source == null) continue;
            float radius = source.name.StartsWith("CF_Bld_") ? 7f : source.name == "CF_MonsterHouse" ? 6.5f : source.name == "CF_Tent" ? 5f : 0f;
            if (radius <= 0f) continue;
            for (float x = -radius; x <= radius; x += 2f)
                for (float z = -radius; z <= radius; z += 2f)
                {
                    if (x * x + z * z > radius * radius) continue;
                    Vector3 p = t.TransformPoint(new Vector3(x, 1f, z));
                    checkedCells++;
                    if (lights.Any(l => (l.transform.position - p).magnitude < l.range * LitCoverage)) continue;
                    dark++;
                    if (where.Count < 5) where.Add($"{t.parent.name}/{t.name} {p:F0}");
                }
        }
        return $"interior cells {checkedCells}, dark {dark}" + (where.Count > 0 ? ": " + string.Join(", ", where) : "");
    }

    private static bool IsUnder(Transform t, string name)
    {
        for (Transform p = t; p != null; p = p.parent) if (p.name == name) return true;
        return false;
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

    private static Material PlainMaterial(string name, Color c)
    {
        string path = $"{CandyForestAssets.MaterialDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = c;
        m.SetFloat("_Glossiness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- passability (C9)

    public const string PassabilityReport = "Temp/CandyForest_Passability.txt";

    // 몸 크기 5가지(지금 쿠키 + 새 캐릭터 범위 네 모서리, §28.10)로 통행 검사. 술래방은 괴물 전용(S11), 울타리 밖은 놀이 공간이 아님.
    // 상자·수리 자리·탈출구·로켓·무지개·스폰 자리는 쿠키·괴물이 모두 3 m 안까지 와야 한다(완료 기준 2).
    [MenuItem(MenuRoot + "3 Check Passability (5 body sizes)")]
    public static void CheckPassabilityMenu() => Debug.Log(CheckPassability());

    public static string CheckPassability()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        float room = CandyForestLayout.RoomHalf + 0.3f;
        var options = new MapPassabilityCheck.Options
        {
            Half = CandyForestLayout.MapRadius + 12f,
            Bodies = MapPassabilityCheck.DesignRange,
            CookieForbidden = p => Mathf.Abs(p.x) < room && Mathf.Abs(p.z) < room,
            IgnorePocket = p => new Vector2(p.x, p.z).magnitude > CandyForestLayout.MapRadius + 0.5f,
            Targets = Targets(scene).ToArray(),
            // 술래방 문(대각선 네 곳) 바로 바깥 — 괴물은 문을 지나 나온다
            MonsterSeeds = Enumerable.Range(0, 4).Select(i => new Vector3(i % 2 == 0 ? -1f : 1f, 0f, i < 2 ? 1f : -1f) * 12.5f).ToArray(),
            MapImage = "Temp/CandyForest_PassabilityMap.png",
        };
        MapPassabilityCheck.Result[] results = MapPassabilityCheck.Run(scene, options);
        var sb = new System.Text.StringBuilder();
        foreach (MapPassabilityCheck.Result r in results) sb.Append(r.Clean ? "CLEAN " : "ISSUES ").Append(r);
        System.IO.Directory.CreateDirectory("Temp");
        System.IO.File.WriteAllText(PassabilityReport, sb.ToString());
        return $"[CandyForestMap] passability: {results.Count(r => r.Clean)}/{results.Length} clean — {PassabilityReport}";
    }

    private static IEnumerable<(string, Vector3)> Targets(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("CHEST_Slot_") || n.StartsWith("RocketSpawn_") || n.StartsWith("RainbowSpawn_") || n.StartsWith("Spawn_Cookie_")
                    || n.StartsWith("Repair_Point") || n.StartsWith("ExitTrigger_"))
                    yield return (n + (n.StartsWith("Repair_Point") ? "@" + t.parent.parent.parent.name : ""), t.position);
            }
    }

    // ---------------------------------------------------------------- build settings

    // CandyForest 씬을 빌드에 넣고, 판마다 고르는 맵 목록에서 Test_CandyForest를 바꾼다(§28.11 Q11 — 확인 뒤 교체).
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
}
