using System;
using UnityEngine;

// 받은(Meshy) 건물 겉 모델을 메시 충돌로 두면 계단·통·차양을 타고 올라가 건물 속 빈 곳·지붕에 숨는다(통행 검사 §36 CM7).
// 그래서 방과 문 통로를 뺀 건물 바닥 자리를 높은 상자로 채운다(0.5 m 격자, 줄마다 이어 붙임). 놀이공원 재료 건물(§36)과
// 초콜릿 공장 건물(§41)이 함께 쓴다.
//   건물 자리 = 바깥에서 안으로 쏜 겉 건물(exterior)까지의 거리 안(사람 높이 아래, 1°마다, 양옆 ±8°의 가장 바깥까지)
//   열린 곳 = open(x, z)가 참인 곳(방 바닥·문 통로 — 부르는 쪽이 정한다)
//   열기(깎고 다시 키우기): 2.5 m보다 좁은 빈 띠(괴물 통로보다 좁다)는 막는다 — 넓은 방·통로는 그대로
// 좌표는 root 로컬(건물 원점 기준, 만드는 동안 root는 원점에 있다).
public static class BuildingFill
{
    public const float WallHeight = 9f;      // 오를 수 없는 높이(쿠키 점프 1.8 m) — 지붕·차양 위로 못 간다
    public const float Cell = 0.5f;
    private const int OuterWindow = 8;
    private const int OpenRadius = 2;          // 열기 창 5 × 5칸(2.5 m)
    private const float RayStart = 45f;
    private static readonly float[] OuterRayHeights = { 0.3f, 1.2f, 2.2f }; // 사람 높이 아래 겉 건물 끝(차양은 빼고)

    // 바깥 둘레 반지름(1°마다, 이웃 ±8° 최댓값)
    public static float[] OuterRadii(Collider exterior)
    {
        Physics.SyncTransforms();
        var outer = new float[360];
        for (int d = 0; d < 360; d++)
        {
            var dir = new Vector3(Mathf.Sin(d * Mathf.Deg2Rad), 0f, Mathf.Cos(d * Mathf.Deg2Rad));
            foreach (float h in OuterRayHeights)
                if (exterior.Raycast(new Ray(dir * RayStart + Vector3.up * h, -dir), out RaycastHit hit, RayStart)) outer[d] = Mathf.Max(outer[d], RayStart - hit.distance);
        }
        // 오목한 곳(앞 장식 사이 틈)도 채운다: 쿠키만 들어가는 좁은 틈이 생기지 않게
        var smooth = new float[360];
        for (int d = 0; d < 360; d++)
            for (int k = -OuterWindow; k <= OuterWindow; k++) smooth[d] = Mathf.Max(smooth[d], outer[(d + k + 360) % 360]);
        return smooth;
    }

    // COL_Fill 아래 상자들을 만들고 상자 수를 돌려준다.
    public static int Build(Transform root, Collider exterior, Func<float, float, bool> open, float reach)
    {
        float[] outer = OuterRadii(exterior);
        int n = Mathf.RoundToInt(reach * 2f / Cell);
        var solid = new bool[n, n];
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = -reach + (ix + 0.5f) * Cell, z = -reach + (iz + 0.5f) * Cell;
                int deg = (Mathf.RoundToInt(Mathf.Atan2(x, z) * Mathf.Rad2Deg) + 360) % 360;
                solid[ix, iz] = new Vector2(x, z).magnitude < outer[deg] && !open(x, z);
            }
        CloseNarrowGaps(solid, n);
        return EmitBoxes(root, solid, n, reach);
    }

    private static void CloseNarrowGaps(bool[,] solid, int n)
    {
        bool Open(int x, int z) => x < 0 || z < 0 || x >= n || z >= n || !solid[x, z];
        var eroded = new bool[n, n];
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                bool all = true;
                for (int dz = -OpenRadius; dz <= OpenRadius && all; dz++) for (int dx = -OpenRadius; dx <= OpenRadius && all; dx++) all = Open(ix + dx, iz + dz);
                eroded[ix, iz] = all;
            }
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                if (!Open(ix, iz)) continue;
                bool keep = false;
                for (int dz = -OpenRadius; dz <= OpenRadius && !keep; dz++)
                    for (int dx = -OpenRadius; dx <= OpenRadius && !keep; dx++)
                    {
                        int x = ix + dx, z = iz + dz;
                        keep = x < 0 || z < 0 || x >= n || z >= n || eroded[x, z];
                    }
                if (!keep) solid[ix, iz] = true;
            }
    }

    private static int EmitBoxes(Transform root, bool[,] solid, int n, float reach)
    {
        Transform parent = new GameObject("COL_Fill").transform;
        parent.SetParent(root, false);
        int boxes = 0;
        for (int iz = 0; iz < n; iz++)
        {
            float z = -reach + (iz + 0.5f) * Cell;
            int runStart = -1;
            for (int ix = 0; ix <= n; ix++)
            {
                bool cell = ix < n && solid[ix, iz];
                if (cell && runStart < 0) runStart = ix;
                if (!cell && runStart >= 0)
                {
                    float x0 = -reach + runStart * Cell, x1 = -reach + ix * Cell;
                    var t = new GameObject($"COL_Fill_{boxes:000}").transform;
                    t.SetParent(parent, false);
                    t.localPosition = new Vector3((x0 + x1) / 2f, WallHeight / 2f, z);
                    t.gameObject.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, WallHeight, Cell);
                    boxes++;
                    runStart = -1;
                }
            }
        }
        return boxes;
    }
}
