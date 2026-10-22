using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 저주받은 놀이공원 새 모델 묶음(자이로드롭·바이킹·맵 오브젝트·바닥 타일·재료 건물)을 Models/<묶음>/ 에서 정리한다.
// Blender에서 만든 FBX는 재질 이름만 담고 있으므로, 묶음마다 materials.json(Blender 재질 값)을 읽어
// Materials/M_<이름>.mat(Standard)을 만들고 FBX 재질 슬롯을 그 재질로 다시 잇는다.
// 화질: 텍스처는 줄이지 않고(원본 크기), 밉맵 + 이방성 8 + 반복(타일링) — 멀리서도 뭉개지지 않게.
// 재료 건물(§36)은 세계 크기 세부 결 층(두 번째 UV, Detail Albedo ×2 + Detail Normal)을 더한다 — 크게 키운 건물 겉이 가까이서도 뭉개지지 않게.
// 숨을 곳(텐트)은 V4 숨을 곳 규칙(TwistedPropBuilder: COL_ → 벽 BoxCollider, Light_ → 빛, Zone_ → IndoorZone)으로 프리팹을 만든다.
// 여러 번 불러도 같은 결과(재질 값을 다시 쓰고 프리팹을 덮어쓴다).
public static class CarnivalRideModelsImporter
{
    public const string Root = "Assets/Maps/CursedCandyCarnival/Models";
    public static readonly string[] Sets = { "GyroDrop", "Viking", "MapObjects", "Floor", "MaterialBuildings" };
    public const string PrefabFolder = "Assets/04. Prefabs/Carnival";
    public static readonly string[] Shelters = { "MapObjects/MO_Tent" };
    // 세트별 매끈함 배율(_GlossMapScale). 바닥 타일은 가로등 빛을 받으면 매끈한 타일 하나하나가 하얀 네모로 튀어 낮춘다(§37).
    private static readonly Dictionary<string, float> GlossScale = new Dictionary<string, float> { { "Floor", 0.65f } };
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";

    [Serializable]
    private class MaterialSpec
    {
        public string name;
        public string tex;          // 기본색 텍스처(없으면 color)
        public string normal;       // 노멀 맵(선택)
        public string metalSmooth;  // R=금속, A=매끄러움(선택)
        public float[] color = { 1f, 1f, 1f };
        public float metallic;
        public float rough = 0.6f;
        public float[] emission = { 0f, 0f, 0f };
        public float strength;
        public string detail;       // 세부 결 기본색(두 번째 UV, 선택)
        public string detailNormal; // 세부 결 노멀(두 번째 UV, 선택)
    }

    [Serializable]
    private class MaterialList
    {
        public MaterialSpec[] materials;
    }

    [MenuItem(MenuRoot + "Import Carnival Ride Models (GyroDrop, Viking, MapObjects, Floor, MaterialBuildings)")]
    public static void ImportAllMenu() => Debug.Log("[CarnivalRideModelsImporter] " + ImportAll());

    public static string ImportAll() => string.Join(" | ", Sets.Select(ImportSet)) + " | " + BuildShelterPrefabs();

    // 숨을 곳 프리팹: 벽은 COL_ 상자만 막고(천막 문 천·지붕은 통과), 안쪽·출구 빛, 실내 소리 상자.
    public static string BuildShelterPrefabs()
    {
        TwistedPropBuilder.EnsureFolder(PrefabFolder);
        var report = new List<string>();
        foreach (string shelter in Shelters)
        {
            string modelPath = $"{Root}/{shelter}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { report.Add("missing " + modelPath); continue; }
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = model.name;
            TwistedPropBuilder.AddColliders(root);
            TwistedPropBuilder.AddShelterParts(root);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            int walls = root.GetComponentsInChildren<BoxCollider>(true).Length;
            int lights = root.GetComponentsInChildren<Light>(true).Length;
            bool zone = root.GetComponent<IndoorZone>() != null;
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{model.name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            report.Add($"{model.name}.prefab: {walls} boxes, {lights} lights, zone {zone}");
        }
        AssetDatabase.SaveAssets();
        return string.Join(", ", report);
    }

    public static string ImportSet(string set)
    {
        string folder = $"{Root}/{set}";
        string json = $"{folder}/materials.json";
        if (!File.Exists(json)) return $"{set}: no materials.json";
        MaterialSpec[] specs = JsonUtility.FromJson<MaterialList>(File.ReadAllText(json)).materials ?? new MaterialSpec[0];

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string png in Directory.GetFiles($"{folder}/Textures", "*.png"))
                ConfigureTexture(png.Replace('\\', '/'), specs);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        string matFolder = $"{folder}/Materials";
        if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder(folder, "Materials");
        float glossScale = GlossScale.TryGetValue(set, out float g) ? g : 1f;
        var materials = specs.ToDictionary(s => s.name, s => WriteMaterial(s, folder, matFolder, glossScale));
        AssetDatabase.SaveAssets();

        int models = 0, slots = 0;
        var missing = new HashSet<string>();
        foreach (string fbx in Directory.GetFiles(folder, "*.fbx"))
        {
            string path = fbx.Replace('\\', '/');
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
            ConfigureModel(importer);
            foreach (string name in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name).Distinct().ToList())
            {
                if (materials.TryGetValue(name, out Material mat)) { importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat); slots++; }
                else missing.Add(name);
            }
            importer.SaveAndReimport();
            models++;
        }
        return $"{set}: {models} fbx, {materials.Count} materials, {slots} slots remapped" + (missing.Count > 0 ? ", missing " + string.Join(",", missing) : "");
    }

    private static void ConfigureModel(ModelImporter importer)
    {
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; // 슬롯 이름이 있어야 다시 이을 수 있다
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.addCollider = false;
        importer.bakeAxisConversion = true;
        importer.meshCompression = ModelImporterMeshCompression.Off;      // 정점 위치를 그대로(뭉개짐 방지)
        importer.indexFormat = ModelImporterIndexFormat.Auto;             // 6만 5천 정점이 넘는 선체도 한 메시로
        importer.isReadable = false;
    }

    private static void ConfigureTexture(string path, MaterialSpec[] specs)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        string file = Path.GetFileName(path);
        bool normal = specs.Any(s => s.normal == file || s.detailNormal == file);
        bool linear = specs.Any(s => s.metalSmooth == file);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !normal && !linear;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 8;
        ti.filterMode = FilterMode.Trilinear;
        ti.wrapMode = TextureWrapMode.Repeat;   // 세계 크기 타일링 UV
        ti.maxTextureSize = 4096;               // 원본(1024~4096) 그대로 — 4 m 바닥 무늬는 4096 px
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();
    }

    private static Material WriteMaterial(MaterialSpec s, string folder, string matFolder, float glossScale)
    {
        string path = $"{matFolder}/M_{s.name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard")) { name = "M_" + s.name };
            AssetDatabase.CreateAsset(mat, path);
        }
        Texture2D Load(string file) => string.IsNullOrEmpty(file) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Textures/{file}");

        Texture2D albedo = Load(s.tex);
        mat.mainTexture = albedo;
        mat.color = albedo != null ? Color.white : new Color(s.color[0], s.color[1], s.color[2]);
        mat.SetFloat("_Metallic", s.metallic);
        mat.SetFloat("_Glossiness", 1f - s.rough);

        Texture2D bump = Load(s.normal);
        mat.SetTexture("_BumpMap", bump);
        SetKeyword(mat, "_NORMALMAP", bump != null);

        Texture2D ms = Load(s.metalSmooth);
        mat.SetTexture("_MetallicGlossMap", ms);
        mat.SetFloat("_GlossMapScale", glossScale);
        SetKeyword(mat, "_METALLICGLOSSMAP", ms != null);

        // 세부 결: 두 번째 UV(_UVSec 1)에 기본색 ×2와 노멀. UV가 이미 세계 크기(0.75 m 반복)라 배율은 1.
        Texture2D detail = Load(s.detail), detailNormal = Load(s.detailNormal);
        mat.SetTexture("_DetailAlbedoMap", detail);
        mat.SetTexture("_DetailNormalMap", detailNormal);
        mat.SetFloat("_DetailNormalMapScale", 0.6f);
        mat.SetFloat("_UVSec", detail != null || detailNormal != null ? 1f : 0f);
        SetKeyword(mat, "_DETAIL_MULX2", detail != null || detailNormal != null);

        // Blender 발광 세기(와트 기준)는 Unity HDR 배수로 줄여 옮긴다: 1→1, 10→~2.7, 25→~3.3
        bool glow = s.strength > 0f;
        float intensity = glow ? 1f + Mathf.Log(Mathf.Max(1f, s.strength), 2f) * 0.5f : 0f;
        Color e = new Color(s.emission[0], s.emission[1], s.emission[2]) * intensity;
        mat.SetColor("_EmissionColor", glow ? e : Color.black);
        mat.SetTexture("_EmissionMap", glow ? albedo : null);
        SetKeyword(mat, "_EMISSION", glow);
        mat.globalIlluminationFlags = glow ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SetKeyword(Material mat, string keyword, bool on)
    {
        if (on) mat.EnableKeyword(keyword);
        else mat.DisableKeyword(keyword);
    }
}

