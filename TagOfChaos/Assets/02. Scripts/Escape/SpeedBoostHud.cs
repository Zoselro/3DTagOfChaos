using UnityEngine;
using UnityEngine.UI;

// 인라인 스케이트 남은 시간 막대(Request1009Plan.md §69 B4) — 스태미나 막대 바로 위, 본인만. 스케이트를 쓰면 가득 찼다가 효과가 끝날 때 0이 되고 사라진다.
public class SpeedBoostHud : MonoBehaviour
{
    private const float Height = 10f;
    private const float GapAboveStamina = 26f; // 스태미나 막대(높이 12 + 여백) 위
    private const float FadeSpeed = 5f;
    private static readonly Color Fill = new Color(0.35f, 0.85f, 1f);

    private CanvasGroup group;
    private Image fill;

    public void Init(float width, float hotbarTop)
    {
        float barWidth = width - 6f;
        RectTransform root = EscapeHud.NewRect("SpeedBoost", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, hotbarTop + GapAboveStamina + (Height + 6f) * 0.5f + 8f), new Vector2(barWidth + 6f, Height + 6f));
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        Image bg = root.gameObject.AddComponent<Image>();
        bg.sprite = EscapeHud.WhiteSprite();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;
        fill = EscapeHud.NewImage("Fill", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(barWidth, Height));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.color = Fill;
    }

    private void Update()
    {
        if (group == null) return;
        HideOrSeekPlayer me = LocalCookie();
        float left = me != null ? me.SpeedBoostRemaining01 : 0f;
        if (left > 0f) fill.fillAmount = left;
        group.alpha = Mathf.MoveTowards(group.alpha, left > 0f ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
    }

    private static HideOrSeekPlayer LocalCookie()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is HideOrSeekPlayer cookie && cookie.IsMine && CharacterRegistry.IsAlive(c)) return cookie;
        return null;
    }
}
