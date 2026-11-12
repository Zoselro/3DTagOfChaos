using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 접착 목소리 녹음기(Request1009Plan.md §25.3 Q6, §25.3.1 R2): 게임 시작 전(대기실) 5초 동안 내 목소리를 녹음해 두고,
// 녹음하자마자 방 안 사람들에게 조각을 나눠 미리 보낸다(§72 E2). 녹음기가 붙은 내 AI가 이 목소리를 3D로 낸다. 녹음하지 않은 사람은 녹음기를 쓸 수 없다(§26 H7).
// 8 kHz 한 채널을 8비트 μ-law로 줄여 약 40 KB, 8 KB 조각으로 보낸다(Photon 이벤트 VoiceClip). 받은 목소리는 방 안에서만, 판이 바뀌면 버린다.
public static class VoiceStore
{
    public const int SampleRate = 8000;
    public const int Seconds = 5;
    private static float[] localSamples;
    private static readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();

    public static bool HasLocal => localSamples != null && localSamples.Length > 0;
    public static float[] LocalSamples => localSamples;

    public static void SetLocal(float[] samples)
    {
        localSamples = samples;
        if (PhotonNetwork.LocalPlayer != null) clips.Remove(PhotonNetwork.LocalPlayer.ActorNumber);
        VoiceNet.OnLocalChanged(); // 대기실에서 바로 조각을 나눠 미리 보낸다(§72 E2)
    }

    public static void SetRemote(int actor, float[] samples)
    {
        if (samples == null || samples.Length == 0) return;
        var clip = AudioClip.Create("Voice_" + actor, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        clips[actor] = clip;
    }

    public static void ClearRemote() => clips.Clear();

    public static void RemoveRemote(int actor) => clips.Remove(actor);

    // 방을 나가면(강퇴 포함) 내 녹음을 지운다(Request1009Plan.md §69 A1) — 새 방에서는 다시 녹음해야 한다.
    // PUN은 나간 뒤에도 내 플레이어 속성을 들고 있다가 다음 방에 그대로 보내므로 HasVoice도 지운다(목록의 녹음 표시가 남던 원인).
    public static void ClearLocal()
    {
        localSamples = null;
        clips.Clear();
        if (PhotonNetwork.LocalPlayer != null) PhotonNetwork.LocalPlayer.CustomProperties.Remove(NetKeys.HasVoice);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void WatchRoomExit() => PhotonNetwork.AddCallbackTarget(new RoomExitWatcher());

    private sealed class RoomExitWatcher : IMatchmakingCallbacks
    {
        public void OnLeftRoom() => ClearLocal();
        public void OnFriendListUpdate(List<FriendInfo> friendList) { }
        public void OnCreatedRoom() { }
        public void OnCreateRoomFailed(short returnCode, string message) { }
        public void OnJoinedRoom() { }
        public void OnJoinRoomFailed(short returnCode, string message) { }
        public void OnJoinRandomFailed(short returnCode, string message) { }
    }

    // 그 사람의 목소리(녹음이 없으면 null — 기본 합성 목소리는 없앴다, §26 H7).
    public static AudioClip ClipFor(int actor)
    {
        if (clips.TryGetValue(actor, out AudioClip clip) && clip != null) return clip;
        if (PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber && HasLocal)
        {
            SetRemote(actor, localSamples);
            return clips[actor];
        }
        return null;
    }

    // μ-law 8비트(순수 계산, 시험용).
    public static byte Encode(float sample)
    {
        const float Mu = 255f;
        float s = Mathf.Clamp(sample, -1f, 1f);
        float m = Mathf.Sign(s) * Mathf.Log(1f + Mu * Mathf.Abs(s)) / Mathf.Log(1f + Mu);
        return (byte)Mathf.Clamp(Mathf.RoundToInt((m + 1f) * 127.5f), 0, 255);
    }

    public static float Decode(byte value)
    {
        const float Mu = 255f;
        float m = value / 127.5f - 1f;
        return Mathf.Sign(m) * (Mathf.Pow(1f + Mu, Mathf.Abs(m)) - 1f) / Mu;
    }
}

// 목소리 조각 주고받기(NetEventCodes.VoiceClip). 내용: [조각 번호, 조각 수, byte[], 녹음 번호]. 받은 쪽은 보낸 사람 번호로 모은다(남의 목소리로 꾸밀 수 없다).
// §72 E2: 예전에는 판 시작 프레임에 40 KB를 한꺼번에 보내 느린 연결(핫스팟)에서 위치 패킷이 밀렸다. 이제 대기실에서 녹음하자마자,
// 또 새로 들어온 사람에게, 조각을 ChunkGap초 간격으로 미리 보낸다. 이미 보낸 사람에게는 다시 보내지 않는다(판 시작 때는 빠진 사람만).
// 받는 쪽은 씬과 무관하게 늘 듣는다(대기실 → 게임 씬 전환 중에 온 조각도 버리지 않는다).
public static class VoiceNet
{
    private const int ChunkBytes = 8000;
    public const float ChunkGap = 0.2f;
    private static readonly Dictionary<int, byte[][]> pending = new Dictionary<int, byte[][]>();
    private static readonly Dictionary<int, int> pendingVersion = new Dictionary<int, int>();
    private static readonly HashSet<int> sentTo = new HashSet<int>();   // 지금 녹음을 다 받은 사람
    private static readonly HashSet<int> waiting = new HashSet<int>();  // 보낼 차례를 기다리는 사람
    private static Job job;
    private static int localVersion;

    public static int ChunksSent { get; private set; } // 시험·로그용

    private sealed class Job
    {
        public byte[] Bytes;
        public int[] Targets;
        public int Version;
        public int Next;
        public float NextAt;
        public int Total => (Bytes.Length + ChunkBytes - 1) / ChunkBytes;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Runner.Instance != null) return;
        var go = new GameObject("VoiceNet");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
    }

    // 내 녹음이 바뀌었다(대기실 녹음 버튼) — 방 안 모두에게 새로 보낸다.
    public static void OnLocalChanged()
    {
        localVersion++;
        sentTo.Clear();
        job = null;
        QueueEveryone();
    }

    // 판 시작(EscapeManager.Start): 아직 못 받은 사람에게만 보낸다(대기실에서 이미 보냈으면 아무것도 안 함).
    public static void EnsureSent() => QueueEveryone();

    private static void QueueEveryone()
    {
        if (!VoiceStore.HasLocal || !PhotonNetwork.InRoom || PhotonNetwork.OfflineMode) return;
        foreach (Player p in PhotonNetwork.PlayerListOthers) Queue(p.ActorNumber);
    }

    private static void Queue(int actor)
    {
        if (actor <= 0 || sentTo.Contains(actor)) return;
        if (job != null && System.Array.IndexOf(job.Targets, actor) >= 0) return;
        waiting.Add(actor);
    }

    private static void Forget(int actor)
    {
        sentTo.Remove(actor);
        waiting.Remove(actor);
        pending.Remove(actor);
        pendingVersion.Remove(actor);
        VoiceStore.RemoveRemote(actor);
    }

    private static void Reset()
    {
        sentTo.Clear();
        waiting.Clear();
        pending.Clear();
        pendingVersion.Clear();
        job = null;
    }

    private static void Tick()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode || !VoiceStore.HasLocal) { job = null; waiting.Clear(); return; }
        if (job == null)
        {
            if (waiting.Count == 0) return;
            var targets = new int[waiting.Count];
            waiting.CopyTo(targets);
            waiting.Clear();
            job = new Job { Bytes = EncodeLocal(), Targets = targets, Version = localVersion, NextAt = Time.unscaledTime };
        }
        if (Time.unscaledTime < job.NextAt) return;
        int total = job.Total;
        int c = job.Next;
        int len = Mathf.Min(ChunkBytes, job.Bytes.Length - c * ChunkBytes);
        var chunk = new byte[len];
        System.Array.Copy(job.Bytes, c * ChunkBytes, chunk, 0, len);
        PhotonNetwork.RaiseEvent(NetEventCodes.VoiceClip, new object[] { c, total, chunk, job.Version },
            new RaiseEventOptions { TargetActors = job.Targets }, SendOptions.SendReliable);
        ChunksSent++;
        job.Next++;
        job.NextAt = Time.unscaledTime + ChunkGap;
        if (job.Next < total) return;
        foreach (int a in job.Targets) sentTo.Add(a);
        Debug.Log($"[VoiceNet] Voice sent to {job.Targets.Length} player(s) in {total} chunks.");
        job = null;
    }

    private static byte[] EncodeLocal()
    {
        float[] samples = VoiceStore.LocalSamples;
        var bytes = new byte[samples.Length];
        for (int i = 0; i < samples.Length; i++) bytes[i] = VoiceStore.Encode(samples[i]);
        return bytes;
    }

    public static void Receive(int sender, object data)
    {
        if (!(data is object[] d) || d.Length < 3 || !(d[0] is int index) || !(d[1] is int total) || !(d[2] is byte[] chunk)) return;
        if (total <= 0 || total > 16 || index < 0 || index >= total || chunk.Length > ChunkBytes) return; // 크기 제한(최대 128 KB)
        int version = d.Length > 3 && d[3] is int v ? v : 0;
        // 새 녹음의 조각이 오면 옛 녹음 조각은 버린다(두 녹음이 섞이지 않게)
        if (!pending.TryGetValue(sender, out byte[][] parts) || parts.Length != total
            || !pendingVersion.TryGetValue(sender, out int had) || had != version)
        {
            pending[sender] = parts = new byte[total][];
            pendingVersion[sender] = version;
        }
        parts[index] = chunk;
        foreach (byte[] p in parts) if (p == null) return;
        int length = 0;
        foreach (byte[] p in parts) length += p.Length;
        var samples = new float[length];
        int k = 0;
        foreach (byte[] p in parts) foreach (byte b in p) samples[k++] = VoiceStore.Decode(b);
        VoiceStore.SetRemote(sender, samples);
        pending.Remove(sender);
        pendingVersion.Remove(sender);
    }

    // 씬과 무관하게 방 안에서 늘 도는 송수신기.
    private sealed class Runner : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public static Runner Instance { get; private set; }

        private void Awake() => Instance = this;

        private void Update() => Tick();

        public void OnEvent(EventData e)
        {
            if (e.Code == NetEventCodes.VoiceClip) Receive(e.Sender, e.CustomData);
        }

        public override void OnJoinedRoom()
        {
            Reset();
            QueueEveryone();
        }

        public override void OnLeftRoom() => Reset();

        public override void OnPlayerEnteredRoom(Player newPlayer) => Queue(newPlayer.ActorNumber);

        public override void OnPlayerLeftRoom(Player otherPlayer) => Forget(otherPlayer.ActorNumber);
    }
}
