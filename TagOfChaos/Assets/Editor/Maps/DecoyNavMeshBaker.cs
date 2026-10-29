using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// AI(미끼)가 다닐 NavMesh를 맵마다 굽는다(Request1009Plan.md §44.7 AI0).
// - 쿠키 몸 크기(DecoyNavMesh.Agent*)로 물리 충돌체를 모아 굽는다. 문 잎(InteractableDoor 아래 충돌체)은 빼서 열린 것처럼 둔다.
//   트리거·캐릭터 몸도 뺀다. 술래방은 걷지 못하는 곳(영역 1)으로 덮는다.
// - 결과는 Assets/Maps/DecoyNavMesh/<씬>.asset, 씬 맨 위 "DecoyNavMesh" 오브젝트가 들고 있다.
// - 숨을 곳(창고·텐트)에 DecoyHideSpot을 붙이고 안쪽 자리(NavMesh 위)를 채운다.
// - 확인: 상자 자리·건물 수리 자리·숨을 곳이 NavMesh 위인지, 걸을 수 있는 바닥(4 m 격자)의 몇 %가 NavMesh인지 로그로 남긴다.
// MapSceneBuilder·MapCompactor는 쓰지 않는다 — 이 도구는 씬에 NavMesh와 표시만 더한다.
public static class DecoyNavMeshBaker
{
    public const string AssetFolder = "Assets/Maps/DecoyNavMesh";
    public const string RootName = "DecoyNavMesh";
    public static readonly string[] MapScenes =
    {
        "Assets/Scenes/Maps/CandyForest.unity",
        "Assets/Scenes/Maps/CursedCandyCarnival.unity",
        "Assets/Scenes/Maps/ChocolateFactory.unity",
        "Assets/Scenes/Maps/Test/Test_GingerbreadVillage.unity",
        "Assets/Scenes/Maps/Test/Test_HauntedBakery.unity",
    };
    public static readonly string[] HidePrefixes = { "CHO_Shed", "MO_Tent", "CF_Tent" };
    public const float PointReach = 2.5f;      // 확인 점에서 NavMesh까지 허용 거리
    private const float CoverageStep = 4f;
    private const int NotWalkableArea = 1;
    private static readonly Vector3[] HideOffsets = { Vector3.zero, new Vector3(0f, 0f, 1.6f), new Vector3(0f, 0f, -1.6f), new Vector3(1.4f, 0f, 0f), new Vector3(-1.4f, 0f, 0f) };

    public static string LastReport { get; private set; } = "";

    [MenuItem("Tools/Maps/Bake Decoy NavMesh (All Maps)")]
    public static void BakeAllMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[DecoyNavMesh] Stop Play Mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BakeAll();
    }

    [MenuItem("Tools/Maps/Bake Decoy NavMesh (Open Scene)")]
    public static void BakeOpenMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[DecoyNavMesh] Stop Play Mode first."); return; }
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log(BakeScene(scene));
        EditorSceneManager.SaveScene(scene);
    }

    public static string BakeAll()
    {
        string back = SceneManager.GetActiveScene().path;
        var sb = new StringBuilder();
        foreach (string path in MapScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string line = BakeScene(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(line);
            sb.AppendLine(line);
        }
        if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        LastReport = sb.ToString();
        return LastReport;
    }

    // 한 씬을 굽고 확인 결과 한 줄을 돌려준다.
    public static string BakeScene(Scene scene)
    {
        float started = (float)EditorApplication.timeSinceStartup;
        EscapeManager manager = FindIn<EscapeManager>(scene);
        if (manager == null) return $"[DecoyNavMesh] {scene.name}: no EscapeManager — skipped.";
        MapArea area = AreaOf(manager, scene);
        Transform room = new SerializedObject(manager).FindProperty("monsterRoom").objectReferenceValue as Transform;
        float roomHalf = new SerializedObject(manager).FindProperty("monsterRoomHalfSize").floatValue;

        var bounds = new Bounds(new Vector3(area.Center.x, 20f, area.Center.y), new Vector3(area.Rect.width + 8f, 90f, area.Rect.height + 8f));
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        int removed = sources.RemoveAll(IsExcluded);
        if (room != null)
        {
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                transform = Matrix4x4.TRS(room.position, Quaternion.identity, Vector3.one),
                size = new Vector3(roomHalf * 2f + 1f, 60f, roomHalf * 2f + 1f),
                area = NotWalkableArea,
            });
        }

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(Settings(), sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null) return $"[DecoyNavMesh] {scene.name}: build failed.";
        data.name = scene.name + "_DecoyNavMesh";
        string assetPath = SaveAsset(data, scene.name);
        data = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);

        GameObject root = FindRoot(scene, RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        DecoyNavMesh holder = root.GetComponent<DecoyNavMesh>();
        if (holder == null) holder = root.AddComponent<DecoyNavMesh>();
        holder.EditorSetData(data);
        EditorUtility.SetDirty(holder);

        // 확인과 숨을 곳 자리는 방금 구운 NavMesh로 계산한다(에디터에서 잠깐 올렸다 내린다).
        NavMesh.RemoveAllNavMeshData();
        NavMeshDataInstance live = NavMesh.AddNavMeshData(data);
        string hides = MarkHideSpots(scene, out int hideCount, out int hideMissing);
        string checks = CheckPoints(manager, out int missing);
        float coverage = Coverage(area, manager);
        live.Remove();
        EditorSceneManager.MarkSceneDirty(scene);

        long kb = new FileInfo(assetPath).Length / 1024;
        return $"[DecoyNavMesh] {scene.name}: sources={sources.Count} (excluded {removed}) asset={kb} KB hideSpots={hideCount} (no spot {hideMissing}) " +
               $"pointsOffMesh={missing}{checks} coverage={coverage:P0}{hides} in {(float)EditorApplication.timeSinceStartup - started:F0}s";
    }

    public static NavMeshBuildSettings Settings()
    {
        NavMeshBuildSettings s = NavMesh.GetSettingsByID(0);
        s.agentRadius = DecoyNavMesh.AgentRadius;
        s.agentHeight = DecoyNavMesh.AgentHeight;
        s.agentClimb = DecoyNavMesh.AgentClimb;
        s.agentSlope = DecoyNavMesh.AgentSlope;
        s.minRegionArea = 4f;
        return s;
    }

    // 문 잎(열린 것처럼)·트리거·캐릭터 몸은 길을 막지 않는다.
    private static bool IsExcluded(NavMeshBuildSource s)
    {
        var c = s.component as Collider;
        if (c == null) return false;
        if (c.isTrigger) return true;
        return c.GetComponentInParent<InteractableDoor>() != null || c.GetComponentInParent<IGameCharacter>() != null;
    }

    public static MapArea AreaOf(EscapeManager manager, Scene scene)
    {
        float radius = new SerializedObject(manager).FindProperty("mapRadius").floatValue;
        if (radius > 0f) return new MapArea(Vector2.zero, radius);
        return new MapArea(WitchPath.TryGetGroundRect(scene, out Rect ground) ? ground : new Rect(-70f, -70f, 140f, 140f));
    }

    private static string SaveAsset(NavMeshData data, string sceneName)
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/Maps", "DecoyNavMesh");
        string path = $"{AssetFolder}/{sceneName}.asset";
        var old = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if (old != null)
        {
            EditorUtility.CopySerialized(data, old); // 같은 에셋에 덮어써 씬의 참조(GUID)를 지킨다
            EditorUtility.SetDirty(old);
            AssetDatabase.SaveAssets();
        }
        else AssetDatabase.CreateAsset(data, path);
        return path;
    }

    // 창고·텐트 뿌리마다 DecoyHideSpot을 붙이고, 안쪽 후보 자리 중 NavMesh 위이고 몸 안쪽(수평)인 곳을 최대 3개 채운다.
    private static string MarkHideSpots(Scene scene, out int count, out int missing)
    {
        count = 0;
        missing = 0;
        var perPrefix = new Dictionary<string, int>();
        foreach (GameObject top in scene.GetRootGameObjects())
        foreach (Transform t in top.GetComponentsInChildren<Transform>(true))
        {
            string prefix = HidePrefixOf(t.name);
            if (prefix == null || (t.parent != null && HidePrefixOf(t.parent.name) != null)) continue;
            Bounds b = RendererBounds(t);
            var spots = new List<Vector3>();
            foreach (Vector3 offset in HideOffsets)
            {
                Vector3 p = t.TransformPoint(offset);
                if (!NavMesh.SamplePosition(new Vector3(p.x, t.position.y + 0.5f, p.z), out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.x - b.center.x) > b.extents.x * 0.8f || Mathf.Abs(hit.position.z - b.center.z) > b.extents.z * 0.8f) continue;
                spots.Add(hit.position);
                if (spots.Count >= 3) break;
            }
            DecoyHideSpot spot = t.GetComponent<DecoyHideSpot>();
            if (spot == null) spot = t.gameObject.AddComponent<DecoyHideSpot>();
            spot.EditorSetSpots(spots.ToArray());
            EditorUtility.SetDirty(spot);
            if (spots.Count == 0) missing++;
            count++;
            perPrefix[prefix] = perPrefix.TryGetValue(prefix, out int n) ? n + 1 : 1;
        }
        var sb = new StringBuilder();
        foreach (var kv in perPrefix) sb.Append($" {kv.Key}x{kv.Value}");
        return sb.ToString();
    }

    public static string HidePrefixOf(string name)
    {
        foreach (string p in HidePrefixes) if (name.StartsWith(p)) return p;
        return null;
    }

    private static Bounds RendererBounds(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 4f);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // 상자 자리·건물 수리 자리가 NavMesh에서 PointReach 안인지. 빠진 것은 이름을 붙여 돌려준다.
    private static string CheckPoints(EscapeManager manager, out int missing)
    {
        missing = 0;
        var sb = new StringBuilder();
        var so = new SerializedObject(manager);
        SerializedProperty chests = so.FindProperty("chestAnchors");
        for (int i = 0; i < chests.arraySize; i++)
        {
            var t = chests.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (t != null && !OnMesh(t.position)) { missing++; sb.Append($" chest{i}"); }
        }
        SerializedProperty buildings = so.FindProperty("buildings");
        for (int i = 0; i < buildings.arraySize; i++)
        {
            var b = buildings.GetArrayElementAtIndex(i).objectReferenceValue as QuadrantBuilding;
            if (b != null && !OnMesh(b.RepairPoint)) { missing++; sb.Append($" building{i}"); }
        }
        return sb.Length > 0 ? " (" + sb.ToString().Trim() + ")" : "";
    }

    public static bool OnMesh(Vector3 p) => NavMesh.SamplePosition(p, out _, PointReach, NavMesh.AllAreas);

    // 맵 안 4 m 격자에서 "쿠키가 설 수 있는 바닥"(아래로 쏴 맞은 면이 평평하고 몸이 끼지 않음, 술래방 밖) 중 NavMesh 위인 비율.
    private static float Coverage(MapArea area, EscapeManager manager)
    {
        int floor = 0, onMesh = 0;
        for (float x = area.Rect.xMin + 2f; x < area.Rect.xMax; x += CoverageStep)
        for (float z = area.Rect.yMin + 2f; z < area.Rect.yMax; z += CoverageStep)
        {
            var p = new Vector3(x, 0f, z);
            if (area.IsOutside(p, -3f) || manager.InMonsterRoom(p)) continue;
            if (!Physics.Raycast(new Vector3(x, 60f, z), Vector3.down, out RaycastHit hit, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y < 0.8f || hit.collider.GetComponentInParent<InteractableDoor>() != null) continue;
            Vector3 feet = hit.point;
            if (Physics.CheckCapsule(feet + Vector3.up * 0.6f, feet + Vector3.up * 1.5f, DecoyNavMesh.AgentRadius + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            floor++;
            if (NavMesh.SamplePosition(feet, out NavMeshHit nh, 1f, NavMesh.AllAreas) && Mathf.Abs(nh.position.y - feet.y) < 0.8f) onMesh++;
        }
        return floor > 0 ? onMesh / (float)floor : 0f;
    }

    private static T FindIn<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T c = root.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }
}
