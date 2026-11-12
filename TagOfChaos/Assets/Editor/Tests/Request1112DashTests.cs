using NUnit.Framework;
using UnityEngine;

// Request1009Plan.md §70.2 — 돌진 잡기 판정(돌진 예고·돌진 길·패킷 기준 기록·AI 경로 이력).
public class Request1112DashTests
{
    private const float Speed = 68f, Distance = 20f, Windup = 0.35f, Cooldown = 5f;

    private static DashWindow Window(double startsAt) =>
        new DashWindow { Start = Vector3.zero, Direction = Vector3.forward, MoveStartsAt = startsAt, Speed = Speed, Distance = Distance };

    [Test]
    public void DashWindow_ReachableGrowsWithTimeAndStopsAtDistance()
    {
        DashWindow w = Window(10.0);
        Assert.AreEqual(0f, w.ReachableAt(9.9), 1e-4f);
        Assert.AreEqual(6.8f, w.ReachableAt(10.1), 1e-3f);
        Assert.AreEqual(Distance, w.ReachableAt(11.0), 1e-4f);
        Assert.IsTrue(w.Covers(10.2, 0f));
        Assert.IsFalse(w.Covers(10.0 + Distance / Speed + 0.6, 0.5f));
    }

    [Test]
    public void DashWindow_PathErrorAcceptsOnPathAndRejectsBeyondReach()
    {
        DashWindow w = Window(10.0);
        // 0.1초 뒤 6.8 m 지점: 길 위
        Assert.Less(w.PathError(new Vector3(0f, 0.4f, 6.8f), 10.1, 0f), 0.01f);
        // 같은 시각에 18 m 지점을 주장: 갈 수 있는 거리보다 11 m 앞
        Assert.Greater(w.PathError(new Vector3(0f, 0f, 18f), 10.1, 0f), 10f);
        // 길 옆으로 5 m
        Assert.AreEqual(5f, w.PathError(new Vector3(5f, 0f, 3f), 10.2, 0f), 0.01f);
        // 시간 여유 0.1초면 6.8 m 더 간 것으로 본다
        Assert.Less(w.PathError(new Vector3(0f, 0f, 13f), 10.1, 0.1f), 0.01f);
    }

    [Test]
    public void DashIntent_RejectsBeforeCooldownBadTimingAndFarStart()
    {
        DashWindow w = Window(100.0 + Windup);
        Assert.IsTrue(MonsterDashLedger.IsAcceptable(w, 100.0, Windup, false, 0, Cooldown, 0f));
        Assert.IsTrue(MonsterDashLedger.IsAcceptable(w, 100.0, Windup, true, 95.0, Cooldown, 0f));
        Assert.IsFalse(MonsterDashLedger.IsAcceptable(w, 100.0, Windup, true, 98.0, Cooldown, 0f), "second intent inside the cooldown");
        Assert.IsFalse(MonsterDashLedger.IsAcceptable(Window(103.0), 100.0, Windup, false, 0, Cooldown, 0f), "start time far from sent + windup");
        Assert.IsFalse(MonsterDashLedger.IsAcceptable(w, 100.0, Windup, false, 0, Cooldown, 8f), "start position off the recorded path");
    }

    [Test]
    public void Ledger_ReturnsWindowOnlyAroundTheDash()
    {
        MonsterDashLedger.Clear();
        MonsterDashLedger.DebugIgnore = false;
        Assert.IsTrue(MonsterDashLedger.Record(7, Window(50.0), 50.0 - Windup, Windup, Cooldown, true, 0f));
        Assert.IsTrue(MonsterDashLedger.TryGet(7, 50.1, out _));
        Assert.IsFalse(MonsterDashLedger.TryGet(7, 52.0, out _));
        Assert.IsFalse(MonsterDashLedger.TryGet(8, 50.1, out _));
        // 쿨타임 안의 두 번째 예고는 적지 않는다
        Assert.IsFalse(MonsterDashLedger.Record(7, Window(51.0), 51.0 - Windup, Windup, Cooldown, true, 0f));
        Assert.IsTrue(MonsterDashLedger.TryGet(7, 50.1, out DashWindow kept));
        Assert.AreEqual(50.0, kept.MoveStartsAt, 1e-6);
        MonsterDashLedger.Clear();
    }

    // 방장이 받는 것: 지연 뒤 도착하는 초당 10번 위치 패킷. 술래는 준비 자세로 서 있다가 초속 68 m로 d m 돌진해 닿는 순간 주장한다.
    // 패킷 기록만으로는 짧은 돌진이 거절되고(예전 버그), 돌진 길을 함께 보면 모두 인정된다.
    [Test]
    public void DashClaim_PacketTrackAloneRejectsShortDashes_DashPathAcceptsAll()
    {
        int rejectedWithout = 0, rejectedWith = 0, cases = 0;
        foreach (float d in new[] { 3f, 6f, 10f, 15f, 20f })
        foreach (float latency in new[] { 0.04f, 0.08f, 0.15f })
        foreach (float phase in new[] { 0.01f, 0.04f, 0.08f })
        {
            cases++;
            double dashStart = 100.0, claimAt = dashStart + d / Speed, judgeAt = claimAt + latency;
            var track = new PositionTrack(30);
            for (double t = dashStart - 1.0 + phase; t + latency <= judgeAt; t += 0.1)
                track.Record(t, Vector3.forward * Mathf.Clamp((float)(t - dashStart) * Speed, 0f, d));
            Vector3 claimed = Vector3.forward * d;
            float lead = NetSecurityRules.PacketLead(claimAt, track.NewestTime);
            Assert.IsTrue(track.TryMinDistanceLeading(claimed, claimAt - NetSecurityRules.GrabRewindWindow, judgeAt, track.NewestPoint, lead, out float error));
            if (error > NetSecurityRules.GrabClaimPathTolerance) rejectedWithout++;
            float withDash = Mathf.Min(error, Window(dashStart).PathError(claimed, claimAt, MonsterDashLedger.ClaimTimeSlack));
            if (withDash > NetSecurityRules.GrabClaimPathTolerance) rejectedWith++;
        }
        Assert.Greater(rejectedWithout, 0, "without the dash path some short dash claims are still rejected (the bug)");
        Assert.AreEqual(0, rejectedWith, "with the dash path every honest dash claim is accepted");
    }

    [Test]
    public void DashClaim_TeleportClaimWithoutIntentStaysRejected()
    {
        var track = new PositionTrack(30);
        for (double t = 99.0; t <= 100.0; t += 0.1) track.Record(t, Vector3.zero); // 서 있었다
        Vector3 claimed = Vector3.forward * 20f;
        float lead = NetSecurityRules.PacketLead(100.05, track.NewestTime);
        Assert.IsTrue(track.TryMinDistanceLeading(claimed, 99.5, 100.1, track.NewestPoint, lead, out float error));
        Assert.Greater(error, NetSecurityRules.GrabClaimPathTolerance);
        // 돌진 예고가 있어도 출발 직후(0.05초)에 20 m 지점은 갈 수 없다
        Assert.Greater(Window(100.0).PathError(claimed, 100.05, MonsterDashLedger.ClaimTimeSlack), NetSecurityRules.GrabClaimPathTolerance);
    }

    [Test]
    public void PacketLead_CoversTimeSinceLastPacket()
    {
        Assert.AreEqual(0.05f, NetSecurityRules.PacketLead(10.0, 10.0), 1e-4f);
        Assert.AreEqual(0.13f, NetSecurityRules.PacketLead(10.08, 10.0), 1e-4f);
        Assert.AreEqual(NetSecurityRules.MaxRemoteLead + 0.05f, NetSecurityRules.PacketLead(20.0, 10.0), 1e-4f);
    }

    private static DecoyRoute Route(double startAt, Vector3 from, Vector3 to, float speed) =>
        new DecoyRoute { StartAt = startAt, Speed = speed, Points = new[] { from, to } };

    // 술래가 다가오자 방장이 AI를 도망 경로로 바꿨다. 술래 화면은 그 직후에도 옛 경로로 AI를 본다 — 직전 경로로도 재야 인정된다.
    [Test]
    public void RouteBook_JudgesAgainstPreviousRouteRightAfterSwitch()
    {
        var book = new DecoyRouteBook(null);
        book.SetLocal(1, Route(100.0, Vector3.zero, new Vector3(40f, 0f, 0f), 4f));          // +x로 걷는 중
        Vector3 at = DecoyRoute.Sample(Route(100.0, Vector3.zero, new Vector3(40f, 0f, 0f), 4f), 102.0).Position; // 2초 뒤 자리(8 m)
        book.SetLocal(1, Route(102.0, at, at + new Vector3(0f, 0f, 40f), 8f));              // 그 자리에서 +z로 도망(달리기)

        // 술래 화면은 102.25초에도 옛 경로(x = 9 m)를 본다. 새 경로로는 그때 z로 2 m 가 있다.
        Vector3 seenOld = new Vector3(9f, 0f, 0f);
        float withHistory = book.MinDistance(1, seenOld, 102.25 - 0.1, 102.4);
        Assert.Less(withHistory, 0.3f, "the previous route explains what the monster saw");
        Assert.IsTrue(book.TryGet(1, out DecoyRoute current));
        Assert.Greater(DecoyRoute.MinDistance(current, seenOld, 102.25 - 0.1, 102.4), 1f, "the new route alone does not");

        // 바뀐 지 오래면(유예 뒤) 직전 경로는 쓰지 않는다
        Vector3 farOld = new Vector3(20f, 0f, 0f);
        Assert.Greater(book.MinDistance(1, farOld, 104.0, 105.0), NetSecurityRules.GrabClaimPathTolerance);
    }

    [Test]
    public void RouteBook_SameRouteAgainDoesNotOverwritePrevious()
    {
        var book = new DecoyRouteBook(null);
        DecoyRoute first = Route(100.0, Vector3.zero, new Vector3(40f, 0f, 0f), 4f);
        DecoyRoute second = Route(102.0, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 40f), 8f);
        book.SetLocal(1, first);
        book.SetLocal(1, second);
        book.SetLocal(1, second); // 방장 자신의 쓰기가 방 속성으로 돌아옴
        Assert.Less(book.MinDistance(1, new Vector3(9f, 0f, 0f), 102.15, 102.3), 0.3f);
    }
}
