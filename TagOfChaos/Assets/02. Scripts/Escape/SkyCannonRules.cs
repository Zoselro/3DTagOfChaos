using UnityEngine;

// Heaven Of Bakery 하늘 대포 규칙(Request1009Plan.md §89.24·§89.25) — 순수 계산이라 방장·모든 화면·시험이 같은 값을 쓴다.
// 섬마다 두 대가 박혀 있고 각자 다른 섬 하나를 겨눈다(회전대가 돌지 않음). 쿠키·AI만 탄다(술래는 나중에 텔레포트 스킬).
// 한 대에 한 명: 앞 사람이 착지한 뒤 Cooldown(2초)이 지나야 다음 사람이 탈 수 있다.
// 시간표(해치 → 들어감 → 닫힘 → 겨눔 → 발사 → 비행)는 콜라 대포(ColaCannonRules)와 같고, 모델 크기만 다르다.
// 모델 축(Unity, 대포 로컬): 포구 = +Z, 뒤 계단·해치 = −Z. 원점 = 받침 가운데 바닥. 포신 모델은 수평(쉬는 각도는 여기서 준다).
public static class SkyCannonRules
{
    public const float Cooldown = 2f;          // 착지 뒤 다음 사람이 탈 수 있을 때까지
    // public const float PinHeight = 2.2f;       // 받침 바닥 → 포신 핀
    // public const float BarrelFront = 2.85f;    // 핀 → 포구
    // public const float BarrelBack = 1.85f;     // 핀 → 뒤 끝(해치)
    // §91 사용자 Meshy 대포 6종(바퀴 달린 포가, 포신 반지름 0.95 m로 맞춤) — 6종 평균. 받침 윗면 0.9 m + 바퀴 바닥 → 핀 3.3 m.
    public const float BaseTop = 0.9f;
    public const float PinHeight = 4.2f;       // 받침 바닥 → 포신 핀
    public const float BarrelFront = 2.9f;     // 핀 → 포구
    public const float BarrelBack = 1.95f;     // 핀 → 뒤 끝(해치)
    public const float RestPitch = 20f;
    public const float FirePitch = 40f;
    public const float BoardReach = 2.8f;      // 뒤 계단 앞에서 E가 뜨는 거리
    public const float AuthorityReach = BoardReach + 1.5f;   // 방장이 받는 거리(지연 여유)
    // public static readonly Vector3 StepsLocal = new Vector3(0f, 0f, -4.9f);   // 맨 아래 계단 앞 바닥
    public static readonly Vector3 StepsLocal = new Vector3(0f, 0f, -5.9f);   // §91 맨 아래 계단 앞 바닥(포가가 길어져 계단을 1 m 뒤로·2배 높이)

    public static Quaternion Rotation(float yaw) => Quaternion.Euler(0f, yaw, 0f);
    public static Vector3 Pivot(Vector3 pos) => pos + Vector3.up * PinHeight;
    public static Vector3 StepsPoint(Vector3 pos, float yaw) => pos + Rotation(yaw) * StepsLocal;
    public static Vector3 Mouth(Vector3 pos, float yaw, float pitch) => Pivot(pos) + ColaCannonRules.BarrelDirection(yaw, pitch) * BarrelFront;
    public static Vector3 Breech(Vector3 pos, float yaw, float pitch) => Pivot(pos) - ColaCannonRules.BarrelDirection(yaw, pitch) * BarrelBack;
    public static Vector3 Inside(Vector3 pos, float yaw, float pitch) => Pivot(pos) - ColaCannonRules.BarrelDirection(yaw, pitch) * 0.8f + Vector3.down * 0.9f;
    public static float PitchAt(double boardAt, double now) => Mathf.Lerp(RestPitch, FirePitch, ColaCannonRules.AimProgress(boardAt, now));

    // 발사 입구(발 기준 — 몸 가운데가 축에 오게 0.9 m 아래)와 착지 자리 사이 비행 시간.
    public static Vector3 LaunchPoint(Vector3 pos, float yaw) => Mouth(pos, yaw, FirePitch) + Vector3.down * 0.9f;
    public static float FlightSeconds(Vector3 pos, float yaw, Vector3 dest)
    {
        Vector3 m = LaunchPoint(pos, yaw);
        return ColaCannonRules.FlightSeconds(new Vector2(dest.x - m.x, dest.z - m.z).magnitude);
    }

    // 한 탑승 기록이 대포를 붙잡고 있는 마지막 시각(착지 + Cooldown).
    public static double ReleaseAt(EscapeState.Cannon c) => c.BoardAt + ColaCannonRules.LandAt(c.Flight) + Cooldown;

    // 같은 대포가 비행 중이거나 착지 뒤 Cooldown 안이면 바쁘다.
    public static bool Busy(EscapeState s, int index, double now)
    {
        if (s == null) return false;
        foreach (EscapeState.Cannon c in s.Cannons)
            if (c.IsSky && c.SkyIndex == index && c.Boarded && now < ReleaseAt(c)) return true;
        return false;
    }

    // 이 대포의 지금 탑승 기록(없으면 false). 바쁜 동안 하나뿐이다.
    public static bool TryActive(EscapeState s, int index, double now, out EscapeState.Cannon ride)
    {
        ride = default;
        if (s == null) return false;
        for (int i = s.Cannons.Count - 1; i >= 0; i--)
        {
            EscapeState.Cannon c = s.Cannons[i];
            if (c.IsSky && c.SkyIndex == index && c.Boarded && now < ReleaseAt(c)) { ride = c; return true; }
        }
        return false;
    }

    // AI 대포 비행 경로의 점(DecoyRoute Fly): 0 = 계단 앞, 1 = 병 안, 2 = 발사 입구, 3 = 착지.
    public static Vector3[] DecoyFlightPoints(Vector3 pos, float yaw, Vector3 dest) => new[]
    {
        StepsPoint(pos, yaw), Inside(pos, yaw, RestPitch), LaunchPoint(pos, yaw), dest,
    };
}
