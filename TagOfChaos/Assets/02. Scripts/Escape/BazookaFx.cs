using UnityEngine;

// §90 B3 바주카포 연출(모든 화면): 로켓 꼬리 불꽃·연기 줄, 포구 불꽃, 폭발(불꽃 구 + 연기 + 파편 + 빛 + 소리 + 가까운 사람 화면 흔들림).
// 입자는 코드로 만든다(새 프리팹 없이 — 다른 투척 효과와 같은 방식).
public static class BazookaFx
{
    private static Material additive, smoke, debris;

    private static Material Additive()
    {
        if (additive == null) { additive = new Material(Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default")); additive.SetColor("_TintColor", new Color(1f, 0.6f, 0.2f, 0.6f)); }
        return additive;
    }

    private static Material Smoke()
    {
        if (smoke == null) smoke = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default"));
        return smoke;
    }

    private static Material Debris()
    {
        if (debris == null) { debris = new Material(Shader.Find("Standard")); debris.color = new Color(0.32f, 0.22f, 0.16f); }
        return debris;
    }

    // ---------------- 로켓 꼬리 ----------------

    // 날아가는 로켓 모습 뒤에 붙인다(ProjectileSystem.CreateView). 로켓은 +Z로 날아가므로 -Z로 뿜는다.
    public static void AttachTrail(Transform rocket, float length)
    {
        var fire = NewSystem("RocketFire", rocket, new Vector3(0f, 0f, -length * 0.5f), Additive());
        var m = fire.main;
        m.startLifetime = 0.18f; m.startSpeed = 6f; m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.35f, 0.05f));
        m.simulationSpace = ParticleSystemSimulationSpace.World; m.loop = true;
        var em = fire.emission; em.rateOverTime = 90f;
        var sh = fire.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.08f;
        fire.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var col = fire.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.2f, 0f));
        fire.Play();

        var trail = NewSystem("RocketSmoke", rocket, new Vector3(0f, 0f, -length * 0.55f), Smoke());
        var sm = trail.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f); sm.startSpeed = 0.6f; sm.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.73f, 0.7f, 0.6f), new Color(0.55f, 0.53f, 0.5f, 0.5f));
        sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.loop = true;
        var sem = trail.emission; sem.rateOverTime = 0f; sem.rateOverDistance = 4f;
        var ssz = trail.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
        var scol = trail.colorOverLifetime; scol.enabled = true; scol.color = Fade(new Color(0.8f, 0.78f, 0.75f), new Color(0.6f, 0.6f, 0.6f));
        trail.Play();

        var light = new GameObject("RocketLight").AddComponent<Light>();
        light.transform.SetParent(rocket, false);
        light.transform.localPosition = new Vector3(0f, 0f, -length * 0.6f);
        light.type = LightType.Point; light.color = new Color(1f, 0.6f, 0.25f); light.range = 7f; light.intensity = 2.5f;
    }

    public static void MuzzleFlash(Vector3 at, Vector3 forward)
    {
        GameObject go = new GameObject("BazookaMuzzle");
        go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(forward));
        var ps = NewSystem("Flash", go.transform, Vector3.zero, Additive());
        var m = ps.main; m.loop = false; m.duration = 0.1f; m.startLifetime = 0.15f; m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1f); m.startColor = new Color(1f, 0.75f, 0.3f);
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f;
        ps.Play();
        // 뒤로 뿜는 연기(무반동포)
        var back = NewSystem("Backblast", go.transform, Vector3.zero, Smoke());
        back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var bm = back.main; bm.loop = false; bm.duration = 0.2f; bm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); bm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        bm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); bm.startColor = new Color(0.8f, 0.78f, 0.75f, 0.55f);
        var bem = back.emission; bem.rateOverTime = 0f; bem.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
        var bsh = back.shape; bsh.shapeType = ParticleSystemShapeType.Cone; bsh.angle = 25f;
        back.Play();
        Object.Destroy(go, 2f);
    }

    // ---------------- 폭발 ----------------

    public static void Explode(Vector3 at)
    {
        GameObject go = new GameObject("BazookaExplosion");
        go.transform.position = at;

        var fire = NewSystem("Fireball", go.transform, Vector3.zero, Additive());
        var fm = fire.main; fm.loop = false; fm.duration = 0.25f; fm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        fm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f); fm.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        fm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.45f), new Color(1f, 0.4f, 0.08f));
        var fem = fire.emission; fem.rateOverTime = 0f; fem.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
        var fsh = fire.shape; fsh.shapeType = ParticleSystemShapeType.Sphere; fsh.radius = 0.8f;
        var fsz = fire.sizeOverLifetime; fsz.enabled = true; fsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
        var fcol = fire.colorOverLifetime; fcol.enabled = true; fcol.color = Fade(new Color(1f, 0.9f, 0.5f), new Color(0.8f, 0.15f, 0f));
        fire.Play();

        var smk = NewSystem("Smoke", go.transform, Vector3.up * 0.5f, Smoke());
        var sm = smk.main; sm.loop = false; sm.duration = 0.5f; sm.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f); sm.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.28f, 0.27f, 0.75f), new Color(0.55f, 0.52f, 0.5f, 0.6f));
        sm.gravityModifier = -0.15f;
        var sem = smk.emission; sem.rateOverTime = 0f; sem.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 40) });
        var ssh = smk.shape; ssh.shapeType = ParticleSystemShapeType.Hemisphere; ssh.radius = 1.4f;
        var ssz = smk.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2f));
        var scol = smk.colorOverLifetime; scol.enabled = true; scol.color = Fade(new Color(0.4f, 0.38f, 0.36f), new Color(0.6f, 0.6f, 0.6f));
        smk.Play();

        // 파편(작은 상자 메시, 중력 + 바닥 튕김)
        var deb = NewSystem("Debris", go.transform, Vector3.zero, Debris());
        var dm = deb.main; dm.loop = false; dm.duration = 0.1f; dm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(6f, 16f); dm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
        dm.gravityModifier = 2.2f; dm.startRotation3D = true; dm.startRotationX = dm.startRotationY = dm.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        var dem = deb.emission; dem.rateOverTime = 0f; dem.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
        var dsh = deb.shape; dsh.shapeType = ParticleSystemShapeType.Hemisphere; dsh.radius = 0.5f;
        var dcol = deb.collision; dcol.enabled = true; dcol.type = ParticleSystemCollisionType.World; dcol.bounce = 0.35f; dcol.dampen = 0.4f; dcol.quality = ParticleSystemCollisionQuality.Low;
        var dr = deb.GetComponent<ParticleSystemRenderer>(); dr.renderMode = ParticleSystemRenderMode.Mesh; dr.mesh = CubeMesh(); dr.sharedMaterial = Debris();
        deb.Play();

        var light = new GameObject("Flash").AddComponent<Light>();
        light.transform.SetParent(go.transform, false);
        light.type = LightType.Point; light.color = new Color(1f, 0.65f, 0.3f); light.range = 22f; light.intensity = 6f;
        light.gameObject.AddComponent<FadeLight>();

        GameAudio.PlayAt(SoundId.BazookaExplode, at);
        Camera cam = Camera.main;
        if (cam != null)
        {
            float k = 1f - Mathf.Clamp01(Vector3.Distance(cam.transform.position, at) / 30f);
            if (k > 0f) CameraShake.Add(0.45f * k, 0.6f);
        }
        Object.Destroy(go, 4f);
    }

    private sealed class FadeLight : MonoBehaviour
    {
        private Light l; private float t;
        private void Awake() => l = GetComponent<Light>();
        private void Update() { t += Time.deltaTime; if (l != null) l.intensity = Mathf.Max(0f, 6f * (1f - t / 0.35f)); }
    }

    private static Mesh cube;
    private static Mesh CubeMesh()
    {
        if (cube != null) return cube;
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube = g.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(g);
        return cube;
    }

    private static ParticleSystem NewSystem(string name, Transform parent, Vector3 local, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    private static ParticleSystem.MinMaxGradient Fade(Color a, Color b)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }
}
