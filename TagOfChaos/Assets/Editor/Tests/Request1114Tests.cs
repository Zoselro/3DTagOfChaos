using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Request1009Plan.md §74 — 플레이 피드백 20건(규칙·값 시험).
public class Request1114Tests
{
    private static EscapeState StateWithBuildings(params bool[] active)
    {
        var s = new EscapeState();
        foreach (bool a in active) s.Buildings.Add(new EscapeState.Building { MaterialId = "M", Active = a, ItemIndex = -1 });
        return s;
    }

    // P12: 쓰는 재료 건물 하나에 함정 하나. 재료가 꽂힌 건물·쓰지 않는 건물·건물 밖은 안 된다.
    [Test]
    public void CanPlaceBomb_OnePerActiveBuildingWithoutMaterial()
    {
        EscapeState s = StateWithBuildings(true, false, true);
        Assert.AreEqual(EscapeRules.BombPlace.Ok, EscapeRules.CanPlaceBomb(s, 0));
        Assert.AreEqual(EscapeRules.BombPlace.NoBuilding, EscapeRules.CanPlaceBomb(s, -1));
        Assert.AreEqual(EscapeRules.BombPlace.NoBuilding, EscapeRules.CanPlaceBomb(s, 9));
        Assert.AreEqual(EscapeRules.BombPlace.InactiveBuilding, EscapeRules.CanPlaceBomb(s, 1));

        s.Bombs.Add(new EscapeState.Bomb { Id = 1, Site = 0, Building = -1 });
        Assert.AreEqual(EscapeRules.BombPlace.AlreadyThere, EscapeRules.CanPlaceBomb(s, 0));
        Assert.AreEqual(EscapeRules.BombPlace.Ok, EscapeRules.CanPlaceBomb(s, 2), "another building is free");

        EscapeState.Bomb spent = s.Bombs[0]; spent.Spent = true; s.Bombs[0] = spent;
        Assert.AreEqual(EscapeRules.BombPlace.Ok, EscapeRules.CanPlaceBomb(s, 0), "a spent trap frees the building");

        EscapeState.Building b = s.Buildings[2]; b.ItemIndex = 3; s.Buildings[2] = b;
        Assert.AreEqual(EscapeRules.BombPlace.MaterialInstalled, EscapeRules.CanPlaceBomb(s, 2));
        b.ItemIndex = -1; s.Buildings[2] = b; // 스파이가 재료를 빼면 다시 놓을 수 있다
        Assert.AreEqual(EscapeRules.BombPlace.Ok, EscapeRules.CanPlaceBomb(s, 2));
    }

    // P12: 놓은 건물(Site)이 상태에 실려 모든 화면이 같은 규칙으로 본다.
    [Test]
    public void BombSite_SurvivesEncodeDecode()
    {
        EscapeState s = StateWithBuildings(true);
        s.Bombs.Add(new EscapeState.Bomb { Id = 7, Owner = 2, Site = 0, Building = -1 });
        EscapeState back = EscapeState.Decode(s.Encode());
        Assert.AreEqual(1, back.Bombs.Count);
        Assert.AreEqual(0, back.Bombs[0].Site);
        Assert.AreEqual(7, back.Bombs[0].Id);
    }

    // P3: 로켓 칸 재료는 칸마다 다르고, 쿠키 건물이 쓰지 않는 재료를 먼저 고른다.
    [Test]
    public void PickRocketMaterial_PrefersUnusedAndDistinct()
    {
        var options = new List<string> { "A", "B", "C" };
        var used = new HashSet<string> { "A", "B" };
        var taken = new HashSet<string>();
        for (int roll = 0; roll < 6; roll++) Assert.AreEqual("C", EscapeRules.PickRocketMaterial(options, taken, used, roll));
        taken.Add("C");
        for (int roll = 0; roll < 6; roll++)
        {
            string second = EscapeRules.PickRocketMaterial(options, taken, used, roll);
            Assert.AreNotEqual("C", second, "two slots never share a material when another is left");
        }
        Assert.IsNull(EscapeRules.PickRocketMaterial(new List<string>(), taken, used, 0));
        Assert.AreEqual("A", EscapeRules.PickRocketMaterial(new List<string> { "A" }, new HashSet<string> { "A" }, used, -5), "only one option: reuse it");
    }

    // P16: 춤은 다른 행동 평균과 같은 비중 — 행동 종류가 n개면 약 1/(n+1).
    [Test]
    public void DanceProbability_MatchesOtherActivities()
    {
        var p = new DecoyRules.Personality { Wander = 4, Rest = 4, Visit = 4, Hide = 4 };
        Assert.AreEqual(0.2f, DecoyRules.DanceProbability(p, false, true, true), 1e-4f, "4 activities + dance = 20%");
        Assert.AreEqual(1f / 3f, DecoyRules.DanceProbability(p, true, true, true), 1e-4f, "prepare: wander/rest + dance");
        for (int seed = 0; seed < 50; seed++)
        {
            float chance = DecoyRules.DanceProbability(DecoyRules.FromSeed(seed), false, true, true);
            Assert.That(chance, Is.InRange(0.12f, 0.3f), $"seed {seed}");
        }
        Assert.IsTrue(DecoyRules.RollDance(0.05, p, false, true, true));
        Assert.IsFalse(DecoyRules.RollDance(0.5, p, false, true, true));
    }

    // P8: 마녀 타이머 10분 고정.
    [Test]
    public void WitchTimer_IsTenMinutes()
    {
        Assert.AreEqual(10, RoomState.WitchTimerMinutes);
        Assert.AreEqual(600, RoomState.TimeAttackSeconds(), "outside a room: the fixed value");
    }

    // P5: 감도는 0.2~3.0, 기본 1.0.
    [Test]
    public void MouseSensitivity_Clamps()
    {
        Assert.AreEqual(0.2f, MouseSensitivity.Clamp(0f), 1e-5f);
        Assert.AreEqual(3f, MouseSensitivity.Clamp(10f), 1e-5f);
        Assert.AreEqual(1.5f, MouseSensitivity.Clamp(1.5f), 1e-5f);
        Assert.AreEqual(MouseSensitivity.Default, MouseSensitivity.Clamp(float.NaN), 1e-5f);
        float before = MouseSensitivity.Value;
        try
        {
            MouseSensitivity.Set(5f);
            Assert.AreEqual(3f, MouseSensitivity.Value, 1e-5f);
        }
        finally { MouseSensitivity.Set(before); }
    }

    // P6: 준비 시간은 모두 들어온 뒤부터 — 최대 2분 기다리고, 시간이 지났다고 괴물 때문에 대기실로 돌아가지 않는다.
    [Test]
    public void ArrivalWait_IsTwoMinutes()
    {
        Assert.AreEqual(120f, RoomLifecycleWatcher.MaxArrivalWait, 1e-4f);
        double now = 1000.0;
        float prepare = GameSettings.Current.PrepareSeconds;
        double provisional = now + GameSettings.Current.SceneLoadGrace + RoomLifecycleWatcher.MaxArrivalWait + prepare;
        Assert.IsTrue(MonsterArrival.ShouldShortenPrepare(provisional, now, prepare), "everyone in early → count from now");
    }

    // P15: 팔 / 촉수 뼈 구분.
    [Test]
    public void FirstPersonParts_SplitArmsAndTentacles()
    {
        Assert.IsTrue(MonsterFirstPersonLimbs.IsArmBone("Hand_Top_L"));
        Assert.IsFalse(MonsterFirstPersonLimbs.IsArmBone("Tentacle_01_L"));
        Assert.IsTrue(MonsterFirstPersonLimbs.IsTentacleBone("Tentacle_03_R"));
        Assert.IsTrue(MonsterFirstPersonLimbs.IsPartBone("Forearm_Low_R", MonsterFirstPersonLimbs.Part.Arms));
        Assert.IsFalse(MonsterFirstPersonLimbs.IsPartBone("Forearm_Low_R", MonsterFirstPersonLimbs.Part.Tentacles));
    }
}
