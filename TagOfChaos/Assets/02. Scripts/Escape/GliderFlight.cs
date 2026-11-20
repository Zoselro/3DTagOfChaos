using Photon.Pun;
using UnityEngine;

// §90 P5 패러글라이더(Heaven Of Bakery 전용 아이템, 1회용). 쿠키(스파이 포함)마다 붙고, 상태의 Glides에 내 번호가 있으면 활공한다.
// - 모든 화면: 등 위로 펼친 캐노피(원점 = 엉덩이) + 앉은 자세(Glide 상태).
// - 본인: 앞으로 초속 11 m, 내려가는 속도 초속 1.6 m. W = 빠르게(14 / 3), S = 천천히(7 / 1.2), A·D와 마우스로 방향.
//   땅에 닿거나 Space를 누르면 접는다(GlideClose — 아이템은 펼 때 없어졌다). 오르지는 않는다(사용자: 높은 곳에서 오래 활공으로 충분).
public class GliderFlight : MonoBehaviour
{
    public const float Forward = 11f, Sink = 1.6f;
    public const float FastForward = 14f, FastSink = 3f;
    public const float SlowForward = 7f, SlowSink = 1.2f;
    public const float TurnSpeed = 90f;      // A·D 도/초
    public const float OpenSeconds = 0.4f;   // 펼치는 동안 속도를 서서히
    private const float GroundProbe = 0.7f;

    private EscapeManager manager;
    private HideOrSeekPlayer cookie;
    private Rigidbody body;
    private Transform canopy;
    private Transform hipsBone;
    // §91 하네스(모델 원점 = 앉는 자리)를 엉덩이 뼈 약간 아래·뒤에 매 프레임 맞춘다 — 예전에는 펼 때 한 번 엉덩이 높이에 두어,
    // 앉는 활공 자세(엉덩이가 내려감)에서는 하네스가 등 위·목에 걸려 보였다.
    public static readonly Vector3 SeatOffset = new Vector3(0f, -0.28f, -0.14f);
    private bool gliding;
    private float yaw, openedAt;
    private bool closeSent;

    public bool IsGliding => gliding;

    public void Init(EscapeManager escape, HideOrSeekPlayer owner, Rigidbody rb)
    {
        manager = escape;
        cookie = owner;
        body = rb;
    }

    private int Actor => cookie != null && cookie.View != null && cookie.View.Owner != null ? cookie.View.Owner.ActorNumber : -1;

    // 활공 속도(순수 계산 — 시험): 앞뒤 입력(+1 = W, -1 = S).
    public static Vector2 Speeds(float forwardInput)
    {
        if (forwardInput > 0.3f) return new Vector2(FastForward, FastSink);
        if (forwardInput < -0.3f) return new Vector2(SlowForward, SlowSink);
        return new Vector2(Forward, Sink);
    }

    private void Update()
    {
        EscapeState s = manager != null ? manager.State : null;
        bool now = s != null && s.GlideIndex(Actor) >= 0;
        if (now != gliding)
        {
            gliding = now;
            SetCanopy(gliding);
            if (gliding) { yaw = transform.eulerAngles.y; openedAt = Time.time; closeSent = false; GameAudio.PlayAt(SoundId.GliderOpen, transform.position); }
        }
        // if (canopy != null && gliding) canopy.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (!gliding || !cookie.IsMine) return;
        if (PlayerInput.JumpPressed || Grounded()) Close();
        float turn = PlayerInput.Move.x;
        Camera cam = Camera.main;
        float camYaw = cam != null ? cam.transform.eulerAngles.y : yaw;
        yaw = Mathf.MoveTowardsAngle(yaw, camYaw, 60f * Time.deltaTime) + turn * TurnSpeed * Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (!gliding || cookie == null || !cookie.IsMine || body == null || body.isKinematic) return;
        Vector2 sp = Speeds(PlayerInput.Move.y);
        float k = Mathf.Clamp01((Time.time - openedAt) / OpenSeconds);
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        // float vy = Mathf.Lerp(body.linearVelocity.y, -sp.y, k);
        // 물리 한 칸의 중력이 이 뒤에 더해지므로 미리 빼 둔다(재 보니 1.6 → 1.8 m/s로 가라앉았다).
        float vy = Mathf.Lerp(body.linearVelocity.y, -sp.y, k) - (body.useGravity ? Physics.gravity.y * Time.fixedDeltaTime * k : 0f);
        body.linearVelocity = fwd * sp.x * k + Vector3.up * vy + new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z) * (1f - k);
        body.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
    }

    private void LateUpdate()
    {
        if (canopy == null || !canopy.gameObject.activeSelf) return;
        Quaternion yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 seat = hipsBone != null ? hipsBone.position : transform.position + Vector3.up * 0.9f;
        canopy.SetPositionAndRotation(seat + yaw * SeatOffset, yaw);
    }

    private bool Grounded() =>
        Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit h, GroundProbe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
        && h.collider.GetComponentInParent<IGameCharacter>() == null;

    private void Close()
    {
        if (closeSent) return;
        closeSent = true;
        manager.Request(EscapeOp.GlideClose);
    }

    private void SetCanopy(bool on)
    {
        if (on && canopy == null) Build();
        if (canopy != null) canopy.gameObject.SetActive(on);
    }

    private void Build()
    {
        Animator animator = GetComponentInChildren<Animator>();
        Transform hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        canopy = new GameObject("Paraglider").transform;
        canopy.SetParent(transform, false);
        hipsBone = hips;
        canopy.position = hips != null ? hips.position : transform.position + Vector3.up * 0.9f;
        canopy.localScale = new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y, 1f / transform.lossyScale.z);
        GameObject model = EscapeCatalogSO.Current != null ? EscapeCatalogSO.Current.GliderModel : null;
        if (model != null)
        {
            GameObject m = Instantiate(model, canopy, false);
            foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        else
        {
            GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(wing.GetComponent<Collider>());
            wing.transform.SetParent(canopy, false);
            wing.transform.localPosition = Vector3.up * 3f;
            wing.transform.localScale = new Vector3(4.8f, 0.2f, 1.4f);
            EscapeVisuals.Tint(wing, new Color(1f, 0.6f, 0.75f), 0.4f);
        }
    }
}
