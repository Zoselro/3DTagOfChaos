using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 상호작용 문(GameLobbyScene.md §14). 파괴되지 않은 쿠키나 괴물이 가까이에서 상호작용 키를 누르면 여닫는다.
// 여는 방향은 누른 캐릭터의 반대쪽이다: 밖에서 누르면 안쪽으로, 안에서 누르면 바깥쪽으로(OpenOutward 파라미터).
// 상태는 Room Prop(NetKeys.DoorStates)에 두고 방장만 쓴다 — 누른 클라이언트는 방장에게 요청(RaiseEvent)하고,
// 모든 클라이언트가 Props 변경을 보고 Animator를 맞춘다. 늦게 들어온 사람도 Props로 현재 상태를 받는다(research.md G5-1).
// 과자집 빌더(WitchCookieHouseBuilder)가 문 오브젝트(Animator가 붙은 경첩 피벗)에 붙인다.
[RequireComponent(typeof(Animator))]
public class InteractableDoor : MonoBehaviourPunCallbacks, IInteractable, IOnEventCallback
{
    public enum DoorState : byte
    {
        Closed = 0,
        OpenInward = 1,
        OpenOutward = 2,
    }

    public const string DefaultOpenParameter = "IsOpen";
    public const string DefaultOutwardParameter = "OpenOutward";
    public const string ClosedStateName = "Closed";
    public const string OpenStateName = "Open";
    public const string OpenOutwardStateName = "OpenOut";
    private const float FallbackSwingDuration = 0.6f;
    private const float RequestTimeout = 1.5f;
    private float leafBottomY; // 상호 작용 기준 높이 - 문짝 바닥 (문턱)

    // 방장이 보냈지만 아직 서버 응답이 오지 않은 값. 온라인에서는 SetCustomProperties가 응답 전까지 로컬 캐시에
    // 반영되지 않아, 두 문을 연달아 바꾸면 앞 변경을 덮어쓸 수 있다 — 방장은 이 값을 Props 위에 겹쳐 쓴다.
    private static readonly Hashtable masterPendingWrites = new Hashtable();

    [Tooltip("Room Prop에서 이 문을 가리키는 ID. 비어 있으면 오브젝트 이름을 쓴다(한 씬 안에서 겹치면 안 된다).")]
    [SerializeField] private string doorId;
    [SerializeField] private string openParameter = DefaultOpenParameter;
    [Tooltip("true면 바깥쪽으로 연다. Animator에 이 파라미터가 없으면 항상 안쪽으로 연다.")]
    [SerializeField] private string outwardParameter = DefaultOutwardParameter;
    [Tooltip("안쪽 열림 클립의 localEulerAngles.y. 부호로 어느 쪽이 집 안쪽인지 판단한다.")]
    [SerializeField] private float inwardOpenAngle = -90f;
    [Tooltip("문짝 충돌체. 비어 있으면 자식 콜라이더를 찾는다.")]
    [SerializeField] private Collider leafCollider;
    [Tooltip("닫힌 문짝 중심에서 이 수평 거리(m) 안이면 상호작용할 수 있다(문 앞뒤 공통).")]
    [SerializeField, Min(0.5f)] private float interactionRange = 2f;

    private Animator animator;
    private int openHash;
    private int outwardHash;
    private bool hasOutwardParameter;
    private float swingDuration = FallbackSwingDuration;

    // 닫힌 상태 기준 문짝 중심과 집 안쪽 방향(월드, 수평). 과자집은 정적 배치라 Awake에서 한 번만 계산한다.
    private Vector3 leafCenter;
    private Vector3 inwardNormal = Vector3.forward;

    private DoorState state = DoorState.Closed;
    private float swingEndTime = -1f;  // 도는 중이면 끝나는 시각, 아니면 -1(도는 동안에는 새 입력을 받지 않는다)
    private float requestExpireTime;   // 방장 응답을 기다리는 동안 같은 입력을 다시 보내지 않는다

    public string DoorId => string.IsNullOrEmpty(doorId) ? name : doorId;
    public DoorState State => state;

    // 수평 위치는 문짝 가운데, 높이는 문짝 바닥. 가운데 높이를 쓰면 높은 문(공장 7.4~8.9m)이
    // CharacterInteractor의 높이 차 제한(2.5m)에 걸려 상호작용 대상에서 빠졌다.
    public Vector3 InteractionPoint => new Vector3(leafCenter.x, leafBottomY, leafCenter.z);
    //public Vector3 InteractionPoint => leafCenter;

    public float InteractionRange => interactionRange;
    private const float RequestReachSlack = 3f; // 방장 화면 위치의 동기화 지연 여유(m)

    private void Awake()
    {
        animator = GetComponent<Animator>();
        openHash = Animator.StringToHash(openParameter);
        outwardHash = Animator.StringToHash(outwardParameter);
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.nameHash == outwardHash && p.type == AnimatorControllerParameterType.Bool) hasOutwardParameter = true;
        if (leafCollider == null) leafCollider = GetComponentInChildren<Collider>(true);
        // 문짝 충돌체는 닫힘·열림·도는 중 언제나 켜 둔다 — 캐릭터는 어떤 경우에도 문짝을 통과할 수 없다.
        // 도는 문짝은 Animator(물리 스텝 갱신)가 키네마틱 Rigidbody로 움직이므로 물리 엔진이 캐릭터를 밀어낸다.
        if (leafCollider != null) leafCollider.enabled = true;
        swingDuration = FindClipLength("Close", FallbackSwingDuration);
        ComputeGeometry();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        InteractableRegistry.Register(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        InteractableRegistry.Unregister(this);
    }

    // 씬 로드 직후 InRoom이 아직 false일 수 있으므로(Bug-fix-plan.md §12) OnJoinedRoom에서도 한 번 더 맞춘다.
    private void Start() => SyncFromRoom();

    public override void OnJoinedRoom() => SyncFromRoom();

    private void Update()
    {
        if (swingEndTime >= 0f && Time.time >= swingEndTime) swingEndTime = -1f;
    }

    // ---------------- IInteractable ----------------

    // 문 쪽 조건은 없다. 메시지 큐가 멈춘 동안(색칠 시간에 대기실에 혼자 남은 괴물)도 쓸 수 있다 — RequestState 참고(Bug-fix-plan.md §36).
    public bool CanInteract(IGameCharacter character) => true;

    public void Interact(IGameCharacter character)
    {
        if (swingEndTime >= 0f || Time.time < requestExpireTime) return;

        DoorState target;
        if (state != DoorState.Closed) target = DoorState.Closed;
        else if (hasOutwardParameter && IsInside(character.gameObject.transform.position)) target = DoorState.OpenOutward;
        else target = DoorState.OpenInward;
        RequestState(target);
    }

    private bool IsInside(Vector3 worldPosition) => Vector3.Dot(worldPosition - leafCenter, inwardNormal) > 0f;

    // ---------------- network ----------------

    private void RequestState(DoorState target)
    {
        // 동기화할 상대가 없으면(방 밖 — 오프라인 테스트 씬 등) 로컬에서만 바꾼다.
        if (!RoomState.IsInRoom())
        {
            ApplyState(target, instant: false);
            return;
        }

        requestExpireTime = Time.time + RequestTimeout;
        if (PhotonNetwork.IsMasterClient)
        {
            WriteState(DoorId, target);
            return;
        }

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(NetEventCodes.DoorStateRequest, new object[] { DoorId, (byte)target }, options, SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.DoorStateRequest || !PhotonNetwork.IsMasterClient) return;
        if (!(photonEvent.CustomData is object[] data) || data.Length < 2) return;
        if (!(data[0] is string id) || id != DoorId) return;
        if (!(data[1] is byte raw) || raw > (byte)DoorState.OpenOutward) return;
        // 요청한 사람의 캐릭터가 문 근처일 때만(F-06 — 멀리서 문을 여닫는 조작 방지). 캐릭터를 못 찾으면(입장 직후 등) 막지 않는다.
        IGameCharacter requester = CharacterRegistry.FindByOwner(photonEvent.Sender);
        if (requester != null && Vector3.Distance(requester.gameObject.transform.position, InteractionPoint) > interactionRange + RequestReachSlack)
        {
            Debug.LogWarning($"[InteractableDoor] Ignored a request for '{id}' from actor {photonEvent.Sender}: too far away.");
            return;
        }
        WriteState(id, (DoorState)raw);
    }

    // 방장 전용. 다른 문의 값은 그대로 두고 이 문의 값만 바꾼 전체 표를 쓴다.
    private static void WriteState(string id, DoorState value)
    {
        var doors = new Hashtable();
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.DoorStates, out object current) && current is Hashtable confirmed)
            foreach (object key in confirmed.Keys) doors[key] = confirmed[key];
        foreach (object key in masterPendingWrites.Keys) doors[key] = masterPendingWrites[key];

        doors[id] = (byte)value;
        masterPendingWrites[id] = (byte)value;
        AuthorityProps.SetRoom(new Hashtable { { NetKeys.DoorStates, doors } });
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!propertiesThatChanged.ContainsKey(NetKeys.DoorStates)) return;
        var doors = propertiesThatChanged[NetKeys.DoorStates] as Hashtable;
        if (doors == null) masterPendingWrites.Clear(); // 판 초기화(RoundStateResetter)로 지워짐 — 모두 닫힘
        else if (masterPendingWrites.TryGetValue(DoorId, out object pending) && doors.TryGetValue(DoorId, out object confirmed)
                 && Equals(pending, confirmed))
            masterPendingWrites.Remove(DoorId); // 보낸 값이 확정됐을 때만 지운다(그 사이 다시 쓴 값은 유지)
        requestExpireTime = 0f;
        ApplyState(ReadState(doors), instant: false);
    }

    public override void OnMasterClientSwitched(Player newMasterClient) => masterPendingWrites.Clear();

    private void SyncFromRoom()
    {
        if (!RoomState.IsInRoom()) return;
        PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.DoorStates, out object value);
        ApplyState(ReadState(value as Hashtable), instant: true);
    }

    private DoorState ReadState(Hashtable doors)
    {
        if (doors != null && doors.TryGetValue(DoorId, out object value) && value is byte raw && raw <= (byte)DoorState.OpenOutward)
            return (DoorState)raw;
        return DoorState.Closed;
    }

    // ---------------- presentation ----------------

    // instant: 입장·씬 로드 때 현재 상태로 바로 맞춘다(애니메이션 없이 끝 자세).
    // 충돌체는 끄지 않는다(Awake 참고) — 도는 동안 문짝을 관통하던 문제(0.6초 동안 충돌체를 껐음)를 막는다.
    private void ApplyState(DoorState target, bool instant)
    {
        if (target == state && !instant) return;

        DoorState previous = state;
        state = target;
        bool open = target != DoorState.Closed;
        bool outward = target == DoorState.OpenOutward;

        if (open && hasOutwardParameter) animator.SetBool(outwardHash, outward);
        animator.SetBool(openHash, open);
        if (!instant) GameAudio.PlayAt(open ? SoundId.DoorOpen : SoundId.DoorClose, transform.position); // S4

        if (instant)
        {
            animator.Play(open ? (outward ? OpenOutwardStateName : OpenStateName) : ClosedStateName, 0, 1f);
            swingEndTime = -1f;
            return;
        }

        // 열린 채 반대 방향 값이 오면(드문 경합) 닫힘 상태를 거쳐 새 방향으로 연다.
        bool directionChanged = previous != DoorState.Closed && open && previous != target;
        if (directionChanged) animator.Play(ClosedStateName, 0, 0f);

        swingEndTime = Time.time + swingDuration;
    }

    // 닫힌 문짝의 가장 얇은 축을 벽의 법선으로 보고, 안쪽 열림 각도로 돌렸을 때 문짝 중심이 가는 쪽을 집 안쪽으로 정한다.
    private void ComputeGeometry()
    {
        Vector3 center = transform.position;
        Vector3 normal = transform.forward;

        var meshCollider = leafCollider as MeshCollider;
        if (meshCollider != null && meshCollider.sharedMesh != null)
        {
            Transform t = meshCollider.transform;
            Bounds local = meshCollider.sharedMesh.bounds;
            Vector3 size = Vector3.Scale(local.size, t.lossyScale);
            center = t.TransformPoint(local.center);
            normal = size.x <= size.z ? t.right : t.forward;
        }
        else if (leafCollider is BoxCollider box)
        {
            // 상자 자기 축으로 잰다 — 건물째 돌려 놓은 문(초콜릿 공장 맵 45°, Request1009Plan.md §42)은 월드 범위가 정사각형에 가까워
            // 두께 방향을 잘못 골라 옆에서 다가온 캐릭터 쪽으로 문이 열렸다.
            Transform t = box.transform;
            Vector3 size = Vector3.Scale(box.size, t.lossyScale);
            center = t.TransformPoint(box.center);
            normal = Mathf.Abs(size.x) <= Mathf.Abs(size.z) ? t.right : t.forward;
        }
        else if (leafCollider != null)
        {
            Bounds world = leafCollider.bounds;
            center = world.center;
            normal = world.size.x <= world.size.z ? Vector3.right : Vector3.forward;
        }

        normal = Vector3.ProjectOnPlane(normal, Vector3.up);
        if (normal.sqrMagnitude < 1e-6f) normal = Vector3.forward;
        normal.Normalize();

        Vector3 offset = center - transform.position;
        Vector3 swing = Quaternion.AngleAxis(inwardOpenAngle, transform.up) * offset - offset;
        inwardNormal = Vector3.Dot(swing, normal) >= 0f ? normal : -normal;
        leafCenter = center;

        leafBottomY = leafCollider != null ? leafCollider.bounds.min.y : transform.position.y;
    }

    private float FindClipLength(string keyword, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return fallback;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip != null && clip.name.Contains(keyword)) return clip.length;
        return fallback;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            if (leafCollider == null) leafCollider = GetComponentInChildren<Collider>(true);
            ComputeGeometry();
        }
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(leafCenter, interactionRange);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(leafCenter, inwardNormal); // 집 안쪽
    }
}
