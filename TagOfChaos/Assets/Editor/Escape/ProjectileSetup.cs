using System.IO;
using UnityEditor;
using UnityEngine;

// 던지는 물체 설정 에셋 3개를 만들고 도구에 연결한다(Request1009Plan.md §70.3.5, 사용자 확정값 §70.4.1).
// 수색 폭탄 투사체 모델(Blender PROJ_SearchBomb.fbx)은 Temp/bl에서 손 모델(ITEM_SearchBomb) 옆으로 옮긴다. 다시 실행해도 같은 결과.
public static class ProjectileSetup
{
    private const string Folder = "Assets/03. SO/Escape/Projectiles";
    private const string ExportedModel = "Temp/bl/PROJ_SearchBomb.fbx";

    [MenuItem("Tools/Escape/Setup Projectiles (§70)")]
    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder("Assets/03. SO/Escape")) AssetDatabase.CreateFolder("Assets/03. SO", "Escape");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/03. SO/Escape", "Projectiles");

        GameObject bombModel = ImportSearchBombModel();

        // 물풍선: 사람 몸에 닿으면 가둠, 벽·바닥이면 그냥 터짐(Q1 A). 허공 12 m·최대 20 m(Q3).
        ProjectileConfigSO balloon = Config("Projectile_WaterBalloon", c =>
        {
            c.baseRange = 12f; c.maxRange = 20f; c.launchAngle = 45f; c.radius = 0.2f;
            c.burstOnImpact = true; c.fuseAfterImpact = 0f; c.maxLifeSeconds = 5f; c.spinSpeed = 0f; c.model = null;
        });
        // 섬광탄: 튕기고 구르다 첫 충돌 2초 뒤(Q2). 허공 12 m·최대 20 m(Q3).
        ProjectileConfigSO flash = Config("Projectile_Flashbang", c =>
        {
            c.baseRange = 12f; c.maxRange = 20f; c.launchAngle = 45f; c.radius = 0.1f;
            c.bounciness = 0.4f; c.surfaceFriction = 0.2f; c.rollDeceleration = 4f; c.restSpeed = 0.35f;
            c.burstOnImpact = false; c.fuseAfterImpact = 2f; c.maxLifeSeconds = 5f; c.spinSpeed = 540f; c.model = null;
        });
        // 수색 폭탄: 설계안 10 m·최대 12 m(Q3), 첫 충돌 2초 뒤(Q2).
        ProjectileConfigSO search = Config("Projectile_SearchBomb", c =>
        {
            c.baseRange = 10f; c.maxRange = 12f; c.launchAngle = 45f; c.radius = 0.15f;
            c.bounciness = 0.35f; c.surfaceFriction = 0.2f; c.rollDeceleration = 3f; c.restSpeed = 0.35f;
            c.burstOnImpact = false; c.fuseAfterImpact = 2f; c.maxLifeSeconds = 5f; c.spinSpeed = 360f; c.model = bombModel; c.modelScale = 1f;
        });

        string linked = "";
        foreach (string guid in AssetDatabase.FindAssets("t:ToolSO"))
        {
            var tool = AssetDatabase.LoadAssetAtPath<ToolSO>(AssetDatabase.GUIDToAssetPath(guid));
            ProjectileConfigSO want = tool.Kind == ToolKind.WaterBalloon ? balloon : tool.Kind == ToolKind.Flashbang ? flash : tool.Kind == ToolKind.SearchBomb ? search : null;
            if (tool.Projectile == want) continue;
            tool.EditorSetProjectile(want);
            EditorUtility.SetDirty(tool);
            linked += tool.name + " ";
        }
        AssetDatabase.SaveAssets();
        return $"configs ok, model {(bombModel != null ? AssetDatabase.GetAssetPath(bombModel) : "none")}, linked: {linked}";
    }

    private static ProjectileConfigSO Config(string name, System.Action<ProjectileConfigSO> set)
    {
        string path = $"{Folder}/{name}.asset";
        var c = AssetDatabase.LoadAssetAtPath<ProjectileConfigSO>(path);
        if (c == null)
        {
            c = ScriptableObject.CreateInstance<ProjectileConfigSO>();
            AssetDatabase.CreateAsset(c, path);
        }
        set(c);
        EditorUtility.SetDirty(c);
        return c;
    }

    private static GameObject ImportSearchBombModel()
    {
        string folder = "Assets/Models/Escape";
        string[] hand = AssetDatabase.FindAssets("ITEM_SearchBomb t:Model");
        if (hand.Length > 0) folder = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(hand[0])).Replace('\\', '/');
        string target = folder + "/PROJ_SearchBomb.fbx";
        if (File.Exists(ExportedModel))
        {
            File.Copy(ExportedModel, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
        }
        var importer = AssetImporter.GetAtPath(target) as ModelImporter;
        if (importer != null && (importer.importAnimation || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard))
        {
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(target);
    }
}
