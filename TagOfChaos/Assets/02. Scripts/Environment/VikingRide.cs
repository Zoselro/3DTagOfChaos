using UnityEngine;

// 판 내내 흔들리는 바이킹(Request1009Plan.md §36, 단계 CM5). 프리팹 루트에 붙이고 배(VK_Ship, 원점 = 축)를 로컬 Z축으로 흔든다.
// 시계는 롤러코스터와 같다(CoasterRide.NetworkTime) — 모든 화면이 같은 각도를 계산하므로 네트워크 메시지가 없다.
// 한 바퀴(Cycle) = 쉼(종) → 점점 커짐 → 가장 크게 → 점점 작아짐. 각도 = 진폭(t) × sin(2π t / 흔들림 주기). 진폭이 부드럽게 바뀌어 진자처럼 보인다.
// 소리: 출발 종(VikingBell), 움직이는 동안 기계 소리(VikingHum, 진폭에 따라 크기), 끝에서 나무 삐걱(VikingCreak), 아래를 지날 때 바람(VikingWhoosh).
public sealed class VikingRide : MonoBehaviour
{
    private const double SnapSeconds = 0.5;
    private const float CatchUpRate = 2f;
    private const float CheckSeconds = 0.25f;
    private const float FadePerSecond = 2f;
    private const float CreakMinAngle = 18f;   // 이보다 작게 흔들릴 때는 끝에서 삐걱이지 않는다
    private const float WhooshMinAngle = 35f;  // 이보다 크게 흔들릴 때 아래를 지나며 바람 소리

    [SerializeField] private Transform ship;
    [SerializeField, Min(1f)] private float swingPeriod = 6.2f;   // 팔 9.5 m 진자(2π√(L/g) ≈ 6.2초)
    [SerializeField, Range(10f, 80f)] private float maxAngle = 70f;
    [SerializeField, Min(0f)] private float restSeconds = 8f;
    [SerializeField, Min(0.1f)] private float rampUpSeconds = 14f;
    [SerializeField, Min(0f)] private float fullSeconds = 18f;
    [SerializeField, Min(0.1f)] private float rampDownSeconds = 14f;
    [SerializeField] private float phaseOffset;                   // 바이킹이 여러 대면 서로 다른 때에 흔들리게

    private Quaternion rest;
    private double clock;
    private bool clockStarted;
    private float angle, lastAngle, lastVelocity;
    private AudioHandle hum;
    private float humFade, nextCheck;
    private bool near, wasMoving;

    public float Cycle => restSeconds + rampUpSeconds + fullSeconds + rampDownSeconds;
    public float CurrentAngle => angle;

    // 한 바퀴 안 시각 → 진폭(0~maxAngle). 쉼 → 커짐(부드럽게) → 가장 크게 → 작아짐.
    public float AmplitudeAt(double t)
    {
        float c = (float)Repeat(t + phaseOffset, Cycle);
        if (c < restSeconds) return 0f;
        c -= restSeconds;
        if (c < rampUpSeconds) return maxAngle * Mathf.SmoothStep(0f, 1f, c / rampUpSeconds);
        c -= rampUpSeconds;
        if (c < fullSeconds) return maxAngle;
        c -= fullSeconds;
        return maxAngle * Mathf.SmoothStep(1f, 0f, c / rampDownSeconds);
    }

    // 시각 t의 배 각도(도). 흔들림은 한 바퀴의 움직이는 구간이 시작될 때 0°에서 출발한다.
    public float AngleAt(double t)
    {
        float amp = AmplitudeAt(t);
        if (amp <= 0f) return 0f;
        double moving = Repeat(t + phaseOffset, Cycle) - restSeconds;
        return amp * Mathf.Sin((float)(moving / swingPeriod) * Mathf.PI * 2f);
    }

    private static double Repeat(double t, double length) => t - System.Math.Floor(t / length) * length;

    private void Awake()
    {
        if (ship != null) rest = ship.localRotation;
    }

    private void OnDisable()
    {
        hum.Stop();
        hum = default;
        humFade = 0f;
        near = false;
    }

    private void Update()
    {
        if (ship == null) return;
        float dt = Time.deltaTime;
        double target = CoasterRide.NetworkTime;
        if (!clockStarted || System.Math.Abs(target - clock) > SnapSeconds) { clock = target; clockStarted = true; }
        else clock += dt + (target - clock - dt) * Mathf.Clamp01(CatchUpRate * dt);

        lastAngle = angle;
        angle = AngleAt(clock);
        ship.localRotation = rest * Quaternion.Euler(0f, 0f, angle);
        UpdateAudio(dt);
    }

    private void UpdateAudio(float dt)
    {
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + CheckSeconds;
            near = AmbientEmitter.ShouldPlay(near, Vector3.Distance(ship.position, AudioListenerAnchor.Position), MaxDistance(SoundId.VikingHum));
        }
        float amp = AmplitudeAt(clock);
        bool moving = amp > 0.5f;
        if (moving && !wasMoving && near) GameAudio.PlayOn(SoundId.VikingBell, ship);
        wasMoving = moving;

        humFade = Mathf.MoveTowards(humFade, near && moving ? Mathf.Lerp(0.35f, 1f, amp / maxAngle) : 0f, FadePerSecond * dt);
        if (humFade > 0.001f && !hum.IsPlaying) hum = GameAudio.StartLoop(SoundId.VikingHum, ship);
        if (hum.IsPlaying)
        {
            if (humFade <= 0.001f) { hum.Stop(); hum = default; }
            else GameAudio.SetLoopFade(hum, humFade);
        }
        if (!near || dt <= 0f) return;

        float velocity = (angle - lastAngle) / dt;
        if (amp > CreakMinAngle && Mathf.Sign(velocity) != Mathf.Sign(lastVelocity) && Mathf.Abs(lastVelocity) > 0.01f)
            GameAudio.PlayOn(SoundId.VikingCreak, ship);                          // 끝에서 멈췄다 돌아설 때
        if (amp > WhooshMinAngle && Mathf.Sign(angle) != Mathf.Sign(lastAngle) && lastAngle != 0f)
            GameAudio.PlayOn(SoundId.VikingWhoosh, ship);                         // 가장 아래를 지날 때
        lastVelocity = velocity;
    }

    private static float MaxDistance(SoundId id) =>
        SoundCatalogSO.Current != null && SoundCatalogSO.Current.TryGet(id, out SoundCatalogSO.Entry e) ? e.maxDistance : 50f;

#if UNITY_EDITOR
    public void EditorSetup(Transform shipTransform) => ship = shipTransform;
    public void EditorSetPhase(float offset) => phaseOffset = offset;
#endif
}
