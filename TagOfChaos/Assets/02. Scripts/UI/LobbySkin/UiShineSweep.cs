using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 방 만들기 강조(Request1009Plan.md §79 효과 3): 몇 초마다 빛줄기가 버튼을 왼쪽에서 오른쪽으로 지나가고(버튼 모양 밖으로 나가지 않게 가린다),
// 둘레에 보라 마법 가루가 천천히 떠오른다. 버튼의 Image 모양을 그대로 가리개로 쓴다.
[DisallowMultipleComponent]
public class UiShineSweep : MonoBehaviour
{
    [SerializeField] private float everySeconds = 3.5f, sweepSeconds = 0.7f;
    [SerializeField] private Color shine = new Color(1f, 0.92f, 1f, 0.45f);
    [SerializeField] private Color dust = new Color(0.8f, 0.5f, 1f, 1f);
    [SerializeField] private int dustCount = 10;

    private RectTransform bar;
    private float startedAt = -10f;
    private readonly List<RectTransform> motes = new List<RectTransform>();
    private readonly List<Image> moteImages = new List<Image>();
    private readonly List<Vector3> moteSeed = new List<Vector3>(); // x 비율, 속도, 위상

    private void Start()
    {
        var owner = GetComponent<Image>();
        var rt = (RectTransform)transform;

        var maskGo = new GameObject("ShineMask", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(LayoutElement));
        maskGo.GetComponent<LayoutElement>().ignoreLayout = true;
        var mr = (RectTransform)maskGo.transform;
        mr.SetParent(rt, false);
        mr.SetSiblingIndex(0);
        mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.offsetMin = mr.offsetMax = Vector2.zero;
        var mi = maskGo.GetComponent<Image>();
        if (owner != null) { mi.sprite = owner.sprite; mi.type = owner.type; mi.pixelsPerUnitMultiplier = owner.pixelsPerUnitMultiplier; }
        mi.raycastTarget = false;
        maskGo.GetComponent<Mask>().showMaskGraphic = false;

        var barGo = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        bar = (RectTransform)barGo.transform;
        bar.SetParent(mr, false);
        bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
        bar.pivot = new Vector2(0.5f, 0.5f);
        bar.sizeDelta = new Vector2(rt.rect.height * 0.55f, rt.rect.height * 2.2f);
        bar.localRotation = Quaternion.Euler(0f, 0f, -22f);
        var bi = barGo.GetComponent<Image>();
        bi.sprite = UiSoftSprites.Dot;
        bi.raycastTarget = false;
        bi.color = shine;
        barGo.SetActive(false);

        for (int i = 0; i < dustCount; i++)
        {
            var go = new GameObject("Dust", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var r = (RectTransform)go.transform;
            r.SetParent(rt, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = Vector2.one * Random.Range(5f, 10f);
            var img = go.GetComponent<Image>();
            img.sprite = UiSoftSprites.Dot;
            img.raycastTarget = false;
            motes.Add(r);
            moteImages.Add(img);
            moteSeed.Add(new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.35f, 0.8f), Random.value));
        }
        startedAt = Time.unscaledTime - everySeconds + 1f;
    }

    private void Update()
    {
        if (bar == null) return;
        var rt = (RectTransform)transform;
        float t = Time.unscaledTime;
        if (t - startedAt >= everySeconds) startedAt = t;
        float k = (t - startedAt) / sweepSeconds;
        bool on = k < 1f;
        if (bar.gameObject.activeSelf != on) bar.gameObject.SetActive(on);
        if (on) bar.anchoredPosition = new Vector2(Mathf.Lerp(-bar.sizeDelta.x, rt.rect.width + bar.sizeDelta.x, Smooth(k)), 0f);

        // 마법 가루: 버튼 둘레 아래에서 위로 떠오르며 옅어진다(3초 주기, 저마다 위상이 다르다)
        Vector2 size = rt.rect.size;
        for (int i = 0; i < motes.Count; i++)
        {
            Vector3 s = moteSeed[i];
            float life = Mathf.Repeat(t * s.y * 0.35f + s.z, 1f);
            float x = s.x * size.x * 1.05f + Mathf.Sin((t + s.z * 10f) * 1.7f) * 6f;
            float y = Mathf.Lerp(-size.y * 0.5f, size.y * 0.5f + 34f, life);
            motes[i].anchoredPosition = new Vector2(x, y);
            moteImages[i].color = new Color(dust.r, dust.g, dust.b, Mathf.Sin(life * Mathf.PI) * 0.8f);
        }
    }

    private static float Smooth(float x) => x * x * (3f - 2f * x);
}
