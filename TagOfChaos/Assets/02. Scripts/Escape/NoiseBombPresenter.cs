using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 괴물 사이렌 함정(옛 소음폭탄 — Request1009Plan.md §25.5 K, §25.3.1 R7, §68 GP5). 스킬 키(Q)로 발밑에 놓고(한 번에 하나, 풀리거나 치운 뒤 20초),
// 근처 건물에서 누가 수리를 시작하면 방장이 건물을 빼빼로 철창에 가둔다(SirenTrapPresenter). 놓인 함정은 괴물에게만 보이고, 자기 함정은 E로 치울 수 있다.
// 수색 폭탄(§68 GP5): 수색 키(R)로 앞으로 던지면 둘레의 진짜 사람만 기침한다(쿨타임 EscapeAuthority.SearchCooldown).
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
    }

    // 지금 내 괴물의 남은 칸(HUD).
    public static int LocalChargesLeft(EscapeManager m)
    {
        if (m == null || m.State == null || PhotonNetwork.LocalPlayer == null) return 0;
        return m.State.BombChargesLeft(PhotonNetwork.LocalPlayer.ActorNumber, EscapeAuthority.BombMaxCharges, PhotonNetwork.Time, EscapeAuthority.BombCooldown);
    }

    // 내 수색 폭탄을 다시 쓸 수 있는 시각(Time.time, HUD — 방장도 같은 쿨타임으로 막는다).
    public static float SearchReadyAt { get; private set; }

    private void Update()
    {
        UpdateSearch();
        if (!PlayerInput.SkillPressed || !EscapeManager.ActionsAllowed || !RoomState.IsLocalMonster()) return;
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (c.Role != CharacterRole.Monster || c.View == null || !c.View.IsMine || !c.CanInteract) continue;
            if (LocalChargesLeft(manager) <= 0) { UiSoundCues.Error(); return; }
            Transform t = c.gameObject.transform;
            manager.Request(EscapeOp.PlaceBomb, 0, 0, t.position + t.forward * 1.2f);
            return;
        }
    }

    private void UpdateSearch()
    {
        if (!PlayerInput.SearchBombPressed || !EscapeManager.ActionsAllowed || !RoomState.IsLocalMonster()) return;
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (c.Role != CharacterRole.Monster || c.View == null || !c.View.IsMine || !c.CanInteract) continue;
            if (Time.time < SearchReadyAt) { UiSoundCues.Error(); return; }
            SearchReadyAt = Time.time + EscapeAuthority.SearchCooldown;
            Transform t = c.gameObject.transform;
            Camera cam = Camera.main;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : t.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = t.forward;
            manager.Request(EscapeOp.SearchBomb, 0, 0, manager.SnapToGround(t.position + fwd * EscapeAuthority.SearchThrowRange));
            GameAudio.Play(SoundId.BalloonThrow);
            return;
        }
    }

    private void Sync()
    {
        EscapeState s = manager.State;
        bool monster = RoomState.IsLocalMonster();
        stale.Clear();
        foreach (var kv in views)
        {
            int i = s != null ? s.BombIndex(kv.Key) : -1;
            if (i < 0 || s.Bombs[i].Spent || !monster) stale.Add(kv.Key);
        }
        foreach (int id in stale)
        {
            if (views[id] != null) Destroy(views[id].gameObject);
            views.Remove(id);
        }
        if (s == null || !monster) return;
        foreach (EscapeState.Bomb b in s.Bombs)
        {
            if (b.Spent || views.ContainsKey(b.Id)) continue;
            views[b.Id] = NoiseBombView.Create(manager, b, transform);
        }
    }
}
