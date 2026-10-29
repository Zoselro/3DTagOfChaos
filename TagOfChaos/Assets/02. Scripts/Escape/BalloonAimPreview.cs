using UnityEngine;

// 물풍선을 들고 있는 동안(본인 화면만) 맞을 대상과 예상 궤적을 보여 준다(Request1009Plan.md §45.7 WB0).
// - 전방 10 m·45° 안 가장 가까운 대상 발밑에 하늘색 고리, 손에서 대상 몸 가운데까지 포물선(던질 때와 같은 ToolAim.ArcPoint).
// - 대상이 없으면 보는 곳 앞 땅까지 흐린 궤적만.
public class BalloonAimPreview : MonoBehaviour
{
    private const int Segments = 24;
    private static readonly Color HitColor = new Color(0.45f, 0.95f, 1f, 0.9f);
    private static readonly Color MissColor = new Color(0.7f, 0.85f, 0.9f, 0.35f);

    private const int RingSegments = 40;
    private LineRenderer line;
    private LineRenderer ring;
    private readonly Vector3[] points = new Vector3[Segments + 1];
    private readonly Vector3[] ringPoints = new Vector3[RingSegments];

    public static BalloonAimPreview Create()
    {
        var go = new GameObject("BalloonAimPreview");
        var p = go.AddComponent<BalloonAimPreview>();
        p.line = go.AddComponent<LineRenderer>();
        p.line.positionCount = Segments + 1;
        p.line.widthMultiplier = 0.1f;
        p.line.material = EscapeVisuals.SoftParticleMaterial();
        p.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var r = new GameObject("TargetRing");
        r.transform.SetParent(go.transform, false);
        p.ring = r.AddComponent<LineRenderer>(); // 바닥과 겹쳐 깜빡이지 않게 얇은 고리 선(원판 대신)
        p.ring.loop = true;
        p.ring.positionCount = RingSegments;
        p.ring.widthMultiplier = 0.12f;
        p.ring.material = EscapeVisuals.SoftParticleMaterial();
        p.ring.startColor = p.ring.endColor = HitColor;
        p.ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.SetActive(false);
        return p;
    }

    public void Show(Vector3 from, Vector3 to, Transform target)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        float apex = ToolAim.ApexHeight(from, to);
        for (int i = 0; i <= Segments; i++) points[i] = ToolAim.ArcPoint(from, to, apex, i / (float)Segments);
        line.SetPositions(points);
        Color c = target != null ? HitColor : MissColor;
        line.startColor = c;
        line.endColor = c;
        ring.gameObject.SetActive(target != null);
        if (target != null)
        {
            Bounds b = WaterBubbleView.BodyBounds(target);
            float radius = Mathf.Max(b.size.x, b.size.z) * 0.45f;
            var centre = new Vector3(b.center.x, target.position.y + 0.12f, b.center.z);
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                ringPoints[i] = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            }
            ring.SetPositions(ringPoints);
        }
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
