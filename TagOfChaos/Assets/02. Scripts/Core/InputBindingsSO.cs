using UnityEngine;

// 키·마우스 입력의 기본값(research.md §12 E5, CLAUDE.md: 전역 SO → Assets/Resources). 사용자가 설정 창에서 바꾼 값은
// KeyBindings가 PC마다 따로 저장하고(Request1009Plan.md §27), 이 에셋은 "기본값으로"가 되돌아갈 값이다.
[CreateAssetMenu(menuName = "TagOfChaos/InputBindings", fileName = "InputBindings")]
public class InputBindingsSO : ScriptableObject
{
    [Header("Movement (방향키는 늘 함께 쓰이는 고정 보조 키)")]
    [SerializeField] private KeyCode moveForwardKey = KeyCode.W;
    [SerializeField] private KeyCode moveBackKey = KeyCode.S;
    [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;

    [Header("Cookie")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode dodgeKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode grabKey = KeyCode.F;

    [Header("Cookie Inventory (EscapePlan.md §1.9)")]
    [Tooltip("손에 든 아이템 떨어뜨리기.")]
    [SerializeField] private KeyCode dropKey = KeyCode.G;
    [Tooltip("손에 든 도구 쓰기(0=왼쪽 마우스). 색칠 시간에는 쓰지 않는다.")]
    [SerializeField, Range(0, 2)] private int useToolButton = 0;

    [Header("Cookie / Monster")]
    [Tooltip("문 등 상호작용 사물(IInteractable) 사용 키. 파괴되지 않은 쿠키와 괴물이 쓴다.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Monster")]
    [SerializeField] private KeyCode tentacleDashKey = KeyCode.LeftShift;
    [Tooltip("괴물 두 번째 스킬 — 소음폭탄 설치(Request1009Plan.md §25.5 K).")]
    [SerializeField] private KeyCode skillKey = KeyCode.Q;

    [Header("Spectator / UI")]
    [SerializeField] private KeyCode spectateNextKey = KeyCode.Space;
    [SerializeField] private KeyCode chatSubmitKey = KeyCode.Return;
    [Tooltip("대기실 음성 채팅 — 누르면서 말하기(§26 Q19).")]
    [SerializeField] private KeyCode pushToTalkKey = KeyCode.T;

    [Header("Mouse")]
    [Tooltip("0=왼쪽, 1=오른쪽, 2=가운데")]
    [SerializeField, Range(0, 2)] private int cameraRotateButton = 1;
    [SerializeField] private string mouseXAxis = "Mouse X";
    [SerializeField] private string mouseYAxis = "Mouse Y";

    public KeyCode MoveForwardKey => moveForwardKey;
    public KeyCode MoveBackKey => moveBackKey;
    public KeyCode MoveLeftKey => moveLeftKey;
    public KeyCode MoveRightKey => moveRightKey;
    public KeyCode PushToTalkKey => pushToTalkKey;
    public KeyCode RunKey => runKey;
    public KeyCode JumpKey => jumpKey;
    public KeyCode DodgeKey => dodgeKey;
    public KeyCode GrabKey => grabKey;
    public KeyCode InteractKey => interactKey;
    public KeyCode DropKey => dropKey;
    public int UseToolButton => useToolButton;
    public KeyCode TentacleDashKey => tentacleDashKey;
    public KeyCode SkillKey => skillKey;
    public KeyCode SpectateNextKey => spectateNextKey;
    public KeyCode ChatSubmitKey => chatSubmitKey;
    public int CameraRotateButton => cameraRotateButton;
    public string MouseXAxis => mouseXAxis;
    public string MouseYAxis => mouseYAxis;
}
