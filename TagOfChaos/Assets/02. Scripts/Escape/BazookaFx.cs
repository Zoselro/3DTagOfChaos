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

    // §92-6 예전(텍스처 없는 사각 입자·정육면체 파편 — 네모나게 보였다)
    // // 날아가는 로켓 모습 뒤에 붙인다(ProjectileSystem.CreateView). 로켓은 +Z로 날아가므로 -Z로 뿜는다.
    // public static void AttachTrail(Transform rocket, float length)
    // {
    //     var fire = NewSystem("RocketFire", rocket, new Vector3(0f, 0f, -length * 0.5f), Additive());
    //     var m = fire.main;
    //     m.startLifetime = 0.18f; m.startSpeed = 6f; m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
    //     m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.35f, 0.05f));
    //     m.simulationSpace = ParticleSystemSimulationSpace.World; m.loop = true;
    //     var em = fire.emission; em.rateOverTime = 90f;
    //     var sh = fire.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.08f;
    //     fire.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    //     var col = fire.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.2f, 0f));
    //     fire.Play();

    //     var trail = NewSystem("RocketSmoke", rocket, new Vector3(0f, 0f, -length * 0.55f), Smoke());
    //     var sm = trail.main;
    //     sm.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f); sm.startSpeed = 0.6f; sm.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
    //     sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.73f, 0.7f, 0.6f), new Color(0.55f, 0.53f, 0.5f, 0.5f));
    //     sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.loop = true;
    //     var sem = trail.emission; sem.rateOverTime = 0f; sem.rateOverDistance = 4f;
    //     var ssz = trail.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
    //     var scol = trail.colorOverLifetime; scol.enabled = true; scol.color = Fade(new Color(0.8f, 0.78f, 0.75f), new Color(0.6f, 0.6f, 0.6f));
    //     trail.Play();

    //     var light = new GameObject("RocketLight").AddComponent<Light>();
    //     light.transform.SetParent(rocket, false);
    //     light.transform.localPosition = new Vector3(0f, 0f, -length * 0.6f);
    //     light.type = LightType.Point; light.color = new Color(1f, 0.6f, 0.25f); light.range = 7f; light.intensity = 2.5f;
    // }

    // §92-6 예전(텍스처 없는 사각 입자·정육면체 파편 — 네모나게 보였다)
    // public static void MuzzleFlash(Vector3 at, Vector3 forward)
    // {
    //     GameObject go = new GameObject("BazookaMuzzle");
    //     go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(forward));
    //     var ps = NewSystem("Flash", go.transform, Vector3.zero, Additive());
    //     var m = ps.main; m.loop = false; m.duration = 0.1f; m.startLifetime = 0.15f; m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
    //     m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1f); m.startColor = new Color(1f, 0.75f, 0.3f);
    //     var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
    //     var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f;
    //     ps.Play();
    //     // 뒤로 뿜는 연기(무반동포)
    //     var back = NewSystem("Backblast", go.transform, Vector3.zero, Smoke());
    //     back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    //     var bm = back.main; bm.loop = false; bm.duration = 0.2f; bm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); bm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
    //     bm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); bm.startColor = new Color(0.8f, 0.78f, 0.75f, 0.55f);
    //     var bem = back.emission; bem.rateOverTime = 0f; bem.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
    //     var bsh = back.shape; bsh.shapeType = ParticleSystemShapeType.Cone; bsh.angle = 25f;
    //     back.Play();
    //     Object.Destroy(go, 2f);
    // }

    // ---------------- 폭발 ----------------

    // §92-6 예전(텍스처 없는 사각 입자·정육면체 파편 — 네모나게 보였다)
    // public static void Explode(Vector3 at)
    // {
    //     GameObject go = new GameObject("BazookaExplosion");
    //     go.transform.position = at;

    //     var fire = NewSystem("Fireball", go.transform, Vector3.zero, Additive());
    //     var fm = fire.main; fm.loop = false; fm.duration = 0.25f; fm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
    //     fm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f); fm.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
    //     fm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.45f), new Color(1f, 0.4f, 0.08f));
    //     var fem = fire.emission; fem.rateOverTime = 0f; fem.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
    //     var fsh = fire.shape; fsh.shapeType = ParticleSystemShapeType.Sphere; fsh.radius = 0.8f;
    //     var fsz = fire.sizeOverLifetime; fsz.enabled = true; fsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
    //     var fcol = fire.colorOverLifetime; fcol.enabled = true; fcol.color = Fade(new Color(1f, 0.9f, 0.5f), new Color(0.8f, 0.15f, 0f));
    //     fire.Play();

    //     var smk = NewSystem("Smoke", go.transform, Vector3.up * 0.5f, Smoke());
    //     var sm = smk.main; sm.loop = false; sm.duration = 0.5f; sm.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
    //     sm.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f); sm.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
    //     sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.28f, 0.27f, 0.75f), new Color(0.55f, 0.52f, 0.5f, 0.6f));
    //     sm.gravityModifier = -0.15f;
    //     var sem = smk.emission; sem.rateOverTime = 0f; sem.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 40) });
    //     var ssh = smk.shape; ssh.shapeType = ParticleSystemShapeType.Hemisphere; ssh.radius = 1.4f;
    //     var ssz = smk.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2f));
    //     var scol = smk.colorOverLifetime; scol.enabled = true; scol.color = Fade(new Color(0.4f, 0.38f, 0.36f), new Color(0.6f, 0.6f, 0.6f));
    //     smk.Play();

    //     // 파편(작은 상자 메시, 중력 + 바닥 튕김)
    //     var deb = NewSystem("Debris", go.transform, Vector3.zero, Debris());
    //     var dm = deb.main; dm.loop = false; dm.duration = 0.1f; dm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
    //     dm.startSpeed = new ParticleSystem.MinMaxCurve(6f, 16f); dm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
    //     dm.gravityModifier = 2.2f; dm.startRotation3D = true; dm.startRotationX = dm.startRotationY = dm.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
    //     var dem = deb.emission; dem.rateOverTime = 0f; dem.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
    //     var dsh = deb.shape; dsh.shapeType = ParticleSystemShapeType.Hemisphere; dsh.radius = 0.5f;
    //     var dcol = deb.collision; dcol.enabled = true; dcol.type = ParticleSystemCollisionType.World; dcol.bounce = 0.35f; dcol.dampen = 0.4f; dcol.quality = ParticleSystemCollisionQuality.Low;
    //     var dr = deb.GetComponent<ParticleSystemRenderer>(); dr.renderMode = ParticleSystemRenderMode.Mesh; dr.mesh = CubeMesh(); dr.sharedMaterial = Debris();
    //     deb.Play();

    //     var light = new GameObject("Flash").AddComponent<Light>();
    //     light.transform.SetParent(go.transform, false);
    //     light.type = LightType.Point; light.color = new Color(1f, 0.65f, 0.3f); light.range = 22f; light.intensity = 6f;
    //     light.gameObject.AddComponent<FadeLight>();

    //     GameAudio.PlayAt(SoundId.BazookaExplode, at);
    //     Camera cam = Camera.main;
    //     if (cam != null)
    //     {
    //         float k = 1f - Mathf.Clamp01(Vector3.Distance(cam.transform.position, at) / 30f);
    //         if (k > 0f) CameraShake.Add(0.45f * k, 0.6f);
    //     }
    //     Object.Destroy(go, 4f);
    // }

    private sealed class FadeLight : MonoBehaviour
    {
        private Light l; private float t;
        private void Awake() => l = GetComponent<Light>();
        private void Update() { t += Time.deltaTime; if (l != null) l.intensity = Mathf.Max(0f, 6f * (1f - t / 0.35f)); }
    }

    // §92-6 예전(텍스처 없는 사각 입자·정육면체 파편 — 네모나게 보였다)
    // private static Mesh cube;
    // private static Mesh CubeMesh()
    // {
    //     if (cube != null) return cube;
    //     GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
    //     cube = g.GetComponent<MeshFilter>().sharedMesh;
    //     Object.Destroy(g);
    //     return cube;
    // }

    // ---------------- §92-6 부드러운 연기·불꽃(텍스처 묶음 2×2 + 흔들림 + 끌림), 울퉁불퉁한 파편 ----------------
    private const string FxPath = "FX/Bazooka/";
    private static Material softSmoke, softFire, softGlow;

    private static Material SoftSmoke()
    {
        if (softSmoke == null) softSmoke = Resources.Load<Material>(FxPath + "M_BazookaSmoke") ?? Smoke();
        return softSmoke;
    }

    private static Material SoftFire()
    {
        if (softFire == null) softFire = Resources.Load<Material>(FxPath + "M_BazookaFire") ?? Additive();
        return softFire;
    }

    private static Material SoftGlow()
    {
        if (softGlow == null) softGlow = Resources.Load<Material>(FxPath + "M_BazookaGlow") ?? Additive();
        return softGlow;
    }

    // 2×2 묶음에서 입자마다 한 장을 골라(평생 같은 장) 돌려 놓는다 — 같은 모양이 되풀이되지 않게.
    private static void Puffy(ParticleSystem ps, float spin)
    {
        var ts = ps.textureSheetAnimation;
        ts.enabled = true; ts.numTilesX = 2; ts.numTilesY = 2;
        ts.animation = ParticleSystemAnimationType.WholeSheet;
        ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
        var m = ps.main;
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        if (spin > 0f)
        {
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-spin * Mathf.Deg2Rad, spin * Mathf.Deg2Rad);
        }
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sortMode = ParticleSystemSortMode.Distance;
        r.maxParticleSize = 3f;
    }

    private static void Drift(ParticleSystem ps, float strength, float frequency)
    {
        var nz = ps.noise;
        nz.enabled = true; nz.strength = strength; nz.frequency = frequency; nz.scrollSpeed = 0.35f;
        nz.quality = ParticleSystemNoiseQuality.Medium; nz.damping = true;
    }

    private static void Drag(ParticleSystem ps, float drag)
    {
        var lv = ps.limitVelocityOverLifetime;
        lv.enabled = true; lv.drag = drag; lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = true;
    }

    private static AnimationCurve EaseOut(float from, float to)
    {
        var c = new AnimationCurve(new Keyframe(0f, from, 0f, (to - from) * 2.2f), new Keyframe(1f, to, 0f, 0f));
        return c;
    }

    // 연기: 처음 진하게 → 천천히 옅어짐(뒤쪽이 길다)
    private static ParticleSystem.MinMaxGradient SmokeFade(Color a, Color b, float peak)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.08f), new GradientAlphaKey(peak * 0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    // 날아가는 로켓 모습 뒤에 붙인다(ProjectileSystem.CreateView). 로켓은 +Z로 날아가므로 -Z로 뿜는다.
    public static void AttachTrail(Transform rocket, float length)
    {
        // 꼬리 불꽃: 부드러운 불 덩어리 + 하얀 심
        var fire = NewSystem("RocketFire", rocket, new Vector3(0f, 0f, -length * 0.5f), SoftFire());
        var m = fire.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f); m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f); m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.5f, 0.15f));
        m.simulationSpace = ParticleSystemSimulationSpace.World; m.loop = true;
        var em = fire.emission; em.rateOverTime = 110f;
        var sh = fire.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.06f;
        fire.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var col = fire.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.3f, 0.05f));
        var fsz = fire.sizeOverLifetime; fsz.enabled = true; fsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
        Puffy(fire, 0f);
        fire.Play();

        var core = NewSystem("RocketGlow", rocket, new Vector3(0f, 0f, -length * 0.52f), SoftGlow());
        var cm = core.main; cm.startLifetime = 0.06f; cm.startSpeed = 0f; cm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.1f);
        cm.startColor = new Color(1f, 0.75f, 0.4f, 0.9f); cm.simulationSpace = ParticleSystemSimulationSpace.Local; cm.loop = true;
        var cem = core.emission; cem.rateOverTime = 40f;
        var csh = core.shape; csh.enabled = false;
        core.Play();

        // 연기 줄: 촘촘한 부드러운 덩어리가 퍼지며 흔들리고 천천히 옅어진다
        var trail = NewSystem("RocketSmoke", rocket, new Vector3(0f, 0f, -length * 0.6f), SoftSmoke());
        var sm = trail.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.2f); sm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f); sm.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.92f, 0.91f, 0.9f), new Color(0.78f, 0.77f, 0.76f));
        sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.loop = true; sm.gravityModifier = -0.03f; sm.maxParticles = 600;
        var sem = trail.emission; sem.rateOverTime = 0f; sem.rateOverDistance = 9f;
        var ssh = trail.shape; ssh.shapeType = ParticleSystemShapeType.Sphere; ssh.radius = 0.12f;
        var ssz = trail.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, EaseOut(0.6f, 3.4f));
        var scol = trail.colorOverLifetime; scol.enabled = true; scol.color = SmokeFade(Color.white, new Color(0.85f, 0.85f, 0.86f), 0.55f);
        Puffy(trail, 25f);
        Drift(trail, 0.45f, 0.45f);
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
        var ps = NewSystem("Flash", go.transform, Vector3.zero, SoftFire());
        var m = ps.main; m.loop = false; m.duration = 0.1f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f); m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.55f, 0.2f));
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.05f;
        var fcol = ps.colorOverLifetime; fcol.enabled = true; fcol.color = Fade(Color.white, new Color(1f, 0.4f, 0.1f));
        Puffy(ps, 0f);
        ps.Play();
        var glow = NewSystem("Glow", go.transform, Vector3.zero, SoftGlow());
        var gm = glow.main; gm.loop = false; gm.duration = 0.05f; gm.startLifetime = 0.09f; gm.startSpeed = 0f; gm.startSize = 2.2f; gm.startColor = new Color(1f, 0.8f, 0.5f, 1f);
        var gem = glow.emission; gem.rateOverTime = 0f; gem.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        var gsh = glow.shape; gsh.enabled = false;
        glow.Play();
        // 뒤로 뿜는 연기(무반동포): 빠르게 뿜고 공기에 끌려 멈추며 부풀어 오른다
        var back = NewSystem("Backblast", go.transform, Vector3.zero, SoftSmoke());
        back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var bm = back.main; bm.loop = false; bm.duration = 0.15f; bm.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f); bm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f);
        bm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); bm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.93f, 0.92f, 0.9f), new Color(0.8f, 0.79f, 0.78f)); bm.gravityModifier = -0.04f;
        var bem = back.emission; bem.rateOverTime = 0f; bem.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
        var bsh = back.shape; bsh.shapeType = ParticleSystemShapeType.Cone; bsh.angle = 22f; bsh.radius = 0.1f;
        var bsz = back.sizeOverLifetime; bsz.enabled = true; bsz.size = new ParticleSystem.MinMaxCurve(1f, EaseOut(0.6f, 3f));
        var bcol = back.colorOverLifetime; bcol.enabled = true; bcol.color = SmokeFade(Color.white, new Color(0.86f, 0.86f, 0.87f), 0.5f);
        Puffy(back, 30f);
        Drag(back, 4f);
        Drift(back, 0.35f, 0.6f);
        back.Play();
        Object.Destroy(go, 2.5f);
    }

    public static void Explode(Vector3 at)
    {
        GameObject go = new GameObject("BazookaExplosion");
        go.transform.position = at;

        // 번쩍임
        var flash = NewSystem("Glow", go.transform, Vector3.up * 0.6f, SoftGlow());
        var gm = flash.main; gm.loop = false; gm.duration = 0.05f; gm.startLifetime = 0.18f; gm.startSpeed = 0f; gm.startSize = 9f; gm.startColor = new Color(1f, 0.8f, 0.5f, 1f);
        var gem = flash.emission; gem.rateOverTime = 0f; gem.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        var gsh = flash.shape; gsh.enabled = false;
        var gcol = flash.colorOverLifetime; gcol.enabled = true; gcol.color = Fade(Color.white, new Color(1f, 0.5f, 0.2f));
        flash.Play();

        // 불덩어리: 부드러운 불꽃이 바깥으로 퍼지며 끌려 멈추고 식는다
        var fire = NewSystem("Fireball", go.transform, Vector3.up * 0.4f, SoftFire());
        var fm = fire.main; fm.loop = false; fm.duration = 0.2f; fm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        fm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f); fm.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
        fm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.6f), new Color(1f, 0.55f, 0.15f)); fm.gravityModifier = -0.2f;
        var fem = fire.emission; fem.rateOverTime = 0f; fem.SetBursts(new[] { new ParticleSystem.Burst(0f, 36), new ParticleSystem.Burst(0.06f, 14) });
        var fsh = fire.shape; fsh.shapeType = ParticleSystemShapeType.Sphere; fsh.radius = 0.6f;
        var fsz = fire.sizeOverLifetime; fsz.enabled = true; fsz.size = new ParticleSystem.MinMaxCurve(1f, EaseOut(0.5f, 1.5f));
        var fcol = fire.colorOverLifetime; fcol.enabled = true; fcol.color = Fade(new Color(1f, 0.95f, 0.7f), new Color(0.7f, 0.15f, 0.02f));
        Puffy(fire, 60f);
        Drag(fire, 5f);
        fire.Play();

        // 연기: 아래는 빠르게 퍼지는 둥근 연기, 위로 천천히 솟는 기둥 — 끌림·흔들림으로 뭉게뭉게
        var smk = NewSystem("Smoke", go.transform, Vector3.up * 0.6f, SoftSmoke());
        var sm = smk.main; sm.loop = false; sm.duration = 0.35f; sm.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.8f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f); sm.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.86f, 0.84f, 0.82f), new Color(0.97f, 0.96f, 0.95f));
        sm.gravityModifier = -0.08f; sm.maxParticles = 200;
        var sem = smk.emission; sem.rateOverTime = 0f; sem.SetBursts(new[] { new ParticleSystem.Burst(0.03f, 34), new ParticleSystem.Burst(0.15f, 16) });
        var ssh = smk.shape; ssh.shapeType = ParticleSystemShapeType.Hemisphere; ssh.radius = 1.1f;
        var ssz = smk.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, EaseOut(0.55f, 2.4f));
        var scol = smk.colorOverLifetime; scol.enabled = true; scol.color = SmokeFade(new Color(0.78f, 0.72f, 0.68f), Color.white, 0.8f);   // 처음엔 불에 그을린 회갈색 → 하얀 뭉게구름
        Puffy(smk, 20f);
        Drag(smk, 3.2f);
        Drift(smk, 0.6f, 0.35f);
        smk.Play();

        // 불티: 짧게 늘어진 빛 줄
        var sparks = NewSystem("Sparks", go.transform, Vector3.up * 0.3f, SoftGlow());
        var km = sparks.main; km.loop = false; km.duration = 0.1f; km.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        km.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f); km.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        km.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.55f, 0.2f)); km.gravityModifier = 1.2f;
        var kem = sparks.emission; kem.rateOverTime = 0f; kem.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
        var ksh = sparks.shape; ksh.shapeType = ParticleSystemShapeType.Hemisphere; ksh.radius = 0.3f;
        var kcol = sparks.colorOverLifetime; kcol.enabled = true; kcol.color = Fade(Color.white, new Color(1f, 0.3f, 0f));
        var kr = sparks.GetComponent<ParticleSystemRenderer>(); kr.renderMode = ParticleSystemRenderMode.Stretch; kr.velocityScale = 0.04f; kr.lengthScale = 1.5f;
        sparks.Play();

        // 파편: 울퉁불퉁한 작은 덩어리(중력 + 바닥 튕김)
        var deb = NewSystem("Debris", go.transform, Vector3.zero, Debris());
        var dm = deb.main; dm.loop = false; dm.duration = 0.1f; dm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(6f, 15f); dm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.32f);
        dm.gravityModifier = 2.2f; dm.startRotation3D = true; dm.startRotationX = dm.startRotationY = dm.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        var dem = deb.emission; dem.rateOverTime = 0f; dem.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
        var dsh = deb.shape; dsh.shapeType = ParticleSystemShapeType.Hemisphere; dsh.radius = 0.5f;
        var drot = deb.rotationOverLifetime; drot.enabled = true; drot.separateAxes = true;
        drot.x = drot.y = drot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var dcol = deb.collision; dcol.enabled = true; dcol.type = ParticleSystemCollisionType.World; dcol.bounce = 0.3f; dcol.dampen = 0.45f; dcol.quality = ParticleSystemCollisionQuality.Low;
        var dsz = deb.sizeOverLifetime; dsz.enabled = true; dsz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
        var dr = deb.GetComponent<ParticleSystemRenderer>(); dr.renderMode = ParticleSystemRenderMode.Mesh; dr.SetMeshes(ChunkMeshes()); dr.sharedMaterial = Debris();
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
        Object.Destroy(go, 5f);
    }

    // 울퉁불퉁한 덩어리 메시 3종(정이십면체 꼭짓점을 흔들어 만든다 — 한 번만).
    private static Mesh[] chunks;
    private static Mesh[] ChunkMeshes()
    {
        if (chunks != null) return chunks;
        chunks = new Mesh[3];
        var rng = new System.Random(926);
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] baseV =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        int[] tris =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        for (int k = 0; k < chunks.Length; k++)
        {
            // 면마다 꼭짓점을 따로 둬서 각진 면이 보이게(납작한 그림자)
            var v = new Vector3[tris.Length];
            var jitter = new Vector3[baseV.Length];
            for (int i = 0; i < baseV.Length; i++)
            {
                float s = 0.75f + (float)rng.NextDouble() * 0.5f;
                jitter[i] = Vector3.Scale(baseV[i].normalized * 0.5f * s, new Vector3(1f, 0.6f + 0.4f * k / 2f, 0.85f));
            }
            var idx = new int[tris.Length];
            for (int i = 0; i < tris.Length; i++) { v[i] = jitter[tris[i]]; idx[i] = i; }
            var mesh = new Mesh { name = "BazookaChunk" + k, vertices = v, triangles = idx };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            chunks[k] = mesh;
        }
        return chunks;
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
