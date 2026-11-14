using UnityEngine;

// 움직일 때 몸이 키 방향으로 도는 빠르기(Request1009Plan.md §78 R1 — 사용자: 0·90·180·−90°로 한 번에 휙 돌지 말고 사이 각도를 거쳐 돌게).
// 이동 방향(속도)은 키를 누르는 순간 바뀌고, 몸만 이 빠르기로 따라 돈다. 우클릭 조준 중 회전(AimMotion.AimTurnDegreesPerSecond)과 같은 720°/초.
public static class MoveTurn
{
    public const float DegreesPerSecond = 720f; // 뒤돌기 0.25초, 90° 0.125초

    public static Quaternion Step(Quaternion from, Vector3 lookDir, float dt)
    {
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 1e-6f) return from;
        return Quaternion.RotateTowards(from, Quaternion.LookRotation(lookDir), DegreesPerSecond * dt);
    }
}
