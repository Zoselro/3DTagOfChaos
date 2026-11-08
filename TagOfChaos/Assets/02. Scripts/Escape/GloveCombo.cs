using System.Collections.Generic;

// 권투 글러브 연타(Request1009Plan.md §68 GP3) — 방장이 센다. 한 대 = 멈칫(FlinchSeconds), 같은 사람이 WindowSeconds 안에
// HitsToKnock대를 맞으면 쓰러져 KnockSeconds 기절(그 뒤 다시 처음부터 센다).
public sealed class GloveCombo
{
    public const int HitsToKnock = 3;
    public const double WindowSeconds = 3.0;
    public const float FlinchSeconds = 0.2f;
    public const float KnockSeconds = 3f;

    private readonly Dictionary<int, List<double>> hits = new Dictionary<int, List<double>>();

    // victim(ViewID)이 now에 한 대 맞았다. 쓰러지면 true.
    public bool Register(int victim, double now)
    {
        if (!hits.TryGetValue(victim, out List<double> list)) hits[victim] = list = new List<double>(HitsToKnock);
        list.RemoveAll(t => now - t > WindowSeconds);
        list.Add(now);
        if (list.Count < HitsToKnock) return false;
        list.Clear();
        return true;
    }

    public int RecentHits(int victim, double now)
    {
        if (!hits.TryGetValue(victim, out List<double> list)) return 0;
        int n = 0;
        foreach (double t in list) if (now - t <= WindowSeconds) n++;
        return n;
    }
}
