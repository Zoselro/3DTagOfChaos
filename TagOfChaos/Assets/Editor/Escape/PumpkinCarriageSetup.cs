using UnityEditor;
using UnityEngine;

// 호박 마차 탈것 프리팹 만들기(Request1009Plan.md §68.7): Blender FBX(몸통 + 경첩이 원점인 문) → Resources/Escape/PumpkinCarriage.prefab.
// 몸통·문에 메시 충돌체(문이 열린 틈으로만 드나듦), 루트에 PumpkinCarriageView. 실행 중 소환은 PumpkinCarriagePresenter가 이 프리팹을 쓴다.
// BuildVehicle은 콜라 대포(ColaCannonSetup, §68.9)도 같이 쓴다.
public static class PumpkinCarriageSetup
{
    public const string ModelPath = "Assets/09. Environment/Escape/Models/PROP_PumpkinCarriage.fbx";
    public const string PrefabPath = "Assets/Resources/Escape/PumpkinCarriage.prefab";

    [MenuItem("TagOfChaos/Escape/Build Pumpkin Carriage Prefab")]
    public static void BuildMenu() => Debug.Log(Build() ? "[PumpkinCarriage] Prefab built: " + PrefabPath : "[PumpkinCarriage] Build failed.");

    public static bool Build() => BuildVehicle<PumpkinCarriageView>(ModelPath, PrefabPath, "PumpkinCarriage");

    // 탈것 프리팹: 빈 루트 아래 FBX(부품 계층 그대로), 메시마다 메시 충돌체, 루트에 TView.
    public static bool BuildVehicle<TView>(string modelPath, string prefabPath, string rootName) where TView : Component
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { Debug.LogWarning("[Vehicle] Model not found: " + modelPath); return false; }
        var root = new GameObject(rootName);
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var col = mf.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
            }
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            root.AddComponent<TView>();
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Escape")) AssetDatabase.CreateFolder("Assets/Resources", "Escape");
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return true;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
