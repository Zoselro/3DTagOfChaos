using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// §92-2 마녀가 부른 거대 괴물도 하늘 섬으로 오르는 경사로(다리)를 걸어서 건넌다.
// 경사로 폭(약 5 m)이 거대 괴물 몸(반지름 3 m)보다 좁아 거대 괴물 NavMesh에는 경사로가 굽히지 않는다(§89.24에서 못 걷는 곳으로 표시).
// 그래서 경사로마다 윗면 가운데 선(꺾이는 점들)을 재고, 아래 끝(본섬)과 위 끝(고원)의 거대 괴물 길을 NavMesh 연결(link)로 잇는다.
// 길 찾기가 그 연결을 쓰면(Expand) 두 끝 사이에 가운데 선 점들을 끼워 넣어 경사로를 따라 걷게 한다. 경사로 위에서 다시 길을 찾을 때는
// LeaveRamp가 가까운 끝(위·아래 중 목표까지 더 짧은 쪽)까지의 가운데 선을 앞에 붙인다.
public static class GiantRampLinks
{
    public const string GroupName = "Ramps";
    public const float Cell = 0.5f;        // 윗면을 재는 격자(m)
    public const int Erode = 4;            // 가장자리(난간·끝)에서 이 칸 수만큼 안쪽 칸만 가운데 선 후보
    public const float StepRise = 0.35f;   // 이웃 칸 높이 차이가 이보다 크면 끊긴 곳(난간 위·바깥)
    public const float SimplifyError = 0.9f; // 가운데 선을 줄일 때 허용 오차(m) — 0.5 m 격자 계단 모양을 지운다
    public const float EndReach = 8f;      // 경사로 끝에서 거대 괴물 길까지 찾는 거리
    public const float EndOut = 3f;        // 끝에서 이만큼 바깥(본섬·고원 쪽)을 찾는다
    public const float MatchRadius = 2f;   // 길의 꺾임이 연결 끝과 이만큼 가까우면 같은 점
    public const float OnRampRadius = 3f;  // 가운데 선에서 이만큼 안이면 경사로 위

    public class Ramp
    {
        public string Name;
        public Vector3[] Line;    // Bottom(본섬 길 위) → 가운데 선 → Top(고원 길 위)
        public Vector3 Bottom => Line[0];
        public Vector3 Top => Line[Line.Length - 1];
        public float Length;
        public NavMeshLinkInstance Link;
    }

    private static readonly List<Ramp> ramps = new List<Ramp>();
    public static IReadOnlyList<Ramp> Ramps => ramps;

    // 거대 괴물 NavMesh를 올린 뒤(GiantNavMesh) 부른다. 경사로가 없는 맵은 아무것도 하지 않는다.
    public static void Rebuild(Scene scene)
    {
        Clear();
        if (!GiantNavMesh.Ready || !GiantNavMesh.HasAgentType || !scene.IsValid()) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != GroupName) continue;
                for (int i = 0; i < t.childCount; i++) TryAdd(t.GetChild(i));
            }
        if (ramps.Count > 0) Debug.Log($"[GiantRamp] {ramps.Count} ramp link(s) for the giant in {scene.name}.");
    }

    public static void Clear()
    {
        foreach (Ramp r in ramps) if (NavMesh.IsLinkValid(r.Link)) NavMesh.RemoveLink(r.Link);
        ramps.Clear();
    }

    private static void TryAdd(Transform ramp)
    {
        var colliders = new List<Collider>();
        foreach (Collider c in ramp.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger && c.enabled) colliders.Add(c);
        if (colliders.Count == 0) return;
        List<Vector3> mid = CenterLine(colliders);
        if (mid.Count < 2) { Debug.LogWarning($"[GiantRamp] {ramp.name}: no centre line."); return; }
        Vector3 outBottom = mid[0] + Flat(mid[0] - mid[1]) * EndOut;
        Vector3 outTop = mid[mid.Count - 1] + Flat(mid[mid.Count - 1] - mid[mid.Count - 2]) * EndOut;
        if (!GiantNavMesh.Sample(outBottom, EndReach, out Vector3 bottom) || Mathf.Abs(bottom.y - mid[0].y) > 2f
            || !GiantNavMesh.Sample(outTop, EndReach, out Vector3 top) || Mathf.Abs(top.y - mid[mid.Count - 1].y) > 2f)
        {
            Debug.LogWarning($"[GiantRamp] {ramp.name}: an end has no giant path nearby (bottom {mid[0]}, top {mid[mid.Count - 1]}).");
            return;
        }
        var line = new List<Vector3> { bottom };
        line.AddRange(mid);
        line.Add(top);
        var r = new Ramp { Name = ramp.name, Line = line.ToArray(), Length = PathLength(line) };
        float straight = Vector3.Distance(bottom, top);
        r.Link = NavMesh.AddLink(new NavMeshLinkData
        {
            startPosition = bottom,
            endPosition = top,
            width = 0f,
            costModifier = straight > 0.1f ? Mathf.Max(1f, r.Length / straight) : 1f, // 실제 걷는 길이만큼 비싸게
            bidirectional = true,
            area = 0,
            agentTypeID = GiantNavMesh.AgentTypeId,
        });
        if (!NavMesh.IsLinkValid(r.Link)) { Debug.LogWarning($"[GiantRamp] {ramp.name}: link was not added."); return; }
        ramps.Add(r);
    }

    // 윗면 가운데 선: 위에서 격자로 쏘아 윗면 높이를 재고, 가장자리에서 Erode 칸 안쪽만 남긴 뒤
    // 가장 낮은 칸에서 이어진 칸을 따라(너비 우선) 가장 높은 칸까지의 길을 줄여서 돌려준다.
    public static List<Vector3> CenterLine(List<Collider> colliders)
    {
        Bounds b = colliders[0].bounds;
        foreach (Collider c in colliders) b.Encapsulate(c.bounds);
        int nx = Mathf.CeilToInt(b.size.x / Cell) + 1, nz = Mathf.CeilToInt(b.size.z / Cell) + 1;
        var height = new float[nx, nz];
        var has = new bool[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                var ray = new Ray(new Vector3(b.min.x + i * Cell, b.max.y + 5f, b.min.z + k * Cell), Vector3.down);
                float best = float.MinValue;
                foreach (Collider c in colliders)
                    if (c.Raycast(ray, out RaycastHit hit, b.size.y + 10f) && hit.normal.y > 0.7f && hit.point.y > best) best = hit.point.y;
                if (best > float.MinValue) { height[i, k] = best; has[i, k] = true; }
            }
        var keep = new bool[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                if (!has[i, k]) continue;
                bool ok = true;
                for (int di = -Erode; di <= Erode && ok; di++)
                    for (int dk = -Erode; dk <= Erode && ok; dk++)
                    {
                        int a = i + di, c = k + dk;
                        if (a < 0 || c < 0 || a >= nx || c >= nz || !has[a, c]) { ok = false; break; }
                        if (Mathf.Abs(height[a, c] - height[i, k]) > StepRise * Mathf.Max(Mathf.Abs(di), Mathf.Abs(dk)) + 0.05f) ok = false;
                    }
                keep[i, k] = ok;
            }
        int si = -1, sk = -1;
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
                if (keep[i, k] && (si < 0 || height[i, k] < height[si, sk])) { si = i; sk = k; }
        if (si < 0) return new List<Vector3>();
        var parent = new int[nx, nz];
        var seen = new bool[nx, nz];
        var queue = new Queue<int>();
        queue.Enqueue(si * nz + sk);
        seen[si, sk] = true;
        parent[si, sk] = -1;
        int far = si * nz + sk;
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue(), ci = cur / nz, ck = cur % nz;
            for (int di = -1; di <= 1; di++)
                for (int dk = -1; dk <= 1; dk++)
                {
                    int a = ci + di, c = ck + dk;
                    if ((di == 0 && dk == 0) || a < 0 || c < 0 || a >= nx || c >= nz || !keep[a, c] || seen[a, c]) continue;
                    if (Mathf.Abs(height[a, c] - height[ci, ck]) > StepRise) continue;
                    seen[a, c] = true;
                    parent[a, c] = cur;
                    queue.Enqueue(a * nz + c);
                    if (height[a, c] > height[far / nz, far % nz]) far = a * nz + c;
                }
        }
        var cells = new List<Vector3>();
        for (int cur = far; cur >= 0; cur = parent[cur / nz, cur % nz])
            cells.Add(new Vector3(b.min.x + (cur / nz) * Cell, height[cur / nz, cur % nz], b.min.z + (cur % nz) * Cell));
        cells.Reverse();
        return Simplify(cells, SimplifyError);
    }

    // 꺾이는 점만 남긴다(더글러스-포이커, 3차원 거리).
    public static List<Vector3> Simplify(List<Vector3> points, float error)
    {
        if (points.Count < 3) return new List<Vector3>(points);
        var keep = new bool[points.Count];
        keep[0] = keep[points.Count - 1] = true;
        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(0, points.Count - 1));
        while (stack.Count > 0)
        {
            Vector2Int s = stack.Pop();
            float worst = 0f;
            int at = -1;
            for (int i = s.x + 1; i < s.y; i++)
            {
                float d = DistanceToSegment(points[i], points[s.x], points[s.y]);
                if (d > worst) { worst = d; at = i; }
            }
            if (at < 0 || worst <= error) continue;
            keep[at] = true;
            stack.Push(new Vector2Int(s.x, at));
            stack.Push(new Vector2Int(at, s.y));
        }
        var result = new List<Vector3>();
        for (int i = 0; i < points.Count; i++) if (keep[i]) result.Add(points[i]);
        return result;
    }

    // 길(NavMesh 꺾임)에 경사로 연결이 들어 있으면 두 끝 사이에 가운데 선 점을 끼워 넣는다(내려갈 때는 거꾸로).
    public static Vector3[] Expand(Vector3[] corners) => Expand(corners, ramps);

    public static Vector3[] Expand(Vector3[] corners, IReadOnlyList<Ramp> list)
    {
        if (corners == null || corners.Length < 2 || list == null || list.Count == 0) return corners;
        var result = new List<Vector3>(corners.Length + 16);
        for (int i = 0; i < corners.Length; i++)
        {
            result.Add(corners[i]);
            if (i + 1 >= corners.Length) break;
            foreach (Ramp r in list)
            {
                bool up = Near(corners[i], r.Bottom) && Near(corners[i + 1], r.Top);
                bool down = !up && Near(corners[i], r.Top) && Near(corners[i + 1], r.Bottom);
                if (!up && !down) continue;
                int n = r.Line.Length;
                for (int j = 1; j < n - 1; j++) result.Add(up ? r.Line[j] : r.Line[n - 1 - j]);
                break;
            }
        }
        return result.ToArray();
    }

    // 경사로 위(가운데 선 OnRampRadius 안)면 true와 함께 그 경사로·가장 가까운 구간 번호(Line[seg]~Line[seg+1])를 준다.
    public static bool OnRamp(Vector3 p, out Ramp ramp, out int seg) => OnRamp(p, ramps, out ramp, out seg);

    public static bool OnRamp(Vector3 p, IReadOnlyList<Ramp> list, out Ramp ramp, out int seg)
    {
        ramp = null;
        seg = -1;
        float best = OnRampRadius;
        if (list == null) return false;
        foreach (Ramp r in list)
            for (int i = 1; i < r.Line.Length - 2; i++) // 두 끝 구간(본섬·고원 길 위)은 경사로가 아니다
            {
                float d = DistanceToSegment(p, r.Line[i], r.Line[i + 1]);
                if (d >= best) continue;
                best = d;
                ramp = r;
                seg = i;
            }
        return ramp != null;
    }

    // 경사로 위 p에서 한쪽 끝까지 가운데 선을 따라가는 점들(p 다음 점부터 끝 점까지).
    public static List<Vector3> LeadTo(Ramp r, int seg, bool toTop)
    {
        var lead = new List<Vector3>();
        if (toTop) for (int j = seg + 1; j < r.Line.Length; j++) lead.Add(r.Line[j]);
        else for (int j = seg; j >= 0; j--) lead.Add(r.Line[j]);
        return lead;
    }

    public static float PathLength(IList<Vector3> p)
    {
        float l = 0f;
        for (int i = 1; i < p.Count; i++) l += Vector3.Distance(p[i - 1], p[i]);
        return l;
    }

    private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= MatchRadius * MatchRadius;

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
    }

    public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }

#if UNITY_EDITOR
    // EditMode 시험용: 연결 없이 경사로 하나를 만든다.
    public static Ramp EditorMakeRamp(string name, Vector3[] line) => new Ramp { Name = name, Line = line, Length = PathLength(line) };
#endif
}
