using UnityEngine;

public class Camera_Ctrl : MonoBehaviour
{
    [SerializeField] GameObject m_Player;
    Vector3 m_TargetPos = Vector3.zero;

    // --- 카메라 회전 관련 설정 ---
    float m_RotH = 0.0f;
    float m_RotV = 0.0f;
    float hSpeed = 5.0f;
    float vSpeed = 2.4f;
    float vMinLimit = -7.0f;
    float vMaxLimit = 80.0f;
    // --- 카메라 회전 관련 설정 ---
    // (줌은 없앴다 — 마우스 휠은 인벤토리 칸 고르기)

    float m_DefaultRotH = 0.0f;
    float m_DefaultRotV = 25.0f;
    [SerializeField] float m_DefaultDist = 3.2f; // 카메라 거리 고정값(대상별 거리가 없을 때)

    Quaternion m_CurrentRotation;
    Quaternion m_TargetRotation;
    Vector3 m_BasicPos = Vector3.zero;
    Vector3 m_BuffPos = Vector3.zero;
    float rotationSmoothTime = 0.08f;
    // (줌 스무딩 필드 m_CurDistance/m_TargetDistance/zoomSmoothTime/zoomVelocity 삭제)

    // 추적 대상별 시선 높이/거리 — 쿠키는 기본값, 괴물은 MonsterController가 자기 체격에 맞는 값을
    // 넘긴다(Bug-fix-plan.md §23.4, 괴물도 쿠키와 같은 3인칭 궤도 카메라를 재사용).
    const float DefaultTargetHeight = 1.5f; // Cookie 캐릭터 키가 옛 모델 대비 약 7% 커져 비례 조정(PlayerControllPlan.md §25.7)
    float m_TargetHeight = DefaultTargetHeight;
    float m_Distance = -1f; // 0 이하 = 인스펙터의 m_DefaultDist 사용
    float CurrentDistance => m_Distance > 0f ? m_Distance : m_DefaultDist;

    public const float CookieTargetHeight = DefaultTargetHeight;

    public GameObject FollowTarget => m_Player;

    // 카메라가 바라보는 점(따라가는 대상의 시선 높이). 소리를 듣는 위치로도 쓴다(AudioListenerAnchor, DistanceFadePlan.md §2.1).
    public Vector3 FocusPoint => m_Player != null ? m_Player.transform.position + Vector3.up * m_TargetHeight : transform.position;

    public void InitCamera(GameObject player)
    {
        InitCamera(player, DefaultTargetHeight, -1f);
    }

    public void InitCamera(GameObject player, float targetHeight, float distance)
    {
         SetFollowTarget(player, targetHeight, distance, keepRotation: false);
    }

    // 추적 대상만 바꾼다. keepRotation=true면 우클릭 드래그로 돌려 둔 현재 각도를 유지해, 관전 대상을 순환할 때
    // 카메라가 기본 각도로 튀지 않는다(Bug-fix-plan.md §28.4). 조작하는 캐릭터(쿠키·괴물)와 관전 대상이 모두 이 한
    // 경로로 카메라를 쓰므로, 우클릭 회전·커서 잠금·회전 보간이 어디서든 똑같이 동작한다.
    public void SetFollowTarget(GameObject target, float targetHeight, float distance, bool keepRotation)
    {
        m_Player = target;
        m_TargetHeight = targetHeight;
        m_Distance = distance;
        m_CamDistance = -1f;
        if (!keepRotation) ResetToDefaultView(); // InitCamera가 Awake든 Start든 언제 호출되든 상관없이 항상 정확히 초기화됨
    }

    void Start()
    {
        // m_Player가 이미 연결되어 있다면(InitCamera가 이 시점 이전에 이미 호출된 경우) 정상 초기화.
        // 아직 연결 전이라면 아무 것도 하지 않고, InitCamera가 나중에 호출될 때 ResetToDefaultView()가
        // 대신 처리한다 — 두 호출 순서에 더 이상 의존하지 않는다.
        ResetToDefaultView();
    }

    private void ResetToDefaultView()
    {
        if (m_Player == null) return;

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y += m_TargetHeight;

        m_RotH = m_DefaultRotH;
        m_RotV = m_DefaultRotV;

        m_CurrentRotation = Quaternion.Euler(m_RotV, m_RotH, 0.0f);
        m_BasicPos = new Vector3(0f, 0f, -CurrentDistance);

        m_BuffPos = m_TargetPos + (m_CurrentRotation * m_BasicPos);
        transform.position = m_BuffPos;
        transform.LookAt(m_TargetPos);
    }

    // 벽 막힘(Request1009Plan.md §69 추가 — 진저브레드 지하에서 카메라가 동굴 벽 밖으로 나가 벽 뒷면이 검은 판처럼 보이고 바깥 하늘이 비쳤다):
    // 대상에서 카메라 쪽으로 구를 쏘아 벽·지형에 막히면 그 앞까지 당긴다. 캐릭터·AI·트리거는 무시한다. 다시 트이면 천천히 원래 거리로 돌아간다.
    const float CamRadius = 0.3f;
    const float CamMinDistance = 0.6f;
    const float CamWallGap = 0.15f;
    const float CamReturnSpeed = 5f;
    readonly RaycastHit[] m_CamHits = new RaycastHit[16];
    float m_CamDistance = -1f;

    float ObstacleFreeDistance(Vector3 from, Vector3 dir, float want)
    {
        // 회전 보간(Slerp) 뒤 쿼터니언이 아주 살짝 정규화가 깨지면 SphereCast가 'IsNormalized(direction)' 단언을 띄운다
        if (dir.sqrMagnitude < 1e-8f) return want;
        dir.Normalize();
        int n = Physics.SphereCastNonAlloc(from, CamRadius, dir, m_CamHits, want, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = want;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = m_CamHits[i];
            if (h.distance <= 0f || h.distance >= best) continue; // 시작부터 겹친 것(대상 몸 안 등)은 무시
            Transform t = h.collider.transform;
            if (m_Player != null && t.IsChildOf(m_Player.transform)) continue;
            if (h.collider.GetComponentInParent<IGameCharacter>() != null || h.collider.GetComponentInParent<DecoyView>() != null) continue;
            best = h.distance;
        }
        return best;
    }

    float CameraDistanceAvoidingWalls(Vector3 dir)
    {
        float want = CurrentDistance;
        float free = Mathf.Max(CamMinDistance, ObstacleFreeDistance(m_TargetPos, dir, want) - CamWallGap);
        if (m_CamDistance < 0f || free < m_CamDistance) m_CamDistance = free; // 막히면 바로 당긴다
        else m_CamDistance = Mathf.MoveTowards(m_CamDistance, free, CamReturnSpeed * Time.deltaTime);
        return m_CamDistance;
    }

    // 흔들림이 없을 때의 카메라 위치. 따라갈 대상이 없어도(마녀가 내리쳐 모두 쓰러진 뒤의 관전 등) 이 자리에서 흔들린다.
    Vector3 m_RestPos;
    bool m_HasRestPos;

    void OnEnable()
    {
        m_HasRestPos = false; // 1인칭 카메라 등이 그사이 카메라를 옮겼을 수 있다
    }

    void LateUpdate()
    {
        if (m_Player == null)
        {
            if (!m_HasRestPos) { m_RestPos = transform.position; m_HasRestPos = true; }
            transform.position = m_RestPos + CameraShake.Offset;
            return;
        }

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y += m_TargetHeight;

        // 마우스 자유 시점(§59 CL): 우클릭 없이 마우스 이동만으로 3인칭 카메라를 캐릭터 둘레로 돌린다(1인칭으로 바꾸지 않는다).
        // 메뉴·채팅·결과 화면·확인창·커서 보이기 키(Alt)일 때는 커서를 풀고 멈춘다(CursorPolicy). 우클릭은 도구 조준(ToolUser)으로만 쓴다.
        bool free = CursorPolicy.ShouldBeFree;
        CursorPolicy.Apply(free);

        if (!free)
        {
            Vector2 delta = PlayerInput.LookDelta;
            m_RotH += delta.x * hSpeed;
            m_RotV -= delta.y * vSpeed;
            m_RotV = ClampAngle(m_RotV, vMinLimit, vMaxLimit);
        }

        m_TargetRotation = Quaternion.Euler(m_RotV, m_RotH, 0.0f);
        m_CurrentRotation = Quaternion.Slerp(m_CurrentRotation, m_TargetRotation,
                             Mathf.Clamp(Time.deltaTime / rotationSmoothTime, 0.0f, 1.0f));

        Vector3 back = m_CurrentRotation * Vector3.back;
        m_BasicPos.z = -CameraDistanceAvoidingWalls(back); // 고정 거리 — 벽에 막히면 그 앞까지

        m_BuffPos = m_TargetPos + (m_CurrentRotation * m_BasicPos);
        transform.position = m_BuffPos;
        transform.LookAt(m_TargetPos);
        m_RestPos = m_BuffPos;
        m_HasRestPos = true;
        transform.position += CameraShake.Offset; // 흔들림은 위치를 다 정한 뒤에 더한다(research.md R4.7-9)
    }

    void OnDisable()
    {
        // 관전 모드 전환 등으로 이 컴포넌트가 꺼질 때 커서가 잠긴 채 남지 않도록 푼다.
        Cursor.lockState = CursorLockMode.None;
    }

    float ClampAngle(float angle, float min, float max)
    {
        angle = Mathf.DeltaAngle(0.0f, angle);
        return Mathf.Clamp(angle, min, max);
    }
}
