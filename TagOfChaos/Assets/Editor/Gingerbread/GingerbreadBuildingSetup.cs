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

    private static readonly string[] RuneColors = { "Yellow", "Red", "Pink", "Blue" }; // 파일 이름(GIN_RUNE_Pink). 초록 룬 변형은 RuneGreenSetup이 Var_Rune_Green으로 바꾼다(§69 D5)

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

    private static string Group(string nm) => nm.StartsWith("GIN_RUNE_") ? "Rune" : nm.StartsWith("GIN_UG_") ? "Underground" : nm.StartsWith("GIN_HD_") ? "Hide" : "Ground";   // GIN_HD_ = 숨는 건물(§54)
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
        OpenAwayFromPresser(inst.transform);
        FixDoorways(inst.transform, nm);
        return root;
    }

    // ---------------- 문 통로 손질(§54, 2026-11-04 사용자 보고) ----------------
    // ① 충돌 껍질(COL_*_Shell, 9k tris로 줄인 몸)에 문 통로를 가로지르는 얇은 조각이 남아 쿠키 몸(캡슐)이 걸렸다 — 광선은 빠져나가
    //    Blender 통행 검사에서 못 잡았다. 문 통로 안(아치 아래, 바닥 0.3 m 위, 문 앞뒤 3.5 m)의 껍질 삼각형을 지운다.
    // ② 문틀(아치 블록·문설주)의 안쪽 면이 몸을 깎은 통로 면과 같은 자리(반지름 3.3 m)에 있어 문을 열면 Z-Fighting이 보였다 —
    //    문틀의 그 면 꼭짓점을 통로 쪽으로 5 cm 당긴다.
    // 메시는 FBX 안 것이라 고칠 수 없으므로 고친 사본을 Generated/Doorways에 저장해 쓴다.
    private const float DoorHalf = DoorWidth / 2f, DoorSpring = 0.12f + 7.6f - DoorWidth / 2f;   // 바닥(FLOOR_Z 0.12) + 문 높이 7.6 − 반폭 = 아치 시작 높이
    private const string DoorwayMeshDir = RootDir + "/Generated/Doorways";

    // 건물 문은 누른 사람 반대쪽으로 연다(Request1009Plan.md §56): 밖에서 누르면 안쪽, 안에서 누르면 바깥쪽.
    // §55.2에서 바깥 열림 파라미터를 비워 안쪽으로만 열게 했더니, 안에서 연 문짝이 누른 쿠키 쪽으로 젖혀져 "밖에서 연 것처럼" 보이고 길을 막았다.
    // 바깥으로 연 문짝이 건물 앞 길을 조금 막는 것은 받아들인다(사용자 결정) — 문 사이 통로 폭은 그대로라 쿠키·괴물이 지나간다.
    internal static void OpenAwayFromPresser(Transform inst)
    {
        foreach (InteractableDoor d in inst.GetComponentsInChildren<InteractableDoor>(true))
        {
            var so = new SerializedObject(d);
            so.FindProperty("outwardParameter").stringValue = InteractableDoor.DefaultOutwardParameter;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // 다시 만들지 않고 기존 건물 프리팹 문만 고친다(§56).
    [MenuItem("Tools/TagOfChaos/Gingerbread/Doors Open Away From Presser")]
    public static void FixPrefabDoorDirection()
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab GIN_", new[] { PrefabDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root.GetComponentsInChildren<InteractableDoor>(true).Length > 0)
            {
                OpenAwayFromPresser(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                n++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"[GingerbreadBuildingSetup] Doors open away from the presser in {n} prefabs.");
    }

    private static void FixDoorways(Transform inst, string nm)
    {
        var doors = inst.GetComponentsInChildren<InteractableDoor>(true).Select(d => d.transform).ToList();
        if (doors.Count == 0) return;
        foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("COL_") && t.name.EndsWith("_Shell"))
            {
                var mc = t.GetComponent<MeshCollider>();
                if (mc != null && mc.sharedMesh != null) mc.sharedMesh = CleanShell(mc.sharedMesh, t, doors, nm);
            }
            else if (t.name == nm + "_Body")
            {
                var bf = t.GetComponent<MeshFilter>();
                if (bf != null && bf.sharedMesh != null) bf.sharedMesh = StripPassageFloor(bf.sharedMesh, t, doors, nm);
            }
            else if (t.name == nm + "_Frame")
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                mf.sharedMesh = InsetFrame(mf.sharedMesh, t, doors, nm);
                var own = doors.Where(d => HasOriginalArch(nm, d.name)).ToList();
                if (own.Count > 0) mf.sharedMesh = StripFrame(mf.sharedMesh, t, own, nm);
                SillLikeFrame(t.GetComponent<MeshRenderer>(), mf.sharedMesh);
            }
        }
    }

    // 문 좌표: x = 문 가운데에서 옆(−3.3..3.3), y = 바닥에서 높이, z = 문짝 선에서 앞뒤
    private static Vector3 DoorLocal(Transform door, Vector3 world) { Vector3 l = door.InverseTransformPoint(world); return new Vector3(l.x - DoorHalf, l.y, l.z); }

    private static Mesh CleanShell(Mesh src, Transform owner, List<Transform> doors, string nm)
    {
        Vector3[] v = src.vertices; int[] tri = src.triangles;
        var keep = new List<int>(tri.Length); int removed = 0;
        for (int i = 0; i < tri.Length; i += 3)
        {
            Vector3 w = owner.TransformPoint((v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f);
            bool inTunnel = doors.Any(d =>
            {
                Vector3 l = DoorLocal(d, w);
                if (Mathf.Abs(l.x) > DoorHalf - 0.12f || l.z * OutSign(d, owner) > 9f || l.z * OutSign(d, owner) < -3.5f || l.y < 0.3f) return false;   // 문짝 바깥 9 m(원본 아치 문은 문짝이 벽 속으로 들어가 있다, §55)
                float top = DoorSpring + Mathf.Sqrt(Mathf.Max(0f, DoorHalf * DoorHalf - l.x * l.x));
                return l.y < top - 0.1f;
            });
            if (inTunnel) { removed++; continue; }
            keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]);
        }
        if (removed > 0) Debug.Log($"[GinBuildings] {nm}: removed {removed} collider triangles inside doorways");
        var m = new Mesh { name = src.name + "_Doorways", indexFormat = src.indexFormat };
        m.vertices = v; m.triangles = keep.ToArray(); m.RecalculateNormals(); m.RecalculateBounds();
        return SaveGenerated(m);
    }

    private static Mesh InsetFrame(Mesh src, Transform owner, List<Transform> doors, string nm)
    {
        const float inset = 0.05f, band = 0.12f;
        Vector3[] v = src.vertices; int moved = 0;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 w = owner.TransformPoint(v[i]);
            foreach (Transform d in doors)
            {
                Vector3 l = DoorLocal(d, w);
                if (Mathf.Abs(l.z) > 1.6f) continue;
                Vector3 nl = l;
                if (l.y < DoorSpring && Mathf.Abs(Mathf.Abs(l.x) - DoorHalf) < band) nl.x -= Mathf.Sign(l.x) * inset;   // 문설주 안쪽 면
                else if (l.y >= DoorSpring - 0.1f)
                {
                    var r = new Vector2(l.x, l.y - DoorSpring);
                    if (Mathf.Abs(r.magnitude - DoorHalf) < band && r.sqrMagnitude > 1e-4f) { Vector2 n2 = r.normalized * (r.magnitude - inset); nl.x = n2.x; nl.y = n2.y + DoorSpring; }   // 아치 안쪽 면
                }
                if (nl == l) continue;
                w = d.TransformPoint(new Vector3(nl.x + DoorHalf, nl.y, nl.z));
                v[i] = owner.InverseTransformPoint(w); moved++;
                break;
            }
        }
        var m = Object.Instantiate(src); m.name = src.name + "_Inset"; m.vertices = v; m.RecalculateBounds();
        if (moved > 0) Debug.Log($"[GinBuildings] {nm}: moved {moved} door-frame vertices off the doorway faces");
        return SaveGenerated(m);
    }

    // 원본 모델에 아치 문 자리가 새겨진 문(§55): 새 아치·문설주 블록이 원본 아치 안쪽에 겹쳐 보인다 — 그 문은 새 문틀을 빼고 원본 아치를 문틀로 쓴다.
    // 새 문틀을 남기는 문: 원본 문 둘레에 아치가 없는 방앗간·해골 가마솥 앞문(빼면 자른 자리가 들쭉날쭉), 원본에 문이 없던 뒷문(빼면 평평한 판만 남음).
    private static readonly string[] OriginalArchFronts = { "GIN_HD_", "GIN_RUNE_", "GIN_UG_", "GIN_BLD_CandyCaneCottage", "GIN_BLD_CandyCogCottage", "GIN_BLD_ChocoCottage" };
    private static readonly string[] OriginalArchBacks = { "GIN_RUNE_", "GIN_UG_CandyCaneCottage", "GIN_UG_CrookedChocoCottage", "GIN_UG_GearworksGate", "GIN_UG_MacaronCottage", "GIN_BLD_CandyCogCottage" };
    private static bool HasOriginalArch(string nm, string door) =>
        door == "Door_Front" ? OriginalArchFronts.Any(nm.StartsWith) : door == "Door_Back" && OriginalArchBacks.Any(nm.StartsWith);

    // 문 로컬 z의 바깥 방향 부호(+1이면 로컬 +z가 건물 밖): 건물 뿌리(가운데) 쪽이 안
    private static float OutSign(Transform door, Transform owner)
    {
        Transform root = owner; while (root.parent != null && root.parent.parent != null) root = root.parent;
        return door.InverseTransformPoint(root.position).z > 0f ? -1f : 1f;
    }

    // 문짝 바깥 통로 바닥(§55.4): 원본 문 자리 홈 안에 깐 통로 바닥(밝은 문설주 재질)이 문 앞·옆으로 밝은 판처럼 보였다.
    // 맵 바닥이 그 아래에 있으므로 문짝 바깥(0.2~12 m)·문 폭 ± 1.5 m 안의 위를 향한 낮은 면(높이 0.35 m 아래)을 지운다(충돌체는 따로라 그대로).
    private static Mesh StripPassageFloor(Mesh src, Transform owner, List<Transform> doors, string nm)
    {
        Vector3[] v = src.vertices; int removed = 0;
        var m = Object.Instantiate(src); m.name = src.name + "_Floorless";
        Material[] mats = owner.GetComponent<MeshRenderer>() != null ? owner.GetComponent<MeshRenderer>().sharedMaterials : new Material[0];
        for (int sub = 0; sub < src.subMeshCount; sub++)
        {
            int[] tri = src.GetTriangles(sub); var keep = new List<int>(tri.Length);
            bool added = sub < mats.Length && mats[sub] != null && mats[sub].name.StartsWith("M_GIN_") && !mats[sub].name.Contains("Exterior");   // 덧댄 면만(원본 겉 재질은 건드리지 않는다)
            if (!added) { m.SetTriangles(tri, sub); continue; }
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = owner.TransformPoint(v[tri[i]]), b = owner.TransformPoint(v[tri[i + 1]]), c = owner.TransformPoint(v[tri[i + 2]]);
                Vector3 w = (a + b + c) / 3f; Vector3 n = Vector3.Cross(b - a, c - a);
                bool floor = n.sqrMagnitude > 1e-12f && Mathf.Abs(n.normalized.y) > 0.7f && w.y - owner.root.position.y < 0.35f;
                if (floor && doors.Any(d => { Vector3 l = DoorLocal(d, w); float o = l.z * OutSign(d, owner); return Mathf.Abs(l.x) < DoorHalf + 1.5f && o > 0.2f && o < 12f; }))
                { removed++; continue; }
                keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]);
            }
            m.SetTriangles(keep, sub);
        }
        m.RecalculateBounds();
        if (removed > 0) Debug.Log($"[GinBuildings] {nm}: removed {removed} passage-floor triangles in front of the doors");
        return SaveGenerated(m);
    }

    // 새 문틀 아래 문턱 돌판(밝은 분홍 돌)이 둥근 벽 옆으로 밝게 보였다(§55.4) — 문턱 재질 칸을 문틀(가장 많이 쓴 재질)과 같게 한다
    private static void SillLikeFrame(MeshRenderer r, Mesh m)
    {
        if (r == null || m == null) return;
        Material[] mats = r.sharedMaterials;
        int sill = System.Array.FindIndex(mats, x => x != null && x.name.Contains("stone_lilac"));
        if (sill < 0) return;
        int best = -1, most = -1;
        for (int i = 0; i < mats.Length && i < m.subMeshCount; i++)
        {
            if (i == sill || mats[i] == null) continue;
            int n = (int)m.GetIndexCount(i);
            if (n > most) { most = n; best = i; }
        }
        if (best < 0) return;
        mats[sill] = mats[best];
        r.sharedMaterials = mats;
    }

    // 문 둘레(옆 5.4 m·앞뒤 3 m 안, 바닥 0.2 m 위)의 문틀 삼각형을 지운다 — 문턱 돌판(높이 0.14 m)은 남긴다
    private static Mesh StripFrame(Mesh src, Transform owner, List<Transform> doors, string nm)
    {
        Vector3[] v = src.vertices; int removed = 0;
        var m = Object.Instantiate(src); m.name = src.name.Replace("_Inset", "") + "_Strip";
        for (int sub = 0; sub < src.subMeshCount; sub++)
        {
            int[] tri = src.GetTriangles(sub); var keep = new List<int>(tri.Length);
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 w = owner.TransformPoint((v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f);
                bool near = doors.Any(d => { Vector3 l = DoorLocal(d, w); float o = l.z * OutSign(d, owner); return Mathf.Abs(l.x) < 5.4f && o > -3f && o < 9f; });   // 문짝 바깥 9 m까지, 문턱 돌판도(통로 바닥이 있다 — 밝은 돌판이 원본 아치 옆 틈으로 보였다, §55.4)
                if (near) { removed++; continue; }
                keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]);
            }
            m.SetTriangles(keep, sub);
        }
        m.RecalculateBounds();
        Debug.Log($"[GinBuildings] {nm}: removed {removed} new door-frame triangles where the original arch frames the door ({string.Join(", ", doors.Select(d => d.name))})");
        return SaveGenerated(m);
    }

    private static Mesh SaveGenerated(Mesh m)
    {
        Directory.CreateDirectory(DoorwayMeshDir);
        string path = $"{DoorwayMeshDir}/{m.name}.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old == null) { AssetDatabase.CreateAsset(m, path); return m; }
        EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); EditorUtility.SetDirty(old);
        return old;
    }

    // 재료 건물 빛기둥(§54): ESC_Glow(방 안 빛)는 지붕에 가려 밖에서 보이지 않는다 — 같은 ESC_Glow 아래에 지붕 위에서 하늘로 솟는
    // 빛기둥(Beacon 셰이더, 안개 무시)을 붙인다. QuadrantBuilding이 ESC_Glow를 켜고 끄므로 함께 켜지고 꺼진다.
    private static void AddRoofBeam(GameObject building)
    {
        Transform glow = FindDeep(building.transform, "ESC_Glow");
        if (glow == null) return;
        float top = 0f;
        foreach (Renderer r in building.GetComponentsInChildren<Renderer>(true)) if (!r.transform.IsChildOf(glow)) top = Mathf.Max(top, r.bounds.max.y);
        Transform old = glow.Find("RoofBeam");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var beam = new GameObject("RoofBeam").transform;
        beam.SetParent(glow, false);
        beam.position = new Vector3(glow.position.x, building.transform.position.y + top - 1f, glow.position.z);
        beam.rotation = Quaternion.identity;
        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        string folder = RootDir + "/Generated";
        Directory.CreateDirectory(folder);
        var gold = new Color(1f, 0.82f, 0.35f);
        const float h = 110f;
        RoundMapKit.BeaconPart(beam, "Beam_Core", cylinder, RoundMapKit.BeaconMaterial(folder, "GIN_RuneBeamCore", gold * 1.6f, 0f, 1.2f, 1.2f), Vector3.up * h / 2f, Quaternion.identity, new Vector3(2.2f, h / 2f, 2.2f));
        RoundMapKit.BeaconPart(beam, "Beam_Haze", cylinder, RoundMapKit.BeaconMaterial(folder, "GIN_RuneBeamHaze", gold * 0.55f, 0f, 0.8f, 2.5f), Vector3.up * h / 2f, Quaternion.identity, new Vector3(7f, h / 2f, 7f));
        // 물체 크기 보정: 부모 축척이 있으면 나눈다
        Vector3 ls = glow.lossyScale;
        if (Mathf.Abs(ls.x - 1f) > 1e-3f || Mathf.Abs(ls.y - 1f) > 1e-3f) beam.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
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
        Transform leafR = MakeLeaf(door.transform, "Leaf_R", r, new Vector3(across.magnitude, 0f, 0f));
        // 활짝 연 문짝이 통로 옆벽에 반쯤 묻혀 Z-Fighting이 보였다(§54) — 경첩을 문 안쪽으로 18 cm 옮기고 문짝 폭을 그만큼 줄인다
        // (닫으면 문 폭을 그대로 채우고, 열면 벽에서 6 cm 떨어진다). 애니메이션은 회전만 바꾸므로 위치·축척은 그대로 남는다.
        const float hingeIn = 0.18f;
        float half = across.magnitude / 2f, k = (half - hingeIn) / half;
        leafL.localPosition += Vector3.right * hingeIn; leafL.localScale = new Vector3(k, 1f, 1f);
        leafR.localPosition -= Vector3.right * hingeIn; leafR.localScale = new Vector3(k, 1f, 1f);
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
        AddRoofBeam(root);
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
