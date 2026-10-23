using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 2026-10-11 추가 요청(Request1009Plan.md §16·§17·§18) 시험.
public class Request1011Tests
{
    // ---- §16.1 상자 ----

    [TestCase(GamePhase.Lobby, false)]
    [TestCase(GamePhase.Prepare, false)] // §26 H3: 준비 시간에는 숨긴다
    [TestCase(GamePhase.Hunt, true)]
    [TestCase(GamePhase.TimeAttack, true)]
    [TestCase(GamePhase.Result, true)]
    public void Chests_ShownOnceTheRoundStarts(GamePhase phase, bool visible)
    {
        Assert.AreEqual(visible, EscapeManager.ChestsVisibleIn(phase));
    }

    [Test]
    public void ChestAppear_GrowsOvershootsAndSettles()
    {
        Assert.AreEqual(0f, MaterialChest.AppearScale(0f), 1e-4f);
        Assert.AreEqual(1.08f, MaterialChest.AppearScale(0.7f), 1e-4f);
        Assert.AreEqual(1f, MaterialChest.AppearScale(1f), 1e-4f);
        Assert.Less(MaterialChest.AppearScale(0.3f), MaterialChest.AppearScale(0.6f));
    }

    // ---- §16.2 아이콘 ----

    [Test]
    public void EveryItem_HasBakedIcon()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemSO");
        Assert.Greater(guids.Length, 0);
        foreach (string guid in guids)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.NotNull(item.Icon, $"{item.ItemId}: run Tools/Escape/Bake Item Icons");
            Assert.AreEqual(ItemIconBaker.IconPath(item), AssetDatabase.GetAssetPath(item.Icon));
            Assert.AreEqual(256, item.Icon.texture.width, item.ItemId);
        }
    }

    // ---- §16.3 쿠키 들기 ----

    [Test]
    public void CarriedBody_LiesAcrossCarrierArms()
    {
        Quaternion carrier = Quaternion.Euler(0f, 37f, 0f);
        Quaternion body = CarryPose.CarriedRotation(carrier);
        Assert.Greater(Vector3.Dot(body * Vector3.up, carrier * Vector3.left), 0.99f, "head toward carrier's left");
        Assert.Greater(Vector3.Dot(body * Vector3.forward, Vector3.up), 0.85f, "face looks up");
        Assert.Greater(Vector3.Dot(body * Vector3.forward, carrier * Vector3.back), 0f, "face turns slightly toward carrier");
    }

    [Test]
    public void CarriedRoot_PutsHipsAboveHands()
    {
        Quaternion carrier = Quaternion.Euler(0f, 90f, 0f);
        Vector3 hands = new Vector3(3f, 1.05f, -2f);
        Vector3 hips = CarryPose.CarriedHips(hands, carrier);
        Assert.AreEqual(hands.y + CarryPose.CarriedLift, hips.y, 1e-4f);
        Assert.AreEqual(CarryPose.CarriedForward, Vector3.Dot(hips - hands, carrier * Vector3.forward), 1e-4f);

        Quaternion rot = CarryPose.CarriedRotation(carrier);
        Vector3 hipsLocal = new Vector3(0f, 0.81f, 0f);
        Vector3 root = CarryPose.RootFor(hips, rot, hipsLocal);
        Assert.That((root + rot * hipsLocal - hips).magnitude, Is.LessThan(1e-4f));
    }

    [Test]
    public void ReleasedBody_StandsFacingCarrier()
    {
        Quaternion q = CarryPose.UprightFacing(Vector3.zero, new Vector3(0f, 2f, -5f));
        Assert.Greater(Vector3.Dot(q * Vector3.up, Vector3.up), 0.999f);
        Assert.Greater(Vector3.Dot(q * Vector3.forward, Vector3.back), 0.999f);
    }

    [Test]
    public void CarriedState_IsAppendedAndWiredInAnimator()
    {
        Assert.AreEqual(7, (int)PlayerMoveState.Carried, "append only — older values are sent over the network");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CookieAnimatorSetup.ControllerPath);
        AnimatorState state = controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == CookieAnimatorSetup.CarriedState);
        Assert.NotNull(state, "run Tools/Cookie/Setup Carry Animator States");
        Assert.AreEqual("Cookie_Hanging_Idle", state.motion.name);
        Assert.IsTrue(controller.parameters.Any(p => p.name == CookieAnimatorSetup.CarriedState && p.type == AnimatorControllerParameterType.Trigger));
    }

    [Test]
    public void NetworkSync_DefaultsToNotCarried()
    {
        Assert.AreEqual(-1, new PlayerNetworkSync().RemoteCarrierViewId);
    }

    // ---- §16.4 아이템 들기 ----

    [Test]
    public void CarryPose_StaysOnWhileAnyReasonRemains()
    {
        var driver = new PlayerAnimationDriver(null);
        driver.SetCarry(CarryReason.Cookie, true);
        driver.SetCarry(CarryReason.TwoHandedItem, true);
        driver.SetCarry(CarryReason.Cookie, false);
        Assert.IsTrue(driver.IsCarryPoseOn);
        driver.SetCarry(CarryReason.TwoHandedItem, false);
        Assert.IsFalse(driver.IsCarryPoseOn);
    }

    [Test]
    public void OneHandedGrip_HoldsNearTheBottom()
    {
        // 망치: 가운데 0.04, 반높이 0.285 → 아래 끝(-0.245)에서 0.12 위(-0.125)가 손에 오도록 0.125 올린다.
        Assert.AreEqual(0.125f, HeldItemPresenter.GripLift(0.04f, 0.285f), 1e-4f);
        Assert.AreEqual(0f, HeldItemPresenter.GripLift(0f, 0.12f), 1e-4f);
    }

    // ---- §17 출력 장치(실제 장치 선택) ----

    [Test]
    public void OutputDevice_MissingOrEmptyFallsBackToSystemDefault()
    {
        var devices = new[] { new WindowsAudioEndpoints.Device("{a}", "Speakers"), new WindowsAudioEndpoints.Device("{b}", "Headset") };
        Assert.AreEqual("{b}", OutputDevices.Resolve("{b}", devices));
        Assert.AreEqual(string.Empty, OutputDevices.Resolve("{gone}", devices), "unplugged device -> system default");
        Assert.AreEqual(string.Empty, OutputDevices.Resolve(string.Empty, devices));
        Assert.AreEqual(string.Empty, OutputDevices.Resolve(null, devices));
    }

    [Test]
    public void OutputDevice_IdPacksLikeWindowsSettings()
    {
        const string id = "{0.0.0.00000000}.{2faeaa2e-a09b-491a-8622-f634f69c1de1}";
        string packed = WindowsAudioEndpoints.PackDeviceId(id);
        StringAssert.StartsWith(@"\\?\SWD#MMDEVAPI#", packed);
        StringAssert.EndsWith("#{e6327cad-dcec-4949-ae8a-991e976a79d2}", packed);
        Assert.AreEqual(id, WindowsAudioEndpoints.UnpackDeviceId(packed));
    }

    [Test]
    public void OutputDevice_ListsThisPcOutputs()
    {
        Assert.IsTrue(OutputDevices.IsSupported, "Windows editor");
        var devices = OutputDevices.Available();
        Assert.Greater(devices.Count, 0, "at least one active output device");
        Assert.IsTrue(devices.TrueForAll(d => !string.IsNullOrEmpty(d.Id) && !string.IsNullOrEmpty(d.Name)));
    }

    [Test]
    public void SoundPanel_HasOutputDropdownAndRefresh()
    {
        var panel = AssetDatabase.LoadAssetAtPath<SoundSettingsPanel>(SoundSettingsInstaller.PrefabPath);
        Assert.IsTrue(panel.HasOutputDropdown);
        Transform row = panel.transform.Find("Box/" + SoundSettingsInstaller.OutputRowName);
        Assert.NotNull(row.Find(SoundSettingsInstaller.OutputDropdownName));
        Assert.NotNull(row.Find(SoundSettingsInstaller.OutputRefreshName));
        Assert.IsNull(row.Find("Output_Headset"), "old preset buttons removed");
    }

    // ---- §16.7 베이커리 고장 소리 ----

    [Test]
    public void BrokenOven_HasClipInCatalog()
    {
        Assert.AreEqual(724, (int)SoundId.OvenBroken);
        var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalogSO>("Assets/Resources/Audio/SoundCatalog.asset");
        Assert.IsTrue(catalog.TryGet(SoundId.OvenBroken, out SoundCatalogSO.Entry e));
        Assert.Greater(e.clips.Length, 0, "run Tools/TagOfChaos/Audio/Build Catalogs");
        Assert.Greater(e.clips[0].length, 7.9f);
    }

    // ---- §18.3 스파이가 끼운 재료 → 열린 상자 ----

    private static EscapeState ChestState(params (bool opened, bool spy, bool hasItem)[] chests)
    {
        var s = new EscapeState();
        for (int c = 0; c < chests.Length; c++)
        {
            s.Chests.Add(new EscapeState.Chest { Anchor = c, Opened = chests[c].opened, Spy = chests[c].spy });
            if (chests[c].hasItem) s.Items.Add(new EscapeState.Item { Id = "x" + c, Loc = ItemLocation.Chest, A = c });
        }
        return s;
    }

    [Test]
    public void RespawnChest_PrefersOpenedEmptyChests()
    {
        // 0 닫힘·빈, 1 열림·빈, 2 열림·아이템 있음, 3 스파이 상자(열림), 4 열림·빈
        EscapeState s = ChestState((false, false, false), (true, false, false), (true, false, true), (true, true, false), (true, false, false));
        var picked = new System.Collections.Generic.HashSet<int>();
        for (int roll = 0; roll < 20; roll++) picked.Add(EscapeAuthority.PickRespawnChest(s, roll));
        CollectionAssert.AreEquivalent(new[] { 1, 4 }, picked);
        Assert.That(EscapeAuthority.PickRespawnChest(s, int.MinValue), Is.EqualTo(1).Or.EqualTo(4), "negative roll");
    }

    [Test]
    public void RespawnChest_FallsBackToClosedEmptyThenNone()
    {
        Assert.AreEqual(0, EscapeAuthority.PickRespawnChest(ChestState((false, false, false), (true, false, true)), 7));
        Assert.AreEqual(-1, EscapeAuthority.PickRespawnChest(ChestState((true, false, true), (true, true, false)), 7));
    }

    // ---- §18.2 마녀 모델·주먹 내리치기 ----

    [Test]
    public void WitchModel_HasRigClipsAndPrefab()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(WitchModelSetup.FbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
        foreach (string name in WitchModelSetup.Clips) Assert.IsTrue(clips.Any(c => c.name == name), name);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WitchModelSetup.ControllerPath);
        Assert.AreEqual("Idle", controller.layers[0].stateMachine.defaultState.name);
        Assert.IsTrue(controller.parameters.Any(p => p.name == "Rise" && p.type == AnimatorControllerParameterType.Trigger));
        Assert.IsTrue(controller.parameters.Any(p => p.name == "Strike" && p.type == AnimatorControllerParameterType.Trigger));
        // §23 X3: 뒤돌아섬(2초, 시각표와 같은 길이)·걷기(반복)
        Assert.IsTrue(controller.parameters.Any(p => p.name == "TurnBack" && p.type == AnimatorControllerParameterType.Trigger));
        Assert.IsTrue(controller.parameters.Any(p => p.name == "Walking" && p.type == AnimatorControllerParameterType.Bool));
        Assert.AreEqual(WitchTimeline.TurnSeconds, clips.First(c => c.name == "TurnBack").length, 0.05f);
        Assert.AreEqual(WitchPresenter.WalkCycleSeconds, clips.First(c => c.name == "Walk").length, 0.05f);
        Assert.IsTrue(clips.First(c => c.name == "Walk").isLooping);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WitchModelSetup.PrefabPath);
        var animator = prefab.GetComponentInChildren<Animator>();
        Assert.AreEqual(controller, animator.runtimeAnimatorController);
        Assert.NotNull(prefab.GetComponentInChildren<SkinnedMeshRenderer>());
        Transform rig = animator.transform;
        Assert.NotNull(FindDeep(rig, "Head"));
        Assert.NotNull(FindDeep(rig, "Hand.L"));
        Assert.NotNull(FindDeep(rig, "Finger11.R"));
        // §24.10: 밟기는 왼다리 본을 돌린다
        foreach (string bone in new[] { "Thigh.L", "Shin.L", "Foot.L" }) Assert.NotNull(FindDeep(rig, bone), bone);
    }

    private static Transform FindDeep(Transform t, string name) =>
        t.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);

    // ---- §18.4 마녀 등장 카메라 ----

    // §23.3 A·B → §24.10: 시각표 장면(타임어택: 로켓 3 → 등장 3 → 돌아섬 2 → 얼굴 2 → 복귀 3 … 도착 3테이크 )
    [Test]
    public void WitchTimeline_ShotsFollowTheScript()
    {
        const double launch = 100.0;
        double appear = WitchTimeline.RocketAppearAt(launch);
        double end = WitchTimeline.TimeAttackEnd(appear, 60);
        WitchShot At(double now, bool result = false) => WitchTimeline.ShotAt(WitchMode.TimeAttack, now, true, launch, appear, end, result);
        Assert.AreEqual(WitchShot.None, At(99.0));
        Assert.AreEqual(WitchShot.Lead, At(101.0));
        Assert.AreEqual(WitchShot.Reveal, At(104.0));
        Assert.AreEqual(WitchShot.Turn, At(107.0));
        Assert.AreEqual(WitchShot.Face, At(109.0));
        Assert.AreEqual(WitchShot.Return, At(111.0));
        Assert.AreEqual(WitchShot.None, At(114.0), "time attack: everyone plays");
        Assert.AreEqual(WitchShot.None, At(end - 0.1), "everyone plays until the witch arrives");
        Assert.AreEqual(WitchShot.FinaleLowAngle, At(end + 1.0));
        Assert.AreEqual(WitchShot.FinaleStomp, At(end + 7.0));
        Assert.AreEqual(WitchShot.FinaleEnd, At(end + 11.0));
        Assert.AreEqual(WitchShot.None, At(end + 4.0, result: true), "result screen");
    }

    [Test]
    public void WitchWalk_SpeedFollowsTheTime()
    {
        Assert.AreEqual(0f, WitchTimeline.WalkProgress(10, 20, 80));
        Assert.AreEqual(0.5f, WitchTimeline.WalkProgress(50, 20, 80), 1e-5f);
        Assert.AreEqual(1f, WitchTimeline.WalkProgress(90, 20, 80));
        for (double t = 20; t < 80; t += 3) Assert.LessOrEqual(WitchTimeline.WalkProgress(t, 20, 80), WitchTimeline.WalkProgress(t + 3, 20, 80));
        // 걷는 시간이 길면 동작도 그만큼 느리게 — 걸음 길이 22.5 m, 330 m
        float slow = WitchPresenter.WalkAnimatorSpeed(330f, 60f, 22.5f), fast = WitchPresenter.WalkAnimatorSpeed(330f, 10f, 22.5f);
        Assert.AreEqual(6f, fast / slow, 1e-3f);
        Assert.AreEqual(330f / 10f / 45f * WitchPresenter.WalkCycleSeconds, fast, 1e-4f);
        // 돌아섬: 2초 동안 0 → 1
        Assert.AreEqual(0f, WitchPresenter.TurnAmount(10.0, 10.0), 1e-5f);
        Assert.AreEqual(0.5f, WitchPresenter.TurnAmount(11.0, 10.0), 1e-5f);
        Assert.AreEqual(1f, WitchPresenter.TurnAmount(12.5, 10.0), 1e-5f);
    }

    [Test]
    public void RevealCamera_CloseUpLooksAtFaceFromFront()
    {
        Vector3 face = new Vector3(10f, 80f, -5f);
        Vector3 forward = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward;
        WitchRevealCamera.CloseUpPose(face, forward + Vector3.down * 0.3f, 90f, out Vector3 pos, out Quaternion rot);
        Vector3 flat = Vector3.ProjectOnPlane(pos - face, Vector3.up);
        Assert.AreEqual(90f * WitchRevealCamera.FaceDistanceRatio, flat.magnitude, 1e-3f);
        Assert.Greater(Vector3.Dot(flat.normalized, forward), 0.999f, "in front of the face");
        Assert.Greater(Vector3.Dot(rot * Vector3.forward, (face - pos).normalized), 0.999f, "looks at the face");
    }

    [Test]
    public void RevealCamera_BlendNeverPassesThroughWitch()
    {
        Vector3 face = new Vector3(0f, 80f, 0f);
        Vector3 forward = Vector3.forward;
        float min = 90f * WitchRevealCamera.FaceDistanceRatio;
        Vector3 own = new Vector3(0f, 2f, -150f); // 마녀 등 뒤 먼 곳
        Quaternion ownRot = Quaternion.LookRotation(Vector3.back);
        for (float w = 0f; w <= 1f; w += 0.05f)
        {
            WitchRevealCamera.BlendPose(own, ownRot, face, forward, 90f, w, out Vector3 pos, out Quaternion rot);
            Assert.GreaterOrEqual((pos - face).magnitude, min * 0.999f, "w=" + w);
            if (w >= 0.5f) Assert.Greater(Vector3.Dot(rot * Vector3.forward, (face - pos).normalized), 0.999f, "faces the witch, w=" + w);
        }
        WitchRevealCamera.BlendPose(own, ownRot, face, forward, 90f, 0f, out Vector3 p0, out Quaternion r0);
        Assert.AreEqual(own, p0);
        Assert.AreEqual(ownRot, r0);
    }

    [Test]
    public void RevealCutscene_TenSecondsWithThreeSecondFade()
    {
        Assert.AreEqual(10f, WitchTimeline.IntroSeconds, 1e-4f, "reveal 3 + turn 2 + face 2 + return 3");
        Assert.AreEqual(WitchTimeline.RevealSeconds, WitchPresenter.DefaultFadeSeconds, 1e-4f, "fully visible when the camera arrives");
        Assert.AreEqual(2f, WitchTimeline.TurnSeconds, 1e-4f);
    }

    [Test]
    public void Cutscene_SuppressesGameplayInput()
    {
        bool before = PlayerInput.CutsceneActive;
        try
        {
            PlayerInput.CutsceneActive = true;
            Assert.IsTrue(PlayerInput.IsGameplaySuppressed);
        }
        finally { PlayerInput.CutsceneActive = before; }
    }

    [Test]
    public void BakeryPaint_IsNewDarkVersion()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/10. Audio/BGM/BakeryPaint.ogg");
        Assert.AreEqual(96f, clip.length, 0.1f, "§18.5 version (60 bpm, 24 bars)");
    }

    // ---- §18.6 로비 곡(한 번 재생) ----

    [Test]
    public void LobbyMusic_IsNewOneShotSong()
    {
        Assert.IsFalse(MusicCatalogSO.IsLoopById(MusicId.Lobby), "lobby song plays once");
        Assert.IsTrue(MusicCatalogSO.IsLoopById(MusicId.GameLobby), "other songs still loop");
        var catalog = AssetDatabase.LoadAssetAtPath<MusicCatalogSO>("Assets/Resources/Audio/MusicCatalog.asset");
        Assert.IsTrue(catalog.TryGet(MusicId.Lobby, out MusicCatalogSO.Entry entry));
        Assert.IsFalse(entry.loop);
        Assert.AreEqual(28f, entry.clip.length, 0.1f, "§18.6 Lobby_Long1_25_Bass2 (28 s)");
    }

    // ---- §20 결과 화면 곡(Result_A)·고장 오븐 톱니(GearB2) ----

    [Test]
    public void ResultMusic_IsResultALoop()
    {
        Assert.AreEqual(MusicId.Result, MusicRules.Select(new MusicContext { Scene = MusicScene.Bakery, Phase = GamePhase.Result }));
        Assert.IsTrue(MusicCatalogSO.IsLoopById(MusicId.Result), "result song loops while the result board is shown");
        var catalog = AssetDatabase.LoadAssetAtPath<MusicCatalogSO>("Assets/Resources/Audio/MusicCatalog.asset");
        Assert.IsTrue(catalog.TryGet(MusicId.Result, out MusicCatalogSO.Entry entry));
        Assert.IsTrue(entry.loop);
        Assert.AreEqual(72f, entry.clip.length, 0.1f, "Result_A (E minor 3/4 60, 24 bars)");
    }

    [Test]
    public void OvenBroken_IsGearB2Loop()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/10. Audio/SFX/Device/OvenBroken.wav");
        Assert.AreEqual(8f, clip.length, 0.01f, "8 s seamless loop");
        float[] data = new float[clip.samples * clip.channels];
        Assert.IsTrue(clip.GetData(data, 0));
        // GearB2: 쇠끼리 부딪히는 높은 소리라 이웃 샘플 차이(고음 성분)가 크다 — 예전 증기·쿵 소리보다 훨씬 거칠다
        double diff = 0, level = 0;
        for (int i = 1; i < data.Length; i++) { diff += Mathf.Abs(data[i] - data[i - 1]); level += Mathf.Abs(data[i]); }
        Assert.Greater(diff / level, 0.17, "metal-on-metal (bright) rather than low thuds — old steam/thud loop was 0.10");
    }

    // ---- §19 로켓 뒤 마녀·쿠키 탈출 뒤 처단·훔치기 10초·강퇴 ----

    [Test]
    public void WitchTimeline_RocketShownBeforeWitch()
    {
        Assert.AreEqual(103.0, WitchTimeline.RocketAppearAt(100.0), 1e-6, "rocket shot 3 s");
        // 타임어택 = 마녀 등장 + 시작 연출 10초 + 방 설정 시간(연출 동안 잃는 시간 없음)
        Assert.AreEqual(103.0 + 10.0 + 180.0, WitchTimeline.TimeAttackEnd(103.0, 180), 1e-6);
        Assert.AreEqual(113.0, WitchTimeline.WalkStart(103.0), 1e-6);
    }

    // §23.1 B: 마녀가 오면 모두(탈출한 쿠키 포함)가 본다.
    [TestCase(WitchMode.TimeAttack, true)]
    [TestCase(WitchMode.None, false)]
    public void WitchTimeline_WhoWatchesCutscene(WitchMode mode, bool watches)
    {
        Assert.AreEqual(watches, WitchTimeline.Watches(mode));
    }

    private static System.Collections.Generic.List<EscapeRules.PlayerInfo> MonsterAndSpyLeft() => new System.Collections.Generic.List<EscapeRules.PlayerInfo>
    {
        new EscapeRules.PlayerInfo { IsMonster = true },
        new EscapeRules.PlayerInfo { IsSpy = true },
        new EscapeRules.PlayerInfo { Escaped = true },
    };

    // §25.5 D: 마녀 타이머가 끝나면 밟고, 박살을 다 보여 준 뒤 끝. 괴물만 남으면 바로 끝.
    [Test]
    public void EscapeRules_WitchStrikesWhenTimerEnds()
    {
        var players = MonsterAndSpyLeft();
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(players, true, 63.0, 64.0, -1f));
        Assert.AreEqual(EscapeRules.Decision.StrikeWitch, EscapeRules.Evaluate(players, true, 64.0, 64.0, -1f));
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(players, true, 65.0, 64.0, 1f));
        Assert.AreEqual(EscapeRules.Decision.EndWitchStrike, EscapeRules.Evaluate(players, true, 67.0, 64.0, EscapeRules.WitchSlamSeconds));
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(players, false, 9999.0, 0.0, -1f), "no game time limit");
        var none = new System.Collections.Generic.List<EscapeRules.PlayerInfo> { new EscapeRules.PlayerInfo { IsMonster = true }, new EscapeRules.PlayerInfo { Escaped = true } };
        Assert.AreEqual(EscapeRules.Decision.EndAllResolved, EscapeRules.Evaluate(none, true, 30.0, 60.0, -1f));
    }

    [Test]
    public void LeadShot_MovesInAndLooksAtTarget()
    {
        Assert.AreEqual(0f, WitchRevealCamera.LeadWeight(-1f));
        Assert.AreEqual(0f, WitchRevealCamera.LeadWeight(0f), 1e-4f);
        Assert.AreEqual(0.5f, WitchRevealCamera.LeadWeight(1f), 1e-4f);
        Assert.AreEqual(1f, WitchRevealCamera.LeadWeight(3f), 1e-4f);

        Vector3 anchor = new Vector3(10f, 0f, 5f);
        WitchRevealCamera.LeadPose(anchor, Vector3.right, anchor, out Vector3 pos, out Quaternion rot);
        Vector3 flat = Vector3.ProjectOnPlane(pos - anchor, Vector3.up);
        Assert.AreEqual(WitchRevealCamera.LeadDistance, flat.magnitude, 1e-3f);
        Assert.Greater(Vector3.Dot(flat.normalized, Vector3.right), 0.999f);
        Assert.Greater(Vector3.Dot(rot * Vector3.forward, (anchor + Vector3.up - pos).normalized), 0.999f);

        // 로켓이 40 m 떠오르면 카메라도 일부 따라 오르고 여전히 로켓을 본다
        Vector3 risen = anchor + Vector3.up * 40f;
        WitchRevealCamera.LeadPose(anchor, Vector3.right, risen, out Vector3 pos2, out Quaternion rot2);
        Assert.Greater(pos2.y, pos.y + 10f);
        Assert.Greater(Vector3.Dot(rot2 * Vector3.forward, (risen + Vector3.up - pos2).normalized), 0.999f);
    }

    // §23.4 Q1: 전신 장면은 맵 쪽에서 몸 전체(발~모자)가 화면에 들어온다.
    [Test]
    public void FullBodyShot_ShowsTheWholeWitchFromTheMapSide()
    {
        Vector3 feet = new Vector3(0f, 0f, 450f);
        const float height = 90f;
        WitchRevealCamera.FullBodyPose(feet, Vector3.back, height, out Vector3 pos, out Quaternion rot);
        Assert.Less(pos.z, feet.z, "on the map side");
        var cam = new GameObject("__bodyCam").AddComponent<Camera>();
        try
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = 60f; cam.aspect = 16f / 9f;
            foreach (Vector3 p in new[] { feet, feet + Vector3.up * height })
            {
                Vector3 v = cam.WorldToViewportPoint(p);
                Assert.That(v.z > 0f && v.y > 0f && v.y < 1f, Is.True, "in view: " + p + " -> " + v);
            }
        }
        finally { Object.DestroyImmediate(cam.gameObject); }
    }

    [Test]
    public void Kick_OnlyHostInLobbyAndNotSelf()
    {
        Assert.IsTrue(LobbyKick.CanKick(1, 1, 2, true, GamePhase.Lobby));
        Assert.IsFalse(LobbyKick.CanKick(3, 1, 2, true, GamePhase.Lobby), "not the host");
        Assert.IsFalse(LobbyKick.CanKick(1, 1, 1, true, GamePhase.Lobby), "host cannot kick self");
        Assert.IsFalse(LobbyKick.CanKick(1, 1, 2, false, GamePhase.Lobby), "already left");
        Assert.IsFalse(LobbyKick.CanKick(1, 1, 2, true, GamePhase.Prepare), "round in progress");
        Assert.IsFalse(LobbyKick.CanKick(1, 1, 2, true, GamePhase.Hunt));
        Assert.AreNotEqual(NetEventCodes.KickRequest, NetEventCodes.Kicked);
    }

    [Test]
    public void Kick_ButtonInPlayerListItem()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyKickInstaller.PrefabPath);
        Transform button = prefab.transform.Find(LobbyKickInstaller.KickButtonName);
        Assert.NotNull(button, "run Tools/Lobby/Setup Kick Button");
        Assert.IsFalse(button.gameObject.activeSelf, "hidden until the host's screen turns it on");
        var item = prefab.GetComponent<PlayerListItem>();
        Assert.AreEqual(button.GetComponent<UnityEngine.UI.Button>(), new SerializedObject(item).FindProperty("kickButton").objectReferenceValue);
    }

    [Test]
    public void Kick_NoticeIsShownOnce()
    {
        LobbyKick.ConsumeKickedNotice();
        LobbyKick.MarkKicked();
        Assert.IsTrue(LobbyKick.ConsumeKickedNotice());
        Assert.IsFalse(LobbyKick.ConsumeKickedNotice());
    }
}
