using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 대기실(GameLobbyScene)의 목소리 녹음(§25.3.1 R2, §26 H11): "목소리 녹음(5초)" — 녹음한 뒤에는 "다시 녹음" — 과 "들어보기" 버튼.
// 판이 시작되기 전까지 몇 번이든 다시 녹음할 수 있고 마지막 녹음을 쓴다. 녹음하면 HasVoice(플레이어 속성)를 켜 목록에 표시되고 녹음기를 쓸 수 있다.
// 마이크는 설정 창에서 고른 장치(InputDevices). 녹음하는 동안 음성 채팅은 마이크를 놓는다(VoiceChat이 IsRecording을 본다) —
// 음성 채팅이 마이크를 다 놓은 뒤 녹음을 시작한다(놓는 순간 Microphone.End가 녹음까지 끊던 문제). 녹음은 마이크 본래 주파수로 하고
// 저장할 때 8 kHz로 줄인다(8 kHz를 지원하지 않는 마이크가 많다).
// 방장이 판을 시작하면 숨긴다(§26 H3). 씬을 고치지 않도록 대기실에 들어올 때 코드로 만든다. 문구는 EscapeTextsSO.
public class VoiceRecorderPanel : MonoBehaviour
{
    private Text recordLabel, listenLabel;
    private Button listenButton;
    private Canvas canvas;
    private string device;
    private AudioClip recording;
    private float recordUntil = -1f;
    private bool preparing;
    private const float ReleaseWaitSeconds = 0.6f;
    private AudioSource preview;

    private static VoiceRecorderPanel instance;
    public static bool IsRecording => instance != null && (instance.recordUntil > 0f || instance.preparing);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneNames.GameLobby || FindFirstObjectByType<VoiceRecorderPanel>() != null) return;
        new GameObject(nameof(VoiceRecorderPanel)).AddComponent<VoiceRecorderPanel>();
    }

    private void Awake()
    {
        instance = this;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();

        recordLabel = NewButton("RecordVoice", new Vector2(24f, 24f), new Vector2(300f, 56f), StartRecording).GetComponentInChildren<Text>();
        listenButton = NewButton("ListenVoice", new Vector2(336f, 24f), new Vector2(170f, 56f), Listen);
        listenLabel = listenButton.GetComponentInChildren<Text>();
        preview = gameObject.AddComponent<AudioSource>();
        preview.playOnAwake = false;
        preview.spatialBlend = 0f;
        ShowIdle();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (recordUntil > 0f && device != null) Microphone.End(device);
    }

    private Button NewButton(string name, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var buttonGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)buttonGo.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        buttonGo.GetComponent<Image>().color = new Color(0.15f, 0.1f, 0.2f, 0.85f);
        var button = buttonGo.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var label = textGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        return button;
    }

    private void ShowIdle()
    {
        EscapeTextsSO t = EscapeTextsSO.Current;
        recordLabel.text = VoiceStore.HasLocal ? t.reRecordVoice : t.recordVoice;
        listenLabel.text = t.listenVoice;
        listenButton.interactable = VoiceStore.HasLocal;
    }

    private void StartRecording()
    {
        if (recordUntil > 0f || preparing) return;
        device = InputDevices.Selected;
        if (device == null)
        {
            recordLabel.text = EscapeTextsSO.Current.noMicrophone;
            return;
        }
        preview.Stop();
        listenButton.interactable = false;
        recordLabel.text = EscapeTextsSO.Current.recording;
        StartCoroutine(RecordWhenMicFree());
    }

    private System.Collections.IEnumerator RecordWhenMicFree()
    {
        preparing = true; // 음성 채팅이 이 값을 보고 마이크를 놓는다
        float until = Time.unscaledTime + ReleaseWaitSeconds;
        yield return null;
        while (VoiceChat.IsCapturing && Time.unscaledTime < until) yield return null;
        yield return new WaitForSecondsRealtime(0.15f); // Photon이 마이크를 닫는 처리가 끝난 뒤 연다
        int rate = RecordRate(device);
        recording = Microphone.Start(device, false, VoiceStore.Seconds, rate);
        recordUntil = Time.unscaledTime + VoiceStore.Seconds;
        preparing = false;
        Debug.Log($"[VoiceRecorder] Recording {VoiceStore.Seconds}s from '{device}' at {rate} Hz.");
    }

    // 마이크가 지원하는 주파수(0,0 = 아무거나)에서 고른다.
    private static int RecordRate(string device)
    {
        Microphone.GetDeviceCaps(device, out int min, out int max);
        if (min == 0 && max == 0) return 44100;
        return Mathf.Clamp(48000, min, max);
    }

    // 마이크 주파수로 녹음한 것을 VoiceStore 주파수(8 kHz)로 줄인다 — 구간 평균(소리가 거칠어지지 않게).
    public static float[] Downsample(float[] source, int sourceRate, int targetRate)
    {
        if (source == null || sourceRate <= targetRate) return source;
        int length = (int)((long)source.Length * targetRate / sourceRate);
        var result = new float[length];
        double step = (double)sourceRate / targetRate;
        for (int i = 0; i < length; i++)
        {
            int from = (int)(i * step), to = Mathf.Min(source.Length, (int)((i + 1) * step));
            float sum = 0f;
            for (int k = from; k < to; k++) sum += source[k];
            result[i] = to > from ? sum / (to - from) : 0f;
        }
        return result;
    }

    private void Listen()
    {
        if (recordUntil > 0f || !VoiceStore.HasLocal || PhotonNetwork.LocalPlayer == null) return;
        AudioClip clip = VoiceStore.ClipFor(PhotonNetwork.LocalPlayer.ActorNumber);
        if (clip == null) return;
        preview.clip = clip;
        preview.Play();
    }

    private void Update()
    {
        // 방장이 판을 시작하면(준비 끝 시각이 기록되면) 버튼을 숨긴다 — 대기실에서만 녹음한다(§26 H3).
        bool started = RoomState.TryGetDouble(NetKeys.PrepareEndTime, out _);
        if (canvas.enabled == started) canvas.enabled = !started;
        if (recordUntil < 0f || Time.unscaledTime < recordUntil) return;
        recordUntil = -1f;
        Microphone.End(device);
        if (recording != null)
        {
            var raw = new float[recording.samples * recording.channels];
            recording.GetData(raw, 0);
            if (recording.channels > 1) raw = Mono(raw, recording.channels);
            float[] samples = Normalize(Downsample(raw, recording.frequency, VoiceStore.SampleRate));
            VoiceStore.SetLocal(samples);
            Debug.Log($"[VoiceRecorder] Recorded {samples.Length} samples, peak {InputDevicePage.Peak(samples):F3}.");
            // 녹음이 있는 사람만 녹음기를 쓸 수 있다 — 방장이 이 값을 본다(§26 H7).
            if (PhotonNetwork.LocalPlayer != null)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { NetKeys.HasVoice, true } });
        }
        ShowIdle();
    }

    // 작게 녹음된 목소리를 키운다: 가장 큰 소리가 TargetPeak가 되도록(최대 MaxGain배). 거의 무음이면 그대로(잡음만 커지지 않게).
    private const float TargetPeak = 0.85f;
    private const float MaxGain = 12f;
    private const float SilencePeak = 0.004f;

    public static float[] Normalize(float[] samples)
    {
        if (samples == null) return null;
        float peak = InputDevicePage.Peak(samples);
        if (peak < SilencePeak) return samples;
        float gain = Mathf.Min(MaxGain, TargetPeak / peak);
        for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
        return samples;
    }

    private static float[] Mono(float[] interleaved, int channels)
    {
        var mono = new float[interleaved.Length / channels];
        for (int i = 0; i < mono.Length; i++) mono[i] = interleaved[i * channels];
        return mono;
    }

    // 자동 검증용: 녹음 없이 이미 녹음한 것처럼(짧은 소리).
    public static void EditorFakeRecording()
    {
        var samples = new float[VoiceStore.SampleRate];
        for (int i = 0; i < samples.Length; i++) samples[i] = 0.2f * Mathf.Sin(i * 0.15f);
        VoiceStore.SetLocal(samples);
        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { NetKeys.HasVoice, true } });
        if (instance != null) instance.ShowIdle();
    }
}
