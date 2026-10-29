using UnityEngine;

// 쿠키·캐릭터의 새 동작(Request1009Plan.md §45.4): 스턴건 조준(우클릭 유지)·물풍선 던지기·뿅망치 휘두르기(상체 Action 층)와
// 물풍선에 갇힘(온몸 Trapped 층). 층 무게를 여기서만 정한다 — 모든 화면에서 같은 규칙으로 돈다.
// 조준은 본인 화면 ToolUser.Aiming → 동기화(PlayerNetworkSync), 던지기·휘두르기는 도구 사용(ToolUser·ToolHit), 갇힘은 StunReceiver.Trap.
public class PlayerActionPose : MonoBehaviour
{
    public const string ActionLayer = "Action";
    public const string TrappedLayer = "Trapped";
    public const string StunAimState = "StunAim";
    public const string ThrowState = "Throw";
    public const string SwingState = "Swing";
    public const string TrappedState = "Trapped";
    public const float ThrowSeconds = 0.8f;
    public const float ThrowRelease = 0.35f;  // 손에서 물풍선이 떠나는 순간
    public const float SwingSeconds = 0.7f;
    public const float SwingHit = 0.32f;      // 망치가 닿는 순간
    private const float BlendSpeed = 8f;

    public enum OneShot { Throw, Swing }

    private Animator animator;
    private int actionLayer = -2;
    private int trappedLayer = -2;
    private bool aiming;
    private bool trapped;
    private float oneShotUntil = -1f;
    private float actionWeight;
    private float trappedWeight;
    private string actionState;

    public bool IsAiming => aiming;
    public bool IsSwinging => Time.time < oneShotUntil && actionState == SwingState;

    private void Awake() => animator = GetComponent<Animator>();

    public void SetAiming(bool on) => aiming = on;

    public void SetTrapped(bool on) => trapped = on;

    public void Play(OneShot shot)
    {
        if (!Resolve() || actionLayer < 0) return;
        string state = shot == OneShot.Throw ? ThrowState : SwingState;
        animator.Play(state, actionLayer, 0f);
        actionState = state;
        oneShotUntil = Time.time + (shot == OneShot.Throw ? ThrowSeconds : SwingSeconds);
        actionWeight = 1f; // 한 번 동작은 바로 덮는다(망치가 닿는 시각과 맞추려고)
    }

    private void Update()
    {
        if (!Resolve()) return;
        bool shot = Time.time < oneShotUntil;
        if (actionLayer >= 0)
        {
            if (!shot && aiming && actionState != StunAimState)
            {
                animator.Play(StunAimState, actionLayer, 0f);
                actionState = StunAimState;
            }
            if (!shot && !aiming) actionState = null;
            actionWeight = Mathf.MoveTowards(actionWeight, shot || aiming ? 1f : 0f, BlendSpeed * Time.deltaTime);
            animator.SetLayerWeight(actionLayer, actionWeight);
        }
        if (trappedLayer >= 0)
        {
            if (trapped && trappedWeight <= 0f) animator.Play(TrappedState, trappedLayer, 0f);
            trappedWeight = Mathf.MoveTowards(trappedWeight, trapped ? 1f : 0f, BlendSpeed * Time.deltaTime);
            animator.SetLayerWeight(trappedLayer, trappedWeight);
        }
    }

    private bool Resolve()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        if (actionLayer == -2) actionLayer = animator.GetLayerIndex(ActionLayer);
        if (trappedLayer == -2) trappedLayer = animator.GetLayerIndex(TrappedLayer);
        return true;
    }
}
