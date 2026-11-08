using System.Collections.Generic;
using UnityEngine;

// 호박 마차 탈것(Request1009Plan.md §68.7) — 모든 화면. EscapeManager.Start가 붙인다.
// 상태(EscapeState.Carriages)에 마차가 생기면 그 자리에 모델(Resources/Escape/PumpkinCarriage)을 연기와 함께 세우고,
// 목록에서 빠지면 연기와 함께 치운다. 문·탑승·순간이동 시간표는 각 마차의 PumpkinCarriageView가 그린다.
public class PumpkinCarriagePresenter : MonoBehaviour
{
    public const string PrefabPath = "Escape/PumpkinCarriage";

    private EscapeManager manager;
    private GameObject prefab;
    private readonly Dictionary<int, PumpkinCarriageView> views = new Dictionary<int, PumpkinCarriageView>();
    private readonly List<int> stale = new List<int>();

    public void Init(EscapeManager owner)
    {
        manager = owner;
        prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) Debug.LogWarning($"[PumpkinCarriage] Prefab Resources/{PrefabPath} not found — run TagOfChaos/Escape/Build Pumpkin Carriage Prefab.");
        manager.StateChanged += Sync;
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= Sync;
    }

    private void Sync()
    {
        EscapeState s = manager.State;
        stale.Clear();
        foreach (var kv in views)
            if (s == null || s.CarriageIndex(kv.Key) < 0) stale.Add(kv.Key);
        foreach (int id in stale)
        {
            if (views[id] != null) views[id].Vanish();
            views.Remove(id);
        }
        if (s == null || prefab == null) return;
        foreach (EscapeState.Carriage c in s.Carriages)
        {
            if (views.TryGetValue(c.Id, out PumpkinCarriageView view) && view != null) { view.Apply(c); continue; }
            var go = Instantiate(prefab, c.Pos, PumpkinCarriageRules.Rotation(c.Yaw));
            go.name = "PumpkinCarriage_" + c.Id;
            view = go.GetComponent<PumpkinCarriageView>();
            if (view == null) view = go.AddComponent<PumpkinCarriageView>();
            view.Bind(manager, c);
            views[c.Id] = view;
        }
    }

    // 쓴 사람 앞 빈 바닥(막히면 옆·뒤)에 문(-X)이 쓴 사람을 보게(VehicleSpot).
    public static bool TryFindSummonSpot(EscapeManager manager, Vector3 user, Vector3 forward, out Vector3 spot, out float yaw) =>
        VehicleSpot.TryFind(manager, user, forward, PumpkinCarriageRules.SummonDistance, Vector3.left, PumpkinCarriageRules.HalfExtents, out spot, out yaw);

    public static void Smoke(Vector3 point, float size) => VehicleSpot.Smoke(point, size, new Color(1f, 0.7f, 0.4f));
}
