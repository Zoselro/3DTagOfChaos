using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 부서지기 규칙 에셋(Resources/Escape/DestructionRules)을 만들고, Blender 조각 모델 목록을 채운다(Request1009Plan.md §66 GD4).
public static class DestructionRulesSetup
{
    public const string AssetPath = "Assets/Resources/Escape/DestructionRules.asset";
    public const string FracturedFolder = "Assets/09. Environment/Fractured";
    public const string CrackTextureFolder = "Assets/Textures/Giant";
    public const string CrackMaterialFolder = "Assets/Materials/Giant";

    [MenuItem("TagOfChaos/Giant/Refresh Destruction Rules")]
    public static string Refresh()
    {
        var rules = AssetDatabase.LoadAssetAtPath<DestructionRulesSO>(AssetPath);
        if (rules == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            rules = ScriptableObject.CreateInstance<DestructionRulesSO>();
            AssetDatabase.CreateAsset(rules, AssetPath);
        }
        var models = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { FracturedFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("_Fractured.fbx")) continue;
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) models.Add(go);
        }
        models.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        rules.EditorSetFracturedNames(WriteHolders(models)); // §73.11: 이름만 — 모델은 작은 에셋이 하나씩 가리킨다
        rules.EditorSetCrackMaterials(CrackMaterials());
        EditorUtility.SetDirty(rules);
        AssetDatabase.SaveAssets();
        return $"DestructionRules: {models.Count} fractured models";
    }

    public const string HolderFolder = "Assets/Resources/Escape/Fractured";

    // 조각 모델마다 Resources/Escape/Fractured/<원본 이름>.asset(FracturedModelSO)을 만들고 원본 이름 목록을 돌려준다.
    // 규칙 에셋이 모델을 직접 가리키면 규칙을 처음 읽을 때 모든 맵의 조각 모델(876 MB)을 한꺼번에 읽었다(§73.11).
    public static string[] WriteHolders(List<GameObject> models)
    {
        Directory.CreateDirectory(HolderFolder);
        var names = new List<string>();
        foreach (GameObject m in models)
        {
            string name = m.name.Replace("_Fractured", "");
            string path = $"{HolderFolder}/{name}.asset";
            var holder = AssetDatabase.LoadAssetAtPath<FracturedModelSO>(path);
            if (holder == null)
            {
                holder = ScriptableObject.CreateInstance<FracturedModelSO>();
                AssetDatabase.CreateAsset(holder, path);
            }
            holder.EditorSet(m);
            EditorUtility.SetDirty(holder);
            names.Add(name);
        }
        return names.ToArray();
    }

    // 금 간 데칼 재질 3개(GD3 텍스처 — Standard Fade + 노멀).
    private static Material[] CrackMaterials()
    {
        Directory.CreateDirectory(CrackMaterialFolder);
        var mats = new Material[3];
        for (int n = 1; n <= 3; n++)
        {
            string path = $"{CrackMaterialFolder}/M_GiantCrack_{n}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            EscapeVisuals.ConfigureTemplate(m, fade: true, glow: false);
            m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CrackTextureFolder}/T_GiantCrack_{n}.png");
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{CrackTextureFolder}/T_GiantCrack_{n}_N.png"));
            m.SetFloat("_BumpScale", 1.2f);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Glossiness", 0.08f);
            m.color = Color.white;
            EditorUtility.SetDirty(m);
            mats[n - 1] = m;
        }
        return mats;
    }
}
