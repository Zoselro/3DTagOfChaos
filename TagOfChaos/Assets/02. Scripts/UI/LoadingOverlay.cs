using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 판 시작 로딩 화면(Request1009Plan.md §73 L5). 방이 맵으로 옮겨 가는 동안 화면 전체를 가리고 진행도를 보여 준다 — 예전에는 멈춘
// 대기실 화면이 그대로 남아 "씬 이동이 느림"으로 보였다. 맵에 들어온 뒤 준비 단계에서 아직 안 온 사람이 있으면 위쪽에
// "다른 참가자를 기다리는 중 (3/4)"을 띄운다(방장이 모두 오면 준비 시간을 시작한다, RoomLifecycleWatcher).
public class LoadingOverlay : MonoBehaviour
{
    private const float RefreshSeconds = 0.25f;

    private static LoadingOverlay instance;
    private GameObject fullScreen;
    private TextMeshProUGUI fullText;
    private GameObject banner;
    private TextMeshProUGUI bannerText;
    private float nextRefresh;
    private bool fontApplied;

    public static bool Showing => instance != null && instance.fullScreen != null && instance.fullScreen.activeSelf; // 시험·로그용
    public static bool Waiting => instance != null && instance.banner != null && instance.banner.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(LoadingOverlay), typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 580; // 알림(ScreenToast 600) 아래
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        instance = go.AddComponent<LoadingOverlay>();
        instance.Build();
    }

    private void Build()
    {
        fullScreen = Panel("Loading", transform, Vector2.zero, Vector2.one, Vector2.zero, new Color(0.04f, 0.03f, 0.06f, 1f));
        fullText = Label(fullScreen.transform, 40f);
        banner = Panel("Waiting", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(760f, 56f), new Color(0f, 0f, 0f, 0.6f));
        ((RectTransform)banner.transform).anchoredPosition = new Vector2(0f, -160f);
        bannerText = Label(banner.transform, 26f);
        fullScreen.SetActive(false);
        banner.SetActive(false);
    }

    private static GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, min == max ? 1f : 0.5f);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = min != max; // 전체 화면일 때만 클릭을 막는다
        return go;
    }

    private static TextMeshProUGUI Label(Transform parent, float size)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + RefreshSeconds;
        EscapeTextsSO texts = EscapeTextsSO.Current;

        bool loading = PhotonNetwork.InRoom && PunSceneLoad.IsBusy;
        if (loading)
        {
            ApplyFont();
            AsyncOperation op = PunSceneLoad.Current;
            int percent = op != null ? Mathf.RoundToInt(Mathf.Clamp01(op.progress / 0.9f) * 100f) : 0;
            fullText.text = string.Format(texts.loadingMap, percent);
        }
        if (fullScreen.activeSelf != loading) fullScreen.SetActive(loading);

        int arrived = 0, total = 0;
        bool waiting = !loading && TryCountArrivals(out arrived, out total) && arrived < total;
        if (waiting)
        {
            ApplyFont();
            bannerText.text = string.Format(texts.waitingPlayers, arrived, total);
        }
        if (banner.activeSelf != waiting) banner.SetActive(waiting);
    }

    // 맵(대기실·로비가 아닌 방 씬)의 준비 단계에서 몇 명이 들어왔는지. 기다림이 끝났으면(MaxArrivalWait) false.
    private static bool TryCountArrivals(out int arrived, out int total)
    {
        arrived = total = 0;
        if (!PhotonNetwork.InRoom || GamePhaseState.Current != GamePhase.Prepare) return false;
        string map = SceneManager.GetActiveScene().name;
        if (map == SceneNames.GameLobby || map == SceneNames.Lobby || map != RoomSceneFollower.RoomScene()) return false;
        if (Time.timeSinceLevelLoad >= RoomLifecycleWatcher.MaxArrivalWait) return false;
        foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
        {
            total++;
            if (RoomState.LoadedScene(p) == map) arrived++;
        }
        return true;
    }

    // 씬에서 쓰는 글꼴(한글 포함)을 따른다(ScreenToast와 같은 방식).
    private void ApplyFont()
    {
        if (fontApplied) return;
        TMP_Text sample = FindFirstObjectByType<TMP_Text>();
        if (sample == null || sample == fullText || sample == bannerText || sample.font == null) return;
        fullText.font = bannerText.font = sample.font;
        fontApplied = true;
    }
}
