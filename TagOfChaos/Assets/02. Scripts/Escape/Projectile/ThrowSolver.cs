using UnityEngine;

// 던질 목표와 처음 속도(Request1009Plan.md §70.3.4). 카메라 가운데 광선이 닿은 곳(허공이면 baseRange 앞의 땅)을 목표로,
// 손에서 그곳에 떨어지는 포물선 속도를 구한다. 미리보기·던지기·방장 검증이 같은 계산을 쓴다.
public static class ThrowSolver
{
    public const float MinDistance = 0.5f;
    public const float SteepExtraDegrees = 12f; // 목표가 높으면 올려다보는 각보다 이만큼 더 세운다

    public const float MinThrow = 1.5f; // 발밑·뒤쪽을 봐도 적어도 이만큼은 앞으로 던진다

    // 카메라 광선(aim)이 닿은 곳. 3인칭 카메라는 몸 뒤에 있으므로 광선은 손 깊이(cameraToOrigin)부터 쏜다 — 카메라와 몸 사이의 땅을
    // 목표로 잡아 뒤로 던지지 않게. 허공이면 baseRange 앞, 사거리(maxRange) 밖이면 안으로 당기고 그 아래 땅을 목표로 한다.
    // 목표가 몸 뒤이거나 MinThrow보다 가까우면 보는 방향 MinThrow 앞으로 민다.
    public static Vector3 TargetPoint(Ray aim, Vector3 origin, ProjectileConfigSO c, int worldMask, float cameraToOrigin)
    {
        Vector3 start = aim.origin + aim.direction * cameraToOrigin;
        bool looked = Physics.Raycast(start, aim.direction, out RaycastHit hit, c.maxRange * 1.5f, worldMask, QueryTriggerInteraction.Ignore);
        Vector3 point = looked ? hit.point : start + aim.direction * c.baseRange;
        Vector3 forward = new Vector3(aim.direction.x, 0f, aim.direction.z);
        forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
        Vector3 flat = new Vector3(point.x - origin.x, 0f, point.z - origin.z);
        bool moved = false;
        if (Vector3.Dot(flat, forward) < MinThrow) { point = origin + forward * MinThrow; moved = true; }
        else if (flat.magnitude > c.maxRange) { point = new Vector3(origin.x, point.y, origin.z) + flat.normalized * c.maxRange; moved = true; }
        if ((!looked || moved) && Physics.Raycast(new Vector3(point.x, Mathf.Max(point.y, origin.y) + 2f, point.z), Vector3.down, out RaycastHit ground, 40f, worldMask, QueryTriggerInteraction.Ignore))
            point = ground.point;
        return point;
    }

    // origin에서 target에 떨어지는 처음 속도. h = d·tanθ − g·d² / (2·v²·cos²θ) → v² = g·d² / (2·cos²θ·(d·tanθ − h)).
    // 목표가 높으면 각을 세운다(올려다보는 각 + 12°).
    public static Vector3 LaunchVelocity(Vector3 origin, Vector3 target, float angleDeg, float gravity)
    {
        Vector3 flat = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
        float d = Mathf.Max(MinDistance, flat.magnitude);
        float h = target.y - origin.y;
        Vector3 dir = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
        float theta = Mathf.Max(angleDeg * Mathf.Deg2Rad, Mathf.Atan2(h, d) + SteepExtraDegrees * Mathf.Deg2Rad);
        theta = Mathf.Clamp(theta, 5f * Mathf.Deg2Rad, 80f * Mathf.Deg2Rad);
        float cos = Mathf.Cos(theta);
        float v = Mathf.Sqrt(gravity * d * d / Mathf.Max(0.01f, 2f * cos * cos * (d * Mathf.Tan(theta) - h)));
        return dir * (v * cos) + Vector3.up * (v * Mathf.Sin(theta));
    }

    // 방장 검증 상한: 평지에서 maxRange를 던지는 속도.
    public static float FlatSpeed(ProjectileConfigSO c) =>
        LaunchVelocity(Vector3.zero, Vector3.forward * c.maxRange, c.launchAngle, c.gravity).magnitude;

    // 위쪽 목표(건물 위·계단)는 더 세게 던지므로 여유를 둔다.
    public const float SpeedSlack = 1.6f;
    public static float MaxSpeed(ProjectileConfigSO c) => FlatSpeed(c) * SpeedSlack;
}
