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
    public string bombNeedsBuilding = "Place it near a material building"; // 사이렌 함정: 재료 건물 둘레 밖(§69 F5)
    public string bombInactiveBuilding = "This building isn't used this round"; // 사이렌 함정: 이번 판에 쓰지 않는 재료 건물(§74 P12)
    public string bombMaterialInstalled = "A material is already installed here"; // 사이렌 함정: 재료가 꽂힌 건물
    public string bombAlreadyThere = "This building already has a trap"; // 사이렌 함정: 건물당 하나
    public string skillDodge = "Dodge";
    public string skillUnknown = "TBD";        // 생존자 스킬(아직 미정)
    public string skillSearch = "Search bomb"; // 술래 수색 폭탄(§68 GP5)
    public string skillWarp = "Teleport"; // §90 T2 술래 순간이동 칸
    public string skillBazooka = "Bazooka"; // §90 B3 술래 바주카포 칸(우클릭 조준 + 좌클릭 발사)
    public string cageMash = "Mash [{0}] to break the cage!"; // 사이렌 철창 안({0} = 점프 키)
    public string carriageBoard = "Ride Pumpkin Carriage";   // 호박 마차 문 앞 E 안내(§68.7)
    public string carriageNoRoom = "No room for the carriage"; // 소환할 빈 바닥이 없음
    public string cannonBoard = "Ride Cola Cannon";          // 콜라 대포 뒤 계단 E 안내(§68.9)
    [Tooltip("§89.24 Heaven Of Bakery 하늘 대포 뒤 계단 E 안내. {0} = 날아갈 섬 이름")]
    public string skyCannonBoard = "Ride Cannon → {0}";
    public string skyIslandAlmond = "Almond Factory";       // §89.24 하늘 섬 이름(대포 안내)
    public string skyIslandChocolate = "Chocolate Factory";
    public string skyIslandFlour = "Wheat Mill";
    public string cannonNoRoom = "No room for the cannon";   // 설치할 빈 바닥이 없음
    public string hostChanged = "{0} is now the host.";       // 대기실 방장이 바뀜(§69 A4, {0} = 닉네임)
    public string recorderAim = "Aim at your own AI";         // 녹음기: 조준한 내 AI가 없음(§69 B3)
    public string carryStruggle = "Mash Left / Right to break free"; // 들린 쿠키: 좌우 연타로 빠져나가기(§69 C2)
    public string claimRoomNotFull = "Wait until the room is full to claim the monster seat"; // 가마솥 신청 거절: 정원 미달(§71 L1)
    public string claimNotAtCauldron = "Step into the cauldron to claim"; // 가마솥 신청 거절: 가마솥 밖
    public string claimNoSeat = "The monster seat is already taken"; // 가마솥 신청 거절: 자리 없음(먼저 들어간 사람이 됨)
    public string claimNoAnswer = "No answer from the host. Step out and back in to try again"; // 가마솥 신청 3번 모두 답 없음(§71 L4)
    public string throwRejected = "Your throw was cancelled"; // 방장이 던지기를 받지 않음(§71 T1)
    public string requestFailed = "The host didn't get that. Please try again"; // 요청 재전송까지 답 없음(§72 E3)
    public string loadingMap = "Loading the map... {0}%"; // 판 시작 로딩 화면(§73 L5, {0} = 진행도 %)
    public string waitingPlayers = "Waiting for other players ({0}/{1})"; // 맵에 아직 안 온 사람이 있음(§73 L5)
    public string masterSlow = "The host's PC is slow — the round may start late or drop. A faster PC should host."; // 방장 화면 저속 안내(§73 L6, 방장 본인에게)

    [Header("Player list (§59 PL — Tab)")]
    public string playerListTitle = "Players";
    public string playerListMonster = "MONSTER";
    public string playerListCookie = "COOKIE";
    public string playerListYou = "(You)";
    public string playerListHost = "HOST";

    [Header("Timers")]
    public string timeAttackFormat = "{0:00}:{1:00}";

    [Header("Request1009Plan.md §90")]
    public string chestsRefilled = "The chests have been refilled"; // §90 C8 상자 리셋(쿠키·스파이)
    public string fallWarning = "Careful: a fall from here will break you"; // §90 W2 화면 경고(가장자리·12 m 위)
    public string fallWarningGlider = "Open your paraglider to land safely"; // §90 W2 패러글라이더를 들고 있을 때
    public string fallIntro = "Falling from the sky islands or ramps breaks you. Glide down with a paraglider."; // §90 W3 준비 시간 안내(이 맵만)
    public string fallSign = "DANGER: FALLING BREAKS YOU"; // §90 W1 표지판 큰 글씨
    public string fallSignSmall = "A paraglider keeps you safe"; // §90 W1 표지판 작은 글씨
    public string warpTitle = "Teleport to a building (1-4 / click, T to close)"; // §90 T2 건물 고르기 창 제목
    public string warpRepaired = "Repaired"; // §90 T2 수리 끝난 건물 표시
    public string warpCooldown = "Teleport ready in {0}s"; // §90 T2 쿨타임
    public string gumStuck = "Gum! You are slowed"; // §90 G6 술래 본인 화면
    public string gliderNeedsAir = "Jump off a ledge first to open the paraglider"; // §90 P5 땅에서 사용

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
