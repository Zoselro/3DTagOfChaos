using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

// 방 나가기(확인창 → 방 나가기 → 로비 씬 전환) 전담. ESC 메뉴의 "나가기"가 부른다 — 화면 왼쪽 위 뒤로가기 버튼은
// 2026-10-03 사용자 결정으로 대기실·게임 화면 모두에서 없앴다(ESC로 나간다).
// GameManager.cs에서 분리됨(architecture-review.md §1.1 — GameManager는 채팅 전용으로 축소).
public class RoomExitController : MonoBehaviourPunCallbacks
{

    [SerializeField] private PhotonView pv; // "LogMsg" RPC 브로드캐스트용 — GameManager와 같은 오브젝트의 PhotonView를 연결
    [SerializeField] private ConfirmDialog confirmDialog;
    // 표시 문구는 씬별로 인스펙터에서 설정한다(코드에 한글을 넣지 않는 프로젝트 규칙). {0} = 닉네임.
    [SerializeField] private string leaveConfirmMessage = "Leave to the lobby?";
    [SerializeField] private string leaveLogFormat = "\n<color=#ff0000>[{0}] left the room</color>";

    public string LeaveLogFormat => leaveLogFormat; // GameManager가 받는 쪽에서 보낸 사람 닉네임으로 꾸민다(S-11)
    public ConfirmDialog Dialog => confirmDialog; // 강퇴 확인(GameLobbyController)도 같은 확인창을 쓴다

    // 나가기: 곧바로 나가지 않고 확인창부터 띄운다
    public void OnClickBackButtonPressed()
    {
        if (confirmDialog != null)
            confirmDialog.Show(leaveConfirmMessage, OnClickBackBtn);
        else
            OnClickBackBtn(); // 확인창이 연결 안 돼 있으면 안전하게 기존 동작으로 폴백
    }

    public void OnClickBackBtn()
    {
        // 씬을 불러오는 중(PUN이 큐를 멈춤)에 나가기를 눌러도 퇴장 완료(OnLeftRoom)를 받도록 큐를 다시 돌린다.
        if (!PhotonNetwork.IsMessageQueueRunning) PhotonNetwork.IsMessageQueueRunning = true;

        // 방 CustomProperties는 마지막 사람이 나가면 방과 함께 사라지므로 따로 지우지 않는다(예전의
        // CurrentRoom.CustomProperties.Clear()는 로컬 사본만 지워 아무 효과가 없었다, research.md §8.12).
        pv.RPC(GameManager.RpcLogMsg, RpcTarget.All, GameManager.KindLeft, string.Empty);

        // LocalPlayer의 CustomProperties는 다음에 들어가는 방으로 그대로 전송되므로, 판 단위 키(파괴 여부·
        // 슬롯 수)만 로컬에서 지운다. SkinIndex는 다음 방에서도 유지한다.
        foreach (string key in NetKeys.RoundPlayerKeys)
            PhotonNetwork.LocalPlayer.CustomProperties.Remove(key);

        Debug.Log("[RoomExit] Leaving room.");
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        // 씬 불러오기 도중 이탈 등으로 바뀐 전역 네트워크 설정을 되돌린다.
        PhotonNetwork.IsMessageQueueRunning = true;
        PhotonNetwork.KeepAliveInBackground = NetworkDefaults.KeepAliveInBackgroundSeconds;

        Debug.Log("[RoomExit] Left room. Loading lobby scene.");
        SceneManager.LoadScene(SceneNames.Lobby);
    }
}
