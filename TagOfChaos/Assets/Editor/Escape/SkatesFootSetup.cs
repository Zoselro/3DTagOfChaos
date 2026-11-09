using UnityEditor;
using UnityEngine;

// 발 스케이트 프리팹(Request1009Plan.md §69 B1): Blender ITEM_Skates_Foot(발끝 = 모델 −Z)을 뒤로 돌려 감싼다 — 프리팹 앞(+Z) = 발끝, 원점 = 발목 아래 바닥.
public static class SkatesFootSetup
{
    public const string ModelPath = "Assets/09. Environment/Escape/Models/ITEM_Skates_Foot.fbx";
    public const string PrefabPath = "Assets/Resources/Escape/SkatesFoot.prefab";

    [MenuItem("TagOfChaos/Escape/Build Skates Foot Prefab")]
    public static void Build()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) { Debug.LogError("[SkatesFoot] Model not found: " + ModelPath); return; }
        var root = new GameObject("SkatesFoot");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * inst.transform.localRotation;
        foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[SkatesFoot] Saved " + PrefabPath);
    }
}
