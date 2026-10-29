using System;
using UnityEngine;

// 바꿀 수 있는 조작 하나(Request1009Plan.md §27 K1). 순서는 설정 창의 줄 순서다. 마우스 버튼도 KeyCode(Mouse0~)로 담는다.
public enum KeyAction
{
    MoveForward,
    MoveBack,
    MoveLeft,
    MoveRight,
    Run,
    Jump,
    Dodge,
    Grab,
    Interact,
    Drop,
    UseTool,
    Slot1,
    Slot2,
    Slot3,
    Slot4,
    CameraRotate,
    MonsterDash,
    MonsterSkill,
    SpectateNext,
    Chat,
    PushToTalk,
    Dance,      // 춤(Request1009Plan.md §44.9) — 끝에 붙여 저장된 키 번호가 밀리지 않게 한다
}

// 같은 때 쓰이는 조작끼리의 묶음 — 겹침(빨간 경고)은 같은 묶음 안에서만 본다(쿠키 달리기와 괴물 돌진은 같은 Shift여도 된다).
[Flags]
public enum KeyGroup
{
    None = 0,
    Cookie = 1,
    Monster = 2,
    Spectate = 4,
    All = Cookie | Monster | Spectate,
}

// 키 설정(§27): 기본값은 Resources/InputBindings(InputBindingsSO), 바꾼 값은 이 PC의 PlayerPrefs("TOC_Key_<조작>")에 둔다.
// 바꾸면 바로 적용된다(PlayerInput이 매번 여기서 읽는다). Esc는 메뉴 전용이라 바꿀 수 없다. 겹침은 허용하고 경고만 한다.
public static class KeyBindings
{
    private const string PrefPrefix = "TOC_Key_";
    public static readonly KeyAction[] All = (KeyAction[])Enum.GetValues(typeof(KeyAction));
    private static KeyCode[] current;

    public static event Action Changed;

    public static KeyCode Get(KeyAction action)
    {
        if (current == null) Load();
        return current[(int)action];
    }

    public static void Set(KeyAction action, KeyCode key)
    {
        if (!CanBind(key)) return;
        if (current == null) Load();
        current[(int)action] = key;
        try { PlayerPrefs.SetInt(PrefPrefix + action, (int)key); PlayerPrefs.Save(); }
        catch (Exception e) { Debug.LogWarning("[KeyBindings] Could not save: " + e.Message); }
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        if (current == null) Load();
        foreach (KeyAction a in All)
        {
            current[(int)a] = Default(a);
            PlayerPrefs.DeleteKey(PrefPrefix + a);
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static bool CanBind(KeyCode key) => key != KeyCode.None && key != KeyCode.Escape;

    public static KeyGroup GroupOf(KeyAction action)
    {
        switch (action)
        {
            case KeyAction.MoveForward:
            case KeyAction.MoveBack:
            case KeyAction.MoveLeft:
            case KeyAction.MoveRight:
            case KeyAction.Interact:
                return KeyGroup.Cookie | KeyGroup.Monster;
            case KeyAction.MonsterDash:
            case KeyAction.MonsterSkill:
                return KeyGroup.Monster;
            case KeyAction.SpectateNext:
                return KeyGroup.Spectate;
            case KeyAction.CameraRotate:
            case KeyAction.Chat:
            case KeyAction.PushToTalk:
                return KeyGroup.All;
            default:
                return KeyGroup.Cookie;
        }
    }

    // 같은 묶음의 다른 조작이 같은 키를 쓰는지(순수 계산 — 키 읽기를 넘겨받아 시험한다).
    public static bool HasConflict(KeyAction action, Func<KeyAction, KeyCode> keyOf)
    {
        KeyCode key = keyOf(action);
        if (key == KeyCode.None) return false;
        KeyGroup group = GroupOf(action);
        foreach (KeyAction other in All)
            if (other != action && (GroupOf(other) & group) != 0 && keyOf(other) == key) return true;
        return false;
    }

    public static bool HasConflict(KeyAction action) => HasConflict(action, Get);

    public static KeyCode Default(KeyAction action)
    {
        InputBindingsSO b = PlayerInput.Bindings;
        switch (action)
        {
            case KeyAction.MoveForward: return b.MoveForwardKey;
            case KeyAction.MoveBack: return b.MoveBackKey;
            case KeyAction.MoveLeft: return b.MoveLeftKey;
            case KeyAction.MoveRight: return b.MoveRightKey;
            case KeyAction.Run: return b.RunKey;
            case KeyAction.Jump: return b.JumpKey;
            case KeyAction.Dodge: return b.DodgeKey;
            case KeyAction.Grab: return b.GrabKey;
            case KeyAction.Interact: return b.InteractKey;
            case KeyAction.Drop: return b.DropKey;
            case KeyAction.UseTool: return KeyCode.Mouse0 + b.UseToolButton;
            case KeyAction.Slot1: return KeyCode.Alpha1;
            case KeyAction.Slot2: return KeyCode.Alpha2;
            case KeyAction.Slot3: return KeyCode.Alpha3;
            case KeyAction.Slot4: return KeyCode.Alpha4;
            case KeyAction.CameraRotate: return KeyCode.Mouse0 + b.CameraRotateButton;
            case KeyAction.MonsterDash: return b.TentacleDashKey;
            case KeyAction.MonsterSkill: return b.SkillKey;
            case KeyAction.SpectateNext: return b.SpectateNextKey;
            case KeyAction.Chat: return b.ChatSubmitKey;
            case KeyAction.PushToTalk: return b.PushToTalkKey;
            case KeyAction.Dance: return b.DanceKey;
            default: return KeyCode.None;
        }
    }

    private static void Load()
    {
        current = new KeyCode[All.Length];
        foreach (KeyAction a in All)
        {
            KeyCode key = Default(a);
            try
            {
                int saved = PlayerPrefs.GetInt(PrefPrefix + a, -1);
                if (saved >= 0 && Enum.IsDefined(typeof(KeyCode), saved) && CanBind((KeyCode)saved)) key = (KeyCode)saved;
            }
            catch (Exception) { }
            current[(int)a] = key;
        }
    }

    // 화면에 보일 키 이름(짧게). 문구가 아니라 키 이름이라 영문이다.
    public static string DisplayName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0: return "LMB";
            case KeyCode.Mouse1: return "RMB";
            case KeyCode.Mouse2: return "MMB";
            case KeyCode.LeftShift: return "L-Shift";
            case KeyCode.RightShift: return "R-Shift";
            case KeyCode.LeftControl: return "L-Ctrl";
            case KeyCode.RightControl: return "R-Ctrl";
            case KeyCode.LeftAlt: return "L-Alt";
            case KeyCode.RightAlt: return "R-Alt";
            case KeyCode.Return: return "Enter";
            case KeyCode.None: return "-";
        }
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
        return key.ToString();
    }
}
