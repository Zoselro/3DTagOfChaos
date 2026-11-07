using Photon.Pun;
using UnityEngine;

// 오프라인 개발 부트스트랩. 구 4라운드 색상 미니게임(ColorSelectionManager)이 GameRule.md v3.6로
// 완전히 대체되면서 자동 시작 기능은 일단 방 생성까지만 남겨뒀다 — 새 게임 룰(가마솥→색칠→GrabKill)
// 자동 시작은 §14.3 범위 밖의 별도 결정 사항.
public class OfflineModeBootstrap : MonoBehaviourPunCallbacks
{
    [SerializeField] private string testRoomName = "OfflineTestRoom";
    [SerializeField] private bool autoCreateRoom = false;
    [SerializeField] private bool spawnAsMonster = false;
    [Tooltip("혼자 테스트할 때 스파이로 시작한다(방에 들어가면 자기를 스파이 목록에 넣고 탈출 배치를 다시 짠다 — 스파이 상자·로켓 칸이 생긴다).")]
    [SerializeField] private bool spawnAsSpy = false;
    [Header("Giant monster test (Request1009Plan.md §64)")]
    [Tooltip("On: the giant monster always appears when the witch timer starts. Off: it never appears (offline room only — online uses the 30% roll).")]
    [SerializeField] private bool spawnGiant = true;
    [Tooltip("Witch time (time attack) in seconds for the offline room. The giant's speed steps up every (this / 10) seconds.")]
    [SerializeField, Min(10)] private int witchTimeSeconds = 60;
    [Tooltip("Start the witch timer as soon as the hunt begins (normally it starts on the first escape), so the giant can be tested right away.")]
    [SerializeField] private bool startWitchTimerOnHunt = true;

    private bool witchTimerStarted;

    // PlayerSpawner/MonsterTestSpawner가 참조하는 개발용 플래그 — 씬 배치 순서에 의존하지 않도록 static.
    public static bool SpawnAsMonster { get; private set; }
    // 오프라인 방에서 거대 괴물이 나올지(체크박스). 이 부트스트랩이 없는 씬·온라인에서는 null(확률로 정한다).
    public static bool? GiantOverride { get; private set; }

    private void Awake()
    {
        PhotonNetwork.OfflineMode = true;
        SpawnAsMonster = spawnAsMonster;
        GiantOverride = spawnGiant;
    }

    // 혼자 스파이로 시험할 때: 방에 들어가면 자기를 스파이 목록에 넣는다.
    // 마녀 시간(초)은 늘 이 값으로 정한다(§64 — 거대 괴물 속도 단계 = 마녀 시간 ÷ 10).
    public override void OnJoinedRoom()
    {
        if (!PhotonNetwork.OfflineMode) return;
        var props = new ExitGames.Client.Photon.Hashtable { [NetKeys.TimeAttackDuration] = witchTimeSeconds };
        if (spawnAsSpy && !spawnAsMonster)
        {
            props[NetKeys.SpyActorNumbers] = new[] { PhotonNetwork.LocalPlayer.ActorNumber };
            props[NetKeys.EscapeState] = null; // 스파이가 있는 판으로 탈출 배치를 다시 짠다
        }
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // PhotonNetwork.OfflineMode와 SpawnAsMonster는 static이라 씬이 바뀌어도 남는다. 에디터에서 이 테스트 씬 다음에
    // 다른 씬을 실행할 때 오프라인 상태가 이어지지 않도록 되돌린다(research.md §8.23).
    private void OnDestroy()
    {
        SpawnAsMonster = false;
        GiantOverride = null;
        if (PhotonNetwork.OfflineMode && !PhotonNetwork.InRoom) PhotonNetwork.OfflineMode = false;
    }

    // 사냥이 시작되면 마녀 타이머를 바로 시작한다(시험용 — 원래는 첫 탈출 때). 쿠키 자리를 앞 샷으로.
    private void Update()
    {
        if (!startWitchTimerOnHunt || witchTimerStarted || !PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient) return;
        if (GamePhaseState.Current != GamePhase.Hunt || EscapeManager.Instance == null || EscapeManager.Instance.State == null) return;
        witchTimerStarted = true;
        HideOrSeekPlayer cookie = FindFirstObjectByType<HideOrSeekPlayer>();
        EscapeManager.Instance.StartWitchTimer(cookie != null ? cookie.transform.position : Vector3.zero);
    }

    private void Start()
    {
        if (!autoCreateRoom) return;
        if (!PhotonNetwork.OfflineMode) return;

        PhotonNetwork.CreateRoom(testRoomName);
    }
}
