using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 맵에 깔린 AI(미끼, Request1009Plan.md §25.5 L)를 그린다. PhotonView 없이 상태의 Decoys만 보고 각 화면이 만든다.
// 모습 = 그 사람 캐릭터(쿠키 프리팹을 겉모습만 남기고 복제 + 그 캐릭터 모델·색, 캐릭터마다 본보기 하나, §45 CH6). 경로를 따라 움직이고(§44), 녹음기가 붙으면
// 주인의 녹음 목소리를 5~20초 간격으로 3D로 낸다(§26 H7). 괴물은 진짜 쿠키처럼 조준 + E로 잡는다(DecoyView — 잡기 동작·가루 뒤 사라짐). 주인이 잡히면 그 사람 AI는 모두 사라진다.
public class DecoyPresenter : MonoBehaviour
{
    private const string CookiePrefabPath = "HideOrSeekPlayer";

    private EscapeManager manager;
    private Transform holder;          // 꺼진 채로 복제해 Awake가 돌지 않게 하는 보관함
    private readonly Dictionary<int, GameObject> templates = new Dictionary<int, GameObject>(); // 캐릭터 칸 → 겉모습만 남긴 본보기
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
        int index = CharacterIndexOf(d.Owner);
        GameObject template = Template(index);
        if (template == null) return null;
        var go = Instantiate(template, holder);
        go.name = "Decoy_" + d.Id;
        go.transform.SetParent(transform, true);
        go.SetActive(true);
        var view = go.AddComponent<DecoyView>();
        view.Init(manager, d.Id, CrumbColorsOf(index));
        return view;
    }

    // 캐릭터 칸마다 한 번: 쿠키 프리팹을 겉모습만 남기고 그 캐릭터 모델·색을 입힌다(PlayerSkinApplier와 같은 규칙).
    private GameObject Template(int index)
    {
        if (templates.TryGetValue(index, out GameObject cached) && cached != null) return cached;
        var prefab = Resources.Load<GameObject>(CookiePrefabPath);
        if (prefab == null) return null;
        if (holder == null)
        {
            holder = new GameObject("DecoyHolder").transform;
            holder.SetParent(transform, false);
            holder.gameObject.SetActive(false);
        }
        GameObject template = Instantiate(prefab, holder);
        template.name = "DecoyTemplate_" + index;
        WitchFinaleStage.StripToVisuals(template);
        var capsule = template.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.95f, 0f);
        capsule.height = 1.9f;
        capsule.radius = 0.35f;
        SkinCatalogSO catalog = GameSettings.Current.CharacterCatalog;
        if (catalog != null && catalog.Count > 0)
        {
            GameObject model = catalog.GetModel(index);
            if (model != null) CharacterModelSwap.Apply(template.transform, model);
            else
            {
                Material skin = catalog.GetMaterial(index);
                if (skin != null) foreach (SkinnedMeshRenderer r in template.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.sharedMaterial = skin;
            }
        }
        templates[index] = template;
        return template;
    }

    private static int CharacterIndexOf(int owner)
    {
        Photon.Realtime.Player p = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(owner) : null;
        if (p != null) return PlayerSkinApplier.CharacterIndexOf(p);
        return CharacterAssignment.Current(owner, CharacterAssignment.Unselected);
    }

    private static Color[] CrumbColorsOf(int index)
    {
        SkinCatalogSO catalog = GameSettings.Current.CharacterCatalog;
        return catalog != null ? catalog.GetCrumbColors(index) : null;
    }
}
