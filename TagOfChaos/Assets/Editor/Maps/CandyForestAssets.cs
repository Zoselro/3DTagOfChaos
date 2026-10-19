using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 캔디숲 실제 모델 맵의 에셋(Request1009Plan.md §28, 단계 C4).
// cf_build.py(Blender)가 Models/에 내보낸 FBX + <이름>.json(문·충돌 고리·받침대)을 읽어:
// 1) Source~/<세트>의 텍스처를 Textures/로 옮긴다(금속 R + 매끄러움 A를 한 장으로 합침).
// 2) 세트마다 Standard 재질 하나(CF_<세트>), 건물·천막 안쪽용 어두운 재질 2개.
// 3) FBX 재질을 그 재질로 연결하고, 충돌체를 붙인 프리팹을 Assets/04. Prefabs/Maps/CandyForest/에 만든다.
// 충돌체 규칙은 §28.10 통행 안전 규칙(S1~S11)을 따른다 — 작은 장식은 충돌 없음, 나무는 줄기만, 닫힌 집은 속이 찬 상자,
// 들어가는 건물은 벽을 따라 둥글게 두른 상자(문 자리는 비움).
public static class CandyForestAssets
{
    public const string Root = "Assets/09. Environment/CandyForest";
    public const string SourceDir = Root + "/Source~";
    public const string ModelDir = Root + "/Models";
    public const string TextureDir = Root + "/Textures";
    public const string MaterialDir = Root + "/Materials";
    public const string PrefabDir = "Assets/04. Prefabs/Maps/CandyForest";
    private const string MenuRoot = "Tools/TagOfChaos/CandyForest/";

    public static readonly string[] Sets =
        { "SwirlTree", "BubblegumTree", "GateItems", "Cottage", "Tiles", "Trinkets", "MatBuildings", "Park", "Whimsical", "Interior1", "Interior234" };

    public const string InnerMaterial = "CF_Inner";
    public const string InnerFloorMaterial = "CF_InnerFloor";
    public const string SocketMaterial = "CF_Socket";
    public const string GlowMaterial = "CF_Glow";
    public const float RepairHeight = 1.3f;     // 쿠키 손 높이(상호작용 높이 차 2.5 m 안, CharacterInteractor)
    private const float WallHeight = 9f;        // 건물·천막 벽 충돌 높이(6 m 위로는 못 감, S6)
    private const float ShellWallThickness = 0.9f; // 괴물집·천막 벽 — 뒤 쿠키 구멍 깊이가 1 m 이하(§28.11.7 H2)

    [MenuItem(MenuRoot + "1 Import Models (textures, materials, prefabs)")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureFolder(TextureDir);
        EnsureFolder(MaterialDir);
        EnsureFolder(PrefabDir);
        int textures = CopyTextures();
        Dictionary<string, Material> materials = BuildMaterials();
        int models = 0, prefabs = 0;
        foreach (string path in ModelPaths())
        {
            ConfigureModel(path, materials);
            models++;
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string path in ModelPaths())
            if (BuildPrefab(path) != null) prefabs++;
        int items = BuildItemModels();
        AssetDatabase.SaveAssets();
        return $"[CandyForestAssets] textures {textures}, materials {materials.Count}, models {models}, prefabs {prefabs}, item models {items}";
    }

    public static string[] ModelPaths() =>
        Directory.Exists(ModelDir) ? Directory.GetFiles(ModelDir, "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray() : new string[0];

    public static string PrefabPath(string asset) => $"{PrefabDir}/{asset}.prefab";
    public static GameObject LoadPrefab(string asset) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(asset));

    // ---------------------------------------------------------------- textures

    private static int CopyTextures()
    {
        int n = 0;
        foreach (string set in Sets)
        {
            string src = $"{SourceDir}/{set}";
            if (!Directory.Exists(src)) continue;
            n += CopyIfNewer($"{src}/{set}_albedo.png", $"{TextureDir}/{set}_albedo.png");
            n += CopyIfNewer($"{src}/{set}_normal.png", $"{TextureDir}/{set}_normal.png");
            string ms = $"{TextureDir}/{set}_ms.png";
            if (!File.Exists(ms)) { WriteMetallicSmoothness($"{src}/{set}_metallic.png", $"{src}/{set}_roughness.png", ms); n++; }
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string set in Sets)
        {
            Configure($"{TextureDir}/{set}_albedo.png", TextureImporterType.Default, true);
            Configure($"{TextureDir}/{set}_normal.png", TextureImporterType.NormalMap, false);
            Configure($"{TextureDir}/{set}_ms.png", TextureImporterType.Default, false);
        }
        return n;
    }

    private static int CopyIfNewer(string src, string dst)
    {
        if (!File.Exists(src)) return 0;
        if (File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src)) return 0;
        File.Copy(src, dst, true);
        return 1;
    }

    // Standard 셰이더의 금속 맵: R = 금속, A = 매끄러움(1 - 거칠기). 거칠기 크기에 맞춘다.
    private static void WriteMetallicSmoothness(string metallicPath, string roughnessPath, string dst)
    {
        var rough = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        rough.LoadImage(File.ReadAllBytes(roughnessPath));
        Texture2D metal = null;
        if (File.Exists(metallicPath))
        {
            metal = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            metal.LoadImage(File.ReadAllBytes(metallicPath));
        }
        int w = rough.width, h = rough.height;
        Color32[] r = rough.GetPixels32();
        Color32[] m = metal != null && metal.width == w && metal.height == h ? metal.GetPixels32() : null;
        var outPixels = new Color32[r.Length];
        for (int i = 0; i < r.Length; i++)
            outPixels[i] = new Color32(m != null ? m[i].r : (byte)0, 0, 0, (byte)(255 - r[i].r));
        var result = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        result.SetPixels32(outPixels);
        File.WriteAllBytes(dst, result.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(rough);
        if (metal != null) UnityEngine.Object.DestroyImmediate(metal);
        UnityEngine.Object.DestroyImmediate(result);
    }

    private static void Configure(string path, TextureImporterType type, bool srgb)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        bool dirty = imp.textureType != type || imp.sRGBTexture != srgb || imp.maxTextureSize != 2048;
        if (!dirty) return;
        imp.textureType = type;
        imp.sRGBTexture = srgb;
        imp.maxTextureSize = 2048;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.mipmapEnabled = true;
        imp.SaveAndReimport();
    }

    // ---------------------------------------------------------------- materials

    private static Dictionary<string, Material> BuildMaterials()
    {
        var result = new Dictionary<string, Material>();
        foreach (string set in Sets)
        {
            Material m = LoadOrCreateMaterial($"CF_{set}");
            m.color = Color.white;
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{set}_albedo.png"));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{set}_normal.png");
            m.SetTexture("_BumpMap", normal);
            if (normal != null) m.EnableKeyword("_NORMALMAP");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{set}_ms.png");
            m.SetTexture("_MetallicGlossMap", ms);
            if (ms != null) m.EnableKeyword("_METALLICGLOSSMAP");
            m.SetFloat("_GlossMapScale", 0.8f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            result[set] = m;
        }
        result[InnerMaterial] = Plain(InnerMaterial, new Color(0.11f, 0.06f, 0.1f));
        result[InnerFloorMaterial] = Plain(InnerFloorMaterial, new Color(0.24f, 0.13f, 0.15f));
        result[SocketMaterial] = Plain(SocketMaterial, new Color(0.12f, 0.1f, 0.12f));
        Material glow = Plain(GlowMaterial, new Color(1f, 0.95f, 0.45f));
        glow.EnableKeyword("_EMISSION");
        glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        glow.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.45f) * 2.5f);
        result[GlowMaterial] = glow;
        return result;
    }

    private static Material Plain(string name, Color c)
    {
        Material m = LoadOrCreateMaterial(name);
        m.color = c;
        m.SetFloat("_Glossiness", 0.1f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material LoadOrCreateMaterial(string name)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Standard"));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    public static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");

    // ---------------------------------------------------------------- models

    private static void ConfigureModel(string path, Dictionary<string, Material> materials)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return;
        imp.importAnimation = false;
        imp.animationType = ModelImporterAnimationType.None;
        imp.importCameras = false;
        imp.importLights = false;
        imp.isReadable = Path.GetFileNameWithoutExtension(path).StartsWith("CF_Lollipop_"); // 아이템 아이콘 굽기
        imp.meshCompression = ModelImporterMeshCompression.Off; // 압축하면 UV가 어긋나 아틀라스 다른 칸 색이 묻는다
        imp.bakeAxisConversion = false; // 축 회전은 Model 자식이 들고 있다(프리팹 루트는 회전 0)
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.SaveAndReimport();
        foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToList())
        {
            string key = Regex.Replace(embedded.name, @"\.\d+$", "");
            if (!materials.TryGetValue(key, out Material target)) continue;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), target);
        }
        imp.SaveAndReimport();
    }

    // ---------------------------------------------------------------- items (§28.5)

    public static readonly string[] CandyColors = { "Red", "Blue", "Green", "Yellow" };
    private const float ItemHeight = 0.8f;

    // 재료 막대사탕 4색 → 아이템 모델 프리팹(ITEM_CandyCell_<색>) + ItemSO 연결. 가운데가 원점(바닥·손·받침에서 지금 재료와 같게).
    public static int BuildItemModels()
    {
        EnsureFolder(EscapeModelBuilder.ItemPrefabFolder);
        int n = 0;
        foreach (string color in CandyColors)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/CF_Lollipop_{color}.fbx");
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>($"Assets/03. SO/Escape/Items/Item_CandyCell_{color}.asset");
            if (model == null || item == null) continue;
            var root = new GameObject($"ITEM_CandyCell_{color}");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Model";
                // 금속 없는 사탕 재질 + 아이템 색을 살짝 곱한다(텍스처의 노랑은 황토색, 초록은 한쪽이 베이지 — 4색이 또렷이 갈리게)
                Material candy = CandyMaterial(color, Color.Lerp(item.Tint, Color.white, 0.35f));
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>()) r.sharedMaterial = candy;
                Bounds b = instance.GetComponentInChildren<Renderer>().bounds;
                float s = ItemHeight / Mathf.Max(0.01f, b.size.y);
                instance.transform.localScale = Vector3.one * s;
                instance.transform.localPosition = -b.center * s;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{EscapeModelBuilder.ItemPrefabFolder}/ITEM_CandyCell_{color}.prefab");
                var so = new SerializedObject(item);
                so.FindProperty("modelPrefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
                n++;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        return n;
    }

    private static Material CandyMaterial(string color, Color tint)
    {
        Material m = LoadOrCreateMaterial("CF_Lollipop_" + color);
        m.color = tint;
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/GateItems_albedo.png"));
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/GateItems_normal.png");
        m.SetTexture("_BumpMap", normal);
        if (normal != null) m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", 0.55f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- prefabs

    [Serializable]
    private class Meta
    {
        public string name;
        public string kind;
        public float[] min, max;
        public float[] outer_radii;
        public float[] inner_half;
        public float[] pedestal;
        public float[] door;
        public float[] hole;
        public float ring_radius;
        public float clearance, inner_half_span;
    }

    private static Meta ReadMeta(string modelPath)
    {
        string json = Path.ChangeExtension(modelPath, ".json");
        return File.Exists(json) ? JsonUtility.FromJson<Meta>(File.ReadAllText(json)) : null;
    }

    private static GameObject BuildPrefab(string modelPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) return null;
        string asset = Path.GetFileNameWithoutExtension(modelPath);
        Meta meta = ReadMeta(modelPath);
        // 프리팹 루트는 회전 없는 빈 오브젝트, FBX는 Model 자식(메시 하나짜리 FBX는 루트에 Blender 축 회전이 있어
        // 그대로 루트로 쓰면 배치 회전·충돌체가 눕는다)
        var go = new GameObject(asset);
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
            instance.name = "Model";
            AddColliders(go, asset, meta);
            bool movable = asset.StartsWith("CF_Lollipop_") || asset.StartsWith("CF_Exit");
            if (!movable)
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterials.Any(m => m != null && (m.name == InnerMaterial || m.name == InnerFloorMaterial))) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(asset));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void AddColliders(GameObject go, string asset, Meta meta)
    {
        Transform root = go.transform;
        switch (asset)
        {
            case "CF_LollipopTree": case "CF_CottonTree": case "CF_PurpleTree":
                Capsule(root, "COL_Trunk", 0.7f, 5f); break;           // 나무 머리는 충돌 없음(S7)
            case "CF_Lamp": Capsule(root, "COL_Pole", 0.35f, 5f); break;
            case "CF_Signpost": Capsule(root, "COL_Pole", 0.35f, 3.5f); break;
            case "CF_CandyCane": Capsule(root, "COL_Pole", 0.55f, 8f); break;
            case "CF_FenceGate": Box(root, "COL_Fence", new Vector3(0f, 2.25f, 0f), new Vector3(6f, 4.5f, 0.7f), 0f); break;
            case "CF_Cottage": case "CF_Shop": case "CF_MonsterBox":
                SolidBounds(root, meta); break;                          // 닫힌 집은 속이 찬 상자(S4)
            case "CF_Arch":
                if (meta != null)
                {
                    float inner = meta.inner_half_span + 0.2f, outer = meta.max[0];
                    float depth = meta.max[2] - meta.min[2];
                    for (int s = -1; s <= 1; s += 2)
                        Box(root, s < 0 ? "COL_LegL" : "COL_LegR", new Vector3(s * (inner + outer) / 2f, 3.5f, 0f), new Vector3(outer - inner, 7f, depth * 0.6f), 0f);
                }
                break;
            case "CF_MonsterHouse": case "CF_Tent":
                if (meta != null) ShellWalls(root, meta); break;
            default:
                if (asset.StartsWith("CF_Bld_") && meta != null) BuildingWalls(root, meta);
                break;                                                   // 그 밖의 작은 장식·바닥 타일은 충돌 없음(S2)
        }
    }

    private static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static void Capsule(Transform root, string name, float radius, float height)
    {
        var c = Child(root, name).gameObject.AddComponent<CapsuleCollider>();
        c.radius = radius;
        c.height = height;
        c.center = new Vector3(0f, height / 2f, 0f);
    }

    private static void Box(Transform root, string name, Vector3 center, Vector3 size, float yaw)
    {
        Transform t = Child(root, name);
        t.localPosition = center;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        t.gameObject.AddComponent<BoxCollider>().size = size;
    }

    private static void SolidBounds(Transform root, Meta meta)
    {
        if (meta == null) return;
        // json 범위·위치는 Unity 축(cf_build.py가 바꿔 적는다)
        var size = new Vector3(meta.max[0] - meta.min[0], meta.max[1] - meta.min[1], meta.max[2] - meta.min[2]) * 0.85f;
        var center = new Vector3((meta.max[0] + meta.min[0]) / 2f, size.y / 2f, (meta.max[2] + meta.min[2]) / 2f);
        size.y = Mathf.Max(size.y, 3f);
        Box(root, "COL_Solid", center, size, 0f);
    }

    // 괴물집·천막(§28.11.5~7): 둥근 벽, 앞(+Z) 괴물 문 6.6 m, 뒤(-Z) 쿠키 구멍 1.4 m. 벽 두께 0.9 m — 구멍 안쪽도 괴물 손이 닿는다.
    private static void ShellWalls(Transform root, Meta meta)
    {
        float r = meta.ring_radius;
        float doorHalf = meta.door[0] / 2f, holeHalf = meta.hole != null && meta.hole.Length > 0 ? meta.hole[0] / 2f : 0.7f;
        Ring(root, "COL_Wall", 72, a => new Vector2(r, r + ShellWallThickness),
            p => (p.z > 0f && Mathf.Abs(p.x) < doorHalf) || (p.z < 0f && Mathf.Abs(p.x) < holeHalf));
        // 구멍 위 문틀(쿠키 키 2.6 m 위는 막힘) — 구멍 폭 그대로 위쪽만
        Box(root, "COL_HoleLintel", new Vector3(0f, (meta.hole[1] + WallHeight) / 2f, -(r + ShellWallThickness / 2f)), new Vector3(holeHalf * 2f + 0.4f, WallHeight - meta.hole[1], ShellWallThickness), 0f);
        // 구멍 자체: 괴물만 막는 보이지 않는 벽(쿠키는 지나감, CookieHole). 실제 괴물 충돌체는 작아 1.4 m 틈도 지나가기 때문
        Transform hole = Child(root, "COL_CookieHole");
        hole.localPosition = new Vector3(0f, meta.hole[1] / 2f, -(r + ShellWallThickness / 2f));
        hole.gameObject.AddComponent<BoxCollider>().size = new Vector3(holeHalf * 2f + 0.3f, meta.hole[1], ShellWallThickness);
        hole.gameObject.AddComponent<CookieHole>();
        Box(root, "COL_DoorLintel", new Vector3(0f, (meta.door[1] + WallHeight) / 2f, r + ShellWallThickness / 2f), new Vector3(doorHalf * 2f + 0.4f, WallHeight - meta.door[1], ShellWallThickness), 0f);
    }

    // 재료 건물(§28.4): 겉 건물 바닥 둘레 ~ 내부 방 벽 사이를 두꺼운 고리로 채우고, 앞·뒤 문 자리(폭 6.6 m)만 비운다.
    // 가운데 받침대(수리 자리)는 2.2 m보다 높은 기둥(위에 못 올라섬, S2).
    private static void BuildingWalls(Transform root, Meta meta)
    {
        float[] nonzero = meta.outer_radii.Where(x => x > 1f).OrderBy(x => x).ToArray();
        float outer = nonzero.Length > 0 ? nonzero[nonzero.Length / 3] * 0.92f : 10f;
        float a = Mathf.Min(meta.inner_half[0] - 1.1f, outer - 1.5f), b = Mathf.Min(meta.inner_half[1] - 1.1f, outer - 1.5f);
        float doorHalf = meta.door[0] / 2f;
        Ring(root, "COL_Wall", 96, ang =>
        {
            float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
            float rin = 1f / Mathf.Sqrt(c * c / (a * a) + s * s / (b * b)); // 타원 반지름
            return new Vector2(Mathf.Min(rin, outer - 0.6f), outer);
        }, p => Mathf.Abs(p.x) < doorHalf);
        // 문 위 막음(괴물 겉모습 높이보다 위) — 지붕 위로 못 감
        foreach (int s in new[] { -1, 1 })
            Box(root, s > 0 ? "COL_FrontLintel" : "COL_BackLintel", new Vector3(0f, (meta.door[1] + WallHeight) / 2f, s * (outer + b) / 2f),
                new Vector3(doorHalf * 2f + 0.6f, WallHeight - meta.door[1], outer - b), 0f);
        float px = meta.pedestal[0], pz = meta.pedestal[1], top = meta.pedestal[2], pr = meta.pedestal[3];
        var ped = Child(root, "COL_Pedestal").gameObject.AddComponent<CapsuleCollider>();
        ped.radius = Mathf.Clamp(pr * 0.8f, 1f, 2f);
        ped.height = Mathf.Max(top, 2.4f) + ped.radius;
        ped.center = new Vector3(px, ped.height / 2f, pz);

        // 수리 자리: 받침대 앞(문 쪽 +Z) 쿠키 손 높이. 받침대 꼭대기는 손이 닿지 않는다(높이 차 2.5 m).
        Transform repair = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Repair_Point"));
        if (repair == null) repair = Child(root, "Repair_Point");
        repair.localPosition = new Vector3(px, RepairHeight, pz + ped.radius + 0.5f);
        repair.localRotation = Quaternion.identity;
        Transform socket = Primitive(root, "Socket_Bld", PrimitiveType.Cylinder, repair.localPosition + Vector3.down * 0.25f, new Vector3(1.4f, 0.08f, 1.4f), LoadMaterial(SocketMaterial));
        socket.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 가동 빛기둥: 받침대에서 지붕을 뚫고 위로(밖에서도 보이게)
        Primitive(root, "ESC_Glow", PrimitiveType.Cylinder, new Vector3(px, top + 13f, pz), new Vector3(0.9f, 13f, 0.9f), LoadMaterial(GlowMaterial));
        var light = Child(root, "Light_Inside").gameObject.AddComponent<Light>();
        light.transform.localPosition = new Vector3(0f, 6.5f, 0f);
        light.type = LightType.Point;
        light.range = 18f;
        light.intensity = 1.7f;
        light.color = new Color(1f, 0.82f, 0.7f);
        light.shadows = LightShadows.None;
    }

    private static Transform Primitive(Transform root, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    // 둥근 벽: 각도마다 (안 반지름, 바깥 반지름)의 상자를 이어 붙인다(겹쳐서 틈 없음, S10). gap(점)이 참인 조각은 비운다.
    private static void Ring(Transform root, string prefix, int segments, Func<float, Vector2> radii, Func<Vector3, bool> gap)
    {
        Transform parent = Child(root, prefix + "s");
        float step = Mathf.PI * 2f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * step, a1 = a0 + step, am = a0 + step / 2f;
            Vector2 r = radii(am);
            bool skip = false;
            foreach (float a in new[] { a0, am, a1 })
                foreach (float rr in new[] { r.x, (r.x + r.y) / 2f, r.y })
                    if (gap(new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr))) skip = true;
            if (skip) continue;
            float mid = (r.x + r.y) / 2f;
            var dir = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am));
            float chord = 2f * r.y * Mathf.Sin(step / 2f) + 0.25f;
            Transform t = Child(parent, $"{prefix}_{i:00}");
            t.localPosition = dir * mid + Vector3.up * WallHeight / 2f;
            t.localRotation = Quaternion.LookRotation(dir);
            t.gameObject.AddComponent<BoxCollider>().size = new Vector3(chord, WallHeight, Mathf.Max(0.4f, r.y - r.x));
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
