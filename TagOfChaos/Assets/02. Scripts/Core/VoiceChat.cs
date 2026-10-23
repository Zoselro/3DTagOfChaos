using System.Collections.Generic;
using Photon.Pun;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

// 거리 음성 채팅(Request1009Plan.md §26 H11). VoiceChatSettingsSO가 켠 씬(지금은 GameLobbyScene)에 들어가면 Photon Voice 클라이언트를 만들어
// PUN 방과 같은 음성 방에 들어가고, 씬을 나가면 없앤다. 말하는 사람마다 Speaker(3D 소리)가 생기고 매 프레임 그 사람 캐릭터 머리 위치로 옮긴다
// — 음성 방의 번호는 PUN 번호와 달라서, 음성 방 닉네임에 PUN 번호를 넣어 짝을 찾는다.
// 보내기: 누르면서 말하기(키 = KeyBindings.PushToTalk) / 항상 말하기(VoiceChatSettings). 녹음기(VoiceRecorderPanel)가 녹음하는 동안은
// 마이크를 놓고 보내지 않으며, 들어오는 음성도 끈다(같은 마이크를 두 곳에서 열 수 없고, 남의 목소리가 녹음에 섞이지 않게).
public class VoiceChat : MonoBehaviour
{
    private static VoiceChat instance;
    private PunVoiceClient client;
    private Recorder recorder;
    private GameObject speakerTemplate;
    private readonly List<Speaker> speakers = new List<Speaker>();
    private bool pausedForRecording;

    public static bool IsActive => instance != null;
    // 이 클라이언트가 지금 마이크를 쓰고 있는지(설정 창 소리 막대가 이 값을 쓴다).
    public static bool IsCapturing => instance != null && instance.recorder != null && instance.recorder.RecordingEnabled;
    public static float InputLevel => IsCapturing && instance.recorder.LevelMeter != null ? instance.recorder.LevelMeter.CurrentPeakAmp : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        bool wanted = VoiceChatSettingsSO.Current.IsEnabledIn(scene.name);
        if (!wanted && instance != null) { Destroy(instance.gameObject); instance = null; }
        if (wanted && instance == null) new GameObject(nameof(VoiceChat)).AddComponent<VoiceChat>();
    }

    private void Awake()
    {
        instance = this;
        VoiceChatSettingsSO settings = VoiceChatSettingsSO.Current;

        speakerTemplate = new GameObject("SpeakerTemplate");
        speakerTemplate.transform.SetParent(transform, false);
        var source = speakerTemplate.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = settings.FullVolumeDistance;
        source.maxDistance = settings.SilentDistance;
        source.dopplerLevel = 0f;
        source.playOnAwake = false;
        speakerTemplate.AddComponent<Speaker>();

        recorder = gameObject.AddComponent<Recorder>();
        recorder.MicrophoneType = Recorder.MicType.Unity;
        recorder.SamplingRate = POpusCodec.Enums.SamplingRate.Sampling48000; // 대부분의 마이크가 48 kHz — 다시 맞추기(resample) 경고를 피한다
        recorder.TransmitEnabled = false;
        recorder.RecordWhenJoined = true;
        ApplyDevice();

        client = gameObject.AddComponent<PunVoiceClient>();
        typeof(VoiceConnection).GetField("usePrimaryRecorder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(client, true); // 캐릭터마다 PhotonVoiceView를 두지 않고 하나의 녹음기를 쓴다
        client.PrimaryRecorder = recorder;
        client.SpeakerPrefab = speakerTemplate;
        client.SpeakerLinked += OnSpeakerLinked;
        if (PhotonNetwork.LocalPlayer != null) client.Client.LocalPlayer.NickName = PhotonNetwork.LocalPlayer.ActorNumber.ToString();
        InputDevices.Changed += ApplyDevice;
    }

    private void OnDestroy()
    {
        InputDevices.Changed -= ApplyDevice;
        if (client != null) client.SpeakerLinked -= OnSpeakerLinked;
        if (instance == this) instance = null;
    }

    private void ApplyDevice()
    {
        string device = InputDevices.Selected;
        if (recorder == null || device == null) return;
        recorder.MicrophoneDevice = new Photon.Voice.DeviceInfo(device, null);
    }

    private void OnSpeakerLinked(Speaker speaker)
    {
        speaker.gameObject.SetActive(true);
        speakers.Add(speaker);
    }

    private void Update()
    {
        if (recorder == null) return;
        bool recording = VoiceRecorderPanel.IsRecording;
        if (recording != pausedForRecording)
        {
            pausedForRecording = recording;
            recorder.RecordingEnabled = !recording; // 녹음하는 동안 마이크를 녹음기에 넘긴다
        }
        bool transmit = VoiceChatSettings.ShouldTransmit(VoiceChatSettings.Mode, PlayerInput.PushToTalkHeld, recording);
        if (recorder.TransmitEnabled != transmit) recorder.TransmitEnabled = transmit;
        FollowSpeakers(recording);
    }

    // 말하는 사람의 소리를 그 사람 캐릭터 머리에 둔다. 캐릭터가 없으면(들어오는 중) 그 자리에 둔다.
    // 녹음하는 동안은 들어오는 음성 채팅도 끈다 — 스피커로 나온 남의 목소리가 내 녹음에 섞이지 않게.
    private void FollowSpeakers(bool muted)
    {
        float mouth = VoiceChatSettingsSO.Current.MouthHeight;
        for (int i = speakers.Count - 1; i >= 0; i--)
        {
            Speaker s = speakers[i];
            if (s == null) { speakers.RemoveAt(i); continue; }
            if (s.TryGetComponent(out AudioSource source) && source.mute != muted) source.mute = muted;
            if (s.RemoteVoice == null) continue;
            int actor = PunActorOf(s.RemoteVoice.PlayerId);
            Transform body = BodyOf(actor);
            if (body != null) s.transform.position = body.position + Vector3.up * mouth;
        }
    }

    private int PunActorOf(int voicePlayerId)
    {
        var room = client != null ? client.Client.CurrentRoom : null;
        var p = room != null ? room.GetPlayer(voicePlayerId) : null;
        return p != null && int.TryParse(p.NickName, out int actor) ? actor : -1;
    }

    private static Transform BodyOf(int actor)
    {
        if (actor < 0) return null;
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (CharacterRegistry.IsAlive(c) && c.View != null && c.View.Owner != null && c.View.Owner.ActorNumber == actor) return c.gameObject.transform;
        return null;
    }
}
