using UnityEngine;
using UnityEngine.UI;

// 인벤토리 1~4칸 바로 위 스태미나 막대(Request1009Plan.md §46.3 — 쿠키·스파이 본인만, 2026-10-31 사용자 요청으로 위치 옮김). 가득 차면 1초 뒤 사라지고,
// 지친 동안(0 → 35까지)은 빨갛게 깜빡인다. EscapeHud가 붙인다. 괴물·관전·탈출한 사람에게는 보이지 않는다.
public class StaminaHud : MonoBehaviour
{
    private const float Height = 12f;
    private const float GapAboveHotbar = 8f;
    private const float HideAfterFull = 1f;
    private const float FadeSpeed = 4f;
    private const float BlinkSpeed = 6f;

    private static readonly Color Normal = new Color(1f, 0.82f, 0.35f);
    private static readonly Color Low = new Color(1f, 0.55f, 0.25f);
    private static readonly Color Tired = new Color(1f, 0.25f, 0.2f);

    private CanvasGroup group;
    private Image fill;
    private float fullSince = -1f;

    // width: 인벤토리 칸 줄 너비, hotbarTop: 인벤토리 칸 윗변 높이(화면 아래에서).
    public void Init(float width, float hotbarTop)
    {
        float barWidth = width - 6f;
        RectTransform root = EscapeHud.NewRect("Stamina", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, hotbarTop + GapAboveHotbar + (Height + 6f) * 0.5f), new Vector2(barWidth + 6f, Height + 6f));
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
        fill.color = Normal;
    }

    public static StaminaModel LocalStamina()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is HideOrSeekPlayer cookie && CharacterRegistry.IsAlive(c) && cookie.IsMine && !cookie.IsBroken) return cookie.Stamina;
        return null;
    }

    private void Update()
    {
        if (group == null) return;
        StaminaModel s = LocalStamina();
        bool left = Photon.Pun.PhotonNetwork.LocalPlayer != null && EscapeManager.HasLeftMap(Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber);
        float target = 0f;
        if (s != null && !left)
        {
            if (!s.IsFull) fullSince = -1f;
            else if (fullSince < 0f) fullSince = Time.unscaledTime;
            target = fullSince < 0f || Time.unscaledTime - fullSince < HideAfterFull ? 1f : 0f;

            fill.fillAmount = s.Normalized;
            if (s.IsExhausted)
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * BlinkSpeed * Mathf.PI);
                fill.color = Color.Lerp(Tired, new Color(0.5f, 0.1f, 0.1f), blink);
            }
            else fill.color = s.Value < s.Settings.dodgeCost ? Low : Normal;
        }
        group.alpha = Mathf.MoveTowards(group.alpha, target, FadeSpeed * Time.unscaledDeltaTime);
    }
}
