using UnityEditor;
using UnityEngine;

// 술래 엘리베이터 표지판 프리팹(Request1009Plan.md §69 D4): Blender PROP_ElevatorSign(앞 = 모델 −Z)을 뒤로 돌려 감싼다 — 프리팹 앞(+Z) = 화살표 면.
public static class ElevatorSignSetup
{
    public const string ModelPath = "Assets/09. Environment/Escape/Models/PROP_ElevatorSign.fbx";
    public const string PrefabPath = "Assets/Resources/Escape/ElevatorSign.prefab";

    [MenuItem("TagOfChaos/Escape/Build Elevator Sign Prefab")]
    public static void Build()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) { Debug.LogError("[ElevatorSign] Model not found: " + ModelPath); return; }
        var root = new GameObject("ElevatorSign");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * inst.transform.localRotation;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[ElevatorSign] Saved " + PrefabPath);
    }
}
