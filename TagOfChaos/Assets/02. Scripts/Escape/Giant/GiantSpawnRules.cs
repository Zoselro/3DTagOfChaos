using System.Collections.Generic;
using UnityEngine;

// 거대 괴물이 나오는 자리·마법진으로 솟아오르는 자리 규칙(Request1009Plan.md §81 G1, 사용자 결정 Q3: 큰 건물·계단 구역만 막는다).
// 거대 괴물 NavMesh는 부서지는 건물을 빼고 구워(GiantNavMeshBaker — 지나가며 부수도록) 건물 바닥 안에도 길이 있다.
// 그래서 길 위 점이라도 몸(반지름 + 여유)이 서 있는 건물·계단 구역과 겹치면 버린다. 텐트·소품은 막지 않는다.
public static class GiantSpawnRules
{
    public const float BodyClearance = GiantNavMesh.AgentRadius + 1.5f; // 몸 반지름 3 m + 여유 — 몸이 벽에 박혀 보이지 않게

    public static bool Blocks(DestructibleUnit u) =>
        u != null && !u.Broken && (u.Grade == DestructibleGrade.Building || u.Grade == DestructibleGrade.Stairs);

    // 이 자리가 서 있는 큰 건물·계단 구역과 수평 BodyClearance 안에서 겹치지 않으면 true(높이는 구역 위아래 안일 때만 본다).
    public static bool ClearOfBuildings(Vector3 p, IReadOnlyList<DestructibleUnit> units)
    {
        if (units == null) return true;
        foreach (DestructibleUnit u in units)
        {
            if (!Blocks(u)) continue;
            Bounds z = u.Zone;
            if (p.y < z.min.y - 1f || p.y > z.max.y) continue;
            if (GiantRules.FlatDistance(p, z.ClosestPoint(p)) < BodyClearance) return false;
        }
        return true;
    }
}
