using UnityEngine;
using UnityEngine.AI;

// 이어지는 길인지 판정(Request1009Plan.md §54). 아주 긴 길(진저브레드 지상 → 시계탑 나선 내리막 → 넓힌 지하 끝)은
// NavMesh.CalculatePath 한 번의 탐색 한도를 넘어 PathPartial로 끝난다. 그때는 부분 길의 끝에서 이어서 다시 찾는다.
// 더 나아가지 못하면(끝점이 그대로) 정말 끊긴 길이다.
public static class NavPath
{
    private const int MaxHops = 4;
    private const float ArriveSqr = 0.6f * 0.6f;
    private const float StuckSqr = 1f;

    public static bool Reaches(Vector3 from, Vector3 to, NavMeshPath scratch)
    {
        Vector3 at = from;
        for (int i = 0; i < MaxHops; i++)
        {
            if (!NavMesh.CalculatePath(at, to, DecoyNavMesh.Filter, scratch)) return false;
            if (scratch.status == NavMeshPathStatus.PathComplete) return true;
            if (scratch.status == NavMeshPathStatus.PathInvalid || scratch.corners.Length == 0) return false;
            Vector3 end = scratch.corners[scratch.corners.Length - 1];
            if ((end - to).sqrMagnitude < ArriveSqr) return true;
            if ((end - at).sqrMagnitude < StuckSqr) return false;
            at = end;
        }
        return false;
    }
}
