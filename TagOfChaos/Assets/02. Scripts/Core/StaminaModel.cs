using UnityEngine;

// 스태미나 값 묶음(Request1009Plan.md §46.3 — GameSettingsSO.Stamina). 진짜 쿠키와 AI(DecoyBrain)가 같은 값을 쓴다.
[System.Serializable]
public class StaminaSettings
{
    [Tooltip("최대 스태미나.")]
    [Min(1f)] public float max = 100f;
    [Tooltip("달리는 동안 초당 소모.")]
    [Min(0f)] public float runDrainPerSecond = 20f;
    [Tooltip("초당 회복(서 있을 때).")]
    [Min(0f)] public float regenPerSecond = 25f;
    [Tooltip("달리기·회피를 멈춘 뒤 회복이 시작될 때까지(초).")]
    [Min(0f)] public float regenDelay = 0.8f;
    [Tooltip("걷는 중 회복 배율.")]
    [Range(0f, 1f)] public float walkRegenMultiplier = 0.7f;
    [Tooltip("0이 되면 이 값까지 찰 때까지 달릴 수 없다.")]
    [Min(0f)] public float exhaustedRecoverAt = 35f;
    [Tooltip("회피 한 번의 비용(이보다 적으면 회피할 수 없다).")]
    [Min(0f)] public float dodgeCost = 25f;
}

// 스태미나 계산(순수 — EditMode로 시험한다). 쿠키 본인 화면(HideOrSeekPlayer)과 방장의 AI 두뇌(DecoyBrain)가 하나씩 들고 있다.
public sealed class StaminaModel
{
    private readonly StaminaSettings settings;
    private float regenDelayLeft;

    public StaminaModel(StaminaSettings settings, float start)
    {
        this.settings = settings ?? new StaminaSettings();
        Value = Mathf.Clamp(start, 0f, this.settings.max);
    }

    public float Value { get; private set; }
    public float Max => settings.max;
    public float Normalized => Value / Mathf.Max(1f, settings.max);
    public bool IsExhausted { get; private set; }
    public bool IsFull => Value >= settings.max - 1e-3f;
    public bool CanRun => !IsExhausted && Value > 0f;
    public bool CanDodge => Value >= settings.dodgeCost;
    public StaminaSettings Settings => settings;

    // 이번 프레임에 지쳤는지(바닥남 — 숨소리·깜빡임). Tick·TrySpend가 켜고 읽으면 꺼진다.
    private bool justExhausted;
    public bool ConsumeJustExhausted()
    {
        bool v = justExhausted;
        justExhausted = false;
        return v;
    }

    // dt초 지남. wantsRun: 달리기 키 + 움직이는 중. 실제로 달렸으면 true(지쳤으면 걷는다).
    public bool Tick(float dt, bool wantsRun, bool moving)
    {
        if (dt <= 0f) return wantsRun && CanRun;
        if (wantsRun && moving && CanRun)
        {
            Value -= settings.runDrainPerSecond * dt;
            regenDelayLeft = settings.regenDelay;
            if (Value <= 0f) Exhaust();
            return true;
        }

        float regenTime = dt;
        if (regenDelayLeft > 0f)
        {
            regenDelayLeft -= dt;
            regenTime = regenDelayLeft < 0f ? -regenDelayLeft : 0f;
            if (regenDelayLeft < 0f) regenDelayLeft = 0f;
        }
        if (regenTime > 0f)
        {
            Value = Mathf.Min(settings.max, Value + settings.regenPerSecond * (moving ? settings.walkRegenMultiplier : 1f) * regenTime);
            if (IsExhausted && Value >= settings.exhaustedRecoverAt) IsExhausted = false;
        }
        return false;
    }

    // 회피 등 한 번에 쓰는 비용. 모자라면 false(쓰지 않는다).
    public bool TrySpend(float amount)
    {
        if (Value < amount) return false;
        Value -= amount;
        regenDelayLeft = settings.regenDelay;
        if (Value <= 0f) Exhaust();
        return true;
    }

    public bool TrySpendDodge() => TrySpend(settings.dodgeCost);

    public void Reset(float value)
    {
        Value = Mathf.Clamp(value, 0f, settings.max);
        IsExhausted = false;
        regenDelayLeft = 0f;
    }

    // 지금 값으로 쉬지 않고 달릴 수 있는 시간(초). 지쳤으면 0.
    public float RunSecondsLeft => CanRun && settings.runDrainPerSecond > 0f ? Value / settings.runDrainPerSecond : CanRun ? float.MaxValue : 0f;

    private void Exhaust()
    {
        Value = 0f;
        if (!IsExhausted) justExhausted = true;
        IsExhausted = true;
    }
}
