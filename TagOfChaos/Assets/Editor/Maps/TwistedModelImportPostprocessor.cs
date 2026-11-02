using UnityEditor;

// 뒤틀린 소품·숨을 곳 모델(ArtSource/Maps/Source~/Scripts/twisted_props.py·twisted_shelters.py가 내보낸 FBX) 임포트 규칙
// (TwistedCandyPlan.md §3). 탈출 모델과 같다: 1 unit = 1 m, 축 변환은 Blender에서 구웠다, 애니메이션·카메라·조명 없음.
// 머티리얼은 TwistedPropBuilder가 만든 MT_* .mat에 이름으로 연결한다(팔레트 색을 정확히 맞추려고 FBX 색은 쓰지 않는다).
public class TwistedModelImportPostprocessor : AssetPostprocessor
{
    public const string ModelFolder = "Assets/09. Environment/Twisted/Models";
    public const string MaterialFolder = "Assets/09. Environment/Twisted/Materials";

    public override uint GetVersion() => 1;

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
        WitchCookieHouseImportPostprocessor.RemapMaterials(importer, MaterialFolder);
    }
}
