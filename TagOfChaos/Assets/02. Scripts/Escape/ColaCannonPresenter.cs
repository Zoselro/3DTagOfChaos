using System.Collections.Generic;
using UnityEngine;

// 콜라 대포 탈것(Request1009Plan.md §68.9) — 모든 화면. EscapeManager.Start가 붙인다.
// 상태(EscapeState.Cannons)에 대포가 생기면 그 자리에 모델(Resources/Escape/ColaCannon)을 연기와 함께 세우고, 목록에서 빠지면 치운다.
// 해치·회전대·포신·발사·비행 시간표는 각 대포의 ColaCannonView가 그린다.
public class ColaCannonPresenter : MonoBehaviour
{
    public const string PrefabPath = "Escape/ColaCannon";

    private EscapeManager manager;
    private GameObject prefab;
    private readonly Dictionary<int, ColaCannonView> views = new Dictionary<int, ColaCannonView>();
    private readonly List<int> stale = new List<int>();

    public void Init(EscapeManager owner)
    {
        manager = owner;
        prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) Debug.LogWarning($"[ColaCannon] Prefab Resources/{PrefabPath} not found — run TagOfChaos/Escape/Build Cola Cannon Prefab.");
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
            if (s == null || s.CannonIndex(kv.Key) < 0) stale.Add(kv.Key);
        foreach (int id in stale)
        {
            if (views[id] != null) views[id].Vanish();
            views.Remove(id);
        }
        if (s == null || prefab == null) return;
        foreach (EscapeState.Cannon c in s.Cannons)
        {
            if (views.TryGetValue(c.Id, out ColaCannonView view) && view != null) { view.Apply(c); continue; }
            var go = Instantiate(prefab, c.Pos, ColaCannonRules.Rotation(c.Yaw));
            go.name = "ColaCannon_" + c.Id;
            view = go.GetComponent<ColaCannonView>();
            if (view == null) view = go.AddComponent<ColaCannonView>();
            view.Bind(manager, c);
            views[c.Id] = view;
        }
    }

    // 쓴 사람 앞 빈 바닥(막히면 옆·뒤)에 뒤 계단(-Z)이 쓴 사람을 보게.
    public static bool TryFindSummonSpot(EscapeManager manager, Vector3 user, Vector3 forward, out Vector3 spot, out float yaw) =>
        VehicleSpot.TryFind(manager, user, forward, ColaCannonRules.SummonDistance, ColaCannonRules.FaceLocal, ColaCannonRules.HalfExtents, out spot, out yaw);

    public static void Smoke(Vector3 point, float size) => VehicleSpot.Smoke(point, size, new Color(0.95f, 0.45f, 0.4f));

    // 발사 순간 입구의 콜라 거품(갈색·흰 거품이 포신 방향으로 뿜어져 나옴).
    public static void Fizz(Vector3 mouth, Vector3 dir)
    {
        ParticleSystem foam = EscapeVisuals.CreateParticles("ColaFizz", null, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(0.9f, 1.6f), Speed = new Vector2(7f, 15f), Size = new Vector2(0.7f, 1.5f),
            Color = new Color(1f, 0.95f, 0.85f, 0.95f), MaxParticles = 160, EmissionOff = true, Gravity = 1.2f,
            Shape = ParticleSystemShapeType.Cone, Angle = 22f, Radius = 0.45f,
            GrowTo = 1.8f, FadeFrom = Color.white, FadeTo = new Color(0.45f, 0.2f, 0.08f), AlphaFrom = 0.95f, Once = true,
        });
        foam.transform.SetPositionAndRotation(mouth, Quaternion.LookRotation(dir));
        foam.Emit(140);
        Destroy(foam.gameObject, 2.5f);
    }
}
