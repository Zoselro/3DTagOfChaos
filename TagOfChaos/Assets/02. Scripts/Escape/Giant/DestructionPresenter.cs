using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 부서지는 물체 연출(Request1009Plan.md §66.5·66.6). DestructionNet.LevelChanged를 받아 모든 화면이 각자 보여 준다(판정에는 쓰지 않음).
// - 맞은 단계(부서지기 전): 밟힌 쪽 표면에 금 간 데칼(메시 판) + 흔들림 + 먼지. 무너지기 한 번 전에는 조각 모델에서
//   맞은 자리의 작은 조각 2~3개가 떨어져 나간다(ChipPieces, §82 — 명세서 2단계 "큰 균열 + 소량 파편").
// - 부서짐(§82): 모든 물체(소품·텐트·건물·계단·술래방)가 Blender로 미리 조각낸 모델 + 미리 구운 무너지는 애니메이션(FracturePlayer)으로
//   원본 자리에서 무너진다. 예전의 큐브 조각은 없앴다. 소리는 재질 묶음별(나무·금속 껍데기·사탕/돌·텐트·건물 붕괴).
//   - 소품·텐트: 바깥으로 터지는 애니메이션 → 잠시 뒤 작아지며 사라짐(같은 종류는 조각 인스턴스를 다시 씀).
//   - 건물·계단·술래방: 맞은 방향(거대 괴물 → 물체) 애니메이션 → 작은 조각은 제자리에서 투명해지며 사라지고(§74 P18) 큰 잔해 몇 개만 남는다.
//     구운 애니메이션이 없는 조각 모델은 예전처럼 가벼운 자체 낙하. 동시에 MaxCollapsing채까지.
//   - 멀면(FarDistance) 또는 조각 모델이 없으면 조각 없이 먼지만(큐브 없음).
// - 늦게 들어온 화면(animate = false): 연출 없이 숨긴다.
public class DestructionPresenter : MonoBehaviour
{
    private const int MaxCollapsing = 3;
    private const float FarDistance = 60f;
    private const float Gravity = 18f;
    private const float SettleSeconds = 4f, FadeSeconds = 1.5f, FadeEndScale = 0.7f;
    private const int RubbleKept = 8;
    private const float PropHoldSeconds = 2.5f, TentHoldSeconds = 4f; // 소품·텐트 조각이 땅에 남아 있는 시간(그 뒤 작아지며 사라짐)
    private const int ChipCount = 3;
    private const float ModelWaitSeconds = 3f; // 무너질 때 조각 모델을 아직 읽는 중이면 이만큼 기다린다(§73.11)

    private DestructionNet net;
    private Mesh quad;
    private readonly Dictionary<int, List<GameObject>> decals = new Dictionary<int, List<GameObject>>();
    private readonly Queue<DestructibleUnit> waiting = new Queue<DestructibleUnit>();
    private int collapsing;

    private void Start()
    {
        net = GetComponent<DestructionNet>();
        if (net != null) net.LevelChanged += OnLevel;
    }

    private void OnDestroy()
    {
        if (net != null) net.LevelChanged -= OnLevel;
        if (quad != null) Destroy(quad);
    }

    // 조각 모델(§73.11): 물체가 처음 맞으면 그 물체 것만 비동기로 읽기 시작한다(건물은 4번 맞아야 무너지므로 시간이 충분하다.
    // 소품은 한 번에 부서지므로 읽는 몇 프레임 동안 원본이 그대로 서 있다가 무너진다).
    public static FracturedModelSO ResolveSet(DestructibleUnit u, out bool loading)
    {
        loading = false;
        if (u.FractureSet != null || string.IsNullOrEmpty(u.FractureName)) return u.FractureSet;
        FracturedModels.Request(u.FractureName);
        u.FractureSet = FracturedModels.GetSet(u.FractureName, out loading);
        return u.FractureSet;
    }

    private IEnumerator WaitSet(DestructibleUnit u)
    {
        float waitUntil = Time.time + ModelWaitSeconds;
        while (ResolveSet(u, out bool loading) == null && loading && Time.time < waitUntil) yield return null;
    }

    // 미는 방향(세계, 수평): 거대 괴물이 밟은 자리 → 물체. 모르면 0(첫 애니메이션).
    public static Vector3 PushDirection(DestructibleUnit u)
    {
        if (!GiantState.TryGetCurrent(out GiantState s) || s.StompAt <= 0) return Vector3.zero;
        Vector3 d = u.Center - s.StompCenter;
        d.y = 0f;
        return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.zero;
    }

    // 재질 묶음별 부서지는 소리(§82 Q3)
    public static SoundId SoundFor(FracturedModelSO set, DestructibleGrade grade)
    {
        if (set == null) return grade == DestructibleGrade.Building || grade == DestructibleGrade.Stairs ? SoundId.CollapseBuilding : SoundId.BreakCandy;
        switch (set.Style)
        {
            case FractureStyle.Wood: return SoundId.BreakWood;
            case FractureStyle.Shell: return SoundId.BreakMetal;
            case FractureStyle.Panel: return SoundId.BreakTent;
            case FractureStyle.Room: case FractureStyle.Building: return SoundId.CollapseBuilding;
            default: return grade == DestructibleGrade.Stairs ? SoundId.CollapseBuilding : SoundId.BreakCandy;
        }
    }

    private static bool IsFar(DestructibleUnit u)
    {
        Camera cam = Camera.main;
        return cam == null || Vector3.Distance(cam.transform.position, u.Center) > FarDistance + u.Bounds.extents.magnitude;
    }

    private void OnLevel(DestructibleUnit u, int from, int to, bool animate)
    {
        ResolveSet(u, out _);
        if (!animate)
        {
            ClearDecals(u);
            if (u.Broken)
            {
                DestructionNet.SetBroken(u, true);
                if (u.Grade == DestructibleGrade.Building) RubbleGround(u);
            }
            return;
        }
        if (!u.Broken)
        {
            AddDecal(u, to);
            if (to == u.HitsToBreak - 1 && u.Grade != DestructibleGrade.Tent) StartCoroutine(ChipPieces(u));
            StartCoroutine(Wobble(u, u.Grade == DestructibleGrade.Building ? 0.12f : 0.18f, 0.35f));
            Dust(u.Bounds.ClosestPoint(StompPoint(u)), Mathf.Clamp(Mathf.Max(u.Bounds.size.x, u.Bounds.size.z) * 0.15f, 1f, 3f), 14);
            return;
        }
        switch (u.Grade)
        {
            case DestructibleGrade.Prop: StartCoroutine(SmallBreak(u, PropHoldSeconds, 0f)); break;
            case DestructibleGrade.Tent: StartCoroutine(SmallBreak(u, TentHoldSeconds, 0.25f)); break;
            default:
                waiting.Enqueue(u);
                break;
        }
    }

    // 부서지는 목록 밖 물체(술래방, §81 R)를 건물처럼 무너뜨린다. 늦게 들어온 화면(animate false)은 숨기기만.
    public void PresentExtra(DestructibleUnit u, bool animate)
    {
        if (u == null) return;
        if (!animate)
        {
            DestructionNet.SetBroken(u, true);
            RubbleGround(u);
            return;
        }
        waiting.Enqueue(u);
    }

    private void Update()
    {
        while (collapsing < MaxCollapsing && waiting.Count > 0) StartCoroutine(Collapse(waiting.Dequeue()));
    }

    // ---------------- 데칼 ----------------

    // 밟힌 쪽: 지금 밟기 자리(거대 괴물 방 속성), 없으면 물체 가운데 앞.
    private static Vector3 StompPoint(DestructibleUnit u)
    {
        if (GiantState.TryGetCurrent(out GiantState s) && s.StompAt > 0) return s.StompCenter + Vector3.up * 3f;
        return u.Center + Vector3.forward * u.Bounds.extents.magnitude;
    }

    private void AddDecal(DestructibleUnit u, int level)
    {
        int stage = u.Grade == DestructibleGrade.Tent ? 2 : Mathf.Clamp(Mathf.CeilToInt(3f * level / u.HitsToBreak), 1, 3); // 계단(10번)도 금 3단계로
        Material mat = DestructionRulesSO.Current.CrackMaterial(stage);
        if (mat == null) return;
        Vector3 from = StompPoint(u);
        Vector3 target = u.Center + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.15f, 0.25f), Random.Range(-0.2f, 0.2f)) * u.Bounds.size.magnitude * 0.3f;
        if (!SurfaceHit(u, from, target, out RaycastHit hit)) return;

        float footprint = Mathf.Max(u.Bounds.size.x, u.Bounds.size.z);
        float size = u.Grade == DestructibleGrade.Building ? Mathf.Clamp(footprint * 0.3f, 4f, 10f) * (0.8f + 0.25f * level) : Mathf.Clamp(footprint * 0.5f, 2.5f, 5f);
        if (quad == null) quad = MakeQuad();
        var go = new GameObject("CrackDecal");
        go.transform.SetParent(u.Root, true);
        go.transform.SetPositionAndRotation(hit.point + hit.normal * 0.05f, Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        go.transform.localScale = Vector3.one; // 부모 크기 영향 없이
        go.transform.localScale = Divide(Vector3.one * size, go.transform.lossyScale);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!decals.TryGetValue(u.Id, out List<GameObject> list)) decals[u.Id] = list = new List<GameObject>();
        list.Add(go);
    }

    // 물체 충돌체 중 밟힌 쪽에서 가운데로 쏜 선이 처음 맞는 면. 충돌체가 없으면 경계 상자 면.
    private static bool SurfaceHit(DestructibleUnit u, Vector3 from, Vector3 target, out RaycastHit best)
    {
        best = default;
        Vector3 dir = target - from;
        float len = dir.magnitude;
        if (len < 0.01f) return false;
        var ray = new Ray(from, dir / len);
        bool any = false;
        foreach (Collider c in u.Colliders)
        {
            if (c == null || !c.enabled) continue;
            if (c.Raycast(ray, out RaycastHit h, len + 5f) && (!any || h.distance < best.distance)) { best = h; any = true; }
        }
        if (any) return true;
        if (!u.Bounds.IntersectRay(ray, out float d)) return false;
        Vector3 p = ray.GetPoint(d);
        Vector3 n = p - u.Center;
        Vector3 e = u.Bounds.extents;
        Vector3 a = new Vector3(Mathf.Abs(n.x) / Mathf.Max(e.x, 0.01f), Mathf.Abs(n.y) / Mathf.Max(e.y, 0.01f), Mathf.Abs(n.z) / Mathf.Max(e.z, 0.01f));
        Vector3 normal = a.x >= a.y && a.x >= a.z ? new Vector3(Mathf.Sign(n.x), 0f, 0f) : a.z >= a.y ? new Vector3(0f, 0f, Mathf.Sign(n.z)) : new Vector3(0f, Mathf.Sign(n.y), 0f);
        best.point = p;
        best.normal = normal;
        return true;
    }

    private void ClearDecals(DestructibleUnit u)
    {
        if (!decals.TryGetValue(u.Id, out List<GameObject> list)) return;
        foreach (GameObject g in list) if (g != null) Destroy(g);
        decals.Remove(u.Id);
    }

    private static Mesh MakeQuad()
    {
        var m = new Mesh { name = "CrackDecalQuad" };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.tangents = new[] { new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f) };
        m.RecalculateBounds();
        return m;
    }

    private static Vector3 Divide(Vector3 a, Vector3 b) =>
        new Vector3(a.x / Mathf.Max(1e-4f, b.x), a.y / Mathf.Max(1e-4f, b.y), a.z / Mathf.Max(1e-4f, b.z));

    // ---------------- 흔들림·소품·텐트 ----------------

    private IEnumerator Wobble(DestructibleUnit u, float amplitude, float seconds)
    {
        Transform t = u.ModelRoot;
        if (t == null) yield break;
        Vector3 rest = t.localPosition;
        for (float e = 0f; e < seconds && !u.Broken; e += Time.deltaTime)
        {
            float k = 1f - e / seconds;
            t.localPosition = rest + new Vector3(Mathf.Sin(e * 71f), 0f, Mathf.Cos(e * 59f)) * amplitude * k;
            yield return null;
        }
        if (t != null) t.localPosition = rest;
    }

    // 소품·텐트(§82): 원본을 숨기고 그 자리에서 조각 모델이 바깥으로 터진다 → hold초 뒤 작아지며 사라짐.
    private IEnumerator SmallBreak(DestructibleUnit u, float hold, float shake)
    {
        yield return WaitSet(u);
        ClearDecals(u);
        FracturedModelSO set = u.FractureSet;
        DestructionNet.SetBroken(u, true);
        GameAudio.PlayAt(SoundFor(set, u.Grade), u.Center);
        float wide = Mathf.Max(u.Bounds.size.x, u.Bounds.size.z);
        Dust(u.Center - Vector3.up * u.Bounds.extents.y * 0.4f, Mathf.Clamp(wide * 0.5f, 0.6f, 5f), u.Grade == DestructibleGrade.Tent ? 26 : 10);
        if (shake > 0f) Shake(u.Center, shake, 35f, 0.4f);
        if (set != null && !IsFar(u)) FracturePlayer.Play(set, u.ModelRoot, PushDirection(u), hold, true);
    }

    // 무너지기 한 번 전(§82): 조각 모델에서 맞은 자리에 가까운 작은 조각 몇 개가 떨어져 나간다(원본은 그대로 — 겉면에서 떨어지는 것처럼 보인다).
    private IEnumerator ChipPieces(DestructibleUnit u)
    {
        FracturedModelSO set = ResolveSet(u, out _);
        if (set == null || set.Model == null || IsFar(u)) yield break;
        var wrap = new GameObject(u.Root.name + "_Chips");
        wrap.transform.SetPositionAndRotation(u.ModelRoot.position, u.ModelRoot.rotation);
        wrap.transform.localScale = u.ModelRoot.lossyScale;
        GameObject inst = Instantiate(set.Model, wrap.transform, false);
        Vector3 at = u.Bounds.ClosestPoint(StompPoint(u));
        var all = new List<Renderer>(inst.GetComponentsInChildren<Renderer>());
        all.Sort((a, b) => (a.bounds.center - at).sqrMagnitude.CompareTo((b.bounds.center - at).sqrMagnitude));
        var near = all.GetRange(0, Mathf.Min(all.Count, ChipCount * 4));
        near.Sort((a, b) => a.bounds.size.sqrMagnitude.CompareTo(b.bounds.size.sqrMagnitude)); // 가까운 것 중 작은 것
        var chips = new List<Fragment>();
        for (int i = 0; i < near.Count && chips.Count < ChipCount; i++)
        {
            Bounds b = near[i].bounds;
            Vector3 out2 = new Vector3(b.center.x - u.Center.x, 0f, b.center.z - u.Center.z);
            out2 = out2.sqrMagnitude > 0.01f ? out2.normalized : Random.insideUnitSphere;
            near[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            chips.Add(new Fragment { T = near[i].transform, V = out2 * Random.Range(1f, 2.5f) + Vector3.up * Random.Range(0.5f, 1.5f), W = Random.insideUnitSphere * 60f, Half = Mathf.Max(0.1f, Mathf.Min(b.extents.x, b.extents.y, b.extents.z)) });
        }
        foreach (Renderer r in all)
            if (!chips.Exists(f => f.T == r.transform)) Destroy(r.gameObject);
        float groundY = u.Bounds.min.y;
        for (float e = 0f; e < 1.8f; e += Time.deltaTime)
        {
            float dt = Time.deltaTime, k = e < 1.4f ? 1f : 1f - (e - 1.4f) / 0.4f;
            foreach (Fragment f in chips)
            {
                if (f.T == null) continue;
                if (!f.Resting)
                {
                    f.V.y -= Gravity * dt;
                    f.T.position += f.V * dt;
                    f.T.Rotate(f.W * dt, Space.World);
                    if (f.T.position.y < groundY + f.Half) { Vector3 p = f.T.position; p.y = groundY + f.Half; f.T.position = p; f.Resting = true; }
                }
                if (k < 1f) f.T.localScale *= Mathf.Max(0f, 1f - dt / 0.4f);
            }
            yield return null;
        }
        Destroy(wrap);
    }

    // ---------------- 건물 붕괴 ----------------

    private sealed class Fragment
    {
        public Transform T;
        public Vector3 V, W;
        public float Delay, Half, Volume;
        public bool Resting;
    }

    // 사라지는 조각의 재질(§74 P18): 원본 재질마다 Standard Fade 복사본을 하나씩 만들어 이 붕괴의 조각들이 함께 쓴다
    // (조각마다 복사하지 않아 재질 수가 원본 재질 수만큼만 는다). 끝나면 복사본을 지운다. Standard가 아닌 재질은 그대로 두고 AllFade = false.
    private sealed class FragmentFader
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        public bool AllFade { get; private set; } = true;

        public void Add(Transform root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material fade = FadeCopy(mats[i]);
                    if (fade != null) mats[i] = fade; else AllFade = false;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 투명해지는 동안 그림자가 남지 않게
            }
        }

        private Material FadeCopy(Material src)
        {
            if (src == null || src.shader == null || src.shader.name != "Standard" || !src.HasProperty(ColorId)) return null;
            if (copies.TryGetValue(src, out Material copy)) return copy;
            copy = new Material(src) { name = src.name + "_Fade" };
            copy.SetFloat("_Mode", 2f);
            copy.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            copy.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            copy.SetInt("_ZWrite", 0);
            copy.DisableKeyword("_ALPHATEST_ON");
            copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            copy.EnableKeyword("_ALPHABLEND_ON");
            copy.EnableKeyword("_NORMALMAP"); // 금 간 데칼(M_GiantCrack)과 같은 변형 — 빌드에 들어 있는 조합을 쓴다
            copy.renderQueue = 3000;
            copies[src] = copy;
            return copy;
        }

        public void SetAlpha(float alpha)
        {
            foreach (KeyValuePair<Material, Material> pair in copies)
            {
                Color c = pair.Key.GetColor(ColorId);
                c.a *= Mathf.Clamp01(alpha);
                pair.Value.SetColor(ColorId, c);
            }
        }

        public void Dispose()
        {
            foreach (Material m in copies.Values) if (m != null) Destroy(m);
            copies.Clear();
        }
    }

    private IEnumerator Collapse(DestructibleUnit u)
    {
        collapsing++;
        try
        {
            yield return WaitSet(u);
            ClearDecals(u);
            bool far = IsFar(u);
            FracturedModelSO set = u.FractureSet;
            GameObject inst = null;
            FracturePlayer player = null;
            if (!far && set != null && set.ClipCount > 0)
            {
                player = FracturePlayer.Play(set, u.ModelRoot, PushDirection(u), -1f, false); // 구운 애니메이션(§82)
                inst = player != null ? player.gameObject : null;
                if (inst != null) inst.name = u.Root.name + "_Collapse";
            }
            else if (!far && u.FracturePrefab != null)
            {
                inst = Instantiate(u.FracturePrefab, u.ModelRoot, false); // 조각 모델은 원본 노드 기준 좌표로 만들었다(크기·회전 그대로)
                inst.transform.SetParent(null, true);
                inst.name = u.Root.name + "_Collapse";
            }
            DestructionNet.SetBroken(u, true);
            RubbleGround(u);
            GameAudio.PlayAt(SoundFor(set, u.Grade), u.Center);
            Shake(u.Center, 0.6f, 60f, 0.8f);
            float footprint = Mathf.Max(u.Bounds.size.x, u.Bounds.size.z);
            float groundY = u.Bounds.min.y;
            Dust(new Vector3(u.Center.x, groundY + 1f, u.Center.z), Mathf.Clamp(footprint * 0.45f, 3f, 14f), 60);
            if (inst == null) yield break; // 멀거나 조각 모델이 없으면 먼지만(큐브 없음)

            var frags = new List<Fragment>();
            float height = Mathf.Max(1f, u.Bounds.size.y);
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Bounds b = r.bounds;
                Vector3 out2 = new Vector3(b.center.x - u.Center.x, 0f, b.center.z - u.Center.z);
                out2 = out2.sqrMagnitude > 0.01f ? out2.normalized : Random.insideUnitSphere;
                frags.Add(new Fragment
                {
                    T = r.transform,
                    V = out2 * Random.Range(0.5f, 3f) + Vector3.up * Random.Range(0f, 1.5f),
                    W = Random.insideUnitSphere * Random.Range(20f, 90f),
                    Delay = Mathf.Clamp01((b.center.y - groundY) / height) * 0.5f + Random.Range(0f, 0.1f), // 아래부터 풀린다
                    Half = Mathf.Max(0.2f, Mathf.Min(b.extents.x, b.extents.y, b.extents.z)),
                    Volume = b.size.x * b.size.y * b.size.z,
                });
            }
            float start = Time.time;
            if (player != null) // 구운 애니메이션이 끝날 때까지(자체 낙하 없음)
                while (player != null && !player.Finished && Time.time - start < player.Length + 1f) yield return null;
            while (player == null && Time.time - start < SettleSeconds)
            {
                float now = Time.time - start, dt = Time.deltaTime;
                foreach (Fragment f in frags)
                {
                    if (f.Resting || now < f.Delay || f.T == null) continue;
                    f.V.y -= Gravity * dt;
                    f.T.position += f.V * dt;
                    f.T.Rotate(f.W * dt, Space.World);
                    float floor = groundY + f.Half;
                    if (f.T.position.y < floor)
                    {
                        Vector3 p = f.T.position; p.y = floor; f.T.position = p;
                        f.V = new Vector3(f.V.x * 0.55f, -f.V.y * 0.25f, f.V.z * 0.55f);
                        f.W *= 0.5f;
                        if (f.V.sqrMagnitude < 0.6f) f.Resting = true;
                    }
                }
                yield return null;
            }
            // 큰 잔해 몇 개만 남기고 나머지는 제자리에서 투명해지며 사라진다(§74 P18 — 예전에는 땅속으로 가라앉았다)
            frags.Sort((a, b) => b.Volume.CompareTo(a.Volume));
            var fading = frags.GetRange(Mathf.Min(RubbleKept, frags.Count), Mathf.Max(0, frags.Count - RubbleKept));
            var fader = new FragmentFader();
            var scales = new Vector3[fading.Count];
            for (int i = 0; i < fading.Count; i++)
            {
                if (fading[i].T == null) continue;
                scales[i] = fading[i].T.localScale;
                fader.Add(fading[i].T);
            }
            try
            {
                for (float e = 0f; e < FadeSeconds; e += Time.deltaTime)
                {
                    float k = e / FadeSeconds;
                    fader.SetAlpha(1f - k);
                    // 투명하게 못 그리는 재질(특수 셰이더)은 크기로 대신 사라지고, 투명해지는 재질도 살짝 줄어든다
                    float scale = fader.AllFade ? Mathf.Lerp(1f, FadeEndScale, k) : 1f - k;
                    for (int i = 0; i < fading.Count; i++) if (fading[i].T != null) fading[i].T.localScale = scales[i] * scale;
                    yield return null;
                }
            }
            finally
            {
                foreach (Fragment f in fading) if (f.T != null) Destroy(f.T.gameObject);
                fader.Dispose();
            }
        }
        finally
        {
            collapsing--;
        }
    }

    // 무너진 자리 바닥: 건물 바닥까지 숨겨 맵 바탕이 드러나므로, 조각 단면 재질(흙빛)의 들쭉날쭉한 판을 깐다.
    private void RubbleGround(DestructibleUnit u)
    {
        Material mat = InnerMaterial(u);
        if (mat == null) return;
        float radius = Mathf.Max(u.Bounds.size.x, u.Bounds.size.z) * 0.42f;
        Vector3 c = new Vector3(u.Center.x, u.Bounds.min.y, u.Center.z);
        float y = u.Bounds.min.y;
        foreach (RaycastHit h in Physics.RaycastAll(c + Vector3.up * 3f, Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (h.collider.GetComponentInParent<IGameCharacter>() == null) y = Mathf.Max(y, h.point.y);
        var go = new GameObject("RubbleGround");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(new Vector3(c.x, y + 0.04f, c.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        go.AddComponent<MeshFilter>().sharedMesh = Disc(radius, u.Id);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Material InnerMaterial(DestructibleUnit u)
    {
        if (u.FracturePrefab == null) return null;
        foreach (Renderer r in u.FracturePrefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
                if (m != null && m.name.StartsWith("M_Fracture_Inner")) return m;
        return null;
    }

    private static Mesh Disc(float radius, int seed)
    {
        const int Segments = 32;
        var rng = new System.Random(seed);
        var v = new Vector3[Segments + 1];
        var uv = new Vector2[Segments + 1];
        var tris = new int[Segments * 3];
        uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            float r = radius * (0.8f + 0.3f * (float)rng.NextDouble());
            v[i + 1] = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
            uv[i + 1] = new Vector2(0.5f + v[i + 1].x / (radius * 2f), 0.5f + v[i + 1].z / (radius * 2f));
            tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = (i + 1) % Segments + 1;
        }
        var m = new Mesh { name = "RubbleGround", vertices = v, uv = uv, triangles = tris };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ---------------- 조각·먼지·흔들림 ----------------

    private static void Dust(Vector3 at, float radius, int count)
    {
        ParticleSystem dust = EscapeVisuals.CreateParticles("DestructionDust", null, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(1.2f, 2f), Speed = new Vector2(1.5f, 4.5f), Size = new Vector2(radius * 0.5f, radius * 0.9f),
            Color = new Color(0.72f, 0.62f, 0.52f, 0.75f), MaxParticles = count + 4, EmissionOff = true,
            Shape = ParticleSystemShapeType.Circle, Radius = radius, ShapeRotation = new Vector3(90f, 0f, 0f),
            GrowTo = 2f, FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 0.7f, Once = true,
        });
        dust.transform.position = at;
        dust.Emit(count);
        Destroy(dust.gameObject, 3f);
    }

    private static void Shake(Vector3 at, float near, float range, float seconds)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float k = 1f - Mathf.Clamp01(Vector3.Distance(cam.transform.position, at) / range);
        if (k > 0f) CameraShake.Add(near * k, seconds);
    }
}
