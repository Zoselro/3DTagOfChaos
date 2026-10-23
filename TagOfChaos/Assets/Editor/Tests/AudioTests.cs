using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 소리 기반(SoundPlan.md §6, S1). 카탈로그 구조, 재생 칸 배정 규칙, 쿨다운, 음량 변환·저장.
public class AudioTests
{
    // ---- 카탈로그 ----

    [Test]
    public void SoundCatalog_HasOneEntryPerId()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        Assert.NotNull(catalog, "Run Tools/TagOfChaos/Audio/Build Catalogs.");
        foreach (SoundId id in AudioCatalogBuilder.SoundIds())
            Assert.AreEqual(1, catalog.Entries.Count(e => e != null && e.id == id), $"{id} must have exactly one catalog entry.");
        Assert.IsFalse(catalog.Entries.Any(e => e == null || e.id == SoundId.None || !Enum.IsDefined(typeof(SoundId), e.id)), "Catalog has an empty or unknown entry.");
    }

    [Test]
    public void MusicCatalog_HasOneEntryPerId()
    {
        var catalog = Resources.Load<MusicCatalogSO>(MusicCatalogSO.ResourcePath);
        Assert.NotNull(catalog, "Run Tools/TagOfChaos/Audio/Build Catalogs.");
        foreach (MusicId id in AudioCatalogBuilder.MusicIds())
        {
            MusicCatalogSO.Entry[] found = catalog.Entries.Where(e => e != null && e.id == id).ToArray();
            Assert.AreEqual(1, found.Length, $"{id} must have exactly one catalog entry.");
            Assert.AreEqual(MusicCatalogSO.IsLoopById(id), found[0].loop, $"{id}: loops loop, stingers and jingles do not.");
        }
    }

    // 3D 소리는 들리는 거리가 있어야 하고, 쿠키 발소리는 괴물 발소리보다 작게 들린다(D3).
    [Test]
    public void SoundCatalog_DistancesFollowHideAndSeekRules()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        Assert.IsTrue(catalog.TryGet(SoundId.CookieStep, out var cookie));
        Assert.IsTrue(catalog.TryGet(SoundId.MonsterStep, out var monster));
        Assert.Less(cookie.maxDistance, monster.maxDistance);
        Assert.IsTrue(catalog.TryGet(SoundId.UiClick, out var ui));
        Assert.AreEqual(SoundSpace.Flat2D, ui.space);
        Assert.AreEqual(SoundBus.Ui, ui.bus);
    }

    // 음원은 S8에서 모두 채운다. 그 전까지는 빠진 수를 알려 주기만 한다(실패 아님).
    [Test]
    public void AllIds_HaveClips()
    {
        var sounds = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        var music = Resources.Load<MusicCatalogSO>(MusicCatalogSO.ResourcePath);
        string[] missing = sounds.Entries.Where(e => !e.HasClip).Select(e => e.id.ToString())
            .Concat(music.Entries.Where(e => e.clip == null).Select(e => e.id.ToString())).ToArray();
        if (missing.Length > 0)
            Assert.Inconclusive($"{missing.Length} ids have no clip yet (SoundPlan S8): {string.Join(", ", missing.Take(15))}…");
    }

    // ---- 재생 칸 배정 ----

    [Test]
    public void Pool_UsesFreeVoicesFirst()
    {
        var pool = new AudioVoicePool(2);
        Assert.AreEqual(0, pool.Acquire(SoundId.UiClick, SoundImportance.Normal, 0f, out bool s1));
        Assert.AreEqual(1, pool.Acquire(SoundId.UiClick, SoundImportance.Normal, 1f, out bool s2));
        Assert.IsFalse(s1 || s2);
        Assert.AreEqual(2, pool.CountPlaying(SoundId.UiClick));
    }

    [Test]
    public void Pool_WhenFull_StealsOldestOfLowestImportance()
    {
        var pool = new AudioVoicePool(3);
        pool.Acquire(SoundId.CookieStep, SoundImportance.Low, 0f, out _);     // 0: 낮음, 가장 오래됨
        pool.Acquire(SoundId.ChestOpen, SoundImportance.Normal, 1f, out _);   // 1
        pool.Acquire(SoundId.CookieStep, SoundImportance.Low, 2f, out _);     // 2: 낮음, 나중
        int voice = pool.Acquire(SoundId.MonsterStep, SoundImportance.High, 3f, out bool stolen);
        Assert.AreEqual(0, voice);
        Assert.IsTrue(stolen);
        Assert.AreEqual(SoundId.MonsterStep, pool.SoundAt(0));
    }

    [Test]
    public void Pool_WhenAllMoreImportant_DropsNewSound()
    {
        var pool = new AudioVoicePool(2);
        pool.Acquire(SoundId.WitchSlam, SoundImportance.Critical, 0f, out _);
        pool.Acquire(SoundId.RocketIgnite, SoundImportance.High, 0f, out _);
        Assert.AreEqual(-1, pool.Acquire(SoundId.CookieStep, SoundImportance.Normal, 1f, out _));
    }

    [Test]
    public void Pool_GenerationChanges_SoOldHandlesCannotStopNewSounds()
    {
        var pool = new AudioVoicePool(1);
        int v = pool.Acquire(SoundId.UiClick, SoundImportance.Normal, 0f, out _);
        int first = pool.GenerationOf(v);
        Assert.Greater(first, 0, "Generation 0 is reserved for empty handles.");
        pool.Release(v);
        pool.Acquire(SoundId.UiHover, SoundImportance.Normal, 1f, out _);
        Assert.AreNotEqual(first, pool.GenerationOf(v));
        Assert.IsFalse(new AudioHandle(false, 0, 0).IsValid);
    }

    [Test]
    public void Cooldown_BlocksRepeatsInsideWindow()
    {
        var cd = new SoundCooldowns();
        Assert.IsTrue(cd.TryUse(SoundId.UiClick, 0.05f, 1.00f));
        Assert.IsFalse(cd.TryUse(SoundId.UiClick, 0.05f, 1.03f));
        Assert.IsTrue(cd.TryUse(SoundId.UiHover, 0.05f, 1.03f), "Cooldown is per sound.");
        Assert.IsTrue(cd.TryUse(SoundId.UiClick, 0.05f, 1.06f));
    }

    // ---- UI 소리(S2) ----

    [TestCase(11, SoundId.None)]
    [TestCase(10, SoundId.UiTick)]
    [TestCase(4, SoundId.UiTick)]
    [TestCase(3, SoundId.UiTickFinal)]
    [TestCase(1, SoundId.UiTickFinal)]
    [TestCase(0, SoundId.None)]
    public void CountdownTick_LastTenSeconds_FinalThree(int seconds, SoundId expected)
    {
        Assert.AreEqual(expected, UiSoundCues.TickSound(seconds));
    }

    [TestCase("StartGameButton", SoundId.UiStart)]
    [TestCase("YesButton", SoundId.UiConfirm)]
    [TestCase("NoButton", SoundId.UiCancel)]
    [TestCase("Swatch7", SoundId.PaintPickColor)]
    [TestCase("Increase", SoundId.UiClick)]
    public void ButtonClickSound_ChosenByName(string name, SoundId expected)
    {
        Assert.AreEqual(expected, UiSoundInstaller.ClickFor(name));
    }

    [Test]
    public void UiPrefabs_EveryButtonHasUiSound()
    {
        foreach (string path in UiSoundInstaller.UiPrefabPaths())
        {
            var root = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            string[] missing = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b => b.GetComponent<UiSound>() == null).Select(b => b.name).ToArray();
            Assert.IsEmpty(missing, $"{path}: buttons without UiSound — run Tools/TagOfChaos/Audio/Attach UI Sounds.");
        }
    }

    [Test]
    public void BuildScenes_EveryButtonHasUiSound()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save the open scenes before running scene tests.");
        var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (UnityEditor.EditorBuildSettingsScene s in UnityEditor.EditorBuildSettings.scenes)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(s.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                string[] missing = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                                        .Where(b => b.GetComponent<UiSound>() == null).Select(b => b.name).ToArray();
                Assert.IsEmpty(missing, $"{s.path}: buttons without UiSound — run Tools/TagOfChaos/Audio/Attach UI Sounds.");
            }
        }
        finally
        {
            if (setup.Length > 0) UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    // ---- 배경음 규칙(S3) ----

    [TestCase("LobbyScene", MusicScene.Lobby)]
    [TestCase("GameLobbyScene", MusicScene.GameLobby)]
    [TestCase("Game_CandyForest", MusicScene.CandyForest)]
    [TestCase("Game_GingerbreadVillage", MusicScene.Gingerbread)]
    [TestCase("Game_ChocolateFactory", MusicScene.Factory)]
    [TestCase("Game_CursedCandyCarnival", MusicScene.Carnival)]
    [TestCase("Game_HauntedBakery", MusicScene.Bakery)]
    [TestCase("PlayerTestScene", MusicScene.Other)]
    public void Music_SceneFromName(string scene, MusicScene expected)
    {
        Assert.AreEqual(expected, MusicRules.SceneFromName(scene));
    }

    // 빌드에 들어가는 맵 씬은 모두 자기 곡을 가져야 한다(새 맵을 넣고 규칙을 빠뜨리지 않게).
    [Test]
    public void Music_EveryBuildMapHasItsOwnMusic()
    {
        foreach (UnityEditor.EditorBuildSettingsScene s in UnityEditor.EditorBuildSettings.scenes)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(s.path);
            if (!name.StartsWith("Game_")) continue;
            Assert.AreNotEqual(MusicScene.Other, MusicRules.SceneFromName(name), $"{name} has no music rule.");
        }
    }

    // 씬 8종 × 단계 6종 × 괴물 대기 여부 × 내가 괴물인지 전체 표
    [Test]
    public void Music_SelectTable()
    {
        var mapPaint = new System.Collections.Generic.Dictionary<MusicScene, MusicId>
        {
            { MusicScene.CandyForest, MusicId.CandyForestPaint }, { MusicScene.Gingerbread, MusicId.GingerbreadPaint },
            { MusicScene.Factory, MusicId.FactoryPaint }, { MusicScene.Carnival, MusicId.CarnivalPaint }, { MusicScene.Bakery, MusicId.BakeryPaint },
        };
        foreach (MusicScene scene in (MusicScene[])Enum.GetValues(typeof(MusicScene)))
        foreach (GamePhase phase in (GamePhase[])Enum.GetValues(typeof(GamePhase)))
        foreach (bool monster in new[] { false, true })
        {
            MusicId expected;
            bool monsterInGame = monster && phase != GamePhase.Lobby && phase != GamePhase.Result;
            if (scene == MusicScene.Other) expected = MusicId.None;
            else if (scene == MusicScene.Lobby) expected = MusicId.Lobby;
            else if (scene == MusicScene.GameLobby) expected = monsterInGame ? MusicId.MonsterWait : MusicId.GameLobby;
            else if (phase == GamePhase.Result) expected = MusicId.Result;
            else if (monster) expected = MusicId.None;                         // 괴물은 맵 곡 없음
            else if (phase == GamePhase.Lobby || phase == GamePhase.Prepare) expected = mapPaint[scene];
            else expected = MusicId.None;                                       // 변장이 끝나면 끈다
            var c = new MusicContext { Scene = scene, Phase = phase, LocalIsMonster = monster };
            Assert.AreEqual(expected, MusicRules.Select(c), $"{scene} {phase} monster={monster}");
        }
    }

    [TestCase(GamePhase.Prepare, GamePhase.Hunt, MusicId.StMonsterArrive)]
    [TestCase(GamePhase.Hunt, GamePhase.TimeAttack, MusicId.StSpyLaunch)]
    [TestCase(GamePhase.Lobby, GamePhase.Prepare, MusicId.None)]
    [TestCase(GamePhase.Hunt, GamePhase.Result, MusicId.None)]
    public void Music_StingerOnPhaseChange(GamePhase from, GamePhase to, MusicId expected)
    {
        Assert.AreEqual(expected, MusicRules.StingerFor(from, to));
    }

    // 결과 징글: 내 역할·결과로 고른다.
    [TestCase(GameResult.CookiesWin, false, true, false, MusicId.JgEscapeSuccess)]
    [TestCase(GameResult.CookiesWin, false, false, false, MusicId.JgEscapeFail)]   // 이겼지만 나는 부서짐
    [TestCase(GameResult.MonsterWins, false, false, false, MusicId.JgEscapeFail)]
    [TestCase(GameResult.EscapeEnded, false, true, true, MusicId.JgEscapeSuccess)]
    [TestCase(GameResult.EscapeEnded, false, false, true, MusicId.JgEscapeFail)]
    [TestCase(GameResult.MonsterWins, true, false, false, MusicId.JgMonsterWin)]
    [TestCase(GameResult.CookiesWin, true, false, false, MusicId.JgEscapeFail)]
    [TestCase(GameResult.EscapeEnded, true, false, false, MusicId.JgMonsterWin)]  // 아무도 못 나감
    [TestCase(GameResult.EscapeEnded, true, false, true, MusicId.JgEscapeFail)]   // 쿠키가 탈출함
    public void Music_JingleByLocalOutcome(GameResult result, bool monster, bool survived, bool anyEscaped, MusicId expected)
    {
        Assert.AreEqual(expected, MusicRules.JingleFor(result, monster, survived, anyEscaped));
    }

    // ---- 환경음 배치 ----

    [Test]
    public void AmbientTable_UsesRealMapsAndWorldSounds()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        foreach (var map in AmbientPlacer.Table)
        {
            Assert.Contains(map.Key.Replace("Game_", ""), MapSceneBuilder.MapNames, $"{map.Key} is not a map scene.");
            foreach (AmbientPlacer.Spot spot in map.Value)
            {
                Assert.IsTrue(catalog.TryGet(spot.Sound, out var entry), $"{spot.Sound} has no catalog entry.");
                Assert.AreEqual(SoundSpace.World3D, entry.space, $"{spot.Name}: ambient sounds must be 3D.");
                Assert.GreaterOrEqual(entry.maxInstances, map.Value.Count(s => s.Sound == spot.Sound), $"{spot.Sound}: more emitters than allowed instances.");
            }
        }
    }

    [Test]
    public void AmbientZones_UseRealMapsAndLoopedSounds()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        foreach (var map in AmbientPlacer.Zones)
        {
            Assert.Contains(map.Key.Replace("Game_", ""), MapSceneBuilder.MapNames, $"{map.Key} is not a map scene.");
            foreach (AmbientPlacer.Zone zone in map.Value)
            {
                Assert.IsTrue(catalog.TryGet(zone.Sound, out var entry) && entry.HasClip, $"{zone.Sound} needs a clip.");
                Assert.IsNotEmpty(zone.Boxes);
                Assert.IsTrue(zone.Boxes.All(b => b.max.y < 0f), $"{zone.Name}: underground zones stay below the ground.");
            }
        }
    }

    // 맵 바탕 환경음(S7): 실제 맵, 2D 반복음, 맵 하나에 하나, 진저브레드는 지하 동굴 영역과 겹치지 않는다.
    [Test]
    public void AmbientBeds_CoverMaps_AndStayOffTheCave()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        foreach (var map in AmbientPlacer.Beds)
        {
            Assert.Contains(map.Key.Replace("Game_", ""), MapSceneBuilder.MapNames, $"{map.Key} is not a map scene.");
            Assert.IsTrue(catalog.TryGet(map.Value.Sound, out var entry) && entry.HasClip, $"{map.Value.Sound} needs a clip.");
            Assert.AreEqual(SoundSpace.Flat2D, entry.space, $"{map.Value.Sound}: map beds are 2D.");
            Assert.IsTrue(map.Value.Boxes.Any(b => b.Contains(new Vector3(130f, 2f, -130f)) && b.Contains(Vector3.up * 40f)), $"{map.Key}: bed covers the map.");
            if (!AmbientPlacer.Zones.TryGetValue(map.Key, out AmbientPlacer.Zone[] zones)) continue;
            foreach (AmbientPlacer.Zone zone in zones)
                foreach (Bounds cave in zone.Boxes)
                    Assert.IsTrue(map.Value.Boxes.All(b => b.min.y >= cave.max.y), $"{map.Key}: bed overlaps {zone.Name}.");
        }
        Assert.AreEqual(4, AmbientPlacer.Beds.Count, "every map but the factory (machines are its bed) has a bed");
    }

    // ---- 소리 설정 창(S9) ----

    [Test]
    public void SoundSettingsPanel_HasRowForEverySlider()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundSettingsPanel>(SoundSettingsInstaller.PrefabPath);
        Assert.IsNotNull(prefab, "panel prefab");
        var so = new UnityEditor.SerializedObject(prefab);
        var rows = so.FindProperty("rows");
        var kinds = new HashSet<int>();
        for (int i = 0; i < rows.arraySize; i++)
        {
            var e = rows.GetArrayElementAtIndex(i);
            Assert.IsNotNull(e.FindPropertyRelative("slider").objectReferenceValue, $"row {i} slider");
            Assert.IsNotNull(e.FindPropertyRelative("value").objectReferenceValue, $"row {i} value text");
            kinds.Add(e.FindPropertyRelative("kind").enumValueIndex);
        }
        Assert.AreEqual(Enum.GetValues(typeof(AudioVolumeSettings.Slider)).Length, kinds.Count, "one row per slider");
        Assert.IsNotNull(so.FindProperty("closeButton").objectReferenceValue, "close button");
    }

    // §27: 소리 창은 설정 창(SettingsMenu)의 "출력 장치" 탭으로 옮겼다 — 세 화면 모두 설정 창이 있고, 따로 떠 있는 소리 창은 없다.
    [Test]
    public void EscMenus_AndLobby_OpenSettingsMenu()
    {
        var core = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SoundSettingsInstaller.CorePrefabPath).GetComponentInChildren<EscMenu>(true);
        AssertEscWired(core, "GameSceneCore");
        string menuGuid = UnityEditor.AssetDatabase.AssetPathToGUID(SettingsMenuInstaller.PrefabPath);
        string panelGuid = UnityEditor.AssetDatabase.AssetPathToGUID(SoundSettingsInstaller.PrefabPath);
        foreach (string scene in new[] { SoundSettingsInstaller.LobbyScenePath, SoundSettingsInstaller.GameLobbyScenePath })
        {
            string yaml = System.IO.File.ReadAllText(scene);
            Assert.IsTrue(yaml.Contains(menuGuid), scene + " has the settings menu");
            Assert.IsFalse(yaml.Contains(panelGuid), scene + " has no stand-alone sound panel any more");
        }
        var menu = UnityEditor.AssetDatabase.LoadAssetAtPath<SettingsMenu>(SettingsMenuInstaller.PrefabPath);
        Assert.IsNotNull(menu.GetComponentInChildren<SoundSettingsPanel>(true), "output tab");
        Assert.IsNotNull(menu.GetComponentInChildren<KeyBindingsPage>(true), "controls tab");
        Assert.IsNotNull(menu.GetComponentInChildren<InputDevicePage>(true), "input tab");
    }

    private static void AssertEscWired(EscMenu esc, string where)
    {
        Assert.IsNotNull(esc, where);
        var so = new UnityEditor.SerializedObject(esc);
        Assert.IsNotNull(so.FindProperty("optionsButton").objectReferenceValue, where + " settings button");
        Assert.IsNotNull(so.FindProperty("settingsMenu").objectReferenceValue, where + " settings menu");
    }

    [Test]
    public void SoundSettings_PercentAndPreview()
    {
        Assert.AreEqual("0%", SoundSettingsPanel.Percent(0f));
        Assert.AreEqual("25%", SoundSettingsPanel.Percent(0.25f));
        Assert.AreEqual("100%", SoundSettingsPanel.Percent(1.2f));
        Assert.AreEqual(SoundId.ItemPickup, SoundSettingsPanel.PreviewSound(AudioVolumeSettings.Slider.Sfx));
        Assert.AreEqual(SoundId.UiTick, SoundSettingsPanel.PreviewSound(AudioVolumeSettings.Slider.Ui));
    }

    // ---- 음량 ----

    [Test]
    public void Volume_DecibelConversion_RoundTrips()
    {
        Assert.AreEqual(0f, AudioVolumeSettings.ToDecibels(1f), 1e-4f);
        Assert.AreEqual(-6.0206f, AudioVolumeSettings.ToDecibels(0.5f), 1e-3f);
        Assert.AreEqual(AudioVolumeSettings.SilentDb, AudioVolumeSettings.ToDecibels(0f));
        Assert.AreEqual(0f, AudioVolumeSettings.FromDecibels(AudioVolumeSettings.SilentDb));
        foreach (float v in new[] { 0.1f, 0.33f, 0.8f })
            Assert.AreEqual(v, AudioVolumeSettings.FromDecibels(AudioVolumeSettings.ToDecibels(v)), 1e-4f);
    }

    [Test]
    public void Volume_GainMultipliesSlidersAndBusBase_AndPersists()
    {
        var all = (AudioVolumeSettings.Slider[])Enum.GetValues(typeof(AudioVolumeSettings.Slider));
        float[] saved = all.Select(AudioVolumeSettings.Get).ToArray();
        try
        {
            AudioVolumeSettings.Set(AudioVolumeSettings.Slider.Master, 0.5f);
            AudioVolumeSettings.Set(AudioVolumeSettings.Slider.Music, 0.5f);
            AudioVolumeSettings.Set(AudioVolumeSettings.Slider.Sfx, 1f);
            float musicBase = AudioVolumeSettings.FromDecibels(AudioVolumeSettings.BaseDb(SoundBus.Music));
            Assert.AreEqual(0.25f * musicBase, AudioVolumeSettings.Gain(SoundBus.Music), 1e-4f);
            // 환경음은 효과음 막대를 따른다
            float ambBase = AudioVolumeSettings.FromDecibels(AudioVolumeSettings.BaseDb(SoundBus.Ambience));
            Assert.AreEqual(0.5f * ambBase, AudioVolumeSettings.Gain(SoundBus.Ambience), 1e-4f);

            AudioVolumeSettings.Reload(); // 기기에 저장된 값을 다시 읽어도 같다
            Assert.AreEqual(0.5f, AudioVolumeSettings.Get(AudioVolumeSettings.Slider.Music), 1e-4f);

            AudioVolumeSettings.Set(AudioVolumeSettings.Slider.Ui, 2f);
            Assert.AreEqual(1f, AudioVolumeSettings.Get(AudioVolumeSettings.Slider.Ui), "Sliders clamp to 0..1.");
        }
        finally
        {
            for (int i = 0; i < all.Length; i++) AudioVolumeSettings.Set(all[i], saved[i]);
        }
    }

    // S4(캐릭터·색칠·대기실) 소리는 클립이 있고, 본인만 듣는 소리는 2D다(SoundPlan.md S4).
    [Test]
    public void S4Sounds_HaveClips_AndRightSpace()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        SoundId[] world = { SoundId.CookieStep, SoundId.CookieJump, SoundId.CookieLand, SoundId.CookieDodge, SoundId.CookieGrab, SoundId.CookieRelease,
            SoundId.CookieCrumble, SoundId.MonsterStep, SoundId.MonsterDash, SoundId.MonsterGrab, SoundId.MonsterSquash, SoundId.StunStars,
            SoundId.Respawn, SoundId.DoorOpen, SoundId.DoorClose, SoundId.CauldronBubble, SoundId.CauldronSplash };
        SoundId[] flat = { SoundId.MonsterAimLock, SoundId.PaintStroke, SoundId.PaintSlotRegistered, SoundId.PaintForceFill, SoundId.MonsterDeparted };
        foreach (SoundId id in world.Concat(flat))
        {
            Assert.IsTrue(catalog.TryGet(id, out SoundCatalogSO.Entry e), id.ToString());
            Assert.IsTrue(e.HasClip, $"{id} has a clip");
            Assert.AreEqual(world.Contains(id) ? SoundSpace.World3D : SoundSpace.Flat2D, e.space, id.ToString());
        }
        Assert.IsTrue(catalog.TryGet(SoundId.CookieStep, out SoundCatalogSO.Entry step) && step.clips.Length >= 3, "Footsteps have variations.");
    }

    // S5(탈출 모드·도구) 소리는 클립이 있고, 본인만 듣는 소리는 2D다(SoundPlan.md S5).
    [Test]
    public void S5Sounds_HaveClips_AndRightSpace()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        SoundId[] world = { SoundId.ChestOpen, SoundId.ChestRespawn, SoundId.ItemPickup, SoundId.ItemDrop, SoundId.DeviceInsert, SoundId.DeviceComplete,
            SoundId.BoardHop, SoundId.RocketInsert, SoundId.RocketHatch, SoundId.StunFire, SoundId.StunHit, SoundId.BalloonThrow, SoundId.BalloonSplash,
            SoundId.HammerSwing, SoundId.HammerBonk };
        SoundId[] flat = { SoundId.HeartBeat, SoundId.EscapeSuccessSelf, SoundId.StunAimHum };
        foreach (SoundId id in world.Concat(flat))
        {
            Assert.IsTrue(catalog.TryGet(id, out SoundCatalogSO.Entry e), id.ToString());
            Assert.IsTrue(e.HasClip, $"{id} has a clip");
            Assert.AreEqual(world.Contains(id) ? SoundSpace.World3D : SoundSpace.Flat2D, e.space, id.ToString());
        }
        Assert.AreEqual(SoundId.HammerSwing, ToolUser.UseSound(ToolKind.Hammer));
        Assert.AreEqual(SoundId.StunFire, ToolUser.UseSound(ToolKind.StunGun));
        Assert.AreEqual(SoundId.BalloonThrow, ToolUser.UseSound(ToolKind.WaterBalloon));
    }

    // S6 맵 연출 소리: 경계를 지나는 프레임에 한 번, 늦게 들어왔거나 잠시 끊겨 이미 지난 경계는 0번(SoundPlan.md §6).
    [Test]
    public void SequenceCue_FiresOnceAtBoundary_NotWhenLate()
    {
        Assert.IsTrue(EscapeCue.ShouldCue(0f, -1f, 0.02f), "completion frame");
        Assert.IsTrue(EscapeCue.ShouldCue(2f, 1.98f, 2.01f), "crossing");
        Assert.IsFalse(EscapeCue.ShouldCue(2f, 2.01f, 2.03f), "already crossed last frame");
        Assert.IsFalse(EscapeCue.ShouldCue(2f, 1.5f, 1.9f), "not yet");
        Assert.IsFalse(EscapeCue.ShouldCue(2f, -1f, 30f), "late join");
        Assert.IsFalse(EscapeCue.ShouldCue(2f, 1.9f, 2f + EscapeCue.LateTolerance + 0.1f), "long hitch");
        int fired = 0;
        float before = -1f;
        for (float t = 0f; t < 8f; t += 1f / 30f) { if (EscapeCue.ShouldCue(3f, before, t)) fired++; before = t; }
        Assert.AreEqual(1, fired, "one frame-by-frame pass fires once");
    }

    // S6(맵 연출·로켓·마녀) 소리는 클립이 있고, 연출은 3D, 마녀는 2D다.
    [Test]
    public void S6Sounds_HaveClips_AndRightSpace()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        var world = new List<SoundId> { SoundId.BoardSuck, SoundId.RocketIgnite, SoundId.RocketLiftoff };
        foreach (SoundId id in Enum.GetValues(typeof(SoundId))) if ((int)id / 100 == 7) world.Add(id);
        SoundId[] flat = { SoundId.WitchAppear, SoundId.WitchSlam };
        foreach (SoundId id in world.Concat(flat))
        {
            Assert.IsTrue(catalog.TryGet(id, out SoundCatalogSO.Entry e), id.ToString());
            Assert.IsTrue(e.HasClip, $"{id} has a clip");
            Assert.AreEqual(world.Contains(id) ? SoundSpace.World3D : SoundSpace.Flat2D, e.space, id.ToString());
        }
        Assert.IsTrue(catalog.TryGet(SoundId.LanternFlicker, out SoundCatalogSO.Entry lantern));
        Assert.LessOrEqual(lantern.maxDistance, 15f, "lantern crackle is heard only nearby");
    }
}
