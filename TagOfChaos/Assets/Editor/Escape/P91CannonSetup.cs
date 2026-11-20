using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Request1009Plan.md §91 하늘 대포 6종(사용자 Meshy) — 텍스처·재질·프리팹(받침+포가+포신+해치)·씬 대포 6대에 SkyCannonSkin 연결.
// FBX는 Blender에서 받침(SkyCannon_CarriageMesh)·포신(SkyCannon_BarrelMesh, 원점 = 핀)·해치(SkyCannon_HatchMesh, 원점 = 경첩)로 나눠 왔다.
public static class P91CannonSetup
{
    public const string Folder = "Assets/09. Environment/HeavenOfBakery/SkyCannons";
    public const string PrefabFolder = "Assets/04. Prefabs/Environment/HeavenOfBakery/SkyCannons";
    public const string BasePrefab = "Assets/04. Prefabs/Environment/HeavenOfBakery/HOB_SkyCannon.prefab";
    public static readonly string[] Names = { "WheatToAlmond", "WheatToChoco", "AlmondToWheat", "AlmondToChoco", "ChocoToWheat", "ChocoToAlmond" };
    public const float StepsBack = 1.0f, StepsTall = 2.0f;
    public static readonly Vector3 BoxCenter = new Vector3(0f, 2.8f, 0.2f), BoxSize = new Vector3(3.8f, 5.6f, 5.6f);

    [MenuItem("Tools/TagOfChaos/Maps/Heaven Of Bakery/P91 Sky Cannons (Meshy 6)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new List<string>();
        AssetDatabase.Refresh();
        EnsureFolder(PrefabFolder);
        var prefabs = new GameObject[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            Material m = SetupMaterial(Names[i]);
            GameObject fbx = SetupModel(Names[i]);
            if (fbx == null || m == null) { log.Add(Names[i] + " missing"); continue; }
            prefabs[i] = BuildPrefab(Names[i], fbx, m, out string note);
            log.Add(Names[i] + " " + note);
        }
        log.Add(AttachSkins(prefabs));
        return "[P91Cannon] " + string.Join(" | ", log);
    }

    private static Material SetupMaterial(string n)
    {
        string color = $"{Folder}/SkyCannon_{n}_color.jpg", normal = $"{Folder}/SkyCannon_{n}_normal.jpg", ms = $"{Folder}/SkyCannon_{n}_ms.png";
        Texture(color, false, true); Texture(normal, true, false); Texture(ms, false, false);
        string path = $"{Folder}/M_SkyCannon_{n}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
        mat.SetFloat("_BumpScale", 1f);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ms));
        mat.EnableKeyword("_METALLICGLOSSMAP");
        mat.SetFloat("_GlossMapScale", 1f);
        mat.SetFloat("_SmoothnessTextureChannel", 0f);
        mat.color = Color.white;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void Texture(string path, bool normal, bool srgb)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.sRGBTexture = srgb;
        imp.maxTextureSize = 2048;
        imp.mipmapEnabled = true;
        imp.SaveAndReimport();
    }

    private static GameObject SetupModel(string n)
    {
        string path = $"{Folder}/SkyCannon_{n}.fbx";
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return null;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.importCameras = false; imp.importLights = false; imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None;
        imp.isReadable = false; imp.globalScale = 1f; imp.useFileScale = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
        return null;
    }

    private static GameObject BuildPrefab(string n, GameObject fbx, Material mat, out string note)
    {
        var root = new GameObject("Model");
        // 받침(옛 대포의 둥근 받침·띠·윗판·계단) — 포탑·포신은 버린다
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab);
        var b = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, root.transform);
        PrefabUtility.UnpackPrefabInstance(b, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        b.name = "Base";
        b.transform.localPosition = Vector3.zero; // 옛 프리팹 뿌리 = 받침 가운데 바닥
        Transform oldTurret = FindDeep(b.transform, SkyCannonView.TurretName);
        if (oldTurret != null) Object.DestroyImmediate(oldTurret.gameObject);
        // foreach (Transform s in b.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("SkyCannon_Step")).ToArray())
        // {
        // // 계단: 포가가 길어진 만큼 1 m 뒤로, 높아진 해치까지 2배 높이(바닥은 그대로)
        // Vector3 w = s.position; s.position = w + root.transform.TransformDirection(Vector3.back) * StepsBack;
        // Vector3 upLocal = s.InverseTransformDirection(Vector3.up);
        // int k = Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.y) ? (Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.z) ? 0 : 2) : (Mathf.Abs(upLocal.y) > Mathf.Abs(upLocal.z) ? 1 : 2);
        // Vector3 ls = s.localScale; ls[k] *= StepsTall; s.localScale = ls;
        // }
        // §91 사용자 요청: 대포 계단은 없앤다(탑승은 뒤쪽 바닥에서 E, 오르는 모습은 SkyCannonView가 그린다)
        foreach (Transform s in b.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("SkyCannon_Step")).ToArray())
            Object.DestroyImmediate(s.gameObject);
        // 새 대포(받침 윗면 위)
        var holder = new GameObject("Cannon").transform;
        holder.SetParent(root.transform, false);
        holder.localPosition = Vector3.up * SkyCannonRules.BaseTop;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, holder);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
        Transform carriage = FindDeep(model.transform, "SkyCannon_CarriageMesh"), barrelMesh = FindDeep(model.transform, "SkyCannon_BarrelMesh"), hatchMesh = FindDeep(model.transform, "SkyCannon_HatchMesh");
        // 포구 방향 확인: 앞(포구)이 뒤보다 길다 → +Z가 아니면 뒤집는다
        Bounds bb = barrelMesh.GetComponent<Renderer>().bounds;
        float front = bb.max.z - barrelMesh.position.z, back = barrelMesh.position.z - bb.min.z;
        bool flipped = front < back;
        if (flipped) holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var turret = new GameObject(SkyCannonView.TurretName).transform; turret.SetParent(root.transform, false); turret.localPosition = holder.localPosition;
        var barrel = new GameObject(SkyCannonView.BarrelName).transform; barrel.SetParent(turret, false); barrel.position = barrelMesh.position; barrel.rotation = root.transform.rotation;
        var hatch = new GameObject(SkyCannonView.HatchName).transform; hatch.SetParent(barrel, false); hatch.position = hatchMesh.position; hatch.rotation = root.transform.rotation;
        carriage.SetParent(turret, true); barrelMesh.SetParent(barrel, true); hatchMesh.SetParent(hatch, true);
        Object.DestroyImmediate(model);
        Object.DestroyImmediate(holder.gameObject);
        note = $"pin {barrel.position.y - root.transform.position.y:F2} hinge {hatch.localPosition:F2} flipped={flipped}";
        string path = $"{PrefabFolder}/HOB_SkyCannon_{n}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    public static GameObject[] LoadPrefabs() => Names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/HOB_SkyCannon_{n}.prefab")).ToArray();

    // 씬의 하늘 대포 6대: SkyCannonSkin + 지금 배치에 맞는 모습 + 몸통 상자.
    public static string AttachSkins(GameObject[] prefabs)
    {
        int n = 0;
        foreach (SkyCannonView v in Object.FindObjectsByType<SkyCannonView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var skin = v.GetComponent<SkyCannonSkin>();
            if (skin == null) skin = v.gameObject.AddComponent<SkyCannonSkin>();
            skin.EditorSetVariants(prefabs);
            int idx = 0;
            if (SkyCannonSkin.TryThemeAt(v.transform.position, out SkyCannonSkin.Theme a) && SkyCannonSkin.TryThemeAt(v.Landing, out SkyCannonSkin.Theme b))
                idx = Mathf.Max(0, SkyCannonSkin.VariantIndex(a, b));
            if (prefabs[idx] != null) skin.EditorShow(idx);
            var box = v.GetComponent<BoxCollider>();
            if (box != null) { box.center = BoxCenter; box.size = BoxSize; }
            EditorUtility.SetDirty(v.gameObject);
            n++;
        }
        if (n > 0) EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return $"skins {n}";
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
