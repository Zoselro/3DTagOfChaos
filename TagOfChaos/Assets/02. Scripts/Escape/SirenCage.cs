using UnityEngine;

// 술래 사이렌 함정(Request1009Plan.md §68 GP5 — 옛 소음폭탄을 대신함). 건물 옆에 놓아 두면 누가 그 건물 수리를 시작하는 순간
// 큰 사이렌과 함께 건물 둘레가 큰 빼빼로 철창(둥근 울타리)에 갇힌다. 안의 사람은 점프 키 연타로 게이지를 채워 철창을 부순다.
// 오래 갇혀 있지 않게 MaxSeconds가 지나면 저절로 풀린다. 규칙·모양 계산만 둔다(방장·화면이 같이 쓴다).
public static class SirenCage
{
    public const int MashNeeded = 30;           // 안의 사람들이 함께 누른 횟수
    public const float MaxSeconds = 20f;        // 이만큼 지나면 저절로 풀림
    public const float MashMinInterval = 0.06f; // 한 사람이 이보다 빨리 누른 것은 세지 않는다
    public const float Margin = 1.5f;           // 건물 경계 밖으로 이만큼 띄워 두른다
    public const float Height = 9f;
    public const float BarSpacing = 1.5f;       // 막대 사이(빠져나가는 것은 보이지 않는 벽 고리가 막는다)
    public const float BarRadius = 0.2f;

    public static bool ShouldRelease(int mash, double cagedAt, double now) => mash >= MashNeeded || now - cagedAt >= MaxSeconds;

    public static float Progress(int mash) => Mathf.Clamp01(mash / (float)MashNeeded);

    // 건물 둘레 원(가운데 = 건물 바닥 가운데, 반지름 = 건물 가로·세로 반 중 큰 쪽 + Margin).
    public static bool Ring(Transform building, out Vector3 center, out float radius)
    {
        center = default;
        radius = 0f;
        if (building == null) return false;
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in building.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer) || !r.gameObject.activeInHierarchy) continue;
            Bounds rb = r.bounds;
            if (rb.extents == Vector3.zero) continue;
            if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
        }
        if (!any) return false;
        center = new Vector3(b.center.x, building.position.y, b.center.z); // 땅 높이 = 건물 뿌리(경계 아래쪽은 땅속까지 내려가 있다)
        radius = Mathf.Max(b.extents.x, b.extents.z) + Margin;
        return true;
    }

    public static bool Inside(Vector3 p, Vector3 center, float radius)
    {
        Vector2 d = new Vector2(p.x - center.x, p.z - center.z);
        return d.magnitude < radius && p.y > center.y - 2f && p.y < center.y + Height + 2f;
    }

    public static int BarCount(float radius) => Mathf.Max(12, Mathf.CeilToInt(2f * Mathf.PI * radius / BarSpacing));
}
