using System.Collections.Generic;
using UnityEngine;

// 거대 괴물이 물체를 부수는 규칙(Request1009Plan.md §66.4). GiantBrain(방장)이 쓴다.
// - 걷는 동안 몸 앞 상자(폭 BodyHalfWidth×2, 앞 Ahead)에 닿은 소품은 바로 부서진다(멈추지 않음).
// - 앞을 막는 텐트·건물이 있으면 멈춰서 그쪽을 밟는다(밟기 1번 = 1단계).
// - 사람 판정: 아직 서 있는 텐트·건물 안의 사람은 벽이 막아 살고(마지막 붕괴 밟기 포함 — 판정 먼저, 붕괴는 그 뒤), 무너진 곳·바깥은 죽는다.
// - 계단(§68 GP6): 지나가다 닿아서는 부서지지 않는다. 대상이 계단 구역(나선 내리막 + 이어진 시계탑)에 있을 때만 그 위에서 10번 밟아 부순다.
public static class GiantDestruction
{
    public const float BodyHalfWidth = 4.5f;   // 몸 폭 9 m의 절반
    public const float BodyHeight = 10f;
    public const float ProtectInset = 0.3f;    // 건물 경계 상자를 이만큼 줄여 안을 잰다(입구 문턱에 선 사람은 맞음)
    public const float StompPropRadius = GiantRules.StompRadius + 1f;

    // 걷는 속도에 맞춘 앞쪽 거리(생각 주기 0.25초 동안 걷는 거리 + 여유)
    public static float Ahead(float speed) => 1.5f + Mathf.Max(0f, speed) * 0.4f;

    // 몸 앞 상자(세계): 가운데·반 크기·방향
    public static void FrontBox(Vector3 body, float yaw, float ahead, out Vector3 center, out Vector3 half, out Quaternion rot)
    {
        rot = Quaternion.Euler(0f, yaw, 0f);
        half = new Vector3(BodyHalfWidth, BodyHeight * 0.5f, (GiantRules.FrontReach + ahead) * 0.5f);
        center = body + Vector3.up * (BodyHeight * 0.5f + 0.3f) + rot * Vector3.forward * half.z;
    }

    // 텐트·건물이 아직 서 있는지(맞은 횟수가 남았는지)
    public static bool Standing(DestructibleUnit u) => u != null && u.Grade != DestructibleGrade.Prop && !u.Broken;

    public const float StairsReach = GiantRules.FrontReach + GiantRules.StompTriggerRange + 2f; // 몸에서 계단 구역까지 이만큼이면 밟는다

    // 대상이 서 있는 계단 구역 안이면 그 계단(없으면 null)
    public static DestructibleUnit StairsOf(Vector3 p, IReadOnlyList<DestructibleUnit> units)
    {
        if (units == null) return null;
        foreach (DestructibleUnit u in units)
        {
            if (u.Grade != DestructibleGrade.Stairs || u.Broken) continue;
            Bounds z = u.Zone; z.Expand(new Vector3(2f, 2f, 2f));
            if (z.Contains(p)) return u;
        }
        return null;
    }

    // 이 자리(몸)에서 계단을 밟을 수 있는지: 구역까지 수평 StairsReach 안, 높이는 구역 위아래 안
    public static bool CanHitStairs(Vector3 body, DestructibleUnit stairs)
    {
        if (stairs == null) return false;
        Bounds z = stairs.Zone;
        if (body.y < z.min.y - 1f || body.y > z.max.y + 1f) return false;
        return GiantRules.FlatDistance(body, z.ClosestPoint(body)) <= StairsReach;
    }

    // 이 자리가 서 있는 텐트·건물 안인지(벽이 막아 줌)
    public static bool IsProtected(Vector3 p, IReadOnlyList<DestructibleUnit> units, out DestructibleUnit shelter)
    {
        shelter = null;
        if (units == null) return false;
        foreach (DestructibleUnit u in units)
        {
            if (!Standing(u)) continue;
            Bounds b = u.Zone; // 계단은 이어진 시계탑까지
            if (p.x < b.min.x + ProtectInset || p.x > b.max.x - ProtectInset) continue;
            if (p.z < b.min.z + ProtectInset || p.z > b.max.z - ProtectInset) continue;
            if (p.y < b.min.y - 1f || p.y > b.max.y) continue;
            shelter = u;
            return true;
        }
        return false;
    }

    // 대상을 쫓는 비용에 더할 길이: 숨은 건물을 부수는 데 드는 시간 × 지금 속도(§66.4 어그로)
    public static float AggroExtra(Vector3 target, IReadOnlyList<DestructibleUnit> units, float speed)
    {
        if (!IsProtected(target, units, out DestructibleUnit shelter)) return 0f;
        int left = shelter.HitsToBreak - shelter.Level;
        return left * (GiantRules.StompSeconds + GiantRules.StompCooldown) * Mathf.Max(GiantRules.BaseSpeed, speed);
    }

    // 몸 앞 상자에 닿는 물체: 소품은 props에 모으고, 가장 가까운 서 있는 텐트·건물을 돌려준다.
    public static DestructibleUnit Sweep(DestructibleCatalog catalog, Vector3 body, float yaw, float ahead, Collider[] buffer, List<DestructibleUnit> props)
    {
        props?.Clear();
        if (catalog == null || catalog.Count == 0) return null;
        FrontBox(body, yaw, ahead, out Vector3 center, out Vector3 half, out Quaternion rot);
        DestructibleUnit blocking = null;
        float best = float.MaxValue;
        int n = Physics.OverlapBoxNonAlloc(center, half, buffer, rot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (!catalog.TryGet(buffer[i], out DestructibleUnit u) || u.Broken || u.Grade == DestructibleGrade.Stairs) continue; // 계단은 대상이 있을 때만
            if (u.Grade == DestructibleGrade.Prop)
            {
                if (props != null && !props.Contains(u)) props.Add(u);
                continue;
            }
            float d = GiantRules.FlatDistance(body, u.Bounds.ClosestPoint(body));
            if (d < best) { best = d; blocking = u; }
        }
        // 충돌체가 없는 소품(꾸미기)은 경계 상자로 잰다
        if (props != null)
        {
            var box = new Bounds(center, Vector3.zero);
            box.Encapsulate(center + rot * new Vector3(half.x, half.y, half.z));
            box.Encapsulate(center + rot * new Vector3(-half.x, -half.y, half.z));
            box.Encapsulate(center + rot * new Vector3(half.x, half.y, -half.z));
            box.Encapsulate(center + rot * new Vector3(-half.x, -half.y, -half.z));
            foreach (DestructibleUnit u in catalog.Units)
            {
                if (u.Grade != DestructibleGrade.Prop || u.Broken || u.Colliders.Length > 0 || !box.Intersects(u.Bounds)) continue;
                Vector3 local = Quaternion.Inverse(rot) * (u.Bounds.ClosestPoint(center) - center);
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z && !props.Contains(u)) props.Add(u);
            }
        }
        return blocking;
    }

    // 밟은 자리 둘레의 소품(밟기 한 번에 함께 부서짐)
    public static void PropsAround(DestructibleCatalog catalog, Vector3 center, List<DestructibleUnit> into)
    {
        into.Clear();
        if (catalog == null) return;
        foreach (DestructibleUnit u in catalog.Units)
        {
            if (u.Grade != DestructibleGrade.Prop || u.Broken) continue;
            Vector3 c = u.Bounds.ClosestPoint(center);
            if (GiantRules.FlatDistance(c, center) <= StompPropRadius && Mathf.Abs(c.y - center.y) <= GiantRules.StompHeight + 2f) into.Add(u);
        }
    }

    // 사람이 있는 텐트·건물(없으면 null)
    public static DestructibleUnit ShelterOf(Vector3 p, IReadOnlyList<DestructibleUnit> units) =>
        IsProtected(p, units, out DestructibleUnit s) ? s : null;
}
