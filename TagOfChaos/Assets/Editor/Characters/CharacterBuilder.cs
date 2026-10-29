using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 캐릭터 4종(Blender 리깅 결과)을 Unity에 맞춘다(Request1009Plan.md §45.3 CH4·CH5, §45.10 CH7).
// - 머티리얼 CHR_<이름>.mat(Standard — 쿠키와 같은 셰이더, 색 + 노멀)
// - 프리팹 Assets/04. Prefabs/Characters/CHR_<이름>.prefab(모델 + Humanoid 아바타 Animator, 컨트롤러 없음)
// - 캐릭터 목록 5칸: 0 쿠키(지금 몸) + 4종. 화면 이름(label)은 에셋에서 입력한다(코드에 한글 없음) — 이미 있는 칸의 이름은 지키고 새 칸은 비워 둔다.
// - 가루 색: 각 모델 색 텍스처에서 많이 쓰인 색 4개(비슷한 색은 하나로).
public static class CharacterBuilder
{
    public const string ModelRoot = "Assets/Animation/Characters";
    public const string MaterialFolder = "Assets/05. Materials/Character";
    public const string PrefabFolder = "Assets/04. Prefabs/Characters";
    public static readonly string[] Names = { "Macaron", "Montblanc", "Lemonade", "Bungeo" };
    public const string CookieMaterialPath = "Assets/05. Materials/Character/Cookie_BaseSkin_A.mat";
    private static readonly Color CookieCrumb = new Color(0.55f, 0.30f, 0.12f);

    [MenuItem("Tools/Characters/Build Characters (Prefabs + Catalog)")]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[CharacterBuilder] Stop Play Mode first."); return; }
        Debug.Log(Build());
    }

    public static string Build()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/04. Prefabs", "Characters");
        var prefabs = new List<GameObject>();
        var crumbs = new List<Color[]>();
        foreach (string n in Names)
        {
            Material mat = BuildMaterial(n);
            prefabs.Add(BuildPrefab(n, mat));
            crumbs.Add(DominantColors(mat.mainTexture as Texture2D, 4));
        }
        SkinCatalogSO catalog = GameSettings.Current.CharacterCatalog;
        var cookieMat = AssetDatabase.LoadAssetAtPath<Material>(CookieMaterialPath);
        var old = new List<SkinCatalogSO.SkinEntry>();
        for (int i = 0; i < catalog.Count; i++) old.Add(catalog.EditorGet(i));
        var entries = new SkinCatalogSO.SkinEntry[Names.Length + 1];
        entries[0] = new SkinCatalogSO.SkinEntry { label = LabelFor(old, 0, null), material = cookieMat, modelPrefab = null, crumbColors = new[] { CookieCrumb, CookieCrumb * 0.8f, new Color(0.72f, 0.45f, 0.2f) } };
        for (int i = 0; i < Names.Length; i++)
            entries[i + 1] = new SkinCatalogSO.SkinEntry { label = LabelFor(old, i + 1, prefabs[i]), material = prefabs[i].GetComponentInChildren<Renderer>().sharedMaterial, modelPrefab = prefabs[i], crumbColors = crumbs[i] };
        catalog.EditorSet(entries);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return $"[CharacterBuilder] {Names.Length} prefabs, catalog = {entries.Length} entries ({AssetDatabase.GetAssetPath(catalog)}).";
    }

    // 이미 이 모델이 들어 있던 칸의 이름을 지킨다. 없으면 빈칸(에셋에서 입력).
    private static string LabelFor(List<SkinCatalogSO.SkinEntry> old, int index, GameObject prefab)
    {
        foreach (SkinCatalogSO.SkinEntry e in old)
            if (e.modelPrefab != null && e.modelPrefab == prefab) return e.label;
        if (prefab == null && old.Count > 0 && old[0].modelPrefab == null && index == 0 && old.Count == Names.Length + 1) return old[0].label;
        return "";
    }

    private static Material BuildMaterial(string n)
    {
        string path = $"{MaterialFolder}/CHR_{n}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelRoot}/{n}/{n}_Albedo.png"));
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelRoot}/{n}/{n}_Normal.png");
        mat.SetTexture("_BumpMap", normal);
        if (normal != null) mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.25f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject BuildPrefab(string n, Material mat)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/{n}/CHR_{n}.fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = "CHR_" + n;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = mat;
            if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = false;
        }
        Animator a = go.GetComponent<Animator>();
        if (a != null) { a.runtimeAnimatorController = null; a.applyRootMotion = false; }
        string path = $"{PrefabFolder}/CHR_{n}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // 텍스처에서 많이 쓰인 색 count개(어두운 틈·투명 배경 제외, 서로 다른 색).
    public static Color[] DominantColors(Texture2D tex, int count)
    {
        if (tex == null) return new[] { CookieCrumb };
        string path = AssetDatabase.GetAssetPath(tex);
        var bytes = File.ReadAllBytes(path);
        var copy = new Texture2D(2, 2);
        copy.LoadImage(bytes);
        var bins = new Dictionary<int, int>();
        var sums = new Dictionary<int, Vector3>();
        Color32[] px = copy.GetPixels32();
        for (int i = 0; i < px.Length; i += 7)
        {
            Color32 c = px[i];
            if (c.a < 128 || c.r + c.g + c.b < 90) continue;
            int key = (c.r >> 5) << 6 | (c.g >> 5) << 3 | (c.b >> 5);
            bins[key] = bins.TryGetValue(key, out int k) ? k + 1 : 1;
            sums[key] = (sums.TryGetValue(key, out Vector3 v) ? v : Vector3.zero) + new Vector3(c.r, c.g, c.b);
        }
        Object.DestroyImmediate(copy);
        var order = new List<int>(bins.Keys);
        order.Sort((x, y) => bins[y].CompareTo(bins[x]));
        var picked = new List<Color>();
        foreach (int key in order)
        {
            Vector3 avg = sums[key] / bins[key] / 255f;
            var c = new Color(avg.x, avg.y, avg.z);
            bool near = false;
            foreach (Color p in picked) if (Mathf.Abs(p.r - c.r) + Mathf.Abs(p.g - c.g) + Mathf.Abs(p.b - c.b) < 0.25f) near = true;
            if (near) continue;
            picked.Add(c);
            if (picked.Count >= count) break;
        }
        return picked.Count > 0 ? picked.ToArray() : new[] { CookieCrumb };
    }
}
