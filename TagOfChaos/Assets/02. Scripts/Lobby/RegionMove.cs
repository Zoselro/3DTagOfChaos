using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

// 대기실에서 호스트가 서버 지역을 바꿀 때 방 전원이 새 지역의 "같은 방"으로 옮겨 가는 절차(2026-10-03, Request1003Plan.md §1).
// Photon 방은 한 지역 서버 안에만 있어 방째로 옮길 수 없다. 그래서:
//  1) 방장이 지역과 표(token)를 Room Props(NetKeys.RegionMove)에 쓰고 방을 닫는다(RoomSettingsAuthority).
//  2) 모두가 이를 받아(GameLobbyController) 방 이름·인원·설정을 기억하고 방을 나간다 → OnLeftRoom이 LobbyScene을 연다.
//  3) LobbyScene(LobbyController)이 기억한 지역으로 다시 접속해 같은 이름·설정으로 JoinOrCreateRoom — 먼저 도착한 사람이 만든다.
//  4) 들어간 방의 표가 다르면(같은 이름의 남의 방) 나와서 실패를 알린다.
public static class RegionMove
{
    private const char Separator = '|';

    public static string Encode(string region, string token) => region + Separator + token;

    public static bool TryDecode(object value, out string region, out string token)
    {
        region = token = null;
        if (!(value is string text)) return false;
        int i = text.IndexOf(Separator);
        if (i <= 0 || i >= text.Length - 1) return false;
        region = text.Substring(0, i);
        token = text.Substring(i + 1);
        return PhotonRegions.IsValid(region);
    }

    public static string NewToken() => System.Guid.NewGuid().ToString("N").Substring(0, 8);

    // ---------------- 옮기는 중인 정보(씬이 바뀌어도 남도록 정적) ----------------

    public static bool HasPending => PendingRegion != null;
    public static string PendingRegion { get; private set; }
    public static string PendingRoomName { get; private set; }
    public static string PendingToken { get; private set; }
    public static RoomOptions PendingOptions { get; private set; }

    // 2) 방 안에서: 지금 방의 이름·인원·설정으로 새 방 옵션을 만들어 두고 나간다.
    public static void Begin(string region, string token)
    {
        if (HasPending || !PhotonNetwork.InRoom) return;
        Room room = PhotonNetwork.CurrentRoom;
        var props = new Hashtable { { NetKeys.RegionMoveToken, token } };
        if (RoomState.TryGetInt(NetKeys.RoomSpyCount, out int spies)) props[NetKeys.RoomSpyCount] = spies;
        PendingRegion = region;
        PendingRoomName = room.Name;
        PendingToken = token;
        PendingOptions = new RoomOptions { MaxPlayers = room.MaxPlayers, CustomRoomProperties = props };
        PhotonRegions.Saved = region; // 로비의 볼 지역도 새 지역으로

        foreach (string key in NetKeys.RoundPlayerKeys) PhotonNetwork.LocalPlayer.CustomProperties.Remove(key); // 나가기와 같은 정리
        if (!PhotonNetwork.IsMessageQueueRunning) PhotonNetwork.IsMessageQueueRunning = true;
        UnityEngine.Debug.Log($"[RegionMove] Moving room '{room.Name}' to {region}.");
        PhotonNetwork.LeaveRoom();
    }

    // 4) 새 방에 들어간 뒤: 표가 맞는지(같은 이름의 다른 방이 아닌지).
    public static bool IsExpectedRoom(Room room) =>
        room != null && room.CustomProperties.TryGetValue(NetKeys.RegionMoveToken, out object t) && t as string == PendingToken;

    public static void Clear()
    {
        PendingRegion = PendingRoomName = PendingToken = null;
        PendingOptions = null;
    }
}
