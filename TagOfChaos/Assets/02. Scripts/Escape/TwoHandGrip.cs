using UnityEngine;

// 두 손 아이템을 가로 끝에서 잡는 손 자리(Request1009Plan.md §88 G3). 모든 값은 몸 기준(오른쪽 x·위 y·앞 z, 루트 원점).
// 1) 높이·앞: 어깨에서 팔 길이 × Drop 아래, × Reach 앞(§87 뒤 사용자가 맞다고 한 높이).
// 2) 아이템 뒷면이 몸 앞면 + BodyGap보다 앞.
// 3) 팔이 모자라면 손을 덜 뻗고, 아이템이 앞에 있어야 하면 손은 아이템 옆면의 뒤쪽을 잡는다(옆면은 깊이 전체가 잡을 곳).
// 4) 그래도 안 닿으면 손은 닿는 끝까지만(Reachable = false), 아이템은 계산한 자리 그대로.
public static class TwoHandGrip
{
    public const float Drop = 0.12f;
    public const float Reach = 0.9f;
    public const float MaxStretch = 0.95f; // 팔을 다 펴지 않는다(팔꿈치가 살짝 굽은 모양)
    public const float BodyGap = 0.02f;
    public const float SideMargin = 0.05f; // 옆면 앞·뒤 끝에서 손 가운데까지
    public const float MinHalfWidth = 0.1f; // 아주 좁은 아이템이어도 두 손이 겹치지 않게

    public struct Input
    {
        public Vector3 ShoulderR, ShoulderL;
        public float ArmR, ArmL;
        public float BodyFrontZ;
        public float HalfWidth, HalfDepth, Inset;
    }

    public struct Result
    {
        public Vector3 HandR, HandL, ItemCenter;
        public bool Reachable;
    }

    public static Result Solve(in Input g)
    {
        float half = Mathf.Max(g.HalfWidth, MinHalfWidth) - g.Inset;
        float y = Mathf.Min(g.ShoulderR.y - g.ArmR * Drop, g.ShoulderL.y - g.ArmL * Drop);
        float wantZ = Mathf.Min(g.ShoulderR.z + g.ArmR * Reach, g.ShoulderL.z + g.ArmL * Reach);
        float itemZ = Mathf.Max(wantZ, g.BodyFrontZ + BodyGap + g.HalfDepth);
        float zR = HandZ(g.ShoulderR, g.ArmR, half, y, itemZ, g.HalfDepth, out bool okR);
        float zL = HandZ(g.ShoulderL, g.ArmL, -half, y, itemZ, g.HalfDepth, out bool okL);
        return new Result
        {
            HandR = new Vector3(half, y, zR),
            HandL = new Vector3(-half, y, zL),
            ItemCenter = new Vector3(0f, y, itemZ),
            Reachable = okR && okL,
        };
    }

    // 옆면(아이템 z ± 깊이 − 여유) 안에서: 팔이 닿으면 아이템 가운데 깊이(닿는 끝이 더 뒤면 그 끝), 안 닿으면 옆면 뒤쪽 끝.
    public static float HandZ(Vector3 shoulder, float arm, float x, float y, float itemZ, float halfDepth, out bool reachable)
    {
        float lo = itemZ - halfDepth + SideMargin, hi = itemZ + halfDepth - SideMargin;
        if (hi < lo) lo = hi = itemZ; // 얇은 아이템(룬 0.16 m): 옆면 가운데
        float r = arm * MaxStretch, dx = x - shoulder.x, dy = y - shoulder.y;
        float flat = r * r - dx * dx - dy * dy;
        float reachZ = flat > 0f ? shoulder.z + Mathf.Sqrt(flat) : shoulder.z;
        reachable = flat > 0f && reachZ >= lo;
        float z = Mathf.Clamp(itemZ, lo, hi);
        return reachable ? Mathf.Min(z, reachZ) : lo;
    }
}
