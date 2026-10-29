using UnityEngine;

// 물풍선 갇힘 물방울(Request1009Plan.md §45.7 WB3·WB4) — 모든 화면. 완전한 구체 + 위쪽에 작게 묶인 매듭,
// 셰이더 TagOfChaos/WaterBubble(투명한 하늘색·청록, 매끄러운 광택, 테두리 굴절). 크기 = 갇힌 몸 경계 상자의 가장 긴 변 × 1.35
// (쿠키 약 3 m, 괴물 약 9.4 m). 몸 가운데를 따라간다. 터질 때 물방울 조각이 사방으로 튀고 물 터지는 소리가 난다.
public class WaterBubbleView : MonoBehaviour
{
    private const string ShaderName = "TagOfChaos/WaterBubble";
    private static Material material;

    private Transform target;
    private Vector3 centerOffset;
    private float growUntil;
    private float diameter;

    public static Material SharedMaterial
    {
        get
        {
            if (material == null)
            {
                Shader s = Shader.Find(ShaderName);
                material = s != null ? new Material(s) { name = "WaterBubble" } : EscapeVisuals.NewMaterial(new Color(0.5f, 0.9f, 1f, 0.35f), 0.4f, true);
            }
            return material;
        }
    }

    // 몸 경계 상자(렌더러)로 크기와 가운데를 잰다.
    public static WaterBubbleView Create(Transform body)
    {
        Bounds b = BodyBounds(body);
        var go = new GameObject("WaterBubble");
        var view = go.AddComponent<WaterBubbleView>();
        view.target = body;
        view.centerOffset = b.center - body.position;
        view.diameter = ToolAim.BubbleDiameter(b.size);
        view.growUntil = Time.time + 0.25f;
        Build(go.transform, view.diameter);
        go.transform.position = b.center;
        go.transform.localScale = Vector3.one * 0.2f;
        return view;
    }

    // 손에 든·날아가는 작은 물풍선(§45.7 WB5)도 같은 모양.
    public static GameObject CreateSmall(Transform parent, float diameter)
    {
        var go = new GameObject("WaterBalloonSmall");
        go.transform.SetParent(parent, false);
        Build(go.transform, diameter);
        return go;
    }

    private static void Build(Transform root, float diameter)
    {
        Material m = SharedMaterial;
        GameObject sphere = Part(PrimitiveType.Sphere, root, m);
        sphere.transform.localScale = Vector3.one;
        // 위쪽 묶인 부분: 납작한 매듭 + 짧은 꼭지(구 지름의 9%)
        GameObject knot = Part(PrimitiveType.Sphere, root, m);
        knot.transform.localPosition = new Vector3(0f, 0.5f + 0.02f, 0f);
        knot.transform.localScale = new Vector3(0.11f, 0.06f, 0.11f);
        GameObject tip = Part(PrimitiveType.Cylinder, root, m);
        tip.transform.localPosition = new Vector3(0f, 0.57f, 0f);
        tip.transform.localScale = new Vector3(0.05f, 0.03f, 0.05f);
        root.localScale = Vector3.one * diameter;
        root.GetChild(0).localScale = Vector3.one;
    }

    private static GameObject Part(PrimitiveType type, Transform parent, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    public static Bounds BodyBounds(Transform body)
    {
        Renderer[] rs = body.GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds b = new Bounds(body.position + Vector3.up, Vector3.one * 2f);
        foreach (Renderer r in rs)
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r.GetComponentInParent<WaterBubbleView>() != null) continue;
            if (r.GetComponent<TMPro.TMP_Text>() != null || r.name == "Star") continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        transform.position = target.position + centerOffset;
        float grow = Time.time < growUntil ? 1f - (growUntil - Time.time) / 0.25f : 1f;
        transform.localScale = Vector3.one * diameter * Mathf.SmoothStep(0.2f, 1f, grow);
    }

    // 터짐: 물 조각이 튀고 물 터지는 소리, 물방울은 사라진다.
    public void Burst()
    {
        Vector3 c = transform.position;
        GameAudio.PlayAt(SoundId.BalloonSplash, c);
        float size = Mathf.Max(0.12f, diameter * 0.05f);
        var ps = EscapeVisuals.CreateParticles("WaterBubbleBurst", null, new EscapeVisuals.ParticleStyle
        {
            Color = new Color(0.55f, 0.92f, 1f, 0.9f), Size = new Vector2(size * 0.6f, size * 1.4f), Speed = new Vector2(diameter * 1.2f, diameter * 2.4f),
            Lifetime = new Vector2(0.6f, 1.1f), Gravity = 1.4f, MaxParticles = 120, EmissionOff = true, Once = true,
            Shape = ParticleSystemShapeType.Sphere, Radius = diameter * 0.45f, AlphaFrom = 0.9f,
        });
        ps.transform.position = c;
        ps.Emit(70);
        Destroy(ps.gameObject, 2f);
        ToolFx.Play(ToolKind.WaterBalloon, c, new Color(0.45f, 0.88f, 1f));
        Destroy(gameObject);
    }
}
