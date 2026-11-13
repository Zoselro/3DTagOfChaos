using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Request1009Plan.md §75 — 아이템 드는 모습·쓰는 동작(옆걸음·원투·애니메이터 상태).
public class Request1115Tests
{
    // Q2·Q6: 조준 방향을 본 채 움직이면 몸 기준 앞(±60°) = 걷기, 뒤 = 뒷걸음, 나머지 = 옆걸음.
    [Test]
    public void AimMotion_ClassifiesMoveAgainstFacing()
    {
        Vector3 face = Vector3.forward;
        Assert.AreEqual(PlayerMoveState.Walk, AimMotion.Classify(Vector3.forward, face));
        Assert.AreEqual(PlayerMoveState.Walk, AimMotion.Classify(new Vector3(0.5f, 0f, 1f), face), "26° off: still forward");
        Assert.AreEqual(PlayerMoveState.WalkBack, AimMotion.Classify(Vector3.back, face));
        Assert.AreEqual(PlayerMoveState.StrafeRight, AimMotion.Classify(Vector3.right, face));
        Assert.AreEqual(PlayerMoveState.StrafeLeft, AimMotion.Classify(Vector3.left, face));
        Assert.AreEqual(PlayerMoveState.Idle, AimMotion.Classify(Vector3.zero, face));
        // 몸이 오른쪽(+X)을 보면 화면 앞(+Z)으로 걷는 것은 왼쪽 옆걸음
        Assert.AreEqual(PlayerMoveState.StrafeLeft, AimMotion.Classify(Vector3.forward, Vector3.right));
        Assert.IsTrue(AimMotion.IsAimWalk(PlayerMoveState.WalkBack));
        Assert.IsFalse(AimMotion.IsAimWalk(PlayerMoveState.Walk));
    }

    // C1: 첫 주먹은 오른손, 이어 치면 왼손·오른손 번갈아, 1.5초 쉬면 다시 오른손.
    [Test]
    public void Punch_AlternatesRightThenLeft()
    {
        Assert.IsFalse(AimMotion.NextPunchIsLeft(false, 99f), "first punch: right");
        Assert.IsTrue(AimMotion.NextPunchIsLeft(true, 0.5f), "after right: left");
        Assert.IsFalse(AimMotion.NextPunchIsLeft(false, 0.5f), "after left: right");
        Assert.IsFalse(AimMotion.NextPunchIsLeft(true, AimMotion.PunchComboWindow + 0.1f), "pause: right again");
    }

    // 동기화는 상태를 int로 보낸다 — 새 이동 상태는 끝에 붙어 옛 번호가 바뀌지 않는다.
    [Test]
    public void MoveState_NewValuesAppended()
    {
        Assert.AreEqual(8, (int)PlayerMoveState.Dance);
        Assert.AreEqual(9, (int)PlayerMoveState.StrafeLeft);
        Assert.AreEqual(10, (int)PlayerMoveState.StrafeRight);
        Assert.AreEqual(11, (int)PlayerMoveState.WalkBack);
    }

    // 애니메이터: Hold 층(오른팔) < Action 층, 새 상태·클립·트리거가 모두 연결돼 있다.
    [Test]
    public void Controller_HasNewLayersStatesAndClips()
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(DanceStateBuilder.ControllerPath);
        int carry = -1, hold = -1, action = -1;
        for (int i = 0; i < c.layers.Length; i++)
        {
            if (c.layers[i].name == "Carry") carry = i;
            if (c.layers[i].name == PlayerActionPose.HoldLayer) hold = i;
            if (c.layers[i].name == PlayerActionPose.ActionLayer) action = i;
        }
        Assert.That(carry < hold && hold < action, $"layer order carry {carry} < hold {hold} < action {action}");
        Assert.NotNull(c.layers[hold].avatarMask, "right arm mask");
        AssertState(c.layers[hold].stateMachine, PlayerActionPose.ItemHoldState);
        foreach (string s in new[] { PlayerActionPose.SwingState, PlayerActionPose.HammerHoldState, PlayerActionPose.GloveGuardState, PlayerActionPose.PunchRState, PlayerActionPose.PunchLState })
            AssertState(c.layers[action].stateMachine, s);
        foreach (PlayerMoveState m in new[] { PlayerMoveState.StrafeLeft, PlayerMoveState.StrafeRight, PlayerMoveState.WalkBack })
        {
            AssertState(c.layers[0].stateMachine, m.ToString());
            Assert.IsTrue(System.Array.Exists(c.parameters, p => p.name == m.ToString()), "trigger " + m);
        }
        // §75.13: 뒷걸음은 앞걷기 클립을 거꾸로
        foreach (ChildAnimatorState s in c.layers[0].stateMachine.states)
            if (s.state.name == PlayerMoveState.WalkBack.ToString())
            {
                Assert.AreEqual("Cookie_Walking", s.state.motion.name);
                Assert.Less(s.state.speed, 0f, "played backward");
            }
    }

    // 클립 길이가 코드의 시각과 맞는다(닿는 순간 판정).
    [Test]
    public void Clips_MatchCodeTiming()
    {
        Assert.AreEqual(PlayerActionPose.SwingSeconds, Clip("Cookie_HammerSwing").length, 0.02f);
        Assert.AreEqual(PlayerActionPose.PunchSeconds, Clip("Cookie_PunchR").length, 0.02f);
        Assert.AreEqual(PlayerActionPose.PunchSeconds, Clip("Cookie_PunchL").length, 0.02f);
        Assert.Less(ToolUser.HammerSweepFrom, PlayerActionPose.SwingHit);
        Assert.Greater(ToolUser.HammerSweepTo, PlayerActionPose.SwingHit);
        Assert.Less(ToolUser.GloveSweepFrom, PlayerActionPose.PunchHit);
        Assert.Greater(ToolUser.GloveSweepTo, PlayerActionPose.PunchHit);
        foreach (string loop in new[] { "Cookie_HammerHold", "Cookie_GloveGuard", "Cookie_ItemHold", "Cookie_StrafeLeft", "Cookie_StrafeRight" })
            Assert.IsTrue(Clip(loop).isLooping, loop + " loops");
    }

    // §75.12: 머리와 안 겹치면 그대로, 겹치면 쥔 곳을 축으로 비켜 세운 뒤에는 트여 있다(큰 머리 캐릭터).
    [Test]
    public void HammerClearance_TiltsOnlyWhenHeadIsInTheWay()
    {
        Vector3 head = new Vector3(0f, 1.4f, 0f), radii = new Vector3(0.75f, 0.63f, 0.7f); // 마카롱 크기 머리
        Vector3 grip = new Vector3(0.25f, 0.7f, 0.1f);
        Vector3 clearUp = new Vector3(0.65f, 0f, 0.6f);  // 앞 오른쪽으로 뻗은 망치
        Assert.AreEqual(Quaternion.identity, HammerClearance.FindClear(grip, clearUp, 0.3f, head, radii));
        Vector3 overHead = new Vector3(-0.1f, 0.65f, 0f); // 머리 속으로 세운 망치
        Assert.IsTrue(HammerClearance.Hits(grip, overHead, 0.3f, head, radii));
        Quaternion q = HammerClearance.FindClear(grip, overHead, 0.3f, head, radii);
        Assert.AreNotEqual(Quaternion.identity, q);
        Assert.IsFalse(HammerClearance.Hits(grip, q * overHead, 0.3f, head, radii), "tilted hammer is clear");
        Assert.IsTrue(HammerClearance.Overlaps(head, 0.01f, head, radii));
        Assert.IsFalse(HammerClearance.Overlaps(head + Vector3.right * 1.2f, 0.3f, head, radii));
    }

    private static AnimationClip Clip(string name)
    {
        AnimationClip c = DanceStateBuilder.LoadClip(ActionLayerBuilder.Dir + name + ".fbx");
        Assert.NotNull(c, name);
        return c;
    }

    private static void AssertState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState s in sm.states)
            if (s.state.name == name) { Assert.NotNull(s.state.motion, name + " clip"); return; }
        Assert.Fail("state " + name);
    }
}
