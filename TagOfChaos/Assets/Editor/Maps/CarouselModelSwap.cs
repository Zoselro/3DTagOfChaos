using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 저주받은 놀이공원 회전목마 교체(Request1009Plan.md §29, 단계 K2).
// 받은 본체 + 목마 16마리를 Blender에서 조립한 CUR_Carousel.fbx(Source~/Scripts/cur_carousel_build.py)를 CUR_Carousel_Top에 붙인다:
// Top의 옛 메시 겉모습은 지우고 새 모델을 자식 "Model"로 — Top의 Animator(돌기)·키네마틱 Rigidbody는 그대로라 함께 돈다.
// 충돌은 FBX의 COL_Carousel(본체를 줄인 메시)을 볼록 MeshCollider로(지금처럼 들어갈 수 없는 단단한 놀이기구).
// MapSceneBuilder.BuildAnimated도 이 도구를 불러 맵을 다시 만들어도 유지된다.
public static class CarouselModelSwap
{
    public const string TopName = "CUR_Carousel_Top";
    public const string ModelChild = "Model";
    public const string Folder = "Assets/Maps/CursedCandyCarnival/Models/Carousel";
    public const string ModelPath = Folder + "/CUR_Carousel.fbx";
    private const string CollisionName = "COL_Carousel";
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";
    private static readonly string[] Parts = { "CarouselBody", "CarouselHorse" }; // 텍스처·재질 이름(렌더러 CarouselHorses는 CarouselHorse 재질)

    [MenuItem(MenuRoot + "Swap Carousel Model (Cursed Candy Carnival)")]
    public static void SwapMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath("CursedCandyCarnival"), OpenSceneMode.Single);
        Transform map = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Map");
        Transform top = map != null ? map.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == TopName) : null;
        Debug.Log("[CarouselModelSwap] " + Apply(top));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // CUR_Carousel_Top 하나를 바꾼다(여러 번 불러도 같은 결과).
    public static string Apply(Transform top)
    {
        if (top == null) return "no " + TopName;
        GameObject model = Import();
        if (model == null) return "model missing: " + ModelPath;

        foreach (Transform old in top.Cast<Transform>().Where(t => t.name == ModelChild).ToList()) Object.DestroyImmediate(old.gameObject);
        var renderer = top.GetComponent<MeshRenderer>();
        if (renderer != null) Object.DestroyImmediate(renderer);
        var filter = top.GetComponent<MeshFilter>();
        if (filter != null) Object.DestroyImmediate(filter);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, top);
        instance.name = ModelChild;
        instance.transform.localPosition = Vector3.zero; // 받침 바닥 = Top 원점(옛 바닥 원판 밑면)
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        Mesh collision = null;
        foreach (MeshFilter mf in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.name == CollisionName) { collision = mf.sharedMesh; continue; }
            var r = mf.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = MaterialFor(mf.name);
        }
        Transform col = instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == CollisionName);
        if (col != null)
        {
            // 프리팹 인스턴스 안 오브젝트는 지울 수 없다 — 겉모습만 끈다
            var r = col.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }
        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0); // 도는 물체

        var mc = top.GetComponent<MeshCollider>() ?? top.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = collision;
        mc.convex = true;
        int tris = instance.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name != CollisionName).Sum(m => m.sharedMesh.triangles.Length / 3);
        return $"{TopName}: model {instance.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled)} renderers, {tris} tris, collider {(collision != null ? collision.name : "none")} convex";
    }

    private static GameObject Import()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) return null;
        bool changed = false;
        void Set<T>(T current, T wanted, System.Action<T> apply)
        {
            if (Equals(current, wanted)) return;
            apply(wanted);
            changed = true;
        }
        Set(importer.materialImportMode, ModelImporterMaterialImportMode.None, v => importer.materialImportMode = v);
        Set(importer.animationType, ModelImporterAnimationType.None, v => importer.animationType = v);
        Set(importer.importAnimation, false, v => importer.importAnimation = v);
        Set(importer.importCameras, false, v => importer.importCameras = v);
        Set(importer.importLights, false, v => importer.importLights = v);
        Set(importer.addCollider, false, v => importer.addCollider = v);
        Set(importer.bakeAxisConversion, true, v => importer.bakeAxisConversion = v);
        if (changed) importer.SaveAndReimport();
        foreach (string part in Parts)
        {
            ConfigureTexture($"{Folder}/{part}_albedo.png", false);
            ConfigureTexture($"{Folder}/{part}_normal.png", true);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
    }

    private static void ConfigureTexture(string path, bool normal)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType == type && ti.maxTextureSize == 2048 && ti.anisoLevel == 4) return;
        ti.textureType = type;
        ti.maxTextureSize = 2048;
        ti.anisoLevel = 4;
        ti.mipmapEnabled = true;
        ti.SaveAndReimport();
    }

    private static Material MaterialFor(string rendererName)
    {
        string part = Parts.FirstOrDefault(p => rendererName.StartsWith(p)) ?? Parts[0];
        string path = $"{Folder}/M_CUR_{part}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard")) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{part}_albedo.png");
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{part}_normal.png"));
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_Glossiness", 0.25f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
