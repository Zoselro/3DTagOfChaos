using System.IO;
using UnityEditor;
using UnityEngine;

// 분홍 룬 → 초록 룬(Request1009Plan.md §69 D5 — 24번). 분홍 원본은 그대로 두고 초록 판을 만든다:
// 건물 겉 텍스처(알베도·발광)에서 분홍~자주 색만 초록으로 돌린 새 텍스처 + 재질, 손 모델 FBX 복사본 + 초록 재질, 아이콘, 아이템(ID Rune_Green, GUID 그대로 이름만 바꿈),
// GIN_RuneBuilding 프리팹의 Var_Rune_Pink → Var_Rune_Green(초록 재질). 레시피·상자는 아이템을 참조로 들고 있어 그대로 이어진다.
public static class RuneGreenSetup
{
    private const string Dir = "Assets/09. Environment/GingerbreadBuildings/Rune/GIN_RUNE_Pink/";
    private const string GreenDir = "Assets/09. Environment/GingerbreadBuildings/Rune/GIN_RUNE_Green/";
    private const string PinkExterior = "Assets/09. Environment/GingerbreadBuildings/Materials/M_GIN_RUNE_Pink_Exterior.mat";
    private const string GreenExterior = "Assets/09. Environment/GingerbreadBuildings/Materials/M_GIN_RUNE_Green_Exterior.mat";
    private const string ItemPink = "Assets/03. SO/Escape/Items/Item_Rune_Pink.asset";
    private const string ItemGreen = "Assets/03. SO/Escape/Items/Item_Rune_Green.asset";
    private const string ModelPink = "Assets/09. Environment/Escape/Models/ITEM_Rune_Pink.fbx";
    private const string ModelGreen = "Assets/09. Environment/Escape/Models/ITEM_Rune_Green.fbx";
    private const string MatPink = "Assets/09. Environment/Escape/Materials/ME_Rune_Pink.mat";
    private const string MatGreen = "Assets/09. Environment/Escape/Materials/ME_Rune_Green.mat";
    private const string IconPink = "Assets/08. Resources/ItemIcons/Icon_Rune_Pink.png";
    private const string IconGreen = "Assets/08. Resources/ItemIcons/Icon_Rune_Green.png";
    private const string BuildingPrefab = "Assets/04. Prefabs/Environment/GingerbreadBuildings/GIN_RuneBuilding.prefab";
    public static readonly Color Green = new Color(0.25f, 0.9f, 0.35f);

    [MenuItem("TagOfChaos/Escape/Make Green Rune (from Pink)")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(GreenDir.TrimEnd('/'))) AssetDatabase.CreateFolder(Path.GetDirectoryName(GreenDir.TrimEnd('/')).Replace('\\', '/'), "GIN_RUNE_Green");
        string albedo = Recolor(Dir + "GIN_RUNE_Pink_albedo.jpg", GreenDir + "GIN_RUNE_Green_albedo.png");
        string emission = Recolor(Dir + "GIN_RUNE_Pink_emission.jpg", GreenDir + "GIN_RUNE_Green_emission.png");
        Recolor(IconPink, IconGreen, true);
        AssetDatabase.Refresh();
        CopyImportSettings(Dir + "GIN_RUNE_Pink_albedo.jpg", albedo);
        CopyImportSettings(Dir + "GIN_RUNE_Pink_emission.jpg", emission);
        CopyImportSettings(IconPink, IconGreen);

        // 건물 재질
        var pinkMat = AssetDatabase.LoadAssetAtPath<Material>(PinkExterior);
        var greenMat = AssetDatabase.LoadAssetAtPath<Material>(GreenExterior);
        if (greenMat == null) { greenMat = new Material(pinkMat); AssetDatabase.CreateAsset(greenMat, GreenExterior); }
        else greenMat.CopyPropertiesFromMaterial(pinkMat);
        greenMat.name = "M_GIN_RUNE_Green_Exterior";
        var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
        var emissionTex = AssetDatabase.LoadAssetAtPath<Texture2D>(emission);
        foreach (string p in new[] { "_MainTex", "_BaseMap" }) if (greenMat.HasProperty(p) && greenMat.GetTexture(p) != null) greenMat.SetTexture(p, albedoTex);
        if (greenMat.HasProperty("_EmissionMap") && greenMat.GetTexture("_EmissionMap") != null) greenMat.SetTexture("_EmissionMap", emissionTex);
        if (greenMat.HasProperty("_EmissionColor")) greenMat.SetColor("_EmissionColor", ShiftColor(greenMat.GetColor("_EmissionColor")));
        foreach (string p in new[] { "_Color", "_BaseColor" }) if (greenMat.HasProperty(p)) greenMat.SetColor(p, ShiftColor(greenMat.GetColor(p)));
        EditorUtility.SetDirty(greenMat);

        // 손 모델 재질 + FBX 복사본(초록 재질로 바꿔 끼움)
        var mePink = AssetDatabase.LoadAssetAtPath<Material>(MatPink);
        var meGreen = AssetDatabase.LoadAssetAtPath<Material>(MatGreen);
        if (meGreen == null) { meGreen = new Material(mePink); AssetDatabase.CreateAsset(meGreen, MatGreen); }
        meGreen.name = "ME_Rune_Green";
        foreach (string p in new[] { "_Color", "_BaseColor" }) if (meGreen.HasProperty(p)) meGreen.SetColor(p, Green);
        if (meGreen.HasProperty("_EmissionColor")) meGreen.SetColor("_EmissionColor", Green * 1.2f);
        EditorUtility.SetDirty(meGreen);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelGreen) == null) AssetDatabase.CopyAsset(ModelPink, ModelGreen);
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelGreen);
        foreach (var kv in importer.GetExternalObjectMap())
            if (kv.Key.type == typeof(Material)) importer.RemoveRemap(kv.Key);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "ME_Rune_Pink"), meGreen);
        importer.SaveAndReimport();

        // 아이템(GUID 그대로 이름만 바꿈 — 레시피·카탈로그 참조 유지)
        if (AssetDatabase.LoadAssetAtPath<ItemSO>(ItemPink) != null) AssetDatabase.MoveAsset(ItemPink, ItemGreen);
        var item = AssetDatabase.LoadAssetAtPath<ItemSO>(ItemGreen);
        var so = new SerializedObject(item);
        so.FindProperty("itemId").stringValue = "Rune_Green";
        so.FindProperty("displayName").stringValue = "초록 룬";
        so.FindProperty("tint").colorValue = Green;
        so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(IconGreen);
        so.FindProperty("modelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ModelGreen);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 건물 변형
        GameObject root = PrefabUtility.LoadPrefabContents(BuildingPrefab);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Var_Rune_Pink" && t.name != "Var_Rune_Green") continue;
            t.name = "Var_Rune_Green";
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) if (mats[i] == pinkMat) mats[i] = greenMat;
                r.sharedMaterials = mats;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(root, BuildingPrefab);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[RuneGreen] Done: item Rune_Green, building Var_Rune_Green, textures in " + GreenDir);
    }

    // 분홍~자주(색상 285°~355°) 픽셀만 같은 채도·밝기의 초록(색상 + 160°)으로.
    private static string Recolor(string src, string dst, bool keepAlpha = false)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(src));
        Color[] px = tex.GetPixels();
        for (int i = 0; i < px.Length; i++) px[i] = ShiftColor(px[i]);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(dst, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return dst;
    }

    public static Color ShiftColor(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        float deg = h * 360f;
        if (s < 0.12f || deg < 285f || deg > 355f) return c;
        Color g = Color.HSVToRGB(Mathf.Repeat(deg + 160f, 360f) / 360f, s, v, true);
        g.a = c.a;
        return g;
    }

    private static void CopyImportSettings(string from, string to)
    {
        var a = (TextureImporter)AssetImporter.GetAtPath(from);
        var b = (TextureImporter)AssetImporter.GetAtPath(to);
        if (a == null || b == null) return;
        b.textureType = a.textureType;
        b.spriteImportMode = a.spriteImportMode;
        b.sRGBTexture = a.sRGBTexture;
        b.alphaIsTransparency = a.alphaIsTransparency;
        b.mipmapEnabled = a.mipmapEnabled;
        b.maxTextureSize = a.maxTextureSize;
        b.textureCompression = a.textureCompression;
        b.SaveAndReimport();
    }
}
