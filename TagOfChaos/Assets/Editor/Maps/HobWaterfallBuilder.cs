using System.IO;
using UnityEditor;
using UnityEngine;

// Heaven Of Bakery 폭포(Request1009Plan.md §89 HB2): 물줄기 판(F1) + 줄기·가장자리 거품·물보라·안개 파티클(F2~F5)을 코드로 만든다.
// 프리팹 원점 = 떨어지기 시작하는 가장자리 가운데, +Z = 절벽 바깥, −Y = 아래. 텍스처도 코드로 만든다(값을 바꿔 다시 굽기 쉽게).
public static class HobWaterfallBuilder
{
    public const string FxDir = "Assets/09. Environment/HeavenOfBakery/FX";
    public const string PrefabDir = "Assets/04. Prefabs/Environment/HeavenOfBakery";
    public const string PrefabPath = PrefabDir + "/HOB_Waterfall.prefab";
    public const string SmallPrefabPath = PrefabDir + "/HOB_Waterfall_Small.prefab";
    public const string SoundPath = "Assets/09. Environment/HeavenOfBakery/Audio/HOB_WaterfallLoop.ogg";
    private const int Seed = 1117;

    public struct Spec
    {
        public float Width, Drop, Throw;   // 폭(m), 떨어지는 높이(m), 가장자리에서 바깥으로 나가는 거리(m)
        public float Scale;                // 파티클 수·크기 배율
    }

    public static readonly Spec Main = new Spec { Width = 3.4f, Drop = 35f, Throw = 2.2f, Scale = 1f };
    public static readonly Spec Small = new Spec { Width = 1.8f, Drop = 8f, Throw = 0.9f, Scale = 0.5f };

    [MenuItem("Tools/TagOfChaos/Maps/Heaven Of Bakery/Build Waterfall Prefabs")]
    public static void BuildMenu() => Debug.Log(BuildAll());

    public static string BuildAll()
    {
        EnsureFolder(FxDir); EnsureFolder(PrefabDir);
        Texture2D flow = SaveTexture("HOB_FX_FlowNoise", FlowNoise(128, 256), TextureWrapMode.Repeat);
        Texture2D streak = SaveTexture("HOB_FX_Streak", Streak(64, 256), TextureWrapMode.Clamp);
        Texture2D mist = SaveTexture("HOB_FX_Mist", Mist(128), TextureWrapMode.Clamp);
        Texture2D drop = SaveTexture("HOB_FX_Droplet", Droplet(32), TextureWrapMode.Clamp);

        Material sheet = SaveMaterial("M_HOB_WaterfallSheet", Shader.Find("TagOfChaos/WaterfallScroll"), m => { m.SetTexture("_MainTex", flow); m.SetFloat("_Alpha", 0.9f); });
        Shader alpha = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        Material mStreak = SaveMaterial("M_HOB_FX_Streak", alpha, m => { m.SetTexture("_MainTex", streak); m.SetColor("_TintColor", new Color(0.62f, 0.84f, 1f, 0.32f)); });
        Material mFoam = SaveMaterial("M_HOB_FX_Foam", alpha, m => { m.SetTexture("_MainTex", drop); m.SetColor("_TintColor", new Color(0.9f, 0.97f, 1f, 0.45f)); });
        Material mSpray = SaveMaterial("M_HOB_FX_Spray", alpha, m => { m.SetTexture("_MainTex", drop); m.SetColor("_TintColor", new Color(0.8f, 0.92f, 1f, 0.45f)); });
        Material mMist = SaveMaterial("M_HOB_FX_Mist", alpha, m => { m.SetTexture("_MainTex", mist); m.SetColor("_TintColor", new Color(0.95f, 0.95f, 1f, 0.12f)); });

        string a = BuildPrefab(PrefabPath, "HOB_Waterfall", Main, sheet, mStreak, mFoam, mSpray, mMist);
        string b = BuildPrefab(SmallPrefabPath, "HOB_Waterfall_Small", Small, sheet, mStreak, mFoam, mSpray, mMist);
        AssetDatabase.SaveAssets();
        return $"[HobWaterfall] {a} | {b}";
    }

    private static string BuildPrefab(string path, string name, Spec s, Material sheet, Material streak, Material foam, Material spray, Material mist)
    {
        var root = new GameObject(name);
        Mesh mesh = SheetMesh(name + "_Sheet", s);
        string meshPath = $"{FxDir}/{name}_Sheet.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        var sheetGo = new GameObject("Sheet");
        sheetGo.transform.SetParent(root.transform, false);
        sheetGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = sheetGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = sheet;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        float k = s.Scale, fallTime = Mathf.Sqrt(2f * s.Drop / 9.81f);
        // F2 줄기: 가장자리에서 나와 중력으로 떨어지는 늘인 빌보드
        Layer(root.transform, "Streaks", streak, fallTime, 80f * k, Mathf.RoundToInt(fallTime * 80f * k * 1.2f), 1f,
            new Vector2(0.6f, 1.2f) * Mathf.Sqrt(k), ParticleSystemRenderMode.Stretch, new Vector3(s.Width, 0.1f, 0.3f),
            new Vector3(0f, -0.15f, 0.25f), new Vector2(0.5f, 1.5f), new Vector2(0.6f, 1.0f) * (s.Throw / 2.2f));
        // F3 가장자리 거품
        Layer(root.transform, "LipFoam", foam, 0.6f, 40f * k, Mathf.RoundToInt(40f * k), 0.4f,
            new Vector2(0.25f, 0.5f) * Mathf.Sqrt(k), ParticleSystemRenderMode.Billboard, new Vector3(s.Width, 0.05f, 0.2f),
            new Vector3(0f, -0.05f, 0.2f), new Vector2(0.3f, 0.8f), new Vector2(0.2f, 0.4f));
        // F4 물보라: 아래쪽 3분의 1에서 옆으로 튄다
        float sprayY = -s.Drop * 0.72f;
        Layer(root.transform, "Spray", spray, 1f, 30f * k, Mathf.RoundToInt(40f * k), 0.6f,
            new Vector2(0.08f, 0.18f) * Mathf.Sqrt(k), ParticleSystemRenderMode.Billboard, new Vector3(s.Width + 0.6f, s.Drop * 0.25f, 0.6f),
            new Vector3(0f, sprayY, SheetZ(s, -sprayY)), new Vector2(1.5f, 3f), new Vector2(-0.5f, 1.2f));
        // F5 안개: 구름(아래 끝)에 닿는 곳, 천천히 위로
        ParticleSystem m = Layer(root.transform, "Mist", mist, 4f, 10f * k, Mathf.RoundToInt(50f * k), -0.03f,
            new Vector2(4f, 8f) * k, ParticleSystemRenderMode.Billboard, new Vector3(s.Width + 2f, 1f, 2f),
            new Vector3(0f, -s.Drop + 1f, SheetZ(s, s.Drop)), new Vector2(0.2f, 0.5f), new Vector2(0f, 0.2f));
        var rot = m.main; rot.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var sol = m.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f));

        // F6 소리(D7): 부드러운 물소리 8초 고리(코드 합성 — 쏴아 + 작은 물방울), 3D 30 m
        var audio = new GameObject("Audio").AddComponent<AudioSource>();
        audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
        audio.volume = 0.8f * Mathf.Max(0.6f, k);
        audio.transform.SetParent(root.transform, false);
        audio.transform.localPosition = new Vector3(0f, -s.Drop * 0.5f, SheetZ(s, s.Drop * 0.5f));
        audio.loop = true; audio.playOnAwake = true; audio.spatialBlend = 1f;
        audio.rolloffMode = AudioRolloffMode.Linear; audio.minDistance = 4f; audio.maxDistance = 30f * Mathf.Max(0.6f, k);
        audio.dopplerLevel = 0f;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return $"{name}: sheet {mesh.vertexCount} v, fall {fallTime:F1}s";
    }

    // 떨어진 깊이 d(m)에서 판이 가장자리 바깥으로 나간 거리 — 처음은 수평으로 나가다가 포물선으로 떨어진다.
    public static float SheetZ(Spec s, float d) => s.Throw * Mathf.Sqrt(Mathf.Clamp01(d / s.Drop)) + 0.25f;

    private static Mesh SheetMesh(string name, Spec s)
    {
        const int rows = 48, cols = 6;
        var v = new Vector3[(rows + 1) * (cols + 1)];
        var uv = new Vector2[v.Length];
        var c = new Color[v.Length];
        var tri = new int[rows * cols * 6];
        float dist = 0f; Vector3 prev = Vector3.zero;
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            float d = s.Drop * t * t;                                  // 위쪽을 촘촘히(휘는 곳)
            var centre = new Vector3(0f, -d, SheetZ(s, d));
            if (r > 0) dist += Vector3.Distance(prev, centre);
            prev = centre;
            float w = s.Width * (1f + 0.25f * t);                         // 떨어지며 조금 넓어진다
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.03f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.72f) / 0.28f)));
            for (int q = 0; q <= cols; q++)
            {
                float u = q / (float)cols;
                int i = r * (cols + 1) + q;
                v[i] = centre + new Vector3((u - 0.5f) * w, 0f, Mathf.Sin(u * Mathf.PI) * 0.12f);   // 가운데가 살짝 불룩
                uv[i] = new Vector2(u, dist);
                c[i] = new Color(1f, 1f, 1f, fade);
            }
        }
        int n = 0;
        for (int r = 0; r < rows; r++)
            for (int q = 0; q < cols; q++)
            {
                int a = r * (cols + 1) + q, b = a + cols + 1;
                tri[n++] = a; tri[n++] = b; tri[n++] = a + 1;
                tri[n++] = a + 1; tri[n++] = b; tri[n++] = b + 1;
            }
        var mesh = new Mesh { name = name, vertices = v, uv = uv, colors = c, triangles = tri };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static ParticleSystem Layer(Transform parent, string name, Material mat, float life, float rate, int max, float gravity,
        Vector2 size, ParticleSystemRenderMode mode, Vector3 boxScale, Vector3 pos, Vector2 speed, Vector2 outward)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(Seed + name.GetHashCode());
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.85f, life * 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.gravityModifier = gravity;
        main.maxParticles = Mathf.Max(8, max);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = boxScale; sh.randomDirectionAmount = mode == ParticleSystemRenderMode.Stretch ? 0f : 0.6f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);       // 세 축이 같은 방식이어야 한다
        vel.z = new ParticleSystem.MinMaxCurve(outward.x, outward.y);
        var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeInOut();
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = mode; r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.sortingFudge = mode == ParticleSystemRenderMode.Stretch ? -1f : 0f;
        if (mode == ParticleSystemRenderMode.Stretch) { r.lengthScale = 3f; r.velocityScale = 0.12f; }
        return ps;
    }

    private static ParticleSystem.MinMaxGradient FadeInOut()
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    // ---------------- textures (코드로 만든다) ----------------
    private static float Hash(int x, int y) { uint h = (uint)(x * 374761393 + y * 668265263 + Seed); h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / 4294967295f; }

    private static float PeriodicNoise(float x, float y, int px, int py)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int Wx(int a) => ((a % px) + px) % px; int Wy(int a) => ((a % py) + py) % py;
        float a00 = Hash(Wx(x0), Wy(y0)), a10 = Hash(Wx(x0 + 1), Wy(y0)), a01 = Hash(Wx(x0), Wy(y0 + 1)), a11 = Hash(Wx(x0 + 1), Wy(y0 + 1));
        return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
    }

    private static Texture2D FlowNoise(int w, int h)   // 세로로 길게 늘어진 줄무늬 노이즈(이음매 없음)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h, n = 0f, amp = 0.55f;
                for (int o = 0; o < 4; o++)
                {
                    int fx = 8 << o, fy = 2 << o;
                    n += amp * PeriodicNoise(u * fx, v * fy, fx, fy); amp *= 0.5f;
                }
                n = Mathf.Clamp01(n * 1.1f);
                t.SetPixel(x, y, new Color(n, n, n, 1f));
            }
        t.Apply(true);
        return t;
    }

    private static Texture2D Streak(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w - 0.5f, v = (y + 0.5f) / h;
                float across = Mathf.Exp(-u * u / (2f * 0.18f * 0.18f));
                float along = Mathf.SmoothStep(0f, 1f, v / 0.2f) * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.35f);
                float n = 0.7f + 0.3f * PeriodicNoise(u * 4f + 2f, v * 12f, 8, 12);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(across * along * n)));
            }
        t.Apply(true);
        return t;
    }

    private static Texture2D Mist(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, true);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float n = 0.6f + 0.4f * PeriodicNoise(x / 16f, y / 16f, s / 16, s / 16);
                float a = Mathf.Clamp01(1f - r) ; a = a * a * (3 - 2 * a) * n;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply(true);
        return t;
    }

    private static Texture2D Droplet(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, true);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)); a = Mathf.Pow(a, 1.6f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply(true);
        return t;
    }

    private static Texture2D SaveTexture(string name, Texture2D tex, TextureWrapMode wrap)
    {
        string path = $"{FxDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.alphaIsTransparency = true;
        imp.wrapMode = wrap;
        imp.sRGBTexture = name != "HOB_FX_FlowNoise";
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material SaveMaterial(string name, Shader shader, System.Action<Material> setup)
    {
        string path = $"{FxDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        setup(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
