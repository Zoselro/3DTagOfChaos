using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// GameScene 배치 — 다중 괴물 이탈 감지 + 경고(GameRule.md §7.1). 남은 괴물이 0명일 때만 경고(GameSettings, 기본 5초) 후
// GameLobbyScene으로 복귀한다. (대기실에서의 괴물 이탈은 MonsterAssignmentAuthority가 처리한다.)
public class RoomLifecycleWatcher : MonoBehaviourPunCallbacks
{
    private double? monstersGoneAt;

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
        {
            monstersGoneAt = PhotonNetwork.Time;
            props[NetKeys.MonsterDepartedAt] = monstersGoneAt.Value; // 목록 갱신과 한 번에 보낸다
            Debug.Log($"[RoomLifecycle] All monsters left (last: actor {otherPlayer.ActorNumber}). Returning to lobby in {GameSettings.Current.MonsterDepartureReturnDelay}s.");
        }

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
