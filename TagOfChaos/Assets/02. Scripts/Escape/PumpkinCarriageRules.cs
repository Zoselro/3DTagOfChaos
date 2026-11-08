using UnityEngine;

// 호박 마차 탈것의 규칙과 시간표(Request1009Plan.md §68.7) — 순수 계산이라 방장·모든 화면·시험이 같은 값을 쓴다.
// 흐름: 사용 → 내 앞에 연기와 함께 소환(SummonedAt) → 주인이 문 앞에서 E(BoardAt) → 문 열림 → 걸어 들어감 → 문 닫힘 → 연기와 함께 사라지고
// 주인은 맵 안 무작위 지점(Dest)에 나타난다. 타지 않으면 IdleSeconds 뒤 사라진다.
// 모델 축(Unity, 마차 로컬): 문 = -X 쪽, 앞 = +Z. 원점 = 바닥 가운데.
public static class PumpkinCarriageRules
{
    public const float IdleSeconds = 10f;        // 소환 뒤 타지 않으면 사라짐(§68.7 기본안 2)
    public const float DoorOpenEnd = 0.4f;       // BoardAt부터: 문이 다 열림
    public const float EnterEnd = 1.0f;          // 안으로 다 들어감(이때부터 몸이 마차 안)
    public const float DoorCloseEnd = 1.4f;      // 문이 다 닫힘 — 이때부터 잡히지 않는다(§68.7 기본안 3)
    public const float TeleportAt = 1.9f;        // 마차가 사라지고 주인이 도착 지점에 나타남
    public const float RemoveAt = 2.4f;          // 방장이 목록에서 지움(도착 연기가 모든 화면에 보인 뒤)
    public const float DoorOpenDegrees = 100f;
    public const float BoardReach = 2.6f;        // 문 앞 기준점에서 E가 닿는 수평 거리
    public const float SummonDistance = 3.4f;    // 쓴 사람에서 마차 가운데까지(문 쪽이 쓴 사람을 본다)
    public const float SummonReach = 6f;         // 방장 검사: 쓴 사람과 소환 자리 거리
    public static readonly Vector3 HalfExtents = new Vector3(1.8f, 1.6f, 1.75f); // 몸통·바퀴를 감싸는 상자(로컬, 가운데 높이 HalfExtents.y)
    public static readonly Vector3 DoorLocal = new Vector3(-2.15f, 0f, 0f);      // 문 앞 바닥(로컬)
    public static readonly Vector3 InsideLocal = new Vector3(0.05f, 0.72f, 0f);  // 안 바닥 가운데(로컬, 탄 사람이 서는 곳)

    public enum Stage : byte { Waiting, Opening, Entering, Closing, Departing, Gone }

    public static Stage StageAt(double summonedAt, double boardAt, double now)
    {
        if (boardAt <= 0) return now >= summonedAt + IdleSeconds ? Stage.Gone : Stage.Waiting;
        float t = (float)(now - boardAt);
        if (t < DoorOpenEnd) return Stage.Opening;
        if (t < EnterEnd) return Stage.Entering;
        if (t < DoorCloseEnd) return Stage.Closing;
        return t < TeleportAt ? Stage.Departing : Stage.Gone;
    }

    // 문 열린 각도(0 = 닫힘). 타기 전에는 닫혀 있다.
    public static float DoorAngle(double boardAt, double now)
    {
        if (boardAt <= 0) return 0f;
        float t = (float)(now - boardAt);
        if (t <= 0f) return 0f;
        if (t < DoorOpenEnd) return DoorOpenDegrees * Smooth(t / DoorOpenEnd);
        if (t < EnterEnd) return DoorOpenDegrees;
        if (t < DoorCloseEnd) return DoorOpenDegrees * (1f - Smooth((t - EnterEnd) / (DoorCloseEnd - EnterEnd)));
        return 0f;
    }

    // 걸어 들어가는 정도(0 = 문 앞, 1 = 안).
    public static float EnterProgress(double boardAt, double now)
    {
        if (boardAt <= 0) return 0f;
        return Smooth(Mathf.Clamp01(((float)(now - boardAt) - DoorOpenEnd * 0.6f) / (EnterEnd - DoorOpenEnd * 0.6f)));
    }

    // 탄 사람의 이동이 잠기는 동안(E를 누른 순간부터 도착할 때까지).
    public static bool Riding(double boardAt, double now) => boardAt > 0 && now >= boardAt && now < boardAt + TeleportAt;

    // 문이 닫힌 뒤 도착할 때까지는 괴물에게 잡히지 않는다.
    public static bool Protected(double boardAt, double now) => boardAt > 0 && now >= boardAt + DoorCloseEnd && now < boardAt + TeleportAt + 0.5f;

    public static Quaternion Rotation(float yaw) => Quaternion.Euler(0f, yaw, 0f);
    public static Vector3 DoorPoint(Vector3 pos, float yaw) => pos + Rotation(yaw) * DoorLocal;
    public static Vector3 InsidePoint(Vector3 pos, float yaw) => pos + Rotation(yaw) * InsideLocal;

    // 쓴 사람(user)이 바라보는 쪽(forward)에서 offset만큼 돈 방향으로 SummonDistance 떨어진 자리와, 문(-X)이 쓴 사람을 보는 방향.
    public static Vector3 SummonPoint(Vector3 user, Vector3 forward, float offsetDegrees, out float yaw) =>
        VehicleSpot.Point(user, forward, offsetDegrees, SummonDistance, Vector3.left, out yaw);

    private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
