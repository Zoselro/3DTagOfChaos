using System.Collections.Generic;
using UnityEngine;

// 공장 탈출 장치: 초콜릿 증기 기차(EscapeVisualPlan.md §5.4).
//   완성 0~3초: 굴뚝 연기 퍼프가 점점 빨라지고, 바퀴가 조금씩 돌며, 기적 불빛이 깜빡인다 → 탈 수 있음
//   탑승      : 쿠키 인형이 객차 문으로 폴짝 뛰어 들어가 창가 좌석에 앉는다(탄 사람 수만큼 창문에 보인다)
//   출발      : 기적 불빛이 크게 번쩍이고, 기차가 천천히 가속하며 레일을 따라 맵 가장자리의 터널(Tunnel) 속으로 사라진다
// 바퀴·기어는 칸 자리(Slot_00~03, 06~07)를 축 방향으로 돌려 끼운 재료가 함께 돈다. 모델 이름 규칙은 escape_devices.py(device_factory)와 같다.
public class TrainSequence : EscapeSequence
{
    private const float StartupSeconds = 3f;
    private const float WhistleSeconds = 0.8f;
    private const float Acceleration = 3f;
    private const float WheelRadius = 0.27f;
    private const float HopSeconds = 0.7f;
    private const float SeatedScale = 0.62f;
    private static readonly int[] SpinSlots = { 0, 1, 2, 3, 6, 7 };

    private readonly List<Transform> cars = new List<Transform>();
    private readonly List<Vector3> carHome = new List<Vector3>();
    private readonly List<float> carRear = new List<float>();    // 차 중심에서 뒤끝까지(로컬 z)
    private float portal = float.MaxValue;                        // 터널 입구(장치 로컬 z). 차 뒤끝이 지나면 숨긴다
    private readonly List<Transform> spinners = new List<Transform>();
    private readonly List<Quaternion> spinnerHome = new List<Quaternion>();
    private readonly List<Transform> seats = new List<Transform>();
    private readonly List<GameObject> seated = new List<GameObject>();
    private readonly List<float> seatReadyAt = new List<float>();
    private Transform whistle, smokePoint;
    private ParticleSystem smoke;
    private Light lamp;
    private float wheelAngle;
    private int hopsPlayed;

    public override float BoardReadySeconds => StartupSeconds + 0.2f;
    public override float DepartureSeconds => 7.5f;
    public override Transform DepartureFocus => cars.Count > 1 ? cars[1] : BoardPoint; // 탄 쿠키가 앉은 객차

    private void Awake()
    {
        foreach (string name in new[] { "Loco", "Coach" })
        {
            Transform car = transform.Find(name);
            if (car == null) continue;
            cars.Add(car);
            carHome.Add(car.localPosition);
            float rear = 0f;
            foreach (Renderer r in car.GetComponentsInChildren<Renderer>())
                rear = Mathf.Max(rear, car.localPosition.z - transform.InverseTransformPoint(r.bounds.min).z, car.localPosition.z - transform.InverseTransformPoint(r.bounds.max).z);
            carRear.Add(rear);
        }
        Transform tunnel = transform.Find("Tunnel");
        if (tunnel != null) portal = tunnel.localPosition.z;
        foreach (int i in SpinSlots)
        {
            Transform slot = FindDeep(transform, $"Slot_{i:00}");
            if (slot == null) continue;
            spinners.Add(slot);
            spinnerHome.Add(slot.localRotation);
        }
        for (int i = 0; ; i++)
        {
            Transform seat = FindDeep(transform, $"Seat_{i:00}");
            if (seat == null) break;
            seats.Add(seat);
        }
        whistle = FindDeep(transform, "Whistle");
        smokePoint = FindDeep(transform, "Smoke");

        lamp = new GameObject("WhistleLight").AddComponent<Light>();
        lamp.transform.SetParent(whistle != null ? whistle : transform, false);
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.85f, 0.55f);
        lamp.range = 14f;
        lamp.intensity = 0f;

        // 소리(SoundPlan.md S6): 시동 칙칙(점점 빨라짐) / 출발: 기적 → 칙칙폭폭 가속(기관차를 따라감) → 기관차가 터널에 들어가는 순간 울림
        Transform loco = cars.Count > 0 ? cars[0] : null;
        AddCue(SoundId.TrainPuff, CueClock.Completed, 0f, smokePoint);
        AddCue(SoundId.TrainWhistle, CueClock.Departed, 0f, whistle);
        AddCue(SoundId.TrainChug, CueClock.Departed, WhistleSeconds, loco);
        if (loco != null && tunnel != null && portal > carHome[0].z)
        {
            float tunnelAt = WhistleSeconds + Mathf.Sqrt(2f * (portal - carHome[0].z) / Acceleration);
            if (tunnelAt < DepartureSeconds) AddCue(SoundId.TrainTunnel, CueClock.Departed, tunnelAt, tunnel);
        }
    }

    public override void Tick(EscapeState state, double now)
    {
        float c = state != null ? Since(state.CompletedAt, now) : -1f;
        float d = state != null ? Since(state.DepartedAt, now) : -1f;

        // 시동: 힘이 오르며 바퀴가 천천히 돌고 연기가 빨라진다
        float power = c < 0f ? 0f : Mathf.Clamp01(c / StartupSeconds);
        float rolled = d >= WhistleSeconds ? 0.5f * Acceleration * (d - WhistleSeconds) * (d - WhistleSeconds) : 0f;
        float speed = d >= WhistleSeconds ? Acceleration * (d - WhistleSeconds) : 0f;
        float idleSpin = power * 40f * (d >= 0f ? 0f : 1f);                     // 시동 중 제자리에서 살짝 헛돈다
        wheelAngle += (idleSpin + speed / WheelRadius * Mathf.Rad2Deg) * Time.deltaTime;
        for (int i = 0; i < spinners.Count; i++)
            spinners[i].localRotation = spinnerHome[i] * Quaternion.Euler(0f, 0f, wheelAngle);

        bool gone = d >= DepartureSeconds;
        for (int i = 0; i < cars.Count; i++)
        {
            Vector3 p = carHome[i] + Vector3.forward * rolled;
            bool hidden = gone || p.z - carRear[i] > portal + 1f; // 터널 속으로 다 들어감
            if (cars[i].gameObject.activeSelf == hidden) cars[i].gameObject.SetActive(!hidden);
            cars[i].localPosition = p;
        }

        // 기적 불빛: 시동 중 깜빡임 → 기다리는 동안 은은하게 → 출발 때 크게 번쩍
        float blink = c >= 0f && c < StartupSeconds ? (Mathf.Repeat(c, 0.6f) < 0.3f ? 1f : 0.2f) : (c >= 0f ? 0.5f : 0f);
        float burst = d >= 0f ? Mathf.Sin(Mathf.Clamp01(d / WhistleSeconds) * Mathf.PI) : 0f;
        lamp.intensity = blink * 2f + burst * 8f;
        if (whistle != null) whistle.localScale = Vector3.one * (c >= 0f ? 1f + 0.25f * blink + 0.8f * burst : 0.6f);

        UpdateSmoke(c < 0f ? 0f : (d >= 0f ? 1f : power * 0.6f));
        UpdatePassengers(state != null ? state.Waiting.Count : 0);
    }

    public override void OnCookieBoarded(Vector3 from)
    {
        int seat = Mathf.Min(hopsPlayed, seats.Count - 1);
        hopsPlayed++;
        while (seat >= 0 && seatReadyAt.Count <= seat) seatReadyAt.Add(0f);
        if (seat >= 0) seatReadyAt[seat] = Time.time + HopSeconds;
        BoardingFx.Play(from, BoardPoint.position + Vector3.up * 0.8f, BoardingFx.Style.Hop);
    }

    // 탄 쿠키 수만큼 창가 좌석에 인형을 앉힌다(폴짝 뛰는 연출이 끝난 뒤).
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

    // 굴뚝 연기 퍼프(시동 중 점점 잦아지고, 출발하면 가장 잦다).
    private void UpdateSmoke(float amount)
    {
        if (smokePoint == null) return;
        if (amount > 0.05f && smoke == null) smoke = CreateSmoke();
        if (smoke == null) return;
        var emission = smoke.emission;
        emission.rateOverTime = 2f + 14f * amount;
        if (amount <= 0.05f) emission.rateOverTime = 0f;
    }

    private ParticleSystem CreateSmoke()
    {
        // 월드 공간이라 달릴 때 연기가 뒤로 남는다
        ParticleSystem ps = EscapeVisuals.CreateParticles("ChimneySmoke", smokePoint, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(2.2f, 2.2f), Speed = new Vector2(2.5f, 2.5f), Size = new Vector2(0.6f, 1.1f),
            Color = new Color(0.85f, 0.82f, 0.88f, 0.6f), MaxParticles = 80,
            Shape = ParticleSystemShapeType.Cone, Angle = 10f, Radius = 0.3f, GrowTo = 2.4f,
            FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 0.6f,
        });
        ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ps.Play();
        return ps;
    }
}
