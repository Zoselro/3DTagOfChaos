using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 방장이 모든 캐릭터(쿠키·괴물)의 위치를 0.05초마다 1.5초분 기록한다(Request1009Plan.md §46.2 GK0).
// 잡기 확정(GrabKillAuthority)이 "괴물이 본 순간"의 위치와 비교할 때 쓴다. 방장만 기록하고, 방장이 되면 그때부터 쌓는다.
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(PositionHistory));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PositionHistory>();
    }

    // 그 캐릭터(ViewID)의 기록. 방장이 아니거나 기록이 없으면 false.
    public static bool TryGet(int viewId, out PositionTrack track)
    {
        track = null;
        return instance != null && PhotonNetwork.IsMasterClient && instance.tracks.TryGetValue(viewId, out track) && track.Count > 0;
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            if (tracks.Count > 0) tracks.Clear(); // 방장이 아니게 되면 버린다(다시 방장이 되면 새로 쌓는다)
            return;
        }
        double now = PhotonNetwork.Time;
        if (now < nextSampleAt && now > nextSampleAt - SampleInterval * 4) return; // 시계가 크게 뒤로 가면 바로 다시 잰다
        nextSampleAt = now + SampleInterval;

        seen.Clear();
        IReadOnlyList<IGameCharacter> all = CharacterRegistry.All;
        for (int i = 0; i < all.Count; i++)
        {
            IGameCharacter c = all[i];
            if (!CharacterRegistry.IsAlive(c) || c.View == null) continue;
            int id = c.View.ViewID;
            if (!tracks.TryGetValue(id, out PositionTrack track)) tracks[id] = track = new PositionTrack(Capacity);
            track.Record(now, c.gameObject.transform.position);
            seen.Add(id);
        }

        if (tracks.Count == seen.Count) return;
        stale.Clear();
        foreach (int id in tracks.Keys)
            if (!seen.Contains(id)) stale.Add(id);
        for (int i = 0; i < stale.Count; i++) tracks.Remove(stale[i]);
    }
}
