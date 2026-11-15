using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 버튼·입력칸 반응(Request1009Plan.md §79 효과 1): 마우스를 올리거나(버튼) 선택하면(입력칸) 뒤에 보라 빛이 0.15초에 걸쳐 켜지고,
// 누르면 살짝 작아진다. 빛은 그림 파일 없이 UiSoftSprites.Glow로 그린다. 비활성(interactable = false)이면 빛을 켜지 않는다.
[DisallowMultipleComponent]
public class UiHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    public const float FadeSeconds = 0.15f;
    public const float PressScale = 0.96f;

    [SerializeField] private Color glowColor = new Color(0.72f, 0.32f, 1f, 0.9f);
    [SerializeField] private float padding = 22f;      // 테두리 바깥으로 퍼지는 폭
    [SerializeField] private bool glowWhenSelected;    // 입력칸: 선택하는 동안 빛
    [SerializeField, Range(0f, 1f)] private float idleGlow; // 늘 조금 빛나게(방 만들기 버튼)

    private RectTransform rect, glowRect;
    private Image glow;
    private Selectable selectable;
    private bool hover, pressed, selected;
    private float level;
    private Vector3 baseScale = Vector3.one;

    public float Level => level;

    public void Configure(Color color, float pad, bool whenSelected, float idle)
    {
        glowColor = color;
        padding = pad;
        glowWhenSelected = whenSelected;
        idleGlow = idle;
    }

    private void Awake()
    {
        rect = (RectTransform)transform;
        selectable = GetComponent<Selectable>();
        baseScale = rect.localScale;
    }

    private void OnEnable() => EnsureGlow();

    private void EnsureGlow()
    {
        if (glow != null || rect == null || rect.parent == null) return;
        var go = new GameObject(name + "_Glow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        glowRect = (RectTransform)go.transform;
        glowRect.SetParent(rect.parent, false);
        glowRect.SetSiblingIndex(rect.GetSiblingIndex()); // 버튼 바로 뒤(먼저 그려진다)
        glowRect.anchorMin = glowRect.anchorMax = glowRect.pivot = new Vector2(0.5f, 0.5f);
        glow = go.GetComponent<Image>();
        glow.sprite = UiSoftSprites.Glow;
        glow.type = Image.Type.Sliced;
        glow.raycastTarget = false;
        glow.color = Transparent;
        level = 0f;
    }

    private Color Transparent => new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

    private bool Interactable => selectable == null || selectable.IsInteractable();

    private void LateUpdate()
    {
        if (glow == null) { EnsureGlow(); if (glow == null) return; }
        bool on = Interactable && (hover || (glowWhenSelected && selected));
        float target = Interactable ? (on ? 1f : idleGlow) : 0f;
        level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / FadeSeconds);
        glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * level);
        glow.enabled = level > 0.001f && rect.gameObject.activeInHierarchy;
        // 버튼 자리를 따라간다(레이아웃·움직임)
        glowRect.position = rect.TransformPoint(rect.rect.center);
        glowRect.sizeDelta = rect.rect.size + Vector2.one * padding * 2f;
        Vector3 s = pressed && Interactable ? baseScale * PressScale : baseScale;
        rect.localScale = Vector3.MoveTowards(rect.localScale, s, Time.unscaledDeltaTime * 2f);
        glowRect.localScale = rect.localScale;
    }

    private void OnDisable()
    {
        hover = pressed = false;
        if (rect != null) rect.localScale = baseScale;
        if (glow != null) glow.enabled = false;
    }

    private void OnDestroy()
    {
        if (glow != null) Destroy(glow.gameObject);
    }

    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) { hover = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;
    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) => selected = false;
}
