using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 진저브레드 마을 큰 건물(Request1009Plan.md §50) 가져오기·프리팹 도구.
// Blender에서 만든 건물(지상 5·지하 5·룬 4)은 프로젝트 밖 GinStaging/에 올라온다(MCP 전송). 이 도구가
//  1) Assets로 옮기고 가져오기 설정(겉 텍스처 4096·노멀맵·ms 만들기·FBX 재질 이름 연결)을 하고,
//  2) 재질·프리팹(충돌체·여닫는 양문·조명)을 만들고,
//  3) 룬 건물 4채 + 기본(무채색) 건물을 하나의 사분면 건물 프리팹으로 묶는다(§50 B안: 쿠키에게만 판의 룬 색 건물이 보임).
// 건물 FBX 안의 이름 규칙(Blender s2_interior.py):
//  <이름>(몸)·<이름>_Frame·<이름>_Interior·<이름>_Door_FL/FR/BL/BR(경첩 원점)·COL_<이름>_Shell/Props(충돌 전용)·LIGHT_<이름>_<k>(빛 자리)
//  룬 건물만: Socket_Rune·Repair_Point·ESC_Glow(QuadrantBuilding이 이름 앞부분으로 찾는다).
public static class GingerbreadBuildingSetup
{
    private const string StagingDir = "GinStaging";
    private const string RootDir = "Assets/09. Environment/GingerbreadBuildings";
    private const string SharedTexDir = RootDir + "/Shared/Textures";
    private const string MaterialDir = RootDir + "/Materials";
    private const string PrefabDir = "Assets/04. Prefabs/Environment/GingerbreadBuildings";
    private const string DoorController = "Assets/Maps/Common/Doors/MapDoor.controller";
    private const float DoorWidth = 6.6f;          // Blender cfg.DOOR_W (양문 전체 폭)
    private const float DoorInteractRange = 3.4f;  // 맵 문과 같음

    private static readonly string[] RuneColors = { "Yellow", "Red", "Pink", "Blue" };

    // ---------------- 1. 가져오기 ----------------

    [MenuItem("Tools/TagOfChaos/Gingerbread/1. Import Staged Buildings")]
    public static void ImportStaged()
    {
        string staging = Path.GetFullPath(StagingDir);
        if (!Directory.Exists(staging)) { Debug.LogError("[GinBuildings] staging folder missing: " + staging); return; }
        Directory.CreateDirectory(SharedTexDir);
        int copied = 0;
        foreach (string f in Directory.GetFiles(Path.Combine(staging, "Shared")))
        {
            string name = Path.GetFileName(f);
            string dst = name.EndsWith(".json") ? Path.Combine(RootDir, "Shared", name) : Path.Combine(SharedTexDir, name);
            copied += CopyIfChanged(f, dst);
        }
        foreach (string d in Directory.GetDirectories(Path.Combine(staging, "Buildings")))
        {
            string nm = Path.GetFileName(d);
            string dst = BuildingDir(nm);
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(d)) copied += CopyIfChanged(f, Path.Combine(dst, Path.GetFileName(f)));
            MakeMsFromRough(dst, nm);
        }
        AssetDatabase.Refresh();
        ApplyImportSettings();
        Debug.Log($"[GinBuildings] imported {copied} changed files");
    }

    internal static int CopyIfChanged(string src, string dst)
    {
        if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(src).Length && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src)) return 0;
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.Copy(src, dst, true);
        return 1;
    }

    private static string Group(string nm) => nm.StartsWith("GIN_RUNE_") ? "Rune" : nm.StartsWith("GIN_UG_") ? "Underground" : "Ground";
    private static string BuildingDir(string nm) => $"{RootDir}/{Group(nm)}/{nm}";

    // 거칠기(회색 JPG) → ms(PNG, R = 금속 0, A = 매끄러움 1 − 거칠기). 전송량을 줄이려고 Unity에서 만든다.
    private static void MakeMsFromRough(string dir, string nm)
    {
        string rough = Path.Combine(dir, nm + "_rough.jpg");
        string ms = Path.Combine(dir, nm + "_ms.png");
        if (!File.Exists(rough) || (File.Exists(ms) && File.GetLastWriteTimeUtc(ms) >= File.GetLastWriteTimeUtc(rough))) return;
        var src = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
        src.LoadImage(File.ReadAllBytes(rough));
        Color32[] px = src.GetPixels32();
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, (byte)(255 - px[i].r));
        var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
        dst.SetPixels32(px); dst.Apply();
        File.WriteAllBytes(ms, dst.EncodeToPNG());
        Object.DestroyImmediate(src); Object.DestroyImmediate(dst);
    }

    private static void ApplyImportSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { RootDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            string n = Path.GetFileNameWithoutExtension(p);
            bool building = !p.StartsWith(SharedTexDir);
            bool changed = false;
            TextureImporterType type = n.EndsWith("_normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != type) { ti.textureType = type; changed = true; }
            bool srgb = !(n.EndsWith("_normal") || n.EndsWith("_ms") || n.EndsWith("_rough"));
            if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; changed = true; }
            int max = building ? 4096 : 1024;
            if (ti.maxTextureSize != max) { ti.maxTextureSize = max; changed = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; changed = true; }
            if (ti.textureCompression != TextureImporterCompression.CompressedHQ && building) { ti.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }
            if (ti.anisoLevel != 4) { ti.anisoLevel = 4; changed = true; }
            if (changed) ti.SaveAndReimport();
        }
    }

    // ---------------- 2. 재질·프리팹 ----------------

    [System.Serializable] internal class GinMat { public float metal; public float smooth; public float[] emission; public float emissionStrength; public float[] color; }

    internal static Dictionary<string, GinMat> LoadMatTable()
    {
        string p = Path.Combine(RootDir, "Shared", "gin_materials.json");
        var table = new Dictionary<string, GinMat>();
        if (!File.Exists(p)) return table;
        // JsonUtility는 사전을 못 읽으므로 "이름": {…} 를 하나씩 잘라 읽는다.
        string s = File.ReadAllText(p).Trim().TrimStart('{').TrimEnd('}');
        int i = 0;
        while (i < s.Length)
        {
            int q0 = s.IndexOf('"', i); if (q0 < 0) break;
            int q1 = s.IndexOf('"', q0 + 1);
            int b0 = s.IndexOf('{', q1); int b1 = s.IndexOf('}', b0);
            table[s.Substring(q0 + 1, q1 - q0 - 1)] = JsonUtility.FromJson<GinMat>(s.Substring(b0, b1 - b0 + 1).Replace("null", "[]"));
            i = b1 + 1;
        }
        return table;
    }

    private static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    private static Material GetOrCreateMaterial(string matName, Dictionary<string, GinMat> table)
    {
        Directory.CreateDirectory(MaterialDir);
        string path = $"{MaterialDir}/{matName}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        if (matName.EndsWith("_Exterior"))
        {
            string nm = matName.Substring(2, matName.Length - 2 - "_Exterior".Length);   // M_<nm>_Exterior
            string texNm = nm, dir = BuildingDir(nm);
            string albedo = $"{dir}/{nm}_albedo.jpg", emission = $"{dir}/{nm}_emission.jpg";
            if (nm == "GIN_RUNE_Neutral") { texNm = "GIN_RUNE_Yellow"; dir = BuildingDir(texNm); }   // 노멀·ms는 노랑 것을 쓴다
            SetStandard(m, Tex(albedo), Tex($"{dir}/{texNm}_normal.jpg"), Tex($"{dir}/{texNm}_ms.png"), 0f, 1f, Color.white, Tex(emission), Color.white * 1.2f);
        }
        else if (matName.StartsWith("M_GIN_"))
        {
            string key = matName.Substring("M_GIN_".Length);
            table.TryGetValue(key, out GinMat g);
            Color baseCol = g != null && g.color != null && g.color.Length >= 3 ? new Color(g.color[0], g.color[1], g.color[2]) : Color.white;
            Texture2D a = Tex($"{SharedTexDir}/{key}_albedo.png");
            Color emis = g != null && g.emission != null && g.emission.Length >= 3 ? new Color(g.emission[0], g.emission[1], g.emission[2]) * Mathf.Max(1f, g.emissionStrength * 0.35f) : Color.black;
            Texture2D emisMap = Tex($"{SharedTexDir}/{key}_emission.png");   // 시계판·금 간 빛 돌처럼 발광 텍스처가 따로 있는 것
            if (emisMap != null && emis.maxColorComponent <= 0f) emis = Color.white * 1.3f;
            if (emisMap == null && emis.maxColorComponent > 0f && a != null) emisMap = a;
            SetStandard(m, a, Tex($"{SharedTexDir}/{key}_normal.png"), null, g != null ? g.metal : 0f, g != null ? g.smooth : 0.3f, a != null ? Color.white : baseCol,
                emisMap, emis);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void SetStandard(Material m, Texture2D albedo, Texture2D normal, Texture2D ms, float metal, float smooth, Color color, Texture2D emissionMap, Color emission)
    {
        m.shader = Shader.Find("Standard");
        m.SetTexture("_MainTex", albedo); m.SetColor("_Color", color);
        m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f);
        if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", ms);
        if (ms != null) { m.EnableKeyword("_METALLICGLOSSMAP"); m.SetFloat("_GlossMapScale", 1f); }
        else { m.DisableKeyword("_METALLICGLOSSMAP"); m.SetFloat("_Metallic", metal); m.SetFloat("_Glossiness", smooth); }
        bool emits = emission.maxColorComponent > 0.001f;
        m.SetTexture("_EmissionMap", emissionMap); m.SetColor("_EmissionColor", emits ? emission : Color.black);
        if (emits) { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        else { m.DisableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; }
    }

    // FBX 안 재질 이름 → 만든 재질로 연결(재질을 FBX에 묻지 않는다)
    internal static void RemapFbxMaterials(string fbxPath, Dictionary<string, GinMat> table)
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
        bool changed = false;
        if (mi.materialImportMode != ModelImporterMaterialImportMode.ImportStandard) { mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; changed = true; }
        if (mi.importLights) { mi.importLights = false; changed = true; }
        if (mi.importCameras) { mi.importCameras = false; changed = true; }
        if (mi.importAnimation) { mi.importAnimation = false; changed = true; }
        if (changed) mi.SaveAndReimport();
        var names = new HashSet<string>();
        foreach (Renderer r in AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath).GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials) if (m != null) names.Add(m.name);
        var map = mi.GetExternalObjectMap();
        bool remapped = false;
        foreach (string n in names)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), n);
            Material target = GetOrCreateMaterial(n, table);
            if (map.TryGetValue(id, out Object cur) && cur == target) continue;
            mi.AddRemap(id, target); remapped = true;
        }
        if (remapped) mi.SaveAndReimport();
    }

    [MenuItem("Tools/TagOfChaos/Gingerbread/2. Build Building Prefabs")]
    public static void BuildPrefabs()
    {
        var table = LoadMatTable();
        Directory.CreateDirectory(PrefabDir);
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { RootDir }))
        {
            string fbx = AssetDatabase.GUIDToAssetPath(guid);
            string nm = Path.GetFileNameWithoutExtension(fbx);
            if (!nm.StartsWith("GIN_")) continue;
            RemapFbxMaterials(fbx, table);
            if (nm.StartsWith("GIN_RUNE_")) continue;   // 룬은 3단계에서 묶는다
            GameObject root = BuildOne(fbx, nm, out _);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{nm}.prefab");
            Object.DestroyImmediate(root);
            n++;
        }
        // 룬 기본 건물 재질(노랑 몸 + 무채색 텍스처)
        GetOrCreateMaterial("M_GIN_RUNE_Neutral_Exterior", table);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GinBuildings] built {n} building prefabs");
    }

    // FBX 하나를 꾸민 GameObject로: 충돌체·문·조명·정적 표시. bodyParts = 몸·문틀·내부(룬 변형에 쓴다)
    private static GameObject BuildOne(string fbxPath, string nm, out List<GameObject> bodyParts)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var root = new GameObject(nm);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.transform.SetParent(root.transform, false);
        // FBX 맨 위 물체가 하나면 유니티는 그 물체(몸)를 뿌리로 쓴다 — 몸 메시를 자식 <이름>_Body로 옮겨, 몸·문틀·내부만 따로 옮길 수 있게 한다
        var rootMf = inst.GetComponent<MeshFilter>();
        if (rootMf != null)
        {
            var b = new GameObject(nm + "_Body");
            b.transform.SetParent(inst.transform, false);
            b.AddComponent<MeshFilter>().sharedMesh = rootMf.sharedMesh;
            var rootMr = inst.GetComponent<MeshRenderer>();
            b.AddComponent<MeshRenderer>().sharedMaterials = rootMr.sharedMaterials;
            Object.DestroyImmediate(rootMr); Object.DestroyImmediate(rootMf);
        }
        foreach (string partName in new[] { nm + "_Frame", nm + "_Interior" })
        {
            Transform part = FindDeep(inst.transform, partName);
            if (part == null) continue;
            var kids = new List<Transform>();
            foreach (Transform k in part) kids.Add(k);
            foreach (Transform k in kids) k.SetParent(inst.transform, true);
        }
        bodyParts = new List<GameObject>();
        var all = inst.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).ToList();
        foreach (GameObject g in all)
        {
            if (g.name.StartsWith("COL_")) MakeCollider(g);
            else if (g.name.StartsWith("LIGHT_")) MakeLight(g, nm);
            else if (g.name == nm + "_Body" || g.name == nm + "_Frame" || g.name == nm + "_Interior") { bodyParts.Add(g); MarkStatic(g); }
            else if (g.name.StartsWith("Socket_")) { var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh; MarkStatic(g); }
        }
        Vector3 centre = RoomCentre(inst, nm);
        MakeDoor(inst.transform, nm, "F", "Front", centre);
        MakeDoor(inst.transform, nm, "B", "Back", centre);
        return root;
    }

    private static Vector3 RoomCentre(GameObject inst, string nm)
    {
        Transform body = FindDeep(inst.transform, nm + "_Body");
        if (body == null) body = inst.transform;
        var r = body.GetComponent<Renderer>();
        Vector3 c = r != null ? r.bounds.center : body.position;
        return new Vector3(c.x, body.position.y, c.z);
    }

    internal static void MakeCollider(GameObject g)
    {
        var mf = g.GetComponent<MeshFilter>();
        if (mf == null) return;
        var mc = g.GetComponent<MeshCollider>();   // 유니티 null은 널 병합 연산자로 거를 수 없다
        if (mc == null) mc = g.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        var mr = g.GetComponent<MeshRenderer>();
        if (mr != null) Object.DestroyImmediate(mr);
        Object.DestroyImmediate(mf);
        MarkStatic(g);
    }

    private static void MakeLight(GameObject g, string nm)
    {
        var l = g.GetComponent<Light>();
        if (l == null) l = g.AddComponent<Light>();
        l.type = LightType.Point; l.range = 11f; l.intensity = 1.5f; l.shadows = LightShadows.None;
        bool ug = nm.StartsWith("GIN_UG_") || nm.Contains("SkullCauldron");
        l.color = ug ? new Color(0.85f, 0.6f, 1f) : new Color(1f, 0.82f, 0.6f);
    }

    internal static void MarkStatic(GameObject g)
    {
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
    }

    // 양문: 부모(Animator MapDoor + Rigidbody + InteractableDoor) 아래 Leaf_L(왼 경첩, 원점)·Leaf_R(오른 경첩, +X 6.6 m).
    // 열리는 쪽: MapDoor의 Open 클립은 Leaf_L을 −90° 돌린다 — 그때 문짝이 방 가운데 쪽으로 가면 inwardOpenAngle = −90, 아니면 +90.
    private static void MakeDoor(Transform inst, string nm, string side, string label, Vector3 centre)
    {
        Transform l = FindDeep(inst, $"{nm}_Door_{side}L"), r = FindDeep(inst, $"{nm}_Door_{side}R");
        if (l == null || r == null) return;
        MakeDoorPair(inst, l, r, $"{nm}_{label}", label, centre);
    }

    // 경첩 원점 문짝 두 개(l = 왼 경첩, r = 오른 경첩) → 여닫는 양문 하나
    internal static void MakeDoorPair(Transform inst, Transform l, Transform r, string doorId, string label, Vector3 centre)
    {
        var door = new GameObject("Door_" + label);
        door.transform.SetParent(inst, false);
        door.transform.position = l.position;
        Vector3 across = r.position - l.position; across.y = 0f;
        // 부모의 +X = 왼 경첩 → 오른 경첩(right = up × forward 이므로 forward = across × up)
        door.transform.rotation = Quaternion.LookRotation(Vector3.Cross(across.normalized, Vector3.up), Vector3.up);
        Transform leafL = MakeLeaf(door.transform, "Leaf_L", l, Vector3.zero);
        MakeLeaf(door.transform, "Leaf_R", r, new Vector3(across.magnitude, 0f, 0f));
        var anim = door.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(DoorController);
        var rb = door.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        // 안쪽 판정: Leaf_L을 −90° 돌려 본다
        var leafRenderer = leafL.GetComponentInChildren<Renderer>();
        Vector3 before = leafRenderer.bounds.center;
        leafL.localRotation = Quaternion.Euler(0f, -90f, 0f);
        Vector3 after = leafL.GetComponentInChildren<Renderer>().bounds.center;
        leafL.localRotation = Quaternion.identity;
        Vector3 toCentre = centre - before; toCentre.y = 0f;
        Vector3 moved = after - before; moved.y = 0f;
        float inward = Vector3.Dot(moved, toCentre) > 0f ? -90f : 90f;
        var idoor = door.AddComponent<InteractableDoor>();
        var so = new SerializedObject(idoor);
        so.FindProperty("doorId").stringValue = doorId;
        so.FindProperty("inwardOpenAngle").floatValue = inward;
        so.FindProperty("leafCollider").objectReferenceValue = leafL.GetComponent<Collider>();
        so.FindProperty("interactionRange").floatValue = DoorInteractRange;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform MakeLeaf(Transform door, string name, Transform mesh, Vector3 localPos)
    {
        var leaf = new GameObject(name).transform;
        leaf.SetParent(door, false); leaf.localPosition = localPos; leaf.localRotation = Quaternion.identity;
        mesh.SetParent(leaf, true);
        var r = mesh.GetComponent<Renderer>();
        var box = leaf.gameObject.AddComponent<BoxCollider>();
        Bounds b = r.bounds;   // 닫힌 문짝(월드) → 문짝 좌표
        Vector3 c = leaf.InverseTransformPoint(b.center);
        Vector3 s = leaf.InverseTransformVector(b.size);
        box.center = c; box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Max(0.3f, Mathf.Abs(s.z)));
        return leaf;
    }

    internal static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
        return null;
    }

    // ---------------- 3. 룬 건물(사분면) ----------------

    // GIN_RuneBuilding: Common(노랑 것: 충돌체·문·받침·Repair_Point·ESC_Glow·빛) + RuneVariants/Var_Rune_<색>(몸·문틀·내부) + Var_Neutral(노랑 몸 + 무채색 재질).
    // 사분면 Building_Q* 에 넣을 때는 QuadrantBuilding이 붙은 오브젝트의 자식으로 둔다(QuadrantBuilding이 이름 앞부분으로 찾는다).
    [MenuItem("Tools/TagOfChaos/Gingerbread/3. Build Rune Building Prefab")]
    public static void BuildRunePrefab()
    {
        var table = LoadMatTable();
        var root = new GameObject("GIN_RuneBuilding");
        var variants = new GameObject("RuneVariants").transform; variants.SetParent(root.transform, false);
        GameObject common = null;
        foreach (string c in RuneColors)
        {
            string nm = "GIN_RUNE_" + c;
            string fbx = $"{BuildingDir(nm)}/{nm}.fbx";
            if (!File.Exists(fbx)) { Debug.LogError("[GinBuildings] missing " + fbx); Object.DestroyImmediate(root); return; }
            RemapFbxMaterials(fbx, table);
            GameObject one = BuildOne(fbx, nm, out List<GameObject> parts);
            var v = new GameObject("Var_Rune_" + c).transform; v.SetParent(variants, false);
            foreach (GameObject p in parts) p.transform.SetParent(v, true);
            if (c == "Yellow")
            {
                // 기본(무채색) 건물 = 노랑 몸·문틀·내부 복제 + 몸 재질만 무채색
                var neutral = Object.Instantiate(v.gameObject, variants); neutral.name = "Var_Neutral";
                Material nmat = GetOrCreateMaterial("M_GIN_RUNE_Neutral_Exterior", table);
                foreach (Renderer r in neutral.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name.EndsWith("_Exterior")) ms[i] = nmat;
                    r.sharedMaterials = ms;
                }
                common = one; common.name = "Common";
                common.transform.SetParent(root.transform, false);
            }
            else Object.DestroyImmediate(one);
        }
        foreach (Transform v in variants) v.gameObject.SetActive(v.name == "Var_Neutral");
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/GIN_RuneBuilding.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[GinBuildings] built GIN_RuneBuilding.prefab");
    }

    // ---------------- 4. 맵에 룬 건물 넣기 ----------------

    // 열린 씬의 QuadrantBuilding(Building_Q1~Q4)마다: 기존 임시 건물 조각은 지우지 않고 꺼진 OldPlaceholder 아래로 옮기고
    // (QuadrantBuilding이 이름 앞부분으로 찾는 Socket_·Repair_Point·ESC_Glow는 Old_ 를 붙여 겹치지 않게), 새 룬 건물을 자식으로 넣는다.
    // 앞문은 맵 가운데 쪽(z)을 본다. 되돌리려면 GIN_RuneBuilding을 지우고 OldPlaceholder를 켜고 이름의 Old_ 를 뗀다.
    [MenuItem("Tools/TagOfChaos/Gingerbread/4. Install Rune Buildings In Open Scene")]
    public static void InstallRuneBuildings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[GinBuildings] not in Play Mode"); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/GIN_RuneBuilding.prefab");
        if (prefab == null) { Debug.LogError("[GinBuildings] build the rune prefab first"); return; }
        int n = 0;
        foreach (QuadrantBuilding qb in Object.FindObjectsByType<QuadrantBuilding>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform t = qb.transform;
            if (t.Find("GIN_RuneBuilding") != null) continue;
            var old = new GameObject("OldPlaceholder").transform;
            old.SetParent(t, false);
            var kids = new List<Transform>();
            foreach (Transform c in t) if (c != old) kids.Add(c);
            foreach (Transform c in kids)
            {
                c.SetParent(old, true);
                foreach (Transform g in c.GetComponentsInChildren<Transform>(true))
                    if (g.name.StartsWith("Socket_") || g.name.StartsWith("Repair_Point") || g.name.StartsWith("ESC_Glow")) g.name = "Old_" + g.name;
            }
            old.gameObject.SetActive(false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t);
            inst.transform.position = new Vector3(t.position.x, t.position.y, t.position.z);
            float yaw = t.position.z > 0f ? 0f : 180f;   // 프리팹 앞문 = +Z(Blender −Y). z > 0 사분면은 가운데(−z) 쪽을 보게 180°
            inst.transform.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);
            // 같은 프리팹 4채라 문 ID가 겹친다 — 씬 안에서 하나뿐이게 사분면 이름을 붙인다
            foreach (InteractableDoor d in inst.GetComponentsInChildren<InteractableDoor>(true))
            {
                var so = new SerializedObject(d);
                so.FindProperty("doorId").stringValue = $"{t.name}_{d.name.Replace("Door_", "")}";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Undo.RegisterCreatedObjectUndo(inst, "Install rune building");
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[GinBuildings] installed {n} rune buildings");
    }
}
