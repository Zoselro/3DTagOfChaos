using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 새 동작 4개를 PlayerAnimator.controller에 넣는다(Request1009Plan.md §45.4 AN0~AN3).
// - "Action" 층(상체 마스크 CarryUpperBody, Carry 층보다 위): StunAim(반복)·Throw·Swing(한 번). 두 손 들기(Carry) 위를 덮는다.
// - "Trapped" 층(온몸): BalloonTrapped(반복).
// 층 무게는 코드(PlayerActionPose)가 정한다 — 빈 상태를 두지 않아 T-Pose로 덮일 일이 없다.
public static class ActionLayerBuilder
{
    public const string Dir = "Assets/Animation/Cookie/Actions/";
    public const string MaskPath = "Assets/Animation/Cookie/CarryUpperBody.mask";

    [MenuItem("Tools/Characters/Rebuild Action Layers")]
    public static void RebuildMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[ActionLayerBuilder] Stop Play Mode first."); return; }
        Debug.Log(Rebuild());
    }

    public static string Rebuild()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DanceStateBuilder.ControllerPath);
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        RemoveLayer(controller, PlayerActionPose.ActionLayer);
        RemoveLayer(controller, PlayerActionPose.TrappedLayer);

        AddLayer(controller, PlayerActionPose.ActionLayer, mask, new[]
        {
            (PlayerActionPose.StunAimState, "Cookie_StunAim"),
            (PlayerActionPose.ThrowState, "Cookie_ThrowBalloon"),
            (PlayerActionPose.SwingState, "Cookie_HammerSwing"),
        });
        AddLayer(controller, PlayerActionPose.TrappedLayer, null, new[] { (PlayerActionPose.TrappedState, "Cookie_BalloonTrapped") });
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return $"[ActionLayerBuilder] layers = {controller.layers.Length} ({string.Join(", ", System.Array.ConvertAll(controller.layers, l => l.name))}).";
    }

    private static void AddLayer(AnimatorController controller, string name, AvatarMask mask, (string state, string clip)[] states)
    {
        var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, controller);
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState s = sm.AddState(states[i].state, new Vector3(300f, 60f * i, 0f));
            s.motion = DanceStateBuilder.LoadClip(Dir + states[i].clip + ".fbx");
            s.writeDefaultValues = true;
        }
        controller.AddLayer(new AnimatorControllerLayer
        {
            name = name, stateMachine = sm, avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Override, defaultWeight = 0f,
        });
    }

    private static void RemoveLayer(AnimatorController controller, string name)
    {
        for (int i = controller.layers.Length - 1; i >= 0; i--)
            if (controller.layers[i].name == name) controller.RemoveLayer(i);
    }
}
