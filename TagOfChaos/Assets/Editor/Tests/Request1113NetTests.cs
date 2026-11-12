using NUnit.Framework;
using UnityEngine;

// Request1009Plan.md §71·§72 — 네트워크 보강(AI 미래 시작 경로 등).
public class Request1113NetTests
{
    private static DecoyRoute Route(double startAt, Vector3 from, Vector3 to, float speed) =>
        new DecoyRoute { StartAt = startAt, Speed = speed, Points = new[] { from, to } };

    // §71 A1: 새 경로가 시작되기 전까지는 직전 경로를 따른다 — 모든 화면이 시작 전에 받으므로 바뀌는 순간 튀지 않는다.
    [Test]
    public void RouteBook_FollowsPreviousRouteUntilTheNewOneStarts()
    {
        var book = new DecoyRouteBook(null);
        DecoyRoute walk = Route(100.0, Vector3.zero, new Vector3(40f, 0f, 0f), 4f);
        book.SetLocal(1, walk);
        double switchAt = 102.0 + DecoyBrain.RouteLead;
        Vector3 lead = DecoyRoute.Sample(walk, switchAt).Position;               // 옛 경로가 시작 시각에 닿는 자리
        book.SetLocal(1, Route(switchAt, lead, lead + new Vector3(0f, 0f, 40f), 8f));

        var d = new EscapeState.Decoy { Id = 1 };
        Assert.AreEqual(DecoyRoute.Sample(walk, 102.1).Position.x, book.PositionOf(d, 102.1).x, 1e-3f, "before the start: still the old route");
        Vector3 atStart = book.PositionOf(d, switchAt);
        Assert.Less(Vector3.Distance(atStart, lead), 0.01f, "the new route starts exactly where the old one was");
        Assert.Greater(book.PositionOf(d, switchAt + 0.5).z, 3.5f, "after the start: the new route");
        // 시작 직전과 직후가 이어진다(한 프레임 0.02초 사이 이동이 속도 × 시간 정도)
        float step = Vector3.Distance(book.PositionOf(d, switchAt - 0.01), book.PositionOf(d, switchAt + 0.01));
        Assert.Less(step, 0.2f);
    }

    // §72 E3: 같은 요청 번호가 다시 오면(재전송) 처리하지 않는다. 오래된 번호는 Capacity를 넘으면 잊는다.
    [Test]
    public void RequestSeqWindow_IgnoresResentNumbers()
    {
        var w = new RequestSeqWindow();
        Assert.IsTrue(w.Add(1));
        Assert.IsTrue(w.Add(2));
        Assert.IsFalse(w.Add(1), "resent request");
        for (int i = 3; i < 3 + RequestSeqWindow.Capacity; i++) w.Add(i);
        Assert.IsTrue(w.Add(1), "a very old number is forgotten");
    }

    // §72 E3: 요청 번호가 실려 가고, 옛 형식(번호 없음)도 그대로 읽힌다.
    [Test]
    public void EscapeRequest_PayloadCarriesSequence()
    {
        var r = new EscapeRequest { Op = EscapeOp.OpenChest, A = 3, Pos = Vector3.one, Seq = 42 };
        Assert.IsTrue(EscapeRequest.TryParse(r.ToPayload(), 7, out EscapeRequest back));
        Assert.AreEqual(42, back.Seq);
        Assert.AreEqual(7, back.Sender);
        Assert.IsTrue(EscapeRequest.TryParse(new object[] { (byte)EscapeOp.PickUp, 1, 0, Vector3.zero, new int[0] }, 2, out EscapeRequest old));
        Assert.AreEqual(0, old.Seq);
    }

    // §72 E3: 방장이 바뀐 뒤에는 두 번 처리돼도 같은 요청만 새 방장에게 다시 보낸다.
    [Test]
    public void EscapeRequest_OnlySafeOpsAreResentToANewMaster()
    {
        Assert.IsTrue(EscapeRequest.IsResendSafe(EscapeOp.OpenChest));
        Assert.IsTrue(EscapeRequest.IsResendSafe(EscapeOp.PickUp));
        Assert.IsTrue(EscapeRequest.IsResendSafe(EscapeOp.RepairBegin));
        Assert.IsFalse(EscapeRequest.IsResendSafe(EscapeOp.Throw));
        Assert.IsFalse(EscapeRequest.IsResendSafe(EscapeOp.ToolUse));
        Assert.IsFalse(EscapeRequest.IsResendSafe(EscapeOp.Drop));
        Assert.IsFalse(EscapeRequest.IsResendSafe(EscapeOp.DropAll));
        Assert.IsFalse(EscapeRequest.IsResendSafe(EscapeOp.CageMash));
    }

    // §71 L1: 결과 번호는 이벤트에 실려 가므로 고정한다. 이벤트 코드도 겹치지 않는다.
    [Test]
    public void ClaimResult_CodesAreStable()
    {
        Assert.AreEqual(0, (byte)ClaimOutcome.Accepted);
        Assert.AreEqual(3, (byte)ClaimOutcome.NoSeat);
        Assert.AreEqual(16, NetEventCodes.ClaimResult);
        Assert.AreNotEqual(NetEventCodes.ClaimResult, NetEventCodes.VoiceClip);
        Assert.AreNotEqual(NetEventCodes.ClaimResult, NetEventCodes.ProjectileSpawn);
        Assert.AreEqual(16, (byte)EscapeNoticeKind.RequestAck);
    }

    // §72 E2: 목소리 녹음은 조각 간격을 두고 보낸다(40 KB를 한 프레임에 보내지 않게).
    [Test]
    public void Voice_IsSentInSpacedChunks()
    {
        Assert.Greater(VoiceNet.ChunkGap, 0.05f);
        Assert.GreaterOrEqual(Cauldron.RetrySeconds, 0.5f);
        Assert.Greater(Cauldron.MaxTries, 1);
    }

    // §72 E1: 방장 프레임이 평균 15 fps 아래면 느리다고 본다.
    [Test]
    public void MasterFrameWatch_FlagsSlowFrames()
    {
        Assert.IsFalse(MasterFrameWatch.IsSlow(2f, 120), "60 fps");
        Assert.IsTrue(MasterFrameWatch.IsSlow(2f, 10), "5 fps");
        Assert.IsFalse(MasterFrameWatch.IsSlow(2f, 0));
    }

    // §73 L4: 모두 맵에 들어온 사람 수, 준비 시간 당기기(늘리지는 않는다).
    [Test]
    public void MapArrival_CountsPlayersAndOnlyShortensPrepare()
    {
        var scenes = new System.Collections.Generic.Dictionary<int, string> { { 1, "Map" }, { 2, "GameLobbyScene" }, { 3, "Map" } };
        Assert.AreEqual(2, MonsterArrival.ArrivedPlayers(new[] { 1, 2, 3, 4 }, scenes, "Map", out int total));
        Assert.AreEqual(4, total);
        Assert.IsTrue(MonsterArrival.ShouldShortenPrepare(160.0, 100.0, 15f), "provisional end far ahead");
        Assert.IsFalse(MonsterArrival.ShouldShortenPrepare(112.0, 100.0, 15f), "never lengthens");
        Assert.Greater(RoomLifecycleWatcher.MaxArrivalWait, GameSettings.Current.SceneLoadGrace + GameSettings.Current.MonsterArrivalTimeout);
    }
}
