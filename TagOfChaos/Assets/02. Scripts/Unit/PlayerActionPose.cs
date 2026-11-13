using UnityEngine;

// 쿠키·캐릭터의 새 동작(Request1009Plan.md §45.4, §74 P13, §75): 층 무게를 여기서만 정한다 — 모든 화면에서 같은 규칙으로 돈다.
// - "Action" 층(상체): 한 번 동작(던지기·뿅망치 내려찍기·오른/왼 주먹) > 스턴건 조준(우클릭 유지) > 들기 자세(뿅망치 어깨 걸침·글러브 가드).
// - "Hold" 층(오른팔만): 한 손 아이템을 가슴 옆 앞에 드는 자세(§75 D1). Action 층이 켜지면 그 위에서 덮는다.
// - "Trapped" 층(온몸): 물풍선에 갇힘.
// 조준은 본인 화면 ToolUser.Aiming → 동기화(PlayerNetworkSync), 한 번 동작은 도구 사용(ToolUser·ToolHitReceiver), 들기 자세는 HeldItemPresenter(모든 화면).
public class PlayerActionPose : MonoBehaviour
{
    public const string ActionLayer = "Action";
    public const string HoldLayer = "Hold";
    public const string TrappedLayer = "Trapped";
    public const string StunAimState = "StunAim";
    public const string ThrowState = "Throw";
    public const string SwingState = "Swing";
    public const string TrappedState = "Trapped";
    public const string HammerHoldState = "HammerHold"; // 뿅망치를 든 동안 오른쪽 어깨에 걸친 자세(§74 P13)
    public const string GloveGuardState = "GloveGuard"; // 권투 글러브를 낀 동안 두 주먹을 턱 앞에 든 가드(§75 C1)
    public const string PunchRState = "PunchR";
    public const string PunchLState = "PunchL";
    public const string ItemHoldState = "ItemHold";     // 한 손 아이템(§75 D1)
    public const float ThrowSeconds = 0.8f;
    public const float ThrowRelease = 0.35f;  // 손에서 물풍선이 떠나는 순간
    public const float SwingSeconds = 0.7f;   // 어깨 → 머리 위 → 앞 아래로 내려찍기 → 어깨(Cookie_HammerSwing, §75 A2)
    public const float SwingHit = 0.32f;      // 망치가 닿는 순간
    public const float PunchSeconds = 0.4f;   // 가드 → 뻗기 → 가드(Cookie_PunchR/L)
    public const float PunchHit = 0.13f;      // 주먹이 다 뻗은 순간
    private const float BlendSpeed = 8f;

    public enum OneShot { Throw, Swing, PunchR, PunchL }
    public enum Hold { None, Hammer, Glove }

    private Animator animator;
    private int actionLayer = -2;
    private int holdLayer = -2;
    private int trappedLayer = -2;
    private bool aiming;
    private bool trapped;
    private bool oneHandHold;
    private Hold hold;
    private float oneShotUntil = -1f;
    private float actionWeight;
    private float holdWeight;
    private float trappedWeight;
    private string actionState;

    public bool IsAiming => aiming;
    public bool IsSwinging => Time.time < oneShotUntil && actionState == SwingState;
    public bool IsHoldingHammer => hold == Hold.Hammer;
    public bool IsHoldingGlove => hold == Hold.Glove;

    private void Awake() => animator = GetComponent<Animator>();

    public void SetAiming(bool on) => aiming = on;

    public void SetTrapped(bool on) => trapped = on;

    // 뿅망치·글러브를 든 동안의 상체 자세(HeldItemPresenter가 모든 화면에서 정한다). 한 번 동작·조준이 우선이다.
    public void SetHold(Hold kind) => hold = kind;

    // 뿅망치를 든 동안 true(옛 호출부 호환).
    public void SetHammerHold(bool on) => hold = on ? Hold.Hammer : hold == Hold.Hammer ? Hold.None : hold;

    // 한 손 아이템을 든 동안 true — 오른팔만 가슴 옆 앞으로(§75 D1).
    public void SetOneHandHold(bool on) => oneHandHold = on;

    public void Play(OneShot shot)
    {
        if (!Resolve() || actionLayer < 0) return;
        string state;
        float seconds;
        switch (shot)
        {
            case OneShot.Throw: state = ThrowState; seconds = ThrowSeconds; break;
            case OneShot.PunchR: state = PunchRState; seconds = PunchSeconds; break;
            case OneShot.PunchL: state = PunchLState; seconds = PunchSeconds; break;
            default: state = SwingState; seconds = SwingSeconds; break;
        }
        if (!HasState(actionLayer, state)) { state = SwingState; seconds = SwingSeconds; } // 컨트롤러가 옛 것이면
        animator.Play(state, actionLayer, 0f);
        actionState = state;
        oneShotUntil = Time.time + seconds;
        actionWeight = 1f; // 한 번 동작은 바로 덮는다(망치·주먹이 닿는 시각과 맞추려고)
    }

    private string HoldStateName => hold == Hold.Hammer ? HammerHoldState : hold == Hold.Glove ? GloveGuardState : null;

    private void Update()
    {
        if (!Resolve()) return;
        bool shot = Time.time < oneShotUntil;
        if (actionLayer >= 0)
        {
            string holdState = HoldStateName;
            if (!shot)
            {
                if (aiming)
                {
                    if (actionState != StunAimState) { animator.Play(StunAimState, actionLayer, 0f); actionState = StunAimState; }
                }
                else if (holdState != null && HasState(actionLayer, holdState))
                {
                    if (actionState != holdState) { animator.Play(holdState, actionLayer, 0f); actionState = holdState; }
                }
                else actionState = null;
            }
            actionWeight = Mathf.MoveTowards(actionWeight, actionState != null ? 1f : 0f, BlendSpeed * Time.deltaTime);
            animator.SetLayerWeight(actionLayer, actionWeight);
        }
        if (holdLayer >= 0)
        {
            if (oneHandHold && holdWeight <= 0f && HasState(holdLayer, ItemHoldState)) animator.Play(ItemHoldState, holdLayer, 0f);
            holdWeight = Mathf.MoveTowards(holdWeight, oneHandHold ? 1f : 0f, BlendSpeed * Time.deltaTime);
            animator.SetLayerWeight(holdLayer, holdWeight);
        }
        if (trappedLayer >= 0)
        {
            if (trapped && trappedWeight <= 0f) animator.Play(TrappedState, trappedLayer, 0f);
            trappedWeight = Mathf.MoveTowards(trappedWeight, trapped ? 1f : 0f, BlendSpeed * Time.deltaTime);
            animator.SetLayerWeight(trappedLayer, trappedWeight);
        }
    }

    private bool HasState(int layer, string state) => animator.HasState(layer, Animator.StringToHash(state));

    private bool Resolve()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        if (actionLayer == -2) actionLayer = animator.GetLayerIndex(ActionLayer);
        if (holdLayer == -2) holdLayer = animator.GetLayerIndex(HoldLayer);
        if (trappedLayer == -2) trappedLayer = animator.GetLayerIndex(TrappedLayer);
        return true;
    }
}
