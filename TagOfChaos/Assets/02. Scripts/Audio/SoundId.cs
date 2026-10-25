// 효과음 ID(SoundPlan.md §3.2). 값은 SO에 숫자로 저장되므로 묶음마다 백 단위 구간을 두고, 새 소리는 구간 끝에 덧붙인다
// (기존 값을 바꾸거나 중간에 끼우지 않는다). 새 소리 = 항목 추가 + Tools/TagOfChaos/Audio/Build Catalogs(파일 이름 = ID).
public enum SoundId
{
    None = 0,

    // UI(2D)
    UiHover = 100,
    UiClick = 101,
    UiConfirm = 102,
    UiCancel = 103,
    UiStart = 104,
    UiError = 105,
    UiOpen = 106,
    UiClose = 107,
    UiSaved = 108,
    UiTick = 109,
    UiTickFinal = 110,
    UiSlotSelect = 111,
    UiToastInfo = 112,
    UiToastAlert = 113,
    UiChat = 114,

    // 캐릭터(3D)
    CookieStep = 200,
    CookieJump = 201,
    CookieLand = 202,
    CookieDodge = 203,
    CookieGrab = 204,
    CookieRelease = 205,
    MonsterStep = 207,
    MonsterDash = 208,
    MonsterGrab = 209,
    MonsterSquash = 210,
    CookieCrumble = 211,
    MonsterAimLock = 212,
    StunStars = 213,
    Respawn = 214,
    DoorOpen = 216,
    DoorClose = 217,

    // 색칠(2D, 본인)
    PaintStroke = 300,
    PaintPickColor = 301,
    PaintSlotRegistered = 302,
    PaintErase = 303,
    PaintReset = 304,
    PaintForceFill = 305,

    // 대기실
    CauldronBubble = 400,
    CauldronSplash = 401,
    MonsterDeparted = 403,

    // 탈출 모드
    ChestOpen = 500,
    ChestRespawn = 501,
    ItemPickup = 502,
    ItemDrop = 503,
    DeviceInsert = 504,
    DeviceComplete = 505,
    BoardHop = 506,
    BoardSuck = 507,
    RocketInsert = 508,
    RocketHatch = 509,
    RocketIgnite = 510,
    RocketLiftoff = 511,
    WitchAppear = 512,
    WitchSlam = 513,
    HeartBeat = 514,
    EscapeSuccessSelf = 515,
    WitchStep = 516,     // 마녀 발걸음(Request1009Plan.md §23.5 X4)
    WitchShatter = 517,  // 내리칠 때 맵이 무너지는 소리(§23.5 X7)

    // 도구
    StunAimHum = 600,
    StunFire = 601,
    StunHit = 602,
    BalloonThrow = 603,
    BalloonSplash = 604,
    HammerSwing = 605,
    HammerBonk = 606,

    // 맵 연출(3D)
    CakeRumble = 700,
    CakeCrack = 701,
    CakeBurst = 702,
    CakeRocketRise = 703,
    CakeIgnite = 704,
    CakeLaunch = 705,
    CoasterBulbOn = 710,
    CoasterSparks = 711,
    CoasterStartup = 712,
    CoasterLapBar = 713,
    CoasterDepart = 714,
    CoasterRoll = 715,   // 롤러코스터 열차가 달리는 소리(반복, 속도에 따라 크기 — Request1009Plan.md §31)
    CoasterChain = 716,  // 체인/부스터 구간 딸각딸각(반복)
    CoasterWhoosh = 717, // 빠른 내리막 바람(한 번)
    OvenGears = 720,
    OvenPiston = 721,
    OvenDoorRoll = 722,
    OvenFlash = 723,
    OvenBroken = 724, // 완성 전 고장 난 오븐 반복음(Request1009Plan.md §16.7)
    VikingCreak = 725,  // 바이킹이 끝에서 돌아설 때 나무·쇠 삐걱(한 번, 변형 3 — Request1009Plan.md §36)
    VikingWhoosh = 726, // 바이킹이 가장 아래를 지날 때 바람(한 번)
    VikingHum = 727,    // 바이킹 축·모터 소리(반복, 흔들림 크기에 따라)
    VikingBell = 728,   // 바이킹 출발 종(한 번)
    TrainPuff = 730,
    TrainWhistle = 731,
    TrainChug = 732,
    TrainTunnel = 733,
    AltarHum = 740,
    AltarGlow = 741,
    PortalOpen = 742,
    PortalFlash = 743,
    PortalClose = 744,
    LanternFlicker = 745,

    // 환경음(반복)
    AmbCandyForest = 800,
    AmbGingerbread = 801,
    AmbFactory = 802,
    AmbCarnival = 803,
    AmbBakery = 804,
    CarouselSpin = 805,   // 저주받은 놀이공원 회전목마(오르간 왈츠 + 도는 기계)
    AmbCave = 806,        // 진저브레드 지하 유적: 바람 + 가끔 떨어지는 물방울(AmbientZone, 2D)
    FountainWater = 807,  // 놀이공원 분수: 떨어지는 물줄기 + 물 튀는 소리(분수마다 AmbientEmitter, 가까이서만, §39 F3)
}

// 배경음·스팅어·징글 ID(SoundPlan.md §3.1). 규칙은 SoundId와 같다.
public enum MusicId
{
    None = 0,

    // 반복 곡
    Lobby = 1,
    GameLobby = 2,
    MonsterWait = 3,
    Result = 4,          // 판이 끝난 뒤 결과 화면(징글 뒤, Request1009Plan.md §20.1 Result_A)
    CandyForestPaint = 10,
    CandyForestHunt = 11,
    GingerbreadPaint = 12,
    GingerbreadHunt = 13,
    FactoryPaint = 14,
    FactoryHunt = 15,
    CarnivalPaint = 16,
    CarnivalHunt = 17,
    BakeryPaint = 18,
    BakeryHunt = 19,
    TimeAttack = 20,

    // 스팅어(한 번, 배경음을 잠시 낮춘다)
    StMonsterReveal = 100,
    StMonsterArrive = 101,
    StDeviceComplete = 102,
    StSpyLaunch = 103,
    StWitchAppear = 104,

    // 결과 징글
    JgEscapeSuccess = 200,
    JgEscapeFail = 201,
    JgMonsterWin = 202,
}
