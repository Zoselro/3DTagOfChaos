using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 탈출 모드 데이터 에셋을 만든다(EscapePlan.md §5.1): 아이템·도구(Assets/03. SO/Escape), 맵별 레시피, 카탈로그·문구
// (Assets/Resources/Escape). 이미 있는 에셋은 값만 다시 맞춘다(GUID 유지 — 씬·프리팹 참조가 깨지지 않게).
// 표시 이름은 여기서 영문 기본값만 넣는다(코드에 한글 금지). 한글 문구는 에셋에서 입력한다.
public static class EscapeAssetsBuilder
{
    private const string MenuPath = "Tools/TagOfChaos/Escape/Build Data Assets";
    private const string SoRoot = "Assets/03. SO/Escape";
    private const string ResourcesRoot = "Assets/Resources/Escape";

    [MenuItem(MenuPath)]
    public static void Build()
    {
        EnsureFolder("Assets/03. SO", "Escape");
        EnsureFolder(SoRoot, "Items");
        EnsureFolder(SoRoot, "Tools");
        EnsureFolder(SoRoot, "Recipes");
        EnsureFolder("Assets/Resources", "Escape");

        // 도구(D23 — 첫 기본값, 플레이해 보고 조정)
        // §45(2026-10-29): 뿅망치 5 m, 스턴건 20 m, 물풍선 10 m·5초 갇힘·색칠 없음
        ToolSO hammerTool = Tool("Tool_Hammer", ToolKind.Hammer, 3, 5f, 1.2f, 3f, 3f, 3f, false); // 기절 3초(2026-10-29 사용자 지시)
        ToolSO stunTool = Tool("Tool_StunGun", ToolKind.StunGun, 3, 20f, 0.35f, 2f, 1.5f, 3f, false);
        ToolSO balloonTool = Tool("Tool_WaterBalloon", ToolKind.WaterBalloon, 3, 10f, 1.5f, 5f, 5f, 3f, false);

        var items = new List<ItemSO>();
        ItemSO Mat(string id, string name, Color c)
        {
            ItemSO it = Item("Item_" + id, id, name, c, HoldType.TwoHanded, ItemCategory.Material, null);
            items.Add(it);
            return it;
        }
        ItemSO ToolItem(string id, string name, Color c, ToolSO t)
        {
            ItemSO it = Item("Item_" + id, id, name, c, HoldType.OneHanded, ItemCategory.Tool, t);
            items.Add(it);
            return it;
        }

        ItemSO redButton = Mat("RedButton", "Red Button", new Color(0.9f, 0.1f, 0.1f));
        ItemSO seatbelt = Mat("Seatbelt", "Seatbelt", new Color(0.35f, 0.45f, 0.6f));
        ItemSO battery = Mat("Battery", "Battery", new Color(0.55f, 0.85f, 0.2f));
        ItemSO toolbox = Mat("Toolbox", "Toolbox", new Color(0.85f, 0.35f, 0.15f)); // 건물 재료 전용(§26 H4)
        ItemSO gear = Mat("Gear", "Gear", new Color(0.75f, 0.75f, 0.8f));
        ItemSO macaronWheel = Mat("MacaronWheel", "Macaron Wheel", new Color(1f, 0.6f, 0.75f));
        ItemSO cookieWheel = Mat("CookieWheel", "Cookie Wheel", new Color(0.8f, 0.55f, 0.3f));
        ItemSO oil = Mat("ChocolateOil", "Chocolate Oil", new Color(0.3f, 0.15f, 0.08f));
        ItemSO[] runes =
        {
            Mat("Rune_Red", "Rune", new Color(1f, 0.15f, 0.2f)),
            Mat("Rune_Blue", "Rune", new Color(0.2f, 0.45f, 1f)),
            Mat("Rune_Yellow", "Rune", new Color(1f, 0.9f, 0.15f)),
            Mat("Rune_Pink", "Rune", new Color(1f, 0.35f, 0.8f)),
        };
        ItemSO[] cells =
        {
            Mat("CandyCell_Red", "Candy Cell", new Color(1f, 0.15f, 0.15f)),
            Mat("CandyCell_Blue", "Candy Cell", new Color(0.2f, 0.45f, 1f)), // 캔디숲 막대사탕 4색(Request1009Plan.md §28.5 — 주황 → 파랑)
            Mat("CandyCell_Yellow", "Candy Cell", new Color(1f, 0.9f, 0.15f)),
            Mat("CandyCell_Green", "Candy Cell", new Color(0.3f, 0.95f, 0.3f)),
        };
        ItemSO hammer = ToolItem("Hammer", "Toy Hammer", new Color(1f, 0.4f, 0.6f), hammerTool);
        hammer.EditorSetup("Hammer", hammer.DisplayName, new Color(1f, 0.4f, 0.6f), HoldType.TwoHanded, ItemCategory.Tool, hammerTool); // 두 손으로 드는 거대한 망치(§45.5)
        hammer.EditorSetHeldScale(3f);
        ItemSO stunGun = ToolItem("StunGun", "Stun Gun", new Color(0.3f, 0.7f, 1f), stunTool);
        ItemSO balloon = ToolItem("WaterBalloon", "Water Balloon", new Color(0.3f, 0.85f, 1f), balloonTool);

        // Request1009Plan.md §25 — 새 아이템(모델이 오기 전에는 색 도형).
        // 수리키트는 공구상자와 다른 아이템(§26 H4) — 스파이 로켓 칸을 50%씩 채운다. 모델·아이콘은 공구상자 것을 같이 쓴다.
        ItemSO repairKit = Item("Item_RepairKit", "RepairKit", "Repair Kit", new Color(0.85f, 0.35f, 0.15f), HoldType.OneHanded, ItemCategory.Material, null);
        items.Add(repairKit);
        if (repairKit.ModelPrefab == null && toolbox.ModelPrefab != null) repairKit.EditorSetVisuals(toolbox.Icon, toolbox.ModelPrefab);
        ItemSO dismantle = ToolItem("Dismantle", "Dismantle Tool", new Color(0.55f, 0.55f, 0.6f), Tool("Tool_Dismantle", ToolKind.Dismantle, 1, 2f, 0.5f, 0f, 0f, 1f, false));
        ItemSO jail = ToolItem("JailBalloon", "Jail Balloon", new Color(0.6f, 0.85f, 1f), Tool("Tool_JailBalloon", ToolKind.JailBalloon, 1, 12f, 1.5f, 4f, 3f, 3f, false));
        ItemSO skates = ToolItem("Skates", "Inline Skates", new Color(0.95f, 0.3f, 0.55f), Tool("Tool_Skates", ToolKind.Skates, 1, 1f, 0.5f, 0f, 0f, 1f, false, 5f, 2f));
        ItemSO pumpkin = ToolItem("Pumpkin", "Pumpkin Carriage", new Color(1f, 0.55f, 0.1f), Tool("Tool_Pumpkin", ToolKind.Pumpkin, 1, 1f, 0.5f, 0f, 0f, 1f, false));
        ItemSO recorder = ToolItem("Recorder", "Sticky Voice Recorder", new Color(0.75f, 0.45f, 0.95f), Tool("Tool_Recorder", ToolKind.Recorder, 1, 1f, 0.5f, 0f, 0f, 1f, false, 30f, 1f));
        ItemSO heliHat = ToolItem("HeliHat", "Helicopter Hat", new Color(1f, 0.45f, 0.75f), Tool("Tool_HeliHat", ToolKind.HeliHat, 1, 1f, 0.5f, 0f, 0f, 0f, false));
        ItemSO noiseBomb = ToolItem("NoiseBomb", "Noise Bomb", new Color(0.3f, 0.1f, 0.1f), Tool("Tool_NoiseBomb", ToolKind.NoiseBomb, 1, 4f, 4f, 2f, 0f, 0f, false));

        var recipes = new List<EscapeRecipeSO>
        {
            Recipe("CursedCandyCarnival", EscapeExitKind.RollerCoaster, RecipeMode.Slots,
                new[] { G(redButton, 1, 1), G(seatbelt, 1, 1), G(battery, 3, 0), G(toolbox, 3, 0, "Repair") }, null, null,
                new[] { R(seatbelt), R(battery) }, toolbox, fillsCapacity: true),
            Recipe("HauntedBakery", EscapeExitKind.BakeryMachine, RecipeMode.Slots,
                new[] { G(redButton, 1, 1), G(gear, 1, 1), G(battery, 3, 0), G(toolbox, 3, 0, "Repair") }, null, null,
                new[] { R(gear), R(battery) }, toolbox, fillsCapacity: true),
            Recipe("ChocolateFactory", EscapeExitKind.ChocolateTrain, RecipeMode.Slots,
                new[] { G(macaronWheel, 2, 1), G(cookieWheel, 2, 0), G(oil, 2, 0), G(gear, 2, 0) }, null, null,
                new[] { R(oil), R(gear) }, toolbox, fillsCapacity: true),
            Recipe("GingerbreadVillage", EscapeExitKind.RuneAltar, RecipeMode.Counter, null, "Rune", runes,
                new[] { R(runes), R(runes) }, toolbox, fillsCapacity: true), // 모든 맵: 늘 최대 수만 쓰고 남는 칸은 켜진 채 시작(2026-10-03)
            Recipe("CandyForest", EscapeExitKind.CakeRocket, RecipeMode.Counter, null, "Candy Cell", cells,
                new[] { R(cells), R(cells) }, toolbox, fillsCapacity: true),
        };

        // 건물 4곳 재료(§25.5 E — 지금 맵 재료 그대로, 판마다 어느 건물에 넣을지 섞는다)
        SetBuildingMaterials(recipes, "CursedCandyCarnival", redButton, seatbelt, battery, toolbox);
        SetBuildingMaterials(recipes, "HauntedBakery", redButton, gear, battery, toolbox);
        SetBuildingMaterials(recipes, "ChocolateFactory", macaronWheel, cookieWheel, oil, gear);
        SetBuildingMaterials(recipes, "GingerbreadVillage", runes);
        SetBuildingMaterials(recipes, "CandyForest", cells);

        var catalog = LoadOrCreate<EscapeCatalogSO>(ResourcesRoot + "/EscapeCatalog.asset");
        catalog.EditorSetup(items.ToArray(), recipes.ToArray(), hammer);
        catalog.EditorSetupReform(repairKit, dismantle, new[] { stunGun, hammer, balloon } /* 감옥 풍선은 물풍선에 합쳤다(§45.8 Q1) */, new[] { skates, pumpkin, recorder }, new[] { 5, 3, 5 }, heliHat, noiseBomb);
        BuildXRayMaterial();
        EditorUtility.SetDirty(catalog);
        LoadOrCreate<EscapeTextsSO>(ResourcesRoot + "/EscapeTexts.asset");
        BuildMaterialTemplates();

        AssetDatabase.SaveAssets();
        Debug.Log($"[EscapeAssets] items={items.Count} recipes={recipes.Count} catalog={AssetDatabase.GetAssetPath(catalog)}");
    }

    // 런타임에 키워드를 켜서 쓰는 Standard 재질의 틀 4개(research.md R5-21). 에셋이 있어야 빌드에 그 셰이더 변형이 들어간다.
    [MenuItem("Tools/TagOfChaos/Escape/Build Material Templates")]
    public static void BuildMaterialTemplates()
    {
        EnsureFolder("Assets/Resources", "Escape");
        foreach (bool fade in new[] { false, true })
        foreach (bool glow in new[] { false, true })
        {
            string path = $"Assets/Resources/{EscapeVisuals.TemplateName(fade, glow)}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("Standard");
            EscapeVisuals.ConfigureTemplate(mat, fade, glow);
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
    }

    private static EscapeRecipeSO.SlotGroup G(ItemSO item, int slots, int fixedRequired, string label = null) =>
        new EscapeRecipeSO.SlotGroup { item = item, slotCount = slots, fixedRequired = fixedRequired, label = label };

    private static EscapeRecipeSO.RocketSlot R(params ItemSO[] accepts) =>
        new EscapeRecipeSO.RocketSlot { label = accepts[0].DisplayName, accepts = accepts };

    private static ItemSO Item(string file, string id, string name, Color color, HoldType hold, ItemCategory category, ToolSO tool)
    {
        var item = LoadOrCreate<ItemSO>($"{SoRoot}/Items/{file}.asset");
        // 이미 입력한 표시 이름(한글 등)은 덮어쓰지 않는다.
        string keepName = string.IsNullOrEmpty(item.DisplayName) ? name : item.DisplayName;
        item.EditorSetup(id, keepName, color, hold, category, tool);
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void SetBuildingMaterials(List<EscapeRecipeSO> recipes, string map, params ItemSO[] materials)
    {
        foreach (EscapeRecipeSO r in recipes)
        {
            if (r.MapName != map) continue;
            r.EditorSetBuildingMaterials(materials);
            EditorUtility.SetDirty(r);
        }
    }

    // 적외선 투시 재질(Resources/Escape/XRay, XRayMarker가 읽는다 — 빌드에 셰이더가 들어가도록 에셋으로 둔다).
    private static void BuildXRayMaterial()
    {
        Shader shader = Shader.Find("TagOfChaos/XRaySilhouette");
        if (shader == null) { Debug.LogWarning("[EscapeAssets] XRay shader not found."); return; }
        string path = ResourcesRoot + "/XRay.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        EditorUtility.SetDirty(mat);
    }

    private static ToolSO Tool(string file, ToolKind kind, int charges, float range, float radius, float cookieStun, float monsterStun, float cooldown, bool paints,
        float effect = 5f, float speed = 2f)
    {
        var tool = LoadOrCreate<ToolSO>($"{SoRoot}/Tools/{file}.asset");
        if (!EditorPrefs.GetBool("EscapeAssets.ToolsTuned." + file, false)) // 밸런스 조정 값은 한 번만 기본값으로 넣는다
        {
            tool.EditorSetup(kind, charges, range, radius, cookieStun, monsterStun, cooldown, paints, effect, speed);
            EditorPrefs.SetBool("EscapeAssets.ToolsTuned." + file, true);
        }
        EditorUtility.SetDirty(tool);
        return tool;
    }

    private static EscapeRecipeSO Recipe(string map, EscapeExitKind exit, RecipeMode mode, EscapeRecipeSO.SlotGroup[] groups, string counterLabel,
        ItemSO[] counterItems, EscapeRecipeSO.RocketSlot[] rocket, ItemSO toolbox, bool fillsCapacity = false)
    {
        var recipe = LoadOrCreate<EscapeRecipeSO>($"{SoRoot}/Recipes/Recipe_{map}.asset");
        recipe.EditorSetup(map, exit, mode, groups, counterLabel, counterItems, rocket, toolbox, 1, fillsCapacity);
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
