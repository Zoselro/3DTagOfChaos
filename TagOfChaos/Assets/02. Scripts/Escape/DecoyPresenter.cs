using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 맵에 깔린 AI(미끼, Request1009Plan.md §25.5 L)를 그린다. PhotonView 없이 상태의 Decoys만 보고 각 화면이 만든다.
// 모습 = 그 사람 캐릭터(쿠키 프리팹을 겉모습만 남기고 복제 + 판의 캐릭터 색). 늘 가만히 서 있고(Idle), 녹음기가 붙으면
// 주인의 녹음 목소리를 5~20초 간격으로 3D로 낸다(§26 H7). 괴물은 진짜 쿠키처럼 조준 + E로 잡는다(DecoyView — 잡기 동작·가루 뒤 사라짐). 주인이 잡히면 그 사람 AI는 모두 사라진다.
public class DecoyPresenter : MonoBehaviour
{
    private const string CookiePrefabPath = "HideOrSeekPlayer";

    private EscapeManager manager;
    private Transform holder;          // 꺼진 채로 복제해 Awake가 돌지 않게 하는 보관함
    private GameObject template;       // 겉모습만 남긴 쿠키(한 번 만들어 복제)
    private readonly Dictionary<int, DecoyView> views = new Dictionary<int, DecoyView>();
    private readonly List<int> stale = new List<int>();

    public void Init(EscapeManager owner)
    {
        manager = owner;
        manager.StateChanged += Sync;
        Sync();
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= Sync;
    }

    private void Sync()
    {
        EscapeState s = manager.State;
        stale.Clear();
        foreach (int id in views.Keys) if (s == null || s.DecoyIndex(id) < 0) stale.Add(id);
        foreach (int id in stale)
        {
            DecoyView v = views[id];
            views.Remove(id);
            if (v != null) v.Vanish();
        }
        if (s == null) return;
        foreach (EscapeState.Decoy d in s.Decoys)
        {
            if (!views.TryGetValue(d.Id, out DecoyView view) || view == null)
            {
                view = Create(d);
                if (view == null) return;
                views[d.Id] = view;
            }
            view.Apply(d);
        }
    }

    private void Update()
    {
        EscapeState s = manager.State;
        if (s == null) return;
        double now = PhotonNetwork.Time;
        foreach (EscapeState.Decoy d in s.Decoys)
            if (views.TryGetValue(d.Id, out DecoyView view) && view != null) view.Tick(d, now);
    }

    private DecoyView Create(EscapeState.Decoy d)
    {
        if (template == null && !BuildTemplate()) return null;
        var go = Instantiate(template, holder);
        go.name = "Decoy_" + d.Id;
        go.transform.SetParent(transform, true);
        go.SetActive(true);
        var view = go.AddComponent<DecoyView>();
        view.Init(manager, d.Id);
        ApplySkin(go, d.Owner);
        return view;
    }

    private bool BuildTemplate()
    {
        var prefab = Resources.Load<GameObject>(CookiePrefabPath);
        if (prefab == null) return false;
        holder = new GameObject("DecoyHolder").transform;
        holder.SetParent(transform, false);
        holder.gameObject.SetActive(false);
        template = Instantiate(prefab, holder);
        template.name = "DecoyTemplate";
        WitchFinaleStage.StripToVisuals(template);
        var capsule = template.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.95f, 0f);
        capsule.height = 1.9f;
        capsule.radius = 0.35f;
        return true;
    }

    // 판의 캐릭터 색(모델이 오기 전 임시 겉모습 — PlayerSkinApplier와 같은 규칙).
    private static void ApplySkin(GameObject go, int owner)
    {
        SkinCatalogSO catalog = GameSettings.Current.CharacterCatalog;
        if (catalog == null || catalog.Count == 0) return;
        Photon.Realtime.Player p = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(owner) : null;
        int selected = p != null && RoomState.TryGetPlayerInt(p, NetKeys.SkinIndex, out int s) ? s : CharacterAssignment.Unselected;
        Material skin = catalog.GetMaterial(CharacterAssignment.Current(owner, selected));
        if (skin == null) return;
        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.sharedMaterial = skin;
    }
}
