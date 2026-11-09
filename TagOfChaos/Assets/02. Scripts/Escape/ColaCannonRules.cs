using UnityEngine;

// 콜라 대포 탈것의 규칙과 시간표(Request1009Plan.md §68.9) — 순수 계산이라 방장·모든 화면·시험이 같은 값을 쓴다.
// 흐름: 사용 → 내 앞에 연기와 함께 설치(SummonedAt) → 주인이 뒤 계단에서 E(BoardAt, 그 순간 카메라가 보는 방향 AimYaw)
// → 뒤 병뚜껑 해치 열림 → 병 안으로 들어감 → 해치 닫힘 → 회전대·포신이 겨눔 → 입구에서 콜라 거품과 함께 발사 → 포물선으로 Dest에 착지.
// Dest = 보는 방향으로 맵 끝(경계에서 EdgeMargin 안쪽)까지 — 그 끝에서부터 안쪽으로 줄여 가며 내릴 수 있는 첫 곳(2026-11-08 사용자: "그 방향의 맵의 끝부분까지").
// 탈출이 아니라 도망용이라 맵 밖으로는 가지 않는다.
// 모델 축(Unity, 대포 로컬): 포구 = +Z, 계단·해치 = -Z. 원점 = 받침 가운데 바닥, 포신 핀 높이 PinHeight.
public static class ColaCannonRules
{
    public const float IdleSeconds = 10f;       // 설치 뒤 타지 않으면 사라짐(호박 마차와 같다)
    public const float HatchOpenEnd = 0.4f;     // BoardAt부터: 해치 다 열림
    public const float EnterEnd = 1.1f;         // 병 안으로 다 들어감
    public const float HatchCloseEnd = 1.5f;    // 해치 다 닫힘 — 이때부터 잡히지 않는다
    public const float FireAt = 2.3f;           // 회전대·포신 겨누기 끝 = 발사
    public const float LandGrace = 0.8f;        // 착지 뒤 방장이 목록에서 지우기까지
    public const float HatchOpenDegrees = 115f;
    public const float RestPitch = 30f;         // 쉬는 포신 각도(모델 그대로)
    public const float FirePitch = 45f;
    public const float PinHeight = 1.65f;
    public const float BarrelFront = 2.0f;      // 핀 → 입구
    public const float BarrelBack = 1.5f;       // 핀 → 뒤 끝(해치)
    public const float MaxRange = 400f;         // 맵 끝을 찾는 한도(맵 지름보다 크게)
    public const float EdgeStep = 2f;           // 맵 끝을 찾을 때 나아가는 간격
    public const float EdgeMargin = 4f;
    public const float CeilingClearance = 2f;  // 천장에서 이만큼 아래가 비행 꼭대기 한계         // 맵 경계에서 이만큼 안쪽이 "끝"(TryRandomMapPoint의 MapInset과 같게)
    public const float MinRange = 6f;
    public const float RangeStep = 3f;
    public const float BoardReach = 2.4f;
    public const float SummonDistance = 3.4f;
    public const float SummonReach = 6f;
    public static readonly Vector3 HalfExtents = new Vector3(1.4f, 1.6f, 2.25f);
    public static readonly Vector3 StepsLocal = new Vector3(0f, 0f, -2.35f);  // 뒤 계단 앞 바닥(E 기준점)
    public static readonly Vector3 FaceLocal = Vector3.back;                  // 설치할 때 쓴 사람을 보는 쪽(계단)

    public enum Stage : byte { Waiting, Opening, Entering, Closing, Aiming, Flying, Gone }

    // 멀리 갈수록 오래·높이 날지만 끝없이 길어지지 않게(맵 끝까지 200 m 넘게 날 수 있다).
    public static float FlightSeconds(float distance) => Mathf.Min(7f, 1f + distance / 40f);
    public static float ApexHeight(float distance) => Mathf.Clamp(distance * 0.25f, 5f, 40f);
    public static float LandAt(float flightSeconds) => FireAt + flightSeconds;

    public static Stage StageAt(double summonedAt, double boardAt, float flightSeconds, double now)
    {
        if (boardAt <= 0) return now >= summonedAt + IdleSeconds ? Stage.Gone : Stage.Waiting;
        float t = (float)(now - boardAt);
        if (t < HatchOpenEnd) return Stage.Opening;
        if (t < EnterEnd) return Stage.Entering;
        if (t < HatchCloseEnd) return Stage.Closing;
        if (t < FireAt) return Stage.Aiming;
        return t < LandAt(flightSeconds) ? Stage.Flying : Stage.Gone;
    }

    public static float HatchAngle(double boardAt, double now)
    {
        if (boardAt <= 0) return 0f;
        float t = (float)(now - boardAt);
        if (t <= 0f) return 0f;
        if (t < HatchOpenEnd) return HatchOpenDegrees * Smooth(t / HatchOpenEnd);
        if (t < EnterEnd) return HatchOpenDegrees;
        if (t < HatchCloseEnd) return HatchOpenDegrees * (1f - Smooth((t - EnterEnd) / (HatchCloseEnd - EnterEnd)));
        return 0f;
    }

    // 겨누기 진행(0 = 쉬는 자세, 1 = 겨눔). 발사 뒤에는 겨눈 채로.
    public static float AimProgress(double boardAt, double now)
    {
        if (boardAt <= 0) return 0f;
        return Smooth(((float)(now - boardAt) - HatchCloseEnd) / (FireAt - HatchCloseEnd));
    }

    public static float EnterProgress(double boardAt, double now)
    {
        if (boardAt <= 0) return 0f;
        return Smooth(((float)(now - boardAt) - HatchOpenEnd * 0.6f) / (EnterEnd - HatchOpenEnd * 0.6f));
    }

    public static float PitchAt(double boardAt, double now) => Mathf.Lerp(RestPitch, FirePitch, AimProgress(boardAt, now));

    // 회전대가 돌아야 하는 각도(설치 방향 → 겨눈 방향, -180~180).
    public static float TurretDelta(float yaw, float aimYaw) => Mathf.DeltaAngle(yaw, aimYaw);
    public static float TurretYawAt(float yaw, float aimYaw, double boardAt, double now) => TurretDelta(yaw, aimYaw) * AimProgress(boardAt, now);

    public static bool Riding(double boardAt, float flightSeconds, double now) => boardAt > 0 && now >= boardAt && now < boardAt + LandAt(flightSeconds);
    public static bool Protected(double boardAt, float flightSeconds, double now) =>
        boardAt > 0 && now >= boardAt + HatchCloseEnd && now < boardAt + LandAt(flightSeconds) + 0.3f;

    public static Quaternion Rotation(float yaw) => Quaternion.Euler(0f, yaw, 0f);
    public static Vector3 Pivot(Vector3 pos) => pos + Vector3.up * PinHeight;
    public static Vector3 StepsPoint(Vector3 pos, float yaw) => pos + Rotation(yaw) * StepsLocal;

    // 포신 축 방향(입구 쪽, 월드): 월드 yaw(설치 yaw + 회전대) 방향으로 pitch만큼 들림.
    public static Vector3 BarrelDirection(float worldYaw, float pitch) => Quaternion.Euler(-pitch, worldYaw, 0f) * Vector3.forward;
    public static Vector3 Mouth(Vector3 pos, float worldYaw, float pitch) => Pivot(pos) + BarrelDirection(worldYaw, pitch) * BarrelFront;
    public static Vector3 Breech(Vector3 pos, float worldYaw, float pitch) => Pivot(pos) - BarrelDirection(worldYaw, pitch) * BarrelBack;
    // 병 안 웅크린 자리(발 기준 — 몸 가운데가 축 근처에 오게 조금 아래).
    public static Vector3 Inside(Vector3 pos, float worldYaw, float pitch) => Pivot(pos) - BarrelDirection(worldYaw, pitch) * 0.6f + Vector3.down * 0.9f;

    // 발사 뒤 t초의 위치: 입구 → Dest 포물선(꼭대기 = 둘 중 높은 쪽 + ApexHeight).
    // ceiling > 0이면(지하 등 위가 막힘) 꼭대기를 천장 CeilingClearance 아래로 낮춘다(§69 E3).
    public static Vector3 FlightPoint(Vector3 mouth, Vector3 dest, float t, float flightSeconds, float ceiling = 0f)
    {
        float k = Mathf.Clamp01(t / Mathf.Max(0.01f, flightSeconds));
        float dist = Vector2.Distance(new Vector2(mouth.x, mouth.z), new Vector2(dest.x, dest.z));
        float low = Mathf.Max(mouth.y, dest.y);
        float apex = low + ApexHeight(dist);
        if (ceiling > 0f) apex = Mathf.Max(low + 1f, Mathf.Min(apex, ceiling - CeilingClearance));
        Vector3 flat = Vector3.Lerp(mouth, dest, k);
        // 세 점(입구, 꼭대기, 도착)을 지나는 2차 곡선: y(k) = (1-k)²·a + 2k(1-k)·c + k²·b, c는 k=0.5에서 apex가 되게
        float c = 2f * apex - 0.5f * (mouth.y + dest.y);
        flat.y = (1 - k) * (1 - k) * mouth.y + 2 * k * (1 - k) * c + k * k * dest.y;
        return flat;
    }

    private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
