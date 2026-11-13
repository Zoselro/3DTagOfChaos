using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 새 동작을 PlayerAnimator.controller에 넣는다(Request1009Plan.md §45.4 AN0~AN3, §74 P13, §75).
// - "Hold" 층(오른팔 마스크 RightArm, Carry 층 위): ItemHold(반복) — 한 손 아이템을 가슴 옆 앞에(§75 D1).
// - "Action" 층(상체 마스크 CarryUpperBody, Hold 층 위): StunAim·HammerHold·GloveGuard(반복)·Throw·Swing·PunchR·PunchL(한 번).
// - "Trapped" 층(온몸): BalloonTrapped(반복).
// - Base 층: 조준 방향을 본 채 걷는 옆걸음·뒷걸음(StrafeLeft·StrafeRight·WalkBack, §75 Q2 — 뒷걸음은 걷기를 거꾸로, §75.13) — 다른 이동 상태처럼 같은 이름의 트리거와 Any State 전환.
// 층 무게는 코드(PlayerActionPose)가 정한다 — 빈 상태를 두지 않아 T-Pose로 덮일 일이 없다. 여러 번 실행해도 같은 결과.
public static class ActionLayerBuilder
{
    public const string Dir = "Assets/Animation/Cookie/Actions/";
    public const string MaskPath = "Assets/Animation/Cookie/CarryUpperBody.mask";
    public const string RightArmMaskPath = "Assets/Animation/Cookie/RightArm.mask";
    private const float MoveBlend = 0.12f;
    private const float AimWalkSpeed = 1.5f;
    public const string WalkClipPath = "Assets/Animation/Cookie/Cookie_Walking.fbx";

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
        AvatarMask rightArm = EnsureRightArmMask();
        RemoveLayer(controller, PlayerActionPose.HoldLayer);
        RemoveLayer(controller, PlayerActionPose.ActionLayer);
        RemoveLayer(controller, PlayerActionPose.TrappedLayer);

        AddLayer(controller, PlayerActionPose.HoldLayer, rightArm, new[] { (PlayerActionPose.ItemHoldState, "Cookie_ItemHold") });
        AddLayer(controller, PlayerActionPose.ActionLayer, mask, new[]
        {
            (PlayerActionPose.StunAimState, "Cookie_StunAim"),
            (PlayerActionPose.ThrowState, "Cookie_ThrowBalloon"),
            (PlayerActionPose.SwingState, "Cookie_HammerSwing"),       // 어깨 → 머리 위 → 내려찍기 → 어깨(§75 A2)
            (PlayerActionPose.HammerHoldState, "Cookie_HammerHold"),   // 오른쪽 어깨에 걸친 자세(반복, §74 P13)
            (PlayerActionPose.GloveGuardState, "Cookie_GloveGuard"),   // 글러브 가드(반복, §75 C1)
            (PlayerActionPose.PunchRState, "Cookie_PunchR"),
            (PlayerActionPose.PunchLState, "Cookie_PunchL"),
        });
        AddLayer(controller, PlayerActionPose.TrappedLayer, null, new[] { (PlayerActionPose.TrappedState, "Cookie_BalloonTrapped") });
        int moves = BuildAimWalkStates(controller);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return $"[ActionLayerBuilder] layers = {controller.layers.Length} ({string.Join(", ", System.Array.ConvertAll(controller.layers, l => l.name))}), aim-walk states = {moves}.";
    }

    // Base 층 옆걸음·뒷걸음: 상태 이름 = PlayerMoveState 이름 = 트리거 이름(PlayerAnimationDriver.ChangeState가 SetTrigger로 부른다).
    private static int BuildAimWalkStates(AnimatorController controller)
    {
        // 뒷걸음 = 앞으로 걷기(Cookie_Walking)를 거꾸로(§75.13 — 따로 만든 클립은 발목이 꺾여 어색했다). 옆걸음은 발을 바닥에 평평하게 둔 새 클립.
        var moves = new[]
        {
            (PlayerMoveState.StrafeLeft, Dir + "Cookie_StrafeLeft.fbx", AimWalkSpeed),
            (PlayerMoveState.StrafeRight, Dir + "Cookie_StrafeRight.fbx", AimWalkSpeed),
            (PlayerMoveState.WalkBack, WalkClipPath, -1f),
        };
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        int made = 0;
        for (int i = 0; i < moves.Length; i++)
        {
            string name = moves[i].Item1.ToString();
            EnsureParameter(controller, name, AnimatorControllerParameterType.Trigger);
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
                if (t.destinationState != null && t.destinationState.name == name) sm.RemoveAnyStateTransition(t);
            foreach (ChildAnimatorState s in sm.states)
                if (s.state.name == name) sm.RemoveState(s.state);
            AnimatorState state = sm.AddState(name, new Vector3(450f, 400f + 60f * i, 0f));
            state.motion = DanceStateBuilder.LoadClip(moves[i].Item2);
            state.writeDefaultValues = true;
            state.speed = moves[i].Item3; // 옆걸음 1.5배(발이 덜 미끄러지게), 뒷걸음 −1 = 걷기와 같은 빠르기로 거꾸로
            AnimatorStateTransition any = sm.AddAnyStateTransition(state);
            any.canTransitionToSelf = true; // Walk와 같게(같은 트리거가 다시 와도 이어진다)
            any.hasExitTime = false;
            any.duration = MoveBlend;
            any.AddCondition(AnimatorConditionMode.If, 0f, name);
            if (state.motion != null) made++;
        }
        return made;
    }

    // 오른팔(어깨 아래)·오른손만 덮는 마스크 — 걷는 동안 몸·왼팔은 이동 동작 그대로.
    private static AvatarMask EnsureRightArmMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(RightArmMaskPath);
        bool create = mask == null;
        if (create) mask = new AvatarMask();
        for (AvatarMaskBodyPart p = 0; p < AvatarMaskBodyPart.LastBodyPart; p++)
            mask.SetHumanoidBodyPartActive(p, p == AvatarMaskBodyPart.RightArm || p == AvatarMaskBodyPart.RightFingers || p == AvatarMaskBodyPart.RightHandIK);
        if (create) AssetDatabase.CreateAsset(mask, RightArmMaskPath);
        else EditorUtility.SetDirty(mask);
        return mask;
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

    private static void EnsureParameter(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in c.parameters) if (p.name == name) return;
        c.AddParameter(name, type);
    }
}
