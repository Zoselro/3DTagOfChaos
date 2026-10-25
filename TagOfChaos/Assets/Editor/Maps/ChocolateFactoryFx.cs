using UnityEditor;
using UnityEngine;
using static ParticleFxKit;

// 초콜릿 공장 건물의 파티클(Request1009Plan.md §41). Blender가 놓은 FX_<종류>_<이름> 빈 오브젝트 자리에 붙는다.
// - smoke : 굴뚝 연기 — 큰 덩이가 천천히 올라가며 커지고 옅어진다(계속 모락모락)
// - splash: 받는 통에 떨어지는 초콜릿이 튀는 방울
// - drip  : 배수관 입에서 떨어지는 방울
// - bubble: 끓는 솥 표면에 오르는 거품
// - crumbs: 분쇄 롤러 아래로 떨어지는 부스러기
// 모두 반복·미리 돌려 둠·화면 밖 멈춤(ParticleFxKit). 네트워크와 무관한 겉모습.
public static class ChocolateFactoryFx
{
    private static string Folder => ChocolateFactoryAssets.GeneratedFolder;
    private static readonly Color Chocolate = new Color(0.32f, 0.16f, 0.07f, 0.95f);
    private static readonly Color SmokeColor = new Color(0.86f, 0.84f, 0.82f, 1f);

    public static bool Attach(Transform marker, string kind, float radius)
    {
        switch (kind)
        {
            case "smoke": Smoke(marker, radius); return true;
            case "splash": Splash(marker, radius); return true;
            case "drip": Drip(marker, radius); return true;
            case "bubble": Bubble(marker, radius); return true;
            case "crumbs": Crumbs(marker, radius); return true;
            default: return false;
        }
    }

    private static void Smoke(Transform parent, float radius)
    {
        ParticleSystem ps = NewSystem(parent, "Smoke", Vector3.zero, SmokeMaterial(), 80);
        var main = ps.main;
        main.duration = 8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 8.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 1.3f, radius * 1.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = SmokeColor;
        main.gravityModifier = -0.015f;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // 굴뚝을 떠난 연기는 건물과 함께 움직이지 않는다
        var emission = ps.emission;
        emission.rateOverTime = 6f;
        DiscShape(ps, radius * 0.75f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);   // 바람에 한쪽으로 살짝 흐른다
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        var grow = ps.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.6f));
        var spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        FadeOut(ps, 0.42f, 0.55f, Color.white, new Color(0.7f, 0.68f, 0.68f));
    }

    private static void Splash(Transform parent, float radius)
    {
        ParticleSystem ps = NewSystem(parent, "Splash", Vector3.zero, DropMaterial(), 120);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
        main.startColor = Chocolate;
        main.gravityModifier = 1f;
        var emission = ps.emission;
        emission.rateOverTime = 45f;
        RingShape(ps, radius, 0f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        vel.y = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        FadeOut(ps, 1f, 0.7f);
    }

    private static void Drip(Transform parent, float radius)
    {
        ParticleSystem ps = NewSystem(parent, "Drip", Vector3.zero, DropMaterial(), 40);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.13f);
        main.startColor = Chocolate;
        main.gravityModifier = 1f;
        var emission = ps.emission;
        emission.rateOverTime = 9f;
        RingShape(ps, radius, 0f);
        FadeOut(ps, 1f, 0.85f);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 1.8f;
        r.velocityScale = 0.04f;
    }

    private static void Bubble(Transform parent, float radius)
    {
        ParticleSystem ps = NewSystem(parent, "Bubbles", Vector3.zero, DropMaterial(), 60);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new Color(0.45f, 0.24f, 0.11f, 0.9f);
        var emission = ps.emission;
        emission.rateOverTime = 14f;
        DiscShape(ps, radius);
        var grow = ps.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1.2f));
        FadeOut(ps, 0.9f, 0.8f);
    }

    private static void Crumbs(Transform parent, float radius)
    {
        ParticleSystem ps = NewSystem(parent, "Crumbs", Vector3.zero, DropMaterial(), 60);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = new Color(0.25f, 0.12f, 0.05f, 1f);
        main.gravityModifier = 0.8f;
        var emission = ps.emission;
        emission.rateOverTime = 30f;
        DiscShape(ps, radius);
        FadeOut(ps, 1f, 0.85f);
    }

    private static Material SmokeMaterial() => FadeMaterial(Folder + "/M_FX_ChocoSmoke.mat", RadialTexture(Folder + "/FX_SmokePuff.png", Puff));
    private static Material DropMaterial() => FadeMaterial(Folder + "/M_FX_ChocoDrop.mat", RadialTexture(Folder + "/FX_ChocoDrop.png", Dot));
}
