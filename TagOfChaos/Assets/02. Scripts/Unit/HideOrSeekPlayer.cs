using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 쿠키 캐릭터 조정자. 이동·점프·회피 물리와 동기화를 맡고, 입력(PlayerInput)·접지(CharacterGroundDetector)·
// 애니메이션(PlayerAnimationDriver)·캐리 추종(PlayerCarryFollower)은 협력 객체에 맡긴다.
// 캐릭터 공통 계약(IGameCharacter·IRespawnable)으로 관전·카메라·낙하 복귀가 종류와 무관하게 동작한다.
public class HideOrSeekPlayer : MonoBehaviourPunCallbacks, IPunObservable, IRespawnable, IGameCharacter
{
    [Header("Options")]
    [SerializeField] private float speed;
    [SerializeField] private float jumpPower;
    [SerializeField] private LayerMask groundLayer = 1; // 착지 판정용 지형 레이어 (기본: Default)
    [SerializeField] private float groundCheckOffset = 0.3f; // 레이캐스트 시작 높이 및 최소 검사 거리
    [SerializeField] private float jumpFreezeNormalizedTime = 0.5f; // 착지 전까지 Jump 애니메이션을 멈춰둘 재생 지점
    [SerializeField] private float dodgeDuration = 0.5f; // 회피 지속 시간

    [Header("Components")]
    [SerializeField] private PhotonView pv;

    private float baseSpeed;
    private const float DodgeSpeedMultiplier = 2f;
    // 낼 수 있는 가장 빠른 속도(회피) — 방장이 위치 조작을 감지할 때 쓴다(S-15).
    public float MaxMoveSpeed => Mathf.Max(speed, baseSpeed) * DodgeSpeedMultiplier * Mathf.Max(1f, SpeedBoost);

    // 인라인스케이트(Request1009Plan.md §25.5 I): 모든 화면이 같은 명중 알림으로 켠다 — 방장의 이동 검사(MaxMoveSpeed)도 이 배율을 안다.
    private float speedBoost = 1f;
    private float speedBoostUntil = -1f;
    public float SpeedBoost => Time.time < speedBoostUntil ? speedBoost : 1f;

    public void ApplySpeedBoost(float multiplier, float seconds)
    {
        speedBoost = Mathf.Max(1f, multiplier);
        speedBoostUntil = Time.time + seconds;
    }

    // 호박마차(§25.5 I): 본인 화면에서 순간이동. 물리 위치로 옮겨야 Rigidbody가 되돌리지 않는다.
    public void TeleportTo(Vector3 position)
    {
        if (!IsMine) return;
        if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        rb.position = position;
        transform.position = position;
    }

    public void TeleportAfter(Vector3 position, float delay)
    {
        if (IsMine) StartCoroutine(TeleportRoutine(position, delay));
    }

    private System.Collections.IEnumerator TeleportRoutine(Vector3 position, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!IsMovementLocked) TeleportTo(position);
    }

    private HatFlight hatFlight;
    public bool IsFlying => hatFlight != null && hatFlight.IsFlying;

    [Header("States")]
    [SerializeField] private bool isJump;
    [SerializeField] private bool isDodge;
    [SerializeField] private bool keepMovingAfterDodge;

    private Vector3 rotation;
    private Vector3 rotation_value;
    private Vector3 dodgeRotation;
    private Vector3 dodgeMoveDir;
    private float dodgeTimer;
    private bool jumpRequested; // Update()에서 입력만 기록, 실제 점프 적용은 FixedUpdate()에서(PlayerControllPlan.md §18.3)

    private Animator animator;
    private Rigidbody rb;
    private CharacterGroundDetector groundDetector;
    private PlayerAnimationDriver animationDriver;
    private PlayerNetworkSync networkSync;
    private PlayerGrabController grabController;
    private PlayerCarryFollower carryFollower;
    private CarriedBodyPresenter carriedBody;
    private CookieLifeStatePresenter lifePresenter;

    // 그랩버 화면이 들린 쿠키에게 바로 알려 두는 값 — 들린 사람의 동기화(RemoteCarrierViewId)가 도착할 때까지 쓴다.
    private const float CarrierOverrideSeconds = 0.6f;
    private int carrierOverrideId = -1;
    private float carrierOverrideUntil = -1f;

    // 다른 클래스가 이 캐릭터에 보내는 RPC 이름 — 메서드 이름을 바꾸면 컴파일 단계에서 함께 바뀐다(research.md §12 E3).
    public const string RpcOnGrabbedByOwner = nameof(OnGrabbedByOwner);
    public const string RpcOnReleased = nameof(OnReleased);
    public const string RpcRequestGrabKill = nameof(RequestGrabKill);
    public const string RpcOnGrabRejected = nameof(OnGrabRejected);

    // 이 쿠키가 다른 쿠키를 들고 있는지(본인은 자기 상태, 원격은 동기화 값) — 들기 대상 고르기·거절에 쓴다.
    public bool IsCarryingSomeone => pv != null && pv.IsMine ? grabController != null && grabController.IsCarrying : networkSync != null && networkSync.RemoteIsCarrying;

    // 이 쿠키 자신의 상태로 이동이 잠겼는지(파괴·들림). 채팅 같은 입력 차단은 캐릭터 종류와 무관하게 PlayerInput.IsGameplaySuppressed가
    // 맡는다 — 예전에는 채팅이 이 값을 직접 켜 쿠키만 잠겼고, 잠긴 동안 Update가 멈춰 걷기 애니메이션이 남았다(Bug-fix-plan.md §41 ㊵).
    // 탈출·로켓 탑승한 몸은 숨겨지고 물리가 멈추므로(EscapeCharacterState) 이동도 잠근다.
    public bool IsMovementLocked => IsBroken || awaitingDeathUntil > 0f || (carryFollower != null && carryFollower.IsCarried) || (stun != null && stun.IsStunned)
                                    || (pv != null && pv.Owner != null && EscapeManager.HasLeftMap(pv.Owner.ActorNumber))
                                    || IsFlying; // 헬리콥터 모자 비행은 HatFlight가 움직인다(§25.5 H)
    private StunReceiver stun; // 도구에 맞아 기절(EscapePlan.md §1.8)

    // 외부에서 "이 인스턴스가 내 캐릭터인지" 판별할 수단 (GameManager의 채팅 이동잠금이 참조)
    public bool IsMine => pv != null && pv.IsMine;
    public bool IsLocallyControlled => IsMine;

    // IGameCharacter — 관전·카메라가 캐릭터 종류를 몰라도 되도록 하는 공통 계약(research.md §12 E1).
    public CharacterRole Role => CharacterRole.Cookie;
    public PhotonView View => pv;
    // 괴물에게 붙잡혀 부서지는 중인 쿠키도 부서지는 순간까지 관전 대상으로 남긴다 — 파괴 판정은 잡힌 즉시 기록되지만 관전자도
    // 잡힌 본인처럼 분쇄 연출을 끝까지 보게 한다(Bug-fix-plan.md §38).
    public bool IsSpectatable => pv != null && pv.Owner != null && !EscapeManager.HasLeftMap(pv.Owner.ActorNumber) // 탈것에 탔거나 탈출한 쿠키는 숨겨진다
        && (!RoomState.IsBroken(pv.Owner) || (lifePresenter != null && lifePresenter.IsBeingGrabKilled));
    public bool CanInteract => !IsMovementLocked; // 파괴·들림 중에는 상호작용 불가(채팅 중에는 InteractPressed가 억제된다)
    public float CameraTargetHeight => Camera_Ctrl.CookieTargetHeight;
    public float CameraDistance => -1f; // Camera_Ctrl 인스펙터 기본 거리

    // 파괴됐는지 — 방장이 처형·마녀를 확정해 쓴 HitCount로만 정한다(AuditFixPlan.md §8.1). 예전에는 본인이 자기 값을 써서
    // 조작된 클라이언트는 잡혀도 죽지 않을 수 있었다.
    // 파괴는 맵에서만 의미가 있다 — 대기실로 돌아온 직후에는 이전 판 HitCount가 방장의 초기화(RoundStateResetter)가 도착할 때까지
    // 잠깐 남아 있어, 대기실 쿠키가 잠기고 관전으로 넘어가던 문제(2026-10-12 멀티 시험)를 막는다.
    public bool IsBroken => EscapeManager.IsActive && pv != null && pv.Owner != null && RoomState.IsBroken(pv.Owner);

    // 본인: 붙잡혀 처형 연출을 시작했지만 방장의 확정(HitCount)을 기다리는 중 — 이 시각까지 확정이 없으면(방장이 거절) 풀린다.
    private const float GrabKillConfirmTimeout = 4f;
    private float awaitingDeathUntil = -1f;

    public void SetCarry(CarryReason reason, bool on) => animationDriver.SetCarry(reason, on);

    // 이 쿠키를 들고 있는 쿠키의 ViewID(-1 = 안 들림). 본인은 자기 상태, 남의 화면은 그랩버 로컬 값 → 동기화 값 순.
    public int CurrentCarrierViewId
    {
        get
        {
            if (pv != null && pv.IsMine) return carryFollower != null ? carryFollower.CarrierViewId : -1;
            if (Time.time < carrierOverrideUntil) return carrierOverrideId;
            return networkSync != null ? networkSync.RemoteCarrierViewId : -1;
        }
    }

    public void SetLocalCarrierOverride(int carrierViewId)
    {
        carrierOverrideId = carrierViewId;
        carrierOverrideUntil = Time.time + CarrierOverrideSeconds;
    }

    // 다른 쿠키가 나를 들었다(드는 쪽 → 내 주인에게만). 보낸 사람이 그 드는 쪽 캐릭터의 주인이고, 내가 이미 들렸거나 누군가를 들고 있지 않고,
    // 가까울 때만 받아들인다(F-01). 거절하면 드는 쪽에 알려 그 화면의 들기를 되돌린다.
    [PunRPC]
    private void OnGrabbedByOwner(int newCarrierViewId, PhotonMessageInfo info)
    {
        if (pv == null || !pv.IsMine) return;
        PhotonView carrierView = PhotonView.Find(newCarrierViewId);
        bool senderOwnsCarrier = carrierView != null && carrierView != pv && info.Sender != null && carrierView.Owner == info.Sender
                                 && carrierView.GetComponent<HideOrSeekPlayer>() != null;
        float distance = carrierView != null ? Vector3.Distance(carrierView.transform.position, transform.position) : float.MaxValue;
        float range = grabController != null ? grabController.GrabRange : 1.5f;
        if (!NetSecurityRules.CanAcceptCarry(IsBroken || awaitingDeathUntil > 0f, carryFollower.IsCarried, IsCarryingSomeone, senderOwnsCarrier, distance, range)
            || EscapeManager.HasLeftMap(pv.Owner.ActorNumber))
        {
            if (carrierView != null && info.Sender != null) carrierView.RPC(RpcOnGrabRejected, info.Sender, pv.ViewID);
            return;
        }
        if (carryFollower.TryAttach(newCarrierViewId)) animationDriver.ChangeState(PlayerMoveState.Carried);
    }

    // 나를 들고 있는 쪽만 내려놓을 수 있다(F-01 — 예전에는 아무나 보내도 떨어졌다).
    [PunRPC]
    private void OnReleased(PhotonMessageInfo info)
    {
        if (pv == null || !pv.IsMine || !carryFollower.IsCarried) return;
        PhotonView carrierView = PhotonView.Find(carryFollower.CarrierViewId);
        if (carrierView != null && info.Sender != null && carrierView.Owner != info.Sender) return;
        ReleaseFromCarrierLocally();
    }

    // 내가 들려던 쿠키가 거절했다(드는 쪽 캐릭터에서 받는다). 보낸 사람이 그 쿠키의 주인일 때만.
    [PunRPC]
    private void OnGrabRejected(int targetViewId, PhotonMessageInfo info)
    {
        if (pv == null || !pv.IsMine || grabController == null) return;
        PhotonView target = PhotonView.Find(targetViewId);
        if (target == null || target.Owner != info.Sender) return;
        grabController.OnGrabRejected(targetViewId);
    }

    // 파괴 상태면 키네마틱·잠금을 그대로 유지하고, 아니면 물리와 Idle 애니메이션을 되돌린다.
    private void ReleaseFromCarrierLocally()
    {
        if (carryFollower.Detach(restorePhysics: !IsBroken) && !IsBroken)
            animationDriver.ChangeState(PlayerMoveState.Idle);
    }

    // 들린 동안 매 FixedUpdate마다 드는 쪽 두 손 위를 따라간다. 드는 쪽이 방을 나갔거나 파괴돼 사라졌으면
    // 공중에 고정된 채 남지 않도록 스스로 내려온다.
    private bool TryFollowCarrier()
    {
        if (!carryFollower.IsCarried) return false;
        if (carryFollower.TryFollow()) return true;
        ReleaseFromCarrierLocally();
        return false;
    }

    // 괴물 소유 클라이언트가 전원에게 보낸다(MonsterGrabKillTrigger). 모든 클라이언트가 쿠키를 그 괴물의 Grab_Socket에 붙여 처형 연출을
    // 보여 주고(CookieLifeStatePresenter), 파괴 확정은 본인 클라이언트만 한다(Bug-fix-plan.md §36).
    [PunRPC]
    private void RequestGrabKill(int monsterViewId, PhotonMessageInfo info)
    {
        if (pv == null) return;
        // 보낸 사람이 그 괴물 캐릭터의 주인인지 확인한다 — 아무 클라이언트나 쿠키를 파괴할 수 없게(research.md R4.10-2).
        PhotonView monsterView = PhotonView.Find(monsterViewId);
        MonsterController monster = monsterView != null ? monsterView.GetComponent<MonsterController>() : null;
        if (monster == null || info.Sender == null || monsterView.Owner != info.Sender)
        {
            Debug.LogWarning($"[HideOrSeekPlayer] Ignored GrabKill on view {pv.ViewID}: sender {info.Sender?.ActorNumber} does not own monster view {monsterViewId}.");
            return;
        }
        if (IsBroken) return;

        // 방장이 확정을 검증한다 — 처형 연출이 몸을 괴물 손으로 옮기기 전, 방장 화면의 실제 위치로(S-06).
        // 확정하면 방장이 HitCount·DeathCause·CatchCount를 쓰고, 아이템은 EscapeAuthority.OnPlayerBroken이 그 자리에 떨어뜨린다(D11).
        if (PhotonNetwork.IsMasterClient) GrabKillAuthority.TryConfirm(this, monster, info.Sender);

        // 붙잡힘 연출을 먼저 시작해야 곧이어 오는 HitCount 변경에 즉시 사라지지 않는다.
        bool presented = lifePresenter != null && lifePresenter.BeginGrabKill(monster);

        if (!pv.IsMine) return;

        // 본인: 연출만 맞춘다. 파괴 확정은 방장이 쓴 HitCount로 온다(OnPlayerPropertiesUpdate). 확정이 오지 않으면 풀린다.
        awaitingDeathUntil = Time.time + GrabKillConfirmTimeout;

        // 들고 있던 쿠키는 내려놓고, 들려 있었다면 캐리 관계를 끊는다.
        if (grabController != null) grabController.Release();
        ReleaseFromCarrierLocally();

        // 괴물 손에 붙잡혀 들려 가는 동안은 매달려 떠 있는 모션(Held — Cookie_Hanging_Idle)을 재생한다(쿠키끼리 들 때는 Carried).
        // 상태는 위치 동기화로 원격 화면에도 전달된다. 부서지는 순간 몸이 숨겨지므로 따로 되돌리지 않는다(Bug-fix-plan.md §36).
        animationDriver.ChangeState(presented ? PlayerMoveState.Held : PlayerMoveState.Broken);

        // 파괴된 몸은 CookieLifeStatePresenter가 모든 클라이언트에서 렌더러·콜라이더를 끄고 비충돌 레이어로 옮긴다. 콜라이더가 꺼진 채
        // 중력을 받으면 바닥을 뚫고 떨어지므로 물리도 멈춘다(research.md §8.11).
        if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        // 관전 전환은 파괴가 확정된 뒤 CookieLifeStatePresenter가 한다(부서지는 순간 또는 확정이 도착한 순간 중 늦은 쪽).
    }

    // 방장이 내 파괴를 확정했다(처형 또는 마녀): 들기 관계를 끊고 물리를 멈춘다. 관전 전환은 CookieLifeStatePresenter.
    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
    {
        if (pv == null || !pv.IsMine || targetPlayer != pv.Owner || !changedProps.ContainsKey(NetKeys.HitCount) || !IsBroken) return;
        awaitingDeathUntil = -1f;
        if (grabController != null) grabController.Release();
        ReleaseFromCarrierLocally();
        if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    // 처형 확정이 오지 않았다(방장이 거절했거나 방장이 바뀌는 사이 사라짐): 다시 움직일 수 있게 되돌린다.
    private void CancelAwaitedDeath()
    {
        awaitingDeathUntil = -1f;
        if (IsBroken) return;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        animationDriver.ChangeState(PlayerMoveState.Idle);
        Debug.LogWarning($"[HideOrSeekPlayer] Grab kill on view {pv.ViewID} was not confirmed by the master. Released.");
    }

    private void Awake()
    {
        // Photon의 네트워크 디스패치(OnPhotonSerializeView)는 Unity의 Awake→Start 순서와 무관하게
        // 별도 루프(PhotonHandler.Dispatch())에서 호출될 수 있어, 이 오브젝트의 Start()가 아직 실행되기
        // 전에 송수신이 먼저 일어날 수 있다(Bug-fix-plan.md §12). OnPhotonSerializeView가 쓰는 협력 객체
        // (networkSync, animationDriver)와 Rigidbody 참조는 IsMine 여부와 상관없이 Awake에서 가장 먼저
        // 만든다 — animationDriver가 Start에 남아 있어 실제 빌드에서 NRE가 났다(Bug-fix-plan.md §24.6 ⑰-C).
        networkSync = new PlayerNetworkSync();
        animator = GetComponent<Animator>();
        animationDriver = new PlayerAnimationDriver(animator, jumpFreezeNormalizedTime);
        gameObject.AddComponent<CookieAudio>().Init(this, animationDriver); // 캐릭터 소리(SoundPlan.md S4)
        groundDetector = new CharacterGroundDetector(groundLayer, groundCheckOffset);
        rb = GetComponent<Rigidbody>();
        grabController = GetComponent<PlayerGrabController>();
        carriedBody = gameObject.AddComponent<CarriedBodyPresenter>();
        carriedBody.Init(this, animator);
        carryFollower = new PlayerCarryFollower(gameObject, rb, carriedBody);
        lifePresenter = GetComponent<CookieLifeStatePresenter>();
        stun = gameObject.AddComponent<StunReceiver>();
        stun.Init(2.3f);
        AttachEscapeMode();

        if (!pv.IsMine) return;

        Camera_Ctrl camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
        if (camCtrl != null)
            camCtrl.InitCamera(gameObject);
    }

    private void Start()
    {
        baseSpeed = speed;
        if (animator != null)
            animator.applyRootMotion = false; // 이동은 전부 Move()가 Rigidbody 속도를 직접 갱신하므로, 클립에 내장된 루트 모션이 겹쳐 적용되면 안 됨

        // 물리 엔진(Rigidbody) 도입(PlayerControllPlan.md §18.3) — 로컬 소유 캐릭터만 실제 물리
        // 시뮬레이션을 받는다. 원격 캐릭터는 지금처럼 networkSync가 transform을 직접 보간하므로,
        // Rigidbody가 동시에 중력/충돌로 같은 transform을 건드리면 두 시스템이 충돌한다 — 그래서
        // 원격 인스턴스는 isKinematic으로 물리를 꺼둔다.
        rb.isKinematic = !pv.IsMine;
        if (pv.IsMine)
        {
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 빠른 낙하/점프 시 얇은 바닥·난간을 뚫고 지나가는 것 방지
            rb.interpolation = RigidbodyInterpolation.Interpolate; // FixedUpdate 사이 시각적 끊김 완화
            rb.constraints = RigidbodyConstraints.FreezeRotation;  // 회전은 transform.LookAt으로 직접 제어, 물리 충돌로 인한 회전(넘어짐)은 막음
        }

        // Mesh_0(캐릭터 몸통 메시, 옛 Ch36에서 Cookie 마이그레이션으로 개명됨, PlayerControllPlan.md §25/§26)은
        // 콘케이브 메시 콜라이더 에러를 피하려고 별도의 키네마틱 Rigidbody를 갖고 있는데(§21), 그 결과
        // 루트의 CapsuleCollider와는 서로 다른 물리 바디가 되어버려 매 물리 스텝마다 자기 자신과 충돌
        // 판정을 일으키고 있었다 — 위치는 고정된 채 속도만 거대하고 불규칙해지는 원인이었다
        // (Bug-fix-plan.md §14). 같은 캐릭터의 일부이므로 명시적으로 서로의 충돌을 무시한다(로컬/원격
        // 모두 동일한 구조라 IsMine 여부와 무관하게 적용).
        Collider rootCollider = GetComponent<CapsuleCollider>();
        Collider bodyMeshCollider = transform.Find(BodyMeshName)?.GetComponent<Collider>();
        if (rootCollider != null && bodyMeshCollider != null)
        {
            Physics.IgnoreCollision(rootCollider, bodyMeshCollider, true);
            // Mesh_0은 칠하기 레이캐스트(Collider.Raycast) 전용이다 — 물리 접촉은 모두 끈다(Request1009Plan.md §22.6 M5). 바인드 포즈(팔 벌린 자세,
            // 너비 1.88 m)에 고정돼 남의 화면에서는 보이는 몸보다 넓은 벽이 됐고, 들리면 몸과 함께 누워 길을 막았다.
            // excludeLayers는 접촉만 거르고 검사(괴물 조준·시야·바닥 맞춤)는 그대로 이 콜라이더를 맞힌다.
            bodyMeshCollider.excludeLayers = ~0;
        }
        else
            Debug.LogError($"[HideOrSeekPlayer] '{BodyMeshName}' collider or root CapsuleCollider is missing on {name}.");
    }

    private const string BodyMeshName = "Mesh_0";

    // 탈출 모드 맵이면(EscapeManager가 있으면) 인벤토리·손에 든 아이템·탈출 상태를 붙인다(EscapePlan.md §5.4).
    private void AttachEscapeMode()
    {
        EscapeManager escape = EscapeManager.Instance;
        if (escape == null || !escape.isActiveAndEnabled) return;
        gameObject.AddComponent<HeldItemPresenter>().Init(escape, pv);
        gameObject.AddComponent<EscapeCharacterState>().Init(escape, this);
        hatFlight = gameObject.AddComponent<HatFlight>();
        hatFlight.Init(escape, this, rb);
        gameObject.AddComponent<SkateVisual>().Init(this);
        if (pv.IsMine) gameObject.AddComponent<PlayerInventory>().Init(this, escape);
    }


    public override void OnEnable()
    {
        base.OnEnable();
        CharacterRegistry.Register(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        CharacterRegistry.Unregister(this);
    }

    private void Update()
    {
        if (!pv.IsMine)
        {
            UpdateRemote();
            return;
        }

        if (awaitingDeathUntil > 0f && (IsBroken || Time.time >= awaitingDeathUntil))
        {
            if (IsBroken) awaitingDeathUntil = -1f;
            else CancelAwaitedDeath();
        }

        if (IsMovementLocked)
        {
            OnLocalMovementLocked();
            return;
        }

        CheckMovementInput(); // 이동 입력 체크 + 회전 갱신(좌표 이동은 FixedUpdate의 Move()가 담당)
        CheckJumpInput(); // 점프 입력 체크(의도만 기록, 실제 적용은 FixedUpdate)
        CheckDodgeInput(); // 회피 입력 체크
        animationDriver.HandleJumpAnimationHold(); // 착지 전까지 Jump 애니메이션이 끝까지 재생되지 않도록 고정
    }

    // 원격 아바타는 위치·회전·애니메이션을 동기화 값으로 따라간다. 기절 중에도 따라간다 — 예전에는 기절(이동 잠금)이면 보간까지 멈춰
    // 다른 화면에서 얼어 있다가 풀리면 순간이동했다(research.md R4.7-10). 처형 중에는 처형 연출(CookieLifeStatePresenter.LateUpdate)이
    // 위치를 맡으므로 보간하지 않는다(R4.7-6).
    private void UpdateRemote()
    {
        // 들린 몸은 CarriedBodyPresenter가 이 화면의 그랩버 손 위에 놓으므로 보간하지 않는다(이중 지연 제거, Request1009Plan.md §16.3).
        if ((lifePresenter == null || !lifePresenter.IsBeingGrabKilled) && CurrentCarrierViewId < 0)
            networkSync.Interpolate(transform, Time.deltaTime);
        animationDriver.ChangeState(networkSync.RemoteState);
        animationDriver.SetCarry(CarryReason.Cookie, networkSync.RemoteIsCarrying);
    }

    // 본인 캐릭터가 들림·기절·파괴·탑승으로 잠긴 동안. 회피 중이었으면 끝낸다 — 예전에는 회피 타이머가 멈춘 채 남아, 풀린 뒤
    // 옛 방향으로 2배 속도로 밀려났다(research.md R5-14). 기절이면 서 있는 자세로 돌린다(걷던 자세가 굳지 않게).
    private void OnLocalMovementLocked()
    {
        if (isDodge) DodgeOut();
        jumpRequested = false;
        if (stun != null && stun.IsStunned && !IsBroken && !carryFollower.IsCarried)
        {
            animationDriver.ResumePlayback();
            animationDriver.ChangeState(PlayerMoveState.Idle);
        }
    }

    // Rigidbody 조작은 물리 스텝(FixedUpdate)에서만 수행한다(Unity 관례) — 입력은 Update()에서 이미 읽어뒀다.
    private void FixedUpdate()
    {
        if (!pv.IsMine) return;
        if (TryFollowCarrier()) return; // 그랩당한 동안은 일반 이동 로직 대신 캐리 위치만 추적
        if (IsMovementLocked)
        {
            // 기절 순간의 수평 속도가 남아 미끄러지지 않게 멈춘다(research.md R5-19). 키네마틱(파괴·탑승)이면 건드리지 않는다.
            if (!rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        bool grounded = groundDetector.IsGrounded(transform.position);

        // 점프 여부와 무관하게 매 스텝 접지 확인 — 점프 없이 걸어서 맵 밖으로 나가도 이제는
        // 정상적으로 낙하한다(과거엔 ApplyGravity()가 isJump일 때만 동작해 아예 안 떨어졌음, §18.2).
        if (isJump && grounded && rb.linearVelocity.y <= 0f) // 상승 중엔 착지 처리하지 않음(막 점프한 순간 오탐 방지)
        {
            isJump = false;
            animationDriver.ResumePlayback(); // 공중에서 멈춰뒀던 애니메이션 재생 속도 복구
        }

        // 의도한 점프(Space)든 걸어서 벗어난 낙하든, 공중에서는 항상 이동 입력에 자유롭게 반응한다
        // (PlayerControllPlan.md §24 — 기존에는 의도한 점프만 시작 시점 방향으로 고정됐었으나,
        // §23.4에서 낙하에 이미 적용한 자유 공중 조작과 동일하게 통일했다).
        if (jumpRequested && grounded && !isDodge)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpPower, rb.linearVelocity.z);
            isJump = true;
            animationDriver.ReplayJump(); // 연타로 재점프해도 항상 처음부터 재생(Bug-fix-plan.md §15)
        }
        else if (!isJump && !grounded && !isDodge) // 점프 없이 걸어서 가장자리를 벗어나 낙하가 시작된 경우
        {
            isJump = true;
            animationDriver.ReplayJump();
        }
        jumpRequested = false;

        Move(grounded);

        // 낙하 최후 방어선은 괴물과 공용인 FallGuard 컴포넌트로 옮겼다(Bug-fix-plan.md §30.4).
    }

    private void CheckMovementInput()
    {
        Camera cam = Camera.main;
        Vector3 moveDir = PlayerInput.CameraRelativeMove(cam != null ? cam.transform : null);

        if (moveDir != Vector3.zero)
        {
            rotation = moveDir;
            rotation_value = rotation;

            if (isDodge)
                rotation = dodgeRotation;

            if (!isJump && !isDodge) // 점프와 회피 중이 아닐 때만 이동 애니메이션 상태 변경
            {
                bool isRunning = PlayerInput.RunHeld;
                animationDriver.ChangeState(isRunning ? PlayerMoveState.Run : PlayerMoveState.Walk);
            }
        }
        else
        {
            rotation = Vector3.zero;
            rotation_value = Vector3.zero;

            if (!isJump && !isDodge) // 점프와 회피 중이 아닐 때만 이동 애니메이션 상태 변경
                animationDriver.ChangeState(PlayerMoveState.Idle);
        }
    }

    private void CheckJumpInput()
    {
        if (PlayerInput.JumpPressed && !isJump && !isDodge)
        {
            jumpRequested = true;
        }
    }

    // 회피는 3초에 한 번(§26 H9). 스킬 칸(SkillHud)이 남은 시간을 그린다.
    public const float DodgeCooldown = 3f;
    private float dodgeReadyAt;
    public float DodgeCooldownLeft => Mathf.Max(0f, dodgeReadyAt - Time.time);

    private void CheckDodgeInput()
    {
        if (PlayerInput.DodgePressed && rotation != Vector3.zero && !isJump && !isDodge && Time.time >= dodgeReadyAt)
        {
            dodgeReadyAt = Time.time + DodgeCooldown;
            dodgeMoveDir = rotation;
            dodgeRotation = rotation;
            isDodge = true;
            keepMovingAfterDodge = true; // isDodge와 동시에 세팅해야 Move()의 관성 이동 분기가 실제로 도달 가능해짐
            dodgeTimer = dodgeDuration;

            animationDriver.ChangeState(PlayerMoveState.Dodge);
        }

        if (isDodge)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0f)
            {
                DodgeOut();
            }
        }
    }

    private void DodgeOut()
    {
        isDodge = false;
        keepMovingAfterDodge = false;
        rotation = rotation_value;
    }

    // 벽 접촉을 모은다 — 공중에서 벽을 향한 속도를 빼 벽에 붙지 않게 한다(Bug-fix-plan.md §41 ㊴). 물리 소유자만.
    private void OnCollisionStay(Collision collision)
    {
        if (pv.IsMine) groundDetector.RecordContacts(collision);
    }

    // 움직일 때 — 수평 속도만 Rigidbody에 넘기고, 수직 속도(중력/점프)는 물리 엔진이 이미 채운 값을 그대로 보존한다.
    private void Move(bool grounded)
    {
        Vector3 dir;
        float vel;
        Vector3 lookDir;

        if (isDodge && keepMovingAfterDodge) // 캐릭터가 회피중일 경우, 키보드에서 손을 떼더라도 회피를 시작했던 그 방향으로 강제로 밀어붙임
        {
            dir = dodgeMoveDir;
            vel = baseSpeed * DodgeSpeedMultiplier; // speed를 바꿨다 되돌리던 방식은 회피가 끊기면 배율이 남았다(research.md R5-14)
            lookDir = new Vector3(dodgeMoveDir.x, 0f, dodgeMoveDir.z);
        }
        else // Shift를 눌렀을 경우 기본 속도의 +30%(질주). 아니면 100% 속도 유지 — 점프/낙하 중에도 동일하게 적용(PlayerControllPlan.md §24/§25)
        {
            vel = (PlayerInput.RunHeld ? baseSpeed * 1.3f : baseSpeed) * SpeedBoost;
            dir = rotation;
            lookDir = new Vector3(rotation.x, 0f, rotation.z);
        }

        // transform.LookAt() 대신 rb.MoveRotation() 사용 — Rigidbody 보간(interpolation)은
        // MoveRotation으로 갱신된 회전만 인식한다. transform.rotation을 직접 대입하면 Rigidbody가
        // 추적하는 회전값과 어긋나 매 물리 스텝마다 회전이 튀었다 되돌아가길 반복해 걷는 모습이
        // 버벅거리고 미끄러지는 것처럼 보이는 버그가 있었다(Bug-fix-plan.md §13).
        if (lookDir != Vector3.zero)
            rb.MoveRotation(Quaternion.LookRotation(lookDir));

        Vector3 horizontal = new Vector3(dir.x * vel, 0f, dir.z * vel);
        // 공중에서 벽·사물을 향해 계속 밀면 그 수직항력에 비례한 마찰이 중력을 이겨 벽에 붙는다 — 벽을 향한 성분만 뺀다(§41 ㊴).
        // 땅에서는 그대로 둔다(벽 앞에 서서 미는 것은 문제가 없고, 지면 마찰은 경사에서 미끄러지지 않게 해 준다).
        if (!grounded) horizontal = groundDetector.RemoveWallPush(horizontal);
        rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z); // y는 물리 엔진(중력/점프)이 채운 값 그대로 보존
    }


    // 맵 밖으로 떨어졌을 때 VoidKillZone 또는 FallGuard가 호출한다(PlayerControllPlan.md §18.4, IRespawnable).
    public void RespawnToSpawnPoint()
    {
        if (!SceneSpawnPoints.TryFindClearPosition(SceneSpawnPoints.Cookie, GameSettings.Current.CookieSpawnRange, out Vector3 respawnPos))
            return;

        rb.linearVelocity = Vector3.zero; // 낙하 속도가 남아있으면 스폰 직후 바닥을 뚫고 지나갈 수 있음
        // Non-kinematic Rigidbody에서는 transform.position을 직접 대입해도 다음 물리 스텝에서
        // Rigidbody가 자신이 마지막으로 시뮬레이션한 위치로 되돌려버린다 — 반드시 rb.position으로
        // 물리 엔진에도 같이 알려줘야 실제로 순간이동이 반영된다(Play Mode 실측으로 확인된 문제).
        rb.position = respawnPos;
        transform.position = rb.position;

        isJump = false;
        animationDriver.ResumePlayback();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (stream.IsWriting) // 로컬 플레이어의 상태 정보 송신
        {
            networkSync.Write(stream, transform, animationDriver.CurrentState, grabController != null && grabController.IsCarrying, carryFollower.CarrierViewId);
        }
        else // 원격 플레이어의 상태 정보 수신
        {
            networkSync.Read(stream, transform);
        }
    }
}
