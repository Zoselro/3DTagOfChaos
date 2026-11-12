using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

// 대기실 강퇴(Request1009Plan.md §19.6). 호스트(시작 버튼 주인)만, 자기 자신 제외, 대기실 단계에서만.
// 시작·방 설정과 같은 "호스트 요청 → 방장 실행" 구조: 방장이 검증해 대상에게만 Kicked를 보내면 대상이 스스로 방을 나간다.
// 다시 들어오는 것은 막지 않는다(§19.7 Q6). 네트워크·화면은 GameLobbyController가 맡고, 여기는 규칙과 메시지만 둔다.
public static class LobbyKick
{
    public const float CloseConnectionDelay = 3f; // 대상이 이만큼 지나도 방에 남아 있으면(응답 없음) 방장이 연결을 끊는다

    private static bool kickedNotice;

    // 강퇴할 수 있는지(순수 계산, 시험용).
    public static bool CanKick(int requester, int host, int target, bool targetInRoom, GamePhase phase) =>
        requester == host && target != host && targetInRoom && phase == GamePhase.Lobby;

    // 이 화면에서 대상 줄에 강퇴 버튼을 보여 줄지.
    public static bool LocalCanKick(Player target)
    {
        Player me = PhotonNetwork.LocalPlayer;
        return target != null && me != null && RoomState.IsInRoom()
            && CanKick(me.ActorNumber, RoomState.HostActor(), target.ActorNumber, PhotonNetwork.CurrentRoom.GetPlayer(target.ActorNumber) != null, GamePhaseState.Current);
    }

    // 호스트 → 방장 요청(호스트가 방장이 아닐 때).
    public static void SendRequest(int target) =>
        MasterRequestResend.Send(NetEventCodes.KickRequest, target); // §72 E3

    // 방장: 검증한 뒤 대상에게만 Kicked를 보낸다. 받아들였으면 true.
    public static bool Handle(int sender, int target)
    {
        if (!PhotonNetwork.IsMasterClient || !RoomState.IsInRoom()) return false;
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(target);
        if (!CanKick(sender, RoomState.HostActor(), target, player != null, GamePhaseState.Current)) return false;
        PhotonNetwork.RaiseEvent(NetEventCodes.Kicked, target, new RaiseEventOptions { TargetActors = new[] { target } }, SendOptions.SendReliable);
        UnityEngine.Debug.Log($"[LobbyKick] Host (actor {sender}) kicked actor {target}.");
        return true;
    }

    // 받은 Kicked가 나에게 온 진짜 강퇴인지: 방장이 보냈고 대상이 나.
    public static bool IsForMe(int sender, object data) =>
        data is int target && PhotonNetwork.LocalPlayer != null && target == PhotonNetwork.LocalPlayer.ActorNumber
        && RoomState.IsInRoom() && (PhotonNetwork.OfflineMode || sender == PhotonNetwork.CurrentRoom.MasterClientId);

    // 강퇴되어 로비로 돌아갈 때 로비 화면이 한 번 알려 주도록 남겨 둔다.
    public static void MarkKicked() => kickedNotice = true;

    public static bool ConsumeKickedNotice()
    {
        bool value = kickedNotice;
        kickedNotice = false;
        return value;
    }
}
