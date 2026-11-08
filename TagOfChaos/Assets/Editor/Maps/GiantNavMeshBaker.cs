using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// 거대 괴물이 걷는 NavMesh를 맵마다 굽는다(Request1009Plan.md §64.4 GM3).
// - 거대 괴물 몸(GiantNavMesh.Agent*, 에이전트 종류 "Giant")으로 물리 충돌체를 모아 굽는다.
// - 부서지지 않는 것(술래방·탈출구·시계탑·동굴 벽·바닥 등)만 막는다. 부서지는 물체(소품·텐트·건물 — DestructibleCatalog, §66.4)는
//   빼서 가로질러 걷게 한다(가는 길에 서 있으면 GiantBrain이 멈춰 밟아 부순다). 작은 충돌체(PropFootprint 미만)도 뺀다.
//   문 잎·트리거·캐릭터 몸도 뺀다.
// - 결과는 Assets/Maps/GiantNavMesh/<씬>.asset, 씬 맨 위 "GiantNavMesh" 오브젝트가 들고 있다.
// - 확인 그림(위에서 본 층별 지도): 초록 = 거대 괴물이 걸어서 가는 곳, 노랑 = 거대 괴물 길이지만 따로 떨어진 구역(마법진 이동),
//   빨강 = 쿠키 바닥인데 거대 괴물이 못 가는 곳(건물 안·좁은 길 — 문 앞에서 기다림/마법진).
// 씬은 덧붙여 열어(Additive) 굽고 저장한 뒤 닫는다 — 지금 열린 씬은 건드리지 않는다.
public static class GiantNavMeshBaker
{
    public const string AssetFolder = "Assets/Maps/GiantNavMesh";
    public const string RootName = "GiantNavMesh";
    private const float MapStep = 2f;

    public static string LastReport { get; private set; } = "";

    [MenuItem("Tools/Maps/Bake Giant NavMesh (All Maps)")]
    public static void BakeAllMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[GiantNavMesh] Stop Play Mode first."); return; }
        Debug.Log(BakeAll(null));
    }

    // 모든 맵(DecoyNavMeshBaker.MapScenes)을 굽는다. pictureFolder가 있으면 확인 그림을 남긴다.
    public static string BakeAll(string pictureFolder)
    {
        var sb = new StringBuilder();
        foreach (string path in DecoyNavMeshBaker.MapScenes) sb.AppendLine(BakePath(path, pictureFolder));
        LastReport = sb.ToString();
        return LastReport;
    }

    public static string BakePath(string scenePath, string pictureFolder)
    {
        Scene open = SceneManager.GetSceneByPath(scenePath);
        bool wasOpen = open.IsValid() && open.isLoaded;
        if (wasOpen && open.isDirty) return $"[GiantNavMesh] {scenePath}: open with unsaved changes — skipped.";
        Scene scene = wasOpen ? open : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        string line;
        try
        {
            line = BakeScene(scene, pictureFolder);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }
        return line;
    }

    public static string BakeScene(Scene scene, string pictureFolder)
    {
        float started = (float)EditorApplication.timeSinceStartup;
        if (!GiantNavMesh.HasAgentType) return "[GiantNavMesh] agent type 'Giant' missing (ProjectSettings/NavMeshAreas).";
        EscapeManager manager = FindIn<EscapeManager>(scene);
        if (manager == null) return $"[GiantNavMesh] {scene.name}: no EscapeManager — skipped.";
        MapArea area = DecoyNavMeshBaker.AreaOf(manager, scene);

        var bounds = new Bounds(new Vector3(area.Center.x, -7.5f, area.Center.y), new Vector3(area.Rect.width + 8f, 145f, area.Rect.height + 8f));
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        sources.RemoveAll(s => !InScene(s, scene));
        var destructible = new HashSet<Collider>();
        foreach (DestructibleUnit u in DestructibleCatalog.Build(DestructibleCatalog.FindMap(scene), DestructionRulesSO.Current).Units)
            if (u.Grade != DestructibleGrade.Stairs) foreach (Collider c in u.Colliders) destructible.Add(c); // 계단은 거대 괴물을 막는다(위에서 밟아 부숨)
        int props = sources.RemoveAll(s => IsPassable(s) || (s.component is Collider col && destructible.Contains(col)));

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(Settings(), sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null) return $"[GiantNavMesh] {scene.name}: build failed.";
        data.name = scene.name + "_GiantNavMesh";
        string assetPath = SaveAsset(data, scene.name);
        data = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);

        GameObject root = FindRoot(scene, RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        GiantNavMesh holder = root.GetComponent<GiantNavMesh>();
        if (holder == null) holder = root.AddComponent<GiantNavMesh>();
        holder.EditorSetData(data);
        EditorUtility.SetDirty(holder);
        EditorSceneManager.MarkSceneDirty(scene);

        string check = "";
        if (pictureFolder != null)
        {
            NavMeshData cookie = FindIn<DecoyNavMesh>(scene) != null ? FindIn<DecoyNavMesh>(scene).Data : null;
            NavMesh.RemoveAllNavMeshData();
            NavMeshDataInstance live = NavMesh.AddNavMeshData(data);
            NavMeshDataInstance cookieLive = cookie != null ? NavMesh.AddNavMeshData(cookie) : default;
            check = Picture(scene, manager, area, pictureFolder);
            live.Remove();
            if (cookieLive.valid) cookieLive.Remove();
        }
        long kb = new FileInfo(assetPath).Length / 1024;
        return $"[GiantNavMesh] {scene.name}: sources={sources.Count} (props passed {props}) asset={kb} KB{check} in {(float)EditorApplication.timeSinceStartup - started:F0}s";
    }

    public static NavMeshBuildSettings Settings()
    {
        NavMeshBuildSettings s = NavMesh.GetSettingsByID(GiantNavMesh.AgentTypeId);
        s.agentRadius = GiantNavMesh.AgentRadius;
        s.agentHeight = GiantNavMesh.AgentHeight;
        s.agentClimb = GiantNavMesh.AgentClimb;
        s.agentSlope = GiantNavMesh.AgentSlope;
        s.minRegionArea = 30f;
        return s;
    }

    // 문 잎·트리거·캐릭터 몸, 작은 소품은 거대 괴물을 막지 않는다.
    public static bool IsPassable(NavMeshBuildSource s)
    {
        var c = s.component as Collider;
        if (c == null) return false;
        if (c.isTrigger || c.GetComponentInParent<InteractableDoor>() != null || c.GetComponentInParent<IGameCharacter>() != null) return true;
        Vector3 size = c.bounds.size;
        return size.x < GiantNavMesh.PropFootprint && size.z < GiantNavMesh.PropFootprint && size.y < GiantNavMesh.PropHeight;
    }

    private static bool InScene(NavMeshBuildSource s, Scene scene) => s.component == null || s.component.gameObject.scene == scene;

    // 층마다 위에서 본 지도(2 m 칸)와 비율. 층: 쿠키 스폰 높이, 그보다 20 m 넘게 낮은 쿠키 바닥이 있으면 그 층(진저브레드 지하 −36 m).
    private static string Picture(Scene scene, EscapeManager manager, MapArea area, string folder)
    {
        Directory.CreateDirectory(folder);
        GameObject spawn = FindNamed(scene, SceneSpawnPoints.Cookie);
        float ground = spawn != null ? spawn.transform.position.y : 0f;
        var floors = new List<float> { ground };
        if (NavMesh.SamplePosition(new Vector3(area.Center.x, ground - 36f, area.Center.y), out NavMeshHit under, 40f, NavMesh.AllAreas) && under.position.y < ground - 20f)
            floors.Add(under.position.y);

        var sb = new StringBuilder();
        var giant = new NavMeshQueryFilter { agentTypeID = GiantNavMesh.AgentTypeId, areaMask = NavMesh.AllAreas };
        var cookie = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        var path = new NavMeshPath();
        int w = Mathf.CeilToInt(area.Rect.width / MapStep), h = Mathf.CeilToInt(area.Rect.height / MapStep);
        for (int f = 0; f < floors.Count; f++)
        {
            float y = floors[f];
            // 거대 괴물 길 칸을 이어진 구역끼리 묶는다(구역 대표점까지 길이 있으면 같은 구역). 가장 넓은 구역 = 걸어서 다니는 곳.
            var cells = new List<(int i, int j, Vector3 p)>();
            var reps = new List<Vector3>();
            var counts = new List<int>();
            var comp = new Dictionary<int, int>();
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            int floorCells = 0, blocked = 0;
            for (int i = 0; i < w; i++)
            for (int j = 0; j < h; j++)
            {
                var p = new Vector3(area.Rect.xMin + (i + 0.5f) * MapStep, y, area.Rect.yMin + (j + 0.5f) * MapStep);
                bool outside = area.IsOutside(p, 0f);
                Color col = outside ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.22f, 0.22f, 0.25f);
                bool cookieFloor = NavMesh.SamplePosition(p, out NavMeshHit ch, 1.2f, cookie) && Mathf.Abs(ch.position.y - y) < 6f;
                NavMeshHit gh = default;
                bool giantFloor = !outside && NavMesh.SamplePosition(p, out gh, 1.2f, giant) && Mathf.Abs(gh.position.y - y) < 6f;
                if (giantFloor)
                {
                    int k = -1;
                    for (int r = 0; r < reps.Count && k < 0; r++)
                        if (NavMesh.CalculatePath(gh.position, reps[r], giant, path) && path.status == NavMeshPathStatus.PathComplete) k = r;
                    if (k < 0) { k = reps.Count; reps.Add(gh.position); counts.Add(0); }
                    counts[k]++;
                    comp[i * h + j] = k;
                }
                else if (cookieFloor && !outside) { col = new Color(0.85f, 0.25f, 0.2f); blocked++; }
                if (!outside && (cookieFloor || giantFloor)) floorCells++;
                tex.SetPixel(i, j, col);
            }
            int main = 0;
            for (int k = 1; k < counts.Count; k++) if (counts[k] > counts[main]) main = k;
            int walk = 0, island = 0;
            foreach (var kv in comp)
            {
                bool m = kv.Value == main;
                if (m) walk++; else island++;
                tex.SetPixel(kv.Key / h, kv.Key % h, m ? new Color(0.25f, 0.8f, 0.3f) : new Color(0.95f, 0.8f, 0.2f));
            }
            tex.Apply();
            string file = Path.Combine(folder, $"giant_{scene.name}_floor{f}.png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            sb.Append($" | floor{f} y={y:F0}: walk {walk * MapStep * MapStep:F0} m² islands {counts.Count - 1} ({island * MapStep * MapStep:F0} m²) blocked {blocked * MapStep * MapStep:F0} m² ({(floorCells > 0 ? 100f * walk / floorCells : 0f):F0}% walkable)");
        }
        return sb.ToString();
    }

    private static string SaveAsset(NavMeshData data, string sceneName)
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/Maps", "GiantNavMesh");
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

    private static GameObject FindNamed(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
}
