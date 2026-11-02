using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// 색칠 위장 팔레트 면적 측정(TwistedCandyPlan.md §4·V2). 맵 씬을 12시점(반지름 12·30·55 m 링)에서 찍어
// 화면 픽셀을 팔레트 10색 중 가장 가까운 색상(hue)으로 나누고 색마다 화면 면적(%)을 잰다.
// 팔레트 색 기준값도 같은 색 보정을 거치게 카메라 앞 조명 없는 색판으로 찍어 얻는다(쿠키와 맵이 같은 보정을 받으므로).
// 씬은 저장하지 않는다.
public static class PaletteCoverageMeter
{
    public const string PalettePath = "Assets/03. SO/ColorTag/DefaultColorPalette.asset";
    public const float LargeAreaPercent = 1f;   // 이 이상이면 "큰 면적"
    private const int W = 480, H = 270;
    private const float HueTolerance = 0.045f;  // 약 16도

    public sealed class Result
    {
        public string Map;
        public float[] Percent;     // 색 보정 켠 화면
        public float[] RawPercent;  // 색 보정 끈 화면(조명·안개만)
        public float MeanLuminance;
        public int LargeColors => Count(Percent);
        public int RawLargeColors => Count(RawPercent);
        private static int Count(float[] p) { int n = 0; foreach (float v in p) if (v >= LargeAreaPercent) n++; return n; }
    }

    [MenuItem("Tools/TagOfChaos/Maps/Measure Palette Coverage (All)")]
    public static void MeasureAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var sb = new StringBuilder("[PaletteCoverage] map: graded % per color (large colors) / raw\n");
        foreach (string map in TwistedAtmosphere.Maps.Keys) sb.AppendLine(Format(Measure(map)));
        Debug.Log(sb.ToString());
    }

    public static string Format(Result r)
    {
        var sb = new StringBuilder($"{r.Map}: lum {r.MeanLuminance:0.000} |");
        foreach (float v in r.Percent) sb.Append($" {v:0.0}");
        sb.Append($" ({r.LargeColors}) | raw");
        foreach (float v in r.RawPercent) sb.Append($" {v:0.0}");
        sb.Append($" ({r.RawLargeColors})");
        return sb.ToString();
    }

    public static Result Measure(string map)
    {
        string path = TwistedAtmosphere.MapScenePath(map);
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var palette = AssetDatabase.LoadAssetAtPath<ColorPaletteSO>(PalettePath);
        Camera cam = Camera.main;
        var layer = cam.GetComponent<PostProcessLayer>();
        var rt = new RenderTexture(W, H, 24);
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        float oldFov = cam.fieldOfView;
        cam.fieldOfView = 60f;
        var result = new Result { Map = map };
        try
        {
            Vector3[] graded = PaletteHsv(cam, layer, palette, rt, tex, true);
            Vector3[] raw = PaletteHsv(cam, layer, palette, rt, tex, false);
            List<Vector3[]> views = Views();
            result.Percent = Coverage(cam, layer, true, graded, views, rt, tex, out result.MeanLuminance);
            result.RawPercent = Coverage(cam, layer, false, raw, views, rt, tex, out _);
        }
        finally
        {
            if (layer != null) layer.enabled = true;
            cam.fieldOfView = oldFov;
            rt.Release();
            Object.DestroyImmediate(tex);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single); // 카메라 이동 버리기
        }
        return result;
    }

    private static Color[] Render(Camera cam, RenderTexture rt, Texture2D tex)
    {
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        return tex.GetPixels();
    }

    // 조명 없는 색판(밝기 55%)을 맵 위 높은 곳에서 찍어 보정 후 팔레트 색의 HSV를 얻는다.
    private static Vector3[] PaletteHsv(Camera cam, PostProcessLayer layer, ColorPaletteSO palette, RenderTexture rt, Texture2D tex, bool graded)
    {
        int k = palette.Count;
        var holder = new GameObject("__PaletteSwatches");
        Shader unlit = Shader.Find("Unlit/Color");
        cam.transform.SetPositionAndRotation(new Vector3(0f, 500f, 0f), Quaternion.identity);
        var mats = new List<Material>();
        for (int i = 0; i < k; i++)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.transform.SetParent(holder.transform);
            var m = new Material(unlit) { color = palette.GetColor(i) * 0.55f };
            mats.Add(m);
            q.GetComponent<Renderer>().sharedMaterial = m;
            q.transform.position = cam.transform.position + new Vector3(-4.5f + i, 0f, 6f);
            q.transform.localScale = Vector3.one * 0.95f;
        }
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        if (layer != null) layer.enabled = graded;
        Color[] px = Render(cam, rt, tex);
        RenderSettings.fog = fog;
        var hsv = new Vector3[k];
        for (int i = 0; i < k; i++)
        {
            Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + new Vector3(-4.5f + i, 0f, 6f));
            Color c = px[Mathf.Clamp((int)(vp.y * H), 0, H - 1) * W + Mathf.Clamp((int)(vp.x * W), 0, W - 1)];
            Color.RGBToHSV(c, out hsv[i].x, out hsv[i].y, out hsv[i].z);
        }
        Object.DestroyImmediate(holder);
        foreach (Material m in mats) Object.DestroyImmediate(m);
        return hsv;
    }

    private static List<Vector3[]> Views()
    {
        var views = new List<Vector3[]>();
        foreach (float r in new[] { 12f, 30f, 55f })
            for (int a = 0; a < 4; a++)
            {
                float ang = a * Mathf.PI / 2f + 0.4f;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 p = dir * r;
                p.y = (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 120f) ? hit.point.y : 0f) + 2.2f;
                views.Add(new[] { p, p + dir * (a % 2 == 0 ? 10f : -10f) + Vector3.up * 0.5f });
            }
        return views;
    }

    private static float[] Coverage(Camera cam, PostProcessLayer layer, bool graded, Vector3[] pal, List<Vector3[]> views, RenderTexture rt, Texture2D tex, out float meanLum)
    {
        if (layer != null) layer.enabled = graded;
        var count = new double[pal.Length];
        double total = 0, lum = 0;
        foreach (Vector3[] v in views)
        {
            cam.transform.position = v[0];
            cam.transform.LookAt(v[1]);
            foreach (Color c in Render(cam, rt, tex))
            {
                total++;
                lum += c.grayscale;
                Color.RGBToHSV(c, out float h, out float s, out float val);
                if (val < 0.06f) continue;
                int best = -1;
                float bestD = HueTolerance;
                for (int k = 0; k < pal.Length; k++)
                {
                    if (s < Mathf.Max(0.12f, pal[k].y * 0.45f)) continue; // 채도가 너무 빠진 회색 면은 위장 색이 아니다
                    float d = Mathf.Abs(h - pal[k].x);
                    d = Mathf.Min(d, 1f - d);
                    if (d < bestD) { bestD = d; best = k; }
                }
                if (best >= 0) count[best]++;
            }
        }
        meanLum = (float)(lum / total);
        var percent = new float[pal.Length];
        for (int k = 0; k < pal.Length; k++) percent[k] = (float)(100.0 * count[k] / total);
        return percent;
    }
}
