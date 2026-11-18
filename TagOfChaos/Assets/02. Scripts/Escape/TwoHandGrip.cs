using UnityEngine;

// 두 손 아이템을 가로 끝에서 잡는 손 자리(Request1009Plan.md §88 G3). 모든 값은 몸 기준(오른쪽 x·위 y·앞 z, 루트 원점).
// 1) 높이·앞: 어깨에서 팔 길이 × Drop 아래, × Reach 앞(§87 뒤 사용자가 맞다고 한 높이).
// 2) 아이템 뒷면이 몸 앞면 + BodyGap보다 앞.
// 3) 팔이 모자라면 손을 덜 뻗고, 아이템이 앞에 있어야 하면 손은 아이템 옆면의 뒤쪽을 잡는다(옆면은 깊이 전체가 잡을 곳).
// 4) 그래도 안 닿으면 손은 닿는 끝까지만(Reachable = false), 아이템은 계산한 자리 그대로.
// 5) Fit(§88.9, 사용자 결정 "낮춰 들고, 모자라면 작게"): 머리 크고 팔 짧은 캐릭터(마카롱·붕어)는 위 1)~3)로 안 닿는다.
//    같은 높이에서 손을 옆면 뒤 끝 가까이·팔을 끝까지 펴 보고, 안 되면 잡는 높이를 낮추며(높이마다 몸 앞면을 다시 잼),
//    그래도 안 되면 아이템을 5 %씩 줄인다(최소 MinScale). 정한 값(FitParams)은 아이템·캐릭터가 바뀔 때만 다시 구한다(매 프레임 흔들림 없음).
public static class TwoHandGrip
{
    public const float Drop = 0.12f;
    public const float Reach = 0.9f;
    public const float MaxStretch = 0.95f; // 팔을 다 펴지 않는다(팔꿈치가 살짝 굽은 모양)
    public const float BodyGap = 0.02f;
    public const float SideMargin = 0.05f; // 옆면 앞·뒤 끝에서 손 가운데까지
    public const float MinHalfWidth = 0.1f; // 아주 좁은 아이템이어도 두 손이 겹치지 않게
    public const float LooseMargin = 0.02f; // Fit: 옆면 뒤 끝 가까이 잡기
    public const float LooseStretch = 1f;   // Fit: 팔을 끝까지 편다
    public const float MaxDrop = 0.75f;     // Fit: 어깨에서 팔 길이 × 이만큼 아래까지 낮춘다
    public const float HeightStep = 0.02f;
    public const float MinScale = 0.5f;
    public const float ScaleStep = 0.05f;

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
        public float Scale;     // 아이템 크기 배율(Fit이 줄였을 때 < 1)
        public float BodyFront; // 쓴 몸 앞면 z
    }

    // Fit이 정한 값: 기본 높이에서 더 낮춘 길이, 아이템 배율, 옆면 여유·팔 펴기.
    public struct FitParams
    {
        public float ExtraDrop, Scale, Margin, Stretch;
        public static readonly FitParams Normal = new FitParams { ExtraDrop = 0f, Scale = 1f, Margin = SideMargin, Stretch = MaxStretch };
    }

    // frontInBand(아래 y, 위 y): 그 높이 범위 몸 앞면 z. below·above: 잡는 높이에서 아이템 아래·위 끝까지(배율 1).
    public static FitParams Fit(in Input g, float below, float above, System.Func<float, float, float> frontInBand)
    {
        float y0 = BaseY(g), yMin = Mathf.Min(g.ShoulderR.y - g.ArmR * MaxDrop, g.ShoulderL.y - g.ArmL * MaxDrop);
        for (float s = 1f; s >= MinScale - 1e-4f; s -= ScaleStep)
            for (float y = y0; y >= yMin - 1e-4f; y -= HeightStep)
                for (int level = 0; level < 2; level++) // 같은 높이에서 먼저 옆면 뒤 끝·팔 끝까지 → 그다음 낮추기 → 마지막에 줄이기
                {
                    var p = new FitParams { ExtraDrop = y0 - y, Scale = s, Margin = level == 0 ? SideMargin : LooseMargin, Stretch = level == 0 ? MaxStretch : LooseStretch };
                    if (SolveWith(g, p, frontInBand(y - below * s, y + above * s)).Reachable) return p;
                }
        return new FitParams { ExtraDrop = 0f, Scale = MinScale, Margin = LooseMargin, Stretch = LooseStretch };
    }

    private static float BaseY(in Input g) => Mathf.Min(g.ShoulderR.y - g.ArmR * Drop, g.ShoulderL.y - g.ArmL * Drop);

    // Fit 값으로 풀기(매 프레임). bodyFront: 그 높이 범위 몸 앞면.
    public static Result SolveWith(in Input g, FitParams p, float bodyFront)
    {
        float s = p.Scale, hd = g.HalfDepth * s;
        float half = Mathf.Max(g.HalfWidth * s, MinHalfWidth) - g.Inset;
        float y = BaseY(g) - p.ExtraDrop;
        float wantZ = Mathf.Min(g.ShoulderR.z + g.ArmR * Reach, g.ShoulderL.z + g.ArmL * Reach);
        float itemZ = Mathf.Max(wantZ, bodyFront + BodyGap + hd);
        float zR = HandZ(g.ShoulderR, g.ArmR, half, y, itemZ, hd, p.Margin, p.Stretch, out bool okR);
        float zL = HandZ(g.ShoulderL, g.ArmL, -half, y, itemZ, hd, p.Margin, p.Stretch, out bool okL);
        return new Result
        {
            HandR = new Vector3(half, y, zR),
            HandL = new Vector3(-half, y, zL),
            ItemCenter = new Vector3(0f, y, itemZ),
            Reachable = okR && okL,
            Scale = s,
            BodyFront = bodyFront,
        };
    }

    public static Result Solve(in Input g) => SolveWith(g, FitParams.Normal, g.BodyFrontZ);

    // 옆면(아이템 z ± 깊이 − 여유) 안에서: 팔이 닿으면 아이템 가운데 깊이(닿는 끝이 더 뒤면 그 끝), 안 닿으면 옆면 뒤쪽 끝.
    public static float HandZ(Vector3 shoulder, float arm, float x, float y, float itemZ, float halfDepth, out bool reachable) =>
        HandZ(shoulder, arm, x, y, itemZ, halfDepth, SideMargin, MaxStretch, out reachable);

    public static float HandZ(Vector3 shoulder, float arm, float x, float y, float itemZ, float halfDepth, float margin, float stretch, out bool reachable)
    {
        float lo = itemZ - halfDepth + margin, hi = itemZ + halfDepth - margin;
        if (hi < lo) lo = hi = itemZ; // 얇은 아이템(룬 0.16 m): 옆면 가운데
        float r = arm * stretch, dx = x - shoulder.x, dy = y - shoulder.y;
        float flat = r * r - dx * dx - dy * dy;
        float reachZ = flat > 0f ? shoulder.z + Mathf.Sqrt(flat) : shoulder.z;
        reachable = flat > 0f && reachZ >= lo;
        float z = Mathf.Clamp(itemZ, lo, hi);
        return reachable ? Mathf.Min(z, reachZ) : lo;
    }
}
