using UnityEngine;

// §90 P5 Heaven Of Bakery 낙하 규칙(순수 계산 — 시험). 하늘 섬·경사로에서 마지막으로 디딘 높이보다 12 m 넘게 떨어지면
// 패러글라이더를 펴지 않은 쿠키·스파이는 부서진다(DeathCause.Fall). 방장이 위치 기록(0.2초 간격)으로 판정한다:
// 내려가는 속도가 걷기·경사로보다 느리면(> -FallSpeed) 그 높이를 "디딘 높이"로 적고, 그보다 빨리 떨어지는 동안은 적지 않는다.
// 술래는 해당 없음(FallGuard), AI는 길 위만 다닌다.
public static class SkyFallRules
{
    public const string MapName = "HeavenOfBakery";
    public const float LethalDrop = 12f;
    public const float FloorY = 0f;          // 1층 바닥 높이
    public const float FallSpeed = 4f;       // 이보다 빨리 내려가면 떨어지는 중(경사로 걷기·계단은 이보다 느리다)
    public const float EdgeWarnDistance = 3f; // W2 가장자리 3 m 안
    public const float GliderCatchHeight = 6.5f; // 활공 중 이 높이(바닥에서) 아래에서만 술래가 잡을 수 있다

    public static bool MapHasSkyFall(string mapName) => mapName == MapName;

    // 디딘 높이 갱신: 떨어지는 중이 아니면 지금 높이.
    public static float UpdateGroundY(float lastGroundY, float y, float verticalSpeed, bool exempt) =>
        exempt || verticalSpeed > -FallSpeed ? y : lastGroundY;

    public static bool Dies(bool mapHasSkyFall, float lastGroundY, float y, bool gliding) =>
        mapHasSkyFall && !gliding && lastGroundY - y > LethalDrop;

    // W2 화면 경고: 1층 바닥에서 12 m보다 높고 가장자리(아래로 12 m 넘게 빈 곳) 3 m 안. 낮아지면 바로 멈춘다(사용자 고침).
    public static bool ShowWarning(bool mapHasSkyFall, float y, bool nearDropEdge, bool gliding) =>
        mapHasSkyFall && !gliding && y - FloorY > LethalDrop && nearDropEdge;
}
