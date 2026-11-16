using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 부서지기 규칙 에셋(Resources/Escape/DestructionRules)을 만들고, Blender 조각 모델 목록을 채운다(Request1009Plan.md §66 GD4).
// §82: 소품·텐트·계단·술래방 조각 모델(Props82 폴더, 애니메이션 "Collapse" 하나)과 건물 4방향 애니메이션(<이름>_Collapse.fbx, 클립 Collapse_0~3)을
// 조각 모델 에셋(FracturedModelSO)에 넣고, 클립마다 조각들이 평균으로 움직인 방향(모델 뿌리 기준)을 재어 둔다.
public static class DestructionRulesSetup
{
    public const string AssetPath = "Assets/Resources/Escape/DestructionRules.asset";
    public const string FracturedFolder = "Assets/09. Environment/Fractured";
    public const string PropFolder = FracturedFolder + "/Props82";
    public const string StylesPath = PropFolder + "/_styles.txt";   // "이름<탭>wood|shell|chunk|pole|panel|room" (Blender 일괄 처리 기록)
    public const string CrackTextureFolder = "Assets/Textures/Giant";
    public const string CrackMaterialFolder = "Assets/Materials/Giant";

    [MenuItem("TagOfChaos/Giant/Refresh Destruction Rules")]
    public static string Refresh()
    {
        var rules = AssetDatabase.LoadAssetAtPath<DestructionRulesSO>(AssetPath);
        if (rules == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            rules = ScriptableObject.CreateInstance<DestructionRulesSO>();
            AssetDatabase.CreateAsset(rules, AssetPath);
        }
        var buildings = new List<GameObject>();
        var props = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { FracturedFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("_Fractured.fbx")) continue;
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            if (path.StartsWith(PropFolder + "/")) props.Add(go); else buildings.Add(go);
        }
        buildings.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        props.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        Dictionary<string, FractureStyle> styles = ReadStyles();
        rules.EditorSetFracturedNames(WriteHolders(buildings, styles)); // §73.11: 이름만 — 모델은 작은 에셋이 하나씩 가리킨다
        rules.EditorSetPropFracturedNames(WriteHolders(props, styles));
        rules.EditorSetCrackMaterials(CrackMaterials());
        EditorUtility.SetDirty(rules);
        AssetDatabase.SaveAssets();
        return $"DestructionRules: {buildings.Count} buildings, {props.Count} props/tents/stairs/rooms";
    }

    public const string HolderFolder = "Assets/Resources/Escape/Fractured";

    public static string[] WriteHolders(List<GameObject> models) => WriteHolders(models, ReadStyles());

    // 조각 모델마다 Resources/Escape/Fractured/<원본 이름>.asset(FracturedModelSO)을 만들고 원본 이름 목록을 돌려준다.
    // 규칙 에셋이 모델을 직접 가리키면 규칙을 처음 읽을 때 모든 맵의 조각 모델(876 MB)을 한꺼번에 읽었다(§73.11).
    public static string[] WriteHolders(List<GameObject> models, Dictionary<string, FractureStyle> styles)
    {
        Directory.CreateDirectory(HolderFolder);
        var names = new List<string>();
        foreach (GameObject m in models)
        {
            string name = m.name.Replace("_Fractured", "");
            string path = $"{HolderFolder}/{name}.asset";
            var holder = AssetDatabase.LoadAssetAtPath<FracturedModelSO>(path);
            if (holder == null)
            {
                holder = ScriptableObject.CreateInstance<FracturedModelSO>();
                AssetDatabase.CreateAsset(holder, path);
            }
            holder.EditorSet(m);
            AnimationClip[] clips = ClipsFor(m);
            FractureStyle style = name.StartsWith("MonsterRoom_") ? FractureStyle.Room
                : styles.TryGetValue(name, out FractureStyle s) ? s : FractureStyle.Building;
            holder.EditorSetClips(clips, MeasurePushes(m, clips), style);
            EditorUtility.SetDirty(holder);
            names.Add(name);
        }
        return names.ToArray();
    }

    // 조각 모델의 애니메이션: 같은 FBX 안(소품 "Collapse") 또는 옆의 <이름>_Collapse.fbx(건물 4방향, 이름순).
    public static AnimationClip[] ClipsFor(GameObject model)
    {
        string path = AssetDatabase.GetAssetPath(model);
        var clips = new List<AnimationClip>();
        string side = path.Replace("_Fractured.fbx", "_Collapse.fbx");
        foreach (string p in new[] { path, side })
        {
            if (!File.Exists(p)) continue;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) clips.Add(c);
        }
        clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return clips.ToArray();
    }

    // 클립마다 끝 자세에서 조각들이 평균으로 움직인 수평 방향(모델 뿌리 기준, 길이 1). 바깥으로 터지는 것은 거의 0.
    public static Vector3[] MeasurePushes(GameObject model, AnimationClip[] clips)
    {
        var pushes = new Vector3[clips.Length];
        if (clips.Length == 0) return pushes;
        var inst = (GameObject)Object.Instantiate(model);
        inst.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].SampleAnimation(inst, 0f);
                var start = new Vector3[rs.Length];
                for (int k = 0; k < rs.Length; k++) start[k] = inst.transform.InverseTransformDirection(rs[k].bounds.center - inst.transform.position);
                clips[i].SampleAnimation(inst, clips[i].length);
                Vector3 sum = Vector3.zero;
                for (int k = 0; k < rs.Length; k++)
                    sum += inst.transform.InverseTransformDirection(rs[k].bounds.center - inst.transform.position) - start[k];
                Vector3 up = inst.transform.InverseTransformDirection(Vector3.up);
                Vector3 flat = Vector3.ProjectOnPlane(sum / Mathf.Max(1, rs.Length), up);
                pushes[i] = flat.magnitude > 0.05f ? flat.normalized : Vector3.zero;
            }
        }
        finally { Object.DestroyImmediate(inst); }
        return pushes;
    }

    public static Dictionary<string, FractureStyle> ReadStyles()
    {
        var map = new Dictionary<string, FractureStyle>();
        if (!File.Exists(StylesPath)) return map;
        foreach (string line in File.ReadAllLines(StylesPath))
        {
            string[] p = line.Split('\t');
            if (p.Length < 2) continue;
            switch (p[1].Trim())
            {
                case "wood": map[p[0]] = FractureStyle.Wood; break;
                case "shell": map[p[0]] = FractureStyle.Shell; break;
                case "pole": map[p[0]] = FractureStyle.Pole; break;
                case "panel": map[p[0]] = FractureStyle.Panel; break;
                case "room": map[p[0]] = FractureStyle.Room; break;
                default: map[p[0]] = FractureStyle.Chunk; break;
            }
        }
        return map;
    }

    // 금 간 데칼 재질 3개(GD3 텍스처 — Standard Fade + 노멀).
    private static Material[] CrackMaterials()
    {
        Directory.CreateDirectory(CrackMaterialFolder);
        var mats = new Material[3];
        for (int n = 1; n <= 3; n++)
        {
            string path = $"{CrackMaterialFolder}/M_GiantCrack_{n}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            EscapeVisuals.ConfigureTemplate(m, fade: true, glow: false);
            m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CrackTextureFolder}/T_GiantCrack_{n}.png");
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{CrackTextureFolder}/T_GiantCrack_{n}_N.png"));
            m.SetFloat("_BumpScale", 1.2f);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Glossiness", 0.08f);
            m.color = Color.white;
            EditorUtility.SetDirty(m);
            mats[n - 1] = m;
        }
        return mats;
    }
}
