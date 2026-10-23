using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// AuditFixPlan.md(research.md 6차 F-01~F-25 + 클라이언트 조작 S-01~S-15) 수정의 규칙 시험.
public class AuditFixTests
{
    // ---------------- P2 방장 되찾기·쓰기 권한 ----------------

    [TestCase(5, 1, 1, true)]   // 정책상 자격자(1)인 내가 아닌 5가 방장 → 되찾는다
    [TestCase(1, 1, 1, false)]  // 이미 내가 방장
    [TestCase(5, 1, 2, false)]  // 자격자가 내가 아니다(자격자 본인이 되찾는다)
    [TestCase(5, -1, 1, false)] // 자격자 없음(괴물만 남음)
    public void ShouldReclaimMaster(int current, int desired, int local, bool expected) =>
        Assert.AreEqual(expected, NetSecurityRules.ShouldReclaimMaster(current, desired, local));

    [Test]
    public void PlayerWriter_MasterKeysOnlyByMaster()
    {
        Assert.IsTrue(NetSecurityRules.IsAllowedPlayerWriter(true, 1, 3, 1, -1), "master writes victim's HitCount");
        Assert.IsFalse(NetSecurityRules.IsAllowedPlayerWriter(true, 3, 3, 1, -1), "victim cannot write own HitCount");
        Assert.IsFalse(NetSecurityRules.IsAllowedPlayerWriter(true, 4, 3, 1, -1), "others cannot write HitCount");
        Assert.IsTrue(NetSecurityRules.IsAllowedPlayerWriter(true, 2, 3, 1, 2), "previous master during grace");
        Assert.IsTrue(NetSecurityRules.IsAllowedPlayerWriter(true, 0, 3, 1, -1), "unknown writer is not blocked");
    }

    [Test]
    public void PlayerWriter_SelfKeysBySelfOrMaster()
    {
        Assert.IsTrue(NetSecurityRules.IsAllowedPlayerWriter(false, 3, 3, 1, -1));
        Assert.IsTrue(NetSecurityRules.IsAllowedPlayerWriter(false, 1, 3, 1, -1), "master may restore/clear");
        Assert.IsFalse(NetSecurityRules.IsAllowedPlayerWriter(false, 4, 3, 1, -1), "another player cannot change my skin/slot");
    }

    [Test]
    public void RoomWriter_OnlyMaster()
    {
        Assert.IsTrue(NetSecurityRules.IsAllowedRoomWriter(1, 1, -1));
        Assert.IsFalse(NetSecurityRules.IsAllowedRoomWriter(4, 1, -1));
        Assert.IsTrue(NetSecurityRules.IsAllowedRoomWriter(2, 1, 2));
        Assert.IsTrue(NetSecurityRules.IsAllowedRoomWriter(0, 1, -1));
    }

    [Test]
    public void ValuesEqual_ComparesArraysAndTablesByContent()
    {
        Assert.IsTrue(NetSecurityRules.ValuesEqual(new[] { 1, 2 }, new[] { 1, 2 }));
        Assert.IsFalse(NetSecurityRules.ValuesEqual(new[] { 1, 2 }, new[] { 2, 1 }));
        Assert.IsTrue(NetSecurityRules.ValuesEqual(new byte[] { 3, 4 }, new byte[] { 3, 4 }));
        Assert.IsFalse(NetSecurityRules.ValuesEqual(new[] { 1 }, new byte[] { 1 }));
        var a = new ExitGames.Client.Photon.Hashtable { { "d1", (byte)1 } };
        var b = new ExitGames.Client.Photon.Hashtable { { "d1", (byte)1 } };
        Assert.IsTrue(NetSecurityRules.ValuesEqual(a, b));
        b["d1"] = (byte)2;
        Assert.IsFalse(NetSecurityRules.ValuesEqual(a, b));
        Assert.IsTrue(NetSecurityRules.ValuesEqual(null, null));
        Assert.IsFalse(NetSecurityRules.ValuesEqual(2, null));
        Assert.IsTrue(NetSecurityRules.ValuesEqual(2.5, 2.5));
    }

    [Test]
    public void NetKeys_WritersMatchAuthority()
    {
        foreach (string k in new[] { NetKeys.HitCount, NetKeys.DeathCause, NetKeys.CatchCount })
            Assert.IsTrue(NetKeys.IsMasterKey(k, NetKeys.Target.Player), k + " is written by the master");
        foreach (string k in new[] { NetKeys.SkinIndex, NetKeys.SelectedSlot })
            Assert.IsTrue(NetKeys.IsSelfKey(k), k + " is written by the player");
        foreach (NetKeys.Scope s in NetKeys.Scopes)
            if (s.Target == NetKeys.Target.Room) Assert.AreEqual(NetKeys.Writer.Master, s.Writer, s.Key + " (room keys are master-only)");
        CollectionAssert.AreEquivalent(new[] { NetKeys.SelectedSlot }, NetKeys.RoundSelfPlayerKeys);
        CollectionAssert.AreEquivalent(new[] { NetKeys.HitCount, NetKeys.DeathCause, NetKeys.CatchCount }, NetKeys.RoundMasterPlayerKeys);
        Assert.IsFalse(NetKeys.IsMasterKey("curScn", NetKeys.Target.Room), "PUN internal keys are not in the table");
    }

    // ---------------- P3 처형·탈출을 방장이 확정 ----------------

    private static bool Grab(bool monster = true, bool hunt = true, bool alive = true, bool onMap = true, float distance = 1.5f, float rise = 0f, float since = 99f) =>
        NetSecurityRules.CanConfirmGrabKill(monster, hunt, alive, onMap, Vector3.zero, new Vector3(0f, rise, distance), 2.5f, 1.5f, since, 2f);

    [Test]
    public void GrabKill_ConfirmsOnlyValidCatches()
    {
        Assert.IsTrue(Grab());
        Assert.IsTrue(Grab(distance: 5.4f), "within reach + slack (sync delay)");
        Assert.IsFalse(Grab(distance: 9f), "far away (S-06)");
        Assert.IsFalse(Grab(rise: 6f), "too high");
        Assert.IsFalse(Grab(monster: false), "sender is not a monster (fake monster object)");
        Assert.IsFalse(Grab(hunt: false), "paint phase / result");
        Assert.IsFalse(Grab(alive: false), "already broken");
        Assert.IsFalse(Grab(onMap: false), "escaped or riding");
        Assert.IsFalse(Grab(since: 0.5f), "faster than the grab animation");
    }

    [Test]
    public void EscapeState_EscapeIsDecidedByMasterState()
    {
        var s = new EscapeState();
        s.Escaped.Add(4);
        s.Boarded.Add(5);
        Assert.IsFalse(s.HasEscaped(3));
        Assert.IsFalse(s.HasLeftMap(3));
        Assert.IsTrue(s.HasEscaped(4));
        Assert.IsTrue(s.HasEscaped(5));
        Assert.IsFalse(s.HasLeftMap(6));
    }

    // ---------------- P4 받는 쪽 검증 ----------------

    [Test]
    public void Carry_AcceptsOnlyFromNearbyCarrierOwner()
    {
        Assert.IsTrue(NetSecurityRules.CanAcceptCarry(false, false, false, true, 2f, 1.8f));
        Assert.IsFalse(NetSecurityRules.CanAcceptCarry(true, false, false, true, 1f, 1.8f), "broken cookies cannot be carried (same rule as TryGrab)");
        Assert.IsFalse(NetSecurityRules.CanAcceptCarry(false, true, false, true, 1f, 1.8f), "already carried by someone");
        Assert.IsFalse(NetSecurityRules.CanAcceptCarry(false, false, true, true, 1f, 1.8f), "I am carrying someone (chain carry)");
        Assert.IsFalse(NetSecurityRules.CanAcceptCarry(false, false, false, false, 1f, 1.8f), "sender does not own the carrier");
        Assert.IsFalse(NetSecurityRules.CanAcceptCarry(false, false, false, true, 6f, 1.8f), "too far");
    }

    [Test]
    public void ChatText_BlocksRichTextAndLength()
    {
        Assert.AreEqual("<noparse>hi</noparse>", ChatText.Sanitize("  hi \n"));
        Assert.AreEqual("<noparse><size=999>x</noparse>", ChatText.Sanitize("<size=999>x"));
        Assert.AreEqual("<noparse>a<b>b</noparse>", ChatText.Sanitize("a</NoParse><b>b"), "cannot escape the noparse wrapper");
        Assert.AreEqual("<noparse></noparse>".Length + ChatText.MaxLength, ChatText.Sanitize(new string('x', 500)).Length);
        Assert.AreEqual(string.Empty, ChatText.Sanitize("   "));
        Assert.AreEqual(string.Empty, ChatText.Sanitize(null));
    }

    [Test]
    public void ChatRateLimiter_TwoPerSecondPerSender()
    {
        var limiter = new ChatRateLimiter(2, 1f);
        Assert.IsTrue(limiter.Allow(3, 0f));
        Assert.IsTrue(limiter.Allow(3, 0.3f));
        Assert.IsFalse(limiter.Allow(3, 0.6f), "third message within a second");
        Assert.IsTrue(limiter.Allow(4, 0.6f), "other senders are independent");
        Assert.IsTrue(limiter.Allow(3, 1.1f), "next window");
    }

    // ---------------- P5 경합·정합성 ----------------

    [Test]
    public void MonsterRoster_TwoLeaveBeforeEcho()
    {
        int[] sent = { 3, 5 };
        CollectionAssert.AreEqual(new[] { 5 }, MonsterRoster.Remaining(sent, new List<int> { 1, 2, 5 }));
        CollectionAssert.IsEmpty(MonsterRoster.Remaining(sent, new List<int> { 1, 2 }), "both monsters left at once");
        CollectionAssert.IsEmpty(MonsterRoster.Remaining(null, new List<int> { 1 }));
    }

    [Test]
    public void MonsterRoster_SimultaneousClaims()
    {
        int[] first = MonsterRoster.TryAppend(new int[0], 4, 2);
        int[] second = MonsterRoster.TryAppend(first, 6, 2); // 서버 응답 전 두 번째 지원 — 이어서 계산(F-03)
        CollectionAssert.AreEqual(new[] { 4, 6 }, second);
        Assert.IsNull(MonsterRoster.TryAppend(second, 7, 2), "no seat left");
        Assert.IsNull(MonsterRoster.TryAppend(first, 4, 2), "already a monster");
        Assert.IsNull(MonsterRoster.TryAppend(first, 0, 2));
    }

    [Test]
    public void PlausibleMove_DetectsTeleport()
    {
        Assert.IsTrue(NetSecurityRules.IsPlausibleMove(Vector3.zero, new Vector3(2f, 0f, 0f), 0.2f, 15f));
        Assert.IsFalse(NetSecurityRules.IsPlausibleMove(Vector3.zero, new Vector3(40f, 0f, 0f), 0.2f, 15f), "teleport");
        Assert.IsTrue(NetSecurityRules.IsPlausibleMove(Vector3.zero, new Vector3(0f, -30f, 0f), 0.2f, 15f), "falling is fast");
        Assert.IsTrue(NetSecurityRules.IsPlausibleMove(Vector3.zero, new Vector3(40f, 0f, 0f), 0.01f, 15f), "same frame: not judged");
    }

    // ---------------- Request1009Plan.md §21 ----------------

    [TestCase(false, false, true)]  // 사냥 중(첫 탈출 전) 괴물이 모두 나감 → 5초 뒤 대기실
    [TestCase(true, false, false)]  // 첫 탈출로 마녀 타이머가 켜진 뒤 → 판 계속
    [TestCase(false, true, false)]  // 결과가 이미 났다 → 결과 화면이 처리
    [TestCase(true, true, false)]
    public void MonsterRoster_ReturnWhenMonstersGone(bool rocket, bool result, bool expected) =>
        Assert.AreEqual(expected, MonsterRoster.ReturnWhenMonstersGone(rocket, result));

    [Test]
    public void EscapeState_RevisionSurvivesEncoding()
    {
        var s = new EscapeState { Revision = 17 };
        EscapeState back = EscapeState.Decode(s.Encode());
        Assert.IsNotNull(back);
        Assert.AreEqual(17, back.Revision);
        Assert.AreEqual(17, s.Clone().Revision);
    }
}
