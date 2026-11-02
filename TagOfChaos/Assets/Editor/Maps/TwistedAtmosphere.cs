using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

// 뒤틀린 과자 동화 분위기(TwistedCandyPlan.md §2.1·§2.2·V1): 안개·주변광·방향광·카메라 배경색, PostFX 색 보정, 점광원 색 빼기, 고장 난 전구(FlickerLight).
// 대기실은 PostFX를 새로 붙이고, 로비는 배경 그림을 해 질 녘 판으로 바꾼다. 여러 번 실행해도 같은 결과다 —
// 점광원 색·강도는 FlickerLight가 아직 없는 씬에서만 바꾼다(처음 한 번).
// 옛 Game_ 맵 5개의 분위기 표와 적용은 그 맵과 함께 없앴다(Request1009Plan.md §47 CL5). 지금 맵은 각 맵 빌더가 조명을 정한다.
public static class TwistedAtmosphere
{
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";
    public const string GameLobbyScene = "Assets/Scenes/GameLobbyScene.unity";
    public const string LobbyScene = "Assets/Scenes/LobbyScene.unity";
    public const string GameLobbyProfile = "Assets/Maps/GameLobby/GameLobby_PostFX.asset";
    public const string LobbyBackground = "Assets/08. Resources/LobbySceneBackGround_Twisted.png";

    public sealed class Look
    {
        public Color Fog;
        public float FogDensity;
        public float AmbientScale = 1f;
        public bool OverrideSun;
        public Color SunColor;
        public float SunIntensity;
        public float Saturation, Temperature, Tint, Contrast, PostExposure, Vignette;
        public float PointDesaturate;        // 점광원 색을 회색 쪽으로(0~1)
        public float PointScale = 1f;        // 점광원 강도 배율
        public string[] FlickerPrefixes = new string[0];
        public int FlickerEvery = 1;         // 이름이 맞는 조명 중 n개마다 하나
        public string[] BoostPrefixes = new string[0]; // 더 밝게(대기실 가마솥 빛)
        public float BoostScale = 1f;
    }

    // §2.1 목업 값을 게임용으로 다듬은 값 — 안개는 목업의 약 0.8배, 밤 맵은 노출을 올려(+0.6~1.0) 화면 평균 밝기를
    // 원래의 60~70%로 맞췄다(목업 그대로면 30~40%라 길·괴물이 안 보인다, §7 C5).
    public static readonly Look GameLobby = new Look
    {
        Fog = new Color(0.08f, 0.07f, 0.12f), FogDensity = 0.012f,
        Saturation = -25f, Temperature = -6f, Tint = 0f, Contrast = 15f, PostExposure = 0.5f, Vignette = 0f,
        PointDesaturate = 0.25f, PointScale = 1f, FlickerPrefixes = new[] { "Light" }, FlickerEvery = 3,
        BoostPrefixes = new[] { "FireLight", "LiquidGlowLight" }, BoostScale = 1.6f,
    };

    // §5 마녀 집 조명: V1에서 ×0.75로 줄인 등불을 한 번 되돌리고(TW_WitchHouse가 없고 V1이 이미 들어간 씬일 때만), 조명·창문 사본은 매번 같은 값으로.
    public const string WitchHouseRootName = "TW_WitchHouse";
    public const string WitchHousePath = "WitchCookieEnvironment/Witch_Cookie_House";
    public const string LobbyMaterialFolder = "Assets/Maps/GameLobby/Materials";
    private const float V1LobbyPointScale = 0.75f;
    public const float WindowEmissionScale = 1.8f;
    private static readonly string[] WindowMaterials = { "M_Window_Glass_Purple", "M_Window_Glass_Orange" };

    private struct HouseLight
    {
        public string Name; public LightType Type; public Vector3 Position, Target; public Color Color; public float Intensity, Range, Angle;
        public HouseLight(string name, LightType type, Vector3 pos, Vector3 target, Color color, float intensity, float range, float angle)
        { Name = name; Type = type; Position = pos; Target = target; Color = color; Intensity = intensity; Range = range; Angle = angle; }
    }

    // 집(중심 (0, 10, 0), 19×21×19, 정문 +Z) 앞·양옆 바닥에서 올려 비추는 보라·주황 스포트 3개 + 지붕을 비추는 달빛 스포트.
    private static readonly HouseLight[] HouseLights =
    {
        new HouseLight("TW_WitchHouse_Front", LightType.Spot, new Vector3(0f, 0.6f, 18f), new Vector3(0f, 9f, 0f), new Color(1f, 0.62f, 0.32f), 3.2f, 25f, 75f),
        new HouseLight("TW_WitchHouse_Right", LightType.Spot, new Vector3(18f, 0.6f, 2f), new Vector3(0f, 9f, 0f), new Color(0.72f, 0.45f, 1f), 3.2f, 25f, 75f),
        new HouseLight("TW_WitchHouse_Left", LightType.Spot, new Vector3(-18f, 0.6f, 2f), new Vector3(0f, 9f, 0f), new Color(0.72f, 0.45f, 1f), 3.2f, 25f, 75f),
        new HouseLight("TW_WitchHouse_Moon", LightType.Spot, new Vector3(18f, 34f, 22f), new Vector3(0f, 14f, 0f), new Color(0.62f, 0.7f, 1f), 2.2f, 45f, 50f),
    };

    public static string ApplyWitchHouseLighting(bool restoreV1Lanterns)
    {
        GameObject root = GameObject.Find(WitchHouseRootName);
        int restored = 0;
        if (root == null)
        {
            root = new GameObject(WitchHouseRootName);
            if (restoreV1Lanterns) foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Point || GameLobby.BoostPrefixes.Any(p => l.name.StartsWith(p))) continue;
                l.intensity *= GameLobby.PointScale / V1LobbyPointScale;
                var flicker = l.GetComponent<FlickerLight>();
                if (flicker != null) flicker.EditorSetup(l.intensity, 0.12f, new Vector2(3f, 9f));
                EditorUtility.SetDirty(l);
                restored++;
            }
        }
        foreach (HouseLight h in HouseLights)
        {
            Transform t = root.transform.Find(h.Name);
            if (t == null) { t = new GameObject(h.Name).transform; t.SetParent(root.transform, false); }
            t.position = h.Position;
            t.rotation = Quaternion.LookRotation(h.Target - h.Position);
            Light l = t.GetComponent<Light>();
            if (l == null) l = t.gameObject.AddComponent<Light>();
            l.type = h.Type; l.color = h.Color; l.intensity = h.Intensity; l.range = h.Range; l.spotAngle = h.Angle;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel; // 멀리서도 픽셀 조명으로(정점 조명이면 집 벽이 뭉개진다)
            EditorUtility.SetDirty(l);
        }
        return $"witch house lights {HouseLights.Length}, lanterns restored {restored}, windows {BrightenWindows()}";
    }

    // 창문 발광: 공유 재질은 그대로 두고 대기실 전용 사본(발광 ×1.8)을 집에만 끼운다(GingerbreadNeonDimmer와 같은 방식).
    private static int BrightenWindows()
    {
        GameObject house = GameObject.Find(WitchHousePath);
        if (house == null) return 0;
        if (!AssetDatabase.IsValidFolder(LobbyMaterialFolder)) AssetDatabase.CreateFolder("Assets/Maps/GameLobby", "Materials");
        var copies = new Dictionary<string, Material>();
        int swapped = 0;
        foreach (Renderer r in house.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string baseName = mats[i].name.Replace("_Lobby", "");
                if (!WindowMaterials.Contains(baseName)) continue;
                if (!copies.TryGetValue(baseName, out Material copy)) copies[baseName] = copy = LobbyWindowCopy(baseName);
                if (copy == null || mats[i] == copy) continue;
                mats[i] = copy; changed = true; swapped++;
            }
            if (changed) { r.sharedMaterials = mats; EditorUtility.SetDirty(r); }
        }
        return swapped;
    }

    private static Material LobbyWindowCopy(string baseName)
    {
        string src = AssetDatabase.FindAssets($"{baseName} t:Material").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == baseName);
        if (src == null) return null;
        var original = AssetDatabase.LoadAssetAtPath<Material>(src);
        string path = $"{LobbyMaterialFolder}/{baseName}_Lobby.mat";
        var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (copy == null) { copy = new Material(original); AssetDatabase.CreateAsset(copy, path); }
        else copy.CopyPropertiesFromMaterial(original);
        copy.EnableKeyword("_EMISSION");
        copy.SetColor("_EmissionColor", original.GetColor("_EmissionColor") * WindowEmissionScale);
        EditorUtility.SetDirty(copy);
        return copy;
    }

    [MenuItem(MenuRoot + "Apply Game Lobby Look")]
    public static void ApplyGameLobbyOnly()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var lobby = EditorSceneManager.OpenScene(GameLobbyScene, OpenSceneMode.Single);
        Debug.Log($"[TwistedAtmosphere] GameLobby: {ApplyToOpenGameLobby()}");
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);
        AssetDatabase.SaveAssets();
    }

    // 모든 맵·대기실 후처리 프로필에서 비네트를 지운다(Request1009Plan.md §16.5). 지운 프로필 수.
    [MenuItem(MenuRoot + "Remove Vignette (All Profiles)")]
    public static int RemoveAllVignettes()
    {
        int removed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:PostProcessProfile", new[] { "Assets/Maps" }))
            if (RemoveVignette(AssetDatabase.LoadAssetAtPath<PostProcessProfile>(AssetDatabase.GUIDToAssetPath(guid)))) removed++;
        AssetDatabase.SaveAssets();
        Debug.Log($"[TwistedAtmosphere] Removed vignette from {removed} profiles.");
        return removed;
    }

    [MenuItem(MenuRoot + "Apply Twisted Atmosphere (Lobbies)")]
    public static void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var report = new List<string>();
        var lobby = EditorSceneManager.OpenScene(GameLobbyScene, OpenSceneMode.Single);
        report.Add($"GameLobby: {ApplyToOpenGameLobby()}");
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);

        var menu = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);
        report.Add($"Lobby: {ApplyToOpenLobby()}");
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        AssetDatabase.SaveAssets();
        Debug.Log("[TwistedAtmosphere]\n" + string.Join("\n", report));
    }

    public static string ApplyToOpenGameLobby()
    {
        bool v1Applied = Object.FindFirstObjectByType<FlickerLight>(FindObjectsInactive.Include) != null; // V1 등불 ×0.75가 이미 들어간 씬
        string env = ApplyEnvironment(GameLobby);
        PostProcessProfile profile = LoadOrCreateProfile(GameLobbyProfile);
        if (!profile.TryGetSettings(out Bloom bloom)) bloom = AddSetting<Bloom>(profile);
        bloom.intensity.Override(0.9f);
        bloom.threshold.Override(1.1f);
        bloom.diffusion.Override(7f);
        ApplyGrading(profile, GameLobby);

        int layer = LayerMask.NameToLayer("PostProcessing");
        var volume = Object.FindFirstObjectByType<PostProcessVolume>();
        if (volume == null)
        {
            volume = new GameObject("PostFX") { layer = layer < 0 ? 0 : layer }.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
        }
        volume.sharedProfile = profile;
        Camera cam = Camera.main;
        if (cam != null)
        {
            var postLayer = cam.GetComponent<PostProcessLayer>();
            if (postLayer == null)
            {
                postLayer = cam.gameObject.AddComponent<PostProcessLayer>();
                string resGuid = AssetDatabase.FindAssets("t:PostProcessResources").FirstOrDefault();
                if (resGuid != null) postLayer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(resGuid)));
            }
            postLayer.volumeLayer = layer < 0 ? 1 : 1 << layer;
            postLayer.volumeTrigger = cam.transform;
            postLayer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            cam.allowHDR = true;
            EditorUtility.SetDirty(cam.gameObject);
        }
        return env + $", PostFX -> {GameLobbyProfile}, {ApplyWitchHouseLighting(v1Applied)}";
    }

    // 로비(★): 같은 그림의 해 질 녘 판(채도 −42%, 회녹색, 바닥 안개, 비네트 — 원본은 그대로 둔다).
    public static string ApplyToOpenLobby()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LobbyBackground);
        if (sprite == null) return $"missing {LobbyBackground}";
        foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.name != "BackGround") continue;
            image.sprite = sprite;
            EditorUtility.SetDirty(image);
            return $"background -> {sprite.name}";
        }
        return "no BackGround image";
    }

    private static string ApplyEnvironment(Look look)
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = look.Fog;
        RenderSettings.fogDensity = look.FogDensity;
        bool firstTime = Object.FindFirstObjectByType<FlickerLight>(FindObjectsInactive.Include) == null;

        Light sun = null;
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(l => l.name).ThenBy(l => l.transform.position.x).ToArray();
        foreach (Light l in lights) if (l.type == LightType.Directional) sun = l;
        if (look.OverrideSun && sun != null)
        {
            sun.color = look.SunColor;
            sun.intensity = look.SunIntensity;
            EditorUtility.SetDirty(sun);
        }
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = look.Fog; // 안개와 이어지는 지평선
            EditorUtility.SetDirty(cam);
        }
        if (!firstTime) return $"fog {look.FogDensity} (lights already twisted)";

        RenderSettings.ambientLight *= look.AmbientScale;
        RenderSettings.ambientEquatorColor *= look.AmbientScale;
        RenderSettings.ambientGroundColor *= look.AmbientScale;
        int changed = 0, flicker = 0;
        var counters = new Dictionary<string, int>();
        foreach (Light l in lights)
        {
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;
            float gray = l.color.grayscale;
            Color c = Color.Lerp(l.color, new Color(gray, gray, gray), look.PointDesaturate);
            c.a = 1f;
            l.color = c;
            float scale = look.PointScale;
            if (look.BoostPrefixes.Any(p => l.name.StartsWith(p))) scale = look.BoostScale;
            l.intensity *= scale;
            changed++;
            string prefix = look.FlickerPrefixes.FirstOrDefault(p => l.name.StartsWith(p));
            if (prefix != null)
            {
                counters.TryGetValue(prefix, out int n);
                counters[prefix] = n + 1;
                if (n % look.FlickerEvery == 0)
                {
                    var f = l.gameObject.AddComponent<FlickerLight>();
                    f.EditorSetup(l.intensity, 0.12f, new Vector2(3f, 9f));
                    flicker++;
                }
            }
            EditorUtility.SetDirty(l);
        }
        return $"fog {look.FogDensity}, points {changed}, flicker {flicker}";
    }

    private static void ApplyGrading(PostProcessProfile profile, Look look)
    {
        if (!profile.TryGetSettings(out ColorGrading grading))
        {
            grading = AddSetting<ColorGrading>(profile);
            grading.gradingMode.Override(GradingMode.HighDefinitionRange);
            grading.tonemapper.Override(Tonemapper.ACES);
        }
        grading.saturation.Override(look.Saturation);
        grading.temperature.Override(look.Temperature);
        grading.tint.Override(look.Tint);
        grading.contrast.Override(look.Contrast);
        grading.postExposure.Override(look.PostExposure);
        // 비네트(화면 가장자리 어둡게)는 시야가 좁아 보여 지웠다(Request1009Plan.md §16.5). 값이 0이면 설정 자체를 뺀다.
        if (look.Vignette > 0f)
        {
            if (!profile.TryGetSettings(out Vignette vignette)) vignette = AddSetting<Vignette>(profile);
            vignette.intensity.Override(look.Vignette);
            vignette.smoothness.Override(0.55f);
        }
        else RemoveVignette(profile);
        EditorUtility.SetDirty(profile);
    }

    // 프로필에서 비네트 설정과 그 하위 에셋을 지운다. 지웠으면 true.
    public static bool RemoveVignette(PostProcessProfile profile)
    {
        if (profile == null || !profile.TryGetSettings(out Vignette vignette)) return false;
        profile.RemoveSettings<Vignette>();
        if (AssetDatabase.Contains(vignette)) AssetDatabase.RemoveObjectFromAsset(vignette);
        Object.DestroyImmediate(vignette, true);
        EditorUtility.SetDirty(profile);
        return true;
    }

    private static PostProcessProfile LoadOrCreateProfile(string path)
    {
        var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
        if (profile != null) return profile;
        string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    private static T AddSetting<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
    {
        T s = profile.AddSettings<T>();
        s.name = typeof(T).Name;
        s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(s, profile);
        return s;
    }
}
