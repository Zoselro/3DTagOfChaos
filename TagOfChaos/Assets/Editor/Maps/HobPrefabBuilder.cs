using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Heaven Of Bakery 모델 → 프리팹(Request1009Plan.md §89 HB5). 이름으로 충돌체를 정한다:
// 바닥·고원·경사로·계단·다리·건물·탈출구 = Mesh Collider(판 시작 때 NavMesh 다시 굽기에 메시 읽기 필요 → Read/Write 켬),
// 밀·구름·바닥 타일 = 없음, 나머지 소품 = 범위 상자 하나. 밀은 LODGroup(LOD0 낟알 / LOD1 / 사라짐) + 흔들림 셰이더 재질.
public static class HobPrefabBuilder
{
    public const string ModelDir = "Assets/09. Environment/HeavenOfBakery/Models";
    public const string PrefabDir = "Assets/04. Prefabs/Environment/HeavenOfBakery";
    public const string MaterialDir = "Assets/09. Environment/HeavenOfBakery/Materials";
    public const string WheatPrefab = PrefabDir + "/HOB_Wheat.prefab";

    private static readonly string[] MeshColliderPrefixes =
    {
        "HOB_Island_", "HOB_Plateau_", "HOB_CliffWall", "HOB_WaterfallCliff", "HOB_SpiralRamp", "HOB_Stairs", "HOB_Bridge", "HOB_RoundPlaza",
        "HOB_TaggerGazebo", "HOB_ExitPortal", "HOB_House_", "HOB_Bld_", "HOB_ChocoFountain", "HOB_CandyArch", "HOB_WoodFootbridge", "HOB_MarketStall", "HOB_Gate",
    };
    private static readonly string[] NoColliderPrefixes = { "HOB_Wheat", "HOB_Cloud_", "HOB_Tile_", "HOB_Bld_FlourMill_Blades" };

    [MenuItem("Tools/TagOfChaos/Maps/Heaven Of Bakery/Build Prefabs")]
    public static void BuildMenu() => Debug.Log(BuildAll());

    public static bool UsesMeshCollider(string name) => MeshColliderPrefixes.Any(name.StartsWith);
    public static bool NoCollider(string name) => NoColliderPrefixes.Any(name.StartsWith);

    public static string BuildAll()
    {
        EnsureFolder(PrefabDir); EnsureFolder(MaterialDir);
        int mesh = 0, box = 0, none = 0;
        foreach (string orig in Directory.GetFiles(ModelDir, "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
        {
            string path = orig;
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("HOB_WheatClump_")) continue;   // 밀은 LOD 프리팹 하나로(아래)
            string hq = $"{HobHQBuilder.ModelDir}/{name}.fbx";   // §89 품질 개선: 고품질 모델이 있으면 그것으로(재질은 HobHQBuilder가 이어 둠)
            bool isHq = File.Exists(hq);
            if (isHq) path = hq;
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            bool wantRead = UsesMeshCollider(name);
            if (!isHq && (imp.isReadable != wantRead || imp.importAnimation || imp.animationType != ModelImporterAnimationType.None))
            {
                imp.isReadable = wantRead; imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None;
                imp.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // FBX 뿌리는 X −90°(Blender Z 위 → Unity Y 위)로 서 있다 — 맵 빌더가 뿌리 회전을 바꿔도 눕지 않게 빈 뿌리 아래 자식으로 둔다.
            var go = new GameObject(name);
            var inner = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
            PrefabUtility.UnpackPrefabInstance(inner, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inner.name = "Model";
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = name.StartsWith("HOB_Cloud_") ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            if (NoCollider(name)) none++;
            else if (UsesMeshCollider(name))
            {
                foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true)) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                mesh++;
            }
            else
            {
                Bounds b = LocalBounds(go.transform);
                var c = go.AddComponent<BoxCollider>(); c.center = b.center; c.size = b.size;
                box++;
            }
            // 정적 표시는 맵 빌더가 고정된 부분에만 준다(서브 맵 묶음은 판 시작 때 옮겨지므로 정적 일괄 그리기에 넣으면 안 된다).
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
        }
        string wheat = BuildWheat();
        AssetDatabase.SaveAssets();
        return $"[HobPrefabs] mesh colliders {mesh}, box {box}, none {none} | {wheat}";
    }

    // 밀 포기 하나: LOD0(가까이, 낟알 하나하나) / LOD1(가벼움) / 멀면 그리지 않음. 재질 = 흔들림 셰이더(원래 색 그대로).
    private static string BuildWheat()
    {
        var root = new GameObject("HOB_Wheat");
        Shader sway = Shader.Find("TagOfChaos/WheatSway");
        var renderers = new Renderer[2][];
        for (int lod = 0; lod < 2; lod++)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/HOB_WheatClump_LOD{lod}.fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "LOD" + lod;
            renderers[lod] = go.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers[lod])
            {
                r.sharedMaterials = r.sharedMaterials.Select(m => SwayMaterial(m, sway)).ToArray();
                r.shadowCastingMode = lod == 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        var group = root.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(0.12f, renderers[0]), new LOD(0.015f, renderers[1]) });
        group.RecalculateBounds();
        PrefabUtility.SaveAsPrefabAsset(root, WheatPrefab);
        Object.DestroyImmediate(root);
        return "wheat LOD prefab";
    }

    private static Material SwayMaterial(Material source, Shader sway)
    {
        string path = $"{MaterialDir}/M_{source.name}_Sway.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sway); AssetDatabase.CreateAsset(m, path); }
        m.shader = sway;
        m.SetColor("_Color", source.color);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Bounds LocalBounds(Transform t)
    {
        var b = new Bounds(); bool first = true;
        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds w = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = t.InverseTransformPoint(mf.transform.TransformPoint(new Vector3(i % 2 == 0 ? w.min.x : w.max.x, i / 2 % 2 == 0 ? w.min.y : w.max.y, i / 4 == 0 ? w.min.z : w.max.z)));
                if (first) { b = new Bounds(c, Vector3.zero); first = false; } else b.Encapsulate(c);
            }
        }
        return b;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
