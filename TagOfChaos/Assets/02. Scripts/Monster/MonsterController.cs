using Photon.Pun;
using UnityEngine;
using UnityEngine.Serialization;

// 괴물 전용 이동 컨트롤러(GameRule.md §6.2). 시점은 1인칭(MonsterFirstPersonCamera, 몸은 카메라의 수평 방향을
// 바라봄)과 3인칭(쿠키와 같은 Camera_Ctrl 궤도 카메라, 몸은 이동 방향을 바라봄) 두 가지를 모두 지원하고,
// MonsterViewSwitcher가 고른다(GameFixPlan.md F6). 이동은 어느 쪽이든 카메라 기준이다. 쿠키와 입력은
// 같지만 TentacleDash/GrabKill 상태와 애니메이터가 달라 HideOrSeekPlayer를 재사용하지는 않는다.
//
// 이동 방식은 Rigidbody 물리 기반으로 확정됨(사용자 확인, GameRule.md v3.4) — HideOrSeekPlayer.cs와
// 동일한 패턴(로컬만 실제 물리 시뮬레이션, 원격은 isKinematic)을 그대로 따른다.
public class MonsterController : MonoBehaviour, IPunObservable, IRespawnable, IGameCharacter

{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Transform eyeSocket; // 3인칭 카메라 시선 높이 기준(머리 안쪽 지점)
    [SerializeField] private float cameraTargetHeight = 2.2f; // eyeSocket이 비어 있을 때만 쓰는 대체값
    [SerializeField] private float cameraDistance = 4.5f;
    [SerializeField] private float speed = 4f;
    [Tooltip("딛고 설 수 있는 면의 레이어(접지·돌진 지면 따라가기·벽 판정). 예전 obstructionMask(돌진 시작 장애물 검사)의 값을 이어받는다.")]
    [SerializeField, FormerlySerializedAs("obstructionMask")] private LayerMask groundLayer = 1;
    [SerializeField] private MonsterGrabKillTrigger grabKillTrigger;
    [SerializeField] private Animator animator;

    [Header("GrabKill Presentation")]
    [Tooltip("처형당하는 쿠키가 붙어 따라가는 본(Grab_Socket). GrabKill 클립이 이 본을 들어 올렸다 내리찍는다.")]
    [SerializeField] private Transform grabSocket;
    // 아래 진행도(0~1)는 원본 블렌더 파일의 분쇄 연출(Cookie_Ref·Crumb 오브젝트)을 GrabKill 클립(1~80프레임, 2.63초)에 맞춘 값이다
    // — 진행도 = (블렌더 프레임 - 1) / 79 (Bug-fix-plan.md §37).
    [Tooltip("두 손 사이에서 쿠키가 눌리기 시작하는 진행도(블렌더 47프레임).")]
    [SerializeField, Range(0f, 1f)] private float grabKillSquashStart = 0.582f;
    [Tooltip("쿠키가 가장 납작하게 눌린 진행도(블렌더 49프레임).")]
    [SerializeField, Range(0f, 1f)] private float grabKillSquashEnd = 0.608f;
    [Tooltip("쿠키가 작아지기 시작하는 진행도(블렌더 50프레임).")]
    [SerializeField, Range(0f, 1f)] private float grabKillShrinkStart = 0.620f;
    [Tooltip("쿠키 가루가 터지는 진행도(블렌더 51프레임).")]
    [SerializeField, Range(0f, 1f)] private float grabKillCrumbTime = 0.633f;
    [Tooltip("쿠키가 완전히 사라지는 진행도(블렌더 53프레임). 이때 숨기고 잡힌 쿠키는 관전으로 넘어간다.")]
    [SerializeField, Range(0f, 1f)] private float grabKillCrushNormalizedTime = 0.658f;

    private Rigidbody rb;
    private Vector3 moveInput; // Update()에서 입력만 기록, 실제 적용은 FixedUpdate에서
    private CharacterGroundDetector groundDetector; // 접지·지면 법선·벽 접촉(쿠키와 공용, Bug-fix-plan.md §41 ㊳·㊴)

    // 돌진 물리 스텝 상태(Bug-fix-plan.md §41 ㊳)
    private const float GroundCheckDistance = 0.3f;
    private const float GroundProbeLift = 0.05f; // 지면 탐침 구를 몸 아래 반구 중심보다 이만큼 올려 시작 겹침을 피한다
    private const float GroundedGap = 0.1f;      // 발밑 지면까지 이 거리 안이면 접지로 본다
    private float bodyRadius = 0.3f;
    private bool wasDashing;
    private Vector3 lastDashPosition;
    private float lastDashExpected;
    private readonly MonsterTentacleDash tentacleDash = new MonsterTentacleDash();
    private readonly NetworkTransformSync<MonsterMoveState> networkSync = new NetworkTransformSync<MonsterMoveState>();
    private MonsterMoveState currentState = MonsterMoveState.Idle;
    private MonsterMoveState previousState = MonsterMoveState.Idle;
    private MonsterViewSwitcher viewSwitcher; // 본인 클라이언트에서만 있다(GameFixPlan.md F6)

    // GrabKill 재생 시간(= 처형 트리거 쿨다운, GameRule.md v3.5). 예전에는 LateUpdate에서 Animator 상태
    // 이름 + normalizedTime으로 종료를 감지했는데, 같은 프레임 Update()가 곧바로 Idle/Walk로
    // 덮어써 GrabKill 트리거가 취소되고 종료 감지에 영원히 도달하지 못해 괴물이 한 판에 한 번만 처치할
    // 수 있었다(research.md §8.4). 클립 길이로 잰 타이머로 바꾸고, 재생 중에는 이동·상태 전환을 막는다.
    private const string GrabKillClipKeyword = "GrabKill";
    private const float FallbackGrabKillDuration = 2f;
    private float grabKillDuration = FallbackGrabKillDuration;
    private float grabKillRemaining;

    public bool IsGrabKilling => grabKillRemaining > 0f;

    // 처형 연출(CookieLifeStatePresenter)이 쓰는 값. 진행도는 이 클라이언트 Animator 기준이라 원격 괴물에서도 그 화면의 손 움직임과 맞는다.
    private static readonly int GrabKillStateHash = Animator.StringToHash(nameof(MonsterMoveState.GrabKill));
    public Transform GrabSocket => grabSocket;
    public float GrabKillSquashStart => grabKillSquashStart;
    public float GrabKillSquashEnd => grabKillSquashEnd;
    public float GrabKillShrinkStart => grabKillShrinkStart;
    public float GrabKillCrumbTime => grabKillCrumbTime;
    public float GrabKillCrushNormalizedTime => grabKillCrushNormalizedTime;
    public float GrabKillDuration => grabKillDuration;

    // GrabKill 상태를 재생 중이면 진행도(0~1), 아니면 -1.
    public float GrabKillProgress
    {
        get
        {
            if (animator == null) return -1f;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == GrabKillStateHash ? state.normalizedTime : -1f;
        }
    }

    // 촉수 돌진 애니메이션은 이동(TentacleDashDuration, 0.25초)보다 길다(클립 약 2초). 예전에는 이동이 끝나는 즉시
    // Walk/Idle로 바꿔 동작의 앞부분만 보였다(㉚-B). 이제 GrabKill처럼 클립 길이만큼 TentacleDash 상태를 유지하고
    // (이동은 처음 구간만, 나머지는 자리에서 마무리 — 그동안 이동·재돌진 입력 무시), 끝나면 Idle/Walk로 돌아간다.
    // 돌진 경로에 쿠키가 있으면 그 앞에서 멈추기만 한다 — 자동 처형은 없앴다(GameFixPlan.md F1, 잡기는 조준 + E).
    private const string TentacleDashClipKeyword = "TentacleDash";
    private const string CookieLayerName = "Cookie"; // 파괴된 쿠키는 BrokenCookie 레이어로 옮겨져 자동 제외된다
    private float tentacleDashAnimDuration;
    private float tentacleDashAnimRemaining;
    private int cookieLayerMask;
    private readonly RaycastHit[] dashSweepHits = new RaycastHit[8];

    public bool IsTentacleDashing => tentacleDashAnimRemaining > 0f;
    public float DashCooldownLeft => tentacleDash.CooldownLeft; // 스킬 칸(SkillHud, §26 H9)

    // 괴물 체격에 맞는 3인칭 카메라 설정 — 본인 조작 카메라와 관전 카메라(SpectatorController)가 같은 값을 쓴다.
    public float CameraTargetHeight => eyeSocket != null ? eyeSocket.position.y - transform.position.y : cameraTargetHeight;
    public float CameraDistance => cameraDistance;
    public bool IsLocallyControlled => pv != null && pv.IsMine;

    // IGameCharacter — 관전·카메라가 캐릭터 종류를 몰라도 되도록 하는 공통 계약(research.md §12 E1).
    public CharacterRole Role => CharacterRole.Monster;
    public PhotonView View => pv;
    public bool IsSpectatable => true;
    public bool CanInteract => !IsGrabKilling && !IsTentacleDashing && !IsStunned; // 처형·돌진 연출·기절 중에는 상호작용 불가
    private StunReceiver stun; // 도구에 맞아 기절(EscapePlan.md §1.8, D24 — 괴물은 세 도구 모두에 기절)
    public bool IsStunned => stun != null && stun.IsStunned;

    private void OnEnable()
    {
        CharacterRegistry.Register(this);
    }

    private void OnDisable()
    {
        CharacterRegistry.Unregister(this);
    }

    private void Awake()
    {
        groundDetector = new CharacterGroundDetector(groundLayer, GroundCheckDistance);
        stun = gameObject.AddComponent<StunReceiver>();
        stun.Init(5.2f);
        gameObject.AddComponent<MonsterAudio>().Init(this); // 괴물 소리(SoundPlan.md S4)
        if (EscapeManager.IsActive) gameObject.AddComponent<MonsterEscapeState>().Init(this);
        if (!pv.IsMine) return;

        // HideOrSeekPlayer.Awake()와 동일한 "Main Camera에 나를 넘긴다" 패턴 — 쿠키용 3인칭 궤도
        // 카메라를 그대로 재사용하되, 괴물 체격에 맞게 시선 높이/거리만 바꿔 넘긴다. 예전에는 씬에 없는
        // MonsterFirstPersonCamera를 ?.로 찾아 조용히 실패해 카메라가 고정돼 있었다(§23.4.1).
        var camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
        if (camCtrl == null)
        {
            Debug.LogError("MonsterController: Camera_Ctrl not found on Main Camera.");
            return;
        }

        camCtrl.InitCamera(gameObject, CameraTargetHeight, CameraDistance);

        // 1인칭/3인칭 선택(GameFixPlan.md F6). 기본값은 GameSettings.MonsterDefaultView.
        viewSwitcher = gameObject.AddComponent<MonsterViewSwitcher>();
        viewSwitcher.Init(this, eyeSocket != null ? eyeSocket : transform);
    }

    // 괴물 캐릭터는 괴물로 정해진 사람만 만든다(MonsterJoinController). 조작된 클라이언트가 PhotonNetwork.Instantiate로 직접 만든
    // 괴물은 방장이 지우고, 다른 화면은 숨긴다(AuditFixPlan.md §2 — 가짜 괴물로 처형을 보내는 경로 차단). 오프라인 시험은 제외.
    private bool RejectIfNotMonster()
    {
        if (PhotonNetwork.OfflineMode || !RoomState.IsInRoom() || pv == null || pv.Owner == null || !RoomState.HasMonster()) return false;
        if (RoomState.IsMonster(pv.Owner.ActorNumber)) return false;
        Debug.LogWarning($"[MonsterController] Actor {pv.Owner.ActorNumber} is not a monster but owns a monster character. Removing it.");
        if (PhotonNetwork.IsMasterClient) PhotonNetwork.Destroy(gameObject);
        else gameObject.SetActive(false);
        return true;
    }

    // 조준한 쿠키를 잡는다(GameFixPlan.md F1). CharacterInteractor가 E키 입력 때 다른 상호작용보다 먼저 부른다.
    public bool TryGrabAimTarget() => grabKillTrigger != null && grabKillTrigger.TryGrabAimTarget();
    public bool HasGrabAimTarget => grabKillTrigger != null && grabKillTrigger.AimTransform != null;

    private void Start()
    {
        if (RejectIfNotMonster()) return;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = !pv.IsMine; // 원격은 물리 시뮬레이션 끔(HideOrSeekPlayer.Start()와 동일 이유)
        if (pv.IsMine)
        {
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // TentacleDash 고속 이동 대비
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation; // 회전은 FixedUpdate()에서 rb.MoveRotation으로 직접 제어
        }

        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            Vector3 s = transform.lossyScale;
            bodyRadius = capsule.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        }

        grabKillDuration = FindClipLength(GrabKillClipKeyword, FallbackGrabKillDuration);
        tentacleDashAnimDuration = FindClipLength(TentacleDashClipKeyword, GameSettings.Current.TentacleDashDuration);
        cookieLayerMask = LayerMask.GetMask(CookieLayerName);
        if (cookieLayerMask == 0) Debug.LogWarning($"[MonsterController] Layer '{CookieLayerName}' not found. Tentacle dash cannot stop before cookies.");
    }

    private float FindClipLength(string keyword, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return fallback;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name.Contains(keyword)) return clip.length;
        }
        Debug.LogWarning($"[MonsterController] Animation clip containing '{keyword}' not found. Using {fallback}s.");
        return fallback;
    }

    private void Update()
    {
        if (!pv.IsMine)
        {
            networkSync.Interpolate(transform, Time.deltaTime);
            ChangeState(networkSync.RemoteState);
            return;
        }

        tentacleDash.TickCooldown(Time.deltaTime);

        if (IsGrabKilling)
        {
            // 처형 연출 중에는 제자리에서 GrabKill 상태를 유지한다 — 상태가 유지돼야 직렬화로 원격에도 전달된다.
            moveInput = Vector3.zero;
            grabKillRemaining -= Time.deltaTime;
            if (grabKillRemaining > 0f) return;

            grabKillRemaining = 0f;
            if (grabKillTrigger != null) grabKillTrigger.ResetTrigger();
            ChangeState(MonsterMoveState.Idle);
        }

        if (IsTentacleDashing)
        {
            // 돌진 동작이 끝날 때까지 TentacleDash 상태를 유지한다(이동 구간은 FixedUpdate의 돌진 이동이 맡음).
            moveInput = Vector3.zero;
            tentacleDashAnimRemaining -= Time.deltaTime;
            if (tentacleDashAnimRemaining > 0f) return;
            tentacleDashAnimRemaining = 0f;
        }

        if (IsStunned)
        {
            // 기절 중에는 움직이지 않는다(돌진·잡기도 불가).
            moveInput = Vector3.zero;
            ChangeState(MonsterMoveState.Idle);
            return;
        }

        moveInput = ReadCameraRelativeMoveInput();

        // 입력 키는 좌Shift로 확정(사용자 확인, GameRule.md v3.4) — InputBindings의 TentacleDashKey에서 바꿀 수 있다.
        // 쿠키의 Shift(질주)와는 서로 다른 캐릭터 클래스에서 각자 로컬로 읽는 입력이라 충돌 없음.
        if (PlayerInput.TentacleDashPressed && tentacleDash.TryStartDash(transform.forward))
        {
            tentacleDashAnimRemaining = Mathf.Max(tentacleDashAnimDuration, GameSettings.Current.TentacleDashDuration);
            ChangeState(MonsterMoveState.TentacleDash);
        }
        else if (moveInput != Vector3.zero && !tentacleDash.IsDashing)
            ChangeState(MonsterMoveState.Walk);
        else if (!tentacleDash.IsDashing)
            ChangeState(MonsterMoveState.Idle);
    }

    // Rigidbody 조작은 물리 스텝에서만(HideOrSeekPlayer.FixedUpdate()와 동일 관례).
    private void FixedUpdate()
    {
        if (!pv.IsMine) return;

        if (tentacleDash.IsDashing && FixedUpdateDash()) return;

        float verticalVelocity = rb.linearVelocity.y;
        if (wasDashing)
        {
            // 돌진이 끝난 첫 스텝: 오르막 돌진의 위 속도가 이어져 튀어 오르지 않게 한다(아래 속도는 중력에 맡긴다).
            wasDashing = false;
            verticalVelocity = Mathf.Min(verticalVelocity, 0f);
        }

        // transform.rotation 직접 대입 대신 rb.MoveRotation — Rigidbody 보간이 인식하는 회전만 갱신해야
        // 매 물리 스텝마다 회전이 튀지 않는다(쿠키에서 고친 Bug-fix-plan.md §13과 같은 이유).
        // 1인칭이면 몸은 카메라의 수평 방향을 바라본다(조준과 몸 앞 방향을 맞춤). 3인칭이면 이동 방향을 바라본다.
        if (viewSwitcher != null && viewSwitcher.IsFirstPerson)
            rb.MoveRotation(viewSwitcher.YawRotation);
        else if (moveInput != Vector3.zero)
            rb.MoveRotation(Quaternion.LookRotation(moveInput));

        Vector3 horizontal = moveInput * speed;
        // 공중에서 벽을 향해 밀면 마찰로 벽에 붙는다 — 쿠키와 같은 규칙으로 벽을 향한 성분을 뺀다(§41 ㊴).
        if (!groundDetector.IsGrounded(transform.position)) horizontal = groundDetector.RemoveWallPush(horizontal);
        rb.linearVelocity = new Vector3(horizontal.x, verticalVelocity, horizontal.z);
    }

    // 돌진 한 물리 스텝(Bug-fix-plan.md §41 ㊳). 지면 법선을 따라 꺾은 속도로 움직이고(오르막·내리막·요철), 볼록한 언덕 꼭대기에서는
    // 지면에 붙이며, 벽에 막히면 이동을 끝낸다. 충돌·연속 충돌 판정은 물리 엔진이 맡는다(예전 MovePosition은 지형을 뚫었다).
    // 이번 스텝을 돌진으로 처리했으면 true, 막혀서 방금 끝났으면 false(호출부가 일반 이동으로 이어서 처리).
    private bool FixedUpdateDash()
    {
        Vector3 position = rb.position;
        if (wasDashing) tentacleDash.ReportProgress(lastDashExpected, Vector3.Distance(position, lastDashPosition));
        if (!tentacleDash.IsDashing) return false;
        wasDashing = true;

        GameSettingsSO settings = GameSettings.Current;
        float dt = Time.fixedDeltaTime;
        // 한 스텝에 돌진 속도(초당 80m)로 1.6m를 가므로, 최대 경사의 내리막·언덕 꼭대기에서는 한 스텝 사이에 지면이
        // "스텝 길이 × tan(최대 경사)"만큼 떨어질 수 있다. 붙이기 거리를 그만큼 늘려야 경사를 따라 내려간다(고정 0.6m로는 약 20°까지만 따라가
        // 실측에서 3~8m 떠올랐다). 그보다 급한 낙차(낭떠러지)는 붙이지 않고 떨어진다.
        float snapDistance = settings.TentacleDashGroundSnap + tentacleDash.DashSpeed * dt * Mathf.Tan(settings.TentacleDashMaxSlope * Mathf.Deg2Rad);
        bool nearGround = TryProbeGround(position, snapDistance, out float gap, out Vector3 groundNormal);

        Vector3 velocity = tentacleDash.DashVelocity(nearGround, groundNormal, settings.TentacleDashMaxSlope);
        if (nearGround)
        {
            // 경사를 따르되, 발밑과 지면 사이가 벌어졌으면(초속 80m라 지형 곡률만으로도 한 스텝에 0.1~0.5m 뜬다) 그 틈을
            // 이번 스텝에 메우는 아래 속도를 더한다. 예전에는 "직전 스텝이 접지일 때만" 붙여서, 한 번 뜨면 경사 방향의 위 속도를
            // 그대로 가진 채 언덕 꼭대기에서 8m 넘게 튀어 올랐다.
            if (gap > GroundedGap) velocity.y -= gap / dt;
        }
        else
        {
            // 낭떠러지 등 붙이기 거리 밖: 중력에 맡기되 위 속도는 남기지 않는다(돌진이 발사대가 되지 않게).
            velocity.y = Mathf.Min(rb.linearVelocity.y, 0f);
        }

        bool stopBeforeCookie = TryStopBeforeCookieOnDashPath(velocity * dt, out float travel);
        if (stopBeforeCookie)
            velocity = velocity.normalized * (travel / dt); // 쿠키 앞까지만 움직인다(처형하지 않음, GameFixPlan.md F1)

        rb.linearVelocity = velocity;
        lastDashPosition = position;
        lastDashExpected = velocity.magnitude * dt;
        tentacleDash.TickDash(dt);
        if (stopBeforeCookie) tentacleDash.Cancel(); // 돌진 이동만 끝낸다 — 애니메이션 마무리는 그대로 이어진다
        return true;
    }

    // 발밑 지면까지의 거리와 법선. 몸 아래 반구 중심보다 조금 위에서 몸보다 작은 구를 내려 훑는다(시작 겹침 방지).
    private bool TryProbeGround(Vector3 feet, float snapDistance, out float gap, out Vector3 normal)
    {
        float probeRadius = bodyRadius * 0.9f;
        float lift = bodyRadius + GroundProbeLift;
        float startClearance = lift - probeRadius; // 탐침 구 바닥과 발바닥 사이
        if (groundDetector.TryGetGround(feet + Vector3.up * lift, probeRadius, startClearance + snapDistance, out RaycastHit hit))
        {
            gap = Mathf.Max(0f, hit.distance - startClearance);
            normal = hit.normal;
            return true;
        }
        gap = float.PositiveInfinity;
        normal = Vector3.up;
        return false;
    }

    // 벽 접촉을 모은다(공중에서 벽에 붙지 않게, §41 ㊴). 물리 소유자만.
    private void OnCollisionStay(Collision collision)
    {
        if (pv.IsMine) groundDetector.RecordContacts(collision);
    }

    // 이번 스텝의 돌진 경로에 살아 있는 쿠키(스파이 포함)가 있으면 그 앞까지의 거리를 돌려준다. 처형은 하지 않는다
    // (GameFixPlan.md F1). 초속 80m라 한 스텝에 1m 넘게 움직여, 그대로 부딪히면 물리 엔진이 쿠키를 튕겨 날릴 수 있다.
    private const float DashStopMargin = 0.1f;

    private bool TryStopBeforeCookieOnDashPath(Vector3 step, out float travel)
    {
        travel = 0f;
        float distance = step.magnitude;
        if (cookieLayerMask == 0 || distance <= 0f) return false;

        // 충돌 캡슐(반지름 약 0.31m)은 겉모습(앞면 약 1.87m)보다 훨씬 작다. 캡슐 기준으로 멈추면 겉보기에 몸이 쿠키를
        // 덮으므로, 겉모습 앞면이 쿠키에 닿기 직전에 멈춘다. 그러면 쿠키는 바로 잡기 거리 안에 들어온다.
        // 그만큼 앞을 미리 살펴야 이번 스텝에 이미 너무 가까이 가는 일이 없다.
        float lookAhead = (grabKillTrigger != null ? Mathf.Max(0f, grabKillTrigger.BodyFrontOffset - bodyRadius) : 0f) + DashStopMargin;

        Vector3 direction = step / distance;
        Vector3 origin = rb.position + Vector3.up * (bodyRadius + GroundProbeLift);
        int count = Physics.SphereCastNonAlloc(origin, bodyRadius, direction, dashSweepHits, distance + lookAhead,
            cookieLayerMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var cookie = dashSweepHits[i].collider.GetComponentInParent<HideOrSeekPlayer>();
            if (cookie == null || cookie.gameObject == gameObject || cookie.IsBroken) continue;
            nearest = Mathf.Min(nearest, dashSweepHits[i].distance);
        }
        if (nearest == float.MaxValue) return false;

        travel = Mathf.Clamp(nearest - lookAhead, 0f, distance);
        return true;
    }

    // 쿠키와 같은 카메라 기준 수평 이동 방향(3인칭 궤도 카메라 전제, PlayerInput 공용).
    private Vector3 ReadCameraRelativeMoveInput()
    {
        Camera cam = Camera.main;
        return PlayerInput.CameraRelativeMove(cam != null ? cam.transform : null);
    }

    // 맵 밖으로 떨어졌을 때 VoidKillZone 또는 FallGuard가 호출한다(Bug-fix-plan.md §30.4) — 예전에는 괴물용 복귀
    // 경로가 없어 무한 낙하했다. 쿠키(HideOrSeekPlayer.RespawnToSpawnPoint)와 같은 순간이동 규칙을 따른다.
    public void RespawnToSpawnPoint()
    {
        if (rb == null) return;
        if (!SceneSpawnPoints.TryFindClearPosition(SceneSpawnPoints.Monster, GameSettings.Current.MonsterSpawnRange, out Vector3 respawnPos))
            return;

        tentacleDash.Cancel();
        tentacleDashAnimRemaining = 0f;
        wasDashing = false;
        rb.linearVelocity = Vector3.zero;
        // 비키네마틱 Rigidbody는 transform만 바꾸면 다음 물리 스텝에 되돌아가므로 rb.position도 함께 바꾼다.
        rb.position = respawnPos;
        transform.position = respawnPos;
        Debug.Log($"[MonsterController] Respawned at {respawnPos} after falling out of the map.");
    }

    // MonsterGrabKillTrigger가 근접 자동 처형 순간 호출 — 전원에게 GrabKill 애니메이션 재생.
    public void PlayGrabKill()
    {
        // 돌진 중이거나 돌진 마무리 동작 중에 처형이 시작되면 돌진을 끊고 GrabKill을 우선한다(§34).
        tentacleDash.Cancel();
        tentacleDashAnimRemaining = 0f;
        grabKillRemaining = grabKillDuration;
        ChangeState(MonsterMoveState.GrabKill);
    }

    // 상태가 바뀔 때(이전, 새) — 괴물 소리(MonsterAudio). 본인·원격 모두 이 경로로 상태가 바뀐다.
    public event System.Action<MonsterMoveState, MonsterMoveState> StateChanged;
    public MonsterMoveState CurrentMoveState => currentState;

    private void ChangeState(MonsterMoveState newState)
    {
        if (animator == null || previousState == newState) return;

        animator.ResetTrigger(previousState.ToString());
        animator.SetTrigger(newState.ToString());
        MonsterMoveState old = previousState;
        previousState = newState;
        currentState = newState;
        StateChanged?.Invoke(old, newState);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
            networkSync.Write(stream, transform, currentState);
        else
            networkSync.Read(stream, transform);
    }
}
