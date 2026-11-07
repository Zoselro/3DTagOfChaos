using UnityEngine;

// 거대 괴물(MixedMonster ×5)의 규칙 — 순수 계산(Request1009Plan.md §64). 방장 판단(GiantBrain)·모든 화면의 연출(GiantPresenter)·
// 카메라(WitchRevealCamera)·시험이 같은 값을 쓴다. 시각은 모두 공통 시계(PhotonNetwork.Time)와 방 속성만 써서 모든 화면이 같다.
//   등장: 마녀 타이머 시작 때 방장이 확률로 정한다 → 마녀 등장·돌아섬·얼굴 다음에 마법진 3초(원 1.5 + 별 1.5) → 괴물이 2초 동안 솟아오름
//         → 복귀 3초 → 걷기(마녀 걷기와 같은 시각)부터 쫓는다. 그만큼(5초) 타임어택 끝이 늦어진다.
//   속도: 3에서 시작해 (마녀 시간 ÷ 10)마다 1씩 오른다. 오를 때는 RampSeconds 동안 부드럽게 오른다.
//   밟기: 대상이 앞발 자리에서 StompTriggerRange 안에 들어오면 빠르게 내리찍는다 — StompImpactSeconds에 발이 닿고 그 순간 판정.
public static class GiantRules
{
    // 등장 연출
    public const float CircleSeconds = 3f;          // 마법진: 원이 그려지고(절반) 안에 별이 그려짐(절반)
    public const float RevealSeconds = 2f;          // 괴물이 마법진에서 서서히 나타남
    public const float IntroSeconds = CircleSeconds + RevealSeconds;
    public const float CircleFadeSeconds = 1f;      // 다 나타난 뒤 마법진이 사라지는 시간

    // 몸
    public const float Scale = 5f;                  // 모델 원래 크기(키 약 2 m)의 5배 — 술래(약 6.5 m)보다 크다
    public const float Height = 10.2f;              // 5배 키(m, 모자 끝까지)
    public const float FrontReach = 2.9f;           // 몸 중심에서 앞발이 닿는 곳까지(m, 5배)
    public const float NaturalWalkSpeed = 6.3f;     // Walk 동작을 1배로 재생할 때 발이 미끄러지지 않는 속도(m/s, 5배 — 발 뒤로 미는 속도 1.26 × 5)

    // 속도
    public const float BaseSpeed = 3f;
    public const float SpeedStep = 1f;
    public const int SpeedSteps = 10;               // 마녀 시간을 이만큼 나눈 간격마다 SpeedStep씩
    public const float RampSeconds = 0.5f;

    // 밟기(빠른 내리찍기 — 사용자 결정 2026-11-07, §64.11)
    public const float StompImpactSeconds = 0.4f;   // Monster_Stomp 13프레임(30 fps)
    public const float StompSeconds = 0.9f;         // 동작 전체(28프레임) — 이 동안 멈춰 있다
    public const float StompTriggerRange = 3f;      // 앞발 자리에서 이 거리 안이면 내리찍는다
    public const float StompRadius = 3.5f;          // 판정 원(앞발 두 개 사이 가운데)
    public const float StompHeight = 3f;            // 판정 높이(바닥 ±)
    public const float StompCooldown = 0.3f;        // 내리찍은 뒤 다음 밟기까지

    // 층 이동(마법진으로 가라앉았다가 다른 층에서 솟아오름)
    public const float FloorGap = 12f;              // 높이 차이가 이보다 크면 다른 층
    public const float WarpSinkSeconds = 2.5f;      // 마법진 1초 + 가라앉기 1.5초
    public const float WarpRiseSeconds = 2.5f;      // 마법진 1초 + 솟아오르기 1.5초
    public const float WarpSeconds = WarpSinkSeconds + WarpRiseSeconds;
    public const float WarpMinDistance = 12f, WarpMaxDistance = 20f; // 다른 층 대상에서 떨어진 도착 거리

    public const float SpawnMinDistance = 25f;      // 나오는 자리는 모두에게서 이만큼 떨어진 곳(되도록)

    // ---------------- 시각표 ----------------

    // 마법진 시작 = 마녀 등장 + 등장·돌아섬·얼굴(복귀 앞).
    public static double CircleStart(double appearAt) =>
        appearAt + WitchTimeline.RevealSeconds + WitchTimeline.TurnSeconds + WitchTimeline.FaceSeconds;
    public static double RevealStart(double appearAt) => CircleStart(appearAt) + CircleSeconds;
    public static double RevealEnd(double appearAt) => RevealStart(appearAt) + RevealSeconds;

    // 등장 진행(0~1): 마법진 원·별, 괴물 나타남.
    public static float CircleRingProgress(double now, double appearAt) => Clamp01((now - CircleStart(appearAt)) / (CircleSeconds * 0.5));
    public static float CircleStarProgress(double now, double appearAt) => Clamp01((now - CircleStart(appearAt) - CircleSeconds * 0.5) / (CircleSeconds * 0.5));
    public static float RevealProgress(double now, double appearAt) => Clamp01((now - RevealStart(appearAt)) / RevealSeconds);

    // ---------------- 속도 ----------------

    // 속도가 한 단계 오르는 간격(초). 마녀 시간이 없으면 60초 기준.
    public static float StepSeconds(int timeAttackSeconds) => Mathf.Max(1f, (timeAttackSeconds > 0 ? timeAttackSeconds : 60) / (float)SpeedSteps);

    // 걷기 시작 뒤 t초의 속도(m/s).
    public static float Speed(double t, int timeAttackSeconds)
    {
        if (t <= 0) return BaseSpeed;
        float p = StepSeconds(timeAttackSeconds);
        float steps = 0f;
        for (int i = 1; i <= SpeedSteps; i++) steps += Clamp01((t - i * p) / RampSeconds);
        return BaseSpeed + SpeedStep * steps;
    }

    // 걷기 시작부터 t초 동안 간 거리(m) — Speed의 적분(닫힌 식이라 모든 화면이 같은 값).
    public static double Distance(double t, int timeAttackSeconds)
    {
        if (t <= 0) return 0;
        float p = StepSeconds(timeAttackSeconds);
        double d = BaseSpeed * t;
        for (int i = 1; i <= SpeedSteps; i++)
        {
            double u = t - i * p;
            if (u <= 0) break;
            d += SpeedStep * (u <= RampSeconds ? u * u / (2.0 * RampSeconds) : u - RampSeconds * 0.5);
        }
        return d;
    }

    // Walk 동작 재생 배율(이동 속도에 맞춘다 — 발이 미끄러지지 않게).
    public static float WalkAnimRate(float speed) => Mathf.Max(0.05f, speed / NaturalWalkSpeed);

    // ---------------- 밟기 ----------------

    public static Vector3 Forward(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public static Vector3 StompCenter(Vector3 body, float yaw) => body + Forward(yaw) * FrontReach;

    public static bool InStompRange(Vector3 center, Vector3 target) =>
        Mathf.Abs(target.y - center.y) <= StompHeight && FlatDistance(center, target) <= StompRadius;

    // 내리찍기 시작 조건: 대상을 바라봤을 때 앞발 자리에서 StompTriggerRange 안.
    public static bool ShouldStomp(Vector3 body, Vector3 target) =>
        Mathf.Abs(target.y - body.y) <= StompHeight + 1f && FlatDistance(StompCenter(body, YawTo(body, target, 0f)), target) <= StompTriggerRange;

    public static bool OtherFloor(float giantY, float targetY) => Mathf.Abs(targetY - giantY) > FloorGap;

    public static float YawTo(Vector3 from, Vector3 to, float fallback)
    {
        Vector3 d = to - from;
        d.y = 0f;
        return d.sqrMagnitude < 1e-4f ? fallback : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    public static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    private static float Clamp01(double v) => v <= 0 ? 0f : v >= 1 ? 1f : (float)v;
}
