using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 춤 목록(DanceCatalogSO)을 읽어 PlayerAnimator.controller의 Base 레이어에 춤 상태를 다시 만든다(Request1009Plan.md §44.9).
// - int DanceId + Dance 트리거. 춤마다 상태 "Dance_<id>"와 Any State → Dance_<id>(Dance 트리거 && DanceId == id).
// - 빠져나가기는 기존 Any State 전환(Idle·Walk·Run·Jump·Dodge…)이 그대로 맡는다.
// 춤 추가 = 클립을 만들고 목록에 한 줄 + 이 메뉴 한 번.
public static class DanceStateBuilder
{
    public const string ControllerPath = "Assets/Animation/PlayerAnimator.controller";
    public const string CatalogPath = "Assets/Resources/Dance/DanceCatalog.asset";
    public const string FirstClipPath = "Assets/Animation/Cookie/Dance/Cookie_Dance_HandsUp.fbx";
    private const float BlendIn = 0.15f;

    [MenuItem("Tools/Characters/Rebuild Dance States")]
    public static void RebuildMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[DanceStateBuilder] Stop Play Mode first."); return; }
        Debug.Log(Rebuild());
    }

    public static string Rebuild()
    {
        DanceCatalogSO catalog = EnsureCatalog();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) return "[DanceStateBuilder] PlayerAnimator.controller not found.";

        EnsureParameter(controller, DanceCatalogSO.IdParameter, AnimatorControllerParameterType.Int);
        EnsureParameter(controller, DanceCatalogSO.Trigger, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            if (t.destinationState != null && t.destinationState.name.StartsWith(DanceCatalogSO.StatePrefix)) sm.RemoveAnyStateTransition(t);
        foreach (ChildAnimatorState s in sm.states)
            if (s.state.name.StartsWith(DanceCatalogSO.StatePrefix)) sm.RemoveState(s.state);

        int made = 0;
        int row = 0;
        foreach (DanceCatalogSO.DanceEntry d in catalog.Dances)
        {
            if (d == null || d.clip == null) continue;
            AnimatorState state = sm.AddState(DanceCatalogSO.StateName(d.id), new Vector3(650f, 60f * row++, 0f));
            state.motion = d.clip;
            state.writeDefaultValues = true;
            AnimatorStateTransition any = sm.AddAnyStateTransition(state);
            any.canTransitionToSelf = false;
            any.hasExitTime = false;
            any.duration = BlendIn;
            any.AddCondition(AnimatorConditionMode.If, 0f, DanceCatalogSO.Trigger);
            any.AddCondition(AnimatorConditionMode.Equals, d.id, DanceCatalogSO.IdParameter);
            made++;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return $"[DanceStateBuilder] {made} dance state(s) built in {ControllerPath}.";
    }

    // 목록이 없으면 첫 춤(두 손 들고 허리 흔들기)으로 만든다. 화면에 보일 이름은 에셋에서 입력한다(코드에 한글 없음).
    public static DanceCatalogSO EnsureCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DanceCatalogSO>(CatalogPath);
        if (catalog != null) return catalog;
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Dance")) AssetDatabase.CreateFolder("Assets/Resources", "Dance");
        catalog = ScriptableObject.CreateInstance<DanceCatalogSO>();
        catalog.EditorSet(new List<DanceCatalogSO.DanceEntry>
        {
            new DanceCatalogSO.DanceEntry { id = 0, label = "HandsUp", clip = LoadClip(FirstClipPath), loop = true, aiWeight = 1, playerUsable = true },
        });
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    public static AnimationClip LoadClip(string fbxPath)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
        return null;
    }

    private static void EnsureParameter(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in c.parameters) if (p.name == name) return;
        c.AddParameter(name, type);
    }
}
