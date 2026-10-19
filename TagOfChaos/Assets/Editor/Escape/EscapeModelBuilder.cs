using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 탈출 모드 Blender 모델 연결(EscapePlan.md §4.4, P1). escape_assets.py가 내보낸 FBX를 가져온 뒤:
// 1) 공용 팔레트에 없는 머티리얼(ME_* 등)을 Escape/Materials에 .mat으로 꺼내고 FBX에 다시 연결한다.
// 2) 아이템 SO에 모델(ITEM_<ItemId>.fbx)을 연결한다 — 바닥·손·장치 칸·로켓 칸이 모두 이 모델을 쓴다.
// 3) 맵 5개의 탈출 배치를 다시 만든다(EscapeMapSetup이 장치·로켓·상자·마녀에 모델을 쓴다. 자리는 시드로 같다).
public static class EscapeModelBuilder
{
    private const string LogTag = "[EscapeModels]";
    private const string ItemFolder = "Assets/03. SO/Escape/Items";
    public const string ItemPrefabFolder = "Assets/04. Prefabs/Items";

    // 발광해야 하는 머티리얼(Blender 팔레트의 발광 값과 같다). FBX 기본 임포트가 발광을 빼먹어도 여기서 맞춘다.
    private static readonly Dictionary<string, float> Emission = new Dictionary<string, float>
    {
        { "ME_Button_Red", 0.6f }, { "ME_Battery_Green", 0.4f }, { "ME_Rune_Red", 1f }, { "ME_Rune_Pink", 1f },
        { "ME_Rune_Blue", 1f }, { "ME_Rune_Yellow", 1f }, { "ME_Cell_Red", 0.6f }, { "ME_Cell_Orange", 0.6f },
        { "ME_Cell_Yellow", 0.6f }, { "ME_Cell_Green", 0.6f }, { "ME_Stun_Spark", 4f }, { "ME_Rocket_Window", 1.5f },
        { "ME_Rocket_Flame", 4f }, { "ME_Altar_Glow", 4f }, { "ME_Witch_Eye", 6f },
        { "ME_Glow_Teal", 2f }, { "ME_Glow_Orange", 2f }, { "ME_Glow_Purple", 2f }, { "ME_Glow_White", 3f },
        { "ME_Red_Button", 0.5f },
        { "ME_Cabin_Wall", 0.35f }, { "ME_Cabin_Floor", 0.2f }, { "ME_Cabin_Seat", 0.3f }, // 로켓 조종실(그늘 속에서도 보이게, Request1003bPlan.md §6)
    };

    [MenuItem("Tools/TagOfChaos/Escape/Build Models (after Blender export)")]
    public static void BuildAll()
    {
        Debug.Log(Build(rebuildScenes: true));
    }

    public static string Build(bool rebuildScenes)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureFolder(EscapeModelImportPostprocessor.MaterialFolder);

        string[] models = ModelPaths();
        UnlinkEscapeMaterials(models);
        int extracted = 0;
        foreach (string path in models) extracted += ExtractMissingMaterials(path);
        foreach (string path in models)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (EscapeModelImportPostprocessor.RemapMaterials(importer)) importer.SaveAndReimport();
        }
        FixEmission();
        FixGlass();

        int linked = 0;
        var missing = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemSO", new[] { ItemFolder }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            // 프리팹이 있으면 그것(캔디숲 막대사탕처럼 다른 곳에서 만든 모델, §28.5), 없으면 escape_assets.py FBX
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemPrefabFolder}/ITEM_{item.ItemId}.prefab")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>($"{EscapeModelImportPostprocessor.ModelFolder}/ITEM_{item.ItemId}.fbx");
            if (model == null) { missing.Add(item.ItemId); continue; }
            var so = new SerializedObject(item);
            so.FindProperty("modelPrefab").objectReferenceValue = model;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            linked++;
        }
        LinkCatalogModels();
        AssetDatabase.SaveAssets();

        if (rebuildScenes) EscapeMapSetup.SetupAll();
        return $"{LogTag} models {models.Length}, materials extracted {extracted}, items linked {linked}" +
               (missing.Count > 0 ? $", missing item models: {string.Join(", ", missing)}" : string.Empty) +
               (rebuildScenes ? ", map scenes rebuilt" : string.Empty);
    }

    public static string[] ModelPaths() =>
        AssetDatabase.FindAssets("t:Model", new[] { EscapeModelImportPostprocessor.ModelFolder })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx")).OrderBy(p => p).ToArray();

    // 탈출 전용 .mat 연결을 잠시 풀어 FBX에 들어 있는 머티리얼(Blender 색)을 다시 읽을 수 있게 한다.
    private static void UnlinkEscapeMaterials(string[] models)
    {
        EscapeModelImportPostprocessor.SkipRemap = true;
        try
        {
            foreach (string path in models)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                bool removed = false;
                foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> kv in importer.GetExternalObjectMap())
                {
                    if (kv.Value == null || !AssetDatabase.GetAssetPath(kv.Value).StartsWith(EscapeModelImportPostprocessor.MaterialFolder)) continue;
                    importer.RemoveRemap(kv.Key);
                    removed = true;
                }
                if (removed) importer.SaveAndReimport();
            }
        }
        finally
        {
            EscapeModelImportPostprocessor.SkipRemap = false;
        }
    }

    // FBX 안에 들어 있는 머티리얼 중 이름이 같은 .mat이 없는 것을 Escape/Materials로 복사하고, 있으면 색을 맞춘다.
    private static int ExtractMissingMaterials(string modelPath)
    {
        int count = 0;
        foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>())
        {
            Material existing = FindMaterial(embedded.name);
            if (existing != null)
            {
                // 탈출 모드 전용 .mat은 Blender 팔레트 색을 따라간다(공용 M_* 은 맵이 관리하므로 건드리지 않는다).
                if (AssetDatabase.GetAssetPath(existing).StartsWith(EscapeModelImportPostprocessor.MaterialFolder) && existing.color != embedded.color)
                {
                    existing.color = embedded.color;
                    EditorUtility.SetDirty(existing);
                }
                continue;
            }
            var copy = new Material(Shader.Find("Standard")) { name = embedded.name, color = embedded.color };
            copy.SetFloat("_Glossiness", embedded.HasProperty("_Glossiness") ? embedded.GetFloat("_Glossiness") : 0.25f);
            AssetDatabase.CreateAsset(copy, $"{EscapeModelImportPostprocessor.MaterialFolder}/{embedded.name}.mat");
            count++;
        }
        return count;
    }

    private static Material FindMaterial(string name)
    {
        foreach (string folder in new[] { EscapeModelImportPostprocessor.CommonMaterialFolder, EscapeModelImportPostprocessor.MaterialFolder })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{folder}/{name}.mat");
            if (m != null) return m;
        }
        return null;
    }

    private static void FixEmission()
    {
        foreach (KeyValuePair<string, float> e in Emission)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{EscapeModelImportPostprocessor.MaterialFolder}/{e.Key}.mat");
            if (m == null) continue;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", m.color * e.Value);
            EditorUtility.SetDirty(m);
        }
    }

    // 탈것 탑승 인형·결과 화면 유리병(EscapeVisualPlan.md §3.3, §4.3).
    private static void LinkCatalogModels()
    {
        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        if (catalog == null) return;
        var so = new SerializedObject(catalog);
        so.FindProperty("passengerModel").objectReferenceValue = Model("PASSENGER");
        so.FindProperty("jarModel").objectReferenceValue = Model("JAR");
        so.FindProperty("jarCookieModel").objectReferenceValue = Model("JAR_Cookie");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    public static GameObject Model(string unit) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{EscapeModelImportPostprocessor.ModelFolder}/{unit}.fbx");

    // 이름에 Glass가 들어간 머티리얼은 반투명 유리(Standard Fade 모드)로 만든다(유리병·원유 병·연료통 창).
    private static void FixGlass()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { EscapeModelImportPostprocessor.MaterialFolder }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || !m.name.Contains("Glass")) continue;
            Color c = m.color;
            c.a = 0.35f;
            m.color = c;
            m.SetFloat("_Mode", 2f);
            m.SetFloat("_Glossiness", 0.85f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
