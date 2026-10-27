using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// 방 씬 따라가기(Request1009Plan.md §43 F1). PUN의 자동 씬 동기화는 curScn이 바뀌는 순간 "방장인" 클라이언트를 따라가게 하지 않고,
// 그 뒤로는 다음 Room Props 변경이 있어야 다시 확인한다. 시작 직후 방장이 괴물 호스트에게 잠깐 넘어가면 호스트만 대기실에 남고
// 맵에는 괴물도 방장도 없는 판이 진행됐다. 이 컴포넌트는 방 안이면 늘 "내 씬 = 방의 씬"인지 보고, 아니면 방장이어도 그 씬을 불러온다.
// 함께: 끊긴 로드로 메시지 큐가 멈춘 채 남으면 되살리고, 지금 들어와 있는 씬을 Player Prop(NetKeys.LoadedScene)으로 알린다
// (시작 조건 — 모두 대기실에 있음, 맵 도착 감시가 쓴다).
// 씬과 무관하게 방 안이면 늘 동작해야 하므로 RoomPropertyGuard처럼 씬 로드 뒤 스스로 만들어 남는다.
public class RoomSceneFollower : MonoBehaviourPunCallbacks
{
    // PUN 내부 키(PhotonNetwork.CurrentSceneProperty는 internal).
    public const string CurrentSceneKey = "curScn";

    private const float TickInterval = 0.25f;
    private const float FollowAfterSeconds = 2f;    // 서버 응답(curScn 캐시)이 따라올 시간을 준다
    private const float RestoreQueueAfterSeconds = 3f;
    private const float OwnLoadTrustSeconds = 10f;  // 방장이 직접 옮긴 직후 캐시가 옛 값인 동안
    private const float RepublishSeconds = 2f;      // LoadedScene 쓰기 응답이 안 오면 다시 쓴다

    private static RoomSceneFollower instance;
    private static string ownLoadScene;
    private static float ownLoadAt = -100f;

    private float nextTick;
    private float mismatchSince = -1f;
    private float queuePausedSince = -1f;
    private string publishedScene;
    private float publishedAt = -100f;

    public static int FollowCount { get; private set; }       // 시험·로그용
    public static int QueueRestoreCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(RoomSceneFollower));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RoomSceneFollower>();
    }

    // 방이 가리키는 씬(없으면 null).
    public static string RoomScene() =>
        RoomState.IsInRoom() && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CurrentSceneKey, out object v) ? v as string : null;

    // PUN LoadLevel이 씬을 불러오는 중인지(0 = 한 번도 안 불렀음, 1 = 끝남).
    public static bool IsPunLoading
    {
        get
        {
            float p = PhotonNetwork.LevelLoadingProgress;
            return p > 0f && p < 1f;
        }
    }

    // 내가 방의 씬에 들어와 있는지(방 씬 정보가 없으면 그렇다고 본다). 방장 되찾기가 쓴다 — 씬에 없는 사람은 방장이 되면 안 된다.
    public static bool InRoomScene()
    {
        string room = RoomScene();
        string active = SceneManager.GetActiveScene().name;
        return string.IsNullOrEmpty(room) || room == active
            || SceneSyncRules.TrustOwnLoad(ownLoadScene, active, Time.realtimeSinceStartup - ownLoadAt, OwnLoadTrustSeconds);
    }

    // RoomSceneTransition이 방 전체를 옮길 때 알린다.
    public static void NoteOwnLoad(string sceneName)
    {
        ownLoadScene = sceneName;
        ownLoadAt = Time.realtimeSinceStartup;
        RoomSyncLog.Note("Moving the room to " + sceneName);
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        if (now < nextTick) return;
        nextTick = now + TickInterval;

        bool loading = IsPunLoading;
        RestoreQueueIfStuck(now, loading);
        if (!RoomState.IsInRoom() || !PhotonNetwork.IsMessageQueueRunning) { mismatchSince = -1f; return; }

        PublishLoadedScene(now, loading);
        FollowRoomScene(now, loading);
    }

    private void FollowRoomScene(float now, bool loading)
    {
        string room = RoomScene();
        string active = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(room) || room == active || loading || !PhotonNetwork.AutomaticallySyncScene
            || SceneSyncRules.TrustOwnLoad(ownLoadScene, active, now - ownLoadAt, OwnLoadTrustSeconds))
        {
            mismatchSince = -1f;
            return;
        }
        if (mismatchSince < 0f) mismatchSince = now;
        if (!SceneSyncRules.ShouldFollow(room, active, loading, now - mismatchSince, FollowAfterSeconds)) return;

        mismatchSince = -1f;
        FollowCount++;
        RoomSyncLog.Note($"Stranded in {active} while the room is in {room}. Loading it (master: {PhotonNetwork.IsMasterClient}).");
        PhotonNetwork.LoadLevel(room); // 방장이어도 curScn과 같은 값이라 Props는 다시 쓰지 않고 로컬만 로드한다
    }

    private void RestoreQueueIfStuck(float now, bool loading)
    {
        if (PhotonNetwork.IsMessageQueueRunning || loading) { queuePausedSince = -1f; return; }
        if (queuePausedSince < 0f) queuePausedSince = now;
        if (!SceneSyncRules.ShouldRestoreQueue(false, loading, now - queuePausedSince, RestoreQueueAfterSeconds)) return;

        queuePausedSince = -1f;
        QueueRestoreCount++;
        PhotonNetwork.IsMessageQueueRunning = true;
        RoomSyncLog.Note("Message queue was paused without a scene load. Restored it.");
    }

    // 지금 들어와 있는 씬을 알린다. 응답(캐시 반영) 전에는 같은 값을 다시 보내지 않는다.
    private void PublishLoadedScene(float now, bool loading)
    {
        if (loading) return;
        Player me = PhotonNetwork.LocalPlayer;
        string active = SceneManager.GetActiveScene().name;
        if (me.CustomProperties.TryGetValue(NetKeys.LoadedScene, out object v) && v as string == active) return;
        if (publishedScene == active && now - publishedAt < RepublishSeconds) return;

        publishedScene = active;
        publishedAt = now;
        me.SetCustomProperties(new Hashtable { { NetKeys.LoadedScene, active } });
    }

    public override void OnJoinedRoom()
    {
        publishedScene = null;
        mismatchSince = -1f;
    }

    public override void OnMasterClientSwitched(Player newMasterClient) =>
        RoomSyncLog.Note("Master switched to " + (newMasterClient != null ? newMasterClient.ActorNumber : -1));

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed.ContainsKey(CurrentSceneKey)) RoomSyncLog.Note("Room scene changed to " + (changed[CurrentSceneKey] ?? "-"));
    }
}
