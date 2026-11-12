using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 괴물 사이렌 함정(옛 소음폭탄 — Request1009Plan.md §25.5 K, §25.3.1 R7, §68 GP5). 스킬 키(Q)로 발밑에 놓고(한 번에 하나, 풀리거나 치운 뒤 20초),
// 근처 건물에서 누가 수리를 시작하면 방장이 건물을 빼빼로 철창에 가둔다(SirenTrapPresenter). 놓인 함정은 괴물에게만 보이고, 자기 함정은 E로 치울 수 있다.
// 수색 폭탄(§68 GP5, §70.3): 수색 키(R)를 누르고 있으면 궤적, 떼면 보는 곳으로 던진다 — 튕기고 구르다 첫 충돌 2초 뒤 터지면 둘레의 진짜 사람만 기침한다.
public class NoiseBombPresenter : MonoBehaviour
{
    private EscapeManager manager;
    private readonly Dictionary<int, NoiseBombView> views = new Dictionary<int, NoiseBombView>();
    private readonly List<int> stale = new List<int>();

    public void Init(EscapeManager owner)
    {
        manager = owner;
        manager.StateChanged += Sync;
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= Sync;
        if (placePreview != null) Destroy(placePreview.gameObject);
        if (markers != null) Destroy(markers.gameObject);
        searchPreview.Destroy();
    }

    // 지금 내 괴물의 남은 칸(HUD).
    public static int LocalChargesLeft(EscapeManager m)
    {
        if (m == null || m.State == null || PhotonNetwork.LocalPlayer == null) return 0;
        return m.State.BombChargesLeft(PhotonNetwork.LocalPlayer.ActorNumber, EscapeAuthority.BombMaxCharges, PhotonNetwork.Time, EscapeAuthority.BombCooldown);
    }

    // 내 수색 폭탄을 다시 쓸 수 있는 시각(Time.time, HUD — 방장도 같은 쿨타임으로 막는다).
    public static float SearchReadyAt { get; private set; }

    // 놓을 자리 미리보기(§69 F5): 남은 함정이 있고 재료 건물 가까이(둘레 + PreviewMargin) 오면 발 앞 자리에 고리 — 놓을 수 있으면 초록, 둘레 밖이면 빨강.
    private const float PlaceAhead = 1.2f;
    private const float PreviewMargin = 10f;
    private static readonly Color CanPlace = new Color(0.3f, 1f, 0.4f, 0.9f);
    private static readonly Color CannotPlace = new Color(1f, 0.2f, 0.2f, 0.9f);
    private BalloonAimPreview placePreview;
    private ScreenEdgeMarkers markers;

    private void Update()
    {
        UpdateSearch();
        IGameCharacter monster = LocalMonster();
        UpdatePlacePreview(monster);
        UpdateMarkers();
        if (monster == null || !PlayerInput.SkillPressed || !EscapeManager.ActionsAllowed) return;
        if (LocalChargesLeft(manager) <= 0) { UiSoundCues.Error(); return; }
        Vector3 spot = PlaceSpot(monster);
        if (!manager.IsNearMaterialBuilding(spot, EscapeAuthority.BombBuildingRadius))
        {
            UiSoundCues.Error();
            ScreenToast.Show(EscapeTextsSO.Current.bombNeedsBuilding);
            return;
        }
        manager.Request(EscapeOp.PlaceBomb, 0, 0, spot);
    }

    private static IGameCharacter LocalMonster()
    {
        if (!RoomState.IsLocalMonster()) return null;
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.Role == CharacterRole.Monster && c.View != null && c.View.IsMine && c.CanInteract) return c;
        return null;
    }

    private static Vector3 PlaceSpot(IGameCharacter monster)
    {
        Transform t = monster.gameObject.transform;
        return t.position + t.forward * PlaceAhead;
    }

    private void UpdatePlacePreview(IGameCharacter monster)
    {
        bool show = monster != null && EscapeManager.ActionsAllowed && LocalChargesLeft(manager) > 0;
        Vector3 spot = show ? PlaceSpot(monster) : Vector3.zero;
        float distance = show ? manager.NearestMaterialBuilding(spot, out _) : float.MaxValue;
        if (!show || distance > EscapeAuthority.BombBuildingRadius + PreviewMargin)
        {
            if (placePreview != null) placePreview.Hide();
            return;
        }
        if (placePreview == null) placePreview = BalloonAimPreview.Create(dotted: true);
        placePreview.ShowSpot(manager.SnapToGround(spot), 0.6f, distance <= EscapeAuthority.BombBuildingRadius ? CanPlace : CannotPlace);
    }

    // 내가 놓은 함정 위치(§69 F5): 벽 너머로도 화면에 빨간 표시(화면 밖이면 가장자리 화살표). 괴물 본인 화면만.
    private void UpdateMarkers()
    {
        if (markers == null) markers = ScreenEdgeMarkers.Create("SirenTrapMarkers", new Color(1f, 0.15f, 0.15f));
        markers.Begin();
        EscapeState s = manager.State;
        if (s != null && RoomState.IsLocalMonster() && PhotonNetwork.LocalPlayer != null)
        {
            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            foreach (EscapeState.Bomb b in s.Bombs)
                if (b.Owner == me && !b.Spent && !b.Caged) markers.Add(b.Pos + Vector3.up * 0.6f);
        }
        markers.End();
    }

    // 수색 폭탄(§70.3, Q4): 수색 키를 누르고 있는 동안 조준점·점선 궤적, 떼면 그때 보는 곳으로 던진다. 쿨타임 중에 누르면 거절음.
    private readonly ThrowPreview searchPreview = new ThrowPreview();
    private static readonly Color SearchPreviewColor = new Color(1f, 0.35f, 0.3f, 0.9f);
    private bool searchAiming;

    private void UpdateSearch()
    {
        IGameCharacter monster = SearchThrower();
        ItemSO bomb = manager.Catalog.SearchBomb;
        if (monster == null || bomb == null || !bomb.IsTool || bomb.Tool.Projectile == null)
        {
            searchAiming = false;
            searchPreview.Hide();
            return;
        }
        if (PlayerInput.SearchBombPressed)
        {
            if (Time.time < SearchReadyAt) UiSoundCues.Error();
            else searchAiming = true;
        }
        if (!searchAiming) { searchPreview.Hide(); return; }
        Transform t = monster.gameObject.transform;
        if (PlayerInput.SearchBombHeld)
        {
            searchPreview.Show(Camera.main, MonsterHand(t), t, bomb.Tool.Projectile, bomb.Tool.HitRadius, SearchPreviewColor);
            return;
        }
        searchAiming = false;
        searchPreview.Hide();
        ThrowSearchBomb(monster);
    }

    // 지금 보는 곳으로 수색 폭탄을 던진다(시험에서도 부른다). 던졌으면 true.
    public bool ThrowSearchBomb(IGameCharacter monster)
    {
        ItemSO bomb = manager.Catalog.SearchBomb;
        if (monster == null || bomb == null || bomb.Tool.Projectile == null || ProjectileSystem.Instance == null || Time.time < SearchReadyAt) return false;
        SearchReadyAt = Time.time + EscapeAuthority.SearchCooldown;
        Transform t = monster.gameObject.transform;
        ThrowAim aim = ThrowAim.FromCamera(Camera.main, MonsterHand(t), t.forward, bomb.Tool.Projectile);
        ProjectileSystem.Instance.Throw(bomb, -1, 0, aim, t);
        GameAudio.Play(SoundId.BalloonThrow);
        return true;
    }

    private static IGameCharacter SearchThrower()
    {
        if (!EscapeManager.ActionsAllowed || !RoomState.IsLocalMonster()) return null;
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.Role == CharacterRole.Monster && c.View != null && c.View.IsMine && c.CanInteract) return c;
        return null;
    }

    // 술래 손(몸 앞 위쪽) — 거대한 몸이라 사람 손 뼈가 없다. 카메라가 보는 수평 방향으로.
    private const float HandHeight = 2.6f, HandAhead = 1.4f;
    public static Vector3 MonsterHand(Transform monster)
    {
        Camera cam = Camera.main;
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : monster.forward;
        if (fwd.sqrMagnitude < 0.01f) fwd = monster.forward;
        return monster.position + Vector3.up * HandHeight + fwd.normalized * HandAhead;
    }

    private static bool IsMine(EscapeState.Bomb b) => PhotonNetwork.LocalPlayer != null && b.Owner == PhotonNetwork.LocalPlayer.ActorNumber;

    private void Sync()
    {
        EscapeState s = manager.State;
        bool monster = RoomState.IsLocalMonster();
        stale.Clear();
        foreach (var kv in views)
        {
            int i = s != null ? s.BombIndex(kv.Key) : -1;
            if (i < 0 || s.Bombs[i].Spent || !monster || !IsMine(s.Bombs[i])) stale.Add(kv.Key);
        }
        foreach (int id in stale)
        {
            if (views[id] != null) Destroy(views[id].gameObject);
            views.Remove(id);
        }
        if (s == null || !monster) return;
        foreach (EscapeState.Bomb b in s.Bombs)
        {
            if (b.Spent || views.ContainsKey(b.Id) || !IsMine(b)) continue; // 내 함정만 보인다(§69 F5)
            views[b.Id] = NoiseBombView.Create(manager, b, transform);
        }
    }
}
