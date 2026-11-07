using UnityEngine;

// 거대 괴물에게 밟힌 연출(Request1009Plan.md §64.6) — 모든 화면, 판정 값(DeathCause.Giant)을 받은 순간.
// - 쿠키: 캐릭터 가루색 부스러기가 사방으로 튀어 바닥을 구르다(충돌·마찰) 멈추고 작아져 사라진다.
// - 슬라임 캐릭터(에이드공주): 부스러기 없이 분홍 액체가 슬라임처럼 퍼져 SlimeHoldSeconds 머문 뒤 줄어들며 사라진다.
// 거대 괴물이 걸어간 자리에 남는 시럽 웅덩이(제자리걸음용 Puddle 뼈 대신, §64.5)도 같은 웅덩이를 작게 쓴다.
public static class GiantDeathFx
{
    public const int CrumbCount = 26;
    public const float SlimeSpreadSeconds = 0.4f;
    public const float SlimeHoldSeconds = 3f;
    public const float SlimeFadeSeconds = 0.6f;
    public const float SlimeRadius = 2.2f;
    public const float TileLift = 0.08f;

    // 구르는 부스러기. 밟은 발이 덮은 자리 둘레에서 바깥으로 튄다.
    public static void Crumbs(Vector3 feet, Color[] colors, Color fallback)
    {
        ParticleSystem ps = EscapeVisuals.CreateParticles("GiantCrumbs", null, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(3f, 4f),
            Speed = new Vector2(3f, 7f),
            Size = new Vector2(0.18f, 0.34f),
            Color = fallback,
            Gravity = 1.6f,
            MaxParticles = CrumbCount,
            EmissionOff = true,
            Shape = ParticleSystemShapeType.Circle,
            Radius = 0.5f,
            ShapeRotation = new Vector3(90f, 0f, 0f), // 원을 눕혀 수평으로 퍼지게
            FadeFrom = Color.white,
            FadeTo = Color.white,
            AlphaFrom = 1f,
            Once = true,
        });
        ps.transform.position = feet + Vector3.up * 0.6f;
        var main = ps.main;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-12f, 12f); // 구르는 느낌(라디안/초)
        rot.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
        // 부드러운 점 대신 조각(정육면체 메시, 텍스처 없는 파티클 재질 — 조각마다 받은 색)
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        renderer.sharedMaterial = ChunkMaterial();
        // 바닥: 밟힌 자리의 바닥 높이에 충돌 평면을 둔다(World 충돌은 넓은 바닥 메시를 놓쳐 지하까지 떨어졌다).
        // 바닥 그림(타일)이 충돌체보다 조금 위라 평면을 TileLift만큼 올린다.
        var floor = new GameObject("CrumbFloor").transform;
        floor.SetParent(ps.transform, false);
        floor.position = SlimePuddle.SnapDown(feet) + Vector3.up * TileLift;
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.SetPlane(0, floor);
        collision.bounce = 0.3f;
        collision.dampen = 0.35f;
        collision.lifetimeLoss = 0f;
        collision.radiusScale = 1f;
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 8f;
        limit.dampen = 0.08f; // 바닥을 구르다 서서히 멈춤
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));
        var color = ps.colorOverLifetime;
        color.enabled = false; // 조각마다 받은 색 그대로

        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
        for (int i = 0; i < CrumbCount; i++)
        {
            emit.startColor = colors != null && colors.Length > 0 ? colors[Random.Range(0, colors.Length)] : fallback;
            ps.Emit(emit, 1);
        }
        Object.Destroy(ps.gameObject, 5f);
    }

    private static Material chunk;

    // 조각 재질: 파티클 재질(빌드에 들어가는 셰이더)을 복제해 텍스처만 뺀다 — 조각 색(정점 색)이 그대로 보인다.
    private static Material ChunkMaterial()
    {
        if (chunk != null) return chunk;
        chunk = new Material(EscapeVisuals.SoftParticleMaterial()) { name = "GiantCrumbChunk", mainTexture = null };
        return chunk;
    }

    // 분홍 슬라임: 웅덩이가 퍼지고 머문 뒤 줄어들며 사라진다. 작은 방울이 튀어 떨어진다.
    public static void Slime(Vector3 feet, Color color)
    {
        SlimePuddle.Spawn(feet, color, SlimeRadius, SlimeSpreadSeconds, SlimeHoldSeconds, SlimeFadeSeconds, 0.35f);
        ParticleSystem drops = EscapeVisuals.CreateParticles("GiantSlimeDrops", null, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(0.6f, 0.9f),
            Speed = new Vector2(3f, 5.5f),
            Size = new Vector2(0.18f, 0.35f),
            Color = color,
            Gravity = 2.2f,
            MaxParticles = 10,
            EmissionOff = true,
            Shape = ParticleSystemShapeType.Cone,
            Angle = 35f,
            Radius = 0.4f,
            ShapeRotation = new Vector3(-90f, 0f, 0f), // 위로
            FadeFrom = color,
            FadeTo = color,
            AlphaFrom = 1f,
            Once = true,
        });
        drops.transform.position = feet + Vector3.up * 0.2f;
        drops.Emit(8);
        Object.Destroy(drops.gameObject, 2f);
    }

    // 걸어간 자리의 시럽 웅덩이(작게, 빨리 사라짐).
    public static void SyrupDrip(Vector3 feet, Color color) =>
        SlimePuddle.Spawn(feet, color, Random.Range(0.6f, 1.1f), 0.25f, 1.6f, 0.6f, 0.15f);
}

// 바닥 웅덩이 하나: 불규칙한 원판 메시가 퍼지고(spread), 가장자리가 출렁이며 머물다(hold), 줄어들며 투명해진다(fade).
public class SlimePuddle : MonoBehaviour
{
    private const int Segments = 28;
    private const float Lift = 0.1f; // 바닥 타일 그림이 충돌체보다 6 cm 위에 있어 그 위로 띄운다
    private Mesh mesh;
    private Material material;
    private Vector3[] vertices;
    private float[] noise;
    private float radius, spread, hold, fade, wobble, born;
    private Color color;

    public static SlimePuddle Spawn(Vector3 feet, Color color, float radius, float spread, float hold, float fade, float wobble)
    {
        var go = new GameObject("SlimePuddle");
        go.transform.position = SnapDown(feet) + Vector3.up * Lift;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var p = go.AddComponent<SlimePuddle>();
        p.Init(color, radius, spread, hold, fade, wobble);
        return p;
    }

    private void Init(Color c, float r, float s, float h, float f, float w)
    {
        color = c; radius = r; spread = s; hold = h; fade = f; wobble = w;
        born = Time.time;
        noise = new float[Segments];
        for (int i = 0; i < Segments; i++) noise[i] = Random.Range(0.7f, 1.15f);
        vertices = new Vector3[Segments + 1];
        var tris = new int[Segments * 3];
        for (int i = 0; i < Segments; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = (i + 1) % Segments + 1; }
        mesh = new Mesh { name = "SlimePuddle" };
        mesh.vertices = vertices;
        mesh.triangles = tris;
        var normals = new Vector3[Segments + 1];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
        mesh.normals = normals;
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        material = EscapeVisuals.NewMaterial(c, 0.25f, fade: true);
        material.SetFloat("_Glossiness", 0.92f);
        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Update();
    }

    private void Update()
    {
        float t = Time.time - born;
        float size, alpha = 1f;
        if (t < spread) size = Mathf.SmoothStep(0.15f, 1f, t / spread);
        else if (t < spread + hold) size = 1f;
        else
        {
            float k = Mathf.Clamp01((t - spread - hold) / fade);
            size = 1f - 0.6f * k;
            alpha = 1f - k;
            if (k >= 1f) { Destroy(gameObject); return; }
        }
        vertices[0] = Vector3.zero;
        for (int i = 0; i < Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            float r = radius * size * noise[i] * (1f + wobble * 0.12f * Mathf.Sin(t * 5f + i * 1.7f));
            vertices[i + 1] = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        Color c = color;
        c.a = color.a * alpha * 0.92f;
        material.color = c;
    }

    // 바로 아래 바닥(캐릭터·거대 괴물 몸 제외)에 붙인다.
    public static Vector3 SnapDown(Vector3 p)
    {
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (h.collider.GetComponentInParent<IGameCharacter>() == null && h.point.y > best) best = h.point.y;
        return float.IsNegativeInfinity(best) ? p : new Vector3(p.x, best, p.z);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
