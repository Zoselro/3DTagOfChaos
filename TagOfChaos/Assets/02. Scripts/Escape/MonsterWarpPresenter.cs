using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// §90 T2 술래 순간이동 — 모든 화면. EscapeManager.Start가 붙인다.
// 본인(술래): T = 화면 가운데 작은 지도(재료 건물 번호·수리 여부), 1~4 또는 마우스 클릭으로 고름, T/Esc = 닫기 → EscapeOp.MonsterWarp.
// 모든 화면(State.Warps 시각): At에 출발 자리 연기·술래 몸 숨김 → 벽장 문 덜컹 2초(ClosetRattle, 방 안에서 들림) → 0.5초에 열리고 술래가 보임 → 3초 뒤 닫힘.
// 본인: At에 벽장 안(Stand)으로 옮기고 문이 열릴 때까지 움직이지 못한다.
public class MonsterWarpPresenter : MonoBehaviour
{
    private const float MapSize = 460f;
    private const float IconSize = 64f;

    public static float ReadyAt { get; private set; }
    public static bool MapOpen { get; private set; }

    private EscapeManager manager;
    private readonly HashSet<string> started = new HashSet<string>();   // "actor@At" — 출발 연출을 한 번만
    private readonly HashSet<string> rattled = new HashSet<string>();
    private readonly Dictionary<int, Renderer[]> hidden = new Dictionary<int, Renderer[]>();
    private readonly HashSet<MonsterController> holding = new HashSet<MonsterController>();   // §92-5 이번 프레임 뚜껑 아래에 붙잡은 본인 술래
    private RectTransform map;
    private readonly RectTransform[] icons = new RectTransform[EscapeState.BuildingCount];
    private readonly TMP_Text[] iconLabels = new TMP_Text[EscapeState.BuildingCount];
    private readonly Image[] iconBg = new Image[EscapeState.BuildingCount];
    private TMP_Text title;
    private Image meDot;

    public void Init(EscapeManager owner) { manager = owner; }

    private void OnDestroy()
    {
        SetMap(false);
        PlayerInput.WarpMapOpen = false;
        if (map != null) Destroy(map.gameObject);
    }

    private void Update()
    {
        UpdateInput();
        UpdateWarps();
    }

    // ---------------- 본인 입력·지도 ----------------

    private static MonsterController LocalMonster()
    {
        if (!RoomState.IsLocalMonster()) return null;
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is MonsterController m && c.View != null && c.View.IsMine) return m;
        return null;
    }

    private bool CanUse(MonsterController m) =>
        m != null && EscapeManager.ActionsAllowed && MonsterElevator.PhaseAllows(GamePhaseState.Current) && m.CanInteract && manager.State != null
        && PhotonNetwork.LocalPlayer != null && manager.State.WarpIndex(PhotonNetwork.LocalPlayer.ActorNumber) < 0;

    private void UpdateInput()
    {
        MonsterController m = LocalMonster();
        if (MapOpen && (!CanUse(m) || Input.GetKeyDown(KeyCode.Escape))) { SetMap(false); return; }
        if (PlayerInput.MonsterWarpPressed)
        {
            if (MapOpen) { SetMap(false); return; }
            if (m == null) return;
            if (!CanUse(m) || Time.time < ReadyAt) { UiSoundCues.Error(); if (Time.time < ReadyAt) EscapeHud.Toast(string.Format(EscapeTextsSO.Current.warpCooldown, Mathf.CeilToInt(ReadyAt - Time.time))); return; }
            SetMap(true);
        }
        if (!MapOpen) return;
        RefreshMap(m);
        int pick = PlayerInput.WarpPickPressed();
        if (pick < 0 && Input.GetMouseButtonDown(0)) pick = IconUnderMouse();
        if (pick >= 0) Pick(pick);
    }

    // 고르기(시험에서도 부른다). 보냈으면 true.
    public bool Pick(int building)
    {
        if (!MonsterWarpRules.CanPick(manager.State, building)) { UiSoundCues.Error(); return false; }
        ReadyAt = Time.time + MonsterWarpRules.Cooldown;
        manager.Request(EscapeOp.MonsterWarp, building, 0, Vector3.zero);
        SetMap(false);
        GameAudio.Play(SoundId.UiConfirm);
        return true;
    }

    private void SetMap(bool open)
    {
        if (open && map == null) BuildMap();
        MapOpen = open;
        PlayerInput.WarpMapOpen = open;
        if (map != null) map.gameObject.SetActive(open);
    }

    private void BuildMap()
    {
        var canvasGo = new GameObject("WarpMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
        var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
        map = EscapeHud.NewRect("WarpMap", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapSize, MapSize + 60f));
        Image bg = map.gameObject.AddComponent<Image>(); bg.sprite = EscapeHud.WhiteSprite(); bg.color = new Color(0.12f, 0.08f, 0.16f, 0.88f); bg.raycastTarget = false;
        title = EscapeHud.NewText("Title", map, 22f, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f); title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(8f, -50f); title.rectTransform.offsetMax = new Vector2(-8f, -8f);
        title.text = EscapeTextsSO.Current.warpTitle;
        meDot = EscapeHud.NewImage("Me", map, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
        meDot.color = new Color(1f, 0.25f, 0.25f, 1f);
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = EscapeHud.NewRect("B" + (i + 1), map, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(IconSize, IconSize));
            iconBg[i] = icons[i].gameObject.AddComponent<Image>(); iconBg[i].sprite = EscapeHud.WhiteSprite(); iconBg[i].raycastTarget = false;
            iconLabels[i] = EscapeHud.NewText("L", icons[i], 18f, TextAlignmentOptions.Center);
            iconLabels[i].rectTransform.anchorMin = Vector2.zero; iconLabels[i].rectTransform.anchorMax = Vector2.one;
            iconLabels[i].rectTransform.offsetMin = new Vector2(-40f, -26f); iconLabels[i].rectTransform.offsetMax = new Vector2(40f, 0f);
        }
    }

    // 맵 위 위치: 재료 건물 4채와 내 위치를 담는 사각형을 지도에 맞춘다(북 = 위).
    private void RefreshMap(MonsterController me)
    {
        EscapeState s = manager.State;
        if (map == null || s == null) return;
        var pts = new List<Vector3>();
        for (int i = 0; i < EscapeState.BuildingCount; i++) pts.Add(manager.BuildingRepairPoint(i));
        if (me != null) pts.Add(me.transform.position);
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
        float span = Mathf.Max(20f, Mathf.Max(maxX - minX, maxZ - minZ));
        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        float half = (MapSize - IconSize * 1.6f) * 0.5f;
        System.Func<Vector3, Vector2> toMap = p => new Vector2((p.x - center.x) / span * 2f * half, (p.z - center.y) / span * 2f * half - 20f);
        for (int i = 0; i < icons.Length; i++)
        {
            bool active = i < s.Buildings.Count && s.Buildings[i].Active;
            icons[i].gameObject.SetActive(active);
            if (!active) continue;
            icons[i].anchoredPosition = toMap(pts[i]);
            bool repaired = s.Buildings[i].Repaired;
            iconBg[i].color = repaired ? new Color(0.35f, 0.75f, 0.45f, 0.95f) : new Color(0.95f, 0.7f, 0.25f, 0.95f);
            iconLabels[i].text = (i + 1) + (repaired ? "\n<size=13>" + EscapeTextsSO.Current.warpRepaired + "</size>" : string.Empty);
        }
        if (me != null) meDot.rectTransform.anchoredPosition = toMap(me.transform.position);
    }

    private int IconUnderMouse()
    {
        for (int i = 0; i < icons.Length; i++)
            if (icons[i] != null && icons[i].gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(icons[i], Input.mousePosition, null)) return i;
        return -1;
    }

    // ---------------- 모든 화면: 사라짐·덜컹·열림 ----------------

    private void UpdateWarps()
    {
        EscapeState s = manager != null ? manager.State : null;
        double now = PhotonNetwork.Time;
        var keep = new HashSet<int>();
        holding.Clear();
        if (s != null)
            foreach (EscapeState.Warp w in s.Warps)
            {
                MonsterCloset closet = MonsterCloset.For(manager.BuildingTransform(w.Building));
                MonsterWarpRules.Phase phase = MonsterWarpRules.PhaseAt(w, now);
                if (phase == MonsterWarpRules.Phase.Before) continue;
                string key = w.Actor + "@" + w.At.ToString("F2");
                IGameCharacter c = CharacterRegistry.FindByOwner(w.Actor);
                if (started.Add(key))
                {
                    if (c != null && phase < MonsterWarpRules.Phase.Opening) DepartFx(c.gameObject.transform.position);
                    if (c is MonsterController mine && c.View != null && c.View.IsMine) Arrive(mine, w, closet);
                }
                // bool inside = phase == MonsterWarpRules.Phase.Rattle;
                bool hatch = closet != null && closet.Type == MonsterCloset.Kind.Hatch;
                bool inside = MonsterWarpRules.BodyHidden(w, now, hatch);   // §92-5 뚜껑은 올라오기 시작할 때까지 숨김
                if (c != null) { SetHidden(w.Actor, c, inside); if (inside) keep.Add(w.Actor); }
                if (phase == MonsterWarpRules.Phase.Rattle && rattled.Add(key) && closet != null) GameAudio.PlayAt(SoundId.ClosetRattle, closet.SoundPoint);
                if (closet != null)
                {
                    float rattle = phase == MonsterWarpRules.Phase.Rattle ? 1f : 0f;
                    closet.Pose(MonsterWarpRules.DoorOpen(w, now), rattle, (float)now);
                }
                // if (c is MonsterController m2 && c.View != null && c.View.IsMine) m2.WarpLockedUntil = MonsterWarpRules.DoneAt(w);
                if (c is MonsterController m2 && c.View != null && c.View.IsMine)
                {
                    m2.WarpLockedUntil = MonsterWarpRules.LockUntil(w, hatch);
                    if (hatch && closet != null) Rise(m2, w, closet, now, holding);
                }
            }
        // §92-5 뚜껑 아래에서 붙잡아 둔 본인 술래: 순간이동이 사라졌으면(방장 정리 등) 놓아 준다
        MonsterController local = LocalMonster();
        if (local != null && local.WarpHeld && !holding.Contains(local)) local.WarpRelease();
        // 다 끝난 술래 몸 다시 보이기
        var release = new List<int>();
        foreach (int actor in hidden.Keys) if (!keep.Contains(actor)) release.Add(actor);
        foreach (int actor in release) { foreach (Renderer r in hidden[actor]) if (r != null) r.enabled = true; hidden.Remove(actor); }
    }

    private void Arrive(MonsterController me, EscapeState.Warp w, MonsterCloset closet)
    {
        Vector3 to = closet != null ? closet.StandPoint : manager.BuildingRepairPoint(w.Building);
        // me.TeleportTo(to + Vector3.up * 0.1f);
        if (closet != null && closet.Type == MonsterCloset.Kind.Hatch) me.WarpHold(to + Vector3.up * (0.1f - MonsterWarpRules.HatchDepth));   // §92-5 뚜껑 아래
        else me.TeleportTo(to + Vector3.up * 0.1f);
        if (closet != null) me.transform.rotation = closet.Facing;
        MonsterViewSwitcher.ShowActionLocal();
    }

    // §92-5 바닥 뚜껑: 아래에서 기다리다가 뚜껑이 열리면 솟아오른다(본인 화면에서 몸을 옮기면 다른 화면은 위치 동기화로 같이 본다).
    private static void Rise(MonsterController me, EscapeState.Warp w, MonsterCloset closet, double now, HashSet<MonsterController> held)
    {
        if (now >= MonsterWarpRules.RiseEndAt(w))
        {
            if (me.WarpHeld) { me.WarpHold(closet.StandPoint + Vector3.up * 0.1f); me.WarpRelease(); }
            return;
        }
        float k = MonsterWarpRules.Rise01(w, now);
        me.WarpHold(closet.StandPoint + Vector3.up * (0.1f - MonsterWarpRules.HatchDepth * (1f - k)));
        held.Add(me);
    }

    private void SetHidden(int actor, IGameCharacter c, bool hide)
    {
        if (hide && !hidden.ContainsKey(actor))
        {
            Renderer[] rs = c.gameObject.GetComponentsInChildren<Renderer>();
            var on = new List<Renderer>();
            foreach (Renderer r in rs) if (r.enabled) { r.enabled = false; on.Add(r); }
            hidden[actor] = on.ToArray();
        }
    }

    private static void DepartFx(Vector3 at)
    {
        var go = new GameObject("WarpSmoke");
        go.transform.position = at + Vector3.up * 1.5f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = false; m.duration = 0.3f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f); m.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.15f, 0.3f, 0.8f), new Color(0.45f, 0.3f, 0.5f, 0.6f));
        m.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1.5f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        ps.Play();
        GameAudio.PlayAt(SoundId.PortalOpen, at);
    }
}
