using UnityEngine;

// §90 F4 바닥 아이템 반짝임(빛기둥 대신) — 모든 맵·모든 아이템, 쿠키·스파이 화면만. 작은 별 입자가 모델 둘레에서 깜박이고 은은한 빛.
public class GroundItemSparkle : MonoBehaviour
{
    private ParticleSystem stars;
    private Light glow;
    private static Material starMat;

    public static GroundItemSparkle Attach(Transform item)
    {
        var go = new GameObject("Sparkle");
        go.transform.SetParent(item, false);
        var s = go.AddComponent<GroundItemSparkle>();
        s.Build();
        return s;
    }

    public void SetHeight(float y) => transform.localPosition = Vector3.up * y;

    private void Build()
    {
        stars = gameObject.AddComponent<ParticleSystem>();
        stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = stars.main;
        m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); m.startSpeed = 0.05f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); m.maxParticles = 8;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.75f), new Color(1f, 0.85f, 0.4f));
        m.simulationSpace = ParticleSystemSimulationSpace.Local;
        var em = stars.emission; em.rateOverTime = 5f;
        var sh = stars.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.45f;
        var size = stars.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f)));
        var r = GetComponent<ParticleSystemRenderer>();
        if (starMat == null)
        {
            starMat = new Material(Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default"));
            starMat.mainTexture = StarTexture();
        }
        r.sharedMaterial = starMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.type = LightType.Point; glow.range = 1.6f; glow.intensity = 0.8f; glow.color = new Color(1f, 0.92f, 0.65f);
        glow.shadows = LightShadows.None;
        Refresh();
    }

    // 별 모양(가운데 밝고 네 갈래) 작은 텍스처.
    private static Texture2D StarTexture()
    {
        const int n = 32;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - n / 2f + 0.5f) / (n / 2f), dy = (y - n / 2f + 0.5f) / (n / 2f);
                float core = Mathf.Exp(-(dx * dx + dy * dy) * 18f);
                float rays = Mathf.Exp(-Mathf.Abs(dx) * 22f) * Mathf.Exp(-Mathf.Abs(dy) * 3f) + Mathf.Exp(-Mathf.Abs(dy) * 22f) * Mathf.Exp(-Mathf.Abs(dx) * 3f);
                float a = Mathf.Clamp01(core + rays * 0.7f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    // 술래에게는 보이지 않는다(쿠키·스파이 화면만).
    public static bool VisibleToLocal() => !RoomState.IsLocalMonster();

    private float nextCheck;
    private void Update()
    {
        if (Time.unscaledTime >= nextCheck) { nextCheck = Time.unscaledTime + 1f; Refresh(); }
        if (glow != null && glow.enabled) glow.intensity = 0.6f + 0.35f * Mathf.Sin(Time.time * 3.1f + transform.position.x);
    }

    private void Refresh()
    {
        bool on = VisibleToLocal();
        if (stars != null)
        {
            if (on && !stars.isPlaying) stars.Play();
            else if (!on && stars.isPlaying) stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (glow != null) glow.enabled = on;
    }
}
