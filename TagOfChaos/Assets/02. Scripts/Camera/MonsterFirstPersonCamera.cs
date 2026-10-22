using UnityEngine;

// 괴물 1인칭 카메라(GameFixPlan.md F6). Main Camera에 Camera_Ctrl과 함께 붙고, 둘 중 하나만 켜진다
// (MonsterViewSwitcher가 켜고 끈다). 눈 위치(EyeSocket)에서 마우스 이동만으로 시점을 돌린다 — 3인칭처럼
// 우클릭을 누를 필요가 없으므로 켜져 있는 동안 커서를 잠근다. 결과 화면·ESC 메뉴에서는 버튼을 눌러야 하므로 커서를 푼다.
public class MonsterFirstPersonCamera : MonoBehaviour
{
    [SerializeField] private float sensitivityX = 3f;
    [SerializeField] private float sensitivityY = 2f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 70f;
    [Tooltip("1인칭일 때 카메라 근접 클립(m). 몸 반지름(0.31 m)보다 작아야 벽에 붙어도 벽 너머가 비치지 않는다(Request1009Plan.md §35.3)")]
    [SerializeField, Min(0.01f)] private float nearClip = 0.1f;

    [Header("Grab kill (EscapeVisualPlan.md §1.4)")]
    [Tooltip("처형이 시작되면 이 시간 동안 잡힌 쿠키 쪽으로 시점이 돌아간다(초)")]
    [SerializeField, Min(0.01f)] private float killLookSeconds = 0.3f;
    [Tooltip("처형이 끝나면 이 시간 동안 원래 보던 방향으로 돌아온다(초)")]
    [SerializeField, Min(0.01f)] private float killReturnSeconds = 0.4f;

    private Transform eye;
    private float yaw;
    private float pitch;
    private MonsterController monster;
    private float killBlend;          // 0 = 내 시점, 1 = 잡힌 쿠키를 보는 시점
    private Quaternion killLook;      // 마지막으로 본 쿠키 방향(돌아올 때 여기서부터 섞는다)
    private Camera cam;
    private float previousNearClip = -1f; // 3인칭(Camera_Ctrl)으로 돌아갈 때 되돌릴 값

    // 몸이 바라볼 수평 방향(MonsterController가 1인칭일 때 이 방향으로 몸을 돌린다).
    public Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);

    public void Attach(Transform bodyRoot, Transform eyeSocket)
    {
        eye = eyeSocket;
        monster = bodyRoot.GetComponent<MonsterController>();
        yaw = bodyRoot.eulerAngles.y;
        pitch = 0f;
    }

    private void OnEnable()
    {
        if (!FreeCursor()) Cursor.lockState = CursorLockMode.Locked;
        if (cam == null) cam = GetComponent<Camera>();
        if (cam != null)
        {
            previousNearClip = cam.nearClipPlane;
            cam.nearClipPlane = nearClip;
        }
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        if (cam != null && previousNearClip > 0f) cam.nearClipPlane = previousNearClip;
    }

    private void LateUpdate()
    {
        if (eye == null) return;

        // 처형 중에는 잡힌 쿠키(괴물 손의 GrabSocket)를 따라보고, 끝나면 원래 보던 방향으로 돌아온다. 그동안 마우스는 무시한다.
        bool killing = monster != null && monster.IsGrabKilling && monster.GrabSocket != null;

        if (FreeCursor())
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (!killing && killBlend <= 0f)
            {
                Vector2 delta = PlayerInput.LookDelta;
                yaw += delta.x * sensitivityX;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivityY, minPitch, maxPitch);
            }
        }

        Quaternion own = Quaternion.Euler(pitch, yaw, 0f);
        if (killing)
        {
            Vector3 toCookie = monster.GrabSocket.position - eye.position;
            if (toCookie.sqrMagnitude > 0.0001f) killLook = Quaternion.LookRotation(toCookie);
            killBlend = Mathf.MoveTowards(killBlend, 1f, Time.deltaTime / killLookSeconds);
        }
        else if (killBlend > 0f)
        {
            killBlend = Mathf.MoveTowards(killBlend, 0f, Time.deltaTime / killReturnSeconds);
        }

        Quaternion rotation = killBlend > 0f ? Quaternion.Slerp(own, killLook, Mathf.SmoothStep(0f, 1f, killBlend)) : own;
        transform.SetPositionAndRotation(eye.position + CameraShake.Offset, rotation);
    }

    // 3인칭에서 1인칭으로 돌아올 때 3인칭 카메라가 보던 수평 방향을 이어받는다.
    public void SyncYaw(float worldYaw) => yaw = worldYaw;

    // 결과 화면이나 ESC 메뉴(나가기 확인창 포함)에서는 버튼을 눌러야 하므로 커서를 풀고 시점을 멈춘다(EscapeVisualPlan.md §1.2).
    private static bool FreeCursor() => GamePhaseState.Current == GamePhase.Result || EscMenu.IsOpen;
}
