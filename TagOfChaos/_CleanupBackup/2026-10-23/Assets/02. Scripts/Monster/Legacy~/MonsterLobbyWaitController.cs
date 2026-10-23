using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameLobbyScene 배치(MonsterManagers). 괴물 클라이언트만 대기실에 남겨 두었다가, 색칠 페이즈가 끝나는
// 시각에 혼자 GameScene으로 이동시킨다(Bug-fix-plan.md §23.3). 괴물이 아닌 클라이언트에서는 아무 일도
// 하지 않는다.
//
// 흐름: 괴물 확정 → AutomaticallySyncScene=false(방장의 LoadLevel에 끌려가지 않음) → 방장이 시작 버튼으로
// PaintPhaseEndTime을 새로 기록 → 이 변경을 받는 순간 NetHold로 "맵에서 온 것"(쿠키들이 맵에서 만든 캐릭터·동기화 값·색칠)을
// 보관하기 시작한다 → 로컬 카운트다운 종료 시 PhotonNetwork.LoadLevel(맵)(방장이 아니므로 혼자만 로드) → 맵을 다 불러온 순간
// NetHold가 보관한 것을 맵에 꺼낸다. 보관하지 않으면 쿠키 캐릭터가 이 대기실에 생겼다가 씬 전환 때 파괴돼 괴물은 맵에서
// 쿠키를 하나도 볼 수 없다(§23.3.2 F4). 예전에는 메시지 큐를 통째로 멈춰 같은 일을 했는데, 그러면 함께 기다리는 다른 괴물의
// 움직임도 받지 못해 괴물이 2명일 때 서로 보이지 않았다(Request1009Plan.md §21, 결정 Q2-D).
public class MonsterLobbyWaitController : MonoBehaviourPunCallbacks
{

    [SerializeField] private GameObject waitPanelRoot; // 반드시 이 컴포넌트와 다른 오브젝트(자기 자신을 끄는 함정 회피)
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private string countdownFormat = "{0}"; // 표시 문구는 인스펙터에서 입력
    private int lastShownSeconds = -1;
    [SerializeField] private Button[] buttonsToDisableWhileWaiting;      // Back 버튼 등
    [SerializeField] private Behaviour[] behavioursToDisableWhileWaiting; // GameManager(채팅) 등
    // 기다리는 동안 숨길 대기실 UI(참가자 목록 등 — 대기 화면이 대신 보인다).
    [SerializeField] private GameObject[] objectsToHideWhileWaiting;

    private bool isWaiting;

    private static MonsterLobbyWaitController local; // 대기실에 하나 — 배경음(MusicDirector)이 "괴물 대기 중"을 묻는다
    public static bool IsLocalWaiting => local != null && local.isWaiting;
    private double departAtLocalTime; // Time.realtimeSinceStartupAsDouble 기준 출발 시각

    private void Awake()
    {
        local = this;
    }

    private void OnDestroy()
    {
        if (local == this) local = null;
    }

    private void Start()
    {
        if (waitPanelRoot != null) waitPanelRoot.SetActive(false);
        ApplySceneSyncPolicy(); // 이미 괴물로 확정된 상태로 대기실에 들어온 경우
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(NetKeys.MonsterActorNumbers)) ApplySceneSyncPolicy();

        // 시작 신호: 방장이 시작 버튼을 누르며 새로 쓴 PaintPhaseEndTime. 대기실 입장 시 이미 있던(이전 판)
        // 값은 "변경"이 아니므로 여기로 들어오지 않는다.
        if (propertiesThatChanged.ContainsKey(NetKeys.PaintPhaseEndTime) && !isWaiting && RoomState.IsLocalMonster())
            EnterWaiting();
    }

    // 괴물이면 방장의 LoadLevel에 끌려가지 않도록 끄고, 아니면(판 초기화로 괴물 목록이 지워진 경우 포함) 다시 켠다.
    // 예전에는 끄기만 해서, 이전 판 괴물이 대기실로 돌아온 직후 옛 값을 보고 꺼진 채 남으면 다음 판에
    // 방장을 따라가지 못했다(Bug-fix-plan.md §24.5). 값이 바뀔 때만 대입한다 — true 대입은 PUN이 즉시
    // LoadLevelIfSynced를 호출하기 때문이다.
    private void ApplySceneSyncPolicy()
    {
        bool shouldSync = !RoomState.IsLocalMonster();
        if (PhotonNetwork.AutomaticallySyncScene == shouldSync) return;

        PhotonNetwork.AutomaticallySyncScene = shouldSync;
        Debug.Log($"[MonsterLobbyWait] AutomaticallySyncScene={shouldSync} (local actor {PhotonNetwork.LocalPlayer?.ActorNumber}, isMonster={!shouldSync}).");
    }

    private void EnterWaiting()
    {
        if (!RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double endTime)) return;

        // 큐가 멈춘 동안에는 서버 시간 보정을 받지 못하므로, 남은 시간은 지금 한 번만 계산하고
        // 이후로는 timeScale 영향이 없는 로컬 실시간 시계로만 센다.
        double remaining = System.Math.Max(0, endTime - PhotonNetwork.Time);
        departAtLocalTime = Time.realtimeSinceStartupAsDouble + remaining;
        isWaiting = true;
        lastShownSeconds = -1;

        RemoveOtherPlayersAvatars();
        SetWaitingUi(true);

        NetHold.Begin(); // 메시지 큐는 계속 돈다 — 함께 기다리는 괴물의 움직임이 보인다
        Debug.Log($"[MonsterLobbyWait] Monster (actor {PhotonNetwork.LocalPlayer.ActorNumber}) waits in lobby for {remaining:F1}s.");
    }

    private void Update()
    {
        if (!isWaiting) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Leaving)
        {
            isWaiting = false; // 기다리다 나가기를 눌렀으면 맵으로 떠나지 않는다
            return;
        }

        double remaining = departAtLocalTime - Time.realtimeSinceStartupAsDouble;
        int seconds = Mathf.CeilToInt((float)System.Math.Max(0, remaining));
        if (seconds != lastShownSeconds)
        {
            if (lastShownSeconds >= 0) UiSoundCues.CountdownTick(seconds); // 괴물 대기 마지막 10초(로컬 재생이라 큐가 멈춰도 난다)
            lastShownSeconds = seconds;
            if (countdownText != null) countdownText.text = string.Format(countdownFormat, seconds);
        }

        if (remaining > 0) return;

        isWaiting = false;
        string map = GameStartAuthority.CurrentMapScene(); // 방장이 고른 이번 판 맵
        if (string.IsNullOrEmpty(map))
        {
            // 갈 씬이 없으면 보관한 것을 버리고 대기실에 남는다.
            Debug.LogError("[MonsterLobbyWait] No game map to load. Staying in the lobby.");
            NetHold.Discard();
            SetWaitingUi(false);
            return;
        }
        GameAudio.Play(SoundId.MonsterDeparted); // 괴물이 맵으로 떠나는 순간(2D, 씬이 바뀌어도 소리는 이어진다 — S4)
        PhotonNetwork.LoadLevel(map); // 방장이 아니므로 혼자만 로드 — 다 불러오면 NetHold가 보관한 것을 꺼낸다
    }

    // 쿠키들은 이미 맵으로 떠나 그 대기실 아바타는 더 이상 갱신되지 않는다(굳은 채 남음). 원래 주인 쪽에서는 이미 씬 전환으로
    // 파괴됐으므로 로컬에서만 치운다. 함께 기다리는 괴물의 아바타는 남긴다 — 서로 보이고 움직임도 동기화된다(Request1009Plan.md §21).
    private void RemoveOtherPlayersAvatars()
    {
        // 목록을 순회하며 Destroy하면 OnDisable의 등록 해제로 목록이 바뀌므로 먼저 모은다.
        var others = new System.Collections.Generic.List<GameObject>();
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.Role == CharacterRole.Cookie && CharacterRegistry.IsAlive(c) && c.View != null && !c.View.IsMine
                && (c.View.Owner == null || !RoomState.IsMonster(c.View.Owner.ActorNumber))) others.Add(c.gameObject);
        foreach (GameObject go in others) Destroy(go);
    }

    // 기다리는 동안 막을 입력(채팅 — 맵의 채팅과 PhotonView가 겹친다, 뒤로 가기 등)(§23.3.4-①, D3).
    private void SetWaitingUi(bool waiting)
    {
        if (waitPanelRoot != null) waitPanelRoot.SetActive(waiting);

        if (objectsToHideWhileWaiting != null)
        {
            foreach (var go in objectsToHideWhileWaiting)
                if (go != null) go.SetActive(!waiting);
        }

        if (buttonsToDisableWhileWaiting != null)
        {
            foreach (var button in buttonsToDisableWhileWaiting)
                if (button != null) button.interactable = !waiting;
        }

        if (behavioursToDisableWhileWaiting != null)
        {
            foreach (var behaviour in behavioursToDisableWhileWaiting)
                if (behaviour != null) behaviour.enabled = !waiting;
        }
    }
}
