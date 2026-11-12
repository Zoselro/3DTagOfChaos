using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 방장에게 보내는 대기실 요청(시작·방 설정·강퇴·문)의 단일 송신 경로(Request1009Plan.md §72 E3).
// Photon의 신뢰 전송은 방장이 그대로일 때는 잃지 않지만, 보낸 직후 방장이 바뀌면 이전 방장에게 간 요청은 사라진다.
// 최근 KeepSeconds초 안에 보낸 요청을 기억해 두었다가 방장이 바뀌면 새 방장에게 한 번 더 보낸다.
// 다시 받아도 결과가 같은 요청만 이 경로를 쓴다(시작은 중복 방지, 설정·문은 같은 값, 강퇴는 이미 나간 사람이면 무시).
public static class MasterRequestResend
{
    public const float KeepSeconds = 3f;

    private struct Sent
    {
        public byte Code;
        public object Content;
        public float At;
    }

    private static readonly List<Sent> recent = new List<Sent>();

    public static int ResentCount { get; private set; } // 시험·로그용

    public static void Send(byte code, object content)
    {
        Raise(code, content);
        if (PhotonNetwork.OfflineMode) return;
        Trim();
        recent.Add(new Sent { Code = code, Content = content, At = Time.realtimeSinceStartup });
    }

    private static void Raise(byte code, object content) =>
        PhotonNetwork.RaiseEvent(code, content, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);

    private static void Trim()
    {
        float now = Time.realtimeSinceStartup;
        recent.RemoveAll(s => now - s.At > KeepSeconds);
    }

    private static void OnMasterSwitched()
    {
        Trim();
        if (recent.Count == 0 || !PhotonNetwork.InRoom) return;
        foreach (Sent s in recent) Raise(s.Code, s.Content);
        ResentCount += recent.Count;
        Debug.Log($"[MasterRequestResend] Master changed — resent {recent.Count} recent request(s) to the new master.");
        recent.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Watch() => PhotonNetwork.AddCallbackTarget(new Watcher());

    private sealed class Watcher : IInRoomCallbacks, IMatchmakingCallbacks
    {
        public void OnMasterClientSwitched(Player newMasterClient) => OnMasterSwitched();
        public void OnLeftRoom() => recent.Clear();
        public void OnPlayerEnteredRoom(Player newPlayer) { }
        public void OnPlayerLeftRoom(Player otherPlayer) { }
        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }
        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }
        public void OnFriendListUpdate(List<FriendInfo> friendList) { }
        public void OnCreatedRoom() { }
        public void OnCreateRoomFailed(short returnCode, string message) { }
        public void OnJoinedRoom() => recent.Clear();
        public void OnJoinRoomFailed(short returnCode, string message) { }
        public void OnJoinRandomFailed(short returnCode, string message) { }
    }
}
