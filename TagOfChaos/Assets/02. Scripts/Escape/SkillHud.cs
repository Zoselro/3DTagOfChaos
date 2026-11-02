using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 오른쪽 아래 스킬 칸 2개(Request1009Plan.md §26 H9 — 임시 모양): 이름 · 키 · 쿨타임(어둡게 덮고 남은 초).
//   괴물: 돌진(15초) · 소음폭탄(20초, 깔려 있으면 "설치됨")   쿠키·스파이: 회피(3초) · 생존자 스킬("미정")
// 내 캐릭터가 없거나(관전 등) 탈출했으면 숨긴다. EscapeHud가 붙인다. 키 이름은 KeyBindings(설정에서 바꾸면 바로 바뀐다).
public class SkillHud : MonoBehaviour
{
    private const float SlotSize = 104f;
    private const float RefreshSeconds = 0.1f;

    private struct Slot { public GameObject Root; public TMP_Text Name, Key, Time; public Image Cover; }

    private EscapeManager manager;
    private EscapeTextsSO texts;
    private RectTransform root;
    private readonly Slot[] slots = new Slot[2];
    private float nextRefresh;

    public void Init(EscapeManager owner, EscapeTextsSO t)
    {
        manager = owner;
        texts = t;
        root = EscapeHud.NewRect("Skills", transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(SlotSize * 2f + 12f, SlotSize));
        root.pivot = new Vector2(1f, 0f);
        for (int i = 0; i < slots.Length; i++) slots[i] = BuildSlot(i);
    }

    private Slot BuildSlot(int i)
    {
        RectTransform rt = EscapeHud.NewRect("Skill" + i, root, Vector2.zero, Vector2.zero, new Vector2(i * (SlotSize + 12f), 0f), new Vector2(SlotSize, SlotSize));
        rt.pivot = Vector2.zero;
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = EscapeHud.WhiteSprite(); bg.color = new Color(0.36f, 0.27f, 0.45f, 0.9f); bg.raycastTarget = false;
        var slot = new Slot { Root = rt.gameObject };
        slot.Name = Label(rt, "Name", 20f, TextAlignmentOptions.Center, new Vector2(0f, 0.55f), new Vector2(1f, 1f));
        slot.Key = Label(rt, "Key", 18f, TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(1f, 0.4f));
        slot.Key.color = new Color(1f, 0.85f, 0.4f);
        Image cover = EscapeHud.NewImage("Cover", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotSize, SlotSize));
        cover.color = new Color(0f, 0f, 0f, 0.7f);
        cover.type = Image.Type.Filled; cover.fillMethod = Image.FillMethod.Radial360; cover.fillOrigin = (int)Image.Origin360.Top; cover.fillClockwise = false;
        slot.Cover = cover;
        slot.Time = Label(rt, "Time", 30f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        return slot;
    }

    private static TMP_Text Label(RectTransform parent, string name, float size, TextAlignmentOptions align, Vector2 min, Vector2 max)
    {
        TMP_Text t = EscapeHud.NewText(name, parent, size, align);
        t.rectTransform.anchorMin = min; t.rectTransform.anchorMax = max;
        t.rectTransform.offsetMin = new Vector2(4f, 2f); t.rectTransform.offsetMax = new Vector2(-4f, -2f);
        return t;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh || root == null) return;
        nextRefresh = Time.unscaledTime + RefreshSeconds;
        IGameCharacter me = LocalCharacter();
        bool show = me != null && PhotonNetwork.LocalPlayer != null && !EscapeManager.HasLeftMap(PhotonNetwork.LocalPlayer.ActorNumber);
        if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        if (!show) return;
        if (me is MonsterController monster)
        {
            Set(slots[0], texts.skillDash, KeyAction.MonsterDash, monster.DashCooldownLeft, GameSettings.Current.TentacleDashCooldown, null);
            BombState(out float left, out bool placed);
            Set(slots[1], texts.skillBomb, KeyAction.MonsterSkill, left, EscapeAuthority.BombCooldown, placed ? texts.skillBombPlaced : null);
        }
        else if (me is HideOrSeekPlayer cookie)
        {
            Set(slots[0], texts.skillDodge, KeyAction.Dodge, cookie.DodgeCooldownLeft, HideOrSeekPlayer.DodgeCooldown, null);
            // 스태미나가 회피 비용보다 적으면 칸을 어둡게 덮는다(쿨타임 숫자는 그대로, §46.3 ST3).
            if (cookie.DodgeCooldownLeft <= 0.05f && cookie.Stamina != null && !cookie.Stamina.CanDodge) slots[0].Cover.fillAmount = 1f;
            Set(slots[1], texts.skillUnknown, null, 0f, 1f, null, disabled: true);
        }
    }

    // 소음폭탄: 깔아 둔 폭탄이 있으면 "설치됨", 치웠거나 터진 뒤에는 20초 쿨타임.
    private void BombState(out float left, out bool placed)
    {
        left = 0f; placed = false;
        EscapeState s = manager != null ? manager.State : null;
        if (s == null || PhotonNetwork.LocalPlayer == null) return;
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        double now = PhotonNetwork.Time;
        foreach (EscapeState.Bomb b in s.Bombs)
        {
            if (b.Owner != me) continue;
            if (!b.Spent) placed = true;
            else left = Mathf.Max(left, (float)(b.SpentAt + EscapeAuthority.BombCooldown - now));
        }
    }

    private static void Set(Slot slot, string name, KeyAction? key, float left, float total, string overlay, bool disabled = false)
    {
        slot.Name.text = name;
        slot.Key.text = key.HasValue ? KeyBindings.DisplayName(KeyBindings.Get(key.Value)) : string.Empty;
        bool cooling = left > 0.05f;
        float fill = disabled || overlay != null ? 1f : cooling ? Mathf.Clamp01(left / Mathf.Max(0.01f, total)) : 0f;
        slot.Cover.fillAmount = fill;
        slot.Time.text = overlay ?? (cooling ? Mathf.CeilToInt(left).ToString() : string.Empty);
        slot.Time.fontSize = overlay != null ? 20f : 30f;
    }

    private static IGameCharacter LocalCharacter()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (CharacterRegistry.IsAlive(c) && c.View != null && c.View.IsMine && !(c is HideOrSeekPlayer && RoomState.IsBroken(c.View.Owner))) return c;
        return null;
    }
}
