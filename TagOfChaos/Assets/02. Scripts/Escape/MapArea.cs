using UnityEngine;

// 놀이 공간의 수평 경계 — 사각형(테스트 맵 Ground) 또는 원(캔디숲 실제 모델 맵, Request1009Plan.md §28.6).
// 맵 밖 판정·가장 가까운 가장자리 방향(헬리콥터 모자)·안쪽 무작위 지점(호박마차·AI)을 한 곳에서 계산한다.
public readonly struct MapArea
{
    public readonly Rect Rect;      // 원이면 원을 감싸는 사각형
    public readonly Vector2 Center; // x·z
    public readonly float Radius;   // 0이면 사각형

    public bool IsCircle => Radius > 0f;

    public MapArea(Rect rect)
    {
        Rect = rect;
        Center = rect.center;
        Radius = 0f;
    }

    public MapArea(Vector2 center, float radius)
    {
        Center = center;
        Radius = Mathf.Max(0.01f, radius);
        Rect = new Rect(center.x - Radius, center.y - Radius, Radius * 2f, Radius * 2f);
    }

    public bool IsOutside(Vector3 p, float margin)
    {
        if (IsCircle) return Flat(p).magnitude > Radius + margin;
        return p.x < Rect.xMin - margin || p.x > Rect.xMax + margin || p.z < Rect.yMin - margin || p.z > Rect.yMax + margin;
    }

    // 이 점에서 가장 가까운 가장자리로 향하는 수평 방향. 원이면 가운데에서 바깥쪽(가운데 위면 북쪽).
    public Vector3 EdgeDirection(Vector3 p)
    {
        if (!IsCircle) return RectEdgeDirection(Rect, p);
        Vector2 d = Flat(p);
        return d.sqrMagnitude < 1e-4f ? Vector3.forward : new Vector3(d.x, 0f, d.y).normalized;
    }

    // 경계에서 inset m 안쪽의 고른 무작위 지점(x·z).
    public Vector2 RandomPoint(System.Random random, float inset)
    {
        if (IsCircle)
        {
            float r = Mathf.Max(0f, Radius - inset) * Mathf.Sqrt((float)random.NextDouble());
            float a = (float)random.NextDouble() * Mathf.PI * 2f;
            return Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return new Vector2(Mathf.Lerp(Rect.xMin + inset, Rect.xMax - inset, (float)random.NextDouble()),
                           Mathf.Lerp(Rect.yMin + inset, Rect.yMax - inset, (float)random.NextDouble()));
    }

    public static Vector3 RectEdgeDirection(Rect map, Vector3 p)
    {
        float left = p.x - map.xMin, right = map.xMax - p.x, down = p.z - map.yMin, up = map.yMax - p.z;
        float best = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
        if (best == left) return Vector3.left;
        if (best == right) return Vector3.right;
        return best == down ? Vector3.back : Vector3.forward;
    }

    private Vector2 Flat(Vector3 p) => new Vector2(p.x - Center.x, p.z - Center.y);
}
