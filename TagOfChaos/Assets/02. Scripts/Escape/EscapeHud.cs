using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 탈출 모드 화면 표시(EscapePlan.md §1.9, §1.11). 프리팹 없이 코드로 만든다(맵 씬마다 EscapeManager가 하나 만든다).
// - 오른쪽 위: 건물 4곳 상태(Request1009Plan.md §25.5 P) — 쿠키: 재료 그림·이름·게이지, 스파이: "?"·가동 여부(+ 로켓 수리), 괴물: 가동 여부.
//   괴물은 소음폭탄 남은 칸도 본다.
// - 아래 가운데: 인벤토리 4칸(고른 칸 강조, 도구 남은 횟수).
// - 가운데 위: 알림(떴다가 서서히 사라짐) — 스파이 잡힘, 로켓에 끼움 등.
// - 스파이 본인: 시작 때 "당신은 스파이" 알림과 작은 표시.
// - 타임어택 타이머(빨강). 문구는 모두 EscapeTextsSO에서 읽는다(코드에 한글 금지).
public class EscapeHud : MonoBehaviour
{
    private const int MaxRequirementRows = 7;
    private static EscapeHud instance;
    private readonly List<StolenNotice> pendingStolen = new List<StolenNotice>();

    private EscapeManager manager;
    private EscapeTextsSO texts;
    private readonly List<RequirementRow> rows = new List<RequirementRow>();
    private readonly SlotView[] slots = new SlotView[PlayerInventory.SlotCount];
    private CanvasGroup toastGroup;
    private Canvas hudCanvas;
    private RectTransform hotbar;
    private CanvasGroup hotbarGroup;
    private const float HotbarY = 150f;
    private TMP_Text toastText;
    private Coroutine toastRoutine;
    private GameObject spyBadge;
    private TMP_Text timeAttackText;
    private TMP_Text bombText;
    private float nextRequirementRefresh;
    private const float RequirementRefreshSeconds = 0.25f; // 수리 게이지 %
    private PlayerInventory boundInventory;
    private bool spyNoticeShown;
    private int lastTimeAttackSeconds = -1;
    private static Sprite whiteSprite;

    private struct RequirementRow { public GameObject Root; public Image Icon; public TMP_Text Label; }
    private struct SlotView { public Image Frame; public Image Icon; public TMP_Text Name; public TMP_Text Charges; }

    public static void Create(EscapeManager owner)
    {
        if (instance != null) Destroy(instance.gameObject);
        var go = new GameObject(nameof(EscapeHud), typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        instance = go.AddComponent<EscapeHud>();
        instance.hudCanvas = canvas;
        instance.manager = owner;
        instance.texts = EscapeTextsSO.Current;
        instance.Build();
        owner.StateChanged += instance.RefreshRequirements;
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= RefreshRequirements;
        if (instance == this) instance = null;
    }

    // ---------------- static API ----------------

    // alert = 모두가 주목할 알림(스파이 잡힘·마녀) — 소리가 다르다(SoundPlan.md S2).
    public static void Toast(string message, bool alert = false)
    {
        if (instance == null || string.IsNullOrEmpty(message)) return;
        instance.ShowToast(message, 2.5f);
        UiSoundCues.Toast(alert);
    }

    // 스파이가 장치에서 뺀 재료 알림(Request1003bPlan.md §2): 방장이 정한 시각이 되면 띄운다. 괴물 화면에는 띄우지 않는다.
    public static void ScheduleStolen(string itemId, int itemIndex, double showAt)
    {
        if (instance == null || RoomState.IsLocalMonster()) return;
        instance.pendingStolen.Add(new StolenNotice { ItemId = itemId, ItemIndex = itemIndex, ShowAt = showAt });
    }

    // 로켓에 넣은 재료가 다른 상자(또는 장치 옆 바닥)에 다시 생김(Request1009Plan.md §22.6 M4) — 훔침 알림과 같은 규칙으로 기다렸다 띄운다.
    public static void ScheduleRespawned(string itemId, int itemIndex, double showAt, bool onGround)
    {
        if (instance == null || RoomState.IsLocalMonster()) return;
        instance.pendingStolen.Add(new StolenNotice { ItemId = itemId, ItemIndex = itemIndex, ShowAt = showAt, Respawned = true, OnGround = onGround });
    }

    private void TickStolenNotices()
    {
        for (int i = pendingStolen.Count - 1; i >= 0; i--)
        {
            StolenNotice n = pendingStolen[i];
            EscapeState state = manager.State;
            StolenNotice.Decision d = n.Decide(PhotonNetwork.Time, state != null && n.ItemIndex >= 0 && n.ItemIndex < state.Items.Count ? state.Items[n.ItemIndex].Loc : ItemLocation.Held);
            if (d == StolenNotice.Decision.Wait) continue;
            pendingStolen.RemoveAt(i);
            if (d == StolenNotice.Decision.Cancel) continue;
            ItemSO item = manager.Catalog.Find(n.ItemId);
            string name = item != null ? item.DisplayName : n.ItemId;
            Toast(string.Format(n.Template(texts, KoreanText.HasFinalConsonant(name)), name), alert: true);
        }
    }

    public static void Notice(EscapeNoticeKind kind, string itemId)
    {
        if (instance == null) return;
        EscapeTextsSO t = instance.texts;
        switch (kind)
        {
            // 같은 순간에 오는 "잡혔습니다" 알림이 먼저 보이도록 스파이 알림은 조금 뒤에 띄운다.
            case EscapeNoticeKind.SpyCaught: instance.StartCoroutine(instance.ToastLater(t.spyCaught, SpyCaughtToastDelay)); break;
            case EscapeNoticeKind.CookieCaught:
                Toast(CookieCaughtText(t, itemId), alert: true);
                break;
            case EscapeNoticeKind.DeviceComplete: Toast(t.deviceComplete); break;
            case EscapeNoticeKind.BuildingRepaired: Toast(t.buildingRepaired); break;
            case EscapeNoticeKind.ExitsOpened: Toast(t.exitsOpened, alert: true); break;
            case EscapeNoticeKind.RainbowAppeared: Toast(t.rainbowAppeared, alert: true); break;
            case EscapeNoticeKind.RainbowBroken: Toast(t.rainbowBroken, alert: true); break;
            case EscapeNoticeKind.AllDecoysGone: instance.StartCoroutine(instance.ToastLater(t.allDecoysGone, DecoysGoneToastDelay)); break;
            case EscapeNoticeKind.MaterialRestored:
                ItemSO restored = instance.manager.Catalog.Find(itemId);
                string restoredName = restored == null ? itemId : instance.IsUnknownToLocal(restored) ? t.unknownMaterial : restored.DisplayName;
                Toast(string.Format(t.materialRestored, restoredName));
                break;
            case EscapeNoticeKind.NoiseBomb:
                Toast(t.noiseBombAlarm, alert: true);
                if (RoomState.IsLocalMonster() && int.TryParse(itemId, out int building)) AlarmBeacon.Show(instance.manager.BuildingRepairPoint(building));
                break;
        }
    }

    // ---------------- build ----------------

    private void Build()
    {
        // 오른쪽 위 필요한 재료
        var reqRoot = NewRect("Requirements", transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -120f), new Vector2(300f, 40f * MaxRequirementRows));
        reqRoot.pivot = new Vector2(1f, 1f);
        for (int i = 0; i < MaxRequirementRows; i++)
        {
            var row = NewRect("Row" + i, reqRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -i * 40f), new Vector2(0f, 36f));
            row.pivot = new Vector2(0.5f, 1f);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = WhiteSprite(); bg.color = new Color(0f, 0f, 0f, 0.45f); bg.raycastTarget = false;
            var icon = NewImage("Icon", row, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(28f, 28f));
            var label = NewText("Label", row, 22f, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 0f); label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(40f, 0f); label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            rows.Add(new RequirementRow { Root = row.gameObject, Icon = icon, Label = label });
            row.gameObject.SetActive(false);
        }

        // 아래 가운데 인벤토리 4칸
        var bar = NewRect("Hotbar", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, HotbarY), new Vector2(4 * 96f, 90f));
        hotbar = bar;
        hotbarGroup = bar.gameObject.AddComponent<CanvasGroup>();
        hotbarGroup.blocksRaycasts = false;
        for (int i = 0; i < slots.Length; i++)
        {
            var cell = NewRect("Slot" + (i + 1), bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f + i * 96f, 0f), new Vector2(86f, 86f));
            var frame = cell.gameObject.AddComponent<Image>();
            frame.sprite = WhiteSprite(); frame.raycastTarget = false;
            var icon = NewImage("Icon", cell, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(50f, 50f));
            var name = NewText("Name", cell, 15f, TextAlignmentOptions.Bottom);
            name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = new Vector2(2f, 2f); name.rectTransform.offsetMax = new Vector2(-2f, -2f);
            var number = NewText("Key", cell, 16f, TextAlignmentOptions.TopLeft);
            number.rectTransform.anchorMin = Vector2.zero; number.rectTransform.anchorMax = Vector2.one;
            number.rectTransform.offsetMin = new Vector2(6f, 2f); number.rectTransform.offsetMax = new Vector2(-4f, -4f);
            number.text = (i + 1).ToString();
            var charges = NewText("Charges", cell, 18f, TextAlignmentOptions.TopRight);
            charges.rectTransform.anchorMin = Vector2.zero; charges.rectTransform.anchorMax = Vector2.one;
            charges.rectTransform.offsetMin = new Vector2(2f, 2f); charges.rectTransform.offsetMax = new Vector2(-6f, -4f);
            slots[i] = new SlotView { Frame = frame, Icon = icon, Name = name, Charges = charges };
        }

        // 가운데 위 알림
        var toast = NewRect("Toast", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1100f, 60f));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false; toastGroup.interactable = false;
        var toastBg = toast.gameObject.AddComponent<Image>();
        toastBg.sprite = WhiteSprite(); toastBg.color = new Color(0f, 0f, 0f, 0.6f); toastBg.raycastTarget = false;
        toastText = NewText("Text", toast, 30f, TextAlignmentOptions.Center);
        toastText.rectTransform.anchorMin = Vector2.zero; toastText.rectTransform.anchorMax = Vector2.one;
        toastText.rectTransform.offsetMin = Vector2.zero; toastText.rectTransform.offsetMax = Vector2.zero;

        // 스파이 표시(왼쪽 위, 본인에게만)
        var badge = NewRect("SpyBadge", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -60f), new Vector2(140f, 44f));
        var badgeBg = badge.gameObject.AddComponent<Image>();
        badgeBg.sprite = WhiteSprite(); badgeBg.color = new Color(0.55f, 0f, 0.1f, 0.85f); badgeBg.raycastTarget = false;
        var badgeText = NewText("Text", badge, 26f, TextAlignmentOptions.Center);
        badgeText.rectTransform.anchorMin = Vector2.zero; badgeText.rectTransform.anchorMax = Vector2.one;
        badgeText.rectTransform.offsetMin = Vector2.zero; badgeText.rectTransform.offsetMax = Vector2.zero;
        badgeText.text = texts.spyBadge;
        spyBadge = badge.gameObject;
        spyBadge.SetActive(false);

        // 괴물 소음폭탄 남은 칸(아래 가운데, 괴물에게만)
        bombText = NewText("Bombs", transform, 28f, TextAlignmentOptions.Center);
        bombText.rectTransform.anchorMin = new Vector2(0.5f, 0f); bombText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        bombText.rectTransform.anchoredPosition = new Vector2(0f, 60f); bombText.rectTransform.sizeDelta = new Vector2(400f, 44f);
        bombText.color = new Color(1f, 0.55f, 0.5f);
        bombText.gameObject.SetActive(false);

        // 오른쪽 아래 스킬 칸(§26 H9)
        gameObject.AddComponent<SkillHud>().Init(manager, texts);

        // 마녀 타이머(가운데 위, 빨강)

        timeAttackText = NewText("TimeAttack", transform, 44f, TextAlignmentOptions.Center);
        timeAttackText.rectTransform.anchorMin = new Vector2(0.5f, 1f); timeAttackText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        timeAttackText.rectTransform.anchoredPosition = new Vector2(0f, -110f); timeAttackText.rectTransform.sizeDelta = new Vector2(400f, 60f);
        timeAttackText.color = new Color(1f, 0.25f, 0.25f);
        timeAttackText.gameObject.SetActive(false);
    }

    // ---------------- update ----------------

    private void Update()
    {
        // 결과 화면이 뜨면 HUD(가방·필요 재료·알림)는 결과 위를 가리지 않도록 숨긴다.
        bool showHud = GamePhaseState.Current != GamePhase.Result;
        if (hudCanvas.enabled != showHud) hudCanvas.enabled = showHud;
        if (!showHud) return;
        TickStolenNotices();
        if (!Mathf.Approximately(hotbar.anchoredPosition.y, HotbarY)) hotbar.anchoredPosition = new Vector2(0f, HotbarY);
        if (!Mathf.Approximately(hotbarGroup.alpha, 1f)) hotbarGroup.alpha = 1f;
        if (Time.unscaledTime >= nextRequirementRefresh) { nextRequirementRefresh = Time.unscaledTime + RequirementRefreshSeconds; RefreshRequirements(); }
        RefreshBombs();

        if (boundInventory != PlayerInventory.Local)
        {
            if (boundInventory != null) boundInventory.Changed -= RefreshHotbar;
            boundInventory = PlayerInventory.Local;
            if (boundInventory != null) boundInventory.Changed += RefreshHotbar;
            RefreshHotbar();
            RefreshRequirements();
        }

        bool spy = RoomState.IsLocalSpy();
        if (spyBadge.activeSelf != spy) spyBadge.SetActive(spy);
        if (spy && !spyNoticeShown)
        {
            spyNoticeShown = true;
            ShowToast(texts.youAreSpy, 4f);
            UiSoundCues.Toast(alert: false);
        }

        // 마녀 타이머는 등장 연출이 끝난 뒤부터 보인다 — 그 순간 정확히 타이머 값부터 센다(§25 결정 1)
        bool timeAttack = RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out double end) && GamePhaseState.Current == GamePhase.TimeAttack
                          && WitchTimeline.TryGetAppearAt(out double appearAt) && PhotonNetwork.Time >= WitchTimeline.WalkStart(WitchMode.TimeAttack, appearAt);
        if (timeAttackText.gameObject.activeSelf != timeAttack) timeAttackText.gameObject.SetActive(timeAttack);
        if (timeAttack)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(end - PhotonNetwork.Time)));
            if (seconds != lastTimeAttackSeconds)
            {
                if (lastTimeAttackSeconds >= 0) UiSoundCues.CountdownTick(seconds); // 타임어택 마지막 10초
                lastTimeAttackSeconds = seconds;
                timeAttackText.text = string.Format(texts.timeAttackFormat, seconds / 60, seconds % 60);
            }
        }
    }

    private int lastBombs = -1;

    // 괴물: 소음폭탄 남은 칸(Request1009Plan.md §25.5 K).
    private void RefreshBombs()
    {
        bool monster = RoomState.IsLocalMonster();
        if (bombText.gameObject.activeSelf != monster) bombText.gameObject.SetActive(monster);
        if (!monster) return;
        int left = NoiseBombPresenter.LocalChargesLeft(manager);
        if (left == lastBombs) return;
        lastBombs = left;
        bombText.text = string.Format(texts.noiseBombCharges, left, EscapeAuthority.BombMaxCharges);
    }

    private void RefreshHotbar()
    {
        PlayerInventory inv = boundInventory;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemSO item = inv != null ? inv.ItemAt(i) : null;
            bool selected = inv != null && inv.Selected == i;
            slots[i].Frame.color = selected ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0f, 0f, 0f, 0.5f);
            slots[i].Icon.enabled = item != null;
            bool unknown = item != null && IsUnknownToLocal(item);
            if (item != null)
            {
                slots[i].Icon.sprite = item.Icon != null && !unknown ? item.Icon : WhiteSprite();
                slots[i].Icon.color = unknown ? new Color(0.5f, 0.5f, 0.55f) : item.Icon != null ? Color.white : item.Tint;
            }
            slots[i].Name.text = item == null ? string.Empty : unknown ? texts.unknownMaterial : item.DisplayName;
            int index = inv != null && item != null ? inv.HeldItemIndexAt(i) : -1;
            slots[i].Charges.text = item != null && item.IsTool && index >= 0 && manager.State != null ? manager.State.Items[index].Charges.ToString() : string.Empty;
        }
    }

    // 스파이는 건물 재료가 무엇인지 모른다(§25 요청) — 수리키트·도구·로켓에 필요한 재료는 안다(§26 H4).
    private bool IsUnknownToLocal(ItemSO item) =>
        RoomState.IsLocalSpy() && item.Category == ItemCategory.Material && (manager.Catalog.RepairKit == null || item != manager.Catalog.RepairKit)
        && !RocketAccepts(manager.State, item.ItemId);

    private static bool RocketAccepts(EscapeState s, string itemId)
    {
        if (s == null) return false;
        foreach (EscapeState.Slot slot in s.RocketSlots) if (slot.Allows(itemId)) return true;
        return false;
    }

    // 건물 4곳(활성만): 쿠키 = 재료 그림·이름·게이지 %, 괴물 = "?"·가동 여부.
    // 스파이는 건물 대신 "스파이 로켓" 제목 + 칸마다 재료 그림·이름·%(✓) + 넣은 수리키트 수(§26 H4).
    private void RefreshRequirements()
    {
        RefreshHotbar();
        EscapeState s = manager.State;
        var entries = new List<(Sprite icon, Color color, string label, bool done)>();
        if (s != null && RoomState.IsLocalSpy()) AddRocketEntries(s, entries);
        else if (s != null)
        {
            bool seesMaterials = !RoomState.IsLocalSpy() && !RoomState.IsLocalMonster();
            double now = PhotonNetwork.Time;
            foreach (EscapeState.Building b in s.Buildings)
            {
                if (!b.Active) continue;
                ItemSO item = manager.Catalog.Find(b.MaterialId);
                bool done = b.Repaired;
                if (seesMaterials && item != null)
                {
                    string state = done ? "OK" : $"{Mathf.RoundToInt(b.ProgressAtTime(now) * 100f)}%";
                    entries.Add((item.Icon != null ? item.Icon : WhiteSprite(), item.Icon != null ? Color.white : item.Tint, $"{item.DisplayName}  {state}", done));
                }
                else entries.Add((WhiteSprite(), new Color(0.5f, 0.5f, 0.55f), $"{texts.unknownMaterial}  {(done ? "OK" : "...")}", done));
            }
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool show = i < entries.Count;
            if (rows[i].Root.activeSelf != show) rows[i].Root.SetActive(show);
            if (!show) continue;
            var e = entries[i];
            rows[i].Icon.sprite = e.icon;
            rows[i].Icon.color = e.color;
            rows[i].Label.text = e.label;
            rows[i].Label.color = e.done ? new Color(0.5f, 1f, 0.5f) : Color.white;
        }
    }

    private void AddRocketEntries(EscapeState s, List<(Sprite icon, Color color, string label, bool done)> entries)
    {
        if (s.RocketSlots.Count == 0) return;
        Color rocketColor = new Color(1f, 0.55f, 0.2f);
        entries.Add((WhiteSprite(), rocketColor, texts.rocketTitle, s.RocketComplete));
        foreach (EscapeState.Slot slot in s.RocketSlots)
        {
            if (entries.Count >= MaxRequirementRows - 1) break;
            ItemSO item = slot.Accepts != null && slot.Accepts.Length > 0 ? manager.Catalog.Find(slot.Accepts[0]) : null;
            bool single = slot.Accepts != null && slot.Accepts.Length == 1;
            string name = item != null ? item.DisplayName : slot.Label;
            string state = slot.Filled ? "✓" : slot.KitHalf ? texts.rocketKitHalf : "0%";
            Sprite icon = single && item != null && item.Icon != null ? item.Icon : WhiteSprite();
            Color color = icon != WhiteSprite() ? Color.white : single && item != null ? item.Tint : new Color(0.85f, 0.85f, 0.9f);
            entries.Add((icon, color, $"{name}  {state}", slot.Filled));
        }
        int kitsUsed = 0;
        if (manager.Catalog.RepairKit != null)
            foreach (EscapeState.Item it in s.Items) if (it.Id == manager.Catalog.RepairKit.ItemId && it.Loc == ItemLocation.Gone) kitsUsed++;
        ItemSO kit = manager.Catalog.RepairKit;
        entries.Add((kit != null && kit.Icon != null ? kit.Icon : WhiteSprite(), kit != null && kit.Icon != null ? Color.white : rocketColor,
            string.Format(texts.rocketKits, kitsUsed), false));
    }

    // 마녀 타이머 알림과 같은 순간에 오므로 그 알림이 지나간 뒤 띄운다.
    private const float DecoysGoneToastDelay = 4f;
    private const float SpyCaughtToastDelay = 2.5f;

    // "'닉네임'이(가) 잡혔습니다!" — 닉네임 끝 글자로 조사를 고른다.
    public static string CookieCaughtText(EscapeTextsSO t, string actorText)
    {
        string name = actorText;
        if (int.TryParse(actorText, out int actor) && PhotonNetwork.CurrentRoom != null)
        {
            Photon.Realtime.Player p = PhotonNetwork.CurrentRoom.GetPlayer(actor);
            if (p != null) name = string.IsNullOrEmpty(p.NickName) ? "#" + actor : p.NickName;
        }
        string format = !KoreanText.EndsWithHangul(name) ? t.cookieCaughtOther
            : KoreanText.HasFinalConsonant(name) ? t.cookieCaughtWithFinal : t.cookieCaughtNoFinal;
        return string.Format(format, name);
    }

    private IEnumerator ToastLater(string message, float delay)
    {
        yield return new WaitForSeconds(delay);
        Toast(message, alert: true);
    }

    private void ShowToast(string message, float hold)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastText.text = message;
        toastRoutine = StartCoroutine(FadeToast(hold, 1.5f));
    }

    // 떴다가 서서히 사라진다(D34).
    private IEnumerator FadeToast(float hold, float fade)
    {
        toastGroup.alpha = 1f;
        yield return new WaitForSeconds(hold);
        for (float t = 0f; t < fade; t += Time.deltaTime)
        {
            toastGroup.alpha = 1f - t / fade;
            yield return null;
        }
        toastGroup.alpha = 0f;
        toastRoutine = null;
    }

    // ---------------- ui helpers ----------------

    internal static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    internal static Image NewImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rt = NewRect(name, parent, anchor, anchor, pos, size);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.raycastTarget = false;
        return image;
    }

    internal static TMP_Text NewText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    internal static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return whiteSprite;
    }
}
