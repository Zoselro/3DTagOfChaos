using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Request1009Plan.md §90 — 상자 규칙(C8)·껌 수류탄(G6)·바주카포(B3)·술래 순간이동(T2)·낙하(P5)·패러글라이더·바닥 띄우기(F4)·상태 v13.
public class Request1121Tests
{
    private static EscapeState.Building B(string id, bool repaired = false) =>
        new EscapeState.Building { MaterialId = id, Active = true, ItemIndex = repaired ? 0 : -1 };

    // C8: 쿠키가 닿는 곳에 2개 미만인 재료 중 가장 모자란 것, 수리 끝난 건물 재료는 리셋 전엔 안 나온다.
    [Test]
    public void Chest_NormalPick_MostMissingFirst()
    {
        var s = new EscapeState();
        s.Buildings.Add(B("Oil"));
        s.Buildings.Add(B("Gear"));
        s.Buildings.Add(B("Wheel", repaired: true));
        var count = new Dictionary<string, int> { { "Oil", 1 }, { "Gear", 0 }, { "Wheel", 0 } };
        Assert.AreEqual("Gear", ChestRules.NormalPick(s, id => count[id], new System.Random(1)));
        count["Gear"] = 2;
        Assert.AreEqual("Oil", ChestRules.NormalPick(s, id => count[id], new System.Random(1)));
        count["Oil"] = 2;
        Assert.IsNull(ChestRules.NormalPick(s, id => count[id], new System.Random(1)), "enough of all -> cookie item");
        s.ChestResets = 1;
        Assert.AreEqual("Wheel", ChestRules.NormalPick(s, id => count[id], new System.Random(1)), "after reset repaired materials return");
    }

    // C8: 스파이 상자 = 첫 상자 해체 도구, 그 뒤 부품 2 : 수리키트 1.
    [Test]
    public void Chest_SpyPick_DismantleThenTwoPartsOneKit()
    {
        var g = new EscapeState.SpyGift { Actor = 5 };
        var tools = new List<string> { "Flash" };
        var got = new List<string>();
        for (int i = 0; i < 7; i++) got.Add(ChestRules.SpyPick(ref g, "Part", true, "Kit", "Dismantle", tools, new System.Random(2)));
        CollectionAssert.AreEqual(new[] { "Dismantle", "Part", "Kit", "Part", "Part", "Kit", "Part" }, got);
        Assert.AreEqual("Flash", ChestRules.SpyPick(ref g, null, false, "Kit", "Dismantle", tools, new System.Random(2)), "nothing needed -> spy tool");
    }

    [Test]
    public void Chest_ResetRules()
    {
        var s = new EscapeState();
        s.Chests.Add(new EscapeState.Chest { Opened = true });
        s.Chests.Add(new EscapeState.Chest { Spy = true, Opened = false });
        Assert.IsTrue(ChestRules.AllOpened(s, spyPlaying: false), "spy chests ignored without spy");
        Assert.IsFalse(ChestRules.AllOpened(s, spyPlaying: true));
        Assert.IsFalse(ChestRules.Reset8Due(s, true, 100, 100 + 479));
        Assert.IsTrue(ChestRules.Reset8Due(s, true, 100, 100 + 480));
        s.ChestReset8Done = true;
        Assert.IsFalse(ChestRules.Reset8Due(s, true, 100, 1000), "only once");
        Assert.AreEqual(8, ChestRules.SpyChestsPerSpy);
    }

    // G6: 1.5 m(+몸) 안 = 50% 3.5초, 웅덩이는 사람당 4개(가장 오래된 것부터 지움).
    [Test]
    public void Gum_SlowAndPuddles()
    {
        var s = new EscapeState();
        s.Slows.Add(new EscapeState.Slow { Actor = 2, At = 10 });
        Assert.AreEqual(0.5f, GumTrapRules.SpeedFactor(s, 2, 12));
        Assert.AreEqual(1f, GumTrapRules.SpeedFactor(s, 2, 13.6));
        Assert.AreEqual(1f, GumTrapRules.SpeedFactor(s, 3, 12));
        Assert.IsTrue(GumTrapRules.HitsBody(new Vector3(2f, 1f, 0f), Vector3.zero, 4f, 0.6f));
        Assert.IsFalse(GumTrapRules.HitsBody(new Vector3(2.2f, 1f, 0f), Vector3.zero, 4f, 0.6f));
        Assert.AreEqual(0.5f, GumTrapRules.StickAmount(10, 11.75), 1e-4);
        for (int i = 0; i < 3; i++) s.Puddles.Add(new EscapeState.Puddle { Id = i, Owner = 7, PlacedAt = 5 + i });
        Assert.AreEqual(-1, GumTrapRules.OldestToRemove(s, 7));
        s.Puddles.Add(new EscapeState.Puddle { Id = 3, Owner = 7, PlacedAt = 4 });
        Assert.AreEqual(3, GumTrapRules.OldestToRemove(s, 7));
        Assert.IsTrue(GumTrapRules.Steps(Vector3.zero, new Vector3(1.4f, 0.2f, 0f), 0.6f));
        Assert.IsFalse(GumTrapRules.Steps(Vector3.zero, new Vector3(1.4f, 2f, 0f), 0.6f));
    }

    // B3: 3.5 m 잡힘, 6 m 기절, 벽 뒤 안전, 90 m / 45 m/s = 2초.
    [Test]
    public void Bazooka_Effects()
    {
        Assert.AreEqual(BazookaRules.Effect.Kill, BazookaRules.EffectAt(3.4f, false));
        Assert.AreEqual(BazookaRules.Effect.Stun, BazookaRules.EffectAt(5.9f, false));
        Assert.AreEqual(BazookaRules.Effect.None, BazookaRules.EffectAt(6.1f, false));
        Assert.AreEqual(BazookaRules.Effect.None, BazookaRules.EffectAt(1f, true));
        Assert.AreEqual(2f, BazookaRules.MaxLifeSeconds, 1e-4);
        Assert.AreEqual(10f, BazookaRules.Cooldown);
    }

    // T2: 사라짐 → 2초 덜컹 → 0.5초 열림 → 3초 뒤 닫힘, 쿨타임 10초, 진행 중엔 다시 못 씀.
    [Test]
    public void Warp_PhasesAndRules()
    {
        var w = new EscapeState.Warp { Actor = 1, Building = 0, At = 100 };
        Assert.AreEqual(MonsterWarpRules.Phase.Before, MonsterWarpRules.PhaseAt(w, 99.9));
        Assert.AreEqual(MonsterWarpRules.Phase.Rattle, MonsterWarpRules.PhaseAt(w, 101));
        Assert.AreEqual(MonsterWarpRules.Phase.Opening, MonsterWarpRules.PhaseAt(w, 102.2));
        Assert.AreEqual(MonsterWarpRules.Phase.Out, MonsterWarpRules.PhaseAt(w, 104));
        Assert.AreEqual(MonsterWarpRules.Phase.Done, MonsterWarpRules.PhaseAt(w, 106.1));
        Assert.AreEqual(1f, MonsterWarpRules.DoorOpen(w, 103));
        Assert.AreEqual(0f, MonsterWarpRules.DoorOpen(w, 106.5));
        var s = new EscapeState();
        s.Buildings.Add(B("Oil", repaired: true));
        s.Buildings.Add(new EscapeState.Building { MaterialId = "X", Active = false, ItemIndex = -1 });
        Assert.IsTrue(MonsterWarpRules.Allows(s, 1, 0, true, true, 20f, 10f), "repaired building can be picked");
        Assert.IsFalse(MonsterWarpRules.Allows(s, 1, 1, true, true, 20f, 10f), "inactive building");
        Assert.IsFalse(MonsterWarpRules.Allows(s, 1, 0, true, true, 5f, 10f), "cooldown");
        Assert.IsFalse(MonsterWarpRules.Allows(s, 1, 0, false, true, 20f, 10f), "not monster");
        s.Warps.Add(w);
        Assert.IsFalse(MonsterWarpRules.Allows(s, 1, 0, true, true, 20f, 10f), "already warping");
    }

    // P5: 12 m 넘게 떨어지면 부서짐(패러글라이더면 안전), 떨어지는 동안은 디딘 높이를 적지 않는다.
    [Test]
    public void SkyFall_AndGlider()
    {
        float g = SkyFallRules.UpdateGroundY(24f, 23f, -1f, false);
        Assert.AreEqual(23f, g);
        g = SkyFallRules.UpdateGroundY(g, 15f, -9f, false);
        Assert.AreEqual(23f, g, "falling keeps last ground");
        Assert.IsTrue(SkyFallRules.Dies(true, g, 10f, false));
        Assert.IsFalse(SkyFallRules.Dies(true, g, 10f, true), "gliding is safe");
        Assert.IsFalse(SkyFallRules.Dies(false, g, 0f, false), "other maps");
        Assert.IsFalse(SkyFallRules.Dies(true, g, 11.5f, false), "12 m or less");
        Assert.IsTrue(SkyFallRules.ShowWarning(true, 20f, true, false));
        Assert.IsFalse(SkyFallRules.ShowWarning(true, 11f, true, false));
        Assert.AreEqual(new Vector2(11f, 1.6f), GliderFlight.Speeds(0f));
        Assert.AreEqual(new Vector2(14f, 3f), GliderFlight.Speeds(1f));
        Assert.AreEqual(new Vector2(7f, 1.2f), GliderFlight.Speeds(-1f));
    }

    [Test]
    public void GroundCover_LiftClamped()
    {
        Assert.AreEqual(0.7f, GroundCover.LiftFor(0f, 0.5f), 1e-4);
        Assert.AreEqual(GroundCover.MaxLift, GroundCover.LiftFor(0f, 5f));
        Assert.AreEqual(0f, GroundCover.LiftFor(1f, 0f));
        Assert.IsTrue(GroundCover.IsCoverName("CF_Tile_Moss (3)"));
        Assert.IsFalse(GroundCover.IsCoverName("Chest"));
    }

    // 상태 v13: 새 목록이 인코딩을 거쳐 그대로 돌아온다.
    [Test]
    public void State_V13_RoundTrip()
    {
        var s = new EscapeState();
        s.Chests.Add(new EscapeState.Chest { Anchor = 3, Spy = true, Opened = true, Filled = true });
        s.Warps.Add(new EscapeState.Warp { Actor = 4, Building = 2, At = 12.5 });
        s.Puddles.Add(new EscapeState.Puddle { Id = 9, Owner = 3, Pos = new Vector3(1, 2, 3), PlacedAt = 4, TriggeredAt = 6, Victim = 4 });
        s.Slows.Add(new EscapeState.Slow { Actor = 4, At = 6 });
        s.Glides.Add(new EscapeState.Glide { Actor = 3, At = 7 });
        s.SpyGifts.Add(new EscapeState.SpyGift { Actor = 5, Dismantle = true, Parts = 2, Kits = 1 });
        s.ChestResets = 2; s.ChestReset8Done = true;
        EscapeState d = EscapeState.Decode(s.Encode());
        Assert.IsTrue(d.Chests[0].Filled && d.Chests[0].Spy);
        Assert.AreEqual(2, d.Warps[0].Building); Assert.AreEqual(12.5, d.Warps[0].At);
        Assert.AreEqual(new Vector3(1, 2, 3), d.Puddles[0].Pos); Assert.IsTrue(d.Puddles[0].Triggered); Assert.AreEqual(4, d.Puddles[0].Victim);
        Assert.AreEqual(4, d.Slows[0].Actor); Assert.AreEqual(3, d.Glides[0].Actor);
        Assert.AreEqual(2, d.SpyGifts[0].Parts); Assert.IsTrue(d.SpyGifts[0].Dismantle);
        Assert.AreEqual(2, d.ChestResets); Assert.IsTrue(d.ChestReset8Done);
        Assert.AreEqual(0, d.WarpIndex(4)); Assert.AreEqual(0, d.GlideIndex(3)); Assert.AreEqual(0, d.SpyGiftIndex(5));
    }

    // 패러글라이더 = Heaven Of Bakery 전용(다른 맵 상자에서 안 나옴), 바주카포·껌 수류탄 에셋 연결.
    [Test]
    public void Catalog_ExclusivesAndNewItems()
    {
        EscapeCatalogSO cat = EscapeCatalogSO.Current;
        Assert.IsNotNull(cat);
        ItemSO glider = cat.Find("Paraglider");
        Assert.IsNotNull(glider, "Paraglider item");
        Assert.IsNotNull(cat.Find("GumTrap"), "GumTrap item");
        Assert.IsNotNull(cat.Bazooka, "Bazooka item");
        EscapeRecipeSO hob = cat.RecipeFor(SkyFallRules.MapName);
        Assert.IsNotNull(hob);
        Assert.IsTrue(hob.OwnsExclusive(glider));
        Assert.IsFalse(cat.IsExclusiveElsewhere(glider, hob));
        Assert.IsTrue(cat.IsExclusiveElsewhere(glider, cat.RecipeFor("CandyForest")));
        Assert.AreEqual(ToolKind.GumTrap, cat.Find("GumTrap").Tool.Kind);
    }

    // 벽장·바닥 뚜껑 프리팹과 HOB 과일·바나나·비닐하우스 조각 모델.
    [Test]
    public void Assets_ClosetsAndFarmFractures()
    {
        foreach (string p in new[] { "MonsterCloset", "MonsterHatch" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/04. Prefabs/Escape/P90/{p}.prefab");
            Assert.IsNotNull(go, p);
            Assert.IsNotNull(go.GetComponent<MonsterCloset>(), p + " component");
        }
        foreach (string n in new[] { "HOB_BananaTree_A", "HOB_BananaTree_B", "HOB_Greenhouse", "HOB_Apple", "HOB_Strawberry", "HOB_Grape", "HOB_Watermelon" })
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>($"{DestructionRulesSetup.PropFolder}/{n}_Fractured.fbx"), n + " fracture");
    }
}
