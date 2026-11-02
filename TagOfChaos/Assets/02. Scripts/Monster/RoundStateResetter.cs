using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// GameLobbyScene 배치(MonsterManagers). 한 판이 끝나 대기실로 돌아왔을 때 이전 판의 Room/Player
// 상태를 지워, 다음 판에서 괴물을 새로 뽑고 색칠 페이즈·승패 판정이 다시 동작하게 한다(research.md §8.7,
// Bug-fix-plan.md §24.5). 방장은 게임 시작 때 닫았던 방도 다시 연다(§26.3 ㉑-1 — 결과 화면 경로로 돌아오면
// 방이 닫힌 채 남아 LobbyScene에서 입장할 수 없었다).
public class RoundStateResetter : MonoBehaviourPunCallbacks
{
    private bool localResetDone;
    private bool roomResetPending; // 방 초기화·재개방을 아직 못 했다(씬 로드가 끝나기 전 등) — 될 때까지 매 프레임 다시 시도한다

    private void Update()
    {
        // 씬 로드 직후 InRoom이 아직 false일 수 있으므로(Bug-fix-plan.md §12) 방에 들어온 첫 프레임에 수행한다.
        if (!RoomState.IsInRoom()) return;
        if (!localResetDone)
        {
            localResetDone = true;
            ResetLocalPlayerRoundState();
            roomResetPending = true;
        }
        // 예전에는 첫 프레임에 한 번만 시도했다 — 그 순간 대기실 씬 로드가 아직 "끝남"이 아니면 건너뛰고 다시 시도하지 않아, 방이
        // 닫힌 채 남아 로비에서 3/4인데도 들어올 수 없었다(2026-11-02 멀티).
        if (roomResetPending) roomResetPending = !TryResetRoom();
    }

    // 대기실 입장 직후 방장이 바뀌어도(방장 정책으로 방 생성자에게 되돌림, 방장 퇴장) 새 방장이 이전 판 흔적을
    // 이어서 지운다 — 누가 방장이든 한 번은 초기화된다(§26.8.3 ③).
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (localResetDone) roomResetPending = !TryResetRoom();
    }

    // 본인이 쓰는 Player Props(NetKeys.Writer.Self)는 각자 지운다. SkinIndex는 판이 바뀌어도 유지한다.
    // 방장만 쓰는 판 키(HitCount·DeathCause·CatchCount·Escaped)는 방장이 모두의 것을 지운다(TryResetRoom) — 본인이 지우면
    // 권위 속성 감시(RoomPropertyGuard)가 조작으로 보고 되돌린다(AuditFixPlan.md §7.2).
    private static void ResetLocalPlayerRoundState()
    {
        var props = BuildNullProps(PhotonNetwork.LocalPlayer.CustomProperties, NetKeys.RoundSelfPlayerKeys);
        if (props.Count > 0) PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    // 끝났으면 true(방장이 아님·지울 흔적 없음 포함), 지금은 할 수 없어 다시 시도해야 하면 false.
    private static bool TryResetRoom()
    {
        if (!PhotonNetwork.IsMasterClient || !HasFinishedRoundTrace()) return true;
        // 방 씬(curScn)이 대기실이 아니거나 씬 로드 중이면 지우지 않고 다음 프레임에 다시 본다(§46 후속).
        //  - 대기실에 막 들어온 방장: 씬을 불러오는 동안 메시지 큐가 멈춰 있어, 자기가 보낸 curScn = 대기실의 서버 응답이 첫 프레임에는
        //    아직 처리되지 않아 캐시가 옛 맵을 가리킨다(온라인만 — 오프라인은 캐시가 바로 바뀐다). 예전에는 여기서 그냥 끝내 방이 닫힌 채
        //    남았다(2026-11-02 멀티: 3/4인데 로비에서 입장 불가). 응답이 오면 대기실이 되므로 그때 지운다.
        //  - 새 판이 이미 시작돼 맵으로 옮겨 가는 중: 지우면 새 판의 맵·준비 시각이 사라진다. 씬이 바뀌면 이 컴포넌트도 사라진다.
        string roomScene = RoomSceneFollower.RoomScene();
        if ((!string.IsNullOrEmpty(roomScene) && roomScene != SceneNames.GameLobby) || PunSceneLoad.IsBusy) return false;

        var props = BuildNullProps(PhotonNetwork.CurrentRoom.CustomProperties, NetKeys.RoundRoomKeys);
        foreach (System.Collections.DictionaryEntry e in PhotonNetwork.CurrentRoom.CustomProperties)
            if (e.Key is string key && NetKeys.IsRoundPrefixedKey(key, NetKeys.Target.Room)) props[key] = null; // AI 경로 "DR_<id>"(§44.6)
        if (props.Count > 0) AuthorityProps.SetRoom(props);
        foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
        {
            var playerProps = BuildNullProps(p.CustomProperties, NetKeys.RoundMasterPlayerKeys);
            if (playerProps.Count > 0) AuthorityProps.SetPlayer(p, playerProps);
        }

        PhotonNetwork.CurrentRoom.IsOpen = true;    // 게임 시작 때 닫은 방을 다시 연다(두 복귀 경로 공통)
        PhotonNetwork.CurrentRoom.IsVisible = true;
        Debug.Log($"[RoundStateResetter] Cleared {props.Count} round room keys and reopened the room.");
        return true;
    }

    // 이전 판이 진행된 흔적이 남아 있는지. 대기실에서 새로 정해진 괴물·선정 기준 시각은 판이 시작되기 전의
    // 정상 상태이므로 흔적으로 보지 않는다. 시작 버튼 직후의 PrepareEndTime은 미래 시각이라 제외된다.
    private static bool HasFinishedRoundTrace()
    {
        if (RoomState.TryGetInt(NetKeys.GameResult, out _)) return true;
        if (RoomState.TryGetDouble(NetKeys.MonsterDepartedAt, out _)) return true;
        return RoomState.TryGetDouble(NetKeys.PrepareEndTime, out double prepareEnd) && PhotonNetwork.Time >= prepareEnd;
    }

    // 실제로 존재하는 키만 null(=삭제)로 보낸다 — 없는 키까지 보내 불필요한 Props 이벤트를 만들지 않는다.
    private static Hashtable BuildNullProps(Hashtable current, string[] keys)
    {
        var props = new Hashtable();
        foreach (string key in keys)
        {
            if (current.ContainsKey(key)) props[key] = null;
        }
        return props;
    }
}
