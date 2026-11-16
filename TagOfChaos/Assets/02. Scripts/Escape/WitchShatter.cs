using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 마녀가 내리칠 때 맵이 박살 난다(Request1009Plan.md §23.5 X7, 결정 Q4 a). 맵 씬의 WitchPresenter 옆에 붙는다(없으면 마녀가 붙인다).
// - 마녀가 밟은 곳(WitchPresenter.ImpactPoint, §24.10)에서 충격파가 초속 WaveSpeed로 퍼지며, 도착한 물체는 숨기고 먼지가 인다.
//   §82: 큐브 조각을 없앴다. 카메라에서 가까운 MaxFractured개는 거대 괴물이 부술 때와 같은 Blender 조각 모델 + 구운 애니메이션으로 무너진다
//   (부서지는 목록의 물체는 그 조각 모델, 목록 밖 물체는 종류 이름이 같은 조각 모델 — 없으면 먼지만). 나머지는 먼지만.
// - 대상은 이름 목록이 아니라 일반 규칙(맵을 바꿔도 그대로): 중심이 "Ground" 사각형 안에 있는 MeshRenderer 묶음. Ground 자신·Background·
//   FarBackdrop·캐릭터·마녀·물 같은 넓은 평면·아주 작은 것은 뺀다(IsBreakable).
// - 판이 끝나는 장면이라 네트워크로 맞추지 않는다 — 각 화면이 같은 순간(주먹이 닿은 순간)에 같은 규칙으로 보여 준다.
// - 마녀가 나타나면(걸어오는 동안) 대상을 미리 고르고, 카메라 가까운 대상의 조각 모델을 미리 비동기로 읽어 둔다 — 주먹이 닿는 순간 끊기지 않게.
// 쿠키·괴물의 몸은 부스러기 파티클로 터진다(ShatterBody — 도착 연출의 대역 WitchFinaleStage, 늦게 들어온 화면의 CookieLifeStatePresenter·
// MonsterEscapeState). 예전에는 몸 색 큐브였다(Request1009Plan.md §82 Q2): 몸 크기에 맞춘 부스러기(불규칙한 조각 모양 4종)가 튀어 바닥에
// 떨어져 굴렀다가 사라진다. 색은 그 캐릭터의 부스러기 색(§45) 둘레로 조금씩 다르게.
public class WitchShatter : MonoBehaviour
{
    public const float WaveSpeed = 120f;         // 충격파 속도(m/s) — 키 270 m 마녀(§24.10)가 밟으면 맵 끝까지(약 140 m) 1초 남짓
    public const int MaxFractured = 40;          // 조각 모델로 무너지는 물체 수(카메라 가까운 순) — 나머지는 먼지만
    public const float FractureHoldSeconds = 2.6f;
    public const float MinBreakSize = 0.3f;      // 이보다 작은 것은 그대로
    public const float FlatHeight = 0.3f;        // 이보다 얇고 넓은 것(물·바닥 무늬)은 그대로
    public const float FlatWidth = 4f;
    private static readonly string[] KeepRoots = { WitchPath.GroundName, "Background", WitchPath.FarBackdropName };

    public static WitchShatter Current { get; private set; }

    private struct Target
    {
        public Transform Root;
        public Renderer[] Renderers;
        public Bounds Bounds;
        public float At;
        public DestructibleUnit Unit;       // 부서지는 목록의 물체(그 조각 모델·원본 노드) — 없으면 null
        public string FractureName;         // 조각 모델 이름(없으면 null → 먼지만)
        public Transform ModelRoot;
        public bool Fracture;               // 이 화면에서 조각 모델로 무너뜨린다(카메라 가까운 MaxFractured개)
    }

    private readonly List<Target> pending = new List<Target>();
    private ParticleSystem dust;
    private bool fired;
    private List<Target> prepared;          // 마녀가 나타난 뒤 미리 고른 대상
    public int FracturedCount { get; private set; } // 시험용: 이번에 조각 모델로 무너진 수
    public int DustOnlyCount { get; private set; }

    private void OnEnable()
    {
        Current = this;
        WitchPresenter.Impacted += OnImpact;
    }

    private void OnDisable()
    {
        WitchPresenter.Impacted -= OnImpact;
        if (Current == this) Current = null;
    }

    private void OnImpact()
    {
        if (fired) return;
        fired = true;
        WitchPresenter witch = WitchPresenter.Current;
        if (witch == null) return;
        Vector3 impact = witch.ImpactPoint;
        GameAudio.Play(SoundId.WitchShatter);
        if (!WitchPath.TryGetGroundRect(gameObject.scene, out Rect ground)) return;
        float now = Time.time;
        var list = prepared ?? new List<Target>(FindTargets(gameObject.scene, ground));
        for (int i = 0; i < list.Count; i++)
        {
            Target t = list[i];
            if (t.Root == null) continue;
            t.Bounds = CurrentBounds(t); // 미리 고른 뒤 움직였을 수 있다(떠나는 탈것·문)
            t.At = now + Flat(t.Bounds.center - impact).magnitude / WaveSpeed;
            list[i] = t;
        }
        PickFractured(list, true);
        foreach (Target t in list) if (t.Root != null) pending.Add(t);
        pending.Sort((a, b) => a.At.CompareTo(b.At));
        Debug.Log($"[WitchShatter] {pending.Count} object(s) break from {impact}.");
    }

    private void Update()
    {
        Prepare();
        float now = Time.time;
        int done = 0;
        while (done < pending.Count && pending[done].At <= now) Break(pending[done++]);
        if (done > 0) pending.RemoveRange(0, done);
    }

    // 마녀가 보이기 시작하면 대상을 한 번 고르고, 카메라 가까운 대상의 조각 모델을 미리 읽기 시작한다.
    private void Prepare()
    {
        if (fired || prepared != null) return;
        WitchPresenter witch = WitchPresenter.Current;
        if (witch == null || !witch.IsShown) return;
        prepared = WitchPath.TryGetGroundRect(gameObject.scene, out Rect ground)
            ? new List<Target>(FindTargets(gameObject.scene, ground))
            : new List<Target>();
        PickFractured(prepared, false);
        if (dust == null) EmitDust(transform.position, 0f, prewarmOnly: true);
    }

    // 카메라에서 가까운 MaxFractured개(조각 모델이 있는 것)만 조각으로. atImpact: 이미 다 읽은 것만(아직 읽는 중이면 먼지만).
    private static void PickFractured(List<Target> list, bool atImpact)
    {
        Camera cam = Camera.main;
        Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
        var order = new List<int>();
        for (int i = 0; i < list.Count; i++)
        {
            Target t = list[i];
            t.Fracture = false;
            list[i] = t;
            if (t.Root != null && !string.IsNullOrEmpty(t.FractureName) && (t.Unit == null || !t.Unit.Broken)) order.Add(i);
        }
        order.Sort((a, b) => (list[a].Bounds.center - eye).sqrMagnitude.CompareTo((list[b].Bounds.center - eye).sqrMagnitude));
        int picked = 0;
        foreach (int i in order)
        {
            if (picked >= MaxFractured) break;
            Target t = list[i];
            FracturedModels.Request(t.FractureName);
            if (atImpact && FracturedModels.GetSet(t.FractureName, out _) == null) continue;
            t.Fracture = true;
            list[i] = t;
            picked++;
        }
    }

    private static Bounds CurrentBounds(Target t)
    {
        Bounds b = t.Bounds;
        bool any = false;
        foreach (Renderer r in t.Renderers)
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // ---------------- 대상 고르기(맵을 바꿔도 동작하는 일반 규칙) ----------------

    public static List<(Transform root, Renderer[] renderers, Bounds bounds)> FindTargetGroups(Scene scene, Rect ground)
    {
        var groups = new Dictionary<Transform, List<Renderer>>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || !IsBreakable(r, ground)) continue;
                Transform key = GroupRoot(r.transform);
                if (!groups.TryGetValue(key, out List<Renderer> list)) groups[key] = list = new List<Renderer>();
                list.Add(r);
            }
        var result = new List<(Transform, Renderer[], Bounds)>();
        foreach (KeyValuePair<Transform, List<Renderer>> g in groups)
        {
            Bounds b = g.Value[0].bounds;
            foreach (Renderer r in g.Value) b.Encapsulate(r.bounds);
            result.Add((g.Key, g.Value.ToArray(), b));
        }
        return result;
    }

    // 대상 묶음을 조각 모델 단위로: 묶음 안의 부서지는 목록 물체는 하나씩(그 조각 모델·원본 노드), 나머지 렌더러는 묶음 하나로
    // (종류 이름이 같은 조각 모델이 있으면 그것, 없으면 먼지만). 이미 부서진 물체는 뺀다.
    private static IEnumerable<Target> FindTargets(Scene scene, Rect ground)
    {
        DestructibleCatalog catalog = DestructionNet.Instance != null ? DestructionNet.Instance.Catalog : null;
        var byRenderer = new Dictionary<Renderer, DestructibleUnit>();
        if (catalog != null)
            foreach (DestructibleUnit u in catalog.Units)
                foreach (Renderer r in u.Renderers) if (r != null) byRenderer[r] = u;
        var kinds = new HashSet<string>(DestructionRulesSO.Current.PropFracturedNames);
        var seen = new HashSet<DestructibleUnit>(); // 한 물체가 두 묶음에 걸쳐 있어도 한 번만
        foreach (var g in FindTargetGroups(scene, ground))
        {
            var units = new Dictionary<DestructibleUnit, List<Renderer>>();
            var rest = new List<Renderer>();
            foreach (Renderer r in g.renderers)
            {
                if (byRenderer.TryGetValue(r, out DestructibleUnit u))
                {
                    if (u.Broken || (!units.ContainsKey(u) && seen.Contains(u))) continue;
                    seen.Add(u);
                    if (!units.TryGetValue(u, out List<Renderer> list)) units[u] = list = new List<Renderer>();
                    list.Add(r);
                }
                else rest.Add(r);
            }
            foreach (KeyValuePair<DestructibleUnit, List<Renderer>> pair in units)
                yield return new Target
                {
                    Root = pair.Key.Root, Renderers = pair.Key.Renderers, Bounds = pair.Key.Bounds, Unit = pair.Key,
                    FractureName = pair.Key.FractureName, ModelRoot = pair.Key.ModelRoot,
                };
            if (rest.Count == 0) continue;
            Bounds b = rest[0].bounds;
            foreach (Renderer r in rest) b.Encapsulate(r.bounds);
            string kind = DestructibleCatalog.KindName(g.root.name);
            yield return new Target
            {
                Root = g.root, Renderers = rest.ToArray(), Bounds = b,
                FractureName = units.Count == 0 && kinds.Contains(kind) ? kind : null, ModelRoot = g.root,
            };
        }
    }

    public static bool IsBreakable(Renderer r, Rect ground)
    {
        Bounds b = r.bounds;
        if (!ground.Contains(new Vector2(b.center.x, b.center.z))) return false;
        float wide = Mathf.Max(b.size.x, b.size.z);
        if (Mathf.Max(wide, b.size.y) < MinBreakSize) return false;
        if (b.size.y < FlatHeight && wide > FlatWidth) return false;       // 물·바닥 무늬
        for (Transform t = r.transform; t != null; t = t.parent)
        {
            if (System.Array.IndexOf(KeepRoots, t.name) >= 0) return false;
            if (t.GetComponent<WitchPresenter>() != null || t.GetComponent<IGameCharacter>() != null) return false;
        }
        return true;
    }

    // 한 물체로 부술 단위: 씬 루트에서 셋째 단계(예: Map/MainStructures/House_01) 또는 그보다 얕은 곳.
    private static Transform GroupRoot(Transform t)
    {
        var chain = new List<Transform>();
        for (Transform c = t; c != null; c = c.parent) chain.Add(c);
        return chain[Mathf.Max(0, chain.Count - 3)];
    }

    // ---------------- 부수기 ----------------

    private void Break(Target t)
    {
        if (t.Unit != null)
        {
            if (t.Unit.Broken) return; // 그사이 거대 괴물이 부쉈다
            DestructionNet.SetBroken(t.Unit, true);
        }
        else
        {
            foreach (Renderer r in t.Renderers) if (r != null) r.enabled = false;
            if (t.Root != null) foreach (Collider c in t.Root.GetComponentsInChildren<Collider>()) c.enabled = false;
        }
        EmitDust(t.Bounds.center, Mathf.Max(t.Bounds.size.x, t.Bounds.size.y, t.Bounds.size.z));
        FracturedModelSO set = t.Fracture ? FracturedModels.GetSet(t.FractureName, out _) : null;
        WitchPresenter witch = WitchPresenter.Current;
        Vector3 push = witch != null ? Flat(t.Bounds.center - witch.ImpactPoint) : Vector3.zero;
        if (set != null && t.ModelRoot != null && FracturePlayer.Play(set, t.ModelRoot, push.sqrMagnitude > 1e-4f ? push.normalized : Vector3.zero, FractureHoldSeconds, true) != null)
            FracturedCount++;
        else DustOnlyCount++;
    }

    // 쿠키·괴물 몸: 몸 크기·색의 조각 몇 개와 먼지.
    public static void ShatterBody(Renderer[] renderers, Color color)
    {
        if (Current == null || renderers == null || renderers.Length == 0) return;
        Bounds b = default;
        bool any = false;
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;
        Current.Crumbs(b, color);
        GameAudio.PlayAt(SoundId.CrumbleBody, b.center);
    }

    // ---------------- 부스러기(몸, §82 Q2) ----------------

    public const int CrumbsMin = 60, CrumbsMax = 220;
    private ParticleSystem crumbs;
    private Material crumbMaterial;

    public static int CrumbCount(Bounds b) => Mathf.Clamp(Mathf.RoundToInt(b.size.magnitude * 40f), CrumbsMin, CrumbsMax);

    private void Crumbs(Bounds b, Color color)
    {
        EnsureCrumbs();
        if (!crumbs.isPlaying) crumbs.Play(); // 멈춘 시스템은 경계가 갱신되지 않아 화면에서 잘린다(방출은 꺼져 있어 저절로 나오지 않음)
        EmitDust(b.center, Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.5f);
        float scale = Mathf.Max(1f, b.size.y / 2f); // 큰 몸(술래 약 6.5 m)은 부스러기도 크게
        Color dark = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f);
        Color light = new Color(Mathf.Min(1f, color.r * 1.15f), Mathf.Min(1f, color.g * 1.15f), Mathf.Min(1f, color.b * 1.15f), 1f);
        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        int n = CrumbCount(b);
        for (int i = 0; i < n; i++)
        {
            Vector3 at = b.center + Vector3.Scale(Random.insideUnitSphere * 0.5f, b.size);
            Vector3 out2 = Flat(at - b.center);
            out2 = out2.sqrMagnitude > 1e-4f ? out2.normalized : Random.insideUnitSphere;
            emit.position = at;
            emit.velocity = out2 * Random.Range(1.5f, 5f) * Mathf.Sqrt(scale) + Vector3.up * Random.Range(2f, 6f);
            emit.startColor = Color.Lerp(dark, light, Random.value);
            emit.startSize = Random.Range(0.08f, 0.25f) * scale;
            emit.rotation = Random.Range(0f, 360f);
            emit.startLifetime = Random.Range(2.2f, 3.2f);
            crumbs.Emit(emit, 1);
        }
    }

    private void EnsureCrumbs()
    {
        if (crumbs != null) return;
        crumbs = EscapeVisuals.CreateParticles("WitchShatterCrumbs", transform, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(2.2f, 3.2f), Speed = new Vector2(0f, 0f), Size = new Vector2(0.1f, 0.1f), Color = Color.white,
            Gravity = 1.4f, EmissionOff = true, Manual = true, MaxParticles = 3000, Shape = ParticleSystemShapeType.Sphere, Radius = 0.1f,
            FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 1f,
        });
        var col = crumbs.colorOverLifetime; // 거의 끝까지 그대로 있다가 사라진다
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var rot = crumbs.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var collision = crumbs.collision; // 바닥에 떨어져 튕기고 구른다
        collision.enabled = true; collision.type = ParticleSystemCollisionType.World; collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.25f; collision.dampen = 0.45f; collision.lifetimeLoss = 0f; collision.quality = ParticleSystemCollisionQuality.High; // Medium(복셀 캐시)은 땅을 뚫고 떨어졌다
        collision.radiusScale = 0.5f;
        var sheet = crumbs.textureSheetAnimation; // 2×2 조각 모양 중 하나
        sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
        crumbMaterial = new Material(EscapeVisuals.SoftParticleMaterial()) { mainTexture = CrumbTexture() };
        crumbs.GetComponent<ParticleSystemRenderer>().material = crumbMaterial;
    }

    // 불규칙한 부스러기 4종(2×2 칸, 128 px): 다각형 + 위쪽 밝고 가장자리 어두운 음영. 시드 고정이라 늘 같다.
    private static Texture2D CrumbTexture()
    {
        const int size = 128, cell = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "WitchCrumbs" };
        var px = new Color32[size * size];
        var rng = new System.Random(82);
        for (int c = 0; c < 4; c++)
        {
            int ox = (c % 2) * cell, oy = (c / 2) * cell;
            int corners = 6 + rng.Next(4);
            var radius = new float[corners];
            for (int k = 0; k < corners; k++) radius[k] = 0.55f + (float)rng.NextDouble() * 0.35f;
            for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                {
                    float dx = (x + 0.5f) / cell * 2f - 1f, dy = (y + 0.5f) / cell * 2f - 1f;
                    float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * corners;
                    if (ang < 0f) ang += corners;
                    int k0 = Mathf.FloorToInt(ang) % corners, k1 = (k0 + 1) % corners;
                    float edge = Mathf.Lerp(radius[k0], radius[k1], ang - Mathf.Floor(ang));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((edge - d) * cell * 0.5f);              // 1 px 부드러운 가장자리
                    float shade = Mathf.Clamp01(0.75f + 0.3f * (dy - dx) * 0.5f - 0.25f * Mathf.Clamp01((d / edge - 0.7f) / 0.3f));
                    byte v = (byte)(shade * 255f);
                    px[(oy + y) * size + ox + x] = new Color32(v, v, v, (byte)(a * 255f));
                }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private void EmitDust(Vector3 at, float scale, bool prewarmOnly = false)
    {
        if (dust == null)
            dust = EscapeVisuals.CreateParticles("WitchShatterDust", transform, new EscapeVisuals.ParticleStyle
            {
                Lifetime = new Vector2(1.2f, 2.2f), Speed = new Vector2(2f, 7f), Size = new Vector2(2f, 5f),
                Color = new Color(0.62f, 0.55f, 0.5f, 0.55f), Gravity = -0.02f, EmissionOff = true, Manual = true,
                MaxParticles = 1500, Shape = ParticleSystemShapeType.Sphere, Radius = 1f,
                FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 1f, GrowTo = 2f,
            });
        if (prewarmOnly) return;
        var emit = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
        dust.Emit(emit, Mathf.Clamp(Mathf.RoundToInt(scale * 1.5f), 4, 30));
    }

    private void OnDestroy()
    {
        if (crumbMaterial != null) { Destroy(crumbMaterial.mainTexture); Destroy(crumbMaterial); }
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
