using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// 새 마녀(허수아비 마녀, Blender 리깅 — Request1009Plan.md §18.2)를 프로젝트에 맞춘다. 여러 번 실행해도 같은 결과.
// 1) FBX 가져오기: Generic 리그, 동작 5개(Idle·Walk 반복, Rise·Strike·TurnBack — Request1009Plan.md §23 X3), 재질을 ME_WitchScarecrow로 연결.
//    동작 원본: ArtSource/09. Environment/Witch/Source~/witch_rig(anim.py·export.py, Blender bpy로 만든다).
// 2) 재질(Standard: 색·노멀·금속/매끄러움)과 애니메이터(Idle 기본, Rise·Strike·TurnBack 트리거, Walking 참이면 Walk).
// 3) 프리팹 WITCH_Scarecrow(키 270 m로 맞춤 — Request1009Plan.md §24.10, 예전 90 m) → 맵 5개 WITCH_Anchor의 "Model"로 교체하고 WitchPresenter.height도 맞춘다.
public static class WitchModelSetup
{
    public const string Folder = "Assets/09. Environment/Witch"; // Escape/Models 밖 — 그 폴더의 가져오기 규칙(애니메이션 끔)을 피한다
    public const string FbxPath = Folder + "/WITCH_Scarecrow.fbx";
    public const string MaterialPath = Folder + "/ME_WitchScarecrow.mat";
    public const string ControllerPath = Folder + "/WitchAnimator.controller";
    public const string PrefabPath = "Assets/04. Prefabs/Escape/WITCH_Scarecrow.prefab";
    public const float SourceHeight = 1.643f; // Blender 모델 키(m)
    public const float SourceFeet = -0.824f;  // Blender 모델 원점이 몸 가운데라 발 높이(m) — 기준점(WITCH_Anchor)에 발을 맞춘다
    public const float TargetHeight = WitchPresenter.DefaultHeight; // WitchPresenter.height와 같은 크기(270 m)
    public static readonly string[] Clips = { "Idle", "Rise", "Strike", "Walk", "TurnBack" };
    public static readonly string[] Maps = MapScenes.MapNames;

    [MenuItem("Tools/Escape/Setup Witch Model (Scarecrow)")]
    public static void SetupMenu() => Debug.Log("[WitchModelSetup] " + SetupAll(true));

    public static string SetupAll(bool installInScenes)
    {
        ConfigureTextures();
        Material mat = EnsureMaterial();
        ConfigureModel(mat);
        AnimatorController controller = EnsureController();
        GameObject prefab = EnsurePrefab(controller);
        string scenes = installInScenes ? InstallInScenes(prefab) : "skipped";
        AssetDatabase.SaveAssets();
        return $"material {mat.name}, controller {controller.name}, prefab {prefab.name}, scenes {scenes}";
    }

    private static void ConfigureTextures()
    {
        SetTexture(Folder + "/Witch_BaseColor.png", TextureImporterType.Default, true);
        SetTexture(Folder + "/Witch_Normal.png", TextureImporterType.NormalMap, false);
        SetTexture(Folder + "/Witch_MetallicSmoothness.png", TextureImporterType.Default, false);
    }

    private static void SetTexture(string path, TextureImporterType type, bool srgb)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) return;
        if (ti.textureType == type && ti.sRGBTexture == srgb && ti.maxTextureSize == 2048) return;
        ti.textureType = type; ti.sRGBTexture = srgb; ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
    }

    private static Material EnsureMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Witch_BaseColor.png"));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Witch_Normal.png"));
        mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Witch_MetallicSmoothness.png"));
        mat.EnableKeyword("_NORMALMAP");
        mat.EnableKeyword("_METALLICGLOSSMAP");
        mat.SetFloat("_GlossMapScale", 0.6f);
        mat.color = Color.white;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void ConfigureModel(Material mat)
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ME_WitchScarecrow"), mat);
        mi.importAnimation = true;
        TakeInfo[] takes = mi.importedTakeInfos;
        mi.clipAnimations = Clips.Select(name =>
        {
            TakeInfo take = takes.FirstOrDefault(t => t.name.EndsWith(name));
            return new ModelImporterClipAnimation
            {
                name = name,
                takeName = take.name ?? name,
                firstFrame = take.name != null ? take.startTime * take.sampleRate : 0,
                lastFrame = take.name != null ? take.stopTime * take.sampleRate : 0,
                loopTime = name == "Idle" || name == "Walk",
            };
        }).ToArray();
        mi.SaveAndReimport();
    }

    private static AnimatorController EnsureController()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
        AssetDatabase.DeleteAsset(ControllerPath); // 매번 같은 구성으로 새로 만든다
        var c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().Where(a => !a.name.StartsWith("__preview__")).ToDictionary(a => a.name);
        c.AddParameter("Rise", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Strike", AnimatorControllerParameterType.Trigger);
        c.AddParameter("TurnBack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Walking", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        AnimatorState idle = sm.AddState("Idle", new Vector3(300, 0, 0)); idle.motion = clips["Idle"];
        AnimatorState rise = sm.AddState("Rise", new Vector3(300, 100, 0)); rise.motion = clips["Rise"];
        AnimatorState strike = sm.AddState("Strike", new Vector3(300, 200, 0)); strike.motion = clips["Strike"];
        AnimatorState turn = sm.AddState("TurnBack", new Vector3(300, 300, 0)); turn.motion = clips["TurnBack"];
        AnimatorState walk = sm.AddState("Walk", new Vector3(600, 0, 0)); walk.motion = clips["Walk"];
        sm.defaultState = idle;
        // 걷기: Idle ↔ Walk(Walking). 재생 속도는 WitchPresenter가 걷는 속도에 맞춘다.
        AnimatorStateTransition toWalk = idle.AddTransition(walk);
        toWalk.hasExitTime = false; toWalk.duration = 0.4f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "Walking");
        AnimatorStateTransition toIdle = walk.AddTransition(idle);
        toIdle.hasExitTime = false; toIdle.duration = 0.4f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");
        foreach ((AnimatorState s, string trig) in new[] { (rise, "Rise"), (strike, "Strike"), (turn, "TurnBack") })
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(s);
            t.hasExitTime = false; t.duration = 0.15f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, trig);
            AnimatorStateTransition back = s.AddTransition(idle);
            back.hasExitTime = true; back.exitTime = 0.98f; back.duration = 0.2f;
        }
        EditorUtility.SetDirty(c);
        return c;
    }

    private static GameObject EnsurePrefab(AnimatorController controller)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var root = new GameObject("Model");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath));
        model.transform.SetParent(root.transform, false);
        float scale = TargetHeight / SourceHeight;
        model.transform.localScale = Vector3.one * scale;
        model.transform.localPosition = Vector3.up * (-SourceFeet * scale);
        model.name = "Witch";
        var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // 맵 밖 먼 곳에서 화면 밖이어도 동작 시각을 맞춘다
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.updateWhenOffscreen = true; // 90 m로 키운 몸이 움직이면 기본 경계 상자를 벗어난다
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static string InstallInScenes(GameObject prefab)
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        int n = 0;
        foreach (string map in Maps)
        {
            var scene = EditorSceneManager.OpenScene(MapScenes.ScenePath(map), OpenSceneMode.Single);
            WitchPresenter witch = Object.FindFirstObjectByType<WitchPresenter>(FindObjectsInactive.Include);
            if (witch == null) continue;
            foreach (Transform child in witch.transform.Cast<Transform>().ToArray())
                if (child.name == "Model") Object.DestroyImmediate(child.gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, witch.transform);
            inst.name = "Model";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            var so = new SerializedObject(witch);
            so.FindProperty("model").objectReferenceValue = inst.transform;
            so.FindProperty("fadeInSeconds").floatValue = WitchPresenter.DefaultFadeSeconds;
            so.FindProperty("height").floatValue = TargetHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            n++;
        }
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        return n + "/" + Maps.Length;
    }
}
