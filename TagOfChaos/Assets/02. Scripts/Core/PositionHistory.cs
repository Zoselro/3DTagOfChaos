using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 모든 화면이 모든 캐릭터(쿠키·괴물)의 위치를 1.5초분 기록한다(Request1009Plan.md §46.2 GK0, §70.2.5 D4).
// 잡기 확정(GrabKillAuthority)이 "괴물이 본 순간"의 위치와 비교할 때 쓴다. §72 E5: 예전에는 방장만 기록해, 방장이 바뀌면
// 새 방장의 기록이 비어 있어 바뀐 직후의 판정이 지금 위치로만 이뤄졌다. 이제 모두 쌓아 두므로 바로 이어서 판정한다.
// 내 캐릭터는 0.05초마다 실제 위치를, 남의 캐릭터는 위치 패킷이 올 때마다 "보낸 시각 + 받은 위치"를 적는다 —
// 예전에는 남의 캐릭터도 화면에 그려진(보간된) 위치를 적어, 기록이 실제보다 보간만큼 더 늦었다.
public class PositionHistory : MonoBehaviour
{
    public const float SampleInterval = 0.05f;
    public const float KeepSeconds = 1.5f;
    private const int Capacity = (int)(KeepSeconds / SampleInterval);

    private static PositionHistory instance;
    private readonly Dictionary<int, PositionTrack> tracks = new Dictionary<int, PositionTrack>();
    private readonly List<int> stale = new List<int>();
    private readonly HashSet<int> seen = new HashSet<int>();
    private double nextSampleAt;

    // 재현 도구(§70.2.6): 0 이상이면 내 캐릭터도 "지연 초 + 초당 10번 패킷"으로 받은 것처럼 기록한다(오프라인 한 대에서 원격 술래 흉내).
    public static float DebugRemoteLatency = -1f;
    private const float DebugPacketInterval = 0.1f;
    private readonly Queue<(int id, double time, Vector3 position)> debugPending = new Queue<(int, double, Vector3)>();
    private double debugNextPacketAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(PositionHistory));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PositionHistory>();
    }

    // 그 캐릭터(ViewID)의 기록. 기록이 없으면 false.
    public static bool TryGet(int viewId, out PositionTrack track)
    {
        track = null;
        return instance != null && instance.tracks.TryGetValue(viewId, out track) && track.Count > 0;
    }

    // 그 캐릭터의 기록이 패킷 기준(실제보다 늦음)인지 — 그러면 판정이 기록 끝을 앞으로 늘려 본다.
    public static bool IsRemote(PhotonView view) => view != null && (!view.IsMine || DebugRemoteLatency >= 0f);

    // 남의 캐릭터의 위치 패킷을 받았다(OnPhotonSerializeView). sentAt = 보낸 쪽이 보낸 시각(PhotonMessageInfo.SentServerTime).
    public static void RecordRemote(int viewId, double sentAt, Vector3 position)
    {
        if (instance == null || !PhotonNetwork.InRoom) return;
        instance.TrackOf(viewId).Record(sentAt, position);
    }

    private PositionTrack TrackOf(int id)
    {
        if (!tracks.TryGetValue(id, out PositionTrack track)) tracks[id] = track = new PositionTrack(Capacity);
        return track;
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom)
        {
            if (tracks.Count > 0) tracks.Clear(); // 방을 나가면 버린다
            debugPending.Clear();
            return;
        }
        double now = PhotonNetwork.Time;
        bool simulate = DebugRemoteLatency >= 0f;
        while (debugPending.Count > 0 && (!simulate || debugPending.Peek().time + DebugRemoteLatency <= now))
        {
            (int id, double time, Vector3 position) = debugPending.Dequeue();
            if (simulate) TrackOf(id).Record(time, position);
        }
        bool packetDue = simulate && now >= debugNextPacketAt;
        if (packetDue) debugNextPacketAt = now + DebugPacketInterval;

        bool sampleDue = !(now < nextSampleAt && now > nextSampleAt - SampleInterval * 4); // 시계가 크게 뒤로 가면 바로 다시 잰다
        if (!sampleDue && !packetDue) return;
        if (sampleDue) nextSampleAt = now + SampleInterval;

        seen.Clear();
        IReadOnlyList<IGameCharacter> all = CharacterRegistry.All;
        for (int i = 0; i < all.Count; i++)
        {
            IGameCharacter c = all[i];
            if (!CharacterRegistry.IsAlive(c) || c.View == null) continue;
            int id = c.View.ViewID;
            seen.Add(id);
            if (!c.View.IsMine) continue; // 남의 캐릭터는 RecordRemote가 채운다
            if (simulate)
            {
                if (packetDue) debugPending.Enqueue((id, now, c.gameObject.transform.position));
            }
            else if (sampleDue) TrackOf(id).Record(now, c.gameObject.transform.position);
        }

        if (!sampleDue || tracks.Count <= seen.Count) return;
        stale.Clear();
        foreach (int id in tracks.Keys)
            if (!seen.Contains(id)) stale.Add(id);
        for (int i = 0; i < stale.Count; i++) tracks.Remove(stale[i]);
    }
}
