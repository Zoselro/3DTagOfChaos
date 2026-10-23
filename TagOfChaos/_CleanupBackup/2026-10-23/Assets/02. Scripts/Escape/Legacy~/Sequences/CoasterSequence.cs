using System.Collections.Generic;
using UnityEngine;

// 놀이공원 탈출 장치: 저주받은 롤러코스터(EscapeVisualPlan.md §5.2).
//   완성 0~3초: 아치 전구가 차례로 켜지고, 차가 부르르 떨며 바퀴에서 불꽃이 튄다 → 탈 수 있음(전구는 계속 깜빡임)
//   탑승      : 쿠키 인형이 빈 좌석으로 폴짝 뛰어 앉는다(탄 사람 수만큼 좌석에 보인다)
//   출발      : 안전바가 내려오고, 차가 레일(Path_nn)을 따라 가속하며 오르막을 올라 맵 밖으로 달려 나간다
// 모델 이름 규칙은 escape_devices.py(device_carnival)와 같다.
public class CoasterSequence : EscapeSequence
{
    private const float StartupSeconds = 3f;
    private const float BarSeconds = 0.8f;
    private const float RollStart = 1f;          // 안전바가 내려온 뒤 출발
    private const float Acceleration = 5f;
    private const float BarRaisedAngle = -95f;
    private const float HopSeconds = 0.7f;
    private const float SeatedScale = 0.7f;

    private readonly List<Transform> cars = new List<Transform>();
    private readonly List<float> carRest = new List<float>();     // 차마다 레일 위 정지 위치(경로 길이)
    private readonly List<Transform> lapBars = new List<Transform>();
    private readonly List<Transform> bulbs = new List<Transform>();
    private readonly List<Transform> seats = new List<Transform>();
    private readonly List<GameObject> seated = new List<GameObject>();
    private readonly List<float> seatReadyAt = new List<float>();
    private Vector3[] path = new Vector3[0];
    private float[] pathLength = new float[0];
    private ParticleSystem sparks;
    private int hopsPlayed;

    public override float BoardReadySeconds => StartupSeconds + 0.2f;
    public override float DepartureSeconds => 7f;
    public override Transform DepartureFocus => cars.Count > 0 ? cars[0] : BoardPoint;

    private void Awake()
    {
        var points = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Path_")) points.Add(child);
            else if (child.name.StartsWith("Bulb_")) bulbs.Add(child);
            else if (child.name.StartsWith("Car_")) cars.Add(child);
        }
        points.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        bulbs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        cars.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        path = new Vector3[points.Count];
        pathLength = new float[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            path[i] = points[i].localPosition;
            pathLength[i] = i == 0 ? 0f : pathLength[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        }

        foreach (Transform car in cars)
        {
            carRest.Add(ProjectOnPath(car.localPosition));
            foreach (Transform child in car)
                if (child.name.StartsWith("LapBar_")) lapBars.Add(child);
        }
        for (int i = 0; ; i++)
        {
            Transform seat = FindDeep(transform, $"Seat_{i:00}");
            if (seat == null) break;
            seats.Add(seat);
        }

        // 소리(SoundPlan.md S6): 전구 딸깍딸깍 + 모터 시동 + 바퀴 불꽃 / 출발: 안전바 철컥 → 덜컹덜컹 달려 나감(앞 차를 따라감)
        Transform front = cars.Count > 0 ? cars[0] : null;
        AddCue(SoundId.CoasterBulbOn, CueClock.Completed, 0f, bulbs.Count > 0 ? bulbs[bulbs.Count / 2] : null);
        AddCue(SoundId.CoasterStartup, CueClock.Completed, 0f, front);
        AddCue(SoundId.CoasterSparks, CueClock.Completed, 0.3f, cars.Count > 0 ? cars[cars.Count / 2] : null);
        AddCue(SoundId.CoasterLapBar, CueClock.Departed, 0f, front);
        AddCue(SoundId.CoasterDepart, CueClock.Departed, RollStart, front);
    }

    public override void Tick(EscapeState state, double now)
    {
        float c = state != null ? Since(state.CompletedAt, now) : -1f;
        float d = state != null ? Since(state.DepartedAt, now) : -1f;

        // 전구: 완성 전엔 꺼짐, 시동 중 차례로 켜짐, 그 뒤 흐르듯 깜빡임
        for (int i = 0; i < bulbs.Count; i++)
        {
            bool on;
            if (c < 0f) on = false;
            else if (c < StartupSeconds) on = c >= (StartupSeconds - 0.6f) * i / Mathf.Max(1, bulbs.Count);
            else on = ((int)(Time.time * 5f) + i) % 3 != 0;
            if (bulbs[i].gameObject.activeSelf != on) bulbs[i].gameObject.SetActive(on);
        }

        // 시동: 떨림 + 불꽃
        bool starting = c >= 0f && c < StartupSeconds && d < 0f;
        UpdateSparks(starting && c > 0.3f);

        // 출발: 안전바 → 가속하며 경로를 따라 달림
        float bar = d >= 0f ? Ease(d / BarSeconds) : 0f;
        foreach (Transform lapBar in lapBars) lapBar.localRotation = Quaternion.Euler(Mathf.Lerp(BarRaisedAngle, 0f, bar), 0f, 0f);
        float rolled = d >= RollStart ? 0.5f * Acceleration * (d - RollStart) * (d - RollStart) : 0f;
        bool gone = d >= DepartureSeconds;
        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i].gameObject.activeSelf == gone) cars[i].gameObject.SetActive(!gone);
            if (gone) continue;
            PlaceOnPath(cars[i], carRest[i] + rolled);
            if (starting) cars[i].localPosition += Shake(0.035f * (1f - c / StartupSeconds));
        }

        UpdatePassengers(state != null ? state.Waiting.Count : 0);
    }

    public override void OnCookieBoarded(Vector3 from)
    {
        int seat = Mathf.Min(hopsPlayed, seats.Count - 1);
        hopsPlayed++;
        if (seat < 0) { base.OnCookieBoarded(from); return; }
        while (seatReadyAt.Count <= seat) seatReadyAt.Add(0f);
        seatReadyAt[seat] = Time.time + HopSeconds;
        BoardingFx.Play(from, seats[seat].position, BoardingFx.Style.Hop);
    }

    // 탄 쿠키 수만큼 좌석에 인형을 앉힌다(폴짝 뛰는 연출이 끝난 뒤). 늦게 들어온 사람에게는 바로 보인다.
    private void UpdatePassengers(int count)
    {
        count = Mathf.Min(count, seats.Count);
        if (count == 0) hopsPlayed = 0;
        for (int i = 0; i < seats.Count; i++)
        {
            bool show = i < count && (i >= seatReadyAt.Count || Time.time >= seatReadyAt[i]);
            while (seated.Count <= i) seated.Add(null);
            if (show && seated[i] == null) seated[i] = CreatePassenger(seats[i]);
            else if (!show && seated[i] != null) { Destroy(seated[i]); seated[i] = null; }
        }
    }

    private static GameObject CreatePassenger(Transform seat) => EscapeVisuals.CreatePassenger(seat, SeatedScale);

    // ---------------- path ----------------

    private void PlaceOnPath(Transform car, float s)
    {
        if (path.Length < 2) return;
        int i = SegmentAt(s);
        float t = Mathf.InverseLerp(pathLength[i], pathLength[i + 1], s);
        Vector3 dir = path[i + 1] - path[i];
        car.localPosition = Vector3.Lerp(path[i], path[i + 1], t) + (s > pathLength[path.Length - 1] ? dir.normalized * (s - pathLength[path.Length - 1]) : Vector3.zero);
        if (dir.sqrMagnitude > 1e-4f) car.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private int SegmentAt(float s)
    {
        for (int i = 0; i < path.Length - 2; i++)
            if (s < pathLength[i + 1]) return i;
        return path.Length - 2;
    }

    private float ProjectOnPath(Vector3 p)
    {
        float best = 0f, bestDist = float.MaxValue;
        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector3 a = path[i], ab = path[i + 1] - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            float dist = (a + ab * t - p).sqrMagnitude;
            if (dist < bestDist) { bestDist = dist; best = pathLength[i] + ab.magnitude * t; }
        }
        return best;
    }

    // ---------------- fx ----------------

    private void UpdateSparks(bool on)
    {
        if (on && sparks == null) sparks = CreateSparks();
        if (sparks == null) return;
        if (on && !sparks.isEmitting) sparks.Play();
        else if (!on && sparks.isEmitting) sparks.Stop();
    }

    // 바퀴 아래에서 튀는 주황 불꽃(코드로 만든 파티클).
    private ParticleSystem CreateSparks()
    {
        ParticleSystem ps = EscapeVisuals.CreateParticles("StartupSparks", cars.Count > 0 ? cars[cars.Count / 2] : transform, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(0.5f, 0.5f), Speed = new Vector2(3f, 6f), Size = new Vector2(0.08f, 0.18f),
            Color = new Color(1f, 0.7f, 0.25f, 1f), Gravity = 1.5f, MaxParticles = 200, Rate = 90f,
            Shape = ParticleSystemShapeType.Box, BoxScale = new Vector3(1.6f, 6.5f, 0.1f),
            FadeFrom = new Color(1f, 0.9f, 0.5f), FadeTo = new Color(1f, 0.4f, 0.1f), AlphaFrom = 1f,
        });
        ps.transform.localPosition = Vector3.up * 0.2f;
        ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        return ps;
    }

    private static Vector3 Shake(float amount)
    {
        Vector3 v = Random.insideUnitSphere * amount;
        v.y = Mathf.Abs(v.y);
        return v;
    }
}
