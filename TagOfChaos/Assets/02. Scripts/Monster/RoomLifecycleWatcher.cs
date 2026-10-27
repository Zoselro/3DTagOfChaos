using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// GameScene 배치 — 다중 괴물 이탈 감지 + 경고(GameRule.md §7.1). 남은 괴물이 0명일 때만 경고(GameSettings, 기본 5초) 후
// GameLobbyScene으로 복귀한다. (대기실에서의 괴물 이탈은 MonsterAssignmentAuthority가 처리한다.)
// 맵 도착 감시(Request1009Plan.md §43 F2): 괴물이 방에는 남아 있는데 맵에 들어오지 않으면(시작 때 대기실에 남음) 나간 것과
// 같게 본다 — 준비 유예 + MonsterArrivalTimeout 뒤에도 괴물이 한 명도 도착하지 않았으면 같은 경고 후 대기실로 돌아간다.
public class RoomLifecycleWatcher : MonoBehaviourPunCallbacks
{
    private double? monstersGoneAt;
    private bool arrivalChecked;
    private readonly Dictionary<int, string> loadedScenes = new Dictionary<int, string>();

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (monstersGoneAt.HasValue) return;
        if (!PhotonNetwork.IsMasterClient) return; // 떠난 사람이 방장이었어도 PUN은 방장 교체 후 이 콜백을 부른다
        int[] monsters = MonsterRoster.Current(); // 방장이 마지막으로 보낸 목록 — 응답 전에 두 번째 괴물이 나가도 이어서 계산(F-04)
        if (!monsters.Contains(otherPlayer.ActorNumber)) return; // 쿠키가 나간 건 이 경로와 무관(쿠키는 파괴되면 관전만 함)

        // 목록 갱신과 남은 수 판단을 방금 계산한 배열로 한다. 예전에는 SetCustomProperties 직후 캐시를 다시
        // 읽었는데, 온라인(PUN 기본 BroadcastPropsChangeToAll=true)에서는 서버 응답 전까지 캐시가 옛 값이라
        // 항상 "괴물 1명 남음"으로 보여 경고·복귀가 일어나지 않았다(Bug-fix-plan.md §24.4 ⑱).
        int[] remaining = MonsterRoster.Remaining(monsters, PhotonNetwork.CurrentRoom.Players.Keys);
        var props = new Hashtable { { NetKeys.MonsterActorNumbers, remaining } };

        // 괴물 없이도 판을 이어 가는 경우(Request1009Plan.md §21): 첫 탈출로 마녀 타이머가 켜진 뒤.
        // 이때도 목록은 갱신한다(빈 배열) — 마녀 사망·결과 화면·추격 음악이 "괴물 없음"을 본다. 결과가 이미 났으면 결과 화면이 처리한다.
        bool spyRocketLaunched = RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out _); // 첫 탈출로 마녀 타이머가 켜진 뒤(§25.5 G)
        bool resultDecided = RoomState.TryGetInt(NetKeys.GameResult, out _);
        if (remaining.Length == 0 && !MonsterRoster.ReturnWhenMonstersGone(spyRocketLaunched, resultDecided))
            Debug.Log($"[RoomLifecycle] All monsters left (last: actor {otherPlayer.ActorNumber}) — the round continues (rocket launched: {spyRocketLaunched}, result: {resultDecided}).");
        else if (remaining.Length == 0)
            BeginMonstersGoneReturn(props, $"All monsters left (last: actor {otherPlayer.ActorNumber})"); // 목록 갱신과 한 번에 보낸다

        AuthorityProps.SetRoom(props);
    }

    // 괴물이 모두 떠났다: 떠난 시각을 기록해 경고 배너(MonsterDepartureBanner)를 띄우고 정해진 시간 뒤 대기실로 돌아간다.
    // props는 호출한 쪽이 보낸다(괴물 목록 갱신과 한 번에 보내기 위해).
    private void BeginMonstersGoneReturn(Hashtable props, string reason)
    {
        monstersGoneAt = PhotonNetwork.Time;
        props[NetKeys.MonsterDepartedAt] = monstersGoneAt.Value;
        Debug.Log($"[RoomLifecycle] {reason}. Returning to lobby in {GameSettings.Current.MonsterDepartureReturnDelay}s.");
    }

    // 준비 유예 + 도착 대기 시간이 지나면 한 번 확인한다. 그 사이 늦은 괴물은 RoomSceneFollower가 맵으로 끌어온다.
    private void CheckMonsterArrival()
    {
        arrivalChecked = true;
        int[] monsters = MonsterRoster.Remaining(MonsterRoster.Current(), PhotonNetwork.CurrentRoom.Players.Keys);
        if (monsters.Length == 0) return; // 괴물 없는 시험 방·이미 모두 나감(OnPlayerLeftRoom 경로)

        loadedScenes.Clear();
        foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values) loadedScenes[p.ActorNumber] = RoomState.LoadedScene(p);
        string map = SceneManager.GetActiveScene().name;
        int arrived = MonsterArrival.Arrived(monsters, loadedScenes, map);
        RoomSyncLog.Note($"Monster arrival check: {arrived}/{monsters.Length} in {map}");
        if (!MonsterArrival.ShouldReturn(monsters.Length, arrived)) return;

        bool spyRocketLaunched = RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out _);
        bool resultDecided = RoomState.TryGetInt(NetKeys.GameResult, out _);
        if (!MonsterRoster.ReturnWhenMonstersGone(spyRocketLaunched, resultDecided)) return;

        var props = new Hashtable();
        BeginMonstersGoneReturn(props, $"No monster arrived in {map} (monsters: {string.Join(",", monsters)})");
        AuthorityProps.SetRoom(props);
    }

    // 경고 도중 방장이 바뀌면 새 방장이 복귀를 이어받는다 — 시각은 Room Prop에 남아 있다.
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient || monstersGoneAt.HasValue) return;
        if (RoomState.TryGetDouble(NetKeys.MonsterDepartedAt, out double departedAt)) monstersGoneAt = departedAt;
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!arrivalChecked && !monstersGoneAt.HasValue && RoomState.IsInRoom()
            && Time.timeSinceLevelLoad >= GameSettings.Current.SceneLoadGrace + GameSettings.Current.MonsterArrivalTimeout)
            CheckMonsterArrival();
        if (!monstersGoneAt.HasValue) return;
        if (PhotonNetwork.Time < monstersGoneAt.Value + GameSettings.Current.MonsterDepartureReturnDelay) return;

        monstersGoneAt = null;
        ReturnToGameLobby();
    }

    // 방은 나가지 않고, 같은 방을 그대로 유지한 채 대기실로 되돌아간다. 판 상태 초기화와 방 재개방은 대기실의
    // RoundStateResetter가 두 복귀 경로(결과 화면·괴물 이탈) 공통으로 한다(Bug-fix-plan.md §24.5, §26.3).
    private void ReturnToGameLobby()
    {
        RoomSceneTransition.LoadLevelForRoom(SceneNames.GameLobby); // AutomaticallySyncScene으로 전원 함께 이동
    }

    // OnLeftRoom → LobbyScene 로드는 같은 씬의 RoomExitController가 담당한다. 여기서도 로드하면 씬을 두 번
    // 불러왔다(research.md §8.20).
}
