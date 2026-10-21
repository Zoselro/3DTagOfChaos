using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 맵 5종(Assets/Maps)을 게임 씬으로 조립하는 도구(Plan.md/GameScenePlan.md). 게임 규칙·UI는 프리팹 GameSceneCore 하나를
// 인스턴스로 두고(Bug-fix-plan.md §41 ㊶ — 예전에는 GameScene을 한 번 복제해 매니저·HUD가 씬마다 따로 직렬화됐다), 카메라·스폰
// 지점·낙하 영역은 PlayerTestScene에서 복제한 뒤 환경만 맵 FBX로 바꾼다. 조명(맵 조명 JSON + 분위기 조정), 후처리(Bloom·색 보정), 충돌체 정책, 상호작용 문,
// 회전 연출, 스폰·낙하 영역, 지면 뚫림 검사까지 한 번에 하고 스스로 검증한다. 다시 실행하면 씬의 맵 부분만 새로 만든다.
public static class MapSceneBuilder
{
    private const string LogTag = "[MapScene]";
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";
    // 새 맵 씬의 시작점(카메라·스폰 지점·낙하 영역·GameSceneCore 인스턴스). 예전 원본 GameScene은 쓰지 않게 되어 PlayerTestScene으로
    // 바뀌었다(§41 U1) — 복제한 뒤 테스트 전용 루트와 시험용 환경은 지운다.
    private const string TemplateScene = "Assets/Scenes/PlayerTestScene.unity";
    private const string SceneCorePrefab = "Assets/04. Prefabs/Scene/GameSceneCore.prefab";
    private const string SceneFolder = "Assets/Scenes/Maps";
    private const string MapsRoot = "Assets/Maps";
    private const string DoorAssetFolder = "Assets/Maps/Common/Doors";
    private const string AnimFolder = "Assets/Maps/Common/Animations";

    // 씬 루트 이름(다시 빌드할 때 이 루트들만 지우고 새로 만든다)
    private const string RootMap = "Map", RootColliders = "MapColliders", RootLighting = "MapLighting",
        RootDoors = "MapDoors", RootPostFx = "PostFX";
    private static readonly string[] OwnedRoots = { RootMap, RootColliders, RootLighting, RootDoors, RootPostFx, MapCompactor.MarkerRoot };
    private static readonly string[] TemplateEnvironmentRoots = { "Ground", "Directional Light", "TestBootstrap" };
    // GameSceneCore로 옮겨 간 예전 씬 루트(프리팹 도입 전에 만든 맵 씬을 다시 열 때 프리팹 인스턴스로 바꾼다)
    private static readonly string[] LegacySceneCoreRoots = { "GameManager", "GameRuleManagers", "PaintManagers", "Canvas", "EventSystem" };

    private static readonly string[] Categories =
        { "Ground", "Terrain", "MainStructures", "GameplayProps", "Decoration", "Background", "Lighting", "Effects", "Water" };
    private static readonly HashSet<string> WalkableCategories = new HashSet<string> { "Ground", "Terrain" };

    public const float CarouselSecondsPerTurn = 40f;  // 결정 D11(사용자)
    public const float CarouselSpinSign = -1f;        // 위에서 볼 때 반시계 — 말 머리 방향으로 달린다(Request1009Plan.md §8)
    public const float FerrisSecondsPerTurn = 60f;
    public const float MapHalfExtent = 172f;          // 외곽 투명 벽 안쪽(350 m 맵)

    // 분위기(GameScenePlan.md §3.5 표, 결정 D10) — 조명 JSON 값에 곱하거나 덮어쓴다.
    private sealed class Mood
    {
        public float Ambient;       // 환경광 목표 밝기(휘도). JSON 색은 색조만 쓴다 — 원본 야간 색(휘도 0.02~0.05)은 게임 화면에서 거의 검정이라 판독이 안 된다
        public float Directional;   // 방향광 강도(JSON 값을 대체)
        public float FogDensity;
        public float BloomIntensity, BloomThreshold, BloomDiffusion;
        public float Temperature, Tint, Saturation, Contrast, Vignette;
        // 발광 배율(재질 이름 접두어 → 배율). 발광 재질은 다른 맵과 공유하므로 이 맵 전용 사본에만 적용한다(Bug-fix-plan.md §40 D5·D6)
        public Dictionary<string, float> EmissionScale;
    }

    private static readonly Dictionary<string, Mood> Moods = new Dictionary<string, Mood>
    {
        ["CandyForest"] = new Mood { Ambient = 0.42f, Directional = 1.0f, FogDensity = 0.006f, BloomIntensity = 0.8f, BloomThreshold = 1.2f, BloomDiffusion = 6f, Saturation = 10f },
        ["ChocolateFactory"] = new Mood { Ambient = 0.32f, Directional = 0.75f, FogDensity = 0.004f, BloomIntensity = 1.0f, BloomThreshold = 1.2f, BloomDiffusion = 6.5f, Temperature = 15f, Vignette = 0f },
        ["GingerbreadVillage"] = new Mood { Ambient = 0.22f, Directional = 0.25f, FogDensity = 0.006f, BloomIntensity = 1.2f, BloomThreshold = 1.2f, BloomDiffusion = 7f, Temperature = -5f, Vignette = 0f },
        ["HauntedBakery"] = new Mood { Ambient = 0.3f, Directional = 0.35f, FogDensity = 0.005f, BloomIntensity = 0.7f, BloomThreshold = 1.5f, BloomDiffusion = 7f, Tint = 15f, Vignette = 0f,
            EmissionScale = new Dictionary<string, float> { ["M_Neon_"] = 0.4f, ["M_Window_Yellow"] = 0.6f, ["M_Oven_Fire"] = 0.6f, ["M_Lantern_Glow"] = 0.7f, ["M_Glow_Yellow"] = 0.7f } },
        ["CursedCandyCarnival"] = new Mood { Ambient = 0.28f, Directional = 0.3f, FogDensity = 0.007f, BloomIntensity = 0.7f, BloomThreshold = 1.5f, BloomDiffusion = 7.5f, Contrast = 5f, Vignette = 0f,
            EmissionScale = new Dictionary<string, float> { ["M_Neon_"] = 0.4f, ["M_Lantern_Glow"] = 0.7f, ["M_Glow_Yellow"] = 0.7f } },
    };

    public static readonly string[] MapNames = { "CandyForest", "GingerbreadVillage", "ChocolateFactory", "CursedCandyCarnival", "HauntedBakery" };
    public static string ScenePath(string map) => $"{SceneFolder}/Game_{map}.unity";
    public static string SceneName(string map) => $"Game_{map}";

    // ---------------- menu ----------------

    [MenuItem(MenuRoot + "Build All Map Scenes")]
    public static void BuildAll()
    {
        foreach (string map in MapNames) Build(map);
        ApplyBuildSettingsAndMapList();
    }

    [MenuItem(MenuRoot + "Build CandyForest")] private static void B1() => Build("CandyForest");
    [MenuItem(MenuRoot + "Build GingerbreadVillage")] private static void B2() => Build("GingerbreadVillage");
    [MenuItem(MenuRoot + "Build ChocolateFactory")] private static void B3() => Build("ChocolateFactory");
    [MenuItem(MenuRoot + "Build CursedCandyCarnival")] private static void B4() => Build("CursedCandyCarnival");
    [MenuItem(MenuRoot + "Build HauntedBakery")] private static void B5() => Build("HauntedBakery");
    [MenuItem(MenuRoot + "Apply Build Settings And Map List")] public static void ApplyBuildSettingsAndMapListMenu() => ApplyBuildSettingsAndMapList();

    // ---------------- build ----------------

    public static void Build(string map)
    {
        if (!Moods.ContainsKey(map)) { Debug.LogError($"{LogTag} Unknown map {map}"); return; }
        EnsureFolder(SceneFolder);
        string path = ScenePath(map);
        if (!File.Exists(path) && !AssetDatabase.CopyAsset(TemplateScene, path))
        {
            Debug.LogError($"{LogTag} Could not copy {TemplateScene} to {path}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        foreach (GameObject go in scene.GetRootGameObjects())
            if (OwnedRoots.Contains(go.name) || TemplateEnvironmentRoots.Contains(go.name)) Object.DestroyImmediate(go);

        var report = new List<string>();
        if (!EnsureSceneCore(scene, report)) return;
        Transform mapRoot = NewRoot(RootMap);
        var instances = PlaceModels(map, mapRoot, report);
        ApplyColliderPolicy(instances, report);
        MarkStatic(mapRoot);

        LightingJson lighting = LoadLighting(map);
        BuildLighting(map, lighting, report);
        BuildPostFx(map, report);
        ApplyMaterials(map, mapRoot, report);

        BuildAnimated(map, mapRoot, report);
        BuildDoors(map, report);

        Physics.SyncTransforms();
        FixGround(mapRoot, report);
        ReportMissingColliders(mapRoot, report);
        PlaceSpawnsAndKillZone(report);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"{LogTag} {map} built -> {path}\n  " + string.Join("\n  ", report));

        // 방금 만든 원본 크기(±172 m) 씬을 원본으로 보관하고 ±70 m로 축소해 같은 경로에 저장한다(Plan.md/MapReplacePlan.md v2).
        MapCompactor.OnMapBuilt(map);
    }

    // 씬에 GameSceneCore 인스턴스가 정확히 하나 있게 한다. 프리팹 도입 전 형식(매니저·HUD 루트가 씬에 직접 있음)이면 그 루트를 지우고
    // 인스턴스로 바꾼다 — 씬 6개가 같은 값을 따로 들고 있어 한 곳만 고치면 어긋나던 문제(§41 ㊶)가 다시 생기지 않게 한다.
    public static bool EnsureSceneCore(Scene scene, List<string> report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneCorePrefab);
        if (prefab == null) { Debug.LogError($"{LogTag} {SceneCorePrefab} not found."); return false; }

        int instances = 0, removed = 0;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(go) == prefab) { instances++; continue; }
            if (LegacySceneCoreRoots.Contains(go.name)) { Object.DestroyImmediate(go); removed++; }
        }
        if (instances == 0)
        {
            var core = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            core.transform.SetSiblingIndex(0);
            AssignSceneViewIds(scene, core);
        }
        report?.Add($"scene core: {(instances == 0 ? "instantiated" : "kept")} GameSceneCore, removed {removed} legacy roots");
        return true;
    }

    // PUN은 프리팹 에셋 안의 PhotonView를 항상 sceneViewId 0으로 되돌리므로(PhotonViewHandler) 씬 PhotonView의 ID는 씬 인스턴스의
    // 오버라이드로 가져야 한다. 0으로 남으면 실행 중 ViewID가 0이 되어 채팅 RPC 등이 "Illegal view ID:0"으로 실패했다(§41 ㊶ 검증 중 발견).
    // 씬 안에서 쓰지 않는 가장 작은 번호부터 준다.
    private static void AssignSceneViewIds(Scene scene, GameObject core)
    {
        var used = new HashSet<int>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (var view in root.GetComponentsInChildren<Photon.Pun.PhotonView>(true))
                if (!view.transform.IsChildOf(core.transform) && view.sceneViewId > 0) used.Add(view.sceneViewId);

        int next = 1;
        foreach (var view in core.GetComponentsInChildren<Photon.Pun.PhotonView>(true))
        {
            while (used.Contains(next)) next++;
            view.sceneViewId = next;
            used.Add(next);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
        }
    }

    private static Transform NewRoot(string name)
    {
        var go = new GameObject(name);
        return go.transform;
    }

    // ---------------- models ----------------

    private sealed class Placed
    {
        public string Category;
        public GameObject Root;
    }

    // 맵 모델(FBX)에는 남아 있지만 씬에 두지 않는 오브젝트(사용자가 지운 것). Blender 스크립트도 같은 이름을 만들지 않는다.
    // 2026-10-03 캔디숲: 가운데 케이크 로켓이 솟아오를 때 어색하던 마카롱 바위 2개(build_candyforest.py, Request1003bPlan.md §5).
    public static readonly Dictionary<string, string[]> RemovedObjects = new Dictionary<string, string[]>
    {
        { "CandyForest", new[] { "CAN_Macaron_Rock_165", "CAN_Macaron_Rock_166" } },
    };

    private static void RemoveListed(string map, Transform root, List<string> report)
    {
        if (!RemovedObjects.TryGetValue(map, out string[] names)) return;
        int removed = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && System.Array.IndexOf(names, t.name) >= 0) { Object.DestroyImmediate(t.gameObject); removed++; }
        report.Add($"removed listed objects: {removed}");
    }

    private static List<Placed> PlaceModels(string map, Transform parent, List<string> report)
    {
        var placed = new List<Placed>();
        foreach (string category in Categories)
        {
            string modelPath = $"{MapsRoot}/{map}/Models/{map}_{category}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) continue;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            // 충돌체·Static·회전 연출을 오브젝트마다 바꾸므로 모델 인스턴스를 푼다(모델 프리팹 인스턴스는 자식 수정·재배치 불가).
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = category;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            placed.Add(new Placed { Category = category, Root = instance });
        }
        RemoveListed(map, parent, report);
        report.Add($"models: {string.Join(", ", placed.Select(p => $"{p.Category}({p.Root.transform.childCount})"))}");
        return placed;
    }

    // ---------------- colliders (§3.3, D7) ----------------

    private static void ApplyColliderPolicy(List<Placed> placed, List<string> report)
    {
        HashSet<Collider> concaveByShape = AddMissingColliders(placed, report);
        Physics.SyncTransforms(); // 방금 배치한 충돌체를 광선 검사에 반영
        int convex = 0, keptConcave = 0, boundaryHidden = 0;
        var walkThrough = new List<string>();
        var largeStatic = new List<string>();
        var gapConcave = new List<string>();
        foreach (Placed p in placed)
        {
            foreach (MeshCollider mc in p.Root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (mc.name.Contains("COL_Boundary")) // 이름에 맵 접두어가 붙는다(CAN_COL_Boundary_01)
                {
                    var r = mc.GetComponent<MeshRenderer>();
                    if (r != null && r.enabled) { r.enabled = false; boundaryHidden++; }
                    keptConcave++;
                    continue;
                }
                if (WalkableCategories.Contains(p.Category) || IsWalkOver(mc.name) || concaveByShape.Contains(mc))
                {
                    keptConcave++; // 넓은 정적 걷는 지형
                    continue;
                }
                if (HasWalkThroughOpening(mc))
                {
                    keptConcave++; // 아치·정문 등 걸어서 지나가는 구조물(D7)
                    walkThrough.Add(mc.name);
                    continue;
                }
                // convex 껍질은 면 256개가 한도라, 넘으면 Unity가 오류를 내고 일부 껍질만 만든다. 실제로 구워 봐서 실패한
                // 메시는 정적이므로 오목 충돌체를 유지한다. 예전에는 GameplayProps를 메시 경계 상자로 바꿨는데, 원뿔 텐트의 빈 모서리가
                // 투명 벽이 됐다(Request1009Plan.md §10).
                if (!ConvexHullFits(mc.sharedMesh))
                {
                    keptConcave++;
                    largeStatic.Add(mc.name);
                    continue;
                }
                // 볼록 껍질이 차양 아래 같은 빈 곳을 메우면(쿠키가 서서 지나갈 만한 곳) 오목으로 둔다 — 검토한 목록만(§10, ConvexGapScanner).
                mc.convex = true;
                if (ConvexGapScanner.IsListed(mc.name) && ConvexGapScanner.IsStaticCandidate(mc) && ConvexGapScanner.GapArea(mc) >= ConvexGapScanner.MinGapArea)
                {
                    mc.convex = false;
                    keptConcave++;
                    gapConcave.Add(mc.name);
                    continue;
                }
                convex++;
            }
        }
        report.Add($"colliders: convex {convex}, non-convex kept {keptConcave} (walk-through {walkThrough.Count}: {Short(walkThrough)}; hull over limit {largeStatic.Count}: {Short(largeStatic)}; hull fills a gap {gapConcave.Count}: {Short(gapConcave)}), boundary renderers hidden {boundaryHidden}");
    }

    // 원본 FBX는 Ground·Terrain·MainStructures·GameplayProps에만 충돌 메시가 있다. 나머지 분류(장식·배경·조명 메시)에 충돌체를 붙인다
    // (Bug-fix-plan.md §39). 붙인 MeshCollider는 이어지는 규칙(통과형 아치 → 오목, 껍질 한도 → 상자 등)을 그대로 거친다.
    private static readonly HashSet<string> ColliderlessCategories = new HashSet<string> { "Decoration", "Background", "Lighting" };
    private const float FlatHeight = 0.35f, FlatGroundGap = 0.3f;    // D3: 바닥에 깔린 납작한 물체(깔개·마루판)
    private const float TrunkSampleHeight = 1.2f, TrunkMaxFootprint = 0.25f, TrunkMinHeight = 2.5f; // D2: 줄기형 판정
    private const float MergedMeshExtent = 60f;                         // 여러 물체를 합친 지형 장식 메시(맵 전체 크기)

    private static HashSet<Collider> AddMissingColliders(List<Placed> placed, List<string> report)
    {
        Physics.SyncTransforms(); // 바닥 판정(IsLyingOnGround)이 이미 있는 지면 충돌체를 쓴다
        var concave = new HashSet<Collider>();
        int meshes = 0, trunks = 0, merged = 0, flat = 0, outside = 0;
        foreach (Placed p in placed)
        {
            if (!ColliderlessCategories.Contains(p.Category)) continue;
            foreach (MeshFilter mf in p.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                var renderer = mf.GetComponent<Renderer>();
                if (renderer == null) continue;
                Bounds b = renderer.bounds;
                if (Mathf.Max(b.size.x, b.size.z) > MergedMeshExtent)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>(); // 합친 지형 장식 — 볼록 껍질이면 맵 전체를 덮는다
                    mc.sharedMesh = mf.sharedMesh;
                    concave.Add(mc);
                    merged++;
                    continue;
                }
                if (Mathf.Abs(b.center.x) > MapHalfExtent || Mathf.Abs(b.center.z) > MapHalfExtent) { outside++; continue; } // 닿을 수 없는 먼 배경
                if (b.size.y < FlatHeight && IsLyingOnGround(b)) { flat++; continue; }
                if (TryAddTrunkCapsule(mf, b)) { trunks++; continue; }
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                meshes++;
            }
        }
        report.Add($"added colliders: mesh {meshes}, trunk capsules {trunks}, merged terrain decor (non-convex) {merged}; skipped flat {flat}, outside play area {outside}");
        return concave;
    }

    // 납작한 물체 바로 아래(0.3 m 이내)에 이미 충돌체가 있는 지면이 있으면 바닥에 깔린 것으로 본다(공중의 차양·컨베이어 판은 제외).
    private static bool IsLyingOnGround(Bounds b)
    {
        return Physics.Raycast(new Vector3(b.center.x, b.max.y + 0.05f, b.center.z), Vector3.down, out RaycastHit hit, b.size.y + FlatGroundGap + 0.05f, ~0, QueryTriggerInteraction.Ignore)
            && b.min.y - hit.point.y < FlatGroundGap;
    }

    // 나무·막대사탕처럼 바닥 1.2 m 높이의 단면이 전체 윤곽의 25% 미만이면 줄기에만 캡슐을 세운다 — 볼록 껍질은 가지·사탕 머리 아래
    // 빈 공간까지 막아 보이지 않는 벽이 된다(D2). 기울어진 물체는 제외(볼록 껍질로 처리).
    private static bool TryAddTrunkCapsule(MeshFilter mf, Bounds b)
    {
        Transform t = mf.transform;
        if (b.size.y < TrunkMinHeight || Vector3.Dot(t.up, Vector3.up) < 0.98f) return false;
        float fullArea = b.size.x * b.size.z;
        if (fullArea < 1f) return false;

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        int low = 0;
        foreach (Vector3 v in mf.sharedMesh.vertices)
        {
            Vector3 w = t.TransformPoint(v);
            if (w.y - b.min.y > TrunkSampleHeight) continue;
            low++;
            minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
            minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
        }
        if (low < 3 || (maxX - minX) * (maxZ - minZ) >= fullArea * TrunkMaxFootprint) return false;

        float scaleXZ = Mathf.Max(t.lossyScale.x, t.lossyScale.z), scaleY = t.lossyScale.y;
        float radius = Mathf.Max(0.15f, Mathf.Max(maxX - minX, maxZ - minZ) * 0.5f);
        var worldCenter = new Vector3((minX + maxX) * 0.5f, b.center.y, (minZ + maxZ) * 0.5f);
        var capsule = mf.gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.center = t.InverseTransformPoint(worldCenter);
        capsule.radius = radius / scaleXZ;
        capsule.height = Mathf.Max(b.size.y, radius * 2f) / scaleY;
        return true;
    }

    // 조립 뒤 확인: 플레이 영역 안 렌더러 중 충돌체가 없는 것(물·효과·바닥에 깔린 납작한 물체 제외)이 남으면 경고한다(F2).
    private static void ReportMissingColliders(Transform mapRoot, List<string> report)
    {
        Physics.SyncTransforms();
        var missing = new List<string>();
        foreach (MeshFilter mf in mapRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            Transform category = mf.transform;
            while (category.parent != null && category.parent != mapRoot) category = category.parent;
            if (category.name == "Water" || category.name == "Effects" || mf.GetComponent<Collider>() != null) continue;
            var r = mf.GetComponent<Renderer>();
            if (r == null || !r.enabled) continue;
            if (mf.name.StartsWith("CUR_CandyFerrisWheel_")) continue; // 높은 관람차 바퀴는 닿지 않음(GameScenePlan D11)
            Bounds b = r.bounds;
            if (Mathf.Abs(b.center.x) > MapHalfExtent || Mathf.Abs(b.center.z) > MapHalfExtent) continue;
            if (b.size.y < FlatHeight && IsLyingOnGround(b)) continue;
            missing.Add(mf.name);
        }
        report.Add($"colliders missing in play area: {missing.Count}" + (missing.Count > 0 ? $" [{Short(missing)}]" : ""));
        if (missing.Count > 0) Debug.LogWarning($"{LogTag} {missing.Count} renderers without colliders: {Short(missing)}");
    }

    private static readonly Dictionary<Mesh, bool> convexFitCache = new Dictionary<Mesh, bool>();

    // 삼각형 수로 거르면 껍질 면이 적은 고폴리 나무·집까지 오목으로 남는다. PhysX로 한 번 구워 보고 한도 초과 메시지가
    // 나왔는지로 판단한다(같은 메시는 캐시).
    private static bool ConvexHullFits(Mesh mesh)
    {
        if (mesh == null) return false;
        if (convexFitCache.TryGetValue(mesh, out bool fits)) return fits;
        bool failed = false;
        Application.LogCallback onLog = (condition, stack, type) => { if (condition.Contains("Convex Mesh")) failed = true; };
        Application.logMessageReceived += onLog;
        try { Physics.BakeMesh(mesh.GetInstanceID(), true); }
        finally { Application.logMessageReceived -= onLog; }
        convexFitCache[mesh] = !failed;
        return !failed;
    }

    // 움직이는 물체(kinematic)는 오목 충돌체도 허용되므로, 껍질이 한도를 넘으면 오목으로 둔다.
    private static void MakeConvexIfFits(MeshCollider mc)
    {
        if (mc != null) mc.convex = ConvexHullFits(mc.sharedMesh);
    }

    private static string Short(List<string> names) => string.Join(", ", names.Take(10)) + (names.Count > 10 ? " ..." : "");

    private static bool IsWalkOver(string name) =>
        name.Contains("Floor") || name.Contains("Ramp") || name.Contains("Stair") || name.Contains("Bridge")
        || name.Contains("Platform") || name.Contains("Terrain") || name.Contains("Road") || name.Contains("Path")
        || name.Contains("ClockTower"); // 시계탑은 1층 문으로 들어가는 속 빈 건물(EscapeVisualPlan.md §5.5) — convex면 문이 막힌다

    // 아치·정문처럼 "양쪽 가장자리는 막혀 있고 가운데로 지나갈 수 있는" 구조물인지. 경계 상자 가운데를 수평으로 가로지르는 광선은
    // 오목 충돌체를 통과하고, 양쪽 가장자리(폭의 8%·92%) 광선은 막혀야 한다 — 가지 사이로 광선이 빠지는 나무를 통과형으로 오판하지
    // 않게 한다. convex로 바꾸면 그 통로가 막히므로 오목 충돌체를 유지한다. 폭 2.5 m·높이 3 m 이상만 본다(쿠키 몸 기준).
    private static bool HasWalkThroughOpening(MeshCollider mc)
    {
        if (mc.sharedMesh == null) return false;
        Bounds b = mc.bounds;
        if (b.size.y < 3f) return false;
        foreach (float h in new[] { 1.0f, 2.0f })
        {
            float y = b.min.y + h;
            if (b.size.x >= 2.5f && b.size.z >= 1f && IsGateAlong(mc, b, y, alongZ: true)) return true;
            if (b.size.z >= 2.5f && b.size.x >= 1f && IsGateAlong(mc, b, y, alongZ: false)) return true;
        }
        return false;
    }

    private static bool IsGateAlong(MeshCollider mc, Bounds b, float y, bool alongZ)
    {
        Vector3 dir = alongZ ? Vector3.forward : Vector3.right;
        float length = (alongZ ? b.size.z : b.size.x) + 2f;
        Vector3 Origin(float t) => alongZ
            ? new Vector3(Mathf.Lerp(b.min.x, b.max.x, t), y, b.min.z - 1f)
            : new Vector3(b.min.x - 1f, y, Mathf.Lerp(b.min.z, b.max.z, t));
        return PassesThrough(mc, Origin(0.5f), dir, length)
            && !PassesThrough(mc, Origin(0.08f), dir, length)
            && !PassesThrough(mc, Origin(0.92f), dir, length);
    }

    private static bool PassesThrough(MeshCollider mc, Vector3 origin, Vector3 dir, float length)
    {
        // 앞·뒤 양방향 모두 맞지 않으면 통과(한쪽 면만 있는 판 오브젝트 오판 방지)
        bool hitForward = mc.Raycast(new Ray(origin, dir), out _, length);
        bool hitBackward = mc.Raycast(new Ray(origin + dir * length, -dir), out _, length);
        return !hitForward && !hitBackward;
    }

    private static void MarkStatic(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic
                | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.ContributeGI);
    }

    private static void ClearStatic(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
    }

    // ---------------- lighting (§3.5) ----------------

    [Serializable] private class LightingJson
    {
        public float[] ambient_color; public float ambient_intensity; public bool fog; public string fog_mode;
        public float[] fog_color; public float fog_density; public DirectionalJson directional; public PointJson[] point_lights;
    }
    [Serializable] private class DirectionalJson { public float[] color; public float intensity; public float euler_x; public float euler_y; }
    [Serializable] private class PointJson { public string name; public float[] pos_unity; public float[] color; public float range; public float intensity; public bool shadows; }

    private static LightingJson LoadLighting(string map) =>
        JsonUtility.FromJson<LightingJson>(File.ReadAllText($"{MapsRoot}/{map}/{map}_Lighting.json"));

    private static Color Rgb(float[] c, float scale = 1f) => new Color(c[0] * scale, c[1] * scale, c[2] * scale, 1f);

    private const float MaxPointIntensity = 2.5f;

    private static Color WithLuminance(Color c, float luminance)
    {
        float current = Mathf.Max(1e-4f, 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b);
        Color scaled = c * (luminance / current);
        scaled.a = 1f;
        return scaled;
    }

    private static void BuildLighting(string map, LightingJson json, List<string> report)
    {
        Mood mood = Moods[map];
        Transform root = NewRoot(RootLighting);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.transform.SetParent(root, false);
        sun.type = LightType.Directional;
        sun.color = Rgb(json.directional.color);
        sun.intensity = mood.Directional;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(json.directional.euler_x, json.directional.euler_y, 0f);
        RenderSettings.sun = sun;

        var points = new GameObject("PointLights").transform;
        points.SetParent(root, false);
        int shadowed = 0;
        foreach (PointJson p in json.point_lights)
        {
            var light = new GameObject(p.name).AddComponent<Light>();
            light.transform.SetParent(points, false);
            light.transform.position = new Vector3(p.pos_unity[0], p.pos_unity[1], p.pos_unity[2]);
            light.type = LightType.Point;
            light.color = Rgb(p.color);
            light.range = p.range;
            light.intensity = Mathf.Min(p.intensity, MaxPointIntensity); // Blender 키·랜드마크 조명(10~600)을 그대로 쓰면 화면과 실시간 반사 프로브가 하얗게 날아간다
            light.shadows = p.shadows ? LightShadows.Soft : LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            if (p.shadows) shadowed++;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = WithLuminance(Rgb(json.ambient_color), mood.Ambient);
        RenderSettings.fog = json.fog;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Rgb(json.fog_color);
        RenderSettings.fogDensity = mood.FogDensity;
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 1f;

        // 반사 프로브: 맵 전체 1개 + 물마다 1개(초콜릿 강 윤기, §3.5). 시작할 때 한 번 찍는다.
        var probes = new GameObject("ReflectionProbes").transform;
        probes.SetParent(root, false);
        AddProbe(probes, "Probe_Map", new Vector3(0f, 20f, 0f), new Vector3(360f, 120f, 360f), 0);
        int water = 0;
        GameObject waterRoot = GameObject.Find($"{RootMap}/Water");
        if (waterRoot != null)
            foreach (Renderer r in waterRoot.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                AddProbe(probes, $"Probe_{r.name}", b.center + Vector3.up * 3f, b.size + new Vector3(10f, 30f, 10f), 1);
                water++;
            }

        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.renderingPath = RenderingPath.DeferredShading; // 조명 수십~백여 개(D8)
            cam.allowMSAA = false;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor; // 안개와 이어지는 지평선
            cam.farClipPlane = 1000f;
        }
        report.Add($"lighting: sun {sun.intensity}, points {json.point_lights.Length} (shadowed {shadowed}), ambient lum {mood.Ambient} {RenderSettings.ambientLight}, fog {mood.FogDensity}, probes 1+{water}, camera Deferred");
    }

    private static void AddProbe(Transform parent, string name, Vector3 center, Vector3 size, int importance)
    {
        var probe = new GameObject(name).AddComponent<ReflectionProbe>();
        probe.transform.SetParent(parent, false);
        probe.transform.position = center;
        probe.size = size;
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.resolution = 128;
        probe.importance = importance;
        probe.boxProjection = importance > 0;
        probe.hdr = true;
    }

    // ---------------- post processing (§3.6) ----------------

    private static void BuildPostFx(string map, List<string> report)
    {
        Mood mood = Moods[map];
        string profilePath = $"{MapsRoot}/{map}/{map}_PostFX.asset";
        if (AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath) != null) AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);

        var bloom = profile.AddSettings<Bloom>();
        bloom.intensity.Override(mood.BloomIntensity);
        bloom.threshold.Override(mood.BloomThreshold);
        bloom.diffusion.Override(mood.BloomDiffusion);
        bloom.softKnee.Override(0.6f);

        var grading = profile.AddSettings<ColorGrading>();
        grading.gradingMode.Override(GradingMode.HighDefinitionRange);
        grading.tonemapper.Override(Tonemapper.ACES);
        grading.temperature.Override(mood.Temperature);
        grading.tint.Override(mood.Tint);
        grading.saturation.Override(mood.Saturation);
        grading.contrast.Override(mood.Contrast);

        if (mood.Vignette > 0f)
        {
            var vignette = profile.AddSettings<Vignette>();
            vignette.intensity.Override(mood.Vignette);
            vignette.smoothness.Override(0.45f);
        }
        foreach (PostProcessEffectSettings s in profile.settings)
        {
            s.name = s.GetType().Name;
            s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(s, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        int layer = LayerMask.NameToLayer("PostProcessing");
        var volumeGo = new GameObject(RootPostFx) { layer = layer < 0 ? 0 : layer };
        var volume = volumeGo.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 0;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);

        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            var postLayer = GetOrAdd<PostProcessLayer>(cam.gameObject);
            string resGuid = AssetDatabase.FindAssets("t:PostProcessResources").FirstOrDefault();
            if (resGuid != null) postLayer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(resGuid)));
            postLayer.volumeLayer = layer < 0 ? 0 : 1 << layer;
            postLayer.volumeTrigger = cam.transform;
            postLayer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        }
        report.Add($"postfx: bloom {mood.BloomIntensity}/{mood.BloomThreshold}, ACES, vignette {mood.Vignette} -> {profilePath}");
    }

    // 초콜릿 강·액체 윤기(§3.5, D13). 발광 재질은 이미 HDR 값(최대 5~6)이라 Bloom이 바로 잡는다 — 바꾸지 않는다.
    private static void ApplyMaterials(string map, Transform mapRoot, List<string> report)
    {
        ScaleEmission(map, mapRoot, report);
        int changed = 0;
        foreach (string name in new[] { "M_Chocolate_Liquid", "M_Water_Chocolate" })
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MapsRoot}/Common/Materials/{name}.mat");
            if (mat == null) continue;
            mat.SetFloat("_Glossiness", 0.92f);
            mat.SetFloat("_Metallic", 0.1f);
            EditorUtility.SetDirty(mat);
            changed++;
        }
        if (changed > 0) report.Add($"materials: chocolate gloss 0.92 x{changed}");
    }

    // 공용 발광 재질은 다른 맵도 쓰므로, 배율이 있는 맵에서는 맵 전용 사본(같은 경로 재사용 → GUID 유지)을 만들어 발광만 곱하고 끼운다.
    // 매 조립마다 원본 FBX에서 다시 배치하므로 렌더러는 늘 원본 재질에서 시작한다.
    private static void ScaleEmission(string map, Transform mapRoot, List<string> report)
    {
        Dictionary<string, float> scales = Moods[map].EmissionScale;
        if (scales == null) return;
        string folder = $"{MapsRoot}/{map}/Materials";
        EnsureFolder(folder);
        var copies = new Dictionary<Material, Material>();
        int swapped = 0;
        foreach (Renderer r in mapRoot.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                if (src == null || !src.IsKeywordEnabled("_EMISSION") || !src.HasProperty("_EmissionColor")) continue;
                float scale = EmissionScaleFor(scales, src.name);
                if (scale <= 0f) continue;
                if (!copies.TryGetValue(src, out Material copy))
                {
                    string path = $"{folder}/{src.name}_{map}.mat";
                    copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (copy == null) { copy = new Material(src); AssetDatabase.CreateAsset(copy, path); }
                    else copy.CopyPropertiesFromMaterial(src);
                    Color emission = src.GetColor("_EmissionColor");
                    copy.SetColor("_EmissionColor", new Color(emission.r * scale, emission.g * scale, emission.b * scale, emission.a));
                    EditorUtility.SetDirty(copy);
                    copies[src] = copy;
                }
                mats[i] = copy;
                changed = true;
            }
            if (changed) { r.sharedMaterials = mats; swapped++; }
        }
        AssetDatabase.SaveAssets();
        report.Add($"emission: {copies.Count} map copies ({string.Join(", ", copies.Keys.Select(m => $"{m.name} x{EmissionScaleFor(scales, m.name)}"))}), renderers {swapped}");
    }

    private static float EmissionScaleFor(Dictionary<string, float> scales, string materialName)
    {
        foreach (var kv in scales)
            if (materialName.StartsWith(kv.Key)) return kv.Value;
        return 0f;
    }

    // ---------------- animated props (§3.8) ----------------

    private static void BuildAnimated(string map, Transform mapRoot, List<string> report)
    {
        if (map == "CursedCandyCarnival")
        {
            Transform top = FindDeep(mapRoot, "CUR_Carousel_Top");
            if (top != null)
            {
                ClearStatic(top.gameObject);
                AnimationClip spin = WriteCarouselSpinClip();
                AttachLoopAnimator(top.gameObject, "Carousel_Top_Spin", spin, kinematic: true);
                // 받은 본체 + 목마 모델이 있으면 그것으로 바꾼다(Request1009Plan.md §29) — 없으면 예전 메시 그대로
                if (AssetDatabase.LoadAssetAtPath<GameObject>(CarouselModelSwap.ModelPath) != null) report.Add(CarouselModelSwap.Apply(top));
                var mc = top.GetComponent<MeshCollider>();
                if (mc != null && !mc.convex) MakeConvexIfFits(mc);
                report.Add($"carousel: {CarouselSecondsPerTurn}s/turn, convex {mc != null && mc.convex}");
            }

            Transform pivot = FindDeep(mapRoot, "CUR_CandyFerrisWheel_Pivot");
            if (pivot != null)
            {
                var gondolas = new List<string>();
                foreach (Transform t in mapRoot.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (t == pivot || !t.name.StartsWith("CUR_CandyFerrisWheel_") || t.name.Contains("_Leg") || t.name.Contains("_Pivot")) continue;
                    t.SetParent(pivot, true);
                    foreach (Collider c in t.GetComponents<Collider>()) Object.DestroyImmediate(c); // 높은 바퀴는 닿지 않음(D11)
                    if (t.name.Contains("_Gondola")) gondolas.Add(t.name);
                }
                ClearStatic(pivot.gameObject);
                AnimationClip spin = WriteSpinClip("FerrisWheel_Spin", "", "localEulerAnglesRaw.z", FerrisSecondsPerTurn, 1f, gondolas);
                AttachLoopAnimator(pivot.gameObject, "FerrisWheel_Spin", spin, kinematic: false);
                report.Add($"ferris wheel: {FerrisSecondsPerTurn}s/turn, parts {pivot.childCount}, gondolas {gondolas.Count} (counter-rotated)");
            }
        }

        if (map == "HauntedBakery")
        {
            // 마법 오븐 문은 아무나 열 수 없다(EscapeVisualPlan.md §5.3): 탈출 기계가 완성될 때 OvenSequence가 굴려서 연다.
            Transform door = FindDeep(mapRoot, "HAU_MagicOven_Door");
            if (door != null)
            {
                ClearStatic(door.gameObject);
                door.localRotation = Quaternion.identity; // 닫힘
                MakeConvexIfFits(door.GetComponent<MeshCollider>());
                report.Add("magic oven door: closed, opened only by the escape machine");
            }
        }
    }

    // 예전 빌드에서 GameplayProps 중 껍질 한도를 넘어 메시 경계 상자가 된 물체를 오목 MeshCollider로 되돌린다(맵을 다시 만들지 않고, §10).
    [MenuItem(MenuRoot + "Fix Prop Box Colliders")]
    public static void FixPropBoxCollidersMenu() => Debug.Log("[MapSceneBuilder] " + FixPropBoxColliders());

    public static string FixPropBoxColliders()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "cancelled";
        var report = new List<string>();
        foreach (string map in MapNames)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath(map), OpenSceneMode.Single);
            var fixedNames = new List<string>();
            foreach (BoxCollider box in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                var filter = box.GetComponent<MeshFilter>();
                // 메시가 있는 GameplayProps의 상자는 예전 빌드가 껍질 한도를 넘은 메시에만 만들었다(다시 구워 보면 경고만 남는다)
                if (box.isTrigger || filter == null || filter.sharedMesh == null || !UnderCategory(box.transform, "GameplayProps")) continue;
                GameObject go = box.gameObject;
                Object.DestroyImmediate(box);
                go.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                fixedNames.Add(go.name);
            }
            if (fixedNames.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            report.Add($"{map}: {fixedNames.Count} {Short(fixedNames)}");
        }
        return string.Join("; ", report);
    }

    private static bool UnderCategory(Transform t, string category)
    {
        for (; t != null; t = t.parent) if (t.name == category) return true;
        return false;
    }

    private static AnimationClip WriteCarouselSpinClip() =>
        WriteSpinClip("Carousel_Top_Spin", "", "localEulerAnglesRaw.y", CarouselSecondsPerTurn, CarouselSpinSign, null);

    // 맵을 다시 만들지 않고 회전목마 클립만 다시 쓴다(같은 에셋이라 씬의 Animator 연결은 그대로).
    [MenuItem(MenuRoot + "Fix Carousel Direction")]
    public static void FixCarouselDirection()
    {
        AnimationClip clip = WriteCarouselSpinClip();
        AssetDatabase.SaveAssets();
        Debug.Log($"[MapSceneBuilder] {clip.name}: 0 -> {360f * CarouselSpinSign} deg in {CarouselSecondsPerTurn}s");
    }

    // 한 바퀴(360°)를 secondsPerTurn에 도는 반복 클립. counterPaths의 자식은 같은 축으로 반대로 돌아 방향을 유지한다(관람차 곤돌라).
    private static AnimationClip WriteSpinClip(string name, string path, string property, float secondsPerTurn, float sign, List<string> counterPaths)
    {
        AnimationClip clip = LoadOrCreateClip(name);
        clip.ClearCurves();
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), LinearCurve(secondsPerTurn, 0f, 360f * sign));
        if (counterPaths != null)
            foreach (string child in counterPaths)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(child, typeof(Transform), property), LinearCurve(secondsPerTurn, 0f, -360f * sign));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationCurve LinearCurve(float length, float from, float to)
    {
        var curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(length, to));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    private static void AttachLoopAnimator(GameObject go, string name, AnimationClip clip, bool kinematic)
    {
        AnimatorController controller = RecreateController(name);
        AnimatorState state = controller.layers[0].stateMachine.AddState("Loop");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        var animator = GetOrAdd<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (kinematic)
        {
            // 움직이는 충돌체 — 캐릭터를 올바르게 밀어내도록 물리 스텝에 맞춰 돌린다(P8)
            animator.updateMode = AnimatorUpdateMode.Fixed;
            var body = GetOrAdd<Rigidbody>(go);
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    // ---------------- doors (§3.7, D3~D5) ----------------

    [Serializable] private class DoorsJson { public string map; public float leaf_thickness; public DoorJson[] doors; }
    [Serializable] private class DoorJson
    {
        public string id; public string wall; public bool lintel; public float[] center; public float[] along; public float[] inward;
        public float width; public float height; public float wall_thickness; public float leaf_width; public float leaf_height;
        public float bottom_gap; public float hinge_inset; public string leaf_left; public string leaf_right;
    }

    public const string DoorLeafLeft = "Leaf_L", DoorLeafRight = "Leaf_R";
    private const float DoorOpenAngle = 90f;

    private static void BuildDoors(string map, List<string> report)
    {
        string jsonPath = $"{MapsRoot}/{map}/{map}_Doors.json";
        if (!File.Exists(jsonPath)) return;
        var json = JsonUtility.FromJson<DoorsJson>(File.ReadAllText(jsonPath));
        string fbxPath = $"{MapsRoot}/{map}/Models/{map}_Doors.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
        if (importer == null) { Debug.LogError($"{LogTag} {fbxPath} missing"); return; }
        if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab || importer.addCollider || importer.importAnimation)
        {
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
        }
        importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
        importer.SaveAndReimport();

        var meshes = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Mesh>().ToDictionary(m => m.name);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var materials = new Dictionary<string, Material[]>();
        foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>(true))
            materials[r.GetComponent<MeshFilter>().sharedMesh.name] = r.sharedMaterials;

        AnimatorController controller = BuildDoubleDoorController();
        Transform root = NewRoot(RootDoors);
        int built = 0, skipped = 0;
        foreach (DoorJson d in json.doors)
        {
            if (!d.lintel) { skipped++; continue; } // 외곽 담장 입구(상인방 없음)는 열어 둔다(D4)
            if (!meshes.TryGetValue(d.leaf_left, out Mesh leftMesh) || !meshes.TryGetValue(d.leaf_right, out Mesh rightMesh))
            {
                Debug.LogError($"{LogTag} {d.id}: leaf mesh missing");
                continue;
            }
            Vector3 center = V(d.center), along = V(d.along).normalized, inward = V(d.inward).normalized;
            Quaternion rot = Quaternion.LookRotation(inward, Vector3.up); // 로컬 +Z = 방 안쪽
            Vector3 right = rot * Vector3.right;
            float hingeOffset = d.width / 2f - d.hinge_inset;

            var doorGo = new GameObject(d.id);
            doorGo.transform.SetParent(root, false);
            doorGo.transform.SetPositionAndRotation(center - right * hingeOffset + Vector3.up * d.bottom_gap, rot);
            CreateLeaf(doorGo.transform, DoorLeafLeft, leftMesh, materials, Vector3.zero, json.leaf_thickness);
            CreateLeaf(doorGo.transform, DoorLeafRight, rightMesh, materials, Vector3.right * (hingeOffset * 2f), json.leaf_thickness);

            SetupDoorBody(doorGo, controller);
            var interactable = doorGo.AddComponent<InteractableDoor>();
            var so = new SerializedObject(interactable);
            so.FindProperty("doorId").stringValue = d.id;
            so.FindProperty("inwardOpenAngle").floatValue = -DoorOpenAngle; // 왼쪽 문짝(+X로 뻗음)이 -90°로 돌면 +Z(안쪽)로 간다
            so.FindProperty("leafCollider").objectReferenceValue = doorGo.transform.Find(DoorLeafLeft).GetComponent<BoxCollider>();
            so.FindProperty("interactionRange").floatValue = d.width / 2f + 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            built++;
        }
        report.Add($"doors: {built} double doors (skipped {skipped} without lintel)");
        ClearDoorSwings(root, report);
    }

    private static readonly float[] SwingSampleAngles = { 30f, 60f, 90f };
    private const float SwingPushStep = 0.25f, SwingPushMax = 4f;

    // 문짝이 양방향 0~90°로 돌 때 쓸고 지나가는 자리에 소품(기계·의자 등)이 있으면 문 밖(열리는 쪽 멀리)으로 밀어낸다.
    // 문 컨트롤러는 모든 문이 함께 쓰므로 문마다 각도를 줄이는 대신 소품을 옮긴다. 구조물이 걸리면 옮기지 않고 보고만 한다.
    private static void ClearDoorSwings(Transform doorsRoot, List<string> report)
    {
        Physics.SyncTransforms();
        var moved = new List<string>();
        var blocked = new List<string>();
        foreach (InteractableDoor door in doorsRoot.GetComponentsInChildren<InteractableDoor>())
        {
            var own = new HashSet<Collider>(door.GetComponentsInChildren<Collider>());
            for (int guard = 0; guard < 8; guard++)
            {
                if (!FindSwingBlocker(door, own, out Collider blocker, out float side)) break;
                Transform prop = MovableProp(blocker.transform);
                if (prop == null) { blocked.Add($"{door.name}<-{blocker.name}"); break; }

                Vector3 push = door.transform.forward * side;
                float moved01 = 0f;
                while (moved01 < SwingPushMax && SwingOverlaps(door, own, blocker))
                {
                    prop.position += push * SwingPushStep;
                    moved01 += SwingPushStep;
                    Physics.SyncTransforms();
                }
                moved.Add($"{prop.name} {moved01:0.##}m ({door.name})");
            }
        }
        report.Add($"door swings: props moved {moved.Count} [{string.Join(", ", moved)}], structure blockers {blocked.Count} [{string.Join(", ", blocked)}]");
    }

    private static bool FindSwingBlocker(InteractableDoor door, HashSet<Collider> own, out Collider blocker, out float side)
    {
        foreach (float dir in new[] { 1f, -1f }) // +1 = 안쪽으로 열림
            foreach (Collider c in SwingHits(door, own, dir))
            {
                blocker = c;
                side = dir;
                return true;
            }
        blocker = null;
        side = 0f;
        return false;
    }

    private static bool SwingOverlaps(InteractableDoor door, HashSet<Collider> own, Collider target) =>
        SwingHits(door, own, 1f).Contains(target) || SwingHits(door, own, -1f).Contains(target);

    private static IEnumerable<Collider> SwingHits(InteractableDoor door, HashSet<Collider> own, float dir)
    {
        foreach (string leafName in new[] { DoorLeafLeft, DoorLeafRight })
        {
            Transform leaf = door.transform.Find(leafName);
            var box = leaf.GetComponent<BoxCollider>();
            float sign = leafName == DoorLeafLeft ? -1f : 1f; // 왼쪽 -90°·오른쪽 +90°가 안쪽
            foreach (float a in SwingSampleAngles)
            {
                Quaternion rot = door.transform.rotation * Quaternion.Euler(0f, sign * dir * a, 0f);
                Vector3 center = leaf.position + rot * box.center;
                Vector3 half = box.size * 0.5f - Vector3.one * 0.06f; // 문틀·바닥에 스치는 정도는 무시
                foreach (Collider c in Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore))
                    if (!own.Contains(c) && c.GetComponentInParent<InteractableDoor>() == null) yield return c;
            }
        }
    }

    // 소품·장식 분류 아래의 최상위 오브젝트(가져온 FBX의 한 물체)만 옮긴다.
    private static Transform MovableProp(Transform t)
    {
        while (t.parent != null && t.parent.parent != null && t.parent.parent.name != RootMap) t = t.parent;
        if (t.parent == null || t.parent.parent == null) return null;
        string category = t.parent.name;
        return category == "GameplayProps" || category == "Decoration" ? t : null;
    }

    private static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

    private static void CreateLeaf(Transform parent, string name, Mesh mesh, Dictionary<string, Material[]> materials, Vector3 localPos, float thickness)
    {
        var leaf = new GameObject(name);
        leaf.transform.SetParent(parent, false);
        leaf.transform.localPosition = localPos;
        leaf.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = leaf.AddComponent<MeshRenderer>();
        if (materials.TryGetValue(mesh.name, out Material[] mats)) renderer.sharedMaterials = mats;
        // 판 부분만 덮는 상자(철띠·손잡이 돌출은 제외) — 규칙상 convex(상자)
        Bounds b = mesh.bounds;
        var box = leaf.AddComponent<BoxCollider>();
        box.center = new Vector3(b.center.x, b.center.y, 0f);
        box.size = new Vector3(b.size.x, b.size.y, thickness);
    }

    private static void SetupDoorBody(GameObject go, AnimatorController controller)
    {
        var animator = GetOrAdd<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // 화면 밖에서도 문 충돌체가 움직여야 한다
        animator.updateMode = AnimatorUpdateMode.Fixed;           // kinematic 문짝을 물리 스텝에 맞춰 움직인다
        var body = GetOrAdd<Rigidbody>(go);
        body.isKinematic = true;
        body.useGravity = false;
    }

    // 모든 두 장 문이 같은 로컬 구조(Leaf_L은 +X, Leaf_R은 -X로 뻗고 +Z가 방 안쪽)라 컨트롤러 하나를 함께 쓴다.
    private static AnimatorController BuildDoubleDoorController()
    {
        AnimationClip open = WriteDoubleDoorClip("MapDoor_Open", 0f, -DoorOpenAngle, true);
        AnimationClip close = WriteDoubleDoorClip("MapDoor_Close", -DoorOpenAngle, 0f, false);
        AnimationClip openOut = WriteDoubleDoorClip("MapDoor_OpenOut", 0f, DoorOpenAngle, true);
        AnimationClip closeOut = WriteDoubleDoorClip("MapDoor_CloseOut", DoorOpenAngle, 0f, false);

        AnimatorController controller = RecreateController("MapDoor");
        controller.AddParameter(InteractableDoor.DefaultOpenParameter, AnimatorControllerParameterType.Bool);
        controller.AddParameter(InteractableDoor.DefaultOutwardParameter, AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState closed = AddState(sm, InteractableDoor.ClosedStateName, null);
        AnimatorState opening = AddState(sm, InteractableDoor.OpenStateName, open);
        AnimatorState closing = AddState(sm, "Close", close);
        AnimatorState openingOut = AddState(sm, InteractableDoor.OpenOutwardStateName, openOut);
        AnimatorState closingOut = AddState(sm, "CloseOut", closeOut);
        sm.defaultState = closed;
        foreach (AnimatorState from in new[] { closed, closing, closingOut })
        {
            AddTransition(from, opening, true, false);
            AddTransition(from, openingOut, true, true);
        }
        AddTransition(opening, closing, false, null);
        AddTransition(openingOut, closingOut, false, null);
        return controller;
    }

    // 문짝 두 장: 왼쪽은 angle, 오른쪽은 -angle.
    private static AnimationClip WriteDoubleDoorClip(string name, float from, float to, bool easeOut)
    {
        AnimationClip clip = LoadOrCreateClip(name);
        clip.ClearCurves();
        const float length = 1.0f; // 큰 문(폭 3.5~7 m)이라 과자집 문보다 느리게
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(DoorLeafLeft, typeof(Transform), "localEulerAnglesRaw.y"), DoorCurve(length, from, to, easeOut));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(DoorLeafRight, typeof(Transform), "localEulerAnglesRaw.y"), DoorCurve(length, -from, -to, easeOut));
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip WriteDoorAngleClip(string name, string path, float from, float to, float length, bool easeOut)
    {
        AnimationClip clip = LoadOrCreateClip(name);
        clip.ClearCurves();
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.y"), DoorCurve(length, from, to, easeOut));
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationCurve DoorCurve(float length, float from, float to, bool easeOut) =>
        easeOut
            ? new AnimationCurve(new Keyframe(0f, from, 0f, 1.8f * (to - from) / length), new Keyframe(length, to, 0f, 0f))
            : AnimationCurve.EaseInOut(0f, from, length, to);

    private static AnimatorController WriteSingleDoorController(string name, AnimationClip open, AnimationClip close)
    {
        AnimatorController controller = RecreateController(name);
        controller.AddParameter(InteractableDoor.DefaultOpenParameter, AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState closed = AddState(sm, InteractableDoor.ClosedStateName, null);
        AnimatorState opening = AddState(sm, InteractableDoor.OpenStateName, open);
        AnimatorState closing = AddState(sm, "Close", close);
        sm.defaultState = closed;
        AddTransition(closed, opening, true, null);
        AddTransition(closing, opening, true, null);
        AddTransition(opening, closing, false, null);
        return controller;
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion)
    {
        AnimatorState state = sm.AddState(name);
        state.motion = motion;
        state.writeDefaultValues = false;
        return state;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, bool open, bool? outward)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.AddCondition(open ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, InteractableDoor.DefaultOpenParameter);
        if (outward.HasValue)
            t.AddCondition(outward.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, InteractableDoor.DefaultOutwardParameter);
    }

    // ---------------- ground (§3.4) ----------------

    private const float GridStep = 2f;
    private const float HoleTolerance = 0.3f;

    // 보이는 지면(지형·배경 지형·바닥재 렌더러)과 밟히는 지면(실제 충돌체)을 격자로 비교해, 밟을 수 없는 보이는 지면에
    // 같은 메시의 non-convex MeshCollider를 붙인다(GameLobbyScene.md §9와 같은 방식). 물 표면은 제외하고 물 아래 바닥이
    // 밟히는지만 본다(D12).
    private static void FixGround(Transform mapRoot, List<string> report)
    {
        Transform fixRoot = NewRoot(RootColliders);
        var candidates = new List<MeshFilter>();
        foreach (MeshFilter mf in mapRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
            string category = CategoryOf(mf.transform, mapRoot);
            bool terrainLike = mf.name.Contains("Terrain") || mf.name.Contains("Ground") || mf.name.Contains("Floor");
            if ((category == "Background" || category == "Decoration" || category == "Terrain") && terrainLike) candidates.Add(mf);
        }

        var added = new List<string>();
        int holes = 0;
        foreach (MeshFilter mf in candidates)
        {
            var probe = mf.gameObject.AddComponent<MeshCollider>();
            probe.sharedMesh = mf.sharedMesh;
            Physics.SyncTransforms();
            int localHoles = CountHoles(probe, out int samples);
            Object.DestroyImmediate(probe);
            if (localHoles == 0) continue;
            holes += localHoles;
            var fix = new GameObject($"COLFIX_{mf.name}");
            fix.transform.SetParent(fixRoot, false);
            fix.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
            fix.transform.localScale = mf.transform.lossyScale;
            fix.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; // 넓은 정적 걷는 지형 → non-convex
            GameObjectUtility.SetStaticEditorFlags(fix, StaticEditorFlags.BatchingStatic);
            added.Add($"{mf.name}({localHoles}/{samples})");
        }
        Physics.SyncTransforms();
        report.Add($"ground: candidates {candidates.Count}, holes {holes}, colliders added {added.Count} [{string.Join(", ", added)}]");
    }

    private static string CategoryOf(Transform t, Transform mapRoot)
    {
        while (t != null && t.parent != mapRoot) t = t.parent;
        return t != null ? t.name : string.Empty;
    }

    // 이 보이는 면 위의 격자점 중, 실제 충돌체로는 밟히지 않는(없거나 0.3 m 넘게 아래) 점의 수.
    private static int CountHoles(MeshCollider visible, out int samples)
    {
        samples = 0;
        int holes = 0;
        Bounds b = visible.bounds;
        float x0 = Mathf.Max(b.min.x, -MapHalfExtent), x1 = Mathf.Min(b.max.x, MapHalfExtent);
        float z0 = Mathf.Max(b.min.z, -MapHalfExtent), z1 = Mathf.Min(b.max.z, MapHalfExtent);
        for (float x = Mathf.Ceil(x0 / GridStep) * GridStep; x <= x1; x += GridStep)
            for (float z = Mathf.Ceil(z0 / GridStep) * GridStep; z <= z1; z += GridStep)
            {
                var ray = new Ray(new Vector3(x, b.max.y + 5f, z), Vector3.down);
                if (!visible.Raycast(ray, out RaycastHit top, b.size.y + 10f)) continue;
                samples++;
                visible.enabled = false;
                bool solid = Physics.Raycast(ray, out RaycastHit walk, b.size.y + 30f, ~0, QueryTriggerInteraction.Ignore);
                visible.enabled = true;
                if (!solid || walk.point.y < top.point.y - HoleTolerance) holes++;
            }
        return holes;
    }

    // ---------------- spawns & kill zone (§3.9) ----------------

    private static void PlaceSpawnsAndKillZone(List<string> report)
    {
        Vector3 cookie = FindSpawn(new Vector3(0f, 0f, -90f), 2.5f);
        Vector3 monster = FindSpawn(new Vector3(0f, 0f, 90f), 2.5f);
        if ((monster - cookie).magnitude < 60f) monster = FindSpawn(-cookie.normalized * 90f, 2.5f);
        SetPosition(SceneSpawnPoints.Cookie, cookie);
        SetPosition(SceneSpawnPoints.Monster, monster);

        float lowest = LowestGround();
        var kill = GameObject.Find("VoidKillZone");
        if (kill != null)
        {
            kill.transform.position = new Vector3(0f, lowest - 10f, 0f);
            var box = kill.GetComponent<BoxCollider>();
            if (box != null) { box.size = new Vector3(360f, 4f, 360f); box.center = Vector3.zero; box.isTrigger = true; }
        }
        report.Add($"spawns: cookie {cookie:F1}, monster {monster:F1} (distance {(monster - cookie).magnitude:F0} m), kill zone y {lowest - 10f:F1}");
    }

    private static void SetPosition(string name, Vector3 position)
    {
        var go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        go.transform.SetParent(null);
        go.transform.position = position;
    }

    // 목표점에서 가까운 순으로, 평평하고(법선 y ≥ 0.95) 반경 안이 비어 있는 걷는 지면 점을 찾는다.
    private static Vector3 FindSpawn(Vector3 target, float clearRadius)
    {
        for (float r = 0f; r <= 120f; r += 4f)
        {
            int steps = r <= 0f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r / 4f);
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                var p = new Vector3(target.x + r * Mathf.Cos(a), 0f, target.z + r * Mathf.Sin(a));
                if (Mathf.Abs(p.x) > MapHalfExtent - 10f || Mathf.Abs(p.z) > MapHalfExtent - 10f) continue;
                if (!Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.95f || !IsGround(hit.collider)) continue;
                Vector3 feet = hit.point;
                // 지면에서 0.3 m 띄운 구 — 소품·벽·트리거가 없어야 한다(실제 흩뿌림은 실행 중 SpawnPositionFinder가 다시 피한다)
                if (Physics.CheckSphere(feet + Vector3.up * (clearRadius + 0.3f), clearRadius, ~0, QueryTriggerInteraction.Collide)) continue;
                return feet;
            }
        }
        Debug.LogWarning($"{LogTag} No clear spawn near {target}. Using target.");
        return target;
    }

    private static bool IsGround(Collider c)
    {
        Transform t = c.transform;
        while (t.parent != null && t.parent.name != RootMap && t.parent.name != RootColliders) t = t.parent;
        return t.name == "Ground" || t.name == "Terrain" || t.parent != null && t.parent.name == RootColliders;
    }

    private static float LowestGround()
    {
        float lowest = 0f;
        var ground = GameObject.Find($"{RootMap}/Ground");
        if (ground != null)
            foreach (Renderer r in ground.GetComponentsInChildren<Renderer>(true))
                if (!r.name.StartsWith("COL_Boundary")) lowest = Mathf.Min(lowest, r.bounds.min.y);
        return lowest;
    }

    // ---------------- build settings & map list ----------------

    public static void ApplyBuildSettingsAndMapList()
    {
        // 테스트 씬(PlayerTestScene)과 로비 씬들의 항목·활성 여부는 그대로 두고 맵 씬만 다시 채운다.
        var scenes = EditorBuildSettings.scenes.Where(s => !s.path.StartsWith(SceneFolder)).ToList();
        foreach (string map in MapNames)
            if (File.Exists(ScenePath(map))) scenes.Add(new EditorBuildSettingsScene(ScenePath(map), true));
        EditorBuildSettings.scenes = scenes.ToArray();

        var settings = Resources.Load<GameSettingsSO>("GameSettings");
        if (settings != null)
        {
            var so = new SerializedObject(settings);
            var list = so.FindProperty("gameMapScenes");
            var names = MapNames.Where(m => File.Exists(ScenePath(m))).Select(SceneName).ToArray();
            list.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) list.GetArrayElementAtIndex(i).stringValue = names[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
        Debug.Log($"{LogTag} Build scenes: {string.Join(", ", EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)))}");
    }

    // ---------------- helpers ----------------

    private static AnimationClip LoadOrCreateClip(string name)
    {
        EnsureFolder(AnimFolder);
        string path = $"{AnimFolder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null) return clip;
        clip = new AnimationClip { name = name };
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // 컨트롤러는 GUID를 유지한 채 내용을 비우고 다시 채운다 — 지웠다 다시 만들면 메모리의 참조가 끊겼다(Bug-fix-plan.md §35.8).
    private static AnimatorController RecreateController(string name)
    {
        EnsureFolder(DoorAssetFolder);
        string path = $"{DoorAssetFolder}/{name}.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) return AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (AnimatorControllerParameter p in controller.parameters) controller.RemoveParameter(p);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
        return controller;
    }

    // GetComponent는 없을 때 Unity의 "가짜 null"을 돌려주므로 ?? 대신 이 함수를 쓴다.
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            Transform r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
