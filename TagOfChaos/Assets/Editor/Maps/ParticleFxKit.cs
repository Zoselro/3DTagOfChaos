using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 맵 겉모습 파티클을 만드는 공용 도구(Request1009Plan.md §39 분수 물 → §41 초콜릿 공장 연기·초콜릿 방울에서 함께 쓴다).
// 반복·미리 돌려 둠(prewarm)·화면 밖 멈춤인 가벼운 시스템, 수평 고리 모양, 나타났다 사라지는 알파, 반투명 재질·둥근 텍스처.
public static class ParticleFxKit
{
    public static ParticleSystem NewSystem(Transform parent, string name, Vector3 localPos, Material mat, int maxParticles)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;            // 씬이 열릴 때 이미 흐르고 있게
        main.playOnAwake = true;
        main.duration = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = maxParticles;
        main.cullingMode = ParticleSystemCullingMode.Pause; // 화면 밖이면 멈춘다
        var shape = ps.shape;
        shape.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.sortingFudge = 0f;
        return ps;
    }

    // 수평 고리(가장자리). spread > 0이면 그 비율 간격의 각도에서만 나온다(1/6 = 60°마다 6곳). 원은 바깥 방향으로 쏜다.
    public static void RingShape(ParticleSystem ps, float radius, float spread)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0f;
        shape.arc = 360f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
        shape.arcSpread = spread;
        shape.rotation = new Vector3(90f, 0f, 0f); // 원(XY 평면)을 땅과 나란히(XZ)
        shape.randomDirectionAmount = 0f;
    }

    // 수평 원판 안 어디서나(굴뚝 입구·통 표면) 위로 쏜다 — 좁은 원뿔(밑면 = 원판)을 위로 세운다.
    public static void DiscShape(ParticleSystem ps, float radius)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = radius;
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 원뿔 축(+Z)을 위(+Y)로
        shape.randomDirectionAmount = 0f;
    }

    // 처음에 밝게 나타나 끝에서 사라진다(peak = 가장 진한 알파 배율, end = 사라지기 시작하는 시점 비율)
    public static void FadeOut(ParticleSystem ps, float peak, float end) => FadeOut(ps, peak, end, Color.white, Color.white);

    public static void FadeOut(ParticleSystem ps, float peak, float end, Color from, Color to)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(peak * 0.6f, 0f), new GradientAlphaKey(peak, 0.15f), new GradientAlphaKey(peak, Mathf.Min(end, 0.95f)), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    // Particles/Standard Unlit, Fade. 재질 에셋으로 두어 빌드에 셰이더 변형(_ALPHABLEND_ON)이 들어간다(EscapeVisuals 참고).
    public static Material FadeMaterial(string path, Texture2D tex)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Particles/Standard Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.mainTexture = tex;
        m.SetFloat("_Mode", 2f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    // 가운데가 진한 둥근 점(물방울·연기 덩이)
    public static float Dot(float d) => Mathf.Clamp01(1f - d);
    // 가장자리가 진한 얇은 고리(물결)
    public static float Ring(float d) => Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.12f) * (d < 1f ? 1f : 0f);
    // 가장자리가 부드럽게 퍼지는 큰 덩이(연기) — 점보다 넓게 진하다
    public static float Puff(float d) => Mathf.Clamp01(1f - d * d);

    // 흰색 + 알파(반지름 0~1 → alpha(d)²)의 64 px 텍스처. 이미 있으면 그대로 쓴다.
    public static Texture2D RadialTexture(string path, Func<float, float> alpha)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float a = alpha(Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
        }
        tex.SetPixels32(pixels);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
