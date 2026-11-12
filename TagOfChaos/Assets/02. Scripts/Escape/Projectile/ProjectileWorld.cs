using UnityEngine;

// 투사체가 부딪히는 세상(Request1009Plan.md §70.3.4): 벽·바닥·캐릭터 몸(트리거는 무시). 던진 사람 자신의 몸은 지나간다.
public static class ProjectileWorld
{
    private static int mask = -1;
    private static readonly RaycastHit[] hits = new RaycastHit[16];

    // 지나가지 않는 레이어: 기본 레이캐스트 레이어에서 부서진 쿠키(시체)를 뺀다.
    public static int Mask
    {
        get
        {
            if (mask != -1) return mask;
            int m = Physics.DefaultRaycastLayers;
            int broken = LayerMask.NameToLayer("BrokenCookie");
            if (broken >= 0) m &= ~(1 << broken);
            mask = m;
            return mask;
        }
    }

    // 다음 Sweep이 지나갈 몸(던진 사람). 투사체마다 진행 직전에 정한다.
    public static Transform Ignore;

    public static readonly BallisticSim.Sweep Sweep = SweepImpl;

    private static bool SweepImpl(Vector3 from, Vector3 direction, float distance, float radius, out Vector3 centerAtHit, out Vector3 normal, out Collider collider)
    {
        centerAtHit = from;
        normal = Vector3.up;
        collider = null;
        int count = Physics.SphereCastNonAlloc(from, radius, direction, hits, distance, Mask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hits[i];
            // 시작부터 겹친 것(distance 0)은 건너뛴다 — 바닥에 놓인 채로 굴러갈 때 이미 맞닿은 면에 다시 막히지 않게
            if (h.distance <= 0f || h.distance >= best) continue;
            if (Ignore != null && h.collider.transform.IsChildOf(Ignore)) continue;
            best = h.distance;
            centerAtHit = from + direction * h.distance;
            normal = h.normal;
            collider = h.collider;
        }
        return collider != null;
    }

    // 닿은 충돌체의 캐릭터(없으면 null).
    public static IGameCharacter CharacterOf(Collider c) => c != null ? c.GetComponentInParent<IGameCharacter>() : null;
}
