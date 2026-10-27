using System.Text;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

// 방 씬·방장 동기화 진단 로그(Request1009Plan.md §43 F5). 방장 교체·씬 변경·시작 요청 같은 드문 순간에만 한 줄씩 남긴다.
// 멀티에서 문제가 다시 생기면 각 클라이언트 Player.log의 "[RoomSync]" 줄만 모아 순서를 맞춰 본다.
public static class RoomSyncLog
{
    public const string Tag = "[RoomSync]";
    private static readonly StringBuilder line = new StringBuilder(256);

    public static void Note(string what)
    {
        line.Clear();
        line.Append(Tag).Append(' ').Append(what);
        if (RoomState.IsInRoom())
        {
            line.Append(" | me ").Append(PhotonNetwork.LocalPlayer.ActorNumber)
                .Append(" master ").Append(PhotonNetwork.CurrentRoom.MasterClientId)
                .Append(" host ").Append(RoomState.HostActor())
                .Append(" desired ").Append(RoomState.DesiredMasterActor())
                .Append(" monsters ").Append(Format(RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] cached) ? cached : null));
            if (AuthorityProps.TryGetExpectedRoom(NetKeys.MonsterActorNumbers, out object sent)) line.Append(" sent ").Append(Format(sent as int[]));
            line.Append(" curScn ").Append(RoomSceneFollower.RoomScene() ?? "-");
        }
        else line.Append(" | not in room");
        line.Append(" active ").Append(SceneManager.GetActiveScene().name)
            .Append(" queue ").Append(PhotonNetwork.IsMessageQueueRunning ? "on" : "OFF")
            .Append(" loading ").Append(RoomSceneFollower.IsPunLoading ? "yes" : "no")
            .Append(" switchPending ").Append(MasterClientPolicy.SwitchPending ? "yes" : "no");
        Debug.Log(line.ToString());
    }

    private static string Format(int[] actors) => actors == null ? "-" : "[" + string.Join(",", actors) + "]";
}
