using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 초콜릿 공장 건물 확인용 씬(Request1009Plan.md §41, 단계 U2). Test_ChocolateFactory(놀 수 있는 테스트 맵: 관리자·카메라·스폰)를 복사해
// 놀이 공간 밖(+X 쪽)에 건물 8개를 두 줄로 세우고(앞문이 가운데 길을 본다), 쿠키·괴물 스폰을 길 가운데로 옮긴다.
// 실제 맵 배치는 맵 작업 때 한다. 여러 번 실행해도 같은 결과(씬을 다시 복사한다).
public static class ChocolateFactoryBuildingsScene
{
    public const string ScenePath = TestMapBuilder.SceneFolder + "/Test_ChocolateFactoryBuildings.unity";
    public const string RootName = "CHO_BuildingsPreview";
    public static readonly Vector3 Origin = new Vector3(320f, 0f, 0f);
    private const float Spacing = 46f;     // 건물 가로 37 m + 사이 길 9 m
    private const float RowOffset = 32f;   // 건물 앞면이 길 가운데에서 13.5 m 이상(가운데 길 폭 약 27 m)
    private const float GroundHalfX = 110f, GroundHalfZ = 60f;

    [MenuItem("Tools/TagOfChaos/Maps/Build Chocolate Factory Buildings Test Scene")]
    public static void BuildMenu() => Debug.Log("[ChocolateFactoryBuildingsScene] " + Build());

    public static string Build()
    {
        string source = TestMapBuilder.ScenePath("ChocolateFactory");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(source) == null) return "missing " + source;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(source, ScenePath)) return "copy failed";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = Origin;
        AddGround(root.transform);

        var placed = new List<string>();
        string[] keys = ChocolateFactoryAssets.AllBuildings.ToArray();
        for (int i = 0; i < keys.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChocolateFactoryAssets.PrefabPath(keys[i]));
            if (prefab == null) { placed.Add(keys[i] + " missing"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            bool north = i < 4;                                       // 위 줄은 돌려서 앞문(+Z)이 길(−Z 쪽)을 본다
            go.transform.localPosition = new Vector3((i % 4 - 1.5f) * Spacing, 0f, north ? RowOffset : -RowOffset);
            go.transform.localRotation = Quaternion.Euler(0f, north ? 180f : 0f, 0f);
            placed.Add(keys[i]);
        }

        int props = PlaceProps(root.transform);
        MoveSpawn(scene, SceneSpawnPoints.Cookie, Origin + new Vector3(-1.5f * Spacing, 0.5f, 0f));
        MoveSpawn(scene, SceneSpawnPoints.Monster, Origin + new Vector3(-1.5f * Spacing + 6f, 0.5f, 0f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{ScenePath}: {string.Join(", ", placed)}, props {props}";
    }

    // §40 소품: 길 양쪽 가로등·벤치·깃발, 건물 앞문 옆 상자·자루·카트 무리(문 앞 통로 ±5 m는 비운다)
    private static int PlaceProps(Transform root)
    {
        var parent = new GameObject("Props").transform;
        parent.SetParent(root, false);
        int n = 0;
        void Put(string name, float x, float z, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChocolateFactoryAssets.PropPath(name));
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = new Vector3(x, 0f, z);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            n++;
        }
        for (int i = 0; i < 4; i++)
        {
            float bx = (i - 1.5f) * Spacing;
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side * 8f;
                Put("CHO_StreetLamp", bx - Spacing / 2f, z, 0f);
                Put("CHO_Bench", bx + 9f, z, side > 0 ? 180f : 0f);
                Put("CHO_FlagStand", bx - 9f, z, side > 0 ? 180f : 0f);
                Put(i % 2 == 0 ? "CHO_Crate" : "CHO_CacaoCrate", bx + 12f, side * 9.5f, 15f * i);
                Put("CHO_IngredientSack", bx + 13.3f, side * 8.7f, 40f * i);
                Put(i % 2 == 0 ? "CHO_Cart" : "CHO_Pipe", bx - 13f, side * 9.5f, side > 0 ? 180f : 0f);
            }
        }
        Put("CHO_StreetLamp", 2f * Spacing, 8f, 0f);
        Put("CHO_StreetLamp", 2f * Spacing, -8f, 0f);
        for (int k = -3; k <= 3; k++) Put("CHO_Fence", 2f * Spacing + 4f, k * 2.06f, 90f);
        Put("CHO_Banner", -2f * Spacing - 2f, 4f, 90f);
        Put("CHO_Banner", -2f * Spacing - 2f, -4f, 90f);
        return n;
    }

    // 바닥: CHO_FL_Basic(1 m 타일) 재질을 판 크기만큼 반복한 판 + 상자 충돌체(윗면 높이 0)
    private static void AddGround(Transform root)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(root, false);
        ground.transform.localScale = new Vector3(GroundHalfX / 5f, 1f, GroundHalfZ / 5f); // Plane = 10 m
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        var box = ground.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.5f, 0f);
        box.size = new Vector3(10f, 1f, 10f);
        ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();
        GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }

    private static Material GroundMaterial()
    {
        string path = ChocolateFactoryAssets.GeneratedFolder + "/M_CHO_PreviewGround.mat";
        var source = AssetDatabase.LoadAssetAtPath<Material>(ChocolateFactoryAssets.ModelFolder + "/Materials/M_CHO_FloorBasic.mat");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        else if (source != null) m.CopyPropertiesFromMaterial(source);
        m.mainTextureScale = new Vector2(GroundHalfX * 2f, GroundHalfZ * 2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void MoveSpawn(Scene scene, string spawnName, Vector3 position)
    {
        GameObject spawn = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == spawnName)?.gameObject;
        if (spawn != null) spawn.transform.position = position;
    }
}
