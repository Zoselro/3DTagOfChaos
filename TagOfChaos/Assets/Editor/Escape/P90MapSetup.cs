using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;

// Request1009Plan.md §90 맵 배치(다시 실행해도 같은 결과 — 전에 놓은 것을 지우고 다시 놓는다):
// T2 술래 벽장(재료 건물 4채 × 5맵 = 20곳): 건물 안 빈 벽에 등을 대고 방을 보게. 술래(키 6.5 m)가 설 높이(8.3 m)가 안 되면 바닥 뚜껑.
public static class P90MapSetup
{
    public const string PrefabDir = "Assets/04. Prefabs/Escape/P90";
    public const string ClosetPrefab = PrefabDir + "/MonsterCloset.prefab";
    public const string HatchPrefab = PrefabDir + "/MonsterHatch.prefab";
    public static readonly string[] Maps = { "CandyForest", "CursedCandyCarnival", "ChocolateFactory", "GingerbreadVillage", "HeavenOfBakery" };

    private static readonly Vector3 ClosetHalf = new Vector3(4.3f, 4.1f, 1.5f);   // 8.6 × 8.2 × 3.0 m
    private const float ClosetBackZ = -1.45f;                                    // 뿌리에서 등판까지
    private static readonly Vector3 ExitHalf = new Vector3(2.4f, 3.3f, 2.2f);     // 앞 나오는 자리(술래 몸)
    private const float HatchClear = 6.9f;                                        // 뚜껑 위 서는 높이

    [MenuItem("Tools/TagOfChaos/Escape/§90 Build Closet Prefabs")]
    public static string Prefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/04. Prefabs/Escape")) AssetDatabase.CreateFolder("Assets/04. Prefabs", "Escape");
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/04. Prefabs/Escape", "P90");
        string a = Build(ClosetPrefab, P90AssetSetup.ItemDir + "/SK_MonsterCloset.fbx", MonsterCloset.Kind.Closet, "Door_L", "Door_R", new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0f, 3.6f));
        string b = Build(HatchPrefab, P90AssetSetup.ItemDir + "/SK_MonsterHatch.fbx", MonsterCloset.Kind.Hatch, "Leaf_L", "Leaf_R", new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0f, 3.4f));
        return a + " " + b;
    }

    private static string Build(string path, string fbx, MonsterCloset.Kind kind, string left, string right, Vector3 stand, Vector3 exit)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (model == null) return "missing " + fbx;
        var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        try
        {
            GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            PrefabUtility.UnpackPrefabInstance(m, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform l = m.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == left);
            Transform r = m.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == right);
            if (kind == MonsterCloset.Kind.Closet)
            {
                // 몸통만 막는다(문은 열려야 하니 충돌 없음). 몸통 = 모델 뿌리 메시
                MeshFilter body = m.GetComponent<MeshFilter>();
                if (body != null) m.AddComponent<MeshCollider>().sharedMesh = body.sharedMesh;
            }
            var st = new GameObject("Stand").transform; st.SetParent(root.transform, false); st.localPosition = stand;
            var ex = new GameObject("Exit").transform; ex.SetParent(root.transform, false); ex.localPosition = exit;
            root.AddComponent<MonsterCloset>().EditorSetup(kind, st, ex, l, r);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return System.IO.Path.GetFileName(path) + (l != null && r != null ? " ok" : " (doors missing)");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [MenuItem("Tools/TagOfChaos/Escape/§90 Place Monster Closets (all maps)")]
    public static string PlaceAll()
    {
        var log = new List<string>();
        Prefabs();
        foreach (string map in Maps)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/Maps/{map}.unity", OpenSceneMode.Single);
            log.Add(map + ": " + PlaceInScene());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        string s = "[P90MapSetup] " + string.Join(" | ", log);
        Debug.Log(s);
        return s;
    }

    public static string PlaceInScene()
    {
        foreach (MonsterCloset old in Object.FindObjectsByType<MonsterCloset>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
        Physics.SyncTransforms();
        var parts = new List<string>();
        foreach (QuadrantBuilding q in Object.FindObjectsByType<QuadrantBuilding>(FindObjectsSortMode.None).OrderBy(b => b.name))
        {
            bool closet = TryCloset(q, out Vector3 pos, out Quaternion rot);
            if (!closet && !TryHatch(q, out pos, out rot)) { parts.Add(q.name + " none"); continue; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(closet ? ClosetPrefab : HatchPrefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, q.transform);
            go.name = "MonsterCloset_" + q.name.Replace("Building_", "");
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = new Vector3(1f / q.transform.lossyScale.x, 1f / q.transform.lossyScale.y, 1f / q.transform.lossyScale.z);
            Physics.SyncTransforms();
            parts.Add($"{q.name} {(closet ? "closet" : "hatch")} {Vector3.Distance(pos, q.RepairPoint):F1}m");
        }
        return string.Join(", ", parts);
    }

    private static float FloorAt(Vector3 p, float from = 3f) =>
        Physics.Raycast(p + Vector3.up * from, Vector3.down, out RaycastHit h, from + 6f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : float.NaN;

    private static bool Covered(Vector3 floor, float height) =>
        Physics.Raycast(floor + Vector3.up * 0.3f, Vector3.up, out RaycastHit h, 60f, ~0, QueryTriggerInteraction.Ignore) && h.distance + 0.3f >= height;

    private static bool Inside(QuadrantBuilding q, Vector3 p)
    {
        var b = new Bounds(q.transform.position, Vector3.zero);
        foreach (Renderer r in q.GetComponentsInChildren<Renderer>()) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
        b.Expand(new Vector3(-2f, 0f, -2f));
        return p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z;
    }

    private static bool Free(Vector3 center, Vector3 half, Quaternion rot) =>
        !Physics.CheckBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore);

    // 벽장: 수리 자리 둘레(4~16 m)를 1 m 간격으로, 8방향으로 돌려 보며 — 몸이 빈 곳, 등 뒤 1.2 m 안에 벽, 앞에 술래가 나올 빈 곳, 위가 지붕(건물 안).
    private static bool TryCloset(QuadrantBuilding q, out Vector3 best, out Quaternion bestRot)
    {
        best = default; bestRot = Quaternion.identity;
        Vector3 rp = q.RepairPoint;
        float floorRp = rp.y;
        float bestScore = float.MaxValue;
        for (float x = -16f; x <= 16f; x += 1f)
            for (float z = -16f; z <= 16f; z += 1f)
            {
                float dist = new Vector2(x, z).magnitude;
                if (dist < 4f || dist > 16f) continue;
                Vector3 p = new Vector3(rp.x + x, floorRp + 0.5f, rp.z + z);
                float fy = FloorAt(p, 2f);
                if (float.IsNaN(fy) || Mathf.Abs(fy - rp.y) > 3f) continue;   // 같은 층(수리 장치 윗면이 아니라 바닥)
                Vector3 f = new Vector3(p.x, fy, p.z);
                if (!Inside(q, f)) continue;                                      // 건물 안(지붕 충돌체가 없는 건물도 있다)
                if (Covered(f, 0.1f) && !Covered(f, ClosetHalf.y * 2f + 0.15f)) continue;   // 천장이 있으면 8.3 m 이상
                for (int a = 0; a < 360; a += 45)
                {
                    Quaternion rot = Quaternion.Euler(0f, a, 0f);
                    Vector3 fwd = rot * Vector3.forward;
                    if (!Free(f + Vector3.up * (ClosetHalf.y + 0.15f), ClosetHalf - new Vector3(0.05f, 0.1f, 0.05f), rot)) continue;
                    Vector3 back = f + Vector3.up * 3f + fwd * ClosetBackZ;
                    if (!Physics.Raycast(back, -fwd, out RaycastHit wall, 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;   // 등을 벽에
                    Vector3 exitC = f + fwd * (ClosetHalf.z + ExitHalf.z + 0.3f) + Vector3.up * (ExitHalf.y + 0.2f);
                    if (!Free(exitC, ExitHalf, rot)) continue;
                    if (Vector3.Dot(fwd, (rp - f).normalized) < 0.2f) continue;                   // 방(수리 자리)을 본다
                    float score = dist + wall.distance * 2f;
                    if (score < bestScore) { bestScore = score; best = f - fwd * Mathf.Max(0f, wall.distance - 0.15f); bestRot = rot; }   // 등판을 벽에 붙인다
                }
            }
        return bestScore < float.MaxValue;
    }

    // 바닥 뚜껑: 수리 자리 둘레(3~22 m)에서 5 × 5 m가 평평하고 위로 6.9 m 빈 곳(실내가 낮으면 문 밖 바닥도).
    private static bool TryHatch(QuadrantBuilding q, out Vector3 best, out Quaternion bestRot)
    {
        best = default; bestRot = Quaternion.identity;
        Vector3 rp = q.RepairPoint;
        float floorRp = rp.y;
        float bestScore = float.MaxValue;
        for (float x = -22f; x <= 22f; x += 1f)
            for (float z = -22f; z <= 22f; z += 1f)
            {
                float dist = new Vector2(x, z).magnitude;
                if (dist < 3f || dist > 22f) continue;
                Vector3 p = new Vector3(rp.x + x, floorRp + 1f, rp.z + z);
                float fy = FloorAt(p, 2f);
                if (float.IsNaN(fy) || Mathf.Abs(fy - floorRp) > 3f) continue;
                Vector3 f = new Vector3(p.x, fy, p.z);
                bool flat = true;
                foreach (Vector2 o in new[] { new Vector2(2.4f, 2.4f), new Vector2(-2.4f, 2.4f), new Vector2(2.4f, -2.4f), new Vector2(-2.4f, -2.4f) })
                {
                    float y = FloorAt(f + new Vector3(o.x, 0.5f, o.y), 1f);
                    if (float.IsNaN(y) || Mathf.Abs(y - fy) > 0.35f) { flat = false; break; }
                }
                if (!flat || !Free(f + Vector3.up * (HatchClear * 0.5f + 0.45f), new Vector3(2.5f, HatchClear * 0.5f, 2.5f), Quaternion.identity)) continue;
                Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(rp - f, Vector3.up).normalized);
                float score = dist + (Inside(q, f) ? 0f : 8f);   // 건물 안을 먼저
                if (score < bestScore) { bestScore = score; best = f + Vector3.up * 0.01f; bestRot = rot; }
            }
        return bestScore < float.MaxValue;
    }

    // ================= Heaven Of Bakery(U1 섬 밑면·V7 바나나 나무·비닐하우스·과일·W1 낙하 표지판) =================

    public const string FarmPrefabDir = "Assets/04. Prefabs/Escape/P90/Farm";
    // private static readonly string[] FarmModels = { "SK_BananaTree_A", "SK_BananaTree_B", "SK_Watermelon", "SK_Grape", "SK_Apple", "SK_Strawberry" };
    private static readonly string[] FarmModels = { "SK_BananaTree_A", "SK_BananaTree_B", "SK_Watermelon", "SK_Grape", "SK_Apple", "SK_Strawberry", "SK_StrawberryRow", "SK_MelonVineRow", "SK_GrapeTrellis" };
    // §92-7 하우스 안 장식: 낮은 딸기 포기·수박 덩굴은 밟고 지나간다(충돌 없음). 포도 시렁은 기둥·잎 지붕에 충돌.
    private static readonly string[] WalkThroughFarm = { "SK_StrawberryRow", "SK_MelonVineRow" };
    public static string FarmPrefabName(string model) => "HOB_" + model.Substring(3);   // SK_Watermelon → HOB_Watermelon(조각 모델 종류 이름)

    [MenuItem("Tools/TagOfChaos/Escape/§90 Setup Heaven Of Bakery")]
    public static string HeavenOfBakery()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Maps/HeavenOfBakery.unity", OpenSceneMode.Single);
        var log = new List<string> { Islands(), FarmPrefabs(), Bananas(), Greenhouses(), Fruits(), RefillBananas(), Signs() };
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string r = "[P90MapSetup HOB] " + string.Join(" | ", log);
        Debug.Log(r);
        return r;
    }

    private static Transform Find(string name)
    {
        foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (g.name == name) return g.transform;
        return null;
    }

    private static Material HQ(string n) => AssetDatabase.FindAssets("M_HQ_" + n + " t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(m => m != null && m.name == "M_HQ_" + n);

    // U1: 섬 몸체 부분 메시(HOBHQ_*)별 테마 재질. 초콜릿 = 흘러내리는 초코 바위(퐁듀 색), 아몬드 = 크림·구운 아몬드 바위, 밀 = 흙·밀짚 바위.
    private static readonly Dictionary<string, string> Choco = new Dictionary<string, string> {
        { "RockDark", "ChocoDark" }, { "Grass", "ChocoFondue" }, { "Rock", "Choco" }, { "Moss", "ChocoFlow" }, { "Foliage", "ChocoFondue" },
        { "Frosting", "Cream" }, { "Seed", "ChocoDark" }, { "Glaze", "ChocoFlow" }, { "Sprinkle1", "Sprinkle1" } };
    private static readonly Dictionary<string, string> Almond = new Dictionary<string, string> {
        { "RockDark", "BarkDark" }, { "Grass", "Cream" }, { "Rock", "Almond" }, { "Moss", "Wafer" }, { "Foliage", "Cream" },
        { "Frosting", "Icing" }, { "Seed", "Almond" }, { "Glaze", "Glaze" }, { "Sprinkle1", "Sprinkle2" } };
    private static readonly Dictionary<string, string> Flour = new Dictionary<string, string> {
        { "RockDark", "Soil" }, { "Grass", "Burlap" }, { "Rock", "Soil" }, { "Moss", "BurlapLight" }, { "Foliage", "Wafer" },
        { "Frosting", "White" }, { "Seed", "Seed" }, { "Glaze", "Sand" }, { "Sprinkle1", "Sprinkle3" } };

    private static string Islands()
    {
        Transform plateau = Find("Plateau_N");
        Renderer pr = plateau != null ? plateau.GetComponentInChildren<Renderer>() : null;
        if (pr == null) return "islands: no plateau";
        string[] slots = pr.sharedMaterials.Select(m => m != null ? m.name.Replace("HOBHQ_", "") : "").ToArray();
        int themed = 0;
        foreach (var (sub, map) in new[] { ("SubMap_Chocolate", Choco), ("SubMap_Almond", Almond), ("SubMap_Flour", Flour) })
        {
            Transform t = Find(sub);
            if (t == null) continue;
            SubMapTheme theme = t.GetComponent<SubMapTheme>() ?? t.gameObject.AddComponent<SubMapTheme>();
            theme.EditorSetMaterials(slots.Select(n => map.TryGetValue(n, out string m) ? HQ(m) : HQ(n)).ToArray());
            EditorUtility.SetDirty(theme);
            themed++;
        }
        // 자리마다 고원 몸체를 묶고, 지금 배치대로 바로 칠한다(판 시작 때 MapLayoutRandomizer가 다시 칠한다)
        var mlr = Object.FindFirstObjectByType<MapLayoutRandomizer>();
        if (mlr != null)
        {
            Transform[] models = mlr.EditorSubSlots.Select(sl => sl == null ? null : Find(sl.name.Replace("SubSlot_", "Plateau_"))).Select(p => p != null ? p.Find("Model") ?? p : null).ToArray();
            mlr.EditorBindPlateaus(models);
            EditorUtility.SetDirty(mlr);
            foreach (SubMapTheme th in Object.FindObjectsByType<SubMapTheme>(FindObjectsSortMode.None))
            {
                Transform near = models.Where(m => m != null).OrderBy(m => (new Vector2(m.position.x, m.position.z) - new Vector2(th.transform.position.x, th.transform.position.z)).sqrMagnitude).FirstOrDefault();
                th.ApplyTo(near);
            }
        }
        // 1층 큰 섬 밑면: 기본 HOBHQ_* → 같은 이름의 M_HQ_* (흰 단색 대신 바위·잔디 텍스처). 경사로·기둥(대리석)은 그대로.
        Transform main = Find("HOB_Island_Main85");
        int mainSet = 0;
        if (main != null)
            foreach (Renderer r in main.GetComponentsInChildren<Renderer>())
            {
                Material[] ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) { if (ms[i] == null || !ms[i].name.StartsWith("HOBHQ_")) continue; Material m = HQ(ms[i].name.Substring(6)); if (m != null) { ms[i] = m; mainSet++; } }
                r.sharedMaterials = ms;
            }
        return $"islands themes {themed}, main {mainSet}";
    }

    // 장식 프리팹: 모델 + 충돌(메시). 이름 = 조각 모델 종류 이름.
    private static string FarmPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(FarmPrefabDir)) AssetDatabase.CreateFolder(PrefabDir, "Farm");
        int n = 0;
        foreach (string m in FarmModels.Concat(new[] { "SK_Greenhouse" }))
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(P90AssetSetup.FarmDir + "/" + m + ".fbx");
            if (model == null) continue;
            var root = new GameObject(FarmPrefabName(m));
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                if (m == "SK_Greenhouse")
                {
                    GameObject film = AssetDatabase.LoadAssetAtPath<GameObject>(P90AssetSetup.FarmDir + "/SK_GreenhouseFilm.fbx");
                    if (film != null) { var f = (GameObject)PrefabUtility.InstantiatePrefab(film, root.transform); PrefabUtility.UnpackPrefabInstance(f, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        foreach (Renderer r in f.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                }
                // foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                if (!WalkThroughFarm.Contains(m))
                    foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                root.isStatic = true;
                PrefabUtility.SaveAsPrefabAsset(root, $"{FarmPrefabDir}/{FarmPrefabName(m)}.prefab");
                n++;
            }
            finally { Object.DestroyImmediate(root); }
        }
        return "farm prefabs " + n;
    }

    private static GameObject Farm(string model) => AssetDatabase.LoadAssetAtPath<GameObject>($"{FarmPrefabDir}/{FarmPrefabName(model)}.prefab");

    // V7: 1층 나무(과일·둥근·소나무 30그루)를 같은 자리·같은 수의 바나나 나무로. 하늘 섬 나무는 그대로.
    private static string Bananas()
    {
        Transform deco = Find("FieldDeco");
        if (deco == null) return "bananas: no FieldDeco";
        var trees = deco.Cast<Transform>().Where(t => t.name.StartsWith("HOB_Tree_")).ToList();
        int i = 0;
        foreach (Transform t in trees)
        {
            GameObject prefab = Farm(i % 2 == 0 ? "SK_BananaTree_A" : "SK_BananaTree_B");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, deco);
            go.name = prefab.name + " (" + i + ")";
            go.transform.SetPositionAndRotation(t.position, Quaternion.Euler(0f, (i * 137) % 360, 0f));
            float s = 0.9f + (i * 37 % 20) / 100f;
            go.transform.localScale = Vector3.one * s;
            Object.DestroyImmediate(t.gameObject);
            i++;
        }
        int existing = deco.Cast<Transform>().Count(t => t.name.StartsWith("HOB_BananaTree"));
        return $"bananas replaced {i} (total {existing})";
    }

    // 비닐하우스 자리에서 비킨 바나나 나무를 1층 빈 땅에 다시 심어 30그루를 맞춘다.
    private static string RefillBananas()
    {
        Transform deco = Find("FieldDeco");
        var rng = new System.Random(3030);
        int have = deco.Cast<Transform>().Count(t => t.name.StartsWith("HOB_BananaTree")), added = 0;
        for (int k = 0; have < 30 && k < 3000; k++)
        {
            double a = rng.NextDouble() * System.Math.PI * 2, r = 20 + rng.NextDouble() * 60;
            Vector3 at = new Vector3((float)(System.Math.Cos(a) * r), 0f, (float)(System.Math.Sin(a) * r));
            if (!GroundSpot(at, 3.5f, 6f, out Vector3 g)) continue;
            Transform t = Spawn(have % 2 == 0 ? "SK_BananaTree_A" : "SK_BananaTree_B", deco, g, rng.Next(360), " (R" + added + ")");
            have++; added++;
        }
        return $"bananas refilled +{added} = {have}";
    }

    private static readonly List<Vector3> placed = new List<Vector3>();

    private static bool Clear(Vector3 at, float radius) => Clear(at, radius, Vector2.zero);

    // rect(반 폭 x, 반 길이 z) > 0이면 상자·로켓·탈출구는 그 사각형(+3 m) 밖이기만 하면 된다(비닐하우스).
    private static bool Clear(Vector3 at, float radius, Vector2 rect)
    {
        foreach (Vector3 p in placed) if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(at.x, 0, at.z)) < radius + 3f) return false;
        foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            string n = g.name;
            bool keep = n.StartsWith("CHEST_Slot") || n.StartsWith("ExitTrigger") || n.StartsWith("Exit_") || n.StartsWith("Building_Q") || n.StartsWith("RocketSpawn") || n.StartsWith("Ramp_") || n.StartsWith("CookieSpawn") || n.StartsWith("SkyCannon");
            bool small = n.StartsWith("CHEST_Slot") || n.StartsWith("ExitTrigger") || n.StartsWith("Exit_") || n.StartsWith("RocketSpawn") || n.StartsWith("CookieSpawn");
            if (keep && small && rect != Vector2.zero)
            {
                Vector3 d = g.transform.position - at;
                if (Mathf.Abs(d.x) < rect.x + 3f && Mathf.Abs(d.z) < rect.y + 3f) return false;
                continue;
            }
            if (keep && Vector3.Distance(new Vector3(g.transform.position.x, 0, g.transform.position.z), new Vector3(at.x, 0, at.z)) < radius + (n.StartsWith("Building_Q") ? 22f : n.StartsWith("Ramp_") ? 26f : 7f)) return false;
        }
        return true;
    }

    // 1층 바닥(높이 0 근처, 큰 섬 위)에서 반지름 radius·높이 h가 빈 자리.
    private static bool GroundSpot(Vector3 at, float radius, float h, out Vector3 ground)
    {
        ground = at;
        if (!Physics.Raycast(new Vector3(at.x, 30f, at.z), Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (Mathf.Abs(hit.point.y) > 1.2f) return false;
        ground = hit.point;
        if (Physics.CheckBox(ground + Vector3.up * (h * 0.5f + 0.5f), new Vector3(radius, h * 0.5f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return false;
        return Clear(ground, radius);
    }

    private static bool SmallDeco(Transform top, Transform deco) =>
        top.parent == deco && (top.name.StartsWith("HOB_Bush") || top.name.StartsWith("HOB_Flower") || top.name.StartsWith("HOB_RockBush") || top.name.StartsWith("HOB_Bench")
        || top.name.StartsWith("HOB_Fruit_") || top.name.StartsWith("HOB_Cupcake") || top.name.StartsWith("HOB_StrawberryCupcake") || top.name.StartsWith("HOB_RockMushroom"));

    // 큰 과일(수박 12 m): 겹치는 것이 작은 장식(최대 4개)뿐이면 치우고 그 자리.
    private static bool ClearingSpot(Vector3 at, float radius, float h, Transform deco, out Vector3 ground)
    {
        ground = at;
        if (!Physics.Raycast(new Vector3(at.x, 30f, at.z), Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y) > 1.2f) return false;
        ground = hit.point;
        if (!Clear(ground, radius)) return false;
        var props = new HashSet<GameObject>();
        foreach (Collider col in Physics.OverlapBox(ground + Vector3.up * (h * 0.5f + 0.5f), new Vector3(radius, h * 0.5f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform top = col.transform; while (top.parent != null && top.parent != deco) top = top.parent;
            if (!SmallDeco(top, deco)) return false;
            props.Add(top.gameObject);
        }
        if (props.Count > 4) return false;
        foreach (GameObject g in props) Object.DestroyImmediate(g);
        Physics.SyncTransforms();
        return true;
    }

    private static Transform Spawn(string model, Transform parent, Vector3 at, float yaw, string suffix)
    {
        GameObject prefab = Farm(model);
        if (prefab == null) { Debug.LogWarning($"[P90MapSetup] {FarmPrefabName(model)} prefab is missing — import {model}.fbx first."); return null; }   // §92-7
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        // go.name = prefab.name + suffix; // 조각 모델 종류 이름(KindName)은 " (숫자)"만 떼므로 숫자 꼬리로 붙인다.
        go.name = GameObjectUtility.GetUniqueNameForSibling(parent, prefab.name);
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        Physics.SyncTransforms();
        placed.Add(at);
        return go.transform;
    }

    // §92-7 예전(하우스 2동, 안에 사과·딸기 섞어 화분 틀 위) — 주석으로 남김
    // // 비닐하우스 2동: 1층 동·서 가장자리(길이 방향 남북), 탈출구·상자·건물과 겹치지 않는 자리.
    // private static string Greenhouses()
    // {
    //     placed.Clear();
    //     Transform deco = Find("FieldDeco");
    //     foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.StartsWith("HOB_Greenhouse") || t.name.StartsWith("HOB_Watermelon") || t.name.StartsWith("HOB_Grape") || t.name.StartsWith("HOB_Apple") || t.name.StartsWith("HOB_Strawberry") || t.name.StartsWith("FallSign")).ToList())
    //         if (t != null) Object.DestroyImmediate(t.gameObject);
    //     Physics.SyncTransforms();
    //     var log = new List<string>();
    //     foreach (float side in new[] { 1f, -1f })
    //     {
    //         bool done = false;
    //         int removed = 0;
    //         for (float x = 70f; x >= 34f && !done; x -= 2f)
    //             for (float z = 0f; z <= 36f && !done; z += 2f)
    //                 foreach (float zz in new[] { z, -z })
    //                 {
    //                     Vector3 at = new Vector3(side * x, 0f, zz);
    //                     if (!Physics.Raycast(new Vector3(at.x, 30f, at.z), Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y) > 1f) continue;
    //                     if (!Clear(hit.point, 8f, new Vector2(7.5f, 15.5f))) continue;
    //                     bool ok = true;
    //                     foreach (Vector2 o in new[] { new Vector2(7, 15), new Vector2(-7, 15), new Vector2(7, -15), new Vector2(-7, -15) })
    //                         if (!Physics.Raycast(hit.point + new Vector3(o.x, 10f, o.y), Vector3.down, out RaycastHit c, 20f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(c.point.y - hit.point.y) > 0.8f) ok = false;
    //                     if (!ok) continue;
    //                     // 겹치는 것이 1층 작은 장식(덤불·꽃·바위 덤불·벤치·바나나 나무)뿐이면 치우고 놓는다
    //                     Collider[] over = Physics.OverlapBox(hit.point + Vector3.up * 4.6f, new Vector3(7.8f, 4f, 15.8f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
    //                     var props = new HashSet<GameObject>();
    //                     foreach (Collider col in over)
    //                     {
    //                         Transform top = col.transform; while (top.parent != null && top.parent != deco && !top.name.StartsWith("HOB_LampPost")) top = top.parent;
    //                         string kn = top.name;
    //                         bool lamp = kn.StartsWith("HOB_LampPost");   // 길가 가로등은 옮겨도 된다(길은 하우스 터널로 지나간다)
    //                         bool small = lamp || (top.parent == deco && (kn.StartsWith("HOB_Bush") || kn.StartsWith("HOB_Flower") || kn.StartsWith("HOB_RockBush") || kn.StartsWith("HOB_Bench") || kn.StartsWith("HOB_Fruit_") || kn.StartsWith("HOB_Cupcake") || kn.StartsWith("HOB_StrawberryCupcake") || kn.StartsWith("HOB_RockMushroom")));
    //                         if (!small) { ok = false; break; }
    //                         props.Add(top.gameObject);
    //                     }
    //                     if (!ok || props.Count > 8) continue;
    //                     foreach (GameObject g in props) Object.DestroyImmediate(g);
    //                     removed += props.Count;
    //                     Physics.SyncTransforms();
    //                     Transform gh = Spawn("SK_Greenhouse", deco, hit.point, 0f, side > 0 ? " (E)" : " (W)");
    //                     log.Add((side > 0 ? "E" : "W") + gh.position.ToString("F0") + " cleared " + removed);
    //                     Inside(gh, deco, side);
    //                     done = true;
    //                     break;
    //                 }
    //         if (!done) log.Add((side > 0 ? "E" : "W") + " none");
    //     }
    //     return "greenhouses " + string.Join(" ", log);
    // }

    // // 하우스마다: 안에 사과 3·딸기 4(높이가 들어가는 것), 바로 옆 바깥에 수박 1·포도 2 → 하우스 둘레 10개.
    // private static void Inside(Transform gh, Transform deco, float side)
    // {
    //     Vector3 c = gh.position;
    //     var inner = new (string m, Vector3 o)[] {
    //         ("SK_Apple", new Vector3(-3.4f, 0, -9.5f)), ("SK_Apple", new Vector3(3.4f, 0, -1.5f)), ("SK_Apple", new Vector3(-3.4f, 0, 6.5f)),
    //         ("SK_Strawberry", new Vector3(3.6f, 0, -10f)), ("SK_Strawberry", new Vector3(-3.6f, 0, -2.5f)), ("SK_Strawberry", new Vector3(3.6f, 0, 5f)), ("SK_Strawberry", new Vector3(-3.6f, 0, 11.5f)) };
    //     int i = 0;
    //     foreach (var (m, o) in inner) Spawn(m, deco, c + o, (i++ * 71) % 360, $" (GH{(side > 0 ? "E" : "W")}{i})");
    //     // 바깥: 하우스의 맵 가운데 쪽(안쪽) 옆
    //     float inward = -side;
    //     var outer = new (string m, Vector3 o)[] { ("SK_Watermelon", new Vector3(inward * 15f, 0, 4f)), ("SK_Grape", new Vector3(inward * 12f, 0, -10f)), ("SK_Grape", new Vector3(inward * 12f, 0, 14f)) };
    //     foreach (var (m, o) in outer)
    //     {
    //         float rad = m == "SK_Watermelon" ? 6.2f : 2.8f;
    //         Vector3 want = c + o;
    //         for (int k = 0; k < 30; k++)
    //         {
    //             Vector3 tryAt = want + new Vector3(inward * k * 1.5f, 0f, (k % 2 == 0 ? 1 : -1) * k);
    //             bool ok = m == "SK_Watermelon" ? ClearingSpot(tryAt, rad, 10f, deco, out Vector3 g) : GroundSpot(tryAt, rad, 7f, out g);
    //             if (ok) { Spawn(m, deco, g, (i++ * 53) % 360, $" (GH{(side > 0 ? "E" : "W")}{i})"); break; }
    //         }
    //     }
    // }

    // §92-7 비닐하우스 3동 — 하우스 하나에 과일 한 가지: 동 = 딸기, 서 = 수박, 남(없으면 북) = 포도. 바닥은 흙 두둑 2줄(모델).
    public enum GreenhouseTheme { Strawberry, Watermelon, Grape }
    public const float RidgeX = 4f, RidgeTop = 0.5f, TrellisTop = 4.6f;

    private static string Greenhouses()
    {
        placed.Clear();
        Transform deco = Find("FieldDeco");
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.StartsWith("HOB_Greenhouse") || t.name.StartsWith("HOB_Watermelon") || t.name.StartsWith("HOB_Grape") || t.name.StartsWith("HOB_Apple") || t.name.StartsWith("HOB_Strawberry") || t.name.StartsWith("HOB_MelonVineRow") || t.name.StartsWith("FallSign")).ToList())
            if (t != null) Object.DestroyImmediate(t.gameObject);
        Physics.SyncTransforms();
        var log = new List<string>();
        var plan = new (GreenhouseTheme theme, Vector3 outward, string tag)[]
        {
            (GreenhouseTheme.Strawberry, Vector3.right, "E"), (GreenhouseTheme.Watermelon, Vector3.left, "W"), (GreenhouseTheme.Grape, Vector3.back, "S"),
        };
        foreach (var (theme, outward, tag) in plan)
        {
            Transform gh = PlaceGreenhouse(deco, outward, out int removed);
            if (gh == null && theme == GreenhouseTheme.Grape) gh = PlaceGreenhouse(deco, Vector3.forward, out removed);
            if (gh == null) gh = PlaceGreenhouseAnywhere(deco, out removed);   // §92-7 가장자리에 자리가 없으면 섬 어디든(다른 하우스에서 먼 곳)
            if (gh == null) { log.Add(tag + " none"); continue; }
            log.Add($"{theme}{gh.position:F0} yaw {gh.eulerAngles.y:F0} cleared {removed}");
            Furnish(gh, deco, theme);
        }
        return "greenhouses " + string.Join(" ", log);
    }

    // §92-7 섬 전체 격자(반지름 70 m 안)에서 두 방향 모두 시험 — 이미 놓은 하우스에서 가장 먼 자리
    private static Transform PlaceGreenhouseAnywhere(Transform deco, out int removed)
    {
        removed = 0;
        var houses = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent == deco && t.name.StartsWith("HOB_Greenhouse")).Select(t => t.position).ToList();
        var cands = new List<(Vector3 p, bool alongZ, float score)>();
        for (float x = -70f; x <= 70f; x += 2f)
            for (float z = -70f; z <= 70f; z += 2f)
            {
                if (x * x + z * z > 70f * 70f) continue;
                float far = houses.Count == 0 ? 0f : houses.Min(h => Vector2.Distance(new Vector2(h.x, h.z), new Vector2(x, z)));
                if (far < 26f) continue;
                cands.Add((new Vector3(x, 0f, z), true, far)); cands.Add((new Vector3(x, 0f, z), false, far));
            }
        foreach (var c in cands.OrderByDescending(c => c.score))
        {
            Transform gh = TryGreenhouseAt(deco, c.p, c.alongZ, out removed);
            if (gh != null) return gh;
        }
        return null;
    }

    private static Transform TryGreenhouseAt(Transform deco, Vector3 at, bool alongZ, out int removed)
    {
        removed = 0;
        Vector2 rect = alongZ ? new Vector2(7.5f, 15.5f) : new Vector2(15.5f, 7.5f);
        Quaternion rot = Quaternion.Euler(0f, alongZ ? 0f : 90f, 0f);
        if (!Physics.Raycast(new Vector3(at.x, 30f, at.z), Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y) > 1f) return null;
        if (!Clear(hit.point, 8f, rect)) return null;
        foreach (Vector2 c in new[] { new Vector2(7, 15), new Vector2(-7, 15), new Vector2(7, -15), new Vector2(-7, -15) })
        {
            Vector3 corner = hit.point + rot * new Vector3(c.x, 0f, c.y) + Vector3.up * 10f;
            if (!Physics.Raycast(corner, Vector3.down, out RaycastHit ch, 20f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(ch.point.y - hit.point.y) > 0.8f) return null;
        }
        var props = new HashSet<GameObject>();
        foreach (Collider col in Physics.OverlapBox(hit.point + Vector3.up * 4.6f, new Vector3(7.8f, 4f, 15.8f), rot, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform top = col.transform; while (top.parent != null && top.parent != deco && !top.name.StartsWith("HOB_LampPost")) top = top.parent;
            bool movable = top.name.StartsWith("HOB_LampPost") || (top.parent == deco && top.name.StartsWith("HOB_BananaTree")) || SmallDeco(top, deco);
            if (!movable) return null;
            props.Add(top.gameObject);
        }
        if (props.Count > 10) return null;
        foreach (GameObject g in props) Object.DestroyImmediate(g);
        removed = props.Count;
        Physics.SyncTransforms();
        return Spawn("SK_Greenhouse", deco, hit.point, rot.eulerAngles.y, "");
    }

    // 섬 가장자리 쪽(outward)에서 안쪽으로 훑어 하우스(14 × 30 m, 길이 방향 = 가장자리와 나란히)가 들어갈 빈 자리.
    private static Transform PlaceGreenhouse(Transform deco, Vector3 outward, out int removed)
    {
        removed = 0;
        bool alongZ = Mathf.Abs(outward.x) > 0.5f;           // 동·서: 길이 남북 / 남·북: 길이 동서
        Vector3 lateral = alongZ ? Vector3.forward : Vector3.right;
        Vector2 rect = alongZ ? new Vector2(7.5f, 15.5f) : new Vector2(15.5f, 7.5f);
        Quaternion rot = Quaternion.Euler(0f, alongZ ? 0f : 90f, 0f);
        for (float o = 70f; o >= 34f; o -= 2f)
            for (float l = 0f; l <= 36f; l += 2f)
                foreach (float ll in new[] { l, -l })
                {
                    Vector3 at = outward * o + lateral * ll;
                    if (!Physics.Raycast(new Vector3(at.x, 30f, at.z), Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y) > 1f) continue;
                    if (!Clear(hit.point, 8f, rect)) continue;
                    bool ok = true;
                    foreach (Vector2 c in new[] { new Vector2(7, 15), new Vector2(-7, 15), new Vector2(7, -15), new Vector2(-7, -15) })
                    {
                        Vector3 corner = hit.point + rot * new Vector3(c.x, 0f, c.y) + Vector3.up * 10f;
                        if (!Physics.Raycast(corner, Vector3.down, out RaycastHit ch, 20f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(ch.point.y - hit.point.y) > 0.8f) ok = false;
                    }
                    if (!ok) continue;
                    Collider[] over = Physics.OverlapBox(hit.point + Vector3.up * 4.6f, new Vector3(7.8f, 4f, 15.8f), rot, ~0, QueryTriggerInteraction.Ignore);
                    var props = new HashSet<GameObject>();
                    foreach (Collider col in over)
                    {
                        Transform top = col.transform; while (top.parent != null && top.parent != deco && !top.name.StartsWith("HOB_LampPost")) top = top.parent;
                        bool lamp = top.name.StartsWith("HOB_LampPost") || (top.parent == deco && top.name.StartsWith("HOB_BananaTree"));   // 바나나 나무는 치우고 RefillBananas가 다시 심는다
                        if (!lamp && !SmallDeco(top, deco)) { ok = false; break; }
                        props.Add(top.gameObject);
                    }
                    if (!ok || props.Count > 8) continue;
                    foreach (GameObject g in props) Object.DestroyImmediate(g);
                    removed = props.Count;
                    Physics.SyncTransforms();
                    Transform gh = Spawn("SK_Greenhouse", deco, hit.point, rot.eulerAngles.y, "");
                    return gh;
                }
        return null;
    }

    private static void Scale(Transform t, float k) { if (t != null) t.localScale = Vector3.one * k; }

    // 하우스 안: 두둑 2줄(지역 x = ±RidgeX, 길이 = 지역 z) 위에 한 가지 과일. 장식 줄(딸기 포기·수박 덩굴)·포도 시렁은 모델.
    private static void Furnish(Transform gh, Transform deco, GreenhouseTheme theme)
    {
        Vector3 c = gh.position;
        Quaternion r = gh.rotation;
        float yaw = gh.eulerAngles.y;
        System.Func<float, float, float, Vector3> P = (x, y, z) => c + r * new Vector3(x, y, z);
        switch (theme)
        {
            case GreenhouseTheme.Strawberry:
                foreach (float x in new[] { -RidgeX, RidgeX }) Spawn("SK_StrawberryRow", deco, P(x, 0f, 0f), yaw, "");
                int k = 0;
                foreach (float x in new[] { -RidgeX, RidgeX })
                    foreach (float z in x < 0 ? new[] { -9f, -1f, 7f } : new[] { -5f, 3f, 10f })
                        Scale(Spawn("SK_Strawberry", deco, P(x, RidgeTop - 0.08f, z), yaw + (k++ * 67) % 360, ""), 0.55f);
                break;
            case GreenhouseTheme.Watermelon:
                foreach (float x in new[] { -RidgeX, RidgeX }) Spawn("SK_MelonVineRow", deco, P(x, 0f, 0f), yaw, "");
                int m = 0;
                foreach (float x in new[] { -RidgeX, RidgeX })
                    foreach (float z in x < 0 ? new[] { -8f, 3f } : new[] { -3f, 8f })
                        Scale(Spawn("SK_Watermelon", deco, P(x, RidgeTop - 0.12f, z), yaw + 90f + (m++ % 2 == 0 ? 0f : 180f) + (m * 11 % 20 - 10), ""), 0.3f);
                break;
            case GreenhouseTheme.Grape:
                Spawn("SK_GrapeTrellis", deco, c, yaw, "");
                float hang = TrellisTop + 0.1f - 0.42f * 6.5f;   // 송이 맨 위(꼭지)가 시렁에 닿게
                int g = 0;
                foreach (float z in new[] { -9.6f, -4.8f, 4.8f, 9.6f })
                    foreach (float x in new[] { -RidgeX, RidgeX })
                        Scale(Spawn("SK_Grape", deco, P(x + (g % 2 == 0 ? 0.9f : -0.9f) * Mathf.Sign(x), hang, z), yaw + (g++ * 53) % 360, ""), 0.42f);
                foreach (float x in new[] { -2.4f, 2.4f })
                    Scale(Spawn("SK_Grape", deco, P(x, hang, 0f), yaw + (g++ * 53) % 360, ""), 0.42f);
                break;
        }
    }

    // 1층 빈 땅에 고루 20개: 수박 2·포도 4·사과 6·딸기 8(탈출구·길·상자·건물 자리와 겹치지 않게).
    private static string Fruits()
    {
        Transform deco = Find("FieldDeco");
        var want = new List<string>();
        want.AddRange(Enumerable.Repeat("SK_Watermelon", 2)); want.AddRange(Enumerable.Repeat("SK_Grape", 4));
        want.AddRange(Enumerable.Repeat("SK_Apple", 6)); want.AddRange(Enumerable.Repeat("SK_Strawberry", 8));
        var rng = new System.Random(9019);
        int n = 0, tries = 0;
        foreach (string m in want)
        {
            float rad = m == "SK_Watermelon" ? 6.2f : m == "SK_Grape" ? 2.8f : m == "SK_Apple" ? 2.5f : 2.1f;
            float h = m == "SK_Watermelon" ? 10f : m == "SK_Grape" ? 7f : 4.8f;
            for (int k = 0; k < 400; k++, tries++)
            {
                double a = rng.NextDouble() * System.Math.PI * 2, r = 18 + rng.NextDouble() * 62;
                Vector3 at = new Vector3((float)(System.Math.Cos(a) * r), 0f, (float)(System.Math.Sin(a) * r));
                bool big = m == "SK_Watermelon";
                if (!(big ? ClearingSpot(at, rad + 1f, h, deco, out Vector3 g) : GroundSpot(at, rad + 1.5f, h, out g))) continue;
                Spawn(m, deco, g, rng.Next(360), " (F" + n + ")");
                n++;
                break;
            }
        }
        int total = deco.Cast<Transform>().Count(t => t.name.StartsWith("HOB_Watermelon") || t.name.StartsWith("HOB_Grape") || t.name.StartsWith("HOB_Apple") || t.name.StartsWith("HOB_Strawberry"));
        return $"fruits field {n}/20, total {total}";
    }

    // W1: 경사로마다 높이 12 m 지점·두 반환점(약 16·32 m)·위 입구(약 47 m)에 표지판(쿠키·스파이 화면만).
    private static string Signs()
    {
        int n = 0;
        foreach (string rn in new[] { "Ramp_N", "Ramp_SE", "Ramp_SW" })
        {
            Transform ramp = Find(rn);
            if (ramp == null) continue;
            Collider[] cols = ramp.GetComponentsInChildren<Collider>();
            var b = new Bounds(ramp.position, Vector3.zero); foreach (Renderer r in ramp.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var pts = new List<Vector3>();
            for (float x = b.min.x; x < b.max.x; x += 1.5f)
                for (float z = b.min.z; z < b.max.z; z += 1.5f)
                    foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, b.max.y + 5f, z), Vector3.down, b.size.y + 10f, ~0, QueryTriggerInteraction.Ignore))
                        if (System.Array.IndexOf(cols, h.collider) >= 0 && h.normal.y > 0.7f) pts.Add(h.point);
            if (pts.Count == 0) continue;
            float top = pts.Max(p => p.y);
            foreach (float y in new[] { SkyFallRules.LethalDrop, 16f, 32f, top - 1f })
            {
                Vector3 p = pts.OrderBy(q => Mathf.Abs(q.y - y)).First();
                Vector3 c = new Vector3(b.center.x, p.y, b.center.z);
                Vector3 outward = Vector3.ProjectOnPlane(p - c, Vector3.up);
                if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
                MakeSign(ramp, p + outward.normalized * 0.8f, Quaternion.LookRotation(-outward.normalized), rn + "_" + Mathf.RoundToInt(y));
                n++;
            }
        }
        return "signs " + n;
    }

    private static void MakeSign(Transform parent, Vector3 at, Quaternion rot, string tag)
    {
        var root = new GameObject("FallSign_" + tag);
        root.transform.SetParent(parent, true);
        root.transform.SetPositionAndRotation(at, rot);
        Material wood = HQ("Wafer"), dark = HQ("ChocoDark");
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(post.GetComponent<Collider>());
        post.name = "Post"; post.transform.SetParent(root.transform, false); post.transform.localPosition = new Vector3(0f, 1.1f, 0.05f); post.transform.localScale = new Vector3(0.18f, 2.2f, 0.18f);
        if (dark != null) post.GetComponent<Renderer>().sharedMaterial = dark;
        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(board.GetComponent<Collider>());
        board.name = "Board"; board.transform.SetParent(root.transform, false); board.transform.localPosition = new Vector3(0f, 2.3f, 0f); board.transform.localScale = new Vector3(2.6f, 1.15f, 0.12f);
        if (wood != null) board.GetComponent<Renderer>().sharedMaterial = wood;
        TMP_Text big = Text(root.transform, "Big", new Vector3(0f, 2.48f, -0.07f), 3.2f, new Color(0.75f, 0.12f, 0.08f));
        TMP_Text small = Text(root.transform, "Small", new Vector3(0f, 2.02f, -0.07f), 2.2f, new Color(0.25f, 0.15f, 0.1f));
        root.AddComponent<FallSign>().EditorSetup(big, small);
    }

    private static TMP_Text Text(Transform parent, string name, Vector3 pos, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.identity;
        var t = go.GetComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = new Vector2(24f, 3f);
        go.transform.localScale = Vector3.one * 0.1f;
        t.fontSize = size * 10f / 3f; t.alignment = TextAlignmentOptions.Center; t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true; t.fontSizeMin = 4f; t.fontSizeMax = size * 10f / 3f;
        t.text = name == "Big" ? EscapeTextsSO.Current.fallSign : EscapeTextsSO.Current.fallSignSmall;
        return t;
    }
}
