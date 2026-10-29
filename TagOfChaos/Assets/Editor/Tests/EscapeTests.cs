using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 탈출 모드 규칙 검증(Request1009Plan.md §25). 규칙은 네트워크 없이 순수 계산만, 맵 배치는 테스트 맵 씬을 열어 검사한다.
public class EscapeTests
{
    // §25.5 A 인원: 4~5명, 괴물 늘 1명, 스파이 0~1명(방 설정), 쿠키 최소 2명
    [TestCase(4, 0, 0)]
    [TestCase(4, 1, 1)]
    [TestCase(5, 1, 1)]
    [TestCase(5, 3, 1)]
    [TestCase(3, 1, 0)]
    public void Roles_SpiesClampedSoTwoCookiesRemain(int players, int requested, int spies)
    {
        GameSettingsSO s = ScriptableObject.CreateInstance<GameSettingsSO>();
        Assert.AreEqual(1, s.MonsterCountFor(players));
        Assert.AreEqual(spies, s.ClampSpies(requested, players));
        Assert.GreaterOrEqual(players - s.MonsterCountFor(players) - s.ClampSpies(requested, players), s.MinCookies);
        Object.DestroyImmediate(s);
    }

    [Test]
    public void Settings_AssetMatchesReform()
    {
        GameSettingsSO s = GameSettings.Current;
        Assert.AreEqual(5, s.MaxPlayers);
        Assert.AreEqual(4, s.MinPlayers);
        Assert.AreEqual(1, s.MaxSpies);
        Assert.AreEqual(2, s.MinCookies);
        Assert.AreEqual(15f, s.PrepareSeconds, 1e-4f);
        Assert.NotNull(s.CharacterCatalog, "characterCatalog");
        Assert.AreEqual(5, s.CharacterCatalog.Count, "cookie + four characters (Request1009Plan.md §45)");
        foreach (string scene in s.GameMapScenes) Assert.IsTrue(SceneNames.IsMapScene(scene), scene); // Test_ 맵 + 실제 모델 맵(§28)
    }

    // §25.5 B: 고른 캐릭터는 입장 순서대로 인정, 겹치거나 안 고른 사람은 남은 캐릭터
    [Test]
    public void Characters_NoDuplicates_FirstComeKeepsChoice()
    {
        var prefs = new Dictionary<int, int> { { 3, 1 }, { 1, 1 }, { 2, CharacterAssignment.Unselected } };
        int[] flat = CharacterAssignment.Assign(new[] { 3, 1, 2, 4 }, prefs, 4);
        Assert.AreEqual(8, flat.Length);
        var got = new Dictionary<int, int>();
        for (int i = 0; i < flat.Length; i += 2) got[flat[i]] = flat[i + 1];
        Assert.AreEqual(1, got[1], "actor 1 joined first and keeps character 1");
        Assert.AreNotEqual(1, got[3]);
        Assert.AreEqual(4, got.Values.Distinct().Count(), "no duplicates");
        Assert.IsTrue(CharacterAssignment.TryGet(flat, 4, out int c4) && c4 >= 0 && c4 < 4);
        Assert.IsFalse(CharacterAssignment.TryGet(flat, 9, out _));
    }

    // §25.5 D: 게임 제한시간 없음 — 끝은 모두 해결 / 마녀 타이머 끝(밟기 연출 뒤)
    private static EscapeRules.PlayerInfo P(bool monster = false, bool spy = false, bool escaped = false, bool broken = false) =>
        new EscapeRules.PlayerInfo { IsMonster = monster, IsSpy = spy, Escaped = escaped, Broken = broken };

    [Test]
    public void Rules_EndConditions()
    {
        var hunt = new[] { P(monster: true), P(), P(escaped: true), P(spy: true) };
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(hunt, false, 100000, 0, -1), "no time limit");

        var done = new[] { P(monster: true), P(broken: true), P(escaped: true), P(spy: true, broken: true) };
        Assert.AreEqual(EscapeRules.Decision.EndAllResolved, EscapeRules.Evaluate(done, false, 10, 0, -1));
        Assert.AreEqual(EscapeRules.Decision.EndAllResolved, EscapeRules.Evaluate(done, true, 10, 600, -1), "ends at once even during the witch timer");

        var spyLeft = new[] { P(monster: true), P(broken: true), P(spy: true) };
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(spyLeft, false, 10, 0, -1));

        var ta = new[] { P(monster: true), P(), P(escaped: true) };
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(ta, true, 599, 600, -1));
        Assert.AreEqual(EscapeRules.Decision.StrikeWitch, EscapeRules.Evaluate(ta, true, 600, 600, -1));
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(ta, true, 601, 600, 1f));
        Assert.AreEqual(EscapeRules.Decision.EndWitchStrike, EscapeRules.Evaluate(ta, true, 615, 600, EscapeRules.WitchSlamSeconds));
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(new[] { P(monster: true), P(escaped: true) }, false, 10, 0, -1f, departing: true),
            "rocket departure holds the end");
    }

    [Test]
    public void Rules_SafetyCapForcesWitchTimer()
    {
        Assert.IsFalse(EscapeRules.ShouldForceWitchTimer(100 + 1799, 100, 1800, false));
        Assert.IsTrue(EscapeRules.ShouldForceWitchTimer(100 + 1800, 100, 1800, false));
        Assert.IsFalse(EscapeRules.ShouldForceWitchTimer(100 + 5000, 100, 1800, true), "already running");
        Assert.AreEqual(1800.0, GameSettings.Current.SafetyCapSeconds, 1e-6);
    }

    [Test]
    public void Phase_PrepareHuntWitchTimerResult()
    {
        Assert.AreEqual(GamePhase.Lobby, GamePhaseState.Evaluate(false, 0, false, 10));
        Assert.AreEqual(GamePhase.Prepare, GamePhaseState.Evaluate(true, 15, false, 10));
        Assert.AreEqual(GamePhase.Hunt, GamePhaseState.Evaluate(true, 15, false, 15));
        Assert.AreEqual(GamePhase.TimeAttack, GamePhaseState.Evaluate(true, 15, false, 300, witchTimer: true));
        Assert.AreEqual(GamePhase.Result, GamePhaseState.Evaluate(true, 15, true, 300, witchTimer: true));
    }

    // §25.5 E: 수리할 건물 수 = min(4, 쿠키 + 1)
    [TestCase(2, 3)]
    [TestCase(3, 4)]
    [TestCase(4, 4)]
    [TestCase(1, 2)]
    public void Buildings_ActiveCountFollowsCookies(int cookies, int active)
    {
        Assert.AreEqual(active, EscapeAuthority.ActiveBuildingsFor(cookies, EscapeState.BuildingCount));
    }

    // §25.5 E: E 10초로 차오르고, 놓으면 초당 1%씩 줄어든다
    [Test]
    public void Building_ProgressFillsAndDecays()
    {
        var b = new EscapeState.Building { Active = true, ItemIndex = -1, Progress = 0f, ProgressAt = 100, Repairer = 7 };
        Assert.AreEqual(0.5f, b.ProgressAtTime(105), 1e-4f);
        Assert.AreEqual(1f, b.ProgressAtTime(120), 1e-4f, "clamped");
        b.Progress = 0.5f; b.Repairer = 0;
        Assert.AreEqual(0.4f, b.ProgressAtTime(110), 1e-4f, "1 % per second");
        Assert.AreEqual(0f, b.ProgressAtTime(1000), 1e-4f);
        Assert.IsFalse(b.Repaired);
        b.ItemIndex = 3;
        Assert.IsTrue(b.Repaired);
        Assert.AreEqual(1f, b.ProgressAtTime(100), 1e-4f);
        Assert.IsTrue(new EscapeState.Building { Active = false, ItemIndex = -1 }.Repaired, "inactive buildings start repaired");
        Assert.IsTrue(EscapeAuthority.HeldLongEnough(5f, 5f + EscapeState.RepairSeconds, EscapeState.RepairSeconds));
        Assert.IsFalse(EscapeAuthority.HeldLongEnough(5f, 5f + 5f, EscapeState.RepairSeconds));
    }

    [Test]
    public void State_v6_EncodeDecode_RoundTrips()
    {
        var s = new EscapeState { Revision = 9, RocketAnchor = 2, OpenExits = 0b0101, ExitsOpenedAt = 77.5, RainbowAnchor = 4, Rainbow = RainbowState.Opened, NextId = 31 };
        s.Items.Add(new EscapeState.Item { Id = "Gear", Loc = ItemLocation.Held, A = 2, B = 1, Pos = new Vector3(1, 2, 3), Charges = 3 });
        s.Items.Add(new EscapeState.Item { Id = "Battery", Loc = ItemLocation.Device, A = 1, Charges = 0 });
        s.Chests.Add(new EscapeState.Chest { Anchor = 7, Spy = true, Opened = true });
        s.Buildings.Add(new EscapeState.Building { MaterialId = "Gear", Active = true, ItemIndex = -1, Progress = 0.25f, ProgressAt = 50.5, Repairer = 3 });
        s.Buildings.Add(new EscapeState.Building { MaterialId = "Battery", Active = true, ItemIndex = 1, Progress = 1f });
        s.RocketSlots.Add(new EscapeState.Slot { Label = "Kit", Accepts = new[] { "Toolbox", "Gear" }, ItemIndex = -1, RocketFilled = true });
        s.RocketSlots.Add(new EscapeState.Slot { Label = "Oil", Accepts = new[] { "Oil" }, ItemIndex = -1, KitHalf = true });
        s.Escaped.Add(5);
        s.Boarded.Add(6);
        s.Flights.Add(new EscapeState.Flight { Actor = 4, StartAt = 88.25 });
        s.Bombs.Add(new EscapeState.Bomb { Id = 11, Owner = 1, Pos = new Vector3(4, 0, 5), Spent = true, SpentAt = 60 });
        s.Decoys.Add(new EscapeState.Decoy { Id = 12, Owner = 2, Pos = new Vector3(-3, 0, 9), Yaw = 90f, VoiceAt = 70, Seed = 1234, CaughtBy = 7, CaughtAt = 81.5 });

        EscapeState d = EscapeState.Decode(s.Encode());
        Assert.NotNull(d);
        Assert.AreEqual(9, d.Revision);
        Assert.AreEqual(2, d.RocketAnchor);
        Assert.IsTrue(d.ExitOpen(0) && !d.ExitOpen(1) && d.ExitOpen(2) && !d.ExitOpen(3));
        Assert.AreEqual(77.5, d.ExitsOpenedAt, 1e-9);
        Assert.AreEqual(4, d.RainbowAnchor);
        Assert.AreEqual(RainbowState.Opened, d.Rainbow);
        Assert.AreEqual(31, d.NextId);
        Assert.AreEqual(0, d.HeldItemAt(2, 1));
        Assert.AreEqual(ItemLocation.Device, d.Items[1].Loc);
        Assert.IsTrue(d.Chests[0].Spy && d.Chests[0].Opened);
        Assert.AreEqual("Gear", d.Buildings[0].MaterialId);
        Assert.AreEqual(0.25f, d.Buildings[0].Progress, 1e-6f);
        Assert.AreEqual(50.5, d.Buildings[0].ProgressAt, 1e-9);
        Assert.AreEqual(3, d.Buildings[0].Repairer);
        Assert.IsTrue(d.Buildings[1].Repaired);
        Assert.AreEqual(2, d.ActiveBuildingCount);
        Assert.IsFalse(d.AllBuildingsRepaired);
        Assert.IsTrue(d.RocketSlots[0].Filled && d.RocketSlots[0].Allows("Toolbox"));
        Assert.IsTrue(d.HasEscaped(5) && d.HasEscaped(6) && !d.HasEscaped(4));
        Assert.AreEqual(0, d.FlightIndex(4));
        Assert.AreEqual(88.25, d.Flights[0].StartAt, 1e-9);
        Assert.AreEqual(0, d.BombIndex(11));
        Assert.IsTrue(d.Bombs[0].Spent);
        Assert.AreEqual(0, d.DecoyIndex(12));
        Assert.AreEqual(1234, d.Decoys[0].Seed);
        Assert.AreEqual(90f, d.Decoys[0].Yaw, 1e-4f);
        Assert.IsTrue(d.Decoys[0].Caught && d.Decoys[0].CaughtBy == 7);
        Assert.AreEqual(81.5, d.Decoys[0].CaughtAt, 1e-9);
        Assert.IsTrue(d.RocketSlots[1].KitHalf && !d.RocketSlots[1].Filled);
        Assert.AreEqual(0.5f, d.RocketSlots[1].Progress);
        Assert.IsNull(EscapeState.Decode(new byte[] { 4, 0, 0 }), "old format is rejected");
    }

    // §25.3.1 R7: 폭탄 3칸, 터지거나 치운 뒤 20초에 칸이 다시 찬다
    [Test]
    public void Bomb_ChargesRefillTwentySecondsAfterSpent()
    {
        var s = new EscapeState();
        Assert.AreEqual(3, s.BombChargesLeft(1, 3, 0, 20f));
        s.Bombs.Add(new EscapeState.Bomb { Id = 1, Owner = 1 });
        s.Bombs.Add(new EscapeState.Bomb { Id = 2, Owner = 1, Spent = true, SpentAt = 100 });
        s.Bombs.Add(new EscapeState.Bomb { Id = 3, Owner = 2 });
        Assert.AreEqual(1, s.BombChargesLeft(1, 3, 110, 20f));
        Assert.AreEqual(2, s.BombChargesLeft(1, 3, 120, 20f));
        Assert.AreEqual(2, s.BombChargesLeft(2, 3, 0, 20f), "other monster's bombs do not count");
        Assert.AreEqual(1, EscapeAuthority.BombMaxCharges, "one bomb at a time (§26 H6)");
        Assert.AreEqual(20f, EscapeAuthority.BombCooldown);
    }

    // §26 H7: 녹음 목소리는 붙은 뒤 5~20초 쉬고 한 번, 다시 5~20초 쉬고 한 번… 씨앗값만으로 정해진다(모든 화면이 같다)
    [Test]
    public void DecoyVoice_PlaysEveryFiveToTwentySeconds()
    {
        for (int k = 0; k < 50; k++)
        {
            float gap = DecoyVoice.Gap(77, k);
            Assert.That(gap, Is.InRange(DecoyVoice.MinGap, DecoyVoice.MaxGap));
            Assert.AreEqual(gap, DecoyVoice.Gap(77, k));
        }
        Assert.IsFalse(DecoyVoice.TryCurrentPlay(0, 77, 2f, 100, out _), "no recorder");
        Assert.IsFalse(DecoyVoice.TryCurrentPlay(100, 77, 2f, 100 + DecoyVoice.MinGap - 0.1, out _), "still the first rest");
        double first = 100 + DecoyVoice.Gap(77, 0);
        Assert.IsTrue(DecoyVoice.TryCurrentPlay(100, 77, 2f, first + 0.5, out double s1));
        Assert.AreEqual(first, s1, 1e-9);
        double second = first + 2f + DecoyVoice.Gap(77, 1);
        Assert.IsTrue(DecoyVoice.TryCurrentPlay(100, 77, 2f, second + 0.1, out double s2));
        Assert.AreEqual(second, s2, 1e-6, "next play waits for the clip to end, then rests again");
    }

    // §26 H8: 40 m까지 오른 모자는 가장 가까운 맵 가장자리로 곧장 난다
    [Test]
    public void HatFlight_HeadsToNearestEdge()
    {
        var map = new Rect(-70, -50, 140, 100);
        Assert.AreEqual(Vector3.left, HatFlight.NearestEdgeDirection(map, new Vector3(-60, 40, 0)));
        Assert.AreEqual(Vector3.right, HatFlight.NearestEdgeDirection(map, new Vector3(65, 40, 10)));
        Assert.AreEqual(Vector3.back, HatFlight.NearestEdgeDirection(map, new Vector3(0, 40, -45)));
        Assert.AreEqual(Vector3.forward, HatFlight.NearestEdgeDirection(map, new Vector3(5, 40, 30)));
    }

    // §26 H4: 수리키트 — 0% 칸이 있으면 0% 칸을 모두 50%로, 없으면 50% 칸 하나를 100%로(스파이가 그 재료를 들고 있지 않은 칸 먼저)
    [Test]
    public void RepairKit_HalfFillsThenCompletes()
    {
        var slots = new List<EscapeState.Slot>
        {
            new EscapeState.Slot { Accepts = new[] { "Gear" }, ItemIndex = -1 },
            new EscapeState.Slot { Accepts = new[] { "Battery" }, ItemIndex = -1 },
        };
        Assert.IsTrue(EscapeState.ApplyRepairKit(slots, _ => false));
        Assert.IsTrue(slots[0].KitHalf && slots[1].KitHalf && !slots[0].Filled && !slots[1].Filled);
        Assert.AreEqual(0.5f, slots[0].Progress);
        Assert.IsTrue(EscapeState.ApplyRepairKit(slots, slot => slot.Accepts[0] == "Gear"), "spy holds Gear");
        Assert.IsFalse(slots[0].Filled, "the Gear slot is left for the Gear the spy holds");
        Assert.IsTrue(slots[1].Filled);
        Assert.IsTrue(EscapeState.ApplyRepairKit(slots, _ => true), "every slot held: first one");
        Assert.IsTrue(slots[0].Filled);
        Assert.IsFalse(EscapeState.ApplyRepairKit(slots, _ => false), "nothing left to fill");

        var mixed = new List<EscapeState.Slot>
        {
            new EscapeState.Slot { Accepts = new[] { "Gear" }, ItemIndex = -1, KitHalf = true },
            new EscapeState.Slot { Accepts = new[] { "Oil" }, ItemIndex = -1 },
        };
        Assert.IsTrue(EscapeState.ApplyRepairKit(mixed, _ => false));
        Assert.IsTrue(!mixed[0].Filled && mixed[1].KitHalf, "a 0% slot exists, so only 0% slots rise to 50%");
    }

    // §25.3 Q6: 녹음 목소리 μ-law 8비트(보낼 때 줄이고 받을 때 되돌린다)
    [Test]
    public void Voice_MuLawRoundTrip()
    {
        foreach (float v in new[] { -1f, -0.5f, -0.01f, 0f, 0.02f, 0.3f, 0.99f })
            Assert.AreEqual(v, VoiceStore.Decode(VoiceStore.Encode(v)), Mathf.Max(0.004f, Mathf.Abs(v) * 0.05f), v.ToString());
        Assert.IsNull(VoiceStore.ClipFor(-99), "no default voice any more (§26 H7)");
    }

    // 다시 생길 상자: 열린 빈 일반 상자 → 빈 일반 상자 → 바닥(-1). 스파이 상자는 빼다
    [Test]
    public void Respawn_PrefersOpenedEmptyNormalChest()
    {
        var s = new EscapeState();
        s.Chests.Add(new EscapeState.Chest { Anchor = 0, Spy = true, Opened = true });
        s.Chests.Add(new EscapeState.Chest { Anchor = 1 });
        s.Chests.Add(new EscapeState.Chest { Anchor = 2, Opened = true });
        s.Items.Add(new EscapeState.Item { Id = "Gear", Loc = ItemLocation.Chest, A = 2 });
        Assert.AreEqual(1, EscapeAuthority.PickRespawnChest(s, 5), "only empty normal chest");
        s.Items.Clear();
        for (int roll = 0; roll < 5; roll++) Assert.AreEqual(2, EscapeAuthority.PickRespawnChest(s, roll), "opened first");
        var spyOnly = new EscapeState();
        spyOnly.Chests.Add(new EscapeState.Chest { Spy = true });
        Assert.AreEqual(-1, EscapeAuthority.PickRespawnChest(spyOnly, 0));
    }

    [Test]
    public void StolenNotice_WaitsThenShows_CancelsWhenPutBack()
    {
        var n = new StolenNotice { ItemId = "Seatbelt", ItemIndex = 3, ShowAt = 100.0 };
        Assert.AreEqual(StolenNotice.Decision.Wait, n.Decide(99.9, ItemLocation.Held));
        Assert.AreEqual(StolenNotice.Decision.Show, n.Decide(100.0, ItemLocation.Held));
        Assert.AreEqual(StolenNotice.Decision.Show, n.Decide(101.0, ItemLocation.Chest));
        Assert.AreEqual(StolenNotice.Decision.Cancel, n.Decide(101.0, ItemLocation.Device), "back in a building");
    }

    [Test]
    public void RespawnedNotice_UsesItsOwnTexts()
    {
        EscapeTextsSO t = EscapeTextsSO.Current;
        var chest = new StolenNotice { ItemId = "Gear", ItemIndex = 2, ShowAt = 50.0, Respawned = true };
        var ground = new StolenNotice { ItemId = "Gear", ItemIndex = 2, ShowAt = 50.0, Respawned = true, OnGround = true };
        Assert.AreEqual(t.respawnedInChestWithFinal, chest.Template(t, true));
        Assert.AreEqual(t.respawnedOnGroundNoFinal, ground.Template(t, false));
        foreach (string s in new[] { t.respawnedInChestWithFinal, t.respawnedInChestNoFinal, t.respawnedOnGroundWithFinal, t.respawnedOnGroundNoFinal })
            StringAssert.Contains("{0}", s);
    }

    // §25 새 문장은 모두 한국어로 에셋에 들어 있다
    [Test]
    public void Texts_ReformStringsAreKorean()
    {
        EscapeTextsSO t = EscapeTextsSO.Current;
        string[] all =
        {
            t.wrongMaterial, t.buildingRepaired, t.exitsOpened, t.rainbowAppeared, t.rainbowChest,
            t.rainbowBroken, t.breakChest, t.noiseBombAlarm, t.removeBomb,
            t.recordVoice, t.recording, t.noMicrophone,
        };
        foreach (string s in all)
        {
            Assert.IsFalse(string.IsNullOrEmpty(s));
            Assert.IsTrue(s.Any(c => c >= '가' && c <= '힣'), $"Korean text in the asset: {s}");
        }
        StringAssert.Contains("{0}", t.noiseBombCharges);
    }

    // §25.5 I·J·K·M: 새 아이템이 카탈로그에 있고 한국어 이름을 가진다
    [Test]
    public void Catalog_HasReformItems()
    {
        EscapeCatalogSO c = EscapeCatalogSO.Current;
        Assert.NotNull(c.RepairKit, "repair kit");
        Assert.NotNull(c.DismantleTool, "dismantle");
        Assert.NotNull(c.HeliHat, "heli hat");
        Assert.NotNull(c.NoiseBomb, "noise bomb");
        Assert.AreEqual(3, c.SpyRandomTools.Count, "stun gun, hammer, water balloon (jail balloon merged, §45.8 Q1)");
        Assert.AreEqual(3, c.CookieItems.Count, "skates, pumpkin, recorder");
        CollectionAssert.AreEqual(new[] { 5, 3, 5 }, c.CookieItemWeights.ToArray());
        foreach (ItemSO item in new[] { c.RepairKit, c.DismantleTool, c.HeliHat, c.NoiseBomb }.Concat(c.SpyRandomTools).Concat(c.CookieItems))
            Assert.IsTrue(item.DisplayName.Any(ch => ch >= '가' && ch <= '힣'), item.ItemId + " name");
        foreach (string map in MapSceneBuilder.MapNames)
        {
            EscapeRecipeSO recipe = c.RecipeFor(map);
            Assert.NotNull(recipe, map);
            Assert.GreaterOrEqual(recipe.BuildingMaterials.Length, 4, map + ": one material per building");
        }
    }

    [Test]
    public void Inventory_AutoEquip_FindsNextOccupied()
    {
        Assert.AreEqual(2, PlayerInventory.NextOccupied(new[] { -1, -1, 5, 6 }, 0));
        Assert.AreEqual(0, PlayerInventory.NextOccupied(new[] { 4, -1, -1, -1 }, 3));
        Assert.AreEqual(1, PlayerInventory.NextOccupied(new[] { -1, -1, -1, -1 }, 1));
    }

    [TestCase("기어", false)]
    [TestCase("레드 버튼", true)]
    [TestCase("룬", true)]
    [TestCase("Gear", false)]
    public void KoreanText_FinalConsonant(string word, bool expected) => Assert.AreEqual(expected, KoreanText.HasFinalConsonant(word));

    // 방 설정: 인원 4~5, 스파이 0~1, 마녀 타이머 1~10분
    [Test]
    public void RoomSettingStepper_ClampsAndWarns()
    {
        var players = new RoomSettingStepper(4, 5, 5);
        Assert.AreEqual(RoomSettingStepper.Outcome.OutOfRange, players.Step(1));
        Assert.AreEqual(5, players.Value);
        Assert.AreEqual(RoomSettingStepper.Outcome.OutOfRange, players.SetFromText("3"));
        Assert.AreEqual(4, players.Value);
        Assert.AreEqual(RoomSettingStepper.Outcome.Empty, players.SetFromText("abc"));
        var spies = new RoomSettingStepper(0, 1, 0);
        Assert.AreEqual(RoomSettingStepper.Outcome.OutOfRange, spies.SetFromText("2"));
        Assert.AreEqual(1, spies.Value);
        var witch = new RoomSettingStepper(1, 10, 1);
        Assert.AreEqual(RoomSettingStepper.Outcome.OutOfRange, witch.SetFromText("11"));
        Assert.AreEqual(10, witch.Value);
    }

    [Test]
    public void Witch_FadesInOverFiveSeconds()
    {
        Assert.AreEqual(0f, WitchPresenter.FadeAlpha(100, 100, 5f), 1e-4f);
        Assert.AreEqual(0.5f, WitchPresenter.FadeAlpha(100, 102.5, 5f), 1e-4f);
        Assert.AreEqual(1f, WitchPresenter.FadeAlpha(100, 110, 5f), 1e-4f);
    }

    // 예전부터 쓰던 아이템은 Blender 모델이 연결돼 있다(새 아이템 모델은 사용자가 만든다 — §25 G17).
    [Test]
    public void Items_ExistingHaveBlenderModels()
    {
        var pending = new HashSet<string> { "Dismantle", "JailBalloon", "Skates", "Pumpkin", "Recorder", "HeliHat", "NoiseBomb" };
        string[] guids = AssetDatabase.FindAssets("t:ItemSO", new[] { "Assets/03. SO/Escape/Items" });
        Assert.Greater(guids.Length, 0);
        foreach (string guid in guids)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (pending.Contains(item.ItemId)) continue;
            Assert.NotNull(item.ModelPrefab, item.ItemId);
        }
    }

    // §25.5 O: 테스트 맵마다 건물 4·탈출구 4·술래문·상자 40·로켓 자리·무지개 자리·마녀가 연결돼 있다
    [TestCase("CandyForest")]
    [TestCase("GingerbreadVillage")]
    [TestCase("ChocolateFactory")]
    [TestCase("CursedCandyCarnival")]
    [TestCase("HauntedBakery")]
    public void TestMap_EscapeSetupComplete(string map)
    {
        EditorSceneManager.OpenScene(TestMapBuilder.ScenePath(map), OpenSceneMode.Single);
        GameObject root = GameObject.Find(EscapeMapSetup.RootName);
        Assert.NotNull(root, "EscapeSetup");
        var manager = root.GetComponent<EscapeManager>();
        Assert.NotNull(manager);
        var so = new SerializedObject(manager);
        Assert.AreEqual(4, so.FindProperty("buildings").arraySize, "buildings");
        Assert.AreEqual(4, so.FindProperty("exits").arraySize, "exits");
        Assert.AreEqual(EscapeMapSetup.ChestSlotCount, so.FindProperty("chestAnchors").arraySize, "chests");
        Assert.AreEqual(6, so.FindProperty("rocketAnchors").arraySize, "rocket spots");
        Assert.AreEqual(8, so.FindProperty("rainbowAnchors").arraySize, "rainbow spots");
        Assert.NotNull(so.FindProperty("rocket").objectReferenceValue, "rocket");
        Assert.NotNull(so.FindProperty("rainbowChest").objectReferenceValue, "rainbow chest");
        Assert.NotNull(so.FindProperty("monsterRoom").objectReferenceValue, "monster room");
        Assert.NotNull(so.FindProperty("witch").objectReferenceValue, "witch");
        for (int i = 0; i < 4; i++)
        {
            var b = (QuadrantBuilding)so.FindProperty("buildings").GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.NotNull(b, "building " + i);
            Assert.AreEqual($"Building_Q{i + 1}", b.name);
            Assert.NotNull(FindPrefix(b.transform, "Repair_Point"), b.name + " repair point");
        }
        Assert.AreEqual(4, Object.FindObjectsByType<MonsterRoomDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "monster doors");
        foreach (Transform t in root.transform)
            if (t.name.StartsWith("CHEST_Slot_")) Assert.NotNull(t.GetComponentInChildren<MaterialChest>(), t.name);
        Assert.NotNull(root.transform.Find("WITCH_Anchor/Model"), "witch model");
        for (int q = 1; q <= 4; q++) Assert.NotNull(GameObject.Find("Spawn_Cookie_Q" + q), "cookie spawn " + q);
    }

    // 쿠키 스폰에서 네 건물 수리 자리와 네 탈출구까지 걸어갈 수 있다.
    [TestCase("CandyForest")]
    [TestCase("HauntedBakery")]
    public void TestMap_BuildingsAndExitsReachable(string map)
    {
        EditorSceneManager.OpenScene(TestMapBuilder.ScenePath(map), OpenSceneMode.Single);
        Vector3 spawn = GameObject.Find("Spawn_Cookie_Q1").transform.position;
        for (int q = 1; q <= 4; q++)
        {
            Transform b = GameObject.Find($"Building_Q{q}").transform;
            Assert.IsTrue(ReachableNear(spawn, FindPrefix(b, "Repair_Point").position, 1.5f), b.name);
        }
        foreach (string e in new[] { "N", "E", "S", "W" })
            Assert.IsTrue(ReachableNear(spawn, EscapeCue.FindDeep(GameObject.Find("Exit_" + e).transform, "ExitTrigger_" + e).position, 2f), "exit " + e);
    }

    private static Transform FindPrefix(Transform root, string prefix) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(prefix));

    private static bool ReachableNear(Vector3 from, Vector3 target, float radius)
    {
        if (MapPassabilityCheck.FindPath(from, target, cookie: true) != null) return true;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = target + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * radius;
            if (MapPassabilityCheck.FindPath(from, p, cookie: true) != null) return true;
        }
        return false;
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void MaterialTemplate_ExistsWithKeywords(bool fade, bool glow)
    {
        var mat = Resources.Load<Material>(EscapeVisuals.TemplateName(fade, glow));
        Assert.NotNull(mat, $"Missing {EscapeVisuals.TemplateName(fade, glow)}.");
        Assert.AreEqual("Standard", mat.shader.name);
        Assert.AreEqual(fade, mat.IsKeywordEnabled(EscapeVisuals.FadeKeyword), "fade keyword");
        Assert.AreEqual(glow, mat.IsKeywordEnabled(EscapeVisuals.EmissionKeyword), "emission keyword");
    }

    [Test]
    public void XRayMaterial_Exists()
    {
        var mat = Resources.Load<Material>("Escape/XRay");
        Assert.NotNull(mat, "Run EscapeAssetsBuilder.Build");
        Assert.AreEqual("TagOfChaos/XRaySilhouette", mat.shader.name);
    }

    // 투시는 재질이 여럿인 모델의 모든 부분 메시를 칠한다(덧붙인 재질은 마지막 부분 메시에만 그려졌다 — 초콜릿 공장 로켓, Request1009Plan.md §42)
    [Test]
    public void XRayMarker_CoversEverySubMesh()
    {
        var go = new GameObject("XRayTest");
        var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right, Vector3.forward }, subMeshCount = 3 };
        mesh.SetTriangles(new[] { 0, 1, 2 }, 0); mesh.SetTriangles(new[] { 0, 2, 3 }, 1); mesh.SetTriangles(new[] { 0, 3, 1 }, 2);
        try
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var original = new Material(Shader.Find("Standard"));
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { original, original, original };
            var xray = go.AddComponent<XRayMarker>();
            xray.Init(Color.red, 0.55f);
            Transform copy = go.transform.Find("XRay");
            Assert.NotNull(copy, "x-ray copy renderer");
            Assert.IsFalse(copy.gameObject.activeSelf, "hidden until asked");
            xray.SetVisible(true);
            Assert.IsTrue(copy.gameObject.activeSelf);
            Material[] mats = copy.GetComponent<MeshRenderer>().sharedMaterials;
            Assert.AreEqual(3, mats.Length, "one x-ray material per sub-mesh");
            Assert.IsTrue(mats.All(m => m != null && m.shader.name == "TagOfChaos/XRaySilhouette"));
            Assert.AreEqual(0.55f, mats[0].GetFloat("_Fill"), 1e-4f);
            Assert.AreEqual(3, go.GetComponent<MeshRenderer>().sharedMaterials.Length, "the original materials stay as they were");
            xray.SetVisible(false);
            Assert.IsFalse(copy.gameObject.activeSelf);
            // 가까이 온 카메라에는 끈다(스파이 로켓 14 m)
            xray.HideWithin(14f);
            Camera cam = Camera.main;
            GameObject temp = null;
            if (cam == null) { temp = new GameObject("XRayTestCam") { tag = "MainCamera" }; cam = temp.AddComponent<Camera>(); }
            Vector3 saved = cam.transform.position;
            cam.transform.position = go.transform.position + Vector3.forward * 5f;
            xray.SetVisible(true);
            Assert.IsFalse(copy.gameObject.activeSelf, "hidden when the camera is close");
            cam.transform.position = go.transform.position + Vector3.forward * 40f;
            xray.SetVisible(true);
            Assert.IsTrue(copy.gameObject.activeSelf, "shown from afar");
            cam.transform.position = saved;
            if (temp != null) Object.DestroyImmediate(temp);
            Object.DestroyImmediate(original);
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void WitchMaterials_UseOnlyTemplateKeywords()
    {
        string[] allowed = { EscapeVisuals.EmissionKeyword };
        foreach (string guid in AssetDatabase.FindAssets("ME_Witch_ t:Material"))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            string[] extra = mat.shaderKeywords.Where(k => !allowed.Contains(k)).ToArray();
            Assert.IsEmpty(extra, $"{mat.name} uses keywords without a Fade template: {string.Join(" ", extra)}");
        }
    }
}
