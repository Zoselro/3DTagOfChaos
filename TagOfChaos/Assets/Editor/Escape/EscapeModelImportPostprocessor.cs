using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 탈출 모드 Blender 모델(ArtSource/Maps/Source~/Scripts/escape_assets.py가 내보낸 FBX) 임포트 규칙(EscapePlan.md §4.4).
// - 1 unit = 1 m, 축 변환은 Blender에서 구웠으므로 bakeAxisConversion은 끈다. 애니메이션·카메라·조명은 가져오지 않는다.
// - 머티리얼은 맵 공용 팔레트(M_*)와 탈출 모드 전용(ME_*) .mat으로 이름이 같으면 연결한다(앞 폴더 우선).
// - 재료 상자(CHEST): 받침이 몸통 옆면과 같은 평면이고 몸통 띠·뚜껑 테두리 간격이 5 mm라 멀리서 깜빡였다(z-fighting,
//   Request1009Plan.md §16). 장식 부품을 조금씩 키워 겹치는 면 간격을 4 mm 이상으로 만든다(ChestScale).
public class EscapeModelImportPostprocessor : AssetPostprocessor
{
    public const string ModelFolder = "Assets/09. Environment/Escape/Models";
    public const string MaterialFolder = "Assets/09. Environment/Escape/Materials";
    public const string CommonMaterialFolder = "Assets/Maps/Common/Materials";

    // EscapeModelBuilder가 FBX 안의 원래 머티리얼 색을 읽는 동안에는 연결하지 않는다.
    public static bool SkipRemap;

    // 규칙이 바뀌면 숫자를 올려 재임포트를 강제한다.
    public override uint GetVersion() => 4;

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(ModelFolder + "/")) return;

        var importer = (ModelImporter)assetImporter;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        if (!SkipRemap) RemapMaterials(importer);
    }

    // (메시 이름, 머티리얼 이름) → 키울 배율. 바깥에 덮이는 부품일수록 크게: 몸통 껍질 1 < 받침 < 띠 < 걸쇠, 뚜껑 껍질 > 몸통 띠 < 뚜껑 띠 < 뚜껑 걸쇠.
    // 부품마다 같은 배율로 키우므로 부품 모양·이음매는 그대로다(법선 방향으로 띄우면 각진 모서리가 갈라졌다). 기준점은 껍질(ME_Cookie_Gold)의
    // 수평 가운데·바닥 — 받침은 바닥에 붙어 있도록 수평으로만 키운다.
    public const string ChestModelName = "CHEST";
    private const string ShellMaterial = "ME_Cookie_Gold";
    public static readonly Dictionary<string, Vector3> ChestScale = new Dictionary<string, Vector3>
    {
        ["Body/ME_Cookie_Dark"] = new Vector3(1.010f, 1f, 1.010f),
        ["Body/ME_Purple_Deep"] = Vector3.one * 1.012f,
        ["Body/ME_Gold"] = Vector3.one * 1.022f,
        ["Lid/ME_Cookie_Gold"] = Vector3.one * 1.022f,
        ["Lid/ME_Purple_Deep"] = Vector3.one * 1.034f,
        ["Lid/ME_Gold"] = Vector3.one * 1.060f,
    };

    // 배율 뒤에 옮길 거리(m, 상자 기준 +Z = 걸쇠가 있는 앞). 뚜껑 띠 아랫면이 뚜껑 껍질 아랫면과 같은 높이라 열린 뚜껑 밑에서 깜빡였고,
    // 뚜껑 걸쇠가 몸통 띠 윗면과 1 mm 안쪽으로 붙어 있었다.
    public static readonly Dictionary<string, Vector3> ChestShift = new Dictionary<string, Vector3>
    {
        ["Lid/ME_Purple_Deep"] = new Vector3(0f, -0.004f, 0f),
        ["Lid/ME_Gold"] = new Vector3(0f, 0f, 0.006f),
    };

    private void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith(ModelFolder + "/") || System.IO.Path.GetFileNameWithoutExtension(assetPath) != ChestModelName) return;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null) continue;
            Material[] materials = renderer.sharedMaterials;
            Vector3[] vertices = mesh.vertices;
            Vector3 anchor = ShellAnchor(mesh, materials, vertices);
            var moved = new bool[vertices.Length];
            for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
            {
                if (materials[sub] == null) continue;
                string key = filter.name + "/" + materials[sub].name;
                if (!ChestScale.TryGetValue(key, out Vector3 k)) continue;
                ChestShift.TryGetValue(key, out Vector3 shift);
                foreach (int i in mesh.GetTriangles(sub))
                {
                    if (moved[i]) continue;
                    moved[i] = true;
                    vertices[i] = anchor + Vector3.Scale(vertices[i] - anchor, k) + shift;
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }

    private static Vector3 ShellAnchor(Mesh mesh, Material[] materials, Vector3[] vertices)
    {
        Bounds b = mesh.bounds;
        float minY = b.min.y;
        for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
        {
            if (materials[sub] == null || materials[sub].name != ShellMaterial) continue;
            minY = float.MaxValue;
            foreach (int i in mesh.GetTriangles(sub)) minY = Mathf.Min(minY, vertices[i].y);
        }
        return new Vector3(b.center.x, minY, b.center.z);
    }

    public static bool RemapMaterials(ModelImporter importer) =>
        WitchCookieHouseImportPostprocessor.RemapMaterials(importer, CommonMaterialFolder, MaterialFolder);
}
