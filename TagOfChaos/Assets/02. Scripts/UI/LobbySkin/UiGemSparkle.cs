using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 보석 반짝임(Request1009Plan.md §79 효과 2): 액자 귀퉁이·스크롤 손잡이의 보라 보석 자리에 은은한 빛이 숨 쉬듯 밝아졌다 어두워지고,
// 가끔 작은 네 갈래 별이 반짝인다. 자리는 이 RectTransform 안의 비율(0~1)로 준다. 그림 파일 없이 UiSoftSprites로 그린다.
[DisallowMultipleComponent]
public class UiGemSparkle : MonoBehaviour
{
    [SerializeField] private Vector2[] points = { new Vector2(0.06f, 0.95f), new Vector2(0.94f, 0.95f), new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.05f) };
    [SerializeField] private Color color = new Color(0.8f, 0.45f, 1f, 1f);
    [SerializeField] private float glowSize = 46f, starSize = 34f;
    [SerializeField] private Vector2 interval = new Vector2(0.8f, 2.4f); // 별이 뜨는 간격(초)

    private const float BreathSeconds = 2.6f, StarSeconds = 0.7f;
    private readonly List<Image> glows = new List<Image>();
    private readonly List<float> phase = new List<float>();
    private Image star;
    private float starAt = -10f, nextStar;

    public void Configure(Vector2[] normalizedPoints, float glowPx, float starPx)
    {
        points = normalizedPoints;
        glowSize = glowPx;
        starSize = starPx;
    }

    private void OnEnable()
    {
        if (glows.Count == 0) Build();
        nextStar = Time.unscaledTime + Random.Range(interval.x, interval.y);
    }

    private void Build()
    {
        var rt = (RectTransform)transform;
        foreach (Vector2 p in points)
        {
            Image g = NewImage("GemGlow", UiSoftSprites.Dot, glowSize, p);
            glows.Add(g);
            phase.Add(Random.value * Mathf.PI * 2f);
        }
        star = NewImage("GemStar", UiSoftSprites.Star, starSize, Vector2.zero);
        star.enabled = false;
    }

    private Image NewImage(string n, Sprite sprite, float size, Vector2 at)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var r = (RectTransform)go.transform;
        r.SetParent(transform, false);
        r.anchorMin = r.anchorMax = at;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = Vector2.one * size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = new Color(color.r, color.g, color.b, 0f);
        return img;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < glows.Count; i++)
        {
            float b = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / BreathSeconds + phase[i]);
            glows[i].color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.15f, 0.55f, b));
        }
        if (star == null || points.Length == 0) return;
        if (t >= nextStar)
        {
            starAt = t;
            nextStar = t + Random.Range(interval.x, interval.y);
            var r = (RectTransform)star.transform;
            Vector2 p = points[Random.Range(0, points.Length)];
            r.anchorMin = r.anchorMax = p;
            r.anchoredPosition = Random.insideUnitCircle * 4f;
            r.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 45f));
        }
        float k = (t - starAt) / StarSeconds;
        star.enabled = k >= 0f && k < 1f;
        if (star.enabled)
        {
            float a = Mathf.Sin(k * Mathf.PI);
            star.color = new Color(1f, 0.9f, 1f, a);
            star.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.1f, a);
        }
    }
}
