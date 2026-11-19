using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Heaven Of Bakery 베이커리 건물 7채(Request1009Plan.md §89.22): Meshy 건물을 Blender MCP로 다듬은 FBX(외관 + 내부 + 움직이는 부품 + 충돌체 + 표시점)를
// 프리팹으로 만든다. 폴더 Bakery/<이름>/ 에 <이름>.fbx · _albedo/_normal/_ms.png · <이름>.json(소품·다락·문 정보)이 있다.
//  - 외관 재질 M_<이름>_Ext(Standard: albedo·normal·ms — R 금속, A 매끄러움), 내부 재질 HOBHQ_<재질> → 공용 M_HQ_<재질>(HobHQBuilder).
//  - COL_* → MeshCollider만(렌더러 없음), 외관·내부는 정적, 움직이는 부품(_Anim_Blades → SpinPart, _Anim_Flag → FlagWave 재질, _Anim_Drip → TextureScroll)은 정적 아님.
//  - MK_Prop_<번호>_<프리팹> → HOB 소품 프리팹(자리·방향), MK_Light_<k> → 점광원, MK_Wheat_<k> → 흔들리는 밀(HOB_WheatPatch), MK_Socket → 재료 꽂는 자리.
//  - 축 바꾸기는 FBX 안의 MK_RefX/Y/Z(Blender (1,0,0)·(0,1,0)·(0,0,1))로 알아낸다(json의 Blender 좌표를 Unity 로컬로).
public static class HobBakeryBuildings
{
    public const string Root = "Assets/09. Environment/HeavenOfBakery/Bakery";
    public const string PrefabDir = HobPrefabBuilder.PrefabDir + "/Bakery";
    public static readonly string[] Names = { "HOB_BLD_FruitWarehouse", "HOB_BLD_JamFactory", "HOB_BLD_WheatMill", "HOB_BLD_AppleWorkshop", "HOB_BLD_AlmondFactory", "HOB_BLD_OrangeWorkshop", "HOB_BLD_ChocoFactory" };

    [System.Serializable] private class Prop { public string p; public float[] pos; public float yaw; public float s; } // s: 크기 배율(0 = 1, 좁은 방에서 줄인 주인공 소품)
    [System.Serializable] private class Info { public string name; public string key; public float f; public float[] dims; public float[] socket; public Prop[] props; }

    [MenuItem("Tools/TagOfChaos/Maps/Heaven Of Bakery/Build Bakery Building Prefabs")]
    public static void BuildMenu() => Debug.Log(BuildAll());

    public static string BuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "[HobBakery] stop Play Mode first";
        HobPrefabBuilder.EnsureFolder(PrefabDir);
        var log = new List<string>();
        foreach (string n in Names)
        {
            string dir = $"{Root}/{n}";
            if (!File.Exists($"{dir}/{n}.fbx")) { log.Add(n + ": no fbx"); continue; }
            log.Add(BuildOne(n, dir));
        }
        AssetDatabase.SaveAssets();
        return "[HobBakery] " + string.Join(" | ", log);
    }

    // ---------------- 텍스처·재질 ----------------
    private static void TexImport(string path, bool normal, bool srgb, int max)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        bool ch = false;
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType != type) { ti.textureType = type; ch = true; }
        if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; ch = true; }
        if (ti.maxTextureSize != max) { ti.maxTextureSize = max; ch = true; }
        if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; ch = true; }
        if (ti.textureCompression != TextureImporterCompression.CompressedHQ) { ti.textureCompression = TextureImporterCompression.CompressedHQ; ch = true; }
        if (ti.anisoLevel != 4) { ti.anisoLevel = 4; ch = true; }
        if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; ch = true; }   // 한 장짜리 아틀라스 — 가장자리 번짐 방지
        if (ch) ti.SaveAndReimport();
    }

    private static Material ExteriorMaterial(string n, string dir)
    {
        TexImport($"{dir}/{n}_albedo.jpg", false, true, 4096);
        TexImport($"{dir}/{n}_normal.jpg", true, false, 4096);
        TexImport($"{dir}/{n}_ms.png", false, false, 2048);
        string path = $"{dir}/M_{n}_Ext.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Standard");
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{n}_albedo.jpg"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{n}_normal.jpg")); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f);
        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{n}_ms.png")); m.EnableKeyword("_METALLICGLOSSMAP");
        m.SetFloat("_GlossMapScale", 0.85f); m.SetFloat("_SmoothnessTextureChannel", 0f);
        m.SetColor("_Color", Color.white);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material FlagMaterial()
    {
        string path = $"{Root}/M_HOB_Flag.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("TagOfChaos/FlagWave")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", new Color(0.86f, 0.14f, 0.14f)); m.enableInstancing = true; EditorUtility.SetDirty(m);   // 과일 창고 빨간 깃발
        return m;
    }

    // 흘러내리는 초콜릿: UV를 쓰는 Standard(Triplanar HOB_TriPBR는 UV가 없어 TextureScroll이 듣지 않는다) + HQ 초콜릿 타일 텍스처
    private static Material DripMaterial()
    {
        string path = $"{Root}/M_HOB_ChocoDrip.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        var src = HobHQBuilder.MaterialFor("ChocoFlow");
        if (src != null)
        {
            m.SetTexture("_MainTex", src.GetTexture("_MainTex")); m.SetTextureScale("_MainTex", new Vector2(1f, 2f));
            if (src.HasProperty("_BumpMap") && src.GetTexture("_BumpMap") != null) { m.SetTexture("_BumpMap", src.GetTexture("_BumpMap")); m.EnableKeyword("_NORMALMAP"); }
        }
        m.SetColor("_Color", new Color(0.62f, 0.42f, 0.3f)); m.SetFloat("_Glossiness", 0.82f); m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true; EditorUtility.SetDirty(m);
        return m;
    }

    private static void ImportFbx(string fbx, string n, Material ext)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None;
        imp.isReadable = true;   // MeshCollider·NavMesh 굽기
        imp.importNormals = ModelImporterNormals.Import; imp.importTangents = ModelImporterTangents.CalculateMikk;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.importCameras = false; imp.importLights = false;
        imp.SaveAndReimport();
        var names = AssetDatabase.LoadAssetAtPath<GameObject>(fbx).GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToList();
        foreach (string mn in names)
        {
            Material target = null;
            if (mn == n + "_Ext") target = ext;
            else if (mn.StartsWith("HOBHQ_")) target = HobHQBuilder.MaterialFor(mn.Substring(6));
            else if (mn == "HOB_Flag") target = FlagMaterial();
            else if (mn == "HOB_ChocoDrip") target = DripMaterial();
            if (target != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mn), target);
        }
        imp.SaveAndReimport();
    }

    // ---------------- 프리팹 ----------------
    private static string BuildOne(string n, string dir)
    {
        string fbx = $"{dir}/{n}.fbx";
        Material ext = ExteriorMaterial(n, dir);
        ImportFbx(fbx, n, ext);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var root = new GameObject(n);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        inst.name = "Model";
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Transform mt = inst.transform;
        // Blender 축 → Unity 로컬(루트 기준)
        Vector3 o = root.transform.InverseTransformPoint(mt.position);
        Vector3 ex = Ref(root, mt, "MK_RefX") - o, ey = Ref(root, mt, "MK_RefY") - o, ez = Ref(root, mt, "MK_RefZ") - o;
        var info = JsonUtility.FromJson<Info>(File.ReadAllText($"{dir}/{n}.json"));
        int props = 0, lights = 0, wheat = 0, spin = 0;
        var groupProps = new GameObject("Props").transform; groupProps.SetParent(root.transform, false);
        var groupLights = new GameObject("Lights").transform; groupLights.SetParent(root.transform, false);
        foreach (Transform t in mt.GetComponentsInChildren<Transform>(true).ToList())
        {
            if (t == null || t == mt) continue;
            string nm = t.name;
            if (nm.StartsWith("COL_")) { GingerbreadBuildingSetup.MakeCollider(t.gameObject); continue; }
            if (nm.StartsWith("MK_Prop_"))
            {
                string pf = nm.Substring(nm.IndexOf('_', 8) + 1);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{HobPrefabBuilder.PrefabDir}/{pf}.prefab");
                if (prefab != null)
                {
                    var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab, groupProps);
                    p.transform.position = t.position;
                    // 소품 앞(Blender −Y)을 json의 yaw(Blender Z 회전, 도)만큼 돌린 방향으로: Blender 방향 → Unity(ex, ey)
                    int idx = int.Parse(nm.Substring(8, 2));
                    float yaw = info.props != null && idx < info.props.Length ? info.props[idx].yaw * Mathf.Deg2Rad : 0f;
                    Vector3 dU = ex * Mathf.Sin(yaw) + ey * -Mathf.Cos(yaw);
                    p.transform.rotation = Quaternion.LookRotation(Flat(dU), Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(Flat(-ey), Vector3.up));
                    float ps = info.props != null && idx < info.props.Length && info.props[idx].s > 0f ? info.props[idx].s : 1f;
                    if (ps != 1f) p.transform.localScale *= ps;
                    props++;
                }
                Object.DestroyImmediate(t.gameObject); continue;
            }
            if (nm.StartsWith("MK_Light_"))
            {
                var g = new GameObject("Light_" + lights); g.transform.SetParent(groupLights, false); g.transform.position = t.position;
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 9f; l.intensity = 1.3f; l.shadows = LightShadows.None; l.color = new Color(1f, 0.84f, 0.64f);
                lights++; Object.DestroyImmediate(t.gameObject); continue;
            }
            if (nm.StartsWith("MK_Wheat_"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{HobPrefabBuilder.PrefabDir}/HOB_WheatPatch.prefab");
                if (prefab != null)
                {
                    var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab, groupProps);
                    p.transform.position = t.position; p.transform.rotation = Quaternion.Euler(0f, wheat * 37f, 0f);
                    foreach (Collider c in p.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    wheat++;
                }
                Object.DestroyImmediate(t.gameObject); continue;
            }
            if (nm.StartsWith("MK_Ref")) { Object.DestroyImmediate(t.gameObject); continue; }
            if (nm.Contains("_Anim_Blades"))
            {
                // 날개: 원점 = 바퀴 축 가운데(Blender에서 맞춤), 축 = 날개 면의 법선(Blender −Y 쪽 축). 이름 끝 _<도/초>
                var sp = t.gameObject.AddComponent<SpinPart>();
                sp.EditorSetup(t.InverseTransformDirection(ey).normalized, nm.Contains("Apple") ? -32f : -22f);
                spin++; continue;
            }
            if (nm.Contains("_Anim_Drip"))
            {
                var r = t.GetComponent<Renderer>();
                if (r != null) t.gameObject.AddComponent<TextureScroll>().EditorSetup(r, new Vector2(0f, -0.35f));
                continue;
            }
        }
        // 정적: 외관·내부(움직이는 부품 제외)
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.Contains("_Anim_") || r.transform.IsChildOf(groupProps)) continue;
            GingerbreadBuildingSetup.MarkStatic(r.gameObject);
            r.shadowCastingMode = r.name.EndsWith("_Interior") ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.TwoSided;   // 외관 껍데기는 안쪽 면이 없어 양면 그림자
        }
        // 재료 꽂는 자리
        Transform sock = FindDeep(root.transform, "MK_Socket");
        var socketMarker = new GameObject("SocketPoint").transform; socketMarker.SetParent(root.transform, false);
        if (sock != null) { socketMarker.position = sock.position; Object.DestroyImmediate(sock.gameObject); }
        string path = $"{PrefabDir}/{n}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return $"{n}: props {props} lights {lights} wheat {wheat} spin {spin}";
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
    private static Vector3 Ref(GameObject root, Transform mt, string name)
    {
        Transform t = FindDeep(mt, name);
        return t != null ? root.transform.InverseTransformPoint(t.position) : Vector3.zero;
    }
    internal static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
        return null;
    }
}
