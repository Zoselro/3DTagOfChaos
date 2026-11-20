using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Request1009Plan.md §90 자산 설정(다시 실행해도 같은 결과): Blender에서 받은 모델의 재질(Standard 색·노멀·금속/매끈 + 비닐 필름 반투명),
// 술래 바주카 조준·발사 / 쿠키 활공 동작 가져오기와 애니메이터 층·상태, 새 아이템 3종(껌 수류탄·패러글라이더·바주카포)·투사체,
// 카탈로그(쿠키 상자 목록에서 권투 글러브 → 껌, 패러글라이더 = Heaven Of Bakery 전용), 바주카 어깨 자리, 한국어 문구.
public static class P90AssetSetup
{
    public const string ItemDir = "Assets/09. Environment/Escape/Models/P90";
    public const string FarmDir = "Assets/09. Environment/HeavenOfBakery/Farm";
    private const string SoRoot = "Assets/03. SO/Escape";
    private const string ProjDir = SoRoot + "/Projectiles";
    private const string MonsterController = "Assets/Animation/MonsterAnimator.controller";
    private const string CookieController = "Assets/Animation/PlayerAnimator.controller";
    private const string MonsterPrefab = "Assets/04. Prefabs/Resources/MonsterPlayer.prefab";
    private const string AimFbx = "Assets/Animation/Monster/NewAnimation/Monster_Bazooka_Aim.fbx";
    private const string FireFbx = "Assets/Animation/Monster/NewAnimation/Monster_Bazooka_Fire.fbx";
    private const string GlideFbx = "Assets/Animation/Cookie/Actions/Cookie_GlideSit.fbx";
    // Blender 아마추어 공간에서 정한 손잡이 자리(bz_anim.py GRIP, x는 Unity에서 뒤집힘)
    private static readonly Vector3 GripArmature = new Vector3(0.55f, -0.20f, 0.94f);

    [MenuItem("Tools/TagOfChaos/Escape/§90 Setup Assets")]
    public static string Run()
    {
        var log = new List<string>();
        log.Add(Models());
        log.Add(Clips());
        log.Add(MonsterLayer());
        log.Add(CookieGlide());
        log.Add(Items());
        log.Add(Socket());
        log.Add(Texts());
        AssetDatabase.SaveAssets();
        string s = "[P90AssetSetup] " + string.Join(" | ", log);
        Debug.Log(s);
        return s;
    }

    // ---------------- 재질 ----------------

    private static void TexImport(string path, bool normal, bool srgb, int max)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        bool ch = false;
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType != type) { ti.textureType = type; ch = true; }
        if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; ch = true; }
        if (ti.maxTextureSize != max) { ti.maxTextureSize = max; ch = true; }
        if (ti.textureCompression != TextureImporterCompression.CompressedHQ) { ti.textureCompression = TextureImporterCompression.CompressedHQ; ch = true; }
        if (ti.anisoLevel != 4) { ti.anisoLevel = 4; ch = true; }
        if (ch) ti.SaveAndReimport();
    }

    public static Material ModelMaterial(string dir, string n)
    {
        string t = dir + "/Textures/" + n;
        TexImport(t + "_Color.png", false, true, 2048);
        TexImport(t + "_Normal.png", true, false, 2048);
        TexImport(t + "_MetallicSmooth.png", false, false, 2048);
        string path = $"{dir}/M_{n}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Standard");
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "_Color.png"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "_Normal.png")); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f);
        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "_MetallicSmooth.png")); m.EnableKeyword("_METALLICGLOSSMAP");
        m.SetFloat("_GlossMapScale", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f);
        m.SetColor("_Color", Color.white);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // 비닐하우스 필름: 맑은 반투명(Standard Transparent) — 안이 비쳐 보이고 빛이 반짝인다.
    public static Material FilmMaterial()
    {
        string path = FarmDir + "/M_GreenhouseFilm.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Standard");
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetColor("_Color", new Color(0.93f, 0.97f, 1f, 0.22f));
        m.SetFloat("_Glossiness", 0.92f); m.SetFloat("_Metallic", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static string Models()
    {
        int n = 0;
        foreach (string dir in new[] { ItemDir, FarmDir })
            foreach (string fbx in Directory.GetFiles(dir, "*.fbx"))
            {
                string p = fbx.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(p);
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                bool farm = dir == FarmDir || name.StartsWith("SK_Monster");
                imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None;
                imp.isReadable = farm;   // 장식·벽장은 MeshCollider·NavMesh·조각 모델 내보내기
                imp.importCameras = false; imp.importLights = false;
                imp.importNormals = ModelImporterNormals.Import; imp.importTangents = ModelImporterTangents.CalculateMikk;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                Material target = name == "SK_GreenhouseFilm" ? FilmMaterial() : File.Exists($"{dir}/Textures/{name}_Color.png") ? ModelMaterial(dir, name) : null;
                imp.SaveAndReimport();
                if (target != null)
                {
                    var names = AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToList();
                    foreach (string mn in names) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mn), target);
                    imp.SaveAndReimport();
                }
                n++;
            }
        return "models " + n;
    }

    // ---------------- 동작 ----------------

    private static string Clips()
    {
        var monsterAvatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Animation/Monster/NewAnimation/Monster_Rigged.fbx").OfType<Avatar>().FirstOrDefault();
        var cookieAvatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Animation/Cookie/UV/Cookie_Idle.fbx").OfType<Avatar>().FirstOrDefault();
        Clip(AimFbx, "Bazooka_Aim", true, ModelImporterAnimationType.Generic, monsterAvatar);
        Clip(FireFbx, "Bazooka_Fire", false, ModelImporterAnimationType.Generic, monsterAvatar);
        Clip(GlideFbx, "Cookie_GlideSit", true, ModelImporterAnimationType.Human, cookieAvatar);
        FixUnits(AimFbx, AimAnim, true);
        FixUnits(FireFbx, FireAnim, false);
        return $"clips aim={LoadClip(AimFbx) != null} fire={LoadClip(FireFbx) != null} glide={LoadClip(GlideFbx) != null} avatars={monsterAvatar != null}/{cookieAvatar != null}";
    }

    private static void Clip(string path, string name, bool loop, ModelImporterAnimationType type, Avatar src)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return;
        imp.animationType = type;
        if (src != null) { imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; imp.sourceAvatar = src; }
        imp.importAnimation = true;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.SaveAndReimport();
        ModelImporterClipAnimation[] defs = imp.defaultClipAnimations;
        if (defs.Length == 0) return;
        ModelImporterClipAnimation c = defs[0];
        c.name = name;
        c.loopTime = loop;
        c.loopPose = loop;
        c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
        c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
        imp.clipAnimations = new[] { c };
        imp.SaveAndReimport();
    }

    public const string AimAnim = "Assets/Animation/Monster/NewAnimation/Monster_Bazooka_Aim.anim";
    public const string FireAnim = "Assets/Animation/Monster/NewAnimation/Monster_Bazooka_Fire.anim";

    // Blender에서 단위 1로 내보내 아마추어 크기 100·뼈 위치 1/100로 들어왔다 — 기존 술래 클립(Idle: 아마추어 1, Chest y 0.18)과 맞게
    // 뼈 위치를 100배, 아마추어 크기를 1로 고친 복사본(.anim)을 만든다(TentacleDashWindup.anim과 같은 방식).
    private static void FixUnits(string fbx, string anim, bool loop)
    {
        AnimationClip src = LoadClip(fbx);
        if (src == null) return;
        var dst = AssetDatabase.LoadAssetAtPath<AnimationClip>(anim);
        if (dst == null) { dst = new AnimationClip(); AssetDatabase.CreateAsset(dst, anim); }
        dst.ClearCurves();
        dst.frameRate = src.frameRate;
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(src))
        {
            AnimationCurve c = AnimationUtility.GetEditorCurve(src, b);
            bool armature = b.path == "MonsterArmature";
            if (armature && b.propertyName.StartsWith("m_LocalScale")) c = AnimationCurve.Constant(0f, src.length, 1f);
            else if (!armature && b.propertyName.StartsWith("m_LocalPosition") && b.path.StartsWith("MonsterArmature/"))
            {
                Keyframe[] k = c.keys;
                for (int i = 0; i < k.Length; i++) { k[i].value *= 100f; k[i].inTangent *= 100f; k[i].outTangent *= 100f; }
                c.keys = k;
            }
            AnimationUtility.SetEditorCurve(dst, b, c);
        }
        var st = AnimationUtility.GetAnimationClipSettings(dst);
        st.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(dst, st);
        EditorUtility.SetDirty(dst);
    }

    public static AnimationClip LoadClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    // 술래: 윗몸 층 "Bazooka"(척추 아래만) — Empty ↔ Aim(BazookaAim bool), 어디서든 Fire(BazookaFire 트리거) → Aim/Empty.
    private static string MonsterLayer()
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(MonsterController);
        AnimationClip aim = AssetDatabase.LoadAssetAtPath<AnimationClip>(AimAnim), fire = AssetDatabase.LoadAssetAtPath<AnimationClip>(FireAnim);
        if (c == null || aim == null || fire == null) return "monster layer: missing";
        if (!c.parameters.Any(p => p.name == "BazookaAim")) c.AddParameter("BazookaAim", AnimatorControllerParameterType.Bool);
        if (!c.parameters.Any(p => p.name == "BazookaFire")) c.AddParameter("BazookaFire", AnimatorControllerParameterType.Trigger);
        AvatarMask mask = UpperMask();
        int li = System.Array.FindIndex(c.layers, l => l.name == "Bazooka");
        if (li >= 0) c.RemoveLayer(li);
        c.AddLayer("Bazooka");
        AnimatorControllerLayer[] layers = c.layers;
        li = layers.Length - 1;
        layers[li].defaultWeight = 1f;
        layers[li].avatarMask = mask;
        layers[li].blendingMode = AnimatorLayerBlendingMode.Override;
        c.layers = layers;
        AnimatorStateMachine sm = c.layers[li].stateMachine;
        AnimatorState empty = sm.AddState("Empty");
        AnimatorState aimS = sm.AddState("Aim"); aimS.motion = aim;
        AnimatorState fireS = sm.AddState("Fire"); fireS.motion = fire;
        sm.defaultState = empty;
        var toAim = empty.AddTransition(aimS); toAim.hasExitTime = false; toAim.duration = 0.15f; toAim.AddCondition(AnimatorConditionMode.If, 0, "BazookaAim");
        var toEmpty = aimS.AddTransition(empty); toEmpty.hasExitTime = false; toEmpty.duration = 0.2f; toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "BazookaAim");
        var anyFire = sm.AddAnyStateTransition(fireS); anyFire.hasExitTime = false; anyFire.duration = 0.03f; anyFire.canTransitionToSelf = true; anyFire.AddCondition(AnimatorConditionMode.If, 0, "BazookaFire");
        var fireAim = fireS.AddTransition(aimS); fireAim.hasExitTime = true; fireAim.exitTime = 0.95f; fireAim.duration = 0.1f; fireAim.AddCondition(AnimatorConditionMode.If, 0, "BazookaAim");
        var fireEmpty = fireS.AddTransition(empty); fireEmpty.hasExitTime = true; fireEmpty.exitTime = 0.95f; fireEmpty.duration = 0.15f; fireEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "BazookaAim");
        EditorUtility.SetDirty(c);
        return "monster layer ok (mask " + mask.transformCount + ")";
    }

    private static AvatarMask UpperMask()
    {
        string path = "Assets/Animation/Monster/MonsterUpperBody.mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, path); }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefab);
        mask.transformCount = 0;
        mask.AddTransformPath(prefab.transform, true);
        for (int i = 0; i < mask.transformCount; i++)
        {
            string p = mask.GetTransformPath(i);
            mask.SetTransformActive(i, p.Contains("/Spine"));   // 척추·가슴·팔·촉수·머리만(다리·엉덩이는 걷기 그대로)
        }
        for (int b = 0; b < (int)AvatarMaskBodyPart.LastBodyPart; b++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)b, false);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    // 쿠키: 기본 층에 Glide 상태(GlideSit 반복) + 어디서든 Glide 트리거.
    private static string CookieGlide()
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(CookieController);
        AnimationClip glide = LoadClip(GlideFbx);
        if (c == null || glide == null) return "cookie glide: missing";
        if (!c.parameters.Any(p => p.name == "Glide")) c.AddParameter("Glide", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        AnimatorState st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Glide") ?? sm.AddState("Glide", new Vector3(600f, 300f, 0f));
        st.motion = glide;
        if (!sm.anyStateTransitions.Any(t => t.destinationState == st))
        {
            var t = sm.AddAnyStateTransition(st);
            t.hasExitTime = false; t.duration = 0.2f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Glide");
        }
        EditorUtility.SetDirty(c);
        return "cookie glide ok";
    }

    // ---------------- 아이템·투사체·카탈로그 ----------------

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    private static string Items()
    {
        GameObject gumModel = AssetDatabase.LoadAssetAtPath<GameObject>(ItemDir + "/SK_GumGrenade.fbx");
        GameObject rocket = AssetDatabase.LoadAssetAtPath<GameObject>(ItemDir + "/SK_Rocket.fbx");
        GameObject bag = AssetDatabase.LoadAssetAtPath<GameObject>(ItemDir + "/SK_ParagliderBag.fbx");
        GameObject bazookaMesh = AssetDatabase.LoadAssetAtPath<GameObject>(ItemDir + "/SK_Bazooka.fbx");

        var gumProj = LoadOrCreate<ProjectileConfigSO>(ProjDir + "/Projectile_GumTrap.asset");
        gumProj.baseRange = 12f; gumProj.maxRange = 20f; gumProj.launchAngle = 20f; gumProj.radius = 0.15f; gumProj.gravity = 14.2f;   // 물풍선과 같은 던지기
        gumProj.bounciness = 0f; gumProj.burstOnImpact = true; gumProj.fuseAfterImpact = 0f; gumProj.maxLifeSeconds = 5f; gumProj.spinSpeed = 360f; gumProj.model = null; gumProj.modelScale = 1f;
        EditorUtility.SetDirty(gumProj);
        var rocketProj = LoadOrCreate<ProjectileConfigSO>(ProjDir + "/Projectile_Bazooka.asset");
        rocketProj.baseRange = BazookaRules.MaxRange; rocketProj.maxRange = BazookaRules.MaxRange; rocketProj.launchAngle = 10f; rocketProj.radius = 0.25f; rocketProj.gravity = 1f;
        rocketProj.bounciness = 0f; rocketProj.burstOnImpact = true; rocketProj.fuseAfterImpact = 0f; rocketProj.maxLifeSeconds = BazookaRules.MaxLifeSeconds + 0.05f; rocketProj.spinSpeed = 0f;
        rocketProj.model = rocket; rocketProj.modelScale = 1f;
        EditorUtility.SetDirty(rocketProj);

        ToolSO gumTool = Tool("Tool_GumTrap", ToolKind.GumTrap, 2, 12f, GumTrapRules.HitRadius, 0f, 0f, 2f, gumProj);
        ToolSO gliderTool = Tool("Tool_Paraglider", ToolKind.Paraglider, 1, 1f, 0.5f, 0f, 0f, 1f, null);
        ToolSO bazTool = Tool("Tool_Bazooka", ToolKind.Bazooka, 99, BazookaRules.MaxRange, BazookaRules.KillRadius, BazookaRules.StunSeconds, 0f, BazookaRules.Cooldown, rocketProj);

        ItemSO gum = Item("Item_GumTrap", "GumTrap", "Gum grenade", new Color(0.62f, 0.25f, 0.78f), HoldType.OneHanded, gumTool, gumModel);
        ItemSO glider = Item("Item_Paraglider", "Paraglider", "Paraglider", new Color(1f, 0.62f, 0.72f), HoldType.OneHanded, gliderTool, bag);
        ItemSO baz = Item("Item_Bazooka", "Bazooka", "Bazooka", new Color(0.55f, 0.38f, 0.25f), HoldType.OneHanded, bazTool, rocket);
        gum.EditorSetGripPoint(true, Vector3.zero);

        var catalog = AssetDatabase.LoadAssetAtPath<EscapeCatalogSO>("Assets/Resources/" + EscapeCatalogSO.ResourcePath + ".asset");
        var all = catalog.Items.ToList();
        foreach (ItemSO i in new[] { gum, glider, baz }) if (!all.Contains(i)) all.Add(i);
        ItemSO skates = catalog.Find("Skates"), pumpkin = catalog.Find("Pumpkin"), recorder = catalog.Find("Recorder"), flash = catalog.Find("Flashbang"), cannon = catalog.Find("ColaCannon");
        // 쿠키 상자: 권투 글러브(4) → 껌 수류탄(4), 패러글라이더는 콜라 대포 몫(3) — Heaven Of Bakery 전용(다른 맵은 무게 0)
        var cookie = new[] { skates, pumpkin, recorder, flash, gum, cannon, glider };
        var weights = new[] { 5, 3, 5, 3, 4, 3, 3 };
        catalog.EditorSetRequest1119(all.ToArray(), cookie, weights, baz, bazookaMesh,
            AssetDatabase.LoadAssetAtPath<GameObject>(ItemDir + "/SK_GumPuddle.fbx"), AssetDatabase.LoadAssetAtPath<GameObject>(ItemDir + "/SK_GumStuck.fbx"),
            AssetDatabase.LoadAssetAtPath<GameObject>(ItemDir + "/SK_Paraglider.fbx"), LoadClip(GlideFbx), AssetDatabase.LoadAssetAtPath<AnimationClip>(AimAnim), AssetDatabase.LoadAssetAtPath<AnimationClip>(FireAnim));
        EditorUtility.SetDirty(catalog);
        EscapeRecipeSO hob = catalog.RecipeFor("HeavenOfBakery");
        if (hob != null) { hob.EditorSetExclusiveItems(new[] { glider }); EditorUtility.SetDirty(hob); }
        // 개발용 오프라인 시작 아이템(권투 글러브 → 껌 수류탄)은 OfflineModeBootstrap 기본값에서 바꾼다.
        return $"items gum/glider/bazooka, cookie list {cookie.Count(x => x != null)}/7, HOB exclusive {(hob != null)}";
    }

    private static ToolSO Tool(string file, ToolKind kind, int charges, float range, float radius, float cookieStun, float monsterStun, float cooldown, ProjectileConfigSO proj)
    {
        var t = LoadOrCreate<ToolSO>($"{SoRoot}/Tools/{file}.asset");
        t.EditorSetup(kind, charges, range, radius, cookieStun, monsterStun, cooldown, false);
        t.EditorSetProjectile(proj);
        EditorUtility.SetDirty(t);
        return t;
    }

    private static ItemSO Item(string file, string id, string name, Color tint, HoldType hold, ToolSO tool, GameObject model)
    {
        var i = LoadOrCreate<ItemSO>($"{SoRoot}/Items/{file}.asset");
        i.EditorSetup(id, string.IsNullOrEmpty(i.DisplayName) ? name : i.DisplayName, tint, hold, ItemCategory.Tool, tool);
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/08. Resources/ItemIcons/Icon_{id}.png");
        i.EditorSetVisuals(icon, model);
        EditorUtility.SetDirty(i);
        return i;
    }

    // ---------------- 바주카 어깨 자리 ----------------

    // 조준 동작 첫 프레임 자세에서 손잡이 자리(아마추어 공간)를 가슴 뼈 기준으로 바꿔 카탈로그에 적는다.
    private static string Socket()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<EscapeCatalogSO>("Assets/Resources/" + EscapeCatalogSO.ResourcePath + ".asset");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefab);
        AnimationClip aim = AssetDatabase.LoadAssetAtPath<AnimationClip>(AimAnim);
        if (catalog == null || prefab == null || aim == null) return "socket: missing";
        GameObject go = Object.Instantiate(prefab);
        try
        {
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            aim.SampleAnimation(go, 0f);
            Transform arm = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "MonsterArmature");
            Transform chest = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Chest");
            Vector3 pos = arm.TransformPoint(GripArmature);
            Quaternion rot = Quaternion.LookRotation(arm.TransformDirection(Vector3.down), arm.TransformDirection(Vector3.forward));   // 포구 = 아마추어 -Y, 위 = +Z
            Vector3 ls = chest.lossyScale;
            catalog.EditorSetBazookaSocket(chest.InverseTransformPoint(pos), Quaternion.Inverse(chest.rotation) * rot, new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z));
            EditorUtility.SetDirty(catalog);
            return $"socket world {pos:F2} fwd {rot * Vector3.forward:F2}";
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ---------------- 문구(한국어는 에셋에) ----------------

    private const string TextFile = "Assets/Editor/Escape/P90Texts.ko.txt";

    public static Dictionary<string, string> LoadTexts()
    {
        var d = new Dictionary<string, string>();
        if (!File.Exists(TextFile)) return d;
        foreach (string line in File.ReadAllLines(TextFile, System.Text.Encoding.UTF8))
        {
            if (line.StartsWith("#")) continue;
            int eq = line.IndexOf('=');
            if (eq > 0) d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        return d;
    }

    private static string Texts()
    {
        var t = AssetDatabase.LoadAssetAtPath<EscapeTextsSO>("Assets/Resources/Escape/EscapeTexts.asset");
        if (t == null) return "texts: missing";
        Dictionary<string, string> ko = LoadTexts();
        var so = new SerializedObject(t);
        int n = 0;
        foreach (var kv in ko)
        {
            if (kv.Key.StartsWith("item.")) continue;
            var p = so.FindProperty(kv.Key);
            if (p != null && p.propertyType == SerializedPropertyType.String) { p.stringValue = kv.Value; n++; }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(t);
        foreach (var kv in ko)
        {
            if (!kv.Key.StartsWith("item.")) continue;
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>($"{SoRoot}/Items/Item_{kv.Key.Substring(5)}.asset");
            if (item == null) continue;
            var iso = new SerializedObject(item);
            iso.FindProperty("displayName").stringValue = kv.Value;
            iso.ApplyModifiedPropertiesWithoutUndo();
            n++;
        }
        return "texts " + n;
    }
}
