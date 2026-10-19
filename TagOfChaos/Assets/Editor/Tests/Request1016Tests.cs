using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 2026-10-16 요청(Request1009Plan.md §26·§27) 시험.
public class Request1016Tests
{
    // §27 K1: 겹침은 같은 묶음 안에서만 경고 — 쿠키 달리기(Shift)와 괴물 돌진(Shift)은 괜찮고, 쿠키 점프를 E로 바꾸면 상호작용과 겹친다.
    [Test]
    public void KeyBindings_ConflictsOnlyWithinTheSameGroup()
    {
        var keys = new Dictionary<KeyAction, KeyCode>();
        foreach (KeyAction a in KeyBindings.All) keys[a] = KeyBindings.Default(a);
        foreach (KeyAction a in KeyBindings.All)
            Assert.IsFalse(KeyBindings.HasConflict(a, x => keys[x]), "defaults must be clean: " + a);

        Assert.AreEqual(keys[KeyAction.Run], keys[KeyAction.MonsterDash], "same Shift, different groups");
        keys[KeyAction.Jump] = KeyCode.E;
        Assert.IsTrue(KeyBindings.HasConflict(KeyAction.Jump, x => keys[x]));
        Assert.IsTrue(KeyBindings.HasConflict(KeyAction.Interact, x => keys[x]));
        Assert.IsFalse(KeyBindings.HasConflict(KeyAction.MonsterSkill, x => keys[x]));

        keys[KeyAction.Jump] = KeyCode.Q; // 괴물 스킬과 같지만 쿠키 전용 키라 괜찮다
        Assert.IsFalse(KeyBindings.HasConflict(KeyAction.Jump, x => keys[x]));
        keys[KeyAction.SpectateNext] = KeyCode.Mouse1; // 관전에서는 카메라 회전과 겹친다
        Assert.IsTrue(KeyBindings.HasConflict(KeyAction.SpectateNext, x => keys[x]));
    }

    // §26.9 Q24: 판 중 탈출·부서진 사람의 채팅은 같은 처지에게만, 판이 끝나면 모두에게
    [TestCase(true, true, false, false)]
    [TestCase(true, true, true, true)]
    [TestCase(true, false, false, true)]
    [TestCase(true, false, true, true)]
    [TestCase(false, true, false, true)]
    public void Chat_OutPlayersTalkOnlyToEachOtherDuringTheRound(bool round, bool senderOut, bool viewerOut, bool visible)
    {
        Assert.AreEqual(visible, ChatRules.IsVisible(round, senderOut, viewerOut));
    }

    // 녹음: 마이크 주파수(48 kHz 등)로 녹음한 것을 8 kHz로 줄인다 — 길이는 비율대로, 소리는 구간 평균
    [Test]
    public void VoiceRecorder_DownsamplesToStoreRate()
    {
        var src = new float[48000];
        for (int i = 0; i < src.Length; i++) src[i] = 0.5f;
        float[] dst = VoiceRecorderPanel.Downsample(src, 48000, VoiceStore.SampleRate);
        Assert.AreEqual(VoiceStore.SampleRate, dst.Length);
        Assert.AreEqual(0.5f, dst[100], 1e-5f);
        Assert.AreSame(src, VoiceRecorderPanel.Downsample(src, 8000, 8000), "already at the store rate");
    }

    // 작게 녹음된 목소리는 키우고(최대 12배), 거의 무음은 그대로 둔다
    [Test]
    public void VoiceRecorder_NormalizesQuietRecording()
    {
        float[] quiet = { 0.05f, -0.02f, 0.01f };
        VoiceRecorderPanel.Normalize(quiet);
        Assert.AreEqual(0.6f, quiet[0], 1e-4f, "0.05 x 12 (gain capped)");
        float[] normal = { 0.425f, -0.1f };
        VoiceRecorderPanel.Normalize(normal);
        Assert.AreEqual(0.85f, normal[0], 1e-4f);
        float[] silent = { 0.001f };
        VoiceRecorderPanel.Normalize(silent);
        Assert.AreEqual(0.001f, silent[0], 1e-6f);
    }

    // "'닉네임'이(가) 잡혔습니다!" 조사: 받침 있음 → 이, 없음 → 가, 한글이 아니면 → 이(가)
    [Test]
    public void CookieCaught_PicksParticleFromNickname()
    {
        var t = ScriptableObject.CreateInstance<EscapeTextsSO>();
        t.cookieCaughtWithFinal = "W{0}"; t.cookieCaughtNoFinal = "N{0}"; t.cookieCaughtOther = "O{0}";
        Assert.AreEqual("OPlayer1", EscapeHud.CookieCaughtText(t, "Player1"));
        Assert.AreEqual("W\uAE40\uBC25", EscapeHud.CookieCaughtText(t, "\uAE40\uBC25"));
        Assert.AreEqual("N\uCFE0\uD0A4", EscapeHud.CookieCaughtText(t, "\uCFE0\uD0A4"));
        Object.DestroyImmediate(t);
    }

    // §27 후속: "기본값으로"는 확인창(예/아니오)을 거친다
    [Test]
    public void SettingsMenu_ResetAsksFirst()
    {
        var menu = UnityEditor.AssetDatabase.LoadAssetAtPath<SettingsMenu>(SettingsMenuInstaller.PrefabPath);
        var page = menu.GetComponentInChildren<KeyBindingsPage>(true);
        var so = new UnityEditor.SerializedObject(page);
        var confirm = so.FindProperty("resetConfirm").objectReferenceValue as GameObject;
        Assert.IsNotNull(confirm, "confirm dialog");
        Assert.IsFalse(confirm.activeSelf, "hidden until asked");
        Assert.IsNotNull(so.FindProperty("resetYesButton").objectReferenceValue);
        Assert.IsNotNull(so.FindProperty("resetNoButton").objectReferenceValue);
    }

    [Test]
    public void KeyBindings_EscapeIsNeverBindable_AndDefaultsMatchThePlan()
    {
        Assert.IsFalse(KeyBindings.CanBind(KeyCode.Escape));
        Assert.IsFalse(KeyBindings.CanBind(KeyCode.None));
        Assert.IsTrue(KeyBindings.CanBind(KeyCode.Mouse0));
        Assert.AreEqual(KeyCode.W, KeyBindings.Default(KeyAction.MoveForward));
        Assert.AreEqual(KeyCode.D, KeyBindings.Default(KeyAction.MoveRight));
        Assert.AreEqual(KeyCode.Mouse0, KeyBindings.Default(KeyAction.UseTool));
        Assert.AreEqual(KeyCode.Mouse1, KeyBindings.Default(KeyAction.CameraRotate));
        Assert.AreEqual(KeyCode.T, KeyBindings.Default(KeyAction.PushToTalk));
        Assert.AreEqual(KeyCode.Alpha3, KeyBindings.Default(KeyAction.Slot3));
        Assert.AreEqual("LMB", KeyBindings.DisplayName(KeyCode.Mouse0));
        Assert.AreEqual("2", KeyBindings.DisplayName(KeyCode.Alpha2));
    }
}
