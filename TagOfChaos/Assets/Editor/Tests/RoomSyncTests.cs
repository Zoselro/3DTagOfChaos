using System.Collections.Generic;
using NUnit.Framework;

// Request1009Plan.md §43 — 괴물 이탈·재입장 반복 뒤 시작하면 호스트만 대기실에 남던 문제의 규칙 시험.
public class RoomSyncTests
{
    // ---------------- F1 방 씬 따라가기 ----------------

    [TestCase("Game_CandyForest", "GameLobbyScene", false, 2.5f, true)]  // 방은 맵인데 나는 대기실(방장이어도) → 따라감
    [TestCase("Game_CandyForest", "GameLobbyScene", true, 5f, false)]    // 로드 중이면 기다린다
    [TestCase("Game_CandyForest", "GameLobbyScene", false, 1f, false)]   // 서버 응답이 따라올 시간
    [TestCase("GameLobbyScene", "GameLobbyScene", false, 9f, false)]     // 같은 씬
    [TestCase(null, "GameLobbyScene", false, 9f, false)]                 // 방 씬 정보 없음(오프라인 시험 씬 등)
    public void ShouldFollow(string room, string active, bool loading, float mismatch, bool expected) =>
        Assert.AreEqual(expected, SceneSyncRules.ShouldFollow(room, active, loading, mismatch, 2f));

    [Test]
    public void RestoreQueue_OnlyWhenPausedWithoutLoad()
    {
        Assert.IsTrue(SceneSyncRules.ShouldRestoreQueue(false, false, 3f, 3f));
        Assert.IsFalse(SceneSyncRules.ShouldRestoreQueue(false, true, 30f, 3f), "a real scene load keeps the queue paused");
        Assert.IsFalse(SceneSyncRules.ShouldRestoreQueue(true, false, 30f, 3f));
        Assert.IsFalse(SceneSyncRules.ShouldRestoreQueue(false, false, 1f, 3f));
    }

    [Test]
    public void TrustOwnLoad_WhileCurScnCacheIsStale()
    {
        Assert.IsTrue(SceneSyncRules.TrustOwnLoad("Game_CandyForest", "Game_CandyForest", 1f, 10f), "master just moved the room");
        Assert.IsFalse(SceneSyncRules.TrustOwnLoad("Game_CandyForest", "GameLobbyScene", 1f, 10f));
        Assert.IsFalse(SceneSyncRules.TrustOwnLoad("Game_CandyForest", "Game_CandyForest", 12f, 10f), "after the window the room scene wins");
        Assert.IsFalse(SceneSyncRules.TrustOwnLoad(null, "Game_CandyForest", 1f, 10f));
    }

    [Test]
    public void PresentCount_IgnoresDepartedAndDuplicates()
    {
        var present = new HashSet<int> { 1, 2, 4 };
        Assert.AreEqual(1, SceneSyncRules.PresentCount(new[] { 3, 4 }, present), "actor 3 already left");
        Assert.AreEqual(1, SceneSyncRules.PresentCount(new[] { 4, 4 }, present));
        Assert.AreEqual(0, SceneSyncRules.PresentCount(null, present));
        Assert.AreEqual(0, SceneSyncRules.PresentCount(new int[0], present));
    }

    // ---------------- F3 방장 되찾기 ----------------

    [Test]
    public void Reclaim_NotWhenOutsideTheRoomScene()
    {
        Assert.IsTrue(NetSecurityRules.ShouldReclaimMaster(5, 1, 1, true));
        Assert.IsFalse(NetSecurityRules.ShouldReclaimMaster(5, 1, 1, false), "stranded client must not grab the master");
        Assert.IsFalse(NetSecurityRules.ShouldReclaimMaster(1, 1, 1, true));
        Assert.IsFalse(NetSecurityRules.ShouldReclaimMaster(5, 1, 2, true));
    }

    // ---------------- F4 시작 조건 ----------------

    [TestCase(true, true, true, true)]
    [TestCase(true, true, false, false)]  // 재입장한 사람이 아직 대기실을 불러오는 중
    [TestCase(true, false, true, false)]  // 방에 있는 괴물이 모자람(나간 번호만 남음)
    [TestCase(false, true, true, false)]
    public void StartIsReady(bool full, bool monsters, bool inLobby, bool expected) =>
        Assert.AreEqual(expected, StartReadiness.IsReady(full, monsters, inLobby));

    [TestCase(true, 30f, true)]    // 교체 요청 진행 중
    [TestCase(false, 0.4f, true)]  // 교체 직후
    [TestCase(false, 1.5f, false)]
    public void StartWaitsForMasterSwitch(bool pending, float sinceSwitch, bool expected) =>
        Assert.AreEqual(expected, StartReadiness.IsSettling(pending, sinceSwitch, 1f));

    // ---------------- F2 맵 도착 감시 ----------------

    [Test]
    public void MonsterArrival_CountsOnlyMonstersInTheMap()
    {
        var scenes = new Dictionary<int, string>
        {
            { 1, "GameLobbyScene" }, // 대기실에 남은 괴물 호스트
            { 2, "Game_CandyForest" },
            { 3, "Game_CandyForest" },
            { 4, null },             // 아직 알리기 전
        };
        Assert.AreEqual(0, MonsterArrival.Arrived(new[] { 1 }, scenes, "Game_CandyForest"));
        Assert.AreEqual(1, MonsterArrival.Arrived(new[] { 1, 3 }, scenes, "Game_CandyForest"));
        Assert.AreEqual(0, MonsterArrival.Arrived(new[] { 4, 9 }, scenes, "Game_CandyForest"));

        Assert.IsTrue(MonsterArrival.ShouldReturn(1, 0), "no monster arrived → return to lobby");
        Assert.IsFalse(MonsterArrival.ShouldReturn(2, 1), "some arrived → the round continues");
        Assert.IsFalse(MonsterArrival.ShouldReturn(0, 0), "no monsters in this room (test map)");
    }

    // ---------------- 키 표 ----------------

    [Test]
    public void LoadedScene_IsASessionSelfKey()
    {
        Assert.IsTrue(NetKeys.IsSelfKey(NetKeys.LoadedScene));
        CollectionAssert.DoesNotContain(NetKeys.RoundPlayerKeys, NetKeys.LoadedScene, "must survive the round reset");
    }
}
