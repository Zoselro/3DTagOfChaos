using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 벽 너머로 보이는 화면 표시(Request1009Plan.md §69 F5 — 술래의 내 사이렌 함정). 매 프레임 Begin → Add(월드 위치)… → End로 채운다.
// 화면 안이면 그 자리에 마름모 + 거리, 화면 밖·뒤면 가장자리에 붙인 화살표. 표시 칸은 모아 두고 다시 쓴다(할당 없음).
public class ScreenEdgeMarkers : MonoBehaviour
{
    private const float Size = 22f;
    private const float EdgeInset = 36f;

    private struct Marker { public RectTransform Root; public Image Icon; public TMP_Text Label; }

    private readonly List<Marker> pool = new List<Marker>();
    private RectTransform canvasRect;
    private Color color;
    private int used;

    public static ScreenEdgeMarkers Create(string name, Color color)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var markers = go.AddComponent<ScreenEdgeMarkers>();
        markers.canvasRect = (RectTransform)go.transform;
        markers.color = color;
        return markers;
    }

    public void Begin() => used = 0;

    public void Add(Vector3 world)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(world);
        bool behind = sp.z < 0f;
        if (behind) sp = -sp;
        float w = Screen.width, h = Screen.height;
        bool onScreen = !behind && sp.x >= 0f && sp.x <= w && sp.y >= 0f && sp.y <= h;
        Vector2 pos = sp;
        float angle = 0f;
        if (!onScreen)
        {
            Vector2 centre = new Vector2(w, h) * 0.5f;
            Vector2 dir = (pos - centre);
            if (dir.sqrMagnitude < 1f) dir = Vector2.down;
            dir.Normalize();
            float scale = Mathf.Min((w * 0.5f - EdgeInset) / Mathf.Max(0.001f, Mathf.Abs(dir.x)), (h * 0.5f - EdgeInset) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
            pos = centre + dir * scale;
            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        }
        Marker m = Next();
        float k = canvasRect.rect.width / Mathf.Max(1f, w);
        m.Root.anchoredPosition = new Vector2(pos.x * k, pos.y * k);
        m.Icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, onScreen ? 45f : angle + 45f);
        m.Icon.rectTransform.localScale = onScreen ? Vector3.one : new Vector3(1f, 1f, 1f) * 0.8f;
        m.Label.text = Mathf.RoundToInt(Vector3.Distance(cam.transform.position, world)) + "m";
    }

    public void End()
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].Root.gameObject.activeSelf != (i < used)) pool[i].Root.gameObject.SetActive(i < used);
    }

    private Marker Next()
    {
        if (used < pool.Count) return pool[used++];
        RectTransform root = EscapeHud.NewRect("Marker", transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Size, Size));
        Image icon = EscapeHud.NewImage("Icon", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Size, Size));
        icon.color = color;
        icon.raycastTarget = false;
        TMP_Text label = EscapeHud.NewText("Distance", root, 16f, TextAlignmentOptions.Center);
        label.rectTransform.anchoredPosition = new Vector2(0f, -Size);
        label.rectTransform.sizeDelta = new Vector2(80f, 22f);
        label.color = color;
        var m = new Marker { Root = root, Icon = icon, Label = label };
        pool.Add(m);
        used++;
        return m;
    }
}
