using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// 채팅 전담(스폰은 PlayerSpawner, 퇴장은 RoomExitController로 분리됨). 외부에서 참조하는 곳이 없던 싱글톤
// Inst와 Is_Conversating은 제거했다(research.md §8.21).
public class GameManager : MonoBehaviour
{
    const int MAX_CHAT = 50; // 채팅 최대 갯수

    [SerializeField] private PhotonView pv;
    [SerializeField] private TMP_InputField InputFdChat; // 채팅 입력 필드
    [SerializeField] private TextMeshProUGUI txtLogMsg;

    private List<string> m_MsgList = new List<string>();
    private bool bEnter = false;

    private readonly StringBuilder logBuilder = new StringBuilder();

    // 받는 쪽이 꾸미는 문구(AuditFixPlan.md S-11). {0} = 보낸 사람 닉네임, {1} = 본문. 한글 문구는 씬·프리팹에서 설정한다.
    [SerializeField] private string chatFormat = "\n<color=#ffffff>[{0}] {1}</color>";
    [SerializeField] private string ownChatFormat = "\n<color=#ffff00>[{0}] {1}</color>";
    [SerializeField] private string connectedFormat = "\n<color=#33ff33>[{0}] Connected</color>";
    private const string DefaultLeaveFormat = "\n<color=#ff0000>[{0}] left the room</color>";
    private RoomExitController roomExit; // 퇴장 문구(leaveLogFormat)는 기존처럼 RoomExitController 인스펙터 값을 쓴다

    // LogMsg 종류: 본문은 채팅에만 쓰고, 접속·퇴장은 받는 쪽 문구로 만든다.
    public const byte KindChat = 0;
    public const byte KindConnected = 1;
    public const byte KindLeft = 2;

    private readonly ChatRateLimiter rateLimiter = new ChatRateLimiter(2, 1f);

    private void Awake()
    {
        roomExit = GetComponent<RoomExitController>();
        if (roomExit == null) roomExit = FindFirstObjectByType<RoomExitController>();
        // 입력창 바깥을 클릭해 선택이 풀리면 채팅을 닫은 것으로 본다 — 예전에는 bEnter가 true로 남아 다음 Enter가 "닫기"로
        // 처리되고, 그동안 이동 잠금이 풀리지 않았다(research.md R5-1, Bug-fix-plan.md §41 ㊵). 쓰던 글은 지우지 않는다.
        if (InputFdChat != null) InputFdChat.onDeselect.AddListener(_ => { if (bEnter) CloseChat(send: false); });
    }

    private void Start()
    {
        Time.timeScale = 1.0f; // 일시정지 풀어주기
        PhotonNetwork.IsMessageQueueRunning = true; // 포톤 메시지 큐를 활성화하여 RPC 호출을 받을 수 있도록 설정

        // Start()가 실행됐다고 해서 PhotonNetwork.InRoom이 true라는 보장은 없다(Bug-fix-plan.md §12).
        // 그 상태에서 RPC를 보내면 로컬에서는 아무 에러 없이 지나가지만 실제 전송(RaiseEvent)은
        // 조용히 실패해 다른 클라이언트에게 전달되지 않는다 — InRoom이 true가 될 때까지 기다린 뒤 보낸다.
        StartCoroutine(SendConnectedMessageWhenInRoom());
    }

    private IEnumerator SendConnectedMessageWhenInRoom()
    {
        while (!PhotonNetwork.InRoom)
            yield return null;

        // AllBuffered는 방 이벤트 캐시에 무한히 쌓이고, 씬마다 PhotonView ID가 달라 다른 씬의 오브젝트로
        // 재생되기도 했다(research.md §8.19, Bug-fix-plan.md §24.6 ⑰-D). 현재 방에 있는 사람에게만 보낸다.
        pv.RPC(RpcLogMsg, RpcTarget.All, KindConnected, string.Empty);
    }

    private void Update()
    {
        //--- 채팅 구현 텍스트
        if (PlayerInput.ChatSubmitReleased)
        {// 엔터키를 누르면 인풋 필드 활성화, 다시 누르면 전송하고 닫기
            if (!bEnter) OpenChat();
            else CloseChat(send: true);
        }
    }

    // 채팅 중에는 게임플레이 키 입력 전체를 억제한다 — 예전에는 쿠키(HideOrSeekPlayer)만 찾아 잠가 괴물은 채팅을 치며 움직이고
    // 돌진했고, 쿠키도 Update가 일찍 끝나 걷기 애니메이션이 그대로 남았다(Bug-fix-plan.md §41 ㊵·U2). 입력이 0이 되므로
    // 캐릭터 종류와 무관하게 멈추고 Idle로 돌아간다.
    private void OpenChat()
    {
        bEnter = true;
        InputFdChat.gameObject.SetActive(true);
        InputFdChat.ActivateInputField(); // <--- 키보드 커서 입력 상자 쪽으로 가게 만들어 줌
        PlayerInput.IsGameplaySuppressed = true;
    }

    private void CloseChat(bool send)
    {
        bEnter = false; // 입력창을 끌 때 오는 선택 해제 통지가 다시 닫기를 부르지 않도록 먼저 내린다
        PlayerInput.IsGameplaySuppressed = false;
        if (InputFdChat == null) return;
        InputFdChat.gameObject.SetActive(false);
        if (send && !string.IsNullOrEmpty(InputFdChat.text.Trim()))
            BroadcastingChat();
    }

    // 억제는 정적 값이라 씬이 바뀌어도 남는다. 채팅을 연 채 비활성화되거나
    // 씬 전환으로 파괴되면 반드시 되돌린다 — 그러지 않으면 다음 씬에서 캐릭터가 움직이지 않는다.
    private void OnDisable()
    {
        if (bEnter) CloseChat(send: false);
    }

    private void OnDestroy()
    {
        if (bEnter) PlayerInput.IsGameplaySuppressed = false;
    }

    // 중계 하기 위함
    // 다른 클래스(RoomExitController)도 보내는 RPC 이름 — 메서드 이름 변경 시 컴파일 단계에서 함께 바뀐다(research.md §12 E3).
    public const string RpcLogMsg = nameof(LogMsg);

    [PunRPC]
    private void LogMsg(byte kind, string text, PhotonMessageInfo info)
    {
        Photon.Realtime.Player sender = info.Sender;
        int actor = sender != null ? sender.ActorNumber : 0;
        if (!rateLimiter.Allow(actor, Time.unscaledTime)) return; // 도배(초당 2개 초과)는 버린다

        string name = sender != null && !string.IsNullOrEmpty(sender.NickName) ? sender.NickName : "#" + actor;
        name = ChatText.Sanitize(name, 24);
        string msg;
        switch (kind)
        {
            case KindChat:
                string body = ChatText.Sanitize(text);
                if (body.Length == 0) return;
                if (!ChatRules.IsVisible(ChatRules.RoundActive, ChatRules.IsOut(sender), ChatRules.IsOut(PhotonNetwork.LocalPlayer))) return;
                msg = string.Format(sender != null && sender.IsLocal ? ownChatFormat : chatFormat, name, body);
                break;
            case KindConnected:
                msg = string.Format(connectedFormat, name);
                break;
            case KindLeft:
                msg = string.Format(roomExit != null ? roomExit.LeaveLogFormat : DefaultLeaveFormat, name);
                break;
            default:
                return;
        }

        m_MsgList.Add(msg);

        if(m_MsgList.Count > MAX_CHAT)
        {
            m_MsgList.RemoveAt(0);
        }

        // 로그 메시지 Text UI에 텍스트를 누적시켜 표시
        logBuilder.Clear();
        for (int i = 0; i < m_MsgList.Count; i++)
            logBuilder.Append(m_MsgList[i]);
        txtLogMsg.text = logBuilder.ToString();
    }

    //채팅 내용을 중계하는 함수
    private void BroadcastingChat()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        string text = InputFdChat.text.Trim();
        if (text.Length > ChatText.MaxLength) text = text.Substring(0, ChatText.MaxLength);
        pv.RPC(RpcLogMsg, RpcTarget.All, KindChat, text);

        InputFdChat.text = "";
    }
}
