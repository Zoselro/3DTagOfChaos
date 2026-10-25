using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static ParticleFxKit;

// 놀이공원 분수(MO_Fountain)의 움직이는 물(Request1009Plan.md §39 F2·F3). 모델의 고정된 물줄기 막대는 Blender에서 뺐고(F1), 대신
// 프리팹 아래 "Water"에 파티클을 둔다. 여러 번 실행해도 같은 결과(이전 Water를 지우고 다시 만든다).
// - Jets   : 위 그릇 밑 6곳(60°마다)에서 바깥으로 나와 포물선으로 아래 물에 떨어지는 물줄기(늘린 빌보드)
// - Spill  : 위 그릇 가장자리에서 가늘게 넘쳐 흐르는 물
// - Splash : 물줄기가 떨어지는 6곳의 튀는 물방울 + 옅은 물안개
// - Ripples: 같은 6곳에서 퍼지는 물결 고리(수평 빌보드)
// 물 표면 흐름은 FountainWater, 물소리는 AmbientEmitter(SoundId.FountainWater). 치수는 모델(Unity 로컬, m)에서 쟀다.
public static class FountainWaterBuilder
{
    public const string PrefabName = "MO_Fountain";
    public const string WaterName = "Water";
    public const string SoundName = "AMB_Fountain";
    private const string MenuPath = "Tools/TagOfChaos/Maps/Add Fountain Water (MO_Fountain prefab)";

    // 모델 치수(MO_Fountain.fbx): 물줄기 시작(위 그릇 밑) 반지름·높이, 아래 물 표면 높이·떨어지는 반지름, 위 그릇 물 반지름·높이
    public const int JetCount = 6;
    public const float JetRadius = 0.33f, JetHeight = 1.97f;
    public const float PoolHeight = 0.54f, LandRadius = 1.18f;
    public const float BowlRadius = 0.9f, BowlHeight = 2.1f;

    private static string Folder => CarnivalMapAssets.GeneratedFolder;
    private static string DropTexturePath => Folder + "/FX_WaterDrop.png";
    private static string RingTexturePath => Folder + "/FX_WaterRing.png";
    private static string DropMaterialPath => Folder + "/M_FX_FountainDrop.mat";
    private static string RingMaterialPath => Folder + "/M_FX_FountainRing.mat";

    [MenuItem(MenuPath)]
    public static void AddToPrefabMenu() => Debug.Log("[FountainWater] " + AddToPrefab());

    public static string AddToPrefab()
    {
        string path = CarnivalMapAssets.PrefabPath(PrefabName);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return "prefab missing: " + path;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            string result = Attach(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return result;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 분수 루트(모델은 자식 "Model")에 물을 붙인다. CarnivalMapAssets.BuildProp도 부른다.
    public static string Attach(GameObject root)
    {
        Transform old = root.transform.Find(WaterName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform oldSound = root.transform.Find(SoundName);
        if (oldSound != null) Object.DestroyImmediate(oldSound.gameObject);

        Material drop = DropMaterial(), ring = RingMaterial();
        var water = new GameObject(WaterName).transform;
        water.SetParent(root.transform, false);
        Jets(water, drop);
        Spill(water, drop);
        Splash(water, drop);
        Ripples(water, ring);

        Renderer body = root.transform.Find("Model")?.GetComponent<Renderer>();
        int index = body != null ? System.Array.FindIndex(body.sharedMaterials, m => m != null && m.name.Contains("Water")) : -1;
        var flow = root.GetComponent<FountainWater>();
        if (flow == null) flow = root.AddComponent<FountainWater>();
        flow.EditorSetup(body, index);

        var sound = new GameObject(SoundName);
        sound.transform.SetParent(root.transform, false);
        sound.transform.localPosition = new Vector3(0f, 1f, 0f);
        var emitter = sound.AddComponent<AmbientEmitter>();
        var so = new SerializedObject(emitter);
        so.FindProperty("sound").intValue = (int)SoundId.FountainWater;
        so.ApplyModifiedPropertiesWithoutUndo();
        return $"water: {JetCount} jets, spill, splash, ripples, surface index {index}, sound";
    }

    // ---------------- parts ----------------

    private static void Jets(Transform parent, Material mat)
    {
        // 수평으로 나와 1.43 m 떨어지는 데 0.54초 → 그동안 0.85 m 바깥으로(1.57 m/s): 아래 물(반지름 1.18) 위에서 사라진다
        float fall = JetHeight - PoolHeight, time = Mathf.Sqrt(2f * fall / 9.81f), speed = (LandRadius - JetRadius) / time;
        ParticleSystem ps = NewSystem(parent, "Jets", new Vector3(0f, JetHeight, 0f), mat, 420);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(time * 0.97f, time * 1.02f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.96f, speed * 1.04f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.13f);
        main.startColor = new Color(0.8f, 0.94f, 1f, 0.65f);
        main.gravityModifier = 1f;
        var emission = ps.emission;
        emission.rateOverTime = 70f * JetCount;
        RingShape(ps, JetRadius, 1f / JetCount);
        FadeOut(ps, 0.55f, 0.85f);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 1.6f;
        r.velocityScale = 0.06f;
    }

    private static void Spill(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, "Spill", new Vector3(0f, BowlHeight, 0f), mat, 120);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.56f); // 그릇 가장자리 → 아래 물(1.56 m)
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
        main.startColor = new Color(0.8f, 0.94f, 1f, 0.35f);
        main.gravityModifier = 1f;
        var emission = ps.emission;
        emission.rateOverTime = 90f;
        RingShape(ps, BowlRadius, 0f);
        FadeOut(ps, 0.6f, 0.8f);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 2.2f;
        r.velocityScale = 0.05f;
    }

    private static void Splash(Transform parent, Material mat)
    {
        ParticleSystem drops = NewSystem(parent, "Splash", new Vector3(0f, PoolHeight + 0.02f, 0f), mat, 160);
        var main = drops.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f); // 튀어 올랐다 물 표면 근처에서 사라지게
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = new Color(0.85f, 0.96f, 1f, 0.7f);
        main.gravityModifier = 1f;
        var emission = drops.emission;
        emission.rateOverTime = 22f * JetCount;
        RingShape(drops, LandRadius, 1f / JetCount);
        var vel = drops.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        FadeOut(drops, 0.7f, 0.6f);

        ParticleSystem mist = NewSystem(parent, "Mist", new Vector3(0f, PoolHeight + 0.1f, 0f), mat, 40);
        main = mist.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startColor = new Color(0.85f, 0.95f, 1f, 0.12f);
        main.gravityModifier = -0.02f;
        emission = mist.emission;
        emission.rateOverTime = 3f * JetCount;
        RingShape(mist, LandRadius, 1f / JetCount);
        var grow = mist.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
        FadeOut(mist, 0.4f, 1f);
    }

    private static void Ripples(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, "Ripples", new Vector3(0f, PoolHeight + 0.015f, 0f), mat, 40);
        var main = ps.main;
        main.startLifetime = 0.9f;
        main.startSpeed = 0f;
        main.startSize = 0.9f;
        main.startColor = new Color(0.85f, 0.96f, 1f, 0.45f);
        var emission = ps.emission;
        emission.rateOverTime = 2.2f * JetCount;
        RingShape(ps, LandRadius, 1f / JetCount);
        var grow = ps.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));
        FadeOut(ps, 0.15f, 1f);
        ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
    }

    // ---------------- assets ----------------

    private static Material DropMaterial() => FadeMaterial(DropMaterialPath, RadialTexture(DropTexturePath, ParticleFxKit.Dot));
    private static Material RingMaterial() => FadeMaterial(RingMaterialPath, RadialTexture(RingTexturePath, ParticleFxKit.Ring));
}
