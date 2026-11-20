using UnityEngine;

// §90 B3 술래 바주카포 규칙(순수 계산 — 시험). 우클릭 조준(걷기 50%, 돌진·잡기 불가) + 좌클릭 발사, 쿨타임 10초.
// 로켓은 곧게(중력 거의 없음) 초속 45 m로 최대 90 m. 닿으면 폭발:
// - 사람(쿠키·스파이)·AI: 폭발 중심 3.5 m 안 = 잡힘(DeathCause.Bazooka), 3.5~6 m = 기절 1.5초. 벽 뒤(중심에서 몸까지 막힘)는 안전.
// - 물체: 맞은 것이 재료 건물(QuadrantBuilding)이 아닌 건물·텐트·소품이면 한 방에 무너짐 + 둘레 6 m 소품도 부서짐.
// - 술래·마녀·거대 괴물은 맞지 않는다.
public static class BazookaRules
{
    public const int ThrowSlot = -2;          // EscapeOp.Throw A(-1 = 수색 폭탄)
    public const float Cooldown = 10f;
    public const float Speed = 45f;
    public const float MaxRange = 90f;
    public const float KillRadius = 3.5f;
    public const float StunRadius = 6f;
    public const float PropRadius = 6f;
    public const float StunSeconds = 1.5f;
    public const float AimWalkScale = 0.5f;   // 조준 중 걷기(사용자 결정)
    public const float AimHoldSeconds = 0.25f;
    public const float MuzzleReach = 7f;      // 방장 검사: 포구가 술래 기록 위치에서 수평으로 이만큼 안(어깨 위 4.1 m 포) // 우클릭 후 어깨에 메는 시간(이 뒤에야 쏠 수 있다)

    public enum Effect { None, Stun, Kill }

    public static Effect EffectAt(float distance, bool blocked)
    {
        if (blocked) return Effect.None;
        if (distance <= KillRadius) return Effect.Kill;
        if (distance <= StunRadius) return Effect.Stun;
        return Effect.None;
    }

    public static float MaxLifeSeconds => MaxRange / Speed;

    // 이 물체를 바주카포가 부술 수 있는지: 재료 건물(QuadrantBuilding 안팎)과 계단은 안 된다.
    public static bool CanBreak(DestructibleUnit u) =>
        u != null && !u.Broken && u.Root != null && u.Grade != DestructibleGrade.Stairs && !IsMaterialBuilding(u.Root);

    public static bool IsMaterialBuilding(Transform t) =>
        t != null && (t.GetComponentInParent<QuadrantBuilding>() != null || t.GetComponentInChildren<QuadrantBuilding>(true) != null);

    // 폭발 둘레 소품: 물체 경계에서 폭발 중심까지가 PropRadius 안.
    public static bool PropInBlast(DestructibleUnit u, Vector3 center) =>
        u != null && u.Grade == DestructibleGrade.Prop && Vector3.Distance(u.Bounds.ClosestPoint(center), center) <= PropRadius;
}
