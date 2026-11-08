using NUnit.Framework;
using UnityEngine;

// Request1009Plan.md §68 GP2 섬광탄 · GP3 권투 글러브 · GP5 수색 폭탄 / 사이렌 함정 시험.
public class Request1108GpTests
{
    // ---------------- 도구 규칙 ----------------

    [Test]
    public void OnlyStunGunAndFlashbangHitTheMonster()
    {
        Assert.IsTrue(ToolSO.CanHitMonsterKind(ToolKind.StunGun));
        Assert.IsTrue(ToolSO.CanHitMonsterKind(ToolKind.Flashbang), "user decision: flashbang stuns the tagger too");
        Assert.IsFalse(ToolSO.CanHitMonsterKind(ToolKind.BoxingGlove), "glove: cookies only");
        Assert.IsFalse(ToolSO.CanHitMonsterKind(ToolKind.Hammer));
        Assert.IsFalse(ToolSO.CanHitMonsterKind(ToolKind.WaterBalloon));
    }

    [Test]
    public void NewItems_AreInTheCatalogWithTheAgreedNumbers()
    {
        EscapeCatalogSO c = EscapeCatalogSO.Current;
        ItemSO flash = c.Find("Flashbang"), glove = c.Find("BoxingGlove");
        Assert.IsNotNull(flash); Assert.IsNotNull(glove); Assert.IsNotNull(c.SearchBomb);
        Assert.AreEqual(ToolKind.Flashbang, flash.Tool.Kind);
        Assert.AreEqual(3f, flash.Tool.StunSecondsCookie, 1e-4f, "3 s stun");
        Assert.AreEqual(3f, flash.Tool.StunSecondsMonster, 1e-4f, "tagger included, 3 s");
        Assert.IsTrue(flash.Tool.IsThrown && flash.Tool.HitsArea && flash.Tool.UsedByClick);
        Assert.AreEqual(ToolKind.BoxingGlove, glove.Tool.Kind);
        Assert.IsFalse(glove.Tool.IsThrown);
        Assert.AreEqual(ToolKind.SearchBomb, c.SearchBomb.Tool.Kind);
        Assert.IsFalse(c.SearchBomb.Tool.UsedByClick, "monster skill, not an item in hand");
        CollectionAssert.Contains(new System.Collections.Generic.List<ItemSO>(c.CookieItems), flash, "survivor chest");
        CollectionAssert.Contains(new System.Collections.Generic.List<ItemSO>(c.CookieItems), glove, "survivor chest");
        Assert.AreEqual(c.CookieItems.Count, c.CookieItemWeights.Count);
    }

    // ---------------- 권투 글러브 ----------------

    [Test]
    public void Glove_KnocksDownOnTheThirdHitWithinThreeSeconds()
    {
        var combo = new GloveCombo();
        Assert.IsFalse(combo.Register(7, 0.0));
        Assert.IsFalse(combo.Register(7, 1.0));
        Assert.IsFalse(combo.Register(8, 1.5), "another cookie is counted apart");
        Assert.IsTrue(combo.Register(7, 2.9), "third hit inside 3 s -> knocked down");
        Assert.AreEqual(0, combo.RecentHits(7, 3.0), "starts again after the knock-down");

        var slow = new GloveCombo();
        slow.Register(5, 0.0); slow.Register(5, 2.0);
        Assert.IsFalse(slow.Register(5, 3.5), "the first hit fell out of the 3 s window");
        Assert.IsTrue(slow.Register(5, 4.0));
        Assert.AreEqual(0.2f, GloveCombo.FlinchSeconds, 1e-4f);
        Assert.AreEqual(3f, GloveCombo.KnockSeconds, 1e-4f);
    }

    // ---------------- 사이렌 함정 ----------------

    [Test]
    public void SirenCage_BreaksByMashingOrTimesOut()
    {
        Assert.IsFalse(SirenCage.ShouldRelease(SirenCage.MashNeeded - 1, 10.0, 12.0));
        Assert.IsTrue(SirenCage.ShouldRelease(SirenCage.MashNeeded, 10.0, 12.0), "mashed through");
        Assert.IsTrue(SirenCage.ShouldRelease(0, 10.0, 10.0 + SirenCage.MaxSeconds), "never stuck forever");
        Assert.AreEqual(0.5f, SirenCage.Progress(SirenCage.MashNeeded / 2), 0.02f);
        Vector3 c = new Vector3(10, 0, 10);
        Assert.IsTrue(SirenCage.Inside(new Vector3(14, 0.5f, 10), c, 5f));
        Assert.IsFalse(SirenCage.Inside(new Vector3(16, 0.5f, 10), c, 5f), "outside the ring");
        Assert.IsFalse(SirenCage.Inside(new Vector3(10, -20f, 10), c, 5f), "another floor");
        Assert.Greater(SirenCage.BarCount(20f), 60, "bars close enough to look like a cage (walls block the gaps)");
    }

    [Test]
    public void State_KeepsTheCageThroughTheNetwork()
    {
        var s = new EscapeState();
        s.Bombs.Add(new EscapeState.Bomb { Id = 4, Owner = 2, Pos = new Vector3(1, 0, 2), Building = 3, CagedAt = 55.5, Mash = 12 });
        s.Bombs.Add(new EscapeState.Bomb { Id = 5, Owner = 2, Pos = Vector3.zero, Building = -1 });
        EscapeState d = EscapeState.Decode(s.Encode());
        Assert.IsNotNull(d);
        Assert.AreEqual(3, d.Bombs[0].Building);
        Assert.AreEqual(55.5, d.Bombs[0].CagedAt, 1e-9);
        Assert.AreEqual(12, d.Bombs[0].Mash);
        Assert.IsTrue(d.Bombs[0].Caged);
        Assert.IsFalse(d.Bombs[1].Caged, "placed, not triggered yet");
        Assert.GreaterOrEqual(EscapeState.FormatVersion, 7, "v7 added the cage fields (v8 §68.7 carriages)");
    }

    // ---------------- 수색 폭탄 키 ----------------

    [Test]
    public void SearchKey_IsAMonsterKeyWithoutConflicts()
    {
        Assert.AreEqual(KeyCode.R, KeyBindings.Default(KeyAction.MonsterSearch));
        Assert.AreEqual(KeyGroup.Monster, KeyBindings.GroupOf(KeyAction.MonsterSearch));
        Assert.IsFalse(KeyBindings.HasConflict(KeyAction.MonsterSearch, KeyBindings.Default), "R is free among monster keys");
    }
}
