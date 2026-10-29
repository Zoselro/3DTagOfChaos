using UnityEngine;

// 도구 대상 고르기·물풍선 궤적의 순수 계산(Request1009Plan.md §45.5·§45.7) — 시험(Request1029Tests)으로 확인한다.
public static class ToolAim
{
    public const float HammerHalfAngle = 30f;   // 뿅망치: 전방 60° 부채꼴
    public const float BalloonHalfAngle = 22.5f; // 물풍선: 전방 45° 부채꼴

    // 앞 방향 부채꼴(수평 거리 range, 반각 halfAngle) 안에서 가장 가까운 후보 번호. 없으면 -1. 같은 층(높이 차 4 m 안)만.
    public static int PickInCone(Vector3 origin, Vector3 forward, Vector3[] candidates, float range, float halfAngle)
    {
        Vector3 f = new Vector3(forward.x, 0f, forward.z);
        if (f.sqrMagnitude < 1e-6f || candidates == null) return -1;
        f.Normalize();
        float cos = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 d = candidates[i] - origin;
            if (Mathf.Abs(d.y) > 4f) continue;
            d.y = 0f;
            float distance = d.magnitude;
            if (distance > range || distance < 1e-3f) continue;
            if (Vector3.Dot(d / distance, f) < cos) continue;
            if (distance < bestDistance) { bestDistance = distance; best = i; }
        }
        return best;
    }

    // ---------------- 물풍선 포물선(§45.7 WB1) ----------------

    public const float MinApexLift = 1.5f;   // 시작·끝 중 높은 쪽보다 최소 1.5 m 위
    public const float ApexPerMeter = 0.35f;  // 거리 1 m당 0.35 m
    public const float BaseFlight = 0.5f;     // 비행 시간 = 0.5초 + 거리 × 0.05초
    public const float FlightPerMeter = 0.05f;

    public static float ApexHeight(Vector3 from, Vector3 to)
    {
        float distance = Flat(to - from);
        return Mathf.Max(from.y, to.y) + Mathf.Max(MinApexLift, distance * ApexPerMeter);
    }

    public static float FlightSeconds(Vector3 from, Vector3 to) => BaseFlight + Flat(to - from) * FlightPerMeter;

    // t(0~1)의 위치: 수평은 곧게, 높이는 시작·끝을 지나고 가운데에서 꼭대기(ApexHeight)에 닿는 포물선.
    public static Vector3 ArcPoint(Vector3 from, Vector3 to, float apex, float t)
    {
        t = Mathf.Clamp01(t);
        Vector3 p = Vector3.Lerp(from, to, t);
        // y(t) = (1-t)²·y0 + 2t(1-t)·c + t²·y1, 가운데(t=0.5) 높이 = apex → c = 2·apex - (y0 + y1)/2
        float c = 2f * apex - 0.5f * (from.y + to.y);
        p.y = (1 - t) * (1 - t) * from.y + 2f * t * (1 - t) * c + t * t * to.y;
        return p;
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    // ---------------- 물풍선 크기(§45.7 WB3) ----------------

    public const float BubbleScale = 1.35f;

    // 몸 경계 상자 → 물방울 지름(가장 긴 변 × 1.35).
    public static float BubbleDiameter(Vector3 bodySize) => Mathf.Max(bodySize.x, Mathf.Max(bodySize.y, bodySize.z)) * BubbleScale;
}
