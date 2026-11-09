using UnityEngine;

// 재료 건물 지붕 표시(Request1009Plan.md §69 D3 — 21번). 건물 지붕 위에 필요한 재료 아이콘(카메라를 보는 큰 판)과 하늘로 뻗는 재료 색 빛기둥을 세워
// 멀리서도 어디에 무엇을 넣어야 하는지 보이게 한다. 재료를 넣어 가동되면 초록 체크 + 초록 빛기둥. 보일지는 QuadrantBuilding이 정한다(쿠키·술래 O, 스파이 X).
public class RoofMarker : MonoBehaviour
{
    private const float IconSize = 4.5f;
    private const float IconLift = 2.5f;       // 지붕 꼭대기 위
    private const float PillarHeight = 40f;
    private const float PillarWidth = 1.1f;
    private const float UndergroundBelow = -5f; // 수리 지점이 이보다 낮으면 지하 건물
    private const float SurfaceY = -1f;         // 지하 표시가 넘지 않을 높이(지상 바닥 바로 아래)
    private static readonly Color DoneColor = new Color(0.3f, 1f, 0.4f);

    private static Sprite checkSprite;
    private SpriteRenderer icon;
    private SpriteRenderer iconBack;
    private Renderer pillar;
    private float roofY;
    private string shownKey;

    public static RoofMarker Create(QuadrantBuilding building)
    {
        var go = new GameObject("RoofMarker");
        go.transform.SetParent(building.transform, false);
        var marker = go.AddComponent<RoofMarker>();
        marker.roofY = RoofTop(building.transform);
        marker.Build(building.RepairPoint);
        go.SetActive(false);
        return marker;
    }

    private static float RoofTop(Transform building)
    {
        float top = building.position.y;
        foreach (Renderer r in building.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.gameObject.activeInHierarchy || r.name.StartsWith("ESC_Glow") || r.name.StartsWith("Beam") || r is ParticleSystemRenderer) continue;
            if (r.bounds.size.y > 60f) continue; // 빛기둥 같은 긴 장식은 빼고
            top = Mathf.Max(top, r.bounds.max.y);
        }
        return top;
    }

    private void Build(Vector3 repairPoint)
    {
        // 지하 건물: 동굴 천장(지상 바닥)을 뚫고 지상에 보이지 않게 천장 아래에 둔다
        float iconY = roofY + IconLift, pillarHeight = PillarHeight;
        float ceiling = EscapeManager.Instance != null ? EscapeManager.Instance.CeilingAbove(new Vector3(repairPoint.x, roofY + 0.2f, repairPoint.z), 300f) : 0f;
        if (ceiling == 0f && repairPoint.y < UndergroundBelow) ceiling = SurfaceY; // 동굴 천장에 콜라이더가 없으면 지상 바닥 높이로
        if (ceiling != 0f && ceiling > repairPoint.y)
        {
            iconY = Mathf.Min(iconY, ceiling - IconSize - 1f);
            pillarHeight = Mathf.Max(0f, ceiling - iconY - IconSize * 0.5f - 0.3f);
        }
        Vector3 at = new Vector3(repairPoint.x, iconY, repairPoint.z);
        transform.position = at;
        iconBack = NewSprite("IconBack", CircleSprite(), new Color(0f, 0f, 0f, 0.55f), 1.25f, 0);
        icon = NewSprite("Icon", null, Color.white, 1f, 1);
        var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(p.GetComponent<Collider>());
        p.name = "Pillar";
        p.transform.SetParent(transform, false);
        p.transform.localPosition = Vector3.up * (pillarHeight * 0.5f + IconSize * 0.5f);
        p.transform.localScale = new Vector3(PillarWidth, pillarHeight * 0.5f, PillarWidth);
        p.SetActive(pillarHeight > 0.5f);
        pillar = p.GetComponent<Renderer>();
        pillar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pillar.receiveShadows = false;
    }

    private SpriteRenderer NewSprite(string name, Sprite sprite, Color color, float scale, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.transform.localScale = Vector3.one * scale;
        return sr;
    }

    public void Show(bool visible, ItemSO material, bool done)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        if (!visible) return;
        string key = (material != null ? material.ItemId : "") + (done ? "#done" : "");
        if (key == shownKey) return;
        shownKey = key;
        icon.sprite = done ? CheckSprite() : material != null ? material.Icon : null;
        Fit(icon, IconSize * 0.8f);
        Fit(iconBack, IconSize);
        Color c = done ? DoneColor : material != null ? material.Tint : Color.white;
        EscapeVisuals.Tint(pillar.gameObject, c, 2.5f);
    }

    private static void Fit(SpriteRenderer sr, float size)
    {
        if (sr.sprite == null) return;
        Vector2 s = sr.sprite.bounds.size;
        float k = size / Mathf.Max(0.01f, Mathf.Max(s.x, s.y));
        sr.transform.localScale = Vector3.one * k;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Quaternion face = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        icon.transform.rotation = face;
        iconBack.transform.rotation = face;
        iconBack.transform.position = icon.transform.position + face * Vector3.forward * 0.05f; // 아이콘 바로 뒤
    }

    // 초록 체크(코드로 그림 — 에셋 없이).
    private static Sprite CheckSprite()
    {
        if (checkSprite != null) return checkSprite;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        Vector2 a = new Vector2(0.18f, 0.52f), b = new Vector2(0.42f, 0.26f), c = new Vector2(0.84f, 0.76f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                float d = Mathf.Min(SegDist(p, a, b), SegDist(p, b, c));
                float alpha = Mathf.Clamp01((0.085f - d) * n * 0.5f);
                px[y * n + x] = new Color(0.3f, 1f, 0.4f, alpha);
            }
        tex.SetPixels32(px);
        tex.Apply();
        checkSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return checkSprite;
    }

    private static Sprite circle;
    private static Sprite CircleSprite()
    {
        if (circle != null) return circle;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 8f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return circle;
    }

    private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
