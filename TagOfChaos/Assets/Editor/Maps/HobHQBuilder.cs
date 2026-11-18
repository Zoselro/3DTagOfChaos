using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Heaven Of Bakery 고품질 모델(§89 품질 개선): Blender에서 만든 HQ 모델(정점 색 = 접촉 그림자, 재질 슬롯 이름 HOBHQ_<재질>)과
// 이음새 없는 타일 텍스처(HQ_<재질>_Albedo/Normal)를 가져와 공용 재질 M_HQ_<재질>(TagOfChaos/HOB_TriPBR, 월드 축 3방향 투영)을 만들고
// FBX 재질을 그 공용 재질로 이어 준다. 같은 이름의 예전 모델 대신 HQ 모델로 프리팹을 다시 만든다(프리팹 경로·GUID 그대로 → 씬이 바로 바뀐다).
public static class HobHQBuilder
{
    public const string Root = "Assets/09. Environment/HeavenOfBakery/HQ";
    public const string ModelDir = Root + "/Models";
    public const string TileDir = Root + "/Tiles";
    public const string MaterialDir = Root + "/Materials";
    public const string Shader = "TagOfChaos/HOB_TriPBR";

    // 위를 보는 면에 이끼(바위·분홍 돌·껍질) — 양(0~1)
    private static readonly Dictionary<string, float> Moss = new Dictionary<string, float> { { "Rock", 0.5f }, { "RockDark", 0.5f }, { "StoneGrey", 0.3f }, { "Bark", 0.1f } };

    // 발광 세기(타일 정보의 색 × 값): 등불 유리·포털은 밝게, 창문은 은은하게
    private static readonly Dictionary<string, float> EmissionScale = new Dictionary<string, float> { { "Glow", 3f }, { "Portal", 2.5f }, { "PurpleGlow", 2.5f }, { "Water", 0.8f }, { "Window", 0.35f }, { "Pane", 0.35f } };

    [System.Serializable] private class TileMeta { public float tile = 1f; public float metal; public float[] emission; }

    [MenuItem("Tools/TagOfChaos/Maps/Heaven Of Bakery/Import HQ Pack (Blender repo)")]
    public static void FetchMenu() => Debug.Log(FetchFromBlenderRepo(@"F:\Blender", "claude/update-mcp-url"));

    // 사용자의 Blender 저장소 사본에서 받는다: fetch 후 git archive로 transfer/HOB/HQ만 꺼낸다(작업 폴더·브랜치는 건드리지 않는다).
    public static string FetchFromBlenderRepo(string repo, string branch)
    {
        string zip = Path.GetFullPath("Temp/hq_pack.zip");
        if (File.Exists(zip)) File.Delete(zip);
        string log = Git(repo, $"fetch origin {branch}") + " | " + Git(repo, $"archive --format=zip -o \"{zip}\" origin/{branch} transfer/HOB/HQ/Models transfer/HOB/HQ/Tiles");
        return File.Exists(zip) ? ImportZip(zip) : "[HobHQ] archive failed: " + log;
    }

    private static string Git(string dir, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git", args) { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using (var p = System.Diagnostics.Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit(600000); return o.Trim(); }
    }

    [MenuItem("Tools/TagOfChaos/Maps/Heaven Of Bakery/Import HQ Pack (Temp zip)")]
    public static void ImportMenu() => Debug.Log(ImportZip("Temp/hqpilot.zip"));

    public static string ImportZip(string zipPath)
    {
        if (!File.Exists(zipPath)) return "[HobHQ] zip missing " + zipPath;
        HobPrefabBuilder.EnsureFolder(ModelDir); HobPrefabBuilder.EnsureFolder(TileDir); HobPrefabBuilder.EnsureFolder(MaterialDir);
        int n = 0;
        using (ZipArchive z = ZipFile.OpenRead(zipPath))
            foreach (ZipArchiveEntry e in z.Entries)
            {
                if (string.IsNullOrEmpty(e.Name)) continue;
                string dir = e.FullName.Contains("Models/") ? ModelDir : TileDir;
                string dst = $"{dir}/{e.Name}";
                using (Stream s = e.Open()) using (FileStream f = File.Create(dst)) s.CopyTo(f);
                n++;
            }
        AssetDatabase.Refresh();
        return $"[HobHQ] extracted {n} | {SetupAll()}";
    }

    public static string SetupAll()
    {
        int tex = ConfigureTextures(), mats = BuildMaterials(), models = ConfigureModels();
        AssetDatabase.SaveAssets();
        return $"textures {tex}, materials {mats}, models {models}";
    }

    private static int ConfigureTextures()
    {
        int n = 0;
        foreach (string p in Directory.GetFiles(TileDir, "*.png").Select(x => x.Replace('\\', '/')))
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(p);
            bool normal = p.EndsWith("_Normal.png");
            bool dirty = false;
            if (normal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (!normal && !imp.sRGBTexture) { imp.sRGBTexture = true; dirty = true; }
            if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; dirty = true; }
            if (imp.anisoLevel != 4) { imp.anisoLevel = 4; dirty = true; }
            if (!normal && imp.alphaSource != TextureImporterAlphaSource.FromInput) { imp.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
            if (dirty) { imp.SaveAndReimport(); n++; }
        }
        return n;
    }

    private static Dictionary<string, TileMeta> ReadMeta()
    {
        var d = new Dictionary<string, TileMeta>();
        string p = TileDir + "/tiles.json";
        if (!File.Exists(p)) return d;
        // {"Name": {"tile": 1.0, "metal": 0.0, "emission": null|[r,g,b]}, ...} — tiny hand parser (JsonUtility can't read a map)
        string json = File.ReadAllText(p);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json, "\"(\\w+)\"\\s*:\\s*\\{([^}]*)\\}"))
        {
            string body = m.Groups[2].Value; var t = new TileMeta();
            var tile = System.Text.RegularExpressions.Regex.Match(body, "\"tile\"\\s*:\\s*([0-9.]+)"); if (tile.Success) t.tile = float.Parse(tile.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var metal = System.Text.RegularExpressions.Regex.Match(body, "\"metal\"\\s*:\\s*([0-9.]+)"); if (metal.Success) t.metal = float.Parse(metal.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var em = System.Text.RegularExpressions.Regex.Match(body, "\"emission\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (em.Success) t.emission = em.Groups[1].Value.Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            d[m.Groups[1].Value] = t;
        }
        return d;
    }

    public static Material MaterialFor(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/M_HQ_{name}.mat");

    private static int BuildMaterials()
    {
        Shader sh = UnityEngine.Shader.Find(Shader);
        if (sh == null) { Debug.LogError("[HobHQ] shader missing"); return 0; }
        Dictionary<string, TileMeta> meta = ReadMeta();
        var mossAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TileDir}/HQ_Moss_Albedo.png");
        var mossNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TileDir}/HQ_Moss_Normal.png");
        int n = 0;
        foreach (string p in Directory.GetFiles(TileDir, "HQ_*_Albedo.png"))
        {
            string name = Path.GetFileNameWithoutExtension(p).Substring(3); name = name.Substring(0, name.Length - "_Albedo".Length);
            string path = $"{MaterialDir}/M_HQ_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\', '/')));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\', '/').Replace("_Albedo", "_Normal")));
            TileMeta t = meta.TryGetValue(name, out TileMeta tm) ? tm : new TileMeta();
            m.SetFloat("_Tile", t.tile);
            m.SetFloat("_Metallic", t.metal);
            m.SetColor("_EmissionColor", t.emission != null ? new Color(t.emission[0], t.emission[1], t.emission[2]) * (EmissionScale.TryGetValue(name, out float es) ? es : 2f) : Color.black);
            if (t.emission != null) m.EnableKeyword("_EMISSION");
            if (name == "Gold") { m.SetFloat("_Metallic", 0.85f); m.SetColor("_Color", new Color(1.15f, 1.0f, 0.8f)); }
            m.SetFloat("_MacroStrength", name == "Grass" ? 0.6f : name == "Rock" || name == "Sand" ? 0.45f : 0.2f);
            if (name == "Grass") m.SetColor("_Color", new Color(0.7f, 0.8f, 0.62f));        // 넓은 잔디밭이 너무 밝고 형광빛이라 조금 어둡고 차분하게
            if (Moss.TryGetValue(name, out float amount) && mossAlb != null)
            {
                m.EnableKeyword("_MOSS_ON"); m.SetTexture("_MossTex", mossAlb); m.SetTexture("_MossBump", mossNrm); m.SetFloat("_MossAmount", amount);
            }
            else m.DisableKeyword("_MOSS_ON");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            n++;
        }
        return n;
    }

    // FBX 재질 슬롯 HOBHQ_<재질> → 공용 M_HQ_<재질>
    private static int ConfigureModels()
    {
        int n = 0;
        foreach (string p in Directory.GetFiles(ModelDir, "*.fbx").Select(x => x.Replace('\\', '/')))
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(p);
            imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None;
            imp.isReadable = HobPrefabBuilder.UsesMeshCollider(Path.GetFileNameWithoutExtension(p));
            imp.importNormals = ModelImporterNormals.Import; imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.SaveAndReimport();
            var names = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Material>().Select(m => m.name).Distinct().ToList();
            if (names.Count == 0)
                names = AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToList();
            foreach (string mn in names)
            {
                string key = mn.StartsWith("HOBHQ_") ? mn.Substring(6) : mn;
                Material target = MaterialFor(key);
                if (target != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mn), target);
            }
            imp.SaveAndReimport();
            n++;
        }
        return n;
    }
}
