using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 로비 배경 분위기(Request1009Plan.md §79 효과 4): 배경 그림 위에 겹쳐 — 반딧불·보라 포자가 천천히 떠다니고, 등불 자리에 따뜻한 빛이 깜빡이고,
// 달 둘레 빛이 은은하게 밝아졌다 어두워지고, 아래쪽에 옅은 보라 안개가 흐른다. 이 RectTransform은 배경 그림과 같은 크기로 늘여 둔다
// (자리는 배경 그림 안의 비율 0~1). Screen Space - Overlay 캔버스라 파티클 시스템 대신 UI 이미지 조각을 코드로 움직인다.
[DisallowMultipleComponent]
public class LobbyAmbience : MonoBehaviour
{
    [SerializeField] private int fireflies = 34;
    [SerializeField] private Vector2[] lanterns = { new Vector2(0.21f, 0.76f), new Vector2(0.35f, 0.81f), new Vector2(0.51f, 0.73f), new Vector2(0.236f, 0.495f), new Vector2(0.112f, 0.186f) };
    [SerializeField] private Vector2 moon = new Vector2(0.745f, 0.93f);
    [SerializeField] private Color warm = new Color(1f, 0.78f, 0.42f, 1f);
    [SerializeField] private Color purple = new Color(0.78f, 0.45f, 1f, 1f);
    [SerializeField] private Color fogColor = new Color(0.55f, 0.35f, 0.85f, 0.32f);

    private struct Fly { public RectTransform Rect; public Image Img; public Vector2 Home; public float Speed, Phase, Size; public bool Warm; }
    private readonly List<Fly> flies = new List<Fly>();
    private readonly List<Image> lanternGlows = new List<Image>();
    private readonly List<float> lanternSeed = new List<float>();
    private Image moonGlow;
    private RawImage fogA, fogB;

    private void Start()
    {
        moonGlow = Spot("MoonGlow", moon, 360f, new Color(0.85f, 0.75f, 1f, 0.25f));
        foreach (Vector2 p in lanterns)
        {
            lanternGlows.Add(Spot("LanternGlow", p, 150f, new Color(warm.r, warm.g, warm.b, 0.4f)));
            lanternSeed.Add(Random.value * 100f);
        }
        fogA = Fog("FogBack", 0.42f, 0.55f);
        fogB = Fog("FogFront", 0.28f, 0.8f);
        for (int i = 0; i < fireflies; i++)
        {
            bool w = i % 3 != 0;
            float size = w ? Random.Range(7f, 13f) : Random.Range(10f, 18f);
            var img = Spot(w ? "Firefly" : "Spore", Vector2.zero, size, Color.clear);
            flies.Add(new Fly
            {
                Rect = (RectTransform)img.transform, Img = img, Warm = w, Size = size,
                Home = new Vector2(Random.value, Random.Range(0.05f, 0.7f)),
                Speed = Random.Range(0.25f, 0.6f), Phase = Random.value * 100f,
            });
        }
    }

    private Image Spot(string n, Vector2 at, float size, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        var r = (RectTransform)go.transform;
        r.SetParent(transform, false);
        r.anchorMin = r.anchorMax = at;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = Vector2.one * size;
        var img = go.GetComponent<Image>();
        img.sprite = UiSoftSprites.Dot;
        img.raycastTarget = false;
        img.color = c;
        return img;
    }

    private RawImage Fog(string n, float height, float alpha)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(RawImage));
        var r = (RectTransform)go.transform;
        r.SetParent(transform, false);
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, height);
        r.offsetMin = r.offsetMax = Vector2.zero;
        var img = go.GetComponent<RawImage>();
        img.texture = UiSoftSprites.Fog;
        img.raycastTarget = false;
        img.color = new Color(fogColor.r, fogColor.g, fogColor.b, fogColor.a * alpha);
        return img;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        var rt = (RectTransform)transform;
        Vector2 size = rt.rect.size;
        if (size.x < 1f) return;

        // 반딧불·포자: 제자리 둘레를 천천히 맴돌며 위로 떠오르다 다시 아래에서(10~25초 주기), 반딧불은 깜빡인다
        for (int i = 0; i < flies.Count; i++)
        {
            Fly f = flies[i];
            float rise = Mathf.Repeat(t * f.Speed * 0.02f + f.Phase, 1f);
            float x = f.Home.x + Mathf.Sin(t * f.Speed * 0.7f + f.Phase) * 0.03f;
            float y = Mathf.Repeat(f.Home.y + rise * 0.6f, 0.85f) + Mathf.Sin(t * f.Speed + f.Phase * 2f) * 0.012f;
            f.Rect.anchorMin = f.Rect.anchorMax = new Vector2(x, y);
            float fade = Mathf.Clamp01(Mathf.Min(y, 0.85f - y) * 8f);
            float blink = f.Warm ? 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * (1.3f + f.Speed) + f.Phase), 3f) : 0.55f;
            Color c = f.Warm ? new Color(1f, 0.92f, 0.55f) : purple;
            f.Img.color = new Color(c.r, c.g, c.b, blink * fade * 0.9f);
        }

        // 등불: 불규칙하게 깜빡이는 따뜻한 빛
        for (int i = 0; i < lanternGlows.Count; i++)
        {
            float s = lanternSeed[i];
            float n = Mathf.PerlinNoise(t * 3f + s, s) * 0.6f + Mathf.PerlinNoise(t * 11f, s + 7f) * 0.4f;
            lanternGlows[i].color = new Color(warm.r, warm.g, warm.b, Mathf.Lerp(0.18f, 0.55f, n));
            lanternGlows[i].transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.1f, n);
        }

        // 달 둘레 빛: 8초 주기로 은은하게
        if (moonGlow != null)
        {
            float b = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 8f);
            moonGlow.color = new Color(0.85f, 0.75f, 1f, Mathf.Lerp(0.12f, 0.3f, b));
        }

        // 안개: 두 겹이 서로 다른 빠르기로 옆으로 흐른다
        if (fogA != null) fogA.uvRect = new Rect(t * 0.008f, 0f, 1.6f, 1f);
        if (fogB != null) fogB.uvRect = new Rect(-t * 0.014f + 0.37f, 0f, 2.2f, 1f);
    }
}
