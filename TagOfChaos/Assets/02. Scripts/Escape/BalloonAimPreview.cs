using UnityEngine;

// 바닥 고리 표시(본인 화면만): 녹음기로 고른 AI·사이렌 함정 놓을 자리. 물풍선·섬광탄 궤적 미리보기는 지웠다(§74 P1).
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

    // dotted: 점선 궤적(섬광탄 고각 미리보기, §69 B6).
    public static BalloonAimPreview Create(bool dotted = false)
    {
        var go = new GameObject(dotted ? "FlashbangAimPreview" : "BalloonAimPreview");
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
        if (dotted) p.MakeDotted();
        go.SetActive(false);
        return p;
    }

    private static Texture2D dotTexture;

    // 궤적을 점선으로: 반은 불투명·반은 투명한 작은 무늬를 선 길이를 따라 반복한다.
    private void MakeDotted()
    {
        if (dotTexture == null)
        {
            dotTexture = new Texture2D(8, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "AimDots" };
            for (int x = 0; x < 8; x++) dotTexture.SetPixel(x, 0, x < 4 ? Color.white : Color.clear);
            dotTexture.Apply();
        }
        Shader sprite = Shader.Find("Sprites/Default");
        if (sprite == null) return;
        line.material = new Material(sprite) { mainTexture = dotTexture };
        line.textureMode = LineTextureMode.Tile;
        line.widthMultiplier = 0.12f;
        ring.material = new Material(sprite);
    }

    // 사이렌 함정 놓을 자리(§69 F5): 그 자리 고리만(궤적 없음).
    public void ShowSpot(Vector3 at, float radius, Color color)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        line.positionCount = Segments + 1;
        for (int i = 0; i <= Segments; i++) points[i] = at;
        line.SetPositions(points);
        ring.gameObject.SetActive(true);
        ring.startColor = ring.endColor = color;
        var centre = at + Vector3.up * 0.12f;
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i * Mathf.PI * 2f / RingSegments;
            ringPoints[i] = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
        }
        ring.SetPositions(ringPoints);
    }

    // 녹음기: 고른 AI 발밑 고리만(궤적 없음).
    public void ShowRingOnly(Transform target, Color color)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        line.positionCount = Segments + 1;
        for (int i = 0; i <= Segments; i++) points[i] = target.position;
        line.SetPositions(points);
        ring.gameObject.SetActive(true);
        ring.startColor = ring.endColor = color;
        Bounds b = WaterBubbleView.BodyBounds(target);
        float radius = Mathf.Max(0.5f, Mathf.Max(b.size.x, b.size.z) * 0.55f);
        var centre = new Vector3(b.center.x, target.position.y + 0.12f, b.center.z);
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i * Mathf.PI * 2f / RingSegments;
            ringPoints[i] = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
        }
        ring.SetPositions(ringPoints);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
