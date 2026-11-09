using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Request1009Plan.md §69 (37개 요청) 시험 — 순수 값·규칙과 에셋 연결만 본다(Play Mode 동작은 계획서의 검증 기록).
public class Request1109Tests
{
    [Test]
    public void Voice_SoftClipKeepsQuietAndLimitsLoud()
    {
        Assert.AreEqual(0.1f, VoiceGain.SoftClip(0.1f), 0.005f, "quiet voice almost unchanged");
        Assert.LessOrEqual(VoiceGain.SoftClip(3f), 1f);
        Assert.GreaterOrEqual(VoiceGain.SoftClip(-10f), -1f);
        Assert.Greater(VoiceGain.SoftClip(0.9f), VoiceGain.SoftClip(0.5f), "still rises");
    }

    [Test]
    public void CookieControls_Camera_Struggle_Dodge()
    {
        Assert.AreEqual(4.5f, HideOrSeekPlayer.CookieCameraDistance, 1e-4f);
        Assert.AreEqual(10, HideOrSeekPlayer.StrugglePresses);
        Assert.AreEqual(2.5f, HideOrSeekPlayer.StruggleWindow, 1e-4f);
    }

    [Test]
    public void Monster_DashGrabNumbers()
    {
        GameSettingsSO s = AssetDatabase.LoadAssetAtPath<GameSettingsSO>("Assets/Resources/GameSettings.asset");
        Assert.AreEqual(5f, s.TentacleDashCooldown, 1e-4f, "dash cooldown 15 -> 5");
        Assert.AreEqual(0.294f, s.TentacleDashDuration, 1e-3f, "15% slower");
        Assert.AreEqual(3f, s.GrabReachFromFront, 1e-4f, "grab reach 2 -> 3");
        Assert.AreEqual(0.35f, MonsterController.DashWindupSeconds, 1e-4f);
        Assert.AreEqual(0.4f, MonsterController.DashRecoverSeconds, 1e-4f);
        Assert.AreEqual(3, EscapeAuthority.BombMaxCharges, "siren traps up to 3");
        Assert.AreEqual(3f, EscapeAuthority.SearchCooldown, 1e-4f, "search bomb 25 -> 3 s");
        Assert.AreEqual(25, SirenCage.MashNeeded, "space x25 breaks the cage");
    }

    [Test]
    public void Keys_MonsterGrabIsLeftMouseInMonsterGroup()
    {
        Assert.AreEqual(KeyCode.Mouse0, KeyBindings.Default(KeyAction.MonsterGrab));
        Assert.AreEqual(KeyGroup.Monster, KeyBindings.GroupOf(KeyAction.MonsterGrab));
        Assert.IsTrue((KeyBindings.GroupOf(KeyAction.Jump) & KeyGroup.Monster) != 0, "monster can jump");
        Assert.IsFalse(KeyBindings.HasConflict(KeyAction.MonsterGrab, KeyBindings.Default), "left mouse is free in the monster group");
    }

    [Test]
    public void Items_FlashHatAndGreenRune()
    {
        Assert.AreEqual(5f, FlashbangFx.BlindSeconds, 1e-4f);
        Assert.Greater(HatFlight.RiseSpeed, 2f);
        EscapeCatalogSO c = EscapeCatalogSO.Current;
        ItemSO green = c.Find("Rune_Green");
        Assert.IsNotNull(green, "pink rune became green");
        Assert.IsNull(c.Find("Rune_Pink"));
        Color.RGBToHSV(green.Tint, out float h, out _, out _);
        Assert.That(h * 360f, Is.InRange(90f, 160f), "green hue");
        Assert.IsNotNull(green.Icon);
        Assert.IsNotNull(green.ModelPrefab);
        var building = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04. Prefabs/Environment/GingerbreadBuildings/GIN_RuneBuilding.prefab");
        bool hasGreen = false, hasPink = false;
        foreach (Transform t in building.GetComponentsInChildren<Transform>(true))
        {
            hasGreen |= t.name == "Var_Rune_Green";
            hasPink |= t.name == "Var_Rune_Pink";
        }
        Assert.IsTrue(hasGreen); Assert.IsFalse(hasPink);
    }

    [Test]
    public void Blender_AssetsAreWired()
    {
        Assert.IsNotNull(Resources.Load<GameObject>(SkateFeetView.PrefabPath), "foot skates");
        Assert.IsNotNull(Resources.Load<GameObject>(MonsterElevatorMarker.SignPath), "elevator sign");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MonsterWindupClipBuilder.ClipPath);
        Assert.IsNotNull(clip, "dash windup clip");
        Assert.AreEqual(0.33f, clip.length, 0.02f);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(MonsterWindupClipBuilder.ControllerPath);
        bool state = false;
        foreach (ChildAnimatorState s in ctrl.layers[0].stateMachine.states) state |= s.state.name == MonsterMoveState.TentacleDashWindup.ToString() && s.state.motion == clip;
        Assert.IsTrue(state, "animator state matches the enum name");
        var player = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animation/PlayerAnimator.controller");
        Assert.IsTrue(player.layers[0].iKPass, "skates lift the body in OnAnimatorIK");
    }

    [Test]
    public void SirenTrap_OnlyNearMaterialBuildings_TextsExist()
    {
        Assert.AreEqual(12f, EscapeAuthority.BombBuildingRadius, 1e-4f);
        var texts = AssetDatabase.LoadAssetAtPath<EscapeTextsSO>("Assets/Resources/Escape/EscapeTexts.asset");
        Assert.IsFalse(string.IsNullOrEmpty(texts.bombNeedsBuilding));
        Assert.IsFalse(string.IsNullOrEmpty(texts.carryStruggle));
        Assert.IsFalse(string.IsNullOrEmpty(texts.hostChanged));
    }
}
