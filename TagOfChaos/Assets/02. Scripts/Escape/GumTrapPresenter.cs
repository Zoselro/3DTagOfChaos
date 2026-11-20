using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// §90 G6 껌 수류탄 모습 — 모든 화면. EscapeManager.Start가 붙인다.
// - 바닥 웅덩이(State.Puddles): 모두에게 보임. 밟히면 3.5초 동안 줄며 사라짐.
// - 맞은 술래(State.Slows): 두 발에 보라 껌 덩어리 + 발과 처음 붙은 바닥을 잇는 늘어나는 껌 줄(걸을수록 가늘어지고 4 m 넘으면 끊김), 3.5초 동안 줄어듦.
// - Splat: 터지는 순간 보라 방울(ToolHitReceiver).
public class GumTrapPresenter : MonoBehaviour
{
    private const float PuddleModelDiameter = 2.9f;   // SK_GumPuddle 지름(모델)
    private const float StuckModelDiameter = 0.95f;   // SK_GumStuck 고리 지름(모델)
    private const float StrandBreak = 4f;
    private static readonly Color GumColor = new Color(0.62f, 0.25f, 0.78f, 1f);

    private EscapeManager manager;
    private readonly Dictionary<int, Transform> puddles = new Dictionary<int, Transform>();
    private readonly Dictionary<int, Stuck> stuck = new Dictionary<int, Stuck>();
    private readonly List<int> stale = new List<int>();
    private static Material strandMat;

    private sealed class Stuck
    {
        public double At;
        public Transform FootL, FootR;
        public Transform GooL, GooR;
        public LineRenderer LineL, LineR;
        public Vector3 AnchorL, AnchorR;
        public bool BrokeL, BrokeR;
        public bool Toasted;
    }

    public void Init(EscapeManager owner) { manager = owner; }

    private void OnDestroy()
    {
        foreach (Transform t in puddles.Values) if (t != null) Destroy(t.gameObject);
        foreach (Stuck s in stuck.Values) Clear(s);
    }

    private void Update()
    {
        EscapeState s = manager != null ? manager.State : null;
        double now = PhotonNetwork.Time;
        SyncPuddles(s, now);
        SyncStuck(s, now);
    }

    // ---------------- 웅덩이 ----------------

    private void SyncPuddles(EscapeState s, double now)
    {
        stale.Clear();
        foreach (var kv in puddles)
        {
            int i = s != null ? s.Puddles.FindIndex(p => p.Id == kv.Key) : -1;
            if (i < 0) stale.Add(kv.Key);
        }
        foreach (int id in stale) { if (puddles[id] != null) Destroy(puddles[id].gameObject); puddles.Remove(id); }
        if (s == null) return;
        foreach (EscapeState.Puddle p in s.Puddles)
        {
            if (!puddles.TryGetValue(p.Id, out Transform view) || view == null) puddles[p.Id] = view = CreatePuddle(p);
            float k = GumTrapRules.PuddleScale(p, now);
            float size = GumTrapRules.PuddleRadius * 2f / PuddleModelDiameter;
            view.localScale = new Vector3(size * k, size * Mathf.Lerp(0.4f, 1f, k), size * k);
            view.gameObject.SetActive(k > 0.02f);
        }
    }

    private Transform CreatePuddle(EscapeState.Puddle p)
    {
        GameObject model = EscapeCatalogSO.Current != null ? EscapeCatalogSO.Current.GumPuddleModel : null;
        GameObject go = new GameObject("GumPuddle_" + p.Id);   // FBX 루트가 메시 자체라 감싸는 부모에 크기를 준다
        go.transform.SetParent(transform, false);
        if (model != null) Instantiate(model, go.transform, false); else FallbackDisc().transform.SetParent(go.transform, false);
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        go.transform.SetPositionAndRotation(p.Pos + Vector3.up * 0.02f, Quaternion.Euler(0f, (p.Id * 73) % 360, 0f));
        return go.transform;
    }

    private GameObject FallbackDisc()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(PuddleModelDiameter, 0.03f, PuddleModelDiameter);
        EscapeVisuals.Tint(go, GumColor, 0.3f);
        return go;
    }

    // ---------------- 술래 발 ----------------

    private void SyncStuck(EscapeState s, double now)
    {
        stale.Clear();
        foreach (var kv in stuck)
        {
            double? at = GumTrapRules.LatestHit(s, kv.Key);
            if (!at.HasValue || at.Value != kv.Value.At || now >= at.Value + GumTrapRules.SlowSeconds) stale.Add(kv.Key);
        }
        foreach (int id in stale) { Clear(stuck[id]); stuck.Remove(id); }
        if (s == null) return;
        foreach (EscapeState.Slow x in s.Slows)
        {
            if (now < x.At || now >= x.At + GumTrapRules.SlowSeconds) continue;
            if (!stuck.TryGetValue(x.Actor, out Stuck st))
            {
                IGameCharacter c = CharacterRegistry.FindByOwner(x.Actor);
                if (c == null) continue;
                stuck[x.Actor] = st = Create(c, x.At);
                GameAudio.PlayAt(SoundId.GumStick, c.gameObject.transform.position);
            }
            float k = GumTrapRules.StickAmount(st.At, now);
            Place(st.GooL, st.FootL, k);
            Place(st.GooR, st.FootR, k);
            Strand(st.LineL, st.FootL, st.AnchorL, k, ref st.BrokeL);
            Strand(st.LineR, st.FootR, st.AnchorR, k, ref st.BrokeR);
            if (!st.Toasted && PhotonNetwork.LocalPlayer != null && x.Actor == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                st.Toasted = true;
                EscapeHud.Toast(EscapeTextsSO.Current.gumStuck);
            }
        }
    }

    private Stuck Create(IGameCharacter c, double at)
    {
        Transform root = c.gameObject.transform;
        var st = new Stuck { At = at, FootL = FindDeep(root, "Foot_L") ?? root, FootR = FindDeep(root, "Foot_R") ?? root };
        float scale = root.lossyScale.y;
        st.GooL = CreateGoo(scale);
        st.GooR = CreateGoo(scale);
        st.AnchorL = Ground(st.FootL.position);
        st.AnchorR = Ground(st.FootR.position);
        st.LineL = CreateStrand();
        st.LineR = CreateStrand();
        return st;
    }

    private Transform CreateGoo(float characterScale)
    {
        GameObject model = EscapeCatalogSO.Current != null ? EscapeCatalogSO.Current.GumStuckModel : null;
        GameObject go = new GameObject("GumStuck");
        go.transform.SetParent(transform, false);
        if (model != null) Instantiate(model, go.transform, false);
        else { GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.transform.SetParent(go.transform, false); EscapeVisuals.Tint(s, GumColor, 0.3f); }
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        go.name = "GumStuck";
        // 술래 발(키 약 6.5 m) 둘레를 감싸는 크기: 지름 = 캐릭터 크기 × 0.35 m
        go.transform.localScale = Vector3.one * (0.35f * Mathf.Max(1f, characterScale) / StuckModelDiameter);
        go.transform.localScale *= 1f;
        go.AddComponent<GooBase>().Base = go.transform.localScale;
        return go.transform;
    }

    private sealed class GooBase : MonoBehaviour { public Vector3 Base; }

    private static void Place(Transform goo, Transform foot, float k)
    {
        if (goo == null || foot == null) return;
        goo.position = foot.position;
        goo.rotation = Quaternion.Euler(0f, foot.eulerAngles.y, 0f);
        Vector3 b = goo.GetComponent<GooBase>().Base;
        goo.localScale = new Vector3(b.x * (0.5f + 0.5f * k), b.y * k, b.z * (0.5f + 0.5f * k));
        goo.gameObject.SetActive(k > 0.02f);
    }

    private LineRenderer CreateStrand()
    {
        var go = new GameObject("GumStrand");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        if (strandMat == null) { strandMat = new Material(Shader.Find("Sprites/Default")); strandMat.color = GumColor; }
        lr.sharedMaterial = strandMat;
        lr.positionCount = 6;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.startColor = lr.endColor = GumColor;
        return lr;
    }

    // 발에서 처음 붙은 바닥까지 늘어난 껌 줄(가운데가 처지고, 멀어질수록·시간이 갈수록 가늘다).
    private static void Strand(LineRenderer lr, Transform foot, Vector3 anchor, float k, ref bool broke)
    {
        if (lr == null || foot == null) return;
        Vector3 a = foot.position, b = anchor;
        float len = Vector3.Distance(a, b);
        if (len > StrandBreak) broke = true;
        lr.enabled = !broke && k > 0.05f && len > 0.15f;
        if (!lr.enabled) return;
        float w = Mathf.Lerp(0.02f, 0.12f, k) * Mathf.Clamp01(1.2f - len / StrandBreak);
        lr.startWidth = w; lr.endWidth = w * 1.6f;
        for (int i = 0; i < lr.positionCount; i++)
        {
            float t = i / (float)(lr.positionCount - 1);
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= Mathf.Sin(t * Mathf.PI) * len * 0.12f;
            lr.SetPosition(i, p);
        }
    }

    private static Vector3 Ground(Vector3 p) =>
        Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit h, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? h.point : p;

    private static void Clear(Stuck s)
    {
        if (s == null) return;
        foreach (Component c in new Component[] { s.GooL, s.GooR, s.LineL, s.LineR }) if (c != null) Destroy(c.gameObject);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // ---------------- 터짐 ----------------

    public static void Splat(Vector3 point, bool hitMonster)
    {
        var go = new GameObject("GumSplat");
        go.transform.position = point;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.3f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f); main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.gravityModifier = 2.2f; main.startColor = new ParticleSystem.MinMaxGradient(GumColor, new Color(0.85f, 0.45f, 0.95f));
        main.stopAction = ParticleSystemStopAction.Destroy; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(hitMonster ? 40 : 28)) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.3f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (strandMat == null) { strandMat = new Material(Shader.Find("Sprites/Default")); strandMat.color = GumColor; }
        r.sharedMaterial = strandMat;
        ps.Play();
    }
}
