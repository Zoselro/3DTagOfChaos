using UnityEngine;

// 캐릭터 하나의 최근 위치 기록(원형 버퍼, Request1009Plan.md §46.2 GK0). 시각은 PhotonNetwork.Time(초).
// 방장이 "괴물이 본 순간"으로 되돌려 잴 때 쓴다 — 네트워크·씬을 모르는 순수 계산이라 EditMode로 시험한다.
public sealed class PositionTrack
{
    private readonly double[] times;
    private readonly Vector3[] points;
    private int head;  // 다음에 쓸 칸
    private int count;

    public PositionTrack(int capacity)
    {
        capacity = Mathf.Max(2, capacity);
        times = new double[capacity];
        points = new Vector3[capacity];
    }

    public int Count => count;
    public double NewestTime => count > 0 ? times[Index(count - 1)] : double.NegativeInfinity;
    public Vector3 NewestPoint => count > 0 ? points[Index(count - 1)] : Vector3.zero;
    public double OldestTime => count > 0 ? times[Index(0)] : double.PositiveInfinity;

    // 오래된 순 i번째(0 = 가장 오래된 것).
    private int Index(int i) => (head - count + i + times.Length * 2) % times.Length;

    public void Clear()
    {
        head = 0;
        count = 0;
    }

    // 시각이 거꾸로 가면(방장 교체 직후 시계 차이 등) 기록을 비우고 새로 시작한다.
    public void Record(double time, Vector3 position)
    {
        if (count > 0 && time < NewestTime) Clear();
        times[head] = time;
        points[head] = position;
        head = (head + 1) % times.Length;
        if (count < times.Length) count++;
    }

    // [from, to] 동안 지나간 길(기록 점 사이를 직선으로 이음)에 point가 가장 가까이 온 거리. 끝점 current(지금 위치)를 덧붙인다.
    // 그 구간에 기록이 하나도 없으면 false(기록이 없는 사람은 옛 규칙으로 잰다).
    public bool TryMinDistance(Vector3 point, double from, double to, Vector3 current, out float distance)
    {
        distance = float.MaxValue;
        if (count == 0 || to < OldestTime) return false;

        bool hasPrev = false;
        Vector3 prev = default;
        for (int i = 0; i < count; i++)
        {
            int k = Index(i);
            double t = times[k];
            Vector3 p = points[k];
            if (t > to) break;
            // 구간 시작 전의 점은 길의 출발점으로만 쓴다(그 다음 점과 잇는 선분이 구간 시작 시각을 지난다).
            if (t >= from) distance = Mathf.Min(distance, hasPrev ? SegmentDistance(point, prev, p) : Vector3.Distance(point, p));
            prev = p;
            hasPrev = true;
        }
        if (!hasPrev) return false;
        distance = Mathf.Min(distance, SegmentDistance(point, prev, current));
        return true;
    }

    public const float MaxLeadSpeed = 60f; // 앞으로 늘릴 때 쓰는 속도 상한(m/s) — 순간이동 기록이 길을 크게 늘리지 않게

    // 마지막 두 기록으로 잰 속도(m/s). 기록이 둘 미만이면 0.
    public Vector3 Velocity
    {
        get
        {
            if (count < 2) return Vector3.zero;
            int a = Index(count - 2), b = Index(count - 1);
            double dt = times[b] - times[a];
            if (dt <= 1e-4) return Vector3.zero;
            return Vector3.ClampMagnitude((points[b] - points[a]) / (float)dt, MaxLeadSpeed);
        }
    }

    // TryMinDistance에 더해, 지금 위치에서 마지막 속도로 lead초 앞까지 늘린 길도 본다(§46 후속 — 방장 화면의 원격 캐릭터는 실제보다
    // 지연 + 보간만큼 늦다. 달리던 괴물이 보낸 "지금 내 위치"는 방장이 아직 그리지 못한 앞쪽이다).
    public bool TryMinDistanceLeading(Vector3 point, double from, double to, Vector3 current, float lead, out float distance)
    {
        if (!TryMinDistance(point, from, to, current, out distance)) return false;
        if (lead > 0f) distance = Mathf.Min(distance, SegmentDistance(point, current, current + Velocity * lead));
        return true;
    }

    public static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float len = ab.sqrMagnitude;
        if (len < 1e-8f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
        return Vector3.Distance(p, a + ab * t);
    }
}
