// 방장 추정 검사(Request1009Plan.md §46.3 ST4): 보이는 수평 속도로 "달렸는지"를 추정해 같은 스태미나 규칙을 돌린다.
// 지쳐서 달릴 수 없어야 하는데 계속 달리기 속도로 움직인 시간이 쌓이면 알린다(1차는 로그만 — 정상 플레이를 막지 않는다).
public sealed class StaminaWatch
{
    public const float RunSpeedRatio = 1.15f;   // 걷기 속도의 이 배 이상이면 달린 것으로 본다(회피 0.5초도 달리기로 센다)
    public const float AlertSeconds = 3f;       // 지친 뒤 이만큼 더 달리면 알린다
    public const float AlertCooldown = 10f;

    private readonly StaminaModel model;
    private float lastAlertAt = -AlertCooldown;

    public StaminaWatch(StaminaSettings settings)
    {
        model = new StaminaModel(settings, float.MaxValue);
    }

    public float OverrunSeconds { get; private set; }
    public StaminaModel Model => model;

    // dt초 동안 평균 수평 속도 speed로 움직였다. 알려야 하면 true.
    public bool Observe(float dt, float speed, float walkSpeed, float now)
    {
        bool moving = speed > 0.5f;
        bool running = walkSpeed > 0f && speed > walkSpeed * RunSpeedRatio;
        if (running && !model.CanRun) OverrunSeconds += dt;
        else if (!running) OverrunSeconds = 0f;
        model.Tick(dt, running, moving);
        if (OverrunSeconds < AlertSeconds || now - lastAlertAt < AlertCooldown) return false;
        lastAlertAt = now;
        return true;
    }
}
