using System.Linq;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대기실 패널: 참가자 목록, 상태 문구(대기 사유), 호스트 전용 시작 버튼.
// 시작 버튼 주인(호스트 = 가장 먼저 들어온 사람, 괴물 여부 무관)과 게임 진행 권한(Photon 방장 = 괴물이 아닌 사람)을
// 분리했다(Bug-fix-plan.md §33 A안). 실제 시작 절차는 GameStartAuthority가 방장 쪽에서 실행한다.
public class GameLobbyController : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerListItem playerListItemPrefab;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button startGameButton;

    // 상태 문구는 상황별로 인스펙터에서 입력한다(코드에 한글 금지). 예전에는 "4 / 4 대기 중" 하나뿐이라, 정원이
    // 찼는데도 시작 버튼이 꺼져 있는 이유(괴물 선정 대기)를 알 수 없었다(Bug-fix-plan.md §26.4 S1).
    [Header("Status Messages (set in inspector)")]
    [SerializeField] private string waitingPlayersFormat = "{0} / {1} waiting for players";          // {0}=인원, {1}=정원
    [SerializeField] private string selectingMonsterNoTimerText = "Choosing the monster... enter the cauldron";
    [Tooltip("괴물이 여럿 필요할 때. {0} = 필요한 괴물 수, {1} = 정해진 수, {2} = 가마솥에 더 들어가야 할 사람 수")]
    [SerializeField] private string selectingMonstersFormat = "Choosing {0} monsters ({1}/{0}) - {2} more must enter the cauldron";
    [SerializeField] private string readyForMasterFormat = "Monster: {0} - you can start the game";   // {0}=괴물 닉네임 목록
    [SerializeField] private string readyForOthersFormat = "Monster: {0} - waiting for host {1} to start"; // {1}=방장 닉네임
    // 정원·괴물은 정해졌는데 아직 대기실을 불러오는 사람이 있을 때(Request1009Plan.md §43 — 막 재입장한 사람). {0} = 기다리는 사람 수
    [SerializeField] private string waitingArrivalFormat = "Waiting for {0} player(s) to enter the lobby...";
    [Tooltip("강퇴 확인 문구. {0} = 대상 닉네임(Request1009Plan.md §19.6)")]
    [SerializeField] private string kickConfirmFormat = "Kick {0}?";
    [Tooltip("녹음하지 않은 사람이 있을 때 시작 확인 문구(§26 H11)")]
    [SerializeField] private string unrecordedConfirmText = "Some players have not recorded. Start anyway?";

    // 시작 요청 후 방장 응답(씬 전환)을 기다리는 동안 버튼을 잠시 막는다 — 방장 교체 순간에 요청이 무시됐으면 다시 누를 수 있다.
    private const float StartRequestCooldown = 2f;
    private float startRequestPendingUntil = float.NegativeInfinity;
    private bool wasStartRequestPending;

    private int lastKnownPlayerCount = -1;
    private int lastKnownHostActor = -1;
    private readonly StringBuilder nameBuilder = new StringBuilder();

    // 강퇴: 응답하지 않는 대상은 방장이 연결을 끊는다 — PUN 2.55는 보내는 쪽·받는 쪽 모두 켜 두어야 한다.
    private void Awake() => PhotonNetwork.EnableCloseConnection = true;

    // 게임 씬에서는 끈다(F-17): 켜 둔 채 넘어가면 방장이 아닌 사람이 방장이 된 뒤 보낸 연결 끊기 요청에 게임 중에도 응한다.
    private void OnDestroy() => PhotonNetwork.EnableCloseConnection = false;

    private void Start()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return; // 방에 들어오지 않은 채 씬만 단독 실행된 경우 방어

        RefreshPlayerList();
        RefreshStartButton();
    }

    // AutomaticallySyncScene으로 뒤따라 입장하는 클라이언트는 Start() 시점에 룸 상태(인원수/방장 여부)를
    // 아직 완전히 따라잡지 못했을 수 있다 — 화면 값이 실제 값과 달라지면 다음 프레임에 스스로 바로잡는
    // 안전망. 괴물 선정 남은 시간은 초가 바뀔 때만 문구를 갱신한다.
    private bool regionMovePending;

    private void Update()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        if (regionMovePending) ApplyRegionMove();

        int currentCount = PhotonNetwork.CurrentRoom.PlayerCount;
        int currentHost = RoomState.HostActor();

        if (currentCount != lastKnownPlayerCount || currentHost != lastKnownHostActor)
        {
            lastKnownPlayerCount = currentCount;
            lastKnownHostActor = currentHost;
            RefreshPlayerList();
            RefreshStartButton();
        }

        bool isPending = IsStartRequestPending();
        if (isPending != wasStartRequestPending)
        {
            wasStartRequestPending = isPending;
            RefreshStartButton(); // 요청 대기가 끝나면 버튼을 다시 활성화
        }

        GameStartAuthority.TickPending(); // 방장 교체가 가라앉기를 기다리던 시작 요청을 다시 시도(§43)
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerList();
        RefreshStartButton();
    }

    // 누가 목소리를 녹음하면 목록의 녹음 표시를 바꾼다.
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps == null) return;
        if (changedProps.ContainsKey(NetKeys.HasVoice)) RefreshPlayerList();
        if (changedProps.ContainsKey(NetKeys.LoadedScene)) RefreshStartButton(); // 누가 대기실에 들어오면 시작 조건이 바뀐다(§43)
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerList();
        RefreshStartButton();
    }

    // 진행 권한(Photon 방장)이 바뀌어도 시작 버튼 주인(호스트)은 그대로다. 상태 문구만 갱신한다.
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshStartButton();
    }

    // 방장 쪽: 호스트의 시작·방 설정 요청을 받아 각 Authority로 실행한다(요청자·조건은 그 안에서 다시 확인).
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == NetEventCodes.Kicked)
        {
            if (LobbyKick.IsForMe(photonEvent.Sender, photonEvent.CustomData)) LeaveKicked();
            return;
        }
        if (!PhotonNetwork.IsMasterClient) return;
        if (photonEvent.Code == NetEventCodes.KickRequest)
        {
            if (photonEvent.CustomData is int target) KickAsMaster(photonEvent.Sender, target);
            return;
        }
        if (photonEvent.Code == NetEventCodes.StartGameRequest)
        {
            GameStartAuthority.Result result = GameStartAuthority.TryStart(photonEvent.Sender);
            Debug.Log($"[GameStart] Start request from actor {photonEvent.Sender}: {result}");
        }
        else if (photonEvent.Code == NetEventCodes.RoomSettingsRequest)
        {
            RoomSettingsAuthority.HandleRequest(photonEvent);
        }
    }

    // 괴물 확정 여부와 선정 기준 시각이 시작 조건·상태 문구에 들어가므로 바뀌는 순간 갱신한다 — Update()의
    // 안전망은 인원수/방장 여부만 감시해 Room Props 변화는 잡지 못한다.
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        // 호스트가 서버 지역을 바꿨다: 모두 새 지역의 같은 방으로 옮겨 간다(RegionMove). 받는 즉시 모두 방을 나가므로 방장이 쓴 값인지
        // 확인한 뒤(쓴 사람 기록은 이 콜백 직후 도착 — 다음 프레임) 옮긴다. 조작된 클라이언트 하나가 대기실 전원을 내보낼 수 있었다(S-12).
        if (propertiesThatChanged.ContainsKey(NetKeys.RegionMove))
        {
            regionMovePending = true;
            return;
        }

        // 괴물 선정뿐 아니라 방장이 방 설정으로 최대 인원을 바꿔도(Photon 기본 속성 MaxPlayers) 정원 판단이 바뀐다 —
        // 예전에는 괴물 키만 봐서 인원을 줄여 정원이 차도 "인원 부족" 문구가 그대로 남았다(2026-10-03). 어떤 방 속성이든 다시 그린다.
        RefreshStartButton();
    }

    private void ApplyRegionMove()
    {
        regionMovePending = false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.RegionMove, out object move)
            || !RegionMove.TryDecode(move, out string region, out string token)) return;
        if (!RoomPropertyGuard.WrittenByMaster(NetKeys.RegionMove))
        {
            Debug.LogWarning("[GameLobby] Ignored a region move that was not written by the master client.");
            return;
        }
        RegionMove.Begin(region, token);
    }

    private void RefreshPlayerList()
    {
        foreach (Transform child in playerListContent) Destroy(child.gameObject);

        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(pl => pl.ActorNumber)) // 입장 순서대로 표시
        {
            var item = Instantiate(playerListItemPrefab, playerListContent);
            item.SetNickname(p.NickName, RoomState.HasVoice(p));
            Player target = p;
            item.SetKick(LobbyKick.LocalCanKick(target), () => ConfirmKick(target));
        }
    }

    // ---------------- 강퇴(Request1009Plan.md §19.6) ----------------

    private void ConfirmKick(Player target)
    {
        ConfirmDialog dialog = FindFirstObjectByType<RoomExitController>()?.Dialog;
        string message = string.Format(kickConfirmFormat, DisplayName(target));
        if (dialog != null) dialog.Show(message, () => Kick(target));
        else Kick(target);
    }

    private void Kick(Player target)
    {
        if (!LobbyKick.LocalCanKick(target)) return; // 확인하는 사이 나갔거나 판이 시작됐다
        if (PhotonNetwork.IsMasterClient) KickAsMaster(PhotonNetwork.LocalPlayer.ActorNumber, target.ActorNumber);
        else LobbyKick.SendRequest(target.ActorNumber);
    }

    private void KickAsMaster(int sender, int target)
    {
        if (LobbyKick.Handle(sender, target)) StartCoroutine(CloseIfStillHere(target));
    }

    // 대상이 정해진 시간 뒤에도 방에 남아 있으면(응답하지 않음) 연결을 끊는다.
    private System.Collections.IEnumerator CloseIfStillHere(int target)
    {
        yield return new WaitForSecondsRealtime(LobbyKick.CloseConnectionDelay);
        if (!PhotonNetwork.IsMasterClient || !RoomState.IsInRoom()) yield break;
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(target);
        if (player != null) PhotonNetwork.CloseConnection(player);
    }

    // 강퇴됐다: 로비 화면이 알려 주도록 남기고 평소 나가기 경로로 나간다.
    private void LeaveKicked()
    {
        Debug.Log("[LobbyKick] Kicked by the host. Leaving the room.");
        LobbyKick.MarkKicked();
        RoomExitController exit = FindFirstObjectByType<RoomExitController>();
        if (exit != null) exit.OnClickBackBtn();
        else PhotonNetwork.LeaveRoom();
    }

    // 시작 버튼은 호스트에게만 보이고(사용자 요구: "시작 버튼이 보이는 사람은 항상 방장(방을 만든 사람)"), 시작 조건을
    // 만족할 때만 눌린다(회색/흰색 전환은 Button 컴포넌트가 자동 처리). 호스트가 괴물이어도 버튼은 그대로다(§33).
    private void RefreshStartButton()
    {
        bool isHost = RoomState.IsLocalHost();
        startGameButton.gameObject.SetActive(isHost);
        if (isHost) startGameButton.interactable = CanStartGame();
        RefreshStatus();
    }

    private static bool IsRoomFull() => RoomState.IsRoomFull();

    private bool IsStartRequestPending() => Time.unscaledTime < startRequestPendingUntil;

    private bool CanStartGame() => GameStartAuthority.IsReady() && !IsStartRequestPending();

    private void RefreshStatus()
    {
        if (statusText == null || !RoomState.IsInRoom()) return;
        Room room = PhotonNetwork.CurrentRoom;

        if (!IsRoomFull())
        {
            statusText.text = string.Format(waitingPlayersFormat, room.PlayerCount, room.MaxPlayers);
            return;
        }

        if (!RoomState.IsMonsterSelectionComplete())
        {
            // 괴물은 가마솥에 들어간 사람만 된다(늘 1명, Request1009Plan.md §25). 여럿이 필요한 설정이면 몇 명이 더 들어가야 하는지 보여 준다.
            int required = GameSettings.Current.MonsterCountFor(room.PlayerCount);
            int chosen = RoomState.PresentMonsterCount();
            statusText.text = required > 1
                ? string.Format(selectingMonstersFormat, required, chosen, required - chosen)
                : selectingMonsterNoTimerText;
            return;
        }

        int arriving = PlayersNotInLobby();
        if (arriving > 0)
        {
            statusText.text = string.Format(waitingArrivalFormat, arriving);
            return;
        }

        string monsterNames = BuildMonsterNames();
        if (RoomState.IsLocalHost())
        {
            statusText.text = string.Format(readyForMasterFormat, monsterNames);
            return;
        }

        // 시작할 사람 = 호스트(가장 먼저 들어온 사람).
        statusText.text = string.Format(readyForOthersFormat, monsterNames, DisplayName(room.GetPlayer(RoomState.HostActor())));
    }

    private static int PlayersNotInLobby()
    {
        int count = 0;
        foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
            if (RoomState.LoadedScene(p) != SceneNames.GameLobby) count++;
        return count;
    }

    // 닉네임이 비어 있으면(오프라인 테스트 등) 입장 번호로 대신 표시한다.
    private static string DisplayName(Player player)
    {
        if (player == null) return "-";
        return string.IsNullOrEmpty(player.NickName) ? "#" + player.ActorNumber : player.NickName;
    }

    private string BuildMonsterNames()
    {
        nameBuilder.Clear();
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return string.Empty;
        foreach (int actor in monsters)
        {
            Player p = PhotonNetwork.CurrentRoom.GetPlayer(actor);
            if (p == null) continue;
            if (nameBuilder.Length > 0) nameBuilder.Append(", ");
            nameBuilder.Append(DisplayName(p));
        }
        return nameBuilder.ToString();
    }

    // 호스트가 "게임 시작" 버튼을 눌렀을 때 (UI Button.onClick에 연결). 진행 권한을 가진 방장이면 바로 시작하고,
    // 아니면 방장에게 요청한다(GameStartAuthority).
    public void OnStartGameButtonClicked()
    {
        if (!RoomState.IsLocalHost() || !CanStartGame()) return; // 방어적 재확인

        // 녹음하지 않은 사람이 있으면 한 번 묻는다(§26 H11 — 예: 시작, 아니오: 그대로)
        ConfirmDialog dialog = FindFirstObjectByType<RoomExitController>()?.Dialog;
        if (AnyoneUnrecorded() && dialog != null) { dialog.Show(unrecordedConfirmText, StartGame); return; }
        StartGame();
    }

    private static bool AnyoneUnrecorded()
    {
        foreach (Player p in PhotonNetwork.PlayerList) if (!RoomState.HasVoice(p)) return true;
        return false;
    }

    private void StartGame()
    {
        if (!RoomState.IsLocalHost() || !CanStartGame()) return; // 확인하는 사이 조건이 바뀌었을 수 있다

        startRequestPendingUntil = Time.unscaledTime + StartRequestCooldown;
        RefreshStartButton();
        GameStartAuthority.RequestStart();
    }
}
