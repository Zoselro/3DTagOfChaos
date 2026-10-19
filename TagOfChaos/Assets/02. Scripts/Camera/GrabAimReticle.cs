using UnityEngine;
using UnityEngine.UI;

// 괴물 화면 가운데 조준점과 조준 대상 표시(GameFixPlan.md F1). 잡을 수 있는 쿠키를 조준하면 조준점이 빨갛게
// 커지고, 그 쿠키 머리 위에 작은 표시가 뜬다 — 두 쿠키가 겹쳐 있을 때 누구를 잡게 될지 괴물이 알 수 있게 한다.
// 프리팹 없이 코드로 만든다(괴물 본인 클라이언트에만 하나).
public class GrabAimReticle : MonoBehaviour
{
    private bool hadTarget;
    private const float MarkerHeight = 2.4f; // 쿠키 발밑에서 머리 위까지(m)

    private MonsterGrabKillTrigger trigger;
    private Image dot;
    private Image marker;
    private RectTransform markerRect;
    private static Sprite whiteSprite;

    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color targetColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float idleSize = 6f;
    [SerializeField] private float targetSize = 12f;

    public static GrabAimReticle Create(MonsterController owner)
    {
        var root = new GameObject(nameof(GrabAimReticle));
        root.transform.SetParent(owner.transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var reticle = root.AddComponent<GrabAimReticle>();
        reticle.trigger = owner.GetComponentInChildren<MonsterGrabKillTrigger>(true);
        reticle.dot = reticle.CreateImage("Dot", new Vector2(0.5f, 0.5f));
        reticle.marker = reticle.CreateImage("TargetMarker", Vector2.zero);
        reticle.markerRect = reticle.marker.rectTransform;
        reticle.markerRect.sizeDelta = new Vector2(14f, 14f);
        reticle.markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f); // 마름모
        reticle.marker.color = reticle.targetColor;
        return reticle;
    }

    private Image CreateImage(string name, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var image = go.GetComponent<Image>();
        image.sprite = WhiteSprite();
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        return image;
    }

    private static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return whiteSprite;
    }

    private void LateUpdate()
    {
        // 결과 화면에서는 조준점을 숨긴다.
        bool visible = GamePhaseState.Current != GamePhase.Result;
        if (dot.enabled != visible) dot.enabled = visible;
        if (!visible)
        {
            if (marker.enabled) marker.enabled = false;
            return;
        }

        Transform target = trigger != null ? trigger.AimTransform : null; // 쿠키·AI 모두 같은 빨간 표시
        bool hasTarget = target != null;
        if (hasTarget && !hadTarget) GameAudio.Play(SoundId.MonsterAimLock); // 잡을 수 있는 쿠키를 조준에 잡은 순간(괴물 본인 2D, S4)
        hadTarget = hasTarget;

        dot.color = hasTarget ? targetColor : idleColor;
        float size = hasTarget ? targetSize : idleSize;
        dot.rectTransform.sizeDelta = new Vector2(size, size);

        Camera cam = Camera.main;
        bool showMarker = hasTarget && cam != null;
        if (showMarker)
        {
            Vector3 screen = cam.WorldToScreenPoint(target.position + Vector3.up * MarkerHeight);
            showMarker = screen.z > 0f;
            if (showMarker) markerRect.position = screen;
        }
        if (marker.enabled != showMarker) marker.enabled = showMarker;
    }
}
