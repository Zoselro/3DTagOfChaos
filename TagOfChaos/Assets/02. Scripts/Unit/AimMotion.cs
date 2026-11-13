using UnityEngine;

// 조준 방향으로 몸을 돌린 채 움직일 때의 순수 계산(Request1009Plan.md §75 B1·Q2·Q6~Q8) — 시험(Request1115Tests)으로 확인한다.
// - 이동 방향이 몸 앞쪽(±60°)이면 걷기, 뒤쪽이면 뒷걸음, 나머지는 왼쪽/오른쪽 옆걸음.
// - 권투 글러브: 첫 주먹은 오른손, 이어 치면(PunchComboWindow 안) 왼손·오른손 번갈아.
public static class AimMotion
{
    public const float ForwardCos = 0.5f;          // 앞·뒤로 보는 범위(±60°)
    public const float AimTurnDegreesPerSecond = 720f;
    public const float PunchComboWindow = 1.5f;

    public static PlayerMoveState Classify(Vector3 move, Vector3 face)
    {
        move.y = 0f; face.y = 0f;
        if (move.sqrMagnitude < 1e-6f || face.sqrMagnitude < 1e-6f) return PlayerMoveState.Idle;
        move.Normalize(); face.Normalize();
        float forward = Vector3.Dot(move, face);
        if (forward >= ForwardCos) return PlayerMoveState.Walk;
        if (forward <= -ForwardCos) return PlayerMoveState.WalkBack;
        Vector3 right = Vector3.Cross(Vector3.up, face); // 몸 오른쪽(왼손 좌표계)
        return Vector3.Dot(move, right) > 0f ? PlayerMoveState.StrafeRight : PlayerMoveState.StrafeLeft;
    }

    public static bool IsAimWalk(PlayerMoveState s) => s == PlayerMoveState.StrafeLeft || s == PlayerMoveState.StrafeRight || s == PlayerMoveState.WalkBack;

    // 다음 주먹이 왼손인지: 직전 주먹이 오른손이고 PunchComboWindow 안이면 왼손, 아니면 오른손(첫 주먹 포함).
    public static bool NextPunchIsLeft(bool lastWasRight, float secondsSinceLast) =>
        lastWasRight && secondsSinceLast >= 0f && secondsSinceLast < PunchComboWindow;
}
