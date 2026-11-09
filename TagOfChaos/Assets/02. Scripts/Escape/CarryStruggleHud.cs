using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 들린 쿠키 안내(Request1009Plan.md §69 C2) — 본인이 다른 쿠키에게 들린 동안 화면 가운데 아래에 "좌우 연타" 문구와 진행 막대.
// 좌·우를 번갈아 누를수록 막대가 차고(HideOrSeekPlayer.StruggleProgress01), 다 차면 드는 쪽이 내려놓는다.
public class CarryStruggleHud : MonoBehaviour
{
    private const float Width = 420f;
    private const float BarHeight = 12f;
    private const float FadeSpeed = 6f;
    private static readonly Color Fill = new Color(1f, 0.8f, 0.3f);

    private CanvasGroup group;
    private Image fill;
    private TMP_Text label;

    public void Init()
    {
        RectTransform root = EscapeHud.NewRect("CarryStruggle", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -170f), new Vector2(Width, 64f));
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        Image bg = root.gameObject.AddComponent<Image>();
        bg.sprite = EscapeHud.WhiteSprite();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;
        label = EscapeHud.NewText("Label", root, 24f, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = new Vector2(0f, 0.4f);
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        Image barBg = EscapeHud.NewImage("Bar", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(Width - 30f, BarHeight));
        barBg.color = new Color(1f, 1f, 1f, 0.2f);
        fill = EscapeHud.NewImage("Fill", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(Width - 30f, BarHeight));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.color = Fill;
    }

    private void Update()
    {
        if (group == null) return;
        HideOrSeekPlayer me = LocalCookie();
        bool on = me != null && me.IsCarriedByCookie;
        if (on)
        {
            label.text = EscapeTextsSO.Current.carryStruggle;
            fill.fillAmount = me.StruggleProgress01;
        }
        group.alpha = Mathf.MoveTowards(group.alpha, on ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
    }

    private static HideOrSeekPlayer LocalCookie()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is HideOrSeekPlayer cookie && cookie.IsMine && CharacterRegistry.IsAlive(c)) return cookie;
        return null;
    }
}
