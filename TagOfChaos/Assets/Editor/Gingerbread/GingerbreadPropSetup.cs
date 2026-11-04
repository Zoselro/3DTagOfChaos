using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 진저브레드 마을 소품(Request1009Plan.md §48 G6)과 시계탑(§49 CT4) 가져오기·프리팹 도구.
// Blender에서 만든 FBX는 프로젝트 밖 GinStaging/Props/(Ground|Underground|Floor|ClockTower)에 올라온다.
// 재질 이름(M_GIN_<키>)은 건물 도구(GingerbreadBuildingSetup)와 같은 공용 재질·텍스처를 쓴다.
// 충돌체: 부품 메시 그대로의 오목 MeshCollider(아치·문은 빈 곳으로 지나갈 수 있다). COL_* 는 충돌 전용.
// 시계탑: Door_L·Door_R(경첩 원점) → 여닫는 양문(InteractableDoor), COL_StairRamp = 계단 걷기 경사.
public static class GingerbreadPropSetup
{
    private const string StagingDir = "GinStaging/Props";
    private const string RootDir = "Assets/09. Environment/GingerbreadVillage";
    private const string PrefabDir = "Assets/04. Prefabs/Environment/GingerbreadVillage";
    private static readonly string[] Groups = { "Ground", "Underground", "Floor", "ClockTower" };

    [MenuItem("Tools/TagOfChaos/Gingerbread/5. Import And Build Village Props")]
    public static void ImportAndBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[GinProps] not in Play Mode"); return; }
        string staging = Path.GetFullPath(StagingDir);
        if (!Directory.Exists(staging)) { Debug.LogError("[GinProps] staging folder missing: " + staging); return; }
        int copied = 0;
        foreach (string g in Groups)
        {
            string src = Path.Combine(staging, g);
            if (!Directory.Exists(src)) continue;
            foreach (string f in Directory.GetFiles(src, "*.fbx"))
                copied += GingerbreadBuildingSetup.CopyIfChanged(f, $"{RootDir}/Models/{g}/{Path.GetFileName(f)}");
        }
        AssetDatabase.Refresh();
        Debug.Log($"[GinProps] imported {copied} changed files");

        var table = GingerbreadBuildingSetup.LoadMatTable();
        int built = 0;
        foreach (string g in Groups)
        {
            string dir = $"{RootDir}/Models/{g}";
            if (!AssetDatabase.IsValidFolder(dir)) continue;
            Directory.CreateDirectory($"{PrefabDir}/{g}");
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
            {
                string fbx = AssetDatabase.GUIDToAssetPath(guid);
                string nm = Path.GetFileNameWithoutExtension(fbx);
                GingerbreadBuildingSetup.RemapFbxMaterials(fbx, table);
                GameObject root = BuildOne(fbx, nm);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{g}/{nm}.prefab");
                Object.DestroyImmediate(root);
                built++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[GinProps] built {built} prop prefabs");
    }

    private static GameObject BuildOne(string fbxPath, string nm)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var root = new GameObject(nm);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.transform.SetParent(root.transform, false);
        Transform doorL = GingerbreadBuildingSetup.FindDeep(inst.transform, "Door_L");
        Transform doorR = GingerbreadBuildingSetup.FindDeep(inst.transform, "Door_R");
        var doorParts = new HashSet<Transform>();
        if (doorL != null) doorParts.UnionWith(doorL.GetComponentsInChildren<Transform>(true));
        if (doorR != null) doorParts.UnionWith(doorR.GetComponentsInChildren<Transform>(true));
        var all = new List<Transform>(inst.GetComponentsInChildren<Transform>(true));
        foreach (Transform t in all)
        {
            if (doorParts.Contains(t)) continue;   // 문짝은 문 도구가 상자 충돌체를 붙인다
            GameObject g = t.gameObject;
            if (g.name.StartsWith("COL_")) { GingerbreadBuildingSetup.MakeCollider(g); continue; }
            var mf = g.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mc = g.GetComponent<MeshCollider>();
            if (mc == null) mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            GingerbreadBuildingSetup.MarkStatic(g);
        }
        if (doorL != null && doorR != null)
        {
            var body = inst.GetComponentInChildren<Renderer>();
            Vector3 c = body != null ? body.bounds.center : inst.transform.position;
            GingerbreadBuildingSetup.MakeDoorPair(inst.transform, doorL, doorR, nm + "_Front", "Front", new Vector3(c.x, inst.transform.position.y, c.z));
        }
        return root;
    }
}
