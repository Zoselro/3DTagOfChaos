using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyController : MonoBehaviourPunCallbacks
{
    // 빌드 버전(Player Settings → Version, Application.version)이 다르면 Photon이 매치메이킹을 나눠 서로의 방이 보이지 않고 들어갈 수 없다(§71 Q4).
    // 새 빌드를 만들 때마다 Player Settings의 Version을 올린다. 예전 수동 번호의 마지막 값은 "6"이었다.
    private static string GameVersion => Application.version; // 빌드가 바뀌면 올려서 이전 버전 클라이언트와 매치메이킹이 섞이지 않게 함(2: 채팅·들기 RPC 서명 변경, AuditFixPlan.md P4 / 3: 대기실 문 이벤트·다시 생김 알림, Request1009Plan.md §22 / 4: 마녀 연출 시각표, §23 / 5: 마녀 도착 3테이크·3배, §24 / 6: 게임 구성 개편, §25)

    [SerializeField] private TMP_InputField userIdInput;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private RoomListItem roomListItemPrefab;
    [SerializeField] private Button refreshButton;

    [Header("Room Settings (Request1009Plan.md §25.5 A)")]
    [SerializeField] private RoomSettingField playersField;
    [UnityEngine.Serialization.FormerlySerializedAs("timeLimitField")]
    [SerializeField] private RoomSettingField spyCountField;
    [UnityEngine.Serialization.FormerlySerializedAs("timeAttackField")]
    [SerializeField] private RoomSettingField witchTimerField;

    // 서버 지역(EscapeVisualPlan.md §1.1, 2026-10-03 다시 살림 — Request1003Plan.md §1). 방은 한 지역 서버 안에만 있으므로
    // 지역을 고르지 않고 접속하면 PC마다 핑이 좋은 지역에 따로 붙어 서로의 방이 보이지 않는다 — 늘 고른 지역으로 접속한다.
    [Header("Region")]
    [Tooltip("로비에서 볼 지역 — 고르면 그 지역으로 다시 접속해 그 지역의 방 목록을 보여준다")]
    [SerializeField] private RegionSelector viewRegion;
    [Tooltip("방을 만들 지역(타임어택 칸 밑) — 지금 접속한 지역과 다르면 다시 접속한 뒤 방을 만든다")]
    [SerializeField] private RegionSelector createRegion;

    [Header("Feedback Messages (set in inspector)")] // 코드에 한글을 넣지 않는 프로젝트 규칙 — 표시 문구는 프리팹에서 입력
    [SerializeField] private string enterRoomNameMessage = "Enter a room name.";
    [SerializeField] private string enterUserIdMessage = "Enter a user ID.";
    [SerializeField] private string roomNameExistsMessage = "A room with this name already exists.";
    [SerializeField] private string noRoomAvailableMessage = "No room is available to join.";
    [SerializeField] private string cannotJoinRoomMessage = "Cannot join this room.";
    [SerializeField] private string kickedMessage = "You were kicked by the host."; // Request1009Plan.md §19.6
    [SerializeField] private string regionMoveFailedMessage = "Could not follow the room to the new region.";

    private readonly Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    private readonly Dictionary<string, RoomListItem> roomListItems = new Dictionary<string, RoomListItem>();

    // 방 목록 새로고침(GameFixPlan.md F3). OnRoomListUpdate는 변경분만 주므로 한 번 놓치면 그 방이 다음 변경 때까지
    // 보이지 않는다. Photon은 로비에 새로 들어올 때 전체 목록을 다시 보내므로, 로비를 나갔다 다시 들어간다.
    // 연타로 로비를 계속 드나들지 않도록 잠시 버튼을 막는다.
    private const float RefreshCooldown = 2f;
    private float refreshReadyTime;
    private bool rejoinLobbyAfterLeave;

    // 지역 바꾸기: 끊고(OnDisconnected) → targetRegion으로 다시 접속. 다른 지역에 방을 만들거나(로비) 옮겨 간 방에 들어갈 때(대기실의
    // 지역 변경, RegionMove)는 접속 뒤 바로 만든다/들어간다.
    private string targetRegion;
    private bool reconnectAfterDisconnect;
    private string pendingRoomName;
    private RoomOptions pendingRoomOptions;
    private bool pendingJoinOrCreate; // true = 지역 옮기기(같은 이름의 방에 들어가거나 먼저 도착했으면 만든다)

    private static string CurrentRegion => PhotonRegions.Current;

    private void Awake()
    {
        // #Critical: LoadLevel()이 방 전체에 씬 전환을 자동 동기화하게 함 (PunBasics-Tutorial 관례)
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = GameVersion;
        RoomState.HideSettingRow(witchTimerField); // 마녀 타이머는 10분 고정(§74 P8)
        Debug.Log($"[Lobby] Game version {GameVersion} — only players with the same build version can see and join each other's rooms.");
    }


    private void Start()
    {
        userIdInput.text = !string.IsNullOrEmpty(PhotonNetwork.NickName) ? PhotonNetwork.NickName : "Player" + Random.Range(1000, 10000); // 기본값, 직접 수정 가능
        if (LobbyKick.ConsumeKickedNotice()) ShowError(kickedMessage); // 대기실에서 강퇴되어 돌아왔다

        string region = CurrentRegion ?? PhotonRegions.Saved;
        if (RegionMove.HasPending) // 대기실에서 호스트가 지역을 바꿔 방을 옮기는 중
        {
            region = RegionMove.PendingRegion;
            pendingRoomName = RegionMove.PendingRoomName;
            pendingRoomOptions = RegionMove.PendingOptions;
            pendingJoinOrCreate = true;
        }
        if (viewRegion != null)
        {
            viewRegion.SetCode(region);
            viewRegion.Changed += OnViewRegionChanged;
        }
        if (createRegion != null) createRegion.SetCode(region);
        SwitchRegion(region);
    }

    private void OnDestroy()
    {
        if (viewRegion != null) viewRegion.Changed -= OnViewRegionChanged;
    }

    private void OnViewRegionChanged(string code)
    {
        PhotonRegions.Saved = code;
        if (createRegion != null) createRegion.SetCode(code);
        SwitchRegion(code);
    }

    // 이미 그 지역에 붙어 있으면 로비만 들어가고(또는 기다리던 방 만들기·들어가기), 아니면 끊고 다시 접속한다.
    private void SwitchRegion(string region)
    {
        Debug.Log($"[Lobby] Switch region {CurrentRegion ?? "-"} -> {region}" + (pendingRoomName != null ? $" (then room '{pendingRoomName}')" : ""));
        targetRegion = region;
        if (PhotonNetwork.IsConnected && CurrentRegion == region)
        {
            if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby && !PhotonNetwork.InRoom)
            {
                if (!RunPendingRoom()) PhotonNetwork.JoinLobby();
            }
            return; // 아직 마스터 서버로 돌아가는 중이면 OnConnectedToMaster가 이어서 한다
        }
        cachedRoomList.Clear();
        ClearRoomListView();
        if (PhotonNetwork.IsConnected)
        {
            reconnectAfterDisconnect = true;
            PhotonNetwork.Disconnect();
        }
        else
        {
            ConnectToRegion(region);
        }
    }

    // 설정 에셋은 건드리지 않고 사본에 지역만 넣어 접속한다(ConnectToRegion은 PUN에서 사용 중단 예정).
    private static void ConnectToRegion(string region)
    {
        var settings = new AppSettings();
        PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(settings);
        settings.FixedRegion = region;
        PhotonNetwork.ConnectUsingSettings(settings);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!reconnectAfterDisconnect) return;
        reconnectAfterDisconnect = false;
        ConnectToRegion(targetRegion);
    }

    // 기다리던 방 만들기·들어가기가 있으면 실행하고 true.
    private bool RunPendingRoom()
    {
        if (pendingRoomName == null) return false;
        string name = pendingRoomName;
        RoomOptions options = pendingRoomOptions;
        bool joinOrCreate = pendingJoinOrCreate;
        pendingRoomName = null;
        pendingRoomOptions = null;
        pendingJoinOrCreate = false;
        if (joinOrCreate) PhotonNetwork.JoinOrCreateRoom(name, options, TypedLobby.Default);
        else PhotonNetwork.CreateRoom(name, options, TypedLobby.Default);
        return true;
    }

    private void Update()
    {
        bool connected = PhotonNetwork.IsConnectedAndReady && !reconnectAfterDisconnect;
        if (viewRegion != null) viewRegion.SetInteractable(connected && pendingRoomName == null && !RegionMove.HasPending);
        if (refreshButton == null) return;
        bool ready = Time.unscaledTime >= refreshReadyTime && connected && !rejoinLobbyAfterLeave;
        if (refreshButton.interactable != ready) refreshButton.interactable = ready;
    }

    public void OnRefreshButtonClicked()
    {
        if (Time.unscaledTime < refreshReadyTime || !PhotonNetwork.IsConnectedAndReady || rejoinLobbyAfterLeave) return;
        refreshReadyTime = Time.unscaledTime + RefreshCooldown;

        if (PhotonNetwork.InLobby)
        {
            rejoinLobbyAfterLeave = true;
            PhotonNetwork.LeaveLobby(); // OnLeftLobby에서 다시 들어간다
        }
        else
        {
            PhotonNetwork.JoinLobby();
        }
    }

    public override void OnLeftLobby()
    {
        if (!rejoinLobbyAfterLeave) return;
        rejoinLobbyAfterLeave = false;
        PhotonNetwork.JoinLobby(); // OnJoinedLobby가 목록을 비우고, 곧 전체 목록이 OnRoomListUpdate로 온다
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log($"[Lobby] Connected to region {PhotonNetwork.CloudRegion}.");
        if (!RunPendingRoom()) PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        cachedRoomList.Clear();
        ClearRoomListView();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            if (info.RemovedFromList)
            {
                cachedRoomList.Remove(info.Name);
                continue;
            }

            cachedRoomList[info.Name] = info; // IsOpen==false여도 목록에는 남겨둔다 (0.1-5)
        }

        RefreshRoomListView();
    }

    private void RefreshRoomListView()
    {
        foreach (var kv in cachedRoomList)
        {
            if (!roomListItems.TryGetValue(kv.Key, out RoomListItem item))
            {
                item = Instantiate(roomListItemPrefab, roomListContent);
                roomListItems.Add(kv.Key, item);
            }
            item.Refresh(kv.Value, this); // 이름 / "N / 정원" / 입장 버튼 interactable 갱신
        }

        // 목록에서 사라진 방의 UI 항목 정리
        var toRemove = new List<string>();
        foreach (var kv in roomListItems)
        {
            if (!cachedRoomList.ContainsKey(kv.Key)) toRemove.Add(kv.Key);
        }
        foreach (string name in toRemove)
        {
            Destroy(roomListItems[name].gameObject);
            roomListItems.Remove(name);
        }
    }

    private void ClearRoomListView()
    {
        foreach (var kv in roomListItems)
            Destroy(kv.Value.gameObject);
        roomListItems.Clear();
    }

    public void OnMakeRoomButtonClicked()
    {
        if (!TryApplyNickname()) return;

        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            ShowError(enterRoomNameMessage);
            return;
        }

        // 방장이 고른 인원·스파이 수·마녀 타이머(Request1009Plan.md §25.5 A). 입력칸 값은 RoomSettingField가 이미 허용 범위로 맞춰 둔다.
        // 스파이 수와 마녀 타이머는 판이 바뀌어도 유지되는 Room Prop으로 넣는다.
        GameSettingsSO settings = GameSettings.Current;
        int players = playersField != null ? playersField.Value : settings.DefaultPlayers;
        int spies = settings.ClampSpies(spyCountField != null ? spyCountField.Value : settings.DefaultSpies, players);
        var options = new RoomOptions
        {
            MaxPlayers = players,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { NetKeys.RoomSpyCount, spies }, // 마녀 타이머는 방 속성으로 쓰지 않는다(10분 고정, §74 P8)
            },
        };
        string region = createRegion != null ? createRegion.Code : CurrentRegion;
        if (region != null && region != CurrentRegion)
        {
            // 다른 지역에 만든다: 그 지역으로 다시 접속한 뒤(OnConnectedToMaster) 만든다. 로비의 볼 지역도 그 지역으로 맞춘다.
            pendingRoomName = roomName;
            pendingRoomOptions = options;
            pendingJoinOrCreate = false;
            PhotonRegions.Saved = region;
            if (viewRegion != null) viewRegion.SetCode(region);
            SwitchRegion(region);
            return;
        }
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
    }

    public void OnRandomJoinButtonClicked()
    {
        if (!TryApplyNickname()) return;
        PhotonNetwork.JoinRandomRoom(null, 0, MatchmakingMode.RandomMatching, null, null);
    }

    public void JoinRoom(string roomName) // RoomListItem에서 호출
    {
        if (!TryApplyNickname()) return;
        PhotonNetwork.JoinRoom(roomName);
    }

    private bool TryApplyNickname()
    {
        string nickname = userIdInput.text.Trim();
        if (string.IsNullOrEmpty(nickname))
        {
            ShowError(enterUserIdMessage);
            return false;
        }
        PhotonNetwork.NickName = nickname;
        return true;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[Lobby] Create room failed ({returnCode}): {message}");
        if (FailRegionMove()) return;
        ShowError(roomNameExistsMessage); // 대부분 이 케이스 (ErrorCode.GameIdAlreadyExists)
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        ShowError(noRoomAvailableMessage);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[Lobby] Join room failed ({returnCode}): {message}");
        if (FailRegionMove()) return;
        ShowError(cannotJoinRoomMessage); // 방금 꽉 찼거나 방장이 이미 게임을 시작한 경우 등
    }

    // 지역 옮기기 중 실패: 옮기기를 끝내고 그 지역의 로비에 남는다.
    private bool FailRegionMove()
    {
        if (!RegionMove.HasPending) return false;
        RegionMove.Clear();
        ShowError(regionMoveFailedMessage);
        return true;
    }

    // 실패 안내: 문구 + 경고음(SoundPlan.md S2).
    private void ShowError(string message)
    {
        feedbackText.text = message;
        UiSoundCues.Error();
    }

    public override void OnJoinedRoom()
    {
        if (RegionMove.HasPending)
        {
            // 옮겨 간 방이 맞는지(같은 이름의 남의 방이 아닌지) 표로 확인한다.
            bool expected = RegionMove.IsExpectedRoom(PhotonNetwork.CurrentRoom);
            RegionMove.Clear();
            if (!expected)
            {
                PhotonNetwork.LeaveRoom(); // 마스터 서버로 돌아가면 OnConnectedToMaster가 로비에 다시 들어간다
                ShowError(regionMoveFailedMessage);
                return;
            }
        }
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
            PunSceneLoad.Load(SceneNames.GameLobby); // 방을 새로 만든 최초 1인만 로드, 나머지는 자동 동기화
    }
}
