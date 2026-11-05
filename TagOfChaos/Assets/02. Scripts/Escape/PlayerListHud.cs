using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 참가자 목록(Request1009Plan.md §59 PL): 게임 맵에서 목록 키(기본 Tab, KeyAction.PlayerList)를 누르고 있는 동안만 보이고 떼면 숨는다.
// 한 줄 = 닉네임 + 역할(괴물/쿠키) + 나·방장 표시. 상태(잡힘·탈출)는 보여 주지 않는다(사용자 결정).
// 스파이는 쿠키로 보인다 — 본인 화면과 판이 끝나 공개된 뒤(RevealedSpies)에만 스파이 표시.
// 보이는 동안에만, 사람이 들어오고 나가거나 방·플레이어 속성이 바뀔 때 다시 그린다(줄은 미리 만든 것을 재사용). 문구는 EscapeTextsSO.
public class PlayerListHud : MonoBehaviourPunCallbacks
{
    private const int MaxRows = 12;
    private const float Width = 620f, RowHeight = 46f, TitleHeight = 64f;
    private static readonly Color MonsterColor = new Color(1f, 0.42f, 0.42f);
    private static readonly Color CookieColor = new Color(1f, 0.86f, 0.6f);
    private static readonly Color SpyColor = new Color(0.75f, 0.55f, 1f);

    public struct Row
    {
        public int Actor;
        public string Name;
        public bool Monster;
        public bool Spy;   // 이 화면에 스파이로 보여 줄지(ShowSpy로 이미 걸렀다)
        public bool Local;
        public bool Host;
    }

    private struct RowView { public GameObject Root; public TMP_Text Name; public TMP_Text Role; public Image Back; }

    private static PlayerListHud instance;
    private CanvasGroup group;
    private RectTransform panel;
    private TMP_Text title;
    private readonly RowView[] views = new RowView[MaxRows];
    private readonly List<Row> rows = new List<Row>(MaxRows);
    private bool visible;
    private bool dirty = true;

    public static bool IsVisible => instance != null && instance.visible;

    public static void Create()
    {
        if (instance != null) Destroy(instance.gameObject);
        var go = new GameObject(nameof(PlayerListHud), typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;   // 탈출 HUD(40) 위
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        instance = go.AddComponent<PlayerListHud>();
        instance.group = go.GetComponent<CanvasGroup>();
        instance.group.alpha = 0f;
        instance.group.blocksRaycasts = false;
        instance.group.interactable = false;
        instance.Build();
    }

    private void Build()
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        Image back = EscapeHud.NewImage("Panel", transform, mid, Vector2.zero, new Vector2(Width, TitleHeight + RowHeight * MaxRows + 20f));
        back.color = new Color(0.05f, 0.03f, 0.08f, 0.82f);
        panel = back.rectTransform;
        panel.pivot = new Vector2(0.5f, 1f);   // 위쪽 고정 — 인원수만큼 아래로 늘어난다
        panel.anchoredPosition = new Vector2(0f, (TitleHeight + RowHeight * MaxRows + 20f) * 0.5f);
        title = EscapeHud.NewText("Title", panel, 34f, TextAlignmentOptions.Center);
        Place(title.rectTransform, 0f, TitleHeight);
        for (int i = 0; i < MaxRows; i++)
        {
            Image rowBack = EscapeHud.NewImage("Row" + i, panel, mid, Vector2.zero, new Vector2(Width - 30f, RowHeight - 6f));
            rowBack.color = new Color(1f, 1f, 1f, i % 2 == 0 ? 0.06f : 0.02f);
            Place(rowBack.rectTransform, TitleHeight + i * RowHeight, RowHeight - 6f, Width - 30f);
            TMP_Text name = EscapeHud.NewText("Name", rowBack.transform, 26f, TextAlignmentOptions.MidlineLeft);
            Stretch(name.rectTransform, 18f, 200f);
            name.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text role = EscapeHud.NewText("Role", rowBack.transform, 24f, TextAlignmentOptions.MidlineRight);
            Stretch(role.rectTransform, Width - 30f - 190f, 18f);
            views[i] = new RowView { Root = rowBack.gameObject, Name = name, Role = role, Back = rowBack };
        }
    }

    // 판 위쪽에서 top만큼 내려온 줄(높이 height, 폭 width — 0이면 판 폭)
    private void Place(RectTransform rt, float top, float height, float width = 0f)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -(10f + top));
        rt.sizeDelta = new Vector2(width > 0f ? width : Width, height);
    }

    private static void Stretch(RectTransform rt, float left, float right)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, 0f);
        rt.offsetMax = new Vector2(-right, 0f);
    }

    private void Update()
    {
        bool show = PlayerInput.PlayerListHeld && !EscMenu.IsOpen && GamePhaseState.Current != GamePhase.Result && RoomState.IsInRoom();
        if (show != visible)
        {
            visible = show;
            group.alpha = show ? 1f : 0f;
            if (show) dirty = true;
        }
        if (visible && dirty) Refresh();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => dirty = true;
    public override void OnPlayerLeftRoom(Player otherPlayer) => dirty = true;
    public override void OnMasterClientSwitched(Player newMasterClient) => dirty = true;
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) => dirty = true;
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) => dirty = true;

    private void Refresh()
    {
        dirty = false;
        EscapeTextsSO t = EscapeTextsSO.Current;
        rows.Clear();
        int local = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        RoomState.TryGetIntArray(NetKeys.RevealedSpies, out int[] revealed);
        bool localSpy = RoomState.IsLocalSpy();
        foreach (Player p in RoomState.Players)
            rows.Add(new Row
            {
                Actor = p.ActorNumber,
                Name = string.IsNullOrEmpty(p.NickName) ? "#" + p.ActorNumber : p.NickName,
                Monster = RoomState.IsMonster(p.ActorNumber),
                Spy = RoomState.IsSpy(p.ActorNumber) && ShowSpy(p.ActorNumber, local, localSpy, revealed),
                Local = p.ActorNumber == local,
                Host = p.IsMasterClient,
            });
        Sort(rows);
        title.text = $"{t.playerListTitle}  {rows.Count}";
        panel.sizeDelta = new Vector2(Width, TitleHeight + RowHeight * Mathf.Clamp(rows.Count, 1, MaxRows) + 20f);
        for (int i = 0; i < MaxRows; i++)
        {
            bool on = i < rows.Count;
            if (views[i].Root.activeSelf != on) views[i].Root.SetActive(on);
            if (!on) continue;
            Row r = rows[i];
            views[i].Name.text = r.Local ? $"{r.Name} {t.playerListYou}" : r.Name;
            views[i].Name.fontStyle = r.Local ? FontStyles.Bold : FontStyles.Normal;
            string role = r.Monster ? t.playerListMonster : r.Spy ? t.spyBadge : t.playerListCookie;
            views[i].Role.text = r.Host ? $"{t.playerListHost}  {role}" : role;
            views[i].Role.color = r.Monster ? MonsterColor : r.Spy ? SpyColor : CookieColor;
        }
    }

    // ---------------- 순수 규칙(시험) ----------------

    // 스파이 표시: 본인이 스파이면 자기 줄에만, 판이 끝나 공개되면(RevealedSpies) 모두에게.
    public static bool ShowSpy(int actor, int localActor, bool localIsSpy, int[] revealed) =>
        (actor == localActor && localIsSpy) || (revealed != null && System.Array.IndexOf(revealed, actor) >= 0);

    // 괴물 먼저, 그다음 들어온 순서(액터 번호).
    public static void Sort(List<Row> list) =>
        list.Sort((a, b) => a.Monster != b.Monster ? (a.Monster ? -1 : 1) : a.Actor.CompareTo(b.Actor));

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
