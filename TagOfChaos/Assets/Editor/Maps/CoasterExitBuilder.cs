using UnityEditor;
using UnityEngine;

// 저주받은 놀이공원 롤러코스터 모델·재질 도우미(Request1009Plan.md §30에서 시작).
// 실제 맵 놀이기구 빌더(CarnivalRidesBuilder)와 레일 경로 굽기(CoasterPathBaker)가 쓴다:
// 입구·트랙 FBX 가져오기 설정, 입구·통로·레일 재질, 계단 경사 쐐기 메시.
// 옛 Game_CursedCandyCarnival에 탈출구를 조립하던 부분(Build Coaster Exit)은 옛 Game_ 맵과 함께 없앴다(Request1009Plan.md §47 CL5).
public static class CoasterExitBuilder
{
    public const string Folder = "Assets/Maps/CursedCandyCarnival/Models/Coaster";
    public const string EntrancePath = Folder + "/CUR_CoasterEntrance.fbx";
    public const string TrackPath = Folder + "/CUR_CoasterTrack.fbx";
    private const string RampMeshPath = Folder + "/CUR_CoasterRamp.asset";

    // 단위 쐐기: 폭 x −0.5~0.5, 길이 z 0~1에서 높이 0 → 1(속이 찬 볼록 계단 경사 충돌용)
    internal static Mesh RampMesh()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RampMeshPath);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "CUR_CoasterRamp" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f),
            new Vector3(-0.5f, 1f, 1f), new Vector3(0.5f, 1f, 1f),
        };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 4, 1, 5, 4, 2, 4, 3, 3, 4, 5, 0, 4, 2, 1, 3, 5 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, RampMeshPath);
        return mesh;
    }

    // ---------------- assets ----------------

    internal static GameObject Import(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return null;
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.animationType != ModelImporterAnimationType.None
            || importer.importAnimation || importer.addCollider || !importer.bakeAxisConversion)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.bakeAxisConversion = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    internal static Texture2D Texture(string name, bool normal)
    {
        string path = $"{Folder}/{name}.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != type || ti.anisoLevel != 4)
            {
                ti.textureType = type;
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    internal static Material EntranceMaterial()
    {
        Material m = LoadOrCreate("M_CUR_CoasterEntrance");
        m.mainTexture = Texture("CoasterEntrance_albedo", false);
        m.SetTexture("_BumpMap", Texture("CoasterEntrance_normal", true));
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Glossiness", 0.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    internal static Material TunnelMaterial() => PlainMaterial("M_CUR_CoasterTunnel", new Color(0.08f, 0.03f, 0.09f), Color.black, 0.1f);

    // 레일·지지대: 색(참고 그림) + 구운 그늘(AO)
    internal static Material TrackMaterial(string part, Color color)
    {
        Material m = LoadOrCreate("M_CUR_" + part);
        m.color = color;
        m.SetTexture("_OcclusionMap", Texture(part + "_ao", false));
        m.SetFloat("_OcclusionStrength", 1f);
        m.SetFloat("_Glossiness", part.Contains("Rail") ? 0.65f : 0.45f);
        EditorUtility.SetDirty(m);
        return m;
    }

    internal static Material PlainMaterial(string name, Color color, Color emission, float gloss)
    {
        Material m = LoadOrCreate(name);
        m.color = color;
        m.SetFloat("_Glossiness", gloss);
        if (emission != Color.black)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", emission);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    internal static Material LoadOrCreate(string name)
    {
        string path = $"{Folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }
}
