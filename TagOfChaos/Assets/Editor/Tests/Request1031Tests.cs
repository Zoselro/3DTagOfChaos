using System.IO;
using NUnit.Framework;
using UnityEngine;

// Request1009Plan.md §46 — 잡는 순간 도망친 쿠키(되돌림 판정)·스태미나(쿠키·AI).
public class Request1031Tests
{
    private const float Reach = 3.87f; // 몸 앞면 1.87 + 2
    private const float MaxHeight = 6f;

    // ---------------- 위치 기록(GK0) ----------------

    private static PositionTrack Running(double start, Vector3 from, Vector3 velocity, int samples)
    {
        var t = new PositionTrack(30);
        for (int i = 0; i < samples; i++) t.Record(start + i * 0.05, from + velocity * (i * 0.05f));
        return t;
    }

    [Test]
    public void Track_MinDistanceFollowsPathBetweenSamples()
    {
        PositionTrack t = Running(100, Vector3.zero, new Vector3(20f, 0f, 0f), 10); // 0 → 9 m
        Vector3 now = new Vector3(9f, 0f, 0f);
        Assert.IsTrue(t.TryMinDistance(new Vector3(2.5f, 0f, 1f), 100, 100.45, now, out float d));
        Assert.AreEqual(1f, d, 1e-3f, "distance to the segment between samples");
        Assert.IsTrue(t.TryMinDistance(new Vector3(2.5f, 0f, 0f), 100.3, 100.45, now, out float late));
        Assert.AreEqual(2.5f, late, 1e-3f, "the path starts at the last sample before the window (x = 5), not at x = 0");
    }

    [Test]
    public void Track_KeepsOnlyCapacityAndResetsOnTimeReverse()
    {
        var t = new PositionTrack(30);
        for (int i = 0; i < 50; i++) t.Record(i * 0.05, Vector3.right * i);
        Assert.AreEqual(30, t.Count);
        Assert.AreEqual(20 * 0.05, t.OldestTime, 1e-9, "1.5 s kept");
        Assert.IsFalse(t.TryMinDistance(Vector3.zero, 0, 0.5, Vector3.zero, out _), "older than the buffer → no history");
        t.Record(0.1, Vector3.zero);
        Assert.AreEqual(1, t.Count, "clock going back clears the track");
    }

    // ---------------- 되돌림 판정(GK1) ----------------

    [Test]
    public void Grab_ConfirmsWhenMonsterSawCookieInReach()
    {
        Assert.AreEqual(NetSecurityRules.GrabVerdict.Confirmed,
            NetSecurityRules.JudgeGrabClaim(Vector3.zero, new Vector3(0f, 0f, 3.5f), Reach, MaxHeight, 0.15f, 0.3f, 0.2f));
    }

    [Test]
    public void Grab_RejectsMismatchedOldOrFarClaims()
    {
        Vector3 cookie = new Vector3(0f, 0f, 3.5f);
        Assert.AreEqual(NetSecurityRules.GrabVerdict.CookieMismatch,
            NetSecurityRules.JudgeGrabClaim(Vector3.zero, cookie, Reach, MaxHeight, 0.15f, 3f, 0.2f), "cookie never was there");
        Assert.AreEqual(NetSecurityRules.GrabVerdict.MonsterMismatch,
            NetSecurityRules.JudgeGrabClaim(Vector3.zero, cookie, Reach, MaxHeight, 0.15f, 0.2f, 3f));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.ClaimTooOld,
            NetSecurityRules.JudgeGrabClaim(Vector3.zero, cookie, Reach, MaxHeight, 0.7f, 0f, 0f));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.OutOfReach,
            NetSecurityRules.JudgeGrabClaim(Vector3.zero, new Vector3(0f, 0f, 5f), Reach, MaxHeight, 0.1f, 0f, 0f));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.OutOfReach,
            NetSecurityRules.JudgeGrabClaim(Vector3.zero, new Vector3(0f, 7f, 1f), Reach, MaxHeight, 0.1f, 0f, 0f), "too high");
    }

    [Test]
    public void Grab_PreconditionsKeepOldChecks()
    {
        Assert.AreEqual(NetSecurityRules.GrabVerdict.NotMonster, NetSecurityRules.GrabPreconditions(false, true, true, true, 99f, 1f));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.WrongPhase, NetSecurityRules.GrabPreconditions(true, false, true, true, 99f, 1f));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.NotAlive, NetSecurityRules.GrabPreconditions(true, true, false, true, 99f, 1f));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.OffMap, NetSecurityRules.GrabPreconditions(true, true, true, false, 99f, 1f));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.TooSoon, NetSecurityRules.GrabPreconditions(true, true, true, true, 0.5f, 1f));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.Confirmed, NetSecurityRules.GrabPreconditions(true, true, true, true, 99f, 1f));
    }

    // 사용자가 본 버그: 괴물 화면에서 손 안(3.5 m)이었는데 쿠키가 26 m/s로 달아나 방장이 받았을 때(0.25초 뒤)는 10 m 밖.
    [Test]
    public void Grab_CookieFleeingAt26MpsWithQuarterSecondLagIsConfirmed()
    {
        double seenAt = 200.0;
        Vector3 velocity = new Vector3(0f, 0f, 26f);
        Vector3 cookieAtSeen = new Vector3(0f, 0f, 3.5f);
        // 방장 화면의 쿠키 길: 본 시각 0.5초 전부터 지금(본 시각 + 0.25초)까지
        PositionTrack cookieTrack = Running(seenAt - 0.5, cookieAtSeen - velocity * 0.5f, velocity, 16);
        PositionTrack monsterTrack = Running(seenAt - 0.5, Vector3.zero, Vector3.zero, 16);
        double now = seenAt + 0.25;
        Vector3 cookieNow = cookieAtSeen + velocity * 0.25f;
        Assert.Greater(cookieNow.z, Reach + NetSecurityRules.GrabReachSlack, "the old rule (master's current position + 3 m) rejects this");
        Assert.IsFalse(NetSecurityRules.CanConfirmGrabKill(true, true, true, true, Vector3.zero, cookieNow, Reach, MaxHeight, 99f, 1f));

        double from = now - 0.25 - NetSecurityRules.GrabRewindWindow;
        Assert.IsTrue(cookieTrack.TryMinDistance(cookieAtSeen, from, now, cookieNow, out float cookieError));
        Assert.IsTrue(monsterTrack.TryMinDistance(Vector3.zero, from, now, Vector3.zero, out float monsterError));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.Confirmed,
            NetSecurityRules.JudgeGrabClaim(Vector3.zero, cookieAtSeen, Reach, MaxHeight, 0.25f, cookieError, monsterError));

        // 조작: 손 안이지만 쿠키가 있지도 않았던 곳(길에서 옆으로 2.5 m)을 주장하면 거절
        Vector3 fake = new Vector3(2.5f, 0f, 2.5f);
        Assert.IsTrue(cookieTrack.TryMinDistance(fake, from, now, cookieNow, out float fakeError));
        Assert.AreEqual(NetSecurityRules.GrabVerdict.CookieMismatch,
            NetSecurityRules.JudgeGrabClaim(Vector3.zero, fake, Reach, MaxHeight, 0.25f, fakeError, monsterError));
    }

    // 멀티 확인(2026-10-31): 괴물이 달려와 잡으면 괴물이 보낸 "지금 내 위치"는 방장 화면의 괴물보다 앞이다(지연 + 보간).
    [Test]
    public void Grab_RunningMonsterAheadOfMastersViewIsConfirmed()
    {
        Vector3 velocity = new Vector3(0f, 0f, 20f);
        double now = 300.0;
        float age = 0.12f;
        // 방장 화면의 괴물은 실제보다 age + 0.15초 늦다 → 방장 기록의 마지막 위치는 z = 0
        PositionTrack monsterTrack = Running(now - 1.0, -velocity * 1.0f, velocity, 21);
        Vector3 masterView = Vector3.zero;
        Vector3 claimed = velocity * (age + 0.15f); // 괴물 본인 화면의 실제 위치 z = 5.4
        double from = now - age - NetSecurityRules.GrabRewindWindow;
        Assert.IsTrue(monsterTrack.TryMinDistance(claimed, from, now, masterView, out float plain));
        Assert.Greater(plain, NetSecurityRules.GrabClaimPathTolerance, "without lead the master rejected (MonsterMismatch) — the reported bug");
        Assert.IsTrue(monsterTrack.TryMinDistanceLeading(claimed, from, now, masterView, NetSecurityRules.RemoteLead(age), out float led));
        Assert.LessOrEqual(led, NetSecurityRules.GrabClaimPathTolerance);
        Assert.AreEqual(NetSecurityRules.GrabVerdict.Confirmed,
            NetSecurityRules.JudgeGrabClaim(claimed, claimed + new Vector3(0f, 0f, 3.5f), Reach, MaxHeight, age, 0.2f, led));

        // 그래도 앞으로 10 m 넘게 앞선 위치를 주장하면 거절
        Assert.IsTrue(monsterTrack.TryMinDistanceLeading(velocity * 0.8f, from, now, masterView, NetSecurityRules.RemoteLead(age), out float far));
        Assert.Greater(far, NetSecurityRules.GrabClaimPathTolerance);
    }

    // ---------------- AI 잡기 되돌림(GK4) ----------------

    [Test]
    public void DecoyRoute_MinDistanceOverWindow()
    {
        var r = new DecoyRoute { StartAt = 10, Gait = DecoyGait.Walk, Speed = 20f, FaceYaw = float.NaN, Points = new[] { Vector3.zero, new Vector3(0, 0, 100) } };
        Assert.AreEqual(0f, DecoyRoute.MinDistance(r, new Vector3(0, 0, 10), 10.0, 11.0), 0.6f, "passed z=10 at t=10.5");
        Assert.Greater(DecoyRoute.MinDistance(r, new Vector3(0, 0, 10), 11.0, 11.5), 9f, "already past");
    }

    // ---------------- 스태미나(ST1) ----------------

    private static StaminaModel Full() => new StaminaModel(new StaminaSettings(), float.MaxValue);

    private static void Run(StaminaModel s, float seconds, bool wantsRun, bool moving)
    {
        for (float t = 0f; t < seconds - 1e-4f; t += 0.02f) s.Tick(0.02f, wantsRun, moving);
    }

    [Test]
    public void Stamina_FiveSecondsOfRunningEmptiesAndExhausts()
    {
        StaminaModel s = Full();
        Run(s, 4.9f, true, true);
        Assert.IsTrue(s.CanRun);
        Run(s, 0.2f, true, true);
        Assert.AreEqual(0f, s.Value, 1e-3f);
        Assert.IsTrue(s.IsExhausted);
        Assert.IsTrue(s.ConsumeJustExhausted(), "breath once");
        Assert.IsFalse(s.ConsumeJustExhausted());
        Assert.IsFalse(s.Tick(0.02f, true, true), "exhausted → walks even with Shift");
    }

    [Test]
    public void Stamina_RegenDelayAndRecoverThreshold()
    {
        StaminaModel s = Full();
        while (s.CanRun) s.Tick(0.02f, true, true);
        Run(s, 0.78f, false, false);
        Assert.AreEqual(0f, s.Value, 0.6f, "no regen for 0.8 s");
        Run(s, 1.2f, false, false); // 0.4 s of regen at 25/s ≈ 10
        Assert.IsTrue(s.IsExhausted, "still under 35");
        Assert.IsFalse(s.CanRun);
        Run(s, 1.0f, false, false);
        Assert.IsFalse(s.IsExhausted, "35 reached");
        Assert.IsTrue(s.CanRun);
    }

    [Test]
    public void Stamina_WalkingRegeneratesSlower()
    {
        StaminaModel stand = new StaminaModel(new StaminaSettings(), 0f);
        StaminaModel walk = new StaminaModel(new StaminaSettings(), 0f);
        Run(stand, 1f, false, false);
        Run(walk, 1f, false, true);
        Assert.AreEqual(25f, stand.Value, 0.6f);
        Assert.AreEqual(17.5f, walk.Value, 0.6f);
    }

    [Test]
    public void Stamina_DodgeCosts25AndNeedsEnough()
    {
        StaminaModel s = new StaminaModel(new StaminaSettings(), 60f);
        Assert.IsTrue(s.TrySpendDodge());
        Assert.IsTrue(s.TrySpendDodge());
        Assert.AreEqual(10f, s.Value, 1e-3f);
        Assert.IsFalse(s.CanDodge);
        Assert.IsFalse(s.TrySpendDodge());
        Assert.AreEqual(10f, s.Value, 1e-3f, "a refused dodge costs nothing");
    }

    [Test]
    public void Stamina_SettingsLiveInGameSettings()
    {
        GameSettingsSO settings = Resources.Load<GameSettingsSO>("GameSettings");
        Assert.IsNotNull(settings);
        StaminaSettings st = settings.Stamina;
        Assert.AreEqual(100f, st.max);
        Assert.AreEqual(20f, st.runDrainPerSecond);
        Assert.AreEqual(25f, st.regenPerSecond);
        Assert.AreEqual(0.8f, st.regenDelay, 1e-4f);
        Assert.AreEqual(0.7f, st.walkRegenMultiplier, 1e-4f);
        Assert.AreEqual(35f, st.exhaustedRecoverAt);
        Assert.AreEqual(25f, st.dodgeCost);
    }

    // ---------------- 방장 추정(ST4) ----------------

    [Test]
    public void Watch_AlertsOnlyWhenRunningPastTheLimit()
    {
        // 정직한 쿠키: Shift를 계속 누르지만 자기 스태미나 규칙대로 지치면 걷는다(30초)
        var honest = new StaminaWatch(new StaminaSettings());
        var client = Full();
        float now = 0f;
        for (int i = 0; i < 150; i++)
        {
            bool ran = client.Tick(0.2f, true, true);
            Assert.IsFalse(honest.Observe(0.2f, ran ? 26f : 20f, 20f, now += 0.2f), "honest player never alerts");
        }
        var cheat = new StaminaWatch(new StaminaSettings());
        bool alerted = false;
        now = 0f;
        for (int i = 0; i < 100; i++) alerted |= cheat.Observe(0.2f, 26f, 20f, now += 0.2f);
        Assert.IsTrue(alerted, "20 s of non-stop running alerts");
    }

    // ---------------- AI 스태미나(ST6·ST7) ----------------

    [Test]
    public void DecoyRoute_RunUntilSwitchesToWalkSpeed()
    {
        var r = new DecoyRoute { StartAt = 0, Gait = DecoyGait.Run, Speed = 26f, RunUntil = 52f, FaceYaw = float.NaN,
                                 Points = new[] { Vector3.zero, new Vector3(0, 0, 112) } };
        Assert.AreEqual(2f, r.RunSeconds, 1e-4f);
        Assert.AreEqual(2f + 60f / 20f, r.TravelSeconds, 1e-3f, "52 m running + 60 m walking at 20 m/s");
        DecoyRoute.Pose early = DecoyRoute.Sample(r, 1.0);
        Assert.AreEqual(26f, early.Position.z, 1e-3f);
        Assert.IsTrue(early.Running);
        DecoyRoute.Pose late = DecoyRoute.Sample(r, 3.0);
        Assert.AreEqual(72f, late.Position.z, 1e-3f);
        Assert.IsTrue(late.Moving);
        Assert.IsFalse(late.Running, "walks after the run limit");

        var all = r;
        all.RunUntil = 0f;
        Assert.IsTrue(DecoyRoute.Sample(all, 3.0).Running, "0 = run the whole way");
        Assert.AreEqual(112f / 26f, all.TravelSeconds, 1e-3f);
    }

    [Test]
    public void DecoyRoute_Format2RoundTripAndReadsFormat1()
    {
        var points = new Vector3[DecoyRoute.MaxPoints];
        for (int i = 0; i < points.Length; i++) points[i] = new Vector3(i * 3f, 0f, i * 2f);
        var r = new DecoyRoute { StartAt = 55.5, Gait = DecoyGait.Run, Speed = 26f, RunUntil = 37.5f, HoldUntil = 9f, FaceYaw = float.NaN, Points = points };
        byte[] data = DecoyRoute.Encode(r);
        Assert.LessOrEqual(data.Length, 92);
        Assert.AreEqual(2, data[0]);
        Assert.IsTrue(DecoyRoute.TryDecode(data, out DecoyRoute d));
        Assert.AreEqual(37.5f, d.RunUntil, 0.05f);

        // 형식 1(RunUntil 없음): 끝까지 달림
        byte[] v1 = new byte[data.Length - 2];
        System.Array.Copy(data, 0, v1, 0, 17);
        System.Array.Copy(data, 19, v1, 17, data.Length - 19);
        v1[0] = 1;
        Assert.IsTrue(DecoyRoute.TryDecode(v1, out DecoyRoute old));
        Assert.AreEqual(0f, old.RunUntil);
        Assert.AreEqual(points.Length, old.Points.Length);
        Assert.Less(Vector3.Distance(points[5], old.Points[5]), 0.01f);
    }

    [Test]
    public void DecoyBrain_AdoptsAtFiftyStamina()
    {
        Assert.AreEqual(50f, DecoyBrain.AdoptedStamina);
        var s = new StaminaModel(new StaminaSettings(), DecoyBrain.AdoptedStamina);
        Assert.AreEqual(2.5f, s.RunSecondsLeft, 1e-4f, "50 → 2.5 s of running");
    }

    // ---------------- 대기실 무한 스태미나·AI 자리·막대 위치(2026-10-31 후속) ----------------

    [Test]
    public void Lobby_HasNoStaminaAndMapPointsMustBeWalkable()
    {
        // 대기실에는 EscapeManager가 없다 → Stamina가 null → 달리기·회피 제한 없음
        Assert.IsFalse(EscapeManager.IsActive, "edit mode / lobby has no active escape manager");
        string root = Path.Combine(Application.dataPath, "02. Scripts");
        string player = File.ReadAllText(Path.Combine(root, "Unit/HideOrSeekPlayer.cs"));
        StringAssert.Contains("Stamina => EscapeManager.IsActive ? stamina : null", player);
        string lobby = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes/GameLobbyScene.unity"));
        string guid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/02. Scripts/Escape/EscapeManager.cs");
        Assert.IsFalse(lobby.Contains(guid), "GameLobbyScene must not contain an EscapeManager");

        string manager = File.ReadAllText(Path.Combine(root, "Escape/EscapeManager.cs"));
        StringAssert.Contains("IsClearForCharacter(point) && IsWalkableFromSpawn(point)", manager, "AI spawn points must be reachable ground, not roofs/beams");
        Assert.Greater(EscapeHud.HotbarTop, 0f);
        string hud = File.ReadAllText(Path.Combine(root, "Escape/EscapeHud.cs"));
        StringAssert.Contains("Init(HotbarWidth, HotbarTop)", hud, "stamina bar sits right above inventory slots 1-4");
    }

    // ---------------- 방장만 대기실에 남음(2026-10-31 멀티) ----------------

    [Test]
    public void Start_RefusedWhileRoomIsMovingOrLoading()
    {
        Assert.IsFalse(StartReadiness.IsRoomMoving("GameLobbyScene", "GameLobbyScene", false), "normal lobby start");
        Assert.IsFalse(StartReadiness.IsRoomMoving(null, "GameLobbyScene", false), "no room scene yet");
        Assert.IsTrue(StartReadiness.IsRoomMoving("CursedCandyCarnival", "GameLobbyScene", false), "room already points at a map → a second start would pick another map");
        Assert.IsTrue(StartReadiness.IsRoomMoving("GameLobbyScene", "GameLobbyScene", true), "a scene load is still running");
    }

    // PUN LoadLevel을 직접 부르면 방장일 때 진행 중이던 로드를 취소해 그 뒤 로드가 막힌다 — 모든 호출은 PunSceneLoad.Load를 거친다.
    [Test]
    public void Sources_AllSceneLoadsGoThroughPunSceneLoad()
    {
        string root = Path.Combine(Application.dataPath, "02. Scripts");
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith("PunSceneLoad.cs")) continue;
            foreach (string line in File.ReadAllLines(file))
            {
                string code = line.Contains("//") ? line.Substring(0, line.IndexOf("//")) : line;
                Assert.IsFalse(code.Contains("PhotonNetwork.LoadLevel("), $"{Path.GetFileName(file)} calls PhotonNetwork.LoadLevel directly: {line.Trim()}");
            }
        }
        Assert.IsNotNull(typeof(Photon.Pun.PhotonNetwork).GetField("_AsyncLevelLoadingOperation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static), "PUN private field used by PunSceneLoad still exists");
        string start = File.ReadAllText(Path.Combine(root, "Lobby/GameStartAuthority.cs"));
        StringAssert.Contains("StartReadiness.IsRoomMoving(RoomSceneFollower.RoomScene(), SceneNames.GameLobby, PunSceneLoad.IsBusy)", start);
        string reset = File.ReadAllText(Path.Combine(root, "Monster/RoundStateResetter.cs"));
        StringAssert.Contains("roomScene != SceneNames.GameLobby) || PunSceneLoad.IsBusy) return false;", reset, "the resetter must not wipe a round that just started");
    }

    // 2026-11-02 멀티: 대기실로 돌아온 방장의 첫 프레임에 curScn 응답이 아직 처리 전이라(씬 로드 중 큐 멈춤) 캐시가 옛 맵을 가리켰고,
    // 방 초기화가 "새 판 시작"으로 오해해 한 번 건너뛴 뒤 다시 시도하지 않아 방이 닫힌 채 남았다(로비에서 3/4인데 입장 불가).
    [Test]
    public void Resetter_RetriesUntilRoomSceneIsLobby()
    {
        Assert.IsTrue(StartReadiness.IsRoomMoving("CandyForest", "GameLobbyScene", false), "stale curScn looks like 'moving' — the reset must wait, not give up");
        string root = Path.Combine(Application.dataPath, "02. Scripts");
        string reset = File.ReadAllText(Path.Combine(root, "Monster/RoundStateResetter.cs"));
        StringAssert.Contains("if (roomResetPending) roomResetPending = !TryResetRoom();", reset, "retried every frame until it succeeds");
        StringAssert.Contains("roomScene != SceneNames.GameLobby) || PunSceneLoad.IsBusy) return false;", reset, "stale map curScn or a running load → retry later");
        StringAssert.Contains("roomResetPending = !TryResetRoom();", reset);
    }

    // ---------------- 잡힌 AI 동작·거절 알림(GK3·GK6) ----------------

    [Test]
    public void Sources_RejectionRpcAndDecoyHeldMotion()
    {
        string root = Path.Combine(Application.dataPath, "02. Scripts");
        string player = File.ReadAllText(Path.Combine(root, "Unit/HideOrSeekPlayer.cs"));
        StringAssert.Contains("RpcGrabKillRejected", player);
        StringAssert.Contains("lifePresenter.CancelGrabKill()", player);
        StringAssert.Contains("monster.CancelGrabKill()", player);
        string decoy = File.ReadAllText(Path.Combine(root, "Escape/DecoyView.cs"));
        StringAssert.Contains("SetMotion(PlayerMoveState.Held, 0)", decoy, "caught AI plays the same Held motion as a caught cookie");
    }
}
