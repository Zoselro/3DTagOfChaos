using UnityEngine;

// 롤러코스터 레일 가운데 선과 한 바퀴 시간표(Request1009Plan.md §31). 에디터 도구 CoasterPathBaker가 레일 메시에서 구워 넣는다.
// 점·위쪽은 트랙 루트 기준 로컬 좌표, 닫힌 고리(마지막 점 다음은 첫 점). 시간표는 같은 점마다 그 점을 지나는 시각(한 바퀴 안).
// 실행 중에는 표를 찾기만 해서 프레임 비용이 거의 없고, 같은 시각이면 모든 화면이 같은 자리를 계산한다.
[CreateAssetMenu(menuName = "TagOfChaos/Maps/Coaster Path", fileName = "CoasterPath")]
public sealed class CoasterPathSO : ScriptableObject
{
    [SerializeField] private Vector3[] points = new Vector3[0];
    [SerializeField] private Vector3[] ups = new Vector3[0];
    [SerializeField] private float[] distances = new float[0]; // 누적 길이(distances[0] = 0)
    [SerializeField] private float[] times = new float[0];     // 그 점을 지나는 시각(times[0] = 0)
    [SerializeField] private bool[] chain = new bool[0];       // 체인/부스터가 끄는 구간
    [SerializeField] private float length;
    [SerializeField] private float lapTime;
    [SerializeField] private float gauge;
    [SerializeField] private float railTop;                    // 가운데 선에서 레일 윗면까지(위쪽 방향)
    [SerializeField] private float secondTrainOffset = 0.25f;  // 두 번째 편성이 앞서 가는 시간(한 바퀴 비율) — 공용 구간에서 겹치지 않게

    public int Count => points.Length;
    public float Length => length;
    public float LapTime => lapTime;
    public float RailTop => railTop;
    public float SecondTrainOffset => secondTrainOffset;
    public bool IsValid => points.Length >= 4 && length > 0f && lapTime > 0f;

    public Vector3 Point(int i) => points[i];
    public Vector3 Up(int i) => ups[i];
    public float TimeAt(int i) => times[i];

    // s(m, 한 바퀴로 감음)에서의 자리·진행 방향·위쪽(로컬). 점 사이는 Hermite(접선 = 이웃 점 차), 위쪽은 Slerp 후 진행 방향에 직교화.
    public void Sample(float s, out Vector3 position, out Vector3 forward, out Vector3 up)
    {
        int n = points.Length;
        s = Wrap(s, length);
        int i = Segment(distances, s, n);
        int j = (i + 1) % n;
        float d0 = distances[i], d1 = j == 0 ? length : distances[j];
        float seg = Mathf.Max(d1 - d0, 1e-4f);
        float u = Mathf.Clamp01((s - d0) / seg);

        Vector3 p0 = points[i], p1 = points[j];
        Vector3 m0 = (points[j] - points[(i - 1 + n) % n]) * 0.5f;
        Vector3 m1 = (points[(j + 1) % n] - points[i]) * 0.5f;
        float u2 = u * u, u3 = u2 * u;
        position = (2f * u3 - 3f * u2 + 1f) * p0 + (u3 - 2f * u2 + u) * m0 + (-2f * u3 + 3f * u2) * p1 + (u3 - u2) * m1;
        Vector3 tangent = (6f * u2 - 6f * u) * p0 + (3f * u2 - 4f * u + 1f) * m0 + (-6f * u2 + 6f * u) * p1 + (3f * u2 - 2f * u) * m1;
        forward = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : (p1 - p0).normalized;
        Vector3 rawUp = Vector3.Slerp(ups[i], ups[j], u);
        up = Vector3.ProjectOnPlane(rawUp, forward).normalized;
    }

    // 시각 t(초, 한 바퀴로 감음)에 열차 기준점이 있는 거리. 시간표 점 사이(0.5 m)는 일정 속도로 본다.
    public float DistanceAtTime(double t)
    {
        int n = times.Length;
        float tt = (float)Wrap(t, lapTime);
        int i = Segment(times, tt, n);
        int j = (i + 1) % n;
        float t0 = times[i], t1 = j == 0 ? lapTime : times[j];
        float d0 = distances[i], d1 = j == 0 ? length : distances[j];
        float span = Mathf.Max(t1 - t0, 1e-4f);
        return d0 + (d1 - d0) * Mathf.Clamp01((tt - t0) / span);
    }

    // 거리 s에서의 속도(m/s) — 소리 크기용.
    public float SpeedAt(float s)
    {
        int n = points.Length;
        s = Wrap(s, length);
        int i = Segment(distances, s, n);
        int j = (i + 1) % n;
        float dt = (j == 0 ? lapTime : times[j]) - times[i];
        float ds = (j == 0 ? length : distances[j]) - distances[i];
        return dt > 1e-4f ? ds / dt : 0f;
    }

    public bool IsChainAt(float s)
    {
        int i = Segment(distances, Wrap(s, length), points.Length);
        return chain.Length == points.Length && chain[i];
    }

    // 가장 큰 a[k] <= value 인 k(a는 0부터 늘어남).
    private static int Segment(float[] a, float value, int n)
    {
        int lo = 0, hi = n - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (a[mid] <= value) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    private static float Wrap(float v, float period) => period > 0f ? v - Mathf.Floor(v / period) * period : 0f;
    private static double Wrap(double v, float period) => period > 0f ? v - System.Math.Floor(v / period) * period : 0d;

#if UNITY_EDITOR
    public void EditorSet(Vector3[] newPoints, Vector3[] newUps, float[] newDistances, float newLength, float[] newTimes, bool[] newChain,
        float newLapTime, float newGauge, float newRailTop, float newSecondTrainOffset)
    {
        points = newPoints; ups = newUps; distances = newDistances; length = newLength;
        times = newTimes; chain = newChain; lapTime = newLapTime; gauge = newGauge; railTop = newRailTop;
        secondTrainOffset = newSecondTrainOffset;
    }
#endif
}
