using UnityEngine;

// 거대 괴물 마법진(Request1009Plan.md §64.7). 바닥에 원이 서서히 그려지고(ring 0→1), 그 안에 오각별이 한 획씩 그려진다(star 0→1).
// 빛나는 선(LineRenderer 두 개)과 바닥 빛 원판. 등장(3초)·층 이동(1초씩)에 같이 쓴다 — 진행값은 부르는 쪽이 공통 시계로 넣는다.
public class GiantSummonCircle : MonoBehaviour
{
    private const int RingSegments = 96;
    private const float LineWidth = 0.35f;
    private const float Lift = 0.08f;           // 바닥에서 띄우는 높이(겹침 깜빡임 방지)
    private static readonly int[] StarOrder = { 0, 2, 4, 1, 3, 0 }; // 오각별 한 붓 그리기

    private LineRenderer ring, star;
    private Transform glow;
    private Material lineMaterial, glowMaterial;
    private Color color;
    private float radius;
    private readonly Vector3[] ringPoints = new Vector3[RingSegments + 1];
    private readonly Vector3[] starPoints = new Vector3[StarOrder.Length];

    public static GiantSummonCircle Create(Transform parent, Color color, float glowStrength, float radius)
    {
        var go = new GameObject("GiantSummonCircle");
        go.transform.SetParent(parent, false);
        var c = go.AddComponent<GiantSummonCircle>();
        c.Build(color, glowStrength, radius);
        return c;
    }

    private void Build(Color c, float glowStrength, float r)
    {
        color = c;
        radius = r;
        lineMaterial = EscapeVisuals.NewMaterial(c, glowStrength, fade: true);
        ring = NewLine("Ring", RingSegments + 1);
        star = NewLine("Star", StarOrder.Length);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Glow";
        glow = quad.transform;
        glow.SetParent(transform, false);
        glow.localPosition = Vector3.up * (Lift * 0.5f);
        glow.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glow.localScale = Vector3.one * r * 2.6f;
        glowMaterial = new Material(EscapeVisuals.SoftParticleMaterial());
        var qr = quad.GetComponent<Renderer>();
        qr.sharedMaterial = glowMaterial;
        qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        for (int i = 0; i <= RingSegments; i++)
        {
            float a = i / (float)RingSegments * Mathf.PI * 2f;
            ringPoints[i] = new Vector3(Mathf.Sin(a) * r, Lift, Mathf.Cos(a) * r);
        }
        for (int i = 0; i < StarOrder.Length; i++)
        {
            float a = StarOrder[i] / 5f * Mathf.PI * 2f;
            starPoints[i] = new Vector3(Mathf.Sin(a) * r * 0.94f, Lift, Mathf.Cos(a) * r * 0.94f);
        }
        Set(0f, 0f, 0f);
    }

    private LineRenderer NewLine(string name, int capacity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.widthMultiplier = LineWidth;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View; // 어느 쪽에서 봐도 선 굵기가 같다
        lr.sharedMaterial = lineMaterial;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.positionCount = 0;
        return lr;
    }

    // ring·star: 그려진 정도(0~1), alpha: 전체 밝기(사라질 때 1→0).
    public void Set(float ringProgress, float starProgress, float alpha)
    {
        DrawPartial(ring, ringPoints, ringProgress);
        DrawPartial(star, starPoints, starProgress);
        Color c = color;
        c.a = alpha;
        lineMaterial.color = c;
        Color g = color;
        g.a = alpha * 0.55f * Mathf.Max(ringProgress, 0.2f);
        glowMaterial.color = g;
        bool any = alpha > 0.001f && (ringProgress > 0f || starProgress > 0f);
        if (gameObject.activeSelf != any) gameObject.SetActive(any);
    }

    // 점 목록을 progress만큼 이어 그린다(마지막 점은 구간 중간까지).
    private static void DrawPartial(LineRenderer lr, Vector3[] points, float progress)
    {
        if (progress <= 0f) { lr.positionCount = 0; return; }
        float span = Mathf.Clamp01(progress) * (points.Length - 1);
        int full = Mathf.FloorToInt(span);
        int count = Mathf.Min(points.Length, full + 2);
        lr.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = i <= full ? points[i] : Vector3.Lerp(points[full], points[full + 1], span - full);
            lr.SetPosition(i, p);
        }
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
    }
}
