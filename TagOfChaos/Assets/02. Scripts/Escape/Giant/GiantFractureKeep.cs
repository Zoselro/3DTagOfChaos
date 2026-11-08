using UnityEngine;

// 거대 괴물이 건물을 무너뜨려도 남겨 두는 부분(Request1009Plan.md §66 — 사용자 결정: 재료 꽂는 곳은 부서지지 않는다).
// 재료 받침(Socket_)·받침 빛(ESC_Glow)·수리 자리(Repair_Point)와 충돌체 전용(COL_)·조명(LIGHT_)은 조각 모델에서 빼고,
// 무너질 때도 숨기지 않는다. 이름 규칙은 QuadrantBuilding(FindPrefix)과 같다. 조각 내보내기(FractureSourceExporter)와 실행 중 붕괴가 함께 쓴다.
public static class GiantFractureKeep
{
    public static readonly string[] Prefixes = { "Socket_", "ESC_Glow", "Repair_Point", "COL_", "LIGHT_" };

    // t 또는 root까지의 조상 중 하나라도 남길 이름이면 true.
    public static bool IsKept(Transform t, Transform root)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            foreach (string prefix in Prefixes)
                if (p.name.StartsWith(prefix)) return true;
            if (p == root) break;
        }
        return false;
    }
}
