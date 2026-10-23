using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// 대기실에서 기다리는 괴물을 위한 "맵에서 온 것 보관"(Request1009Plan.md §21.6, 결정 Q2-D).
// 예전에는 괴물이 기다리는 동안 메시지 큐를 통째로 멈춰, 쿠키들이 맵에서 만든 캐릭터와 색칠을 맵 도착 후 재생했다 — 그래서 함께
// 기다리는 다른 괴물의 움직임도 받지 못했다. 이제 큐는 돌리고, 큐 멈춤이 하던 일만 골라 대신한다.
// - 보관 중에 남이 만든 캐릭터(PhotonNetwork.Instantiate)는 꺼진 보관 루트 아래에 만든다 — ViewID는 등록되지만 Awake·Update가 돌지 않아
//   대기실에 나타나지 않고, 씬이 바뀌어도 사라지지 않는다.
// - PUN은 꺼진 컴포넌트에도 동기화 값(OnPhotonSerializeView)을 넣으므로, 보관 중에는 관찰 목록을 기록기 하나로 바꿔 마지막 값만 저장한다.
// - 색칠 이벤트는 받은 시각의 색칠 시간 판정과 함께 모아 둔다.
// 괴물이 맵 씬을 다 불러온 순간(PUN이 큐를 다시 돌리기 전) 보관한 캐릭터를 맵으로 옮겨 켜고, 저장한 값과 색칠을 순서대로 적용한다.
public sealed class NetHold : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private static NetHold instance;

    private Transform holdRoot;
    private bool holding;
    private readonly List<GameObject> parked = new List<GameObject>();
    private readonly List<HeldView> heldViews = new List<HeldView>();
    private readonly List<HeldPaint> heldPaint = new List<HeldPaint>();

    private struct HeldView
    {
        public PhotonView View;
        public List<Component> Observed;
        public HeldSyncRecorder Recorder;
    }

    private struct HeldPaint
    {
        public byte Code;
        public int Sender;
        public object[] Data;
        public bool InPaintWindow;
    }

    public static bool IsHolding => instance != null && instance.holding;
    public static int ParkedCount => instance != null ? instance.parked.Count : 0; // 시험·로그용

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(NetHold));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<NetHold>();
        var root = new GameObject("HeldNetworkObjects");
        root.SetActive(false); // 이 아래 오브젝트는 Awake·Update가 돌지 않는다
        root.transform.SetParent(go.transform, false);
        instance.holdRoot = root.transform;
        if (!(PhotonNetwork.PrefabPool is HoldingPool)) PhotonNetwork.PrefabPool = new HoldingPool(PhotonNetwork.PrefabPool);
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
    }

    // 괴물이 대기실에서 기다리기 시작할 때(MonsterLobbyWaitController). 다음 씬(맵)을 다 불러오면 저절로 꺼낸다.
    public static void Begin()
    {
        if (instance == null) return;
        instance.DiscardHeld();
        instance.holding = true;
        Debug.Log("[NetHold] Holding network objects from the map until this client loads it.");
    }

    // 맵으로 가지 않게 됐을 때(갈 맵이 없음 등) 보관한 것을 버린다.
    public static void Discard()
    {
        if (instance == null) return;
        instance.holding = false;
        instance.DiscardHeld();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (holding) Release(scene);
    }

    public override void OnLeftRoom()
    {
        if (!holding) return;
        holding = false;
        DiscardHeld();
    }

    // ---------------- 보관 ----------------

    private void Park(GameObject go)
    {
        go.transform.SetParent(holdRoot, true);
        parked.Add(go);
        foreach (PhotonView view in go.GetComponentsInChildren<PhotonView>(true))
        {
            var recorder = view.gameObject.AddComponent<HeldSyncRecorder>();
            heldViews.Add(new HeldView
            {
                View = view,
                Observed = view.ObservedComponents != null ? new List<Component>(view.ObservedComponents) : null,
                Recorder = recorder,
            });
            view.ObservedComponents = new List<Component> { recorder };
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (!holding || (photonEvent.Code != NetEventCodes.PaintStroke && photonEvent.Code != NetEventCodes.ClearColor)) return;
        if (!(photonEvent.CustomData is object[] data)) return;
        heldPaint.Add(new HeldPaint
        {
            Code = photonEvent.Code,
            Sender = photonEvent.Sender,
            Data = data,
            InPaintWindow = PlayerPaintCanvas.InPaintWindowNow(), // 받은 시각 기준 — 맵 로딩이 길어도 버려지지 않는다
        });
    }

    // ---------------- 꺼내기 ----------------

    private void Release(Scene scene)
    {
        holding = false;
        int objects = 0;
        foreach (GameObject go in parked)
        {
            if (go == null) continue; // 보관 중 주인이 나가 PUN이 지웠다
            go.SetActive(false);
            go.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(go, scene);
            objects++;
        }
        foreach (HeldView h in heldViews)
            if (h.View != null) h.View.ObservedComponents = h.Observed;
        foreach (GameObject go in parked)
            if (go != null) go.SetActive(true); // 여기서 Awake·OnEnable(이벤트 등록)이 돈다

        int synced = 0;
        foreach (HeldView h in heldViews)
        {
            if (h.View == null || h.Recorder == null) continue;
            if (h.Recorder.Data != null)
            {
                try
                {
                    h.View.DeserializeView(new PhotonStream(false, h.Recorder.Data), h.Recorder.Info);
                    synced++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[NetHold] Could not apply the held state of view {h.View.ViewID}: {e.Message}");
                }
            }
            Destroy(h.Recorder);
        }

        int strokes = 0;
        foreach (HeldPaint p in heldPaint)
        {
            if (p.Data.Length == 0 || !(p.Data[0] is int viewId)) continue;
            PhotonView view = PhotonView.Find(viewId);
            PlayerPaintCanvas canvas = view != null ? view.GetComponent<PlayerPaintCanvas>() : null;
            if (canvas == null) continue;
            canvas.ApplyRemote(p.Code, p.Sender, p.Data, p.InPaintWindow);
            strokes++;
        }

        Debug.Log($"[NetHold] Released into {scene.name}: {objects} object(s), {synced} synced state(s), {strokes} paint event(s).");
        parked.Clear();
        heldViews.Clear();
        heldPaint.Clear();
    }

    private void DiscardHeld()
    {
        foreach (GameObject go in parked)
            if (go != null) Destroy(go);
        parked.Clear();
        heldViews.Clear();
        heldPaint.Clear();
    }

    // ---------------- PUN 프리팹 풀 ----------------

    // 보관 중에는 네트워크로 만들어지는 오브젝트를 보관 루트 아래에 둔다. 보관 중에 이 클라이언트가 직접 만드는 오브젝트는 없다
    // (괴물은 맵에 도착한 뒤 MonsterJoinController가 만든다 — 꺼내기는 씬 로드 순간이라 그보다 먼저 끝난다).
    private sealed class HoldingPool : IPunPrefabPool
    {
        private readonly IPunPrefabPool inner;

        public HoldingPool(IPunPrefabPool inner) { this.inner = inner; }

        public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
        {
            GameObject go = inner.Instantiate(prefabId, position, rotation);
            if (go != null && IsHolding) instance.Park(go);
            return go;
        }

        public void Destroy(GameObject gameObject) => inner.Destroy(gameObject);
    }
}
