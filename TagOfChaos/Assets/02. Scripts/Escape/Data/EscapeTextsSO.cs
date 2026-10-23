using UnityEngine;

// 탈출 모드 화면 문구(Resources/Escape/EscapeTexts). 코드에 한글을 쓰지 않으므로 모든 문구는 이 에셋에 입력한다.
[CreateAssetMenu(menuName = "TagOfChaos/Escape/Texts", fileName = "EscapeTexts")]
public class EscapeTextsSO : ScriptableObject
{
    public const string ResourcePath = "Escape/EscapeTexts";

    [Header("Chest labels (§1.4)")]
    public string materialChest = "Material Chest";
    public string spyChest = "Spy Chest";
    public string locked = "Locked";

    [Header("Interaction labels")]
    public string spyRocket = "Rocket";

    [Header("Toasts (§1.10, §1.11)")]
    public string spyCaught = "The spy was caught";
    [Tooltip("스파이가 장치에서 재료를 뺀 뒤 n초 후(괴물 제외). 받침 있을 때. {0} = 재료 이름")]
    public string stolenFromDeviceWithFinal = "The spy took the {0}!";
    [Tooltip("받침 없을 때. {0} = 재료 이름")]
    public string stolenFromDeviceNoFinal = "The spy took the {0}!";
    [Tooltip("스파이가 로켓에 넣은 재료가 다른 상자에 다시 생긴 뒤 n초 후(괴물 제외). 받침 있을 때. {0} = 재료 이름")]
    public string respawnedInChestWithFinal = "The {0} the spy used is back in another chest";
    [Tooltip("받침 없을 때. {0} = 재료 이름")]
    public string respawnedInChestNoFinal = "The {0} the spy used is back in another chest";
    [Tooltip("빈 상자가 없어 탈출 장치 옆 바닥에 놓였을 때. 받침 있을 때. {0} = 재료 이름")]
    public string respawnedOnGroundWithFinal = "The {0} the spy used dropped next to the escape";
    [Tooltip("받침 없을 때. {0} = 재료 이름")]
    public string respawnedOnGroundNoFinal = "The {0} the spy used dropped next to the escape";
    public string youAreSpy = "You are the SPY";
    public string spyBadge = "SPY";
    public string deviceComplete = "The escape is open!";
    public string witchComing = "The witch is coming..."; // 마녀가 나타날 때(멀리서 걸어온다 — Request1009Plan.md §23)

    [Header("Request1009Plan.md §25")]
    public string unknownMaterial = "?";              // 스파이 화면의 재료 이름
    public string wrongMaterial = "Wrong material";   // 스파이가 맞지 않는 재료로 건물을 누름
    public string buildingRepaired = "A building is running!";
    public string exitsOpened = "Two exits are open!";
    public string rainbowAppeared = "A rainbow chest appeared!";
    public string rainbowChest = "Rainbow Chest";
    public string rainbowBroken = "The rainbow chest was destroyed";
    public string breakChest = "Break";
    public string noiseBombAlarm = "ALARM! A building was disturbed";
    [Tooltip("{0} = 남은 칸, {1} = 최대")]
    public string noiseBombCharges = "Bomb {0}/{1}";
    public string removeBomb = "Remove";
    public string recordVoice = "Record voice (5s)";
    public string recording = "Recording...";
    public string noMicrophone = "No microphone";
    // Request1009Plan.md §26
    public string materialRestored = "{0} appeared again in a chest"; // D 안전장치({0} = 재료 이름)
    public string allDecoysGone = "All AIs are gone!";
    public string noRecording = "No recorded voice";
    public string repairRetry = "Try again";
    public string rocketKitHalf = "50%";
    public string rocketKits = "Repair kits used {0}";   // {0} = 넣은 수리키트 수
    public string rocketTitle = "Spy rocket";
    public string reRecordVoice = "Record again (5s)"; // 대기실 녹음 버튼 — 이미 녹음했을 때(§26 H11)
    public string listenVoice = "Listen";
    public string cookieCaughtWithFinal = "'{0}' was caught!"; // 괴물이 쿠키를 잡음 — 닉네임 끝 글자 받침 있음(이), 없음(가), 한글 아님(이(가))
    public string cookieCaughtNoFinal = "'{0}' was caught!";
    public string cookieCaughtOther = "'{0}' was caught!";
    public string skillDash = "Dash";         // 스킬 칸 이름(§26 H9)
    public string skillBomb = "Noise bomb";
    public string skillBombPlaced = "Placed";
    public string skillDodge = "Dodge";
    public string skillUnknown = "TBD";        // 생존자 스킬(아직 미정)

    [Header("Timers")]
    public string timeAttackFormat = "{0:00}:{1:00}";

    private static EscapeTextsSO cached;

    public static EscapeTextsSO Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<EscapeTextsSO>(ResourcePath);
            if (cached == null) cached = CreateInstance<EscapeTextsSO>();
            return cached;
        }
    }
}
