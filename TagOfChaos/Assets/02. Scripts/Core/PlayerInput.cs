using UnityEngine;

// 게임 입력을 읽는 유일한 창구(research.md §12 E5). 게임 코드는 "의도"(이동·점프·회피·그랩·색칠·시점 회전)만
// 묻고, 어떤 키인지와 어떤 입력 API인지는 여기서만 안다. 레거시 Input Manager를 쓰고 있으며, Input System
// 패키지로 옮길 때는 이 파일만 바꾸면 된다. 키 값은 KeyBindings(사용자 설정 → 없으면 Resources/InputBindings 기본값)에서 읽는다.
public static class PlayerInput
{
    private const string ResourcePath = "InputBindings";
    private static InputBindingsSO bindings;

    public static InputBindingsSO Bindings
    {
        get
        {
            if (bindings == null)
            {
                bindings = Resources.Load<InputBindingsSO>(ResourcePath);
                if (bindings == null)
                {
                    Debug.LogWarning($"[PlayerInput] Resources/{ResourcePath} not found. Using default bindings.");
                    bindings = ScriptableObject.CreateInstance<InputBindingsSO>();
                }
            }
            return bindings;
        }
    }

    // 채팅 입력 중처럼 키보드가 게임 조작이 아닌 곳에 쓰이는 동안 true(Bug-fix-plan.md §41 ㊵). 게임플레이 키 입력이 모두 중립값을
    // 돌려주므로 쿠키·괴물·관전·상호작용, 그리고 이후 추가될 캐릭터도 따로 잠그지 않아도 멈추고 Idle로 돌아간다. 채팅을 닫는 키와
    // 마우스(시점 회전·색칠)는 막지 않는다. 정적 값이라 켠 쪽(GameManager)이 비활성·파괴될 때 반드시 되돌린다.
    public static bool IsGameplaySuppressed
    {
        get => chatSuppressed || IsMenuOpen || CutsceneActive;
        set => chatSuppressed = value;
    }
    private static bool chatSuppressed;

    // 모두가 함께 보는 짧은 연출(마녀 등장 카메라, Request1009Plan.md §18.4) 동안 true — 조작을 멈춘다. 켠 쪽(WitchRevealCamera)이 끝나면 되돌린다.
    public static bool CutsceneActive { get; set; }

    // ESC 메뉴(나가기 확인창 포함)가 열려 있는 동안 true(EscapeVisualPlan.md §1.2). 키 입력에 더해 시점 회전도 막는다.
    public static bool IsMenuOpen { get; set; }

    // 이동 입력(x=좌우, y=앞뒤), 각 축 -1~1. 설정한 4개 키 + 방향키(고정 보조).
    public static Vector2 Move => IsGameplaySuppressed
        ? Vector2.zero
        : EditorMove != Vector2.zero ? EditorMove : new Vector2(
            Axis(KeyAction.MoveRight, KeyCode.RightArrow) - Axis(KeyAction.MoveLeft, KeyCode.LeftArrow),
            Axis(KeyAction.MoveForward, KeyCode.UpArrow) - Axis(KeyAction.MoveBack, KeyCode.DownArrow));

    private static float Axis(KeyAction action, KeyCode secondary) => Input.GetKey(KeyBindings.Get(action)) || Input.GetKey(secondary) ? 1f : 0f;
    private static bool Down(KeyAction action) => Input.GetKeyDown(KeyBindings.Get(action));
    private static bool Held(KeyAction action) => Input.GetKey(KeyBindings.Get(action));
    private static bool Up(KeyAction action) => Input.GetKeyUp(KeyBindings.Get(action));

    // 카메라가 바라보는 방향 기준의 수평 이동 방향(정규화, 입력이 없으면 0). 쿠키·괴물 공용(3인칭 궤도 카메라 전제).
    public static Vector3 CameraRelativeMove(Transform cameraTransform)
    {
        if (cameraTransform == null) return Vector3.zero;
        Vector2 move = Move;
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        return (forward * move.y + right * move.x).normalized;
    }

    public static bool RunHeld => !IsGameplaySuppressed && (Held(KeyAction.Run) || EditorRunHeld);
    public static bool JumpPressed => !IsGameplaySuppressed && Down(KeyAction.Jump);
    public static bool DodgePressed => !IsGameplaySuppressed && Down(KeyAction.Dodge);
    public static bool GrabPressed => !IsGameplaySuppressed && Down(KeyAction.Grab);
    public static bool InteractPressed => !IsGameplaySuppressed && (Down(KeyAction.Interact) || EditorTakeInteractPress());
    public static bool SkillPressed => !IsGameplaySuppressed && (Down(KeyAction.MonsterSkill) || EditorTakeSkillPress()); // 괴물 소음폭탄(§25.5 K)
    public static bool EditorSkillPress;
    private static bool EditorTakeSkillPress()
    {
        if (!EditorSkillPress) return false;
        EditorSkillPress = false;
        return true;
    }
    public static bool InteractHeld => !IsGameplaySuppressed && (Held(KeyAction.Interact) || EditorInteractHeld); // 상자·장치 길게 누르기(IHoldInteractable)

#if UNITY_EDITOR
    // Play Mode 자동 검증용(에디터 전용, 빌드에는 없다): E를 누른 것처럼 한 번 / 누르고 있는 것처럼.
    // 누름은 처음 읽힌 프레임 한 번 동안만 true다(GetKeyDown처럼 같은 프레임의 여러 읽기에 모두 보인다).
    public static bool EditorInteractPress;
    public static bool EditorInteractHeld;
    public static bool EditorSpectateNextPress;
    public static Vector2 EditorMove; // 이동 키를 누른 것처럼(괴물·쿠키 걷기 소리 시험)
    public static bool EditorRunHeld; // 달리기 키를 누른 것처럼(들고 달리기 자세 시험)
    private static int editorPressFrame = -1, editorSpectateFrame = -1;
    private static bool EditorTakeInteractPress() => TakeEditorPress(ref EditorInteractPress, ref editorPressFrame);
    private static bool EditorTakeSpectateNextPress() => TakeEditorPress(ref EditorSpectateNextPress, ref editorSpectateFrame);
    private static bool TakeEditorPress(ref bool request, ref int frame)
    {
        if (request)
        {
            request = false;
            frame = Time.frameCount;
        }
        return frame == Time.frameCount;
    }
#else
    private const bool EditorInteractHeld = false;
    private static bool EditorTakeInteractPress() => false;
    private static bool EditorTakeSpectateNextPress() => false;
    private static readonly Vector2 EditorMove = Vector2.zero;
    private const bool EditorRunHeld = false;
#endif
    public static bool TentacleDashPressed => !IsGameplaySuppressed && Down(KeyAction.MonsterDash);
    // 인벤토리(EscapePlan.md §1.9): G키로 떨어뜨리기, 숫자키 1~4로 칸 고르기, 마우스로 도구 쓰기.
    public static bool DropPressed => !IsGameplaySuppressed && Down(KeyAction.Drop);
    public static bool UseToolPressed => !IsGameplaySuppressed && Down(KeyAction.UseTool);

    // 눌린 칸 번호(1~4 → 0~3), 없으면 -1.
    public static int SlotKeyPressed
    {
        get
        {
            if (IsGameplaySuppressed) return -1;
            for (int i = 0; i < 4; i++)
                if (Down(KeyAction.Slot1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) return i; // 숫자판은 고정 보조 키
            return -1;
        }
    }

    public static bool SpectateNextPressed => !IsGameplaySuppressed && (Down(KeyAction.SpectateNext) || EditorTakeSpectateNextPress());
    public static bool ChatSubmitReleased => Up(KeyAction.Chat);
    // 대기실 음성 채팅 "누르면서 말하기"(§26 H11). 채팅 입력 중에는 말하지 않는다.
    public static bool PushToTalkHeld => !chatSuppressed && Held(KeyAction.PushToTalk);

    public static float SlotWheelDelta => Input.mouseScrollDelta.y; // 마우스 휠 — 인벤토리 칸 고르기

    public static bool CameraRotateHeld => Held(KeyAction.CameraRotate);
    public static bool CameraRotatePressed => !IsMenuOpen && Down(KeyAction.CameraRotate);
    public static bool CameraRotateReleased => Up(KeyAction.CameraRotate);
    public static Vector2 CameraRotateDelta => IsMenuOpen ? Vector2.zero : new Vector2(Input.GetAxis(Bindings.MouseXAxis), Input.GetAxis(Bindings.MouseYAxis));

    // 1인칭 시점 회전(GameFixPlan.md F6): 우클릭 없이 마우스 이동만으로 돌린다. 채팅 중에는 멈춘다.
    public static Vector2 LookDelta => IsGameplaySuppressed ? Vector2.zero : CameraRotateDelta;
}
