using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Request1009Plan.md §59: 스턴건만 괴물 명중 · Tab 참가자 목록 · 추격음 45 m/0.68 · 마우스 자유 시점 키.
public class Request1105Tests
{
    [Test]
    public void OnlyStunGunCanHitMonster()
    {
        foreach (ToolKind k in System.Enum.GetValues(typeof(ToolKind)))
            Assert.AreEqual(k == ToolKind.StunGun, ToolSO.CanHitMonsterKind(k), k.ToString());
    }

    [Test]
    public void PlayerList_SpyShownOnlyToSelfOrAfterReveal()
    {
        Assert.IsTrue(PlayerListHud.ShowSpy(3, 3, true, null), "spy sees own badge");
        Assert.IsFalse(PlayerListHud.ShowSpy(3, 2, false, null), "others do not see the spy");
        Assert.IsFalse(PlayerListHud.ShowSpy(3, 2, true, null), "another spy's row stays a cookie");
        Assert.IsTrue(PlayerListHud.ShowSpy(3, 2, false, new[] { 3 }), "revealed at the end");
    }

    [Test]
    public void PlayerList_MonstersFirstThenJoinOrder()
    {
        var rows = new List<PlayerListHud.Row>
        {
            new PlayerListHud.Row { Actor = 4 }, new PlayerListHud.Row { Actor = 2, Monster = true }, new PlayerListHud.Row { Actor = 1 }, new PlayerListHud.Row { Actor = 3 },
        };
        PlayerListHud.Sort(rows);
        CollectionAssert.AreEqual(new[] { 2, 1, 3, 4 }, rows.Select(r => r.Actor).ToArray());
    }

    [Test]
    public void Keys_PlayerListTab_ShowCursorAlt_AppendedForEveryone()
    {
        Assert.AreEqual(KeyCode.Tab, KeyBindings.Default(KeyAction.PlayerList));
        Assert.AreEqual(KeyCode.LeftAlt, KeyBindings.Default(KeyAction.ShowCursor));
        Assert.AreEqual(KeyAction.ShowCursor, KeyBindings.All[KeyBindings.All.Length - 1]);
        Assert.AreEqual(KeyGroup.All, KeyBindings.GroupOf(KeyAction.PlayerList));
        Assert.AreEqual(KeyGroup.All, KeyBindings.GroupOf(KeyAction.ShowCursor));
        Assert.IsFalse(KeyBindings.HasConflict(KeyAction.PlayerList, KeyBindings.Default), "Tab is free");
        Assert.IsFalse(KeyBindings.HasConflict(KeyAction.ShowCursor, KeyBindings.Default), "Alt is free");
    }

    [Test]
    public void ChaseAudio_45m_Volume068_DecoysStillFleeAt30()
    {
        ChaseAudioSettingsSO s = Resources.Load<ChaseAudioSettingsSO>(ChaseAudioSettingsSO.ResourcePath);
        Assert.NotNull(s);
        Assert.AreEqual(45f, s.AudibleRadius, 0.01f);
        Assert.AreEqual(0.68f, s.MaxVolume, 0.001f);
        Assert.AreEqual(11.25f, s.FullRadius, 0.01f, "full-volume radius scales with the audible radius");
        Assert.AreEqual(30f, DecoyRules.FleeRadius, "decoys keep the old flee distance");
    }

    // 괴물 엘리베이터는 준비 시간에 타지 못한다(§62)
    [Test]
    public void MonsterElevator_ClosedDuringPrepare()
    {
        Assert.IsFalse(MonsterElevator.PhaseAllows(GamePhase.Prepare));
        Assert.IsTrue(MonsterElevator.PhaseAllows(GamePhase.Hunt));
        Assert.IsTrue(MonsterElevator.PhaseAllows(GamePhase.TimeAttack));
        Assert.IsFalse(MonsterElevator.PhaseAllows(GamePhase.Result));
        Assert.IsFalse(MonsterElevator.PhaseAllows(GamePhase.Lobby));
    }
}
