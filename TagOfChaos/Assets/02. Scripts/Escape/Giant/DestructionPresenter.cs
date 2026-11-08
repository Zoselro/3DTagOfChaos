using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 부서지는 물체 연출(Request1009Plan.md §66.5·66.6). DestructionNet.LevelChanged를 받아 모든 화면이 각자 보여 준다(판정에는 쓰지 않음).
// - 맞은 단계(부서지기 전): 밟힌 쪽 표면에 금 간 데칼(메시 판) + 흔들림 + 먼지.
// - 소품: 0.1초 부풀었다가 숨기고, 그 재질의 조각이 튀고 먼지.
// - 텐트: 기울며 주저앉은 뒤 숨기고 조각·먼지.
// - 건물: 원본을 숨기고 Blender 조각 모델을 같은 자리에 켜서 아래부터 무너뜨린다(가벼운 자체 낙하 — 물리 엔진 안 씀).
//   4초 뒤 작은 조각은 가라앉아 사라지고 큰 잔해 몇 개만 남는다. 동시에 MaxCollapsing채까지, 멀면(FarDistance) 조각 없이 먼지만.
// - 늦게 들어온 화면(animate = false): 연출 없이 숨긴다.
public class DestructionPresenter : MonoBehaviour
{
    private const int MaxCollapsing = 3;
    private const float FarDistance = 60f;
    private const float Gravity = 18f;
    private const float SettleSeconds = 4f, SinkSeconds = 1.5f, SinkDepth = 2.5f;
    private const int RubbleKept = 8;
    private const int MaxPieces = 120;
    private const float PieceLife = 2.2f;

    private DestructionNet net;
    private Mesh quad;
    private readonly Dictionary<int, List<GameObject>> decals = new Dictionary<int, List<GameObject>>();
    private readonly Queue<DestructibleUnit> waiting = new Queue<DestructibleUnit>();
    private int collapsing;

    // 튀는 조각(소품·텐트) — 풀
    private struct Piece { public Transform T; public Rigidbody Body; public Renderer R; public float Born; public Vector3 Scale; }
    private readonly List<Piece> pieces = new List<Piece>();
    private readonly Stack<Piece> pool = new Stack<Piece>();

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

    private void OnLevel(DestructibleUnit u, int from, int to, bool animate)
    {
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
            StartCoroutine(Wobble(u, u.Grade == DestructibleGrade.Building ? 0.12f : 0.18f, 0.35f));
            Dust(u.Bounds.ClosestPoint(StompPoint(u)), Mathf.Clamp(Mathf.Max(u.Bounds.size.x, u.Bounds.size.z) * 0.15f, 1f, 3f), 14);
            return;
        }
        switch (u.Grade)
        {
            case DestructibleGrade.Prop: StartCoroutine(PropBreak(u)); break;
            case DestructibleGrade.Tent: StartCoroutine(TentBreak(u)); break;
            default:
                waiting.Enqueue(u);
                break;
        }
    }

    private void Update()
    {
        while (collapsing < MaxCollapsing && waiting.Count > 0) StartCoroutine(Collapse(waiting.Dequeue()));
        UpdatePieces();
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

    private IEnumerator PropBreak(DestructibleUnit u)
    {
        Transform t = u.Root;
        Vector3 scale = t.localScale;
        for (float e = 0f; e < 0.1f; e += Time.deltaTime)
        {
            t.localScale = scale * (1f + 0.12f * e / 0.1f);
            yield return null;
        }
        t.localScale = scale;
        Debris(u, Mathf.Clamp(Mathf.RoundToInt(u.Bounds.size.magnitude), 3, 8));
        DestructionNet.SetBroken(u, true);
        Dust(u.Center, Mathf.Clamp(u.Bounds.extents.magnitude, 0.6f, 2.5f), 10);
    }

    private IEnumerator TentBreak(DestructibleUnit u)
    {
        ClearDecals(u);
        Transform t = u.Root;
        Vector3 pos = t.position;
        Quaternion rot = t.rotation;
        Vector3 axis = Vector3.Cross(Vector3.up, Random.onUnitSphere).normalized;
        float sink = u.Bounds.size.y * 0.55f;
        GameAudio.PlayAt(SoundId.WitchShatter, u.Center);
        for (float e = 0f; e < 0.6f; e += Time.deltaTime)
        {
            float k = e / 0.6f;
            t.SetPositionAndRotation(pos - Vector3.up * sink * k * k, Quaternion.AngleAxis(14f * k, axis) * rot);
            yield return null;
        }
        Debris(u, 8);
        DestructionNet.SetBroken(u, true);
        t.SetPositionAndRotation(pos, rot); // 숨긴 뒤 제자리(남기는 부분이 있으면 그대로)
        Dust(u.Center - Vector3.up * u.Bounds.extents.y * 0.5f, Mathf.Clamp(Mathf.Max(u.Bounds.size.x, u.Bounds.size.z) * 0.5f, 2f, 5f), 26);
        Shake(u.Center, 0.25f, 35f, 0.4f);
    }

    // ---------------- 건물 붕괴 ----------------

    private sealed class Fragment
    {
        public Transform T;
        public Vector3 V, W;
        public float Delay, Half, Volume;
        public bool Resting;
    }

    private IEnumerator Collapse(DestructibleUnit u)
    {
        collapsing++;
        try
        {
            ClearDecals(u);
            Camera cam = Camera.main;
            bool far = cam == null || Vector3.Distance(cam.transform.position, u.Center) > FarDistance + u.Bounds.extents.magnitude;
            GameObject inst = null;
            if (!far && u.FracturePrefab != null)
            {
                inst = Instantiate(u.FracturePrefab, u.ModelRoot, false); // 조각 모델은 원본 노드 기준 좌표로 만들었다(크기·회전 그대로)
                inst.transform.SetParent(null, true);
                inst.name = u.Root.name + "_Collapse";
            }
            DestructionNet.SetBroken(u, true);
            RubbleGround(u);
            GameAudio.PlayAt(SoundId.WitchShatter, u.Center);
            Shake(u.Center, 0.6f, 60f, 0.8f);
            float footprint = Mathf.Max(u.Bounds.size.x, u.Bounds.size.z);
            float groundY = u.Bounds.min.y;
            Dust(new Vector3(u.Center.x, groundY + 1f, u.Center.z), Mathf.Clamp(footprint * 0.45f, 3f, 14f), 60);
            if (inst == null)
            {
                if (!far) Debris(u, 14); // 조각 모델이 없는 것(계단): 그 재질 조각만 튄다
                yield break;
            }

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
            while (Time.time - start < SettleSeconds)
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
            // 큰 잔해 몇 개만 남기고 나머지는 가라앉아 사라진다
            frags.Sort((a, b) => b.Volume.CompareTo(a.Volume));
            var sinking = frags.GetRange(Mathf.Min(RubbleKept, frags.Count), Mathf.Max(0, frags.Count - RubbleKept));
            var from = new Vector3[sinking.Count];
            for (int i = 0; i < sinking.Count; i++) from[i] = sinking[i].T != null ? sinking[i].T.position : Vector3.zero;
            for (float e = 0f; e < SinkSeconds; e += Time.deltaTime)
            {
                for (int i = 0; i < sinking.Count; i++) if (sinking[i].T != null) sinking[i].T.position = from[i] - Vector3.up * SinkDepth * (e / SinkSeconds);
                yield return null;
            }
            foreach (Fragment f in sinking) if (f.T != null) Destroy(f.T.gameObject);
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

    private void Debris(DestructibleUnit u, int count)
    {
        Material mat = null;
        foreach (Renderer r in u.Renderers) if (r != null && r.sharedMaterial != null) { mat = r.sharedMaterial; break; }
        if (mat == null) return;
        Vector3 size = u.Bounds.size;
        Vector3 origin = StompPoint(u) - Vector3.up * 3f;
        for (int i = 0; i < count && pieces.Count < MaxPieces; i++)
        {
            Piece p = pool.Count > 0 ? pool.Pop() : NewPiece();
            Vector3 s = Vector3.Max(size / Mathf.Pow(count, 1f / 3f) * Random.Range(0.35f, 0.7f), Vector3.one * 0.12f);
            s = Vector3.Min(s, Vector3.one * 1.6f);
            p.Scale = s;
            p.T.localScale = s;
            p.T.SetPositionAndRotation(u.Center + Vector3.Scale(Random.insideUnitSphere * 0.4f, size), Random.rotation);
            p.R.sharedMaterial = mat;
            p.T.gameObject.SetActive(true);
            Vector3 away = u.Center - origin; away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere;
            p.Body.linearVelocity = (away + Random.insideUnitSphere * 0.6f) * Random.Range(3f, 8f) + Vector3.up * Random.Range(3f, 7f);
            p.Body.angularVelocity = Random.insideUnitSphere * 8f;
            p.Born = Time.time;
            pieces.Add(p);
        }
    }

    private Piece NewPiece()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "DestructionPiece";
        go.layer = 2; // Ignore Raycast: 바닥에는 떨어지고 검사에는 걸리지 않는다
        go.transform.SetParent(transform, false);
        var body = go.AddComponent<Rigidbody>();
        body.mass = 0.3f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return new Piece { T = go.transform, Body = body, R = r };
    }

    private void UpdatePieces()
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            Piece p = pieces[i];
            float age = Time.time - p.Born;
            if (age < PieceLife)
            {
                if (age > PieceLife - 0.5f) p.T.localScale = p.Scale * ((PieceLife - age) / 0.5f);
                continue;
            }
            p.T.gameObject.SetActive(false);
            pool.Push(p);
            pieces.RemoveAt(i);
        }
    }

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
