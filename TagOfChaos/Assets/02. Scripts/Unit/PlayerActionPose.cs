using UnityEngine;

// 쿠키·캐릭터의 새 동작(Request1009Plan.md §45.4, §74 P13, §75): 층 무게를 여기서만 정한다 — 모든 화면에서 같은 규칙으로 돈다.
// - "Action" 층(상체): 한 번 동작(던지기·뿅망치 내려찍기·오른/왼 주먹) > 스턴건 조준(우클릭 유지) > 들기 자세(뿅망치 어깨 걸침·글러브 가드).
// - "Hold" 층(오른팔만): 한 손 아이템을 가슴 옆 앞에 드는 자세(§75 D1). Action 층이 켜지면 그 위에서 덮는다.
// - "Trapped" 층(온몸): 물풍선에 갇힘.
// 조준은 본인 화면 ToolUser.AimView → 동기화(PlayerNetworkSync), 한 번 동작은 도구 사용(ToolUser·ToolHitReceiver), 들기 자세는 HeldItemPresenter(모든 화면).
// 조준 모양(§77 TP1): 스턴건 = StunAim(두 손으로 겨눔), 물풍선·섬광탄 = 던지기 동작을 팔을 뒤로 젖힌 순간(ThrowReadyAt)에 멈춘 자세.
// 스턴건 반동(§77 TP2): 쏜 순간 0.18초 동안 팔·가슴을 위로 튕긴다(애니메이션 뒤 LateUpdate, 손에 든 아이템보다 먼저).
[DefaultExecutionOrder(-20)]
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
    public const string ThrowReadyState = "ThrowReady"; // 실제 상태가 아니라 Throw를 멈춘 자세의 이름
    public const float ThrowReadyAt = 0.28f;  // Throw(0.8초)의 정규화 시각 — 팔을 뒤로 젖힌 순간(손을 떠나는 0.44보다 앞)
    public const float KickSeconds = 0.18f;
    public const float KickArmDegrees = 14f;
    public const float KickChestDegrees = 4f;

    public enum OneShot { Throw, Swing, PunchR, PunchL }
    public enum Hold { None, Hammer, Glove }

    private Animator animator;
    private int actionLayer = -2;
    private int holdLayer = -2;
    private int trappedLayer = -2;
    private bool aiming;
    private bool throwAim;
    private float kickAt = -10f;
    private Avatar boneAvatar;
    private Transform chest, leftUpperArm, rightUpperArm, rightLowerArm;
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

    // 들고 있는 것이 던지는 도구(물풍선·섬광탄)면 true — 조준 자세가 던지기 준비가 된다(HeldItemPresenter가 모든 화면에서 정한다).
    public void SetThrowAim(bool on) => throwAim = on;

    public bool IsThrowReady => aiming && throwAim && actionState == ThrowReadyState;

    // 본인 1인칭 조준 화면인지(CookieAimCamera가 정한다). 던지는 도구는 1인칭에서도 던지기 준비 자세를 그대로 유지한다
    // (§77.1 — 사용자: 1인칭으로 바뀌며 스턴건 자세로 바뀌면 안 되고 던지는 동작이 계속돼야 한다).
    public bool FirstPersonView { get; set; }
    public bool HoldsThrowAim => aiming && throwAim;

    // 스턴건 반동(본인은 ToolUser.Use, 다른 화면은 ToolHit).
    public void Kick() => kickAt = Time.time;

    // 반동 세기(0~1): 0.25까지 빠르게 올라 천천히 돌아온다.
    public static float KickWeight(float t)
    {
        if (t < 0f || t >= 1f) return 0f;
        return t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
    }

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
                if (aiming && throwAim && HasState(actionLayer, ThrowState))
                {
                    // 던지기 준비: Throw를 팔을 젖힌 순간에 매 프레임 다시 세운다
                    animator.Play(ThrowState, actionLayer, ThrowReadyAt);
                    actionState = ThrowReadyState;
                }
                else if (aiming)
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

    private void LateUpdate()
    {
        float w = KickWeight((Time.time - kickAt) / KickSeconds);
        if (w <= 0f || animator == null || !animator.isHuman) return;
        if (boneAvatar != animator.avatar)
        {
            boneAvatar = animator.avatar;
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }
        Vector3 axis = transform.right; // 오른쪽 축으로 음의 각 = 앞이 위로
        Rotate(chest, axis, -KickChestDegrees * w);
        Rotate(rightUpperArm, axis, -KickArmDegrees * w);
        Rotate(leftUpperArm, axis, -KickArmDegrees * 0.8f * w);
        Rotate(rightLowerArm, axis, -KickArmDegrees * 0.5f * w);
    }

    private static void Rotate(Transform bone, Vector3 axis, float degrees)
    {
        if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
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
