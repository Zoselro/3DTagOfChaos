using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// 진저브레드 마을 네온 줄이기(2026-10-03 사용자 요청, SoundPlan.md S3+). 장식 발광 재질을 진저브레드 전용 복사본(발광 45%)으로 바꾸고
// 블룸을 낮춘다. 다른 맵은 원본 재질을 그대로 쓴다. 탈출 배치(EscapeMapSetup)를 다시 만들면 장치 모델이 원본 재질로 돌아오므로 다시 실행한다.
public static class GingerbreadNeonDimmer
{
    private const string MenuPath = "Tools/TagOfChaos/Maps/Gingerbread Dim Neon";
    private const string ScenePath = "Assets/Scenes/Maps/Game_GingerbreadVillage.unity";
    private const string Folder = "Assets/Maps/GingerbreadVillage/Materials";
    public const string Suffix = "_GingerDim";
    public const float EmissionFactor = 0.45f;
    public const float BloomIntensity = 0.6f;
    public const float BloomThreshold = 1.6f;

    // 장식 발광만(게임에 쓰이는 룬·보석·마녀 눈·탈출 장치 보라 빛은 그대로).
    public static readonly HashSet<string> DimmedMaterials = new HashSet<string>
    {
        "M_Window_Yellow", "M_Glow_Pink", "M_Glow_Yellow", "ME_Glow_Orange", "ME_Glow_White", "M_Window_Glass_Purple", "M_Glow_Cyan",
    };

    [MenuItem(MenuPath)]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        Debug.Log($"[GingerDim] {ApplyToOpenScene()}");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static string ApplyToOpenScene()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Maps/GingerbreadVillage", "Materials");
        var dims = new Dictionary<Material, Material>();
        int swapped = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !DimmedMaterials.Contains(m.name)) continue;
                if (!dims.TryGetValue(m, out Material dim)) dims[m] = dim = DimCopy(m);
                mats[i] = dim;
                changed = true;
                swapped++;
            }
            if (!changed) continue;
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
        }

        var volume = Object.FindFirstObjectByType<PostProcessVolume>();
        if (volume != null && volume.sharedProfile != null && volume.sharedProfile.TryGetSettings(out Bloom bloom))
        {
            bloom.intensity.value = BloomIntensity;
            bloom.threshold.value = BloomThreshold;
            EditorUtility.SetDirty(volume.sharedProfile);
        }
        AssetDatabase.SaveAssets();
        return $"swapped {swapped} material slots ({dims.Count} materials), bloom {BloomIntensity}/{BloomThreshold}";
    }

    private static Material DimCopy(Material source)
    {
        string path = $"{Folder}/{source.name}{Suffix}.mat";
        var dim = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (dim != null) return dim;
        dim = new Material(source) { name = source.name + Suffix };
        dim.SetColor("_EmissionColor", source.GetColor("_EmissionColor") * EmissionFactor);
        AssetDatabase.CreateAsset(dim, path);
        return dim;
    }
}
