using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 롤러코스터 레일 메시 → 열차가 달리는 가운데 선·위쪽·속도표(Request1009Plan.md §31, 단계 Q1). 결과는 CoasterPathSO.
// 받은 레일은 아래쪽 한 구간을 두 고리(A·B)가 같이 쓰는 θ 모양(합류 J1·분기 J2)이라, 경로를
// 공용 구간 → A 고리 → 공용 구간 → B 고리 로 번갈아 잇는다(한 바퀴에 레일 전체를 지난다).
// - 따라가기: 0.5 m씩 나아가며 진행 방향에 수직인 얇은 상자(레일 폭·두께만큼) 안 꼭짓점의 무게중심으로 가운데를 맞추고,
//   상자 안 단면의 짧은 축을 레일 면 법선(위쪽)으로 이어 간다(고리 꼭대기에서 뒤집힘). 상자가 좁아 8자 교차에서 다른 줄로 넘어가지 않는다.
// - 속도: 가장 높은 점에서 체인 속도로 출발, 에너지 보존 − 구름·공기 저항. 최저 속도(뒤집힌 곳은 더 높게) 아래면 부스터가 끌어올린다.
// - 두 번째 편성의 시간 차: 한 바퀴 동안 두 열차 사이 최소 거리가 가장 큰 값(반 바퀴는 공용 구간에서 겹친다).
public static class CoasterPathBaker
{
    public const string TrackPath = CoasterExitBuilder.TrackPath;
    public const string AssetPath = "Assets/03. SO/Maps/CUR_CoasterPath.asset";
    private const string TrainMetaPath = CoasterExitBuilder.Folder + "/coaster_train.json";
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";

    // 따라가기(레일 단면: 폭 약 1.8 m, 두께 약 0.9 m — 2026-10-21 측정)
    private const float Step = 0.5f, Half = 1.4f, Normal = 0.6f, Slab = 0.25f, TangentFollow = 0.7f, NormalFollow = 0.3f, Search = 2.2f;
    private const int MaxSteps = 1200, SmoothPasses = 30;
    private const float Spacing = 0.5f;

    // 속도(m/s)
    private const float Gravity = 9.81f, Rolling = 0.015f, Air = 0.0008f, MinSpeed = 3f, MinSpeedInverted = 8f;

    [Serializable] private class TrainMeta { public float train_length; }

    public struct Report
    {
        public bool Ok;
        public string Message;
        public float Length, LapTime, Offset, MinGap;
        public int Uncovered;
    }

    [MenuItem(MenuRoot + "Bake Coaster Path (Cursed Candy Carnival)")]
    public static void BakeMenu() => Debug.Log("[CoasterPathBaker] " + Bake().Message);

    public static Report Bake()
    {
        var report = new Report();
        Vector3[] verts = RailVertices();
        if (verts == null) { report.Message = "rail mesh missing"; return report; }
        var grid = new PointGrid(verts);

        // A: 가장 낮은 점(공용 구간)에서 +x로, 처음 자리로 돌아올 때까지
        int low = 0, right = 0;
        for (int i = 1; i < verts.Length; i++)
        {
            if (verts[i].y < verts[low].y) low = i;
            if (verts[i].x > verts[right].x) right = i;
        }
        Vector3 startA = grid.SliceCentre(verts[low], 0);
        var loopA = TraceRail(grid, startA, Vector3.right, Vector3.up, null);
        if (!loopA.Closed) { report.Message = "loop A not closed"; return report; }
        // B: 오른쪽 끝에서 위로(합류 J1까지)·아래로(분기 J2까지) — 고리 안쪽(−x)이 위쪽
        Vector3 startB = grid.SliceCentre(verts[right], 1);
        Func<Vector3, bool> onA = q => Nearest(loopA.Points, q, out _) < 0.6f;
        var up = TraceRail(grid, startB, Vector3.up, Vector3.left, onA);
        var down = TraceRail(grid, startB, Vector3.down, Vector3.left, onA);
        if (!up.Closed || !down.Closed) { report.Message = "loop B does not join loop A"; return report; }
        Nearest(loopA.Points, up.Points[up.Points.Count - 1], out int j1);
        Nearest(loopA.Points, down.Points[down.Points.Count - 1], out int j2);

        var pts = new List<Vector3>();
        var ups = new List<Vector3>();
        AppendCyclic(loopA, j1, j2, pts, ups);              // 공용(J1 → J2)
        AppendCyclic(loopA, j2, j1, pts, ups);              // A 고리(J2 → J1)
        AppendCyclic(loopA, j1, j2, pts, ups);              // 공용
        for (int i = down.Points.Count - 2; i >= 0; i--) { pts.Add(down.Points[i]); ups.Add(down.Ups[i]); } // J2 → 오른쪽 끝
        for (int i = 1; i < up.Points.Count - 1; i++) { pts.Add(up.Points[i]); ups.Add(up.Ups[i]); }       // 오른쪽 끝 → J1

        Resample(Smooth(Dedupe(pts, ups)), out Vector3[] points, out Vector3[] upv, out float[] dist, out float length);
        report.Uncovered = Uncovered(verts, points);
        float railTop = RailTop(grid, points, upv);
        Speeds(points, upv, out float[] times, out bool[] chain, out float lap);

        float trainLength = 20f;
        if (File.Exists(TrainMetaPath)) trainLength = JsonUtility.FromJson<TrainMeta>(File.ReadAllText(TrainMetaPath)).train_length;
        var asset = AssetDatabase.LoadAssetAtPath<CoasterPathSO>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<CoasterPathSO>();
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        asset.EditorSet(points, upv, dist, length, times, chain, lap, 1.83f, railTop, 0f);
        report.Offset = BestOffset(asset, trainLength, out report.MinGap);
        asset.EditorSet(points, upv, dist, length, times, chain, lap, 1.83f, railTop, report.Offset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        report.Ok = report.Uncovered == 0;
        report.Length = length;
        report.LapTime = lap;
        report.Message = $"points {points.Length}, length {length:F1} m, lap {lap:F1} s, rail top {railTop:F2} m, " +
                         $"second train +{report.Offset:F2} lap (min gap {report.MinGap:F1} m), uncovered rail vertices {report.Uncovered}";
        return report;
    }

    // ---------------- rail mesh ----------------

    // 레일 메시 꼭짓점(FBX 루트 기준 = 씬의 트랙 루트 로컬), 같은 자리는 하나로
    private static Vector3[] RailVertices()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(TrackPath);
        if (root == null) return null;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.name.Contains("Rail")) continue;
            Matrix4x4 m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var seen = new HashSet<Vector3Int>();
            var list = new List<Vector3>();
            foreach (Vector3 v in mf.sharedMesh.vertices)
            {
                Vector3 p = m.MultiplyPoint3x4(v);
                if (seen.Add(Vector3Int.RoundToInt(p * 1000f))) list.Add(p);
            }
            return list.ToArray();
        }
        return null;
    }

    private sealed class PointGrid
    {
        private readonly Vector3[] points;
        private readonly Dictionary<Vector3Int, List<int>> cells = new Dictionary<Vector3Int, List<int>>();
        private readonly List<Vector3> result = new List<Vector3>();

        public PointGrid(Vector3[] points)
        {
            this.points = points;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3Int k = Vector3Int.FloorToInt(points[i]);
                if (!cells.TryGetValue(k, out List<int> list)) cells[k] = list = new List<int>();
                list.Add(i);
            }
        }

        // 반지름 r 안의 점(돌려준 목록은 다음 호출 때 다시 쓴다)
        public List<Vector3> Near(Vector3 q, float r)
        {
            result.Clear();
            int rr = Mathf.CeilToInt(r);
            Vector3Int c = Vector3Int.FloorToInt(q);
            float r2 = r * r;
            for (int x = -rr; x <= rr; x++)
                for (int y = -rr; y <= rr; y++)
                    for (int z = -rr; z <= rr; z++)
                    {
                        if (!cells.TryGetValue(c + new Vector3Int(x, y, z), out List<int> list)) continue;
                        foreach (int i in list)
                            if ((points[i] - q).sqrMagnitude < r2) result.Add(points[i]);
                    }
            return result;
        }

        // p 둘레 2.5 m 안에서 axis 좌표가 p와 거의 같은 점들의 무게중심(단면 가운데)
        public Vector3 SliceCentre(Vector3 p, int axis)
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Vector3 v in Near(p, 2.5f))
                if (Mathf.Abs(v[axis] - p[axis]) < 0.25f) { sum += v; n++; }
            return n > 0 ? sum / n : p;
        }
    }

    // ---------------- tracing ----------------

    private sealed class TraceResult
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        public readonly List<Vector3> Ups = new List<Vector3>();
        public bool Closed;
    }

    // stop == null: 처음 자리로 돌아오면 닫힘. 아니면 stop(p)가 참이 되는 점까지(그 점 포함).
    private static TraceResult TraceRail(PointGrid grid, Vector3 p, Vector3 t, Vector3 n, Func<Vector3, bool> stop)
    {
        var trace = new TraceResult();
        Vector3 start = p;
        float travelled = 0f;
        for (int step = 0; step < MaxSteps; step++)
        {
            trace.Points.Add(p);
            trace.Ups.Add(n);
            Vector3 q = p + t * Step;
            Vector3 b = Vector3.Cross(t, n).normalized;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Vector3 v in grid.Near(q, Search))
            {
                Vector3 d = v - q;
                if (Mathf.Abs(Vector3.Dot(d, t)) < Slab && Mathf.Abs(Vector3.Dot(d, n)) < Normal && Mathf.Abs(Vector3.Dot(d, b)) < Half) { sum += v; count++; }
            }
            Vector3 next = q;
            if (count >= 8)
            {
                Vector3 corr = sum / count - q;
                next = q + corr - t * Vector3.Dot(corr, t);
            }
            t = ((1f - TangentFollow) * t + TangentFollow * (next - p).normalized).normalized;
            n = MinorAxis(grid, next, t, n);
            travelled += (next - p).magnitude;
            p = next;
            if (stop == null ? travelled > 20f && (p - start).magnitude < Step * 0.9f : travelled > 5f && stop(p))
            {
                if (stop != null) { trace.Points.Add(p); trace.Ups.Add(n); }
                trace.Closed = true;
                break;
            }
        }
        return trace;
    }

    // 진행 방향에 수직인 단면에서 꼭짓점이 가장 덜 퍼진 축 = 레일 면 법선. 이전 위쪽과 같은 쪽으로 천천히 따라간다.
    private static Vector3 MinorAxis(PointGrid grid, Vector3 c, Vector3 t, Vector3 n)
    {
        Vector3 a = Vector3.ProjectOnPlane(n, t).normalized;
        Vector3 b = Vector3.Cross(t, a);
        float sbb = 0f, sba = 0f, saa = 0f;
        int count = 0;
        foreach (Vector3 v in grid.Near(c, Search))
        {
            Vector3 d = v - c;
            float dt = Vector3.Dot(d, t), da = Vector3.Dot(d, a), db = Vector3.Dot(d, b);
            if (Mathf.Abs(dt) > 0.6f || Mathf.Abs(da) > Normal || Mathf.Abs(db) > Half) continue;
            sbb += db * db; sba += db * da; saa += da * da;
            count++;
        }
        if (count >= 8)
        {
            float major = 0.5f * Mathf.Atan2(2f * sba, sbb - saa); // b에서 a 쪽으로 잰 긴 축 각도
            Vector3 minor = (-Mathf.Sin(major) * b + Mathf.Cos(major) * a).normalized;
            if (Vector3.Dot(minor, n) < 0f) minor = -minor;
            n = ((1f - NormalFollow) * n + NormalFollow * minor).normalized;
        }
        return Vector3.ProjectOnPlane(n, t).normalized;
    }

    private static float Nearest(List<Vector3> points, Vector3 q, out int index)
    {
        float best = float.MaxValue;
        index = 0;
        for (int i = 0; i < points.Count; i++)
        {
            float d = (points[i] - q).sqrMagnitude;
            if (d < best) { best = d; index = i; }
        }
        return Mathf.Sqrt(best);
    }

    // 닫힌 고리 from → to(to 앞까지)
    private static void AppendCyclic(TraceResult loop, int from, int to, List<Vector3> pts, List<Vector3> ups)
    {
        int n = loop.Points.Count;
        for (int i = from; i != to; i = (i + 1) % n) { pts.Add(loop.Points[i]); ups.Add(loop.Ups[i]); }
    }

    // ---------------- smoothing & resampling ----------------

    private static (List<Vector3> pts, List<Vector3> ups) Dedupe(List<Vector3> pts, List<Vector3> ups)
    {
        var p = new List<Vector3> { pts[0] };
        var u = new List<Vector3> { ups[0] };
        for (int i = 1; i < pts.Count; i++)
            if ((pts[i] - p[p.Count - 1]).magnitude > 0.2f) { p.Add(pts[i]); u.Add(ups[i]); }
        return (p, u);
    }

    // 닫힌 고리 라플라스 다듬기(합류·분기 이음매의 꺾임을 없앤다)
    private static (Vector3[] pts, Vector3[] ups) Smooth((List<Vector3> pts, List<Vector3> ups) src)
    {
        Vector3[] p = src.pts.ToArray(), u = src.ups.ToArray();
        int n = p.Length;
        var np = new Vector3[n];
        var nu = new Vector3[n];
        for (int pass = 0; pass < SmoothPasses; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                int a = (i - 1 + n) % n, b = (i + 1) % n;
                np[i] = p[i] * 0.5f + (p[a] + p[b]) * 0.25f;
                nu[i] = (u[i] * 0.5f + (u[a] + u[b]) * 0.25f).normalized;
            }
            (p, np) = (np, p);
            (u, nu) = (nu, u);
        }
        return (p, u);
    }

    private static void Resample((Vector3[] pts, Vector3[] ups) src, out Vector3[] points, out Vector3[] ups, out float[] dist, out float length)
    {
        Vector3[] p = src.pts, u = src.ups;
        int n = p.Length;
        var cum = new float[n + 1];
        for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + (p[(i + 1) % n] - p[i]).magnitude;
        length = cum[n];
        int count = Mathf.RoundToInt(length / Spacing);
        points = new Vector3[count];
        ups = new Vector3[count];
        dist = new float[count];
        int seg = 0;
        for (int k = 0; k < count; k++)
        {
            float s = length * k / count;
            while (cum[seg + 1] < s) seg++;
            float f = Mathf.InverseLerp(cum[seg], cum[seg + 1], s);
            points[k] = Vector3.Lerp(p[seg], p[(seg + 1) % n], f);
            ups[k] = Vector3.Slerp(u[seg], u[(seg + 1) % n], f);
            dist[k] = s;
        }
        // 실제 점 사이 거리로 다시 재고, 위쪽을 진행 방향에 직교화
        float acc = 0f;
        for (int k = 0; k < count; k++)
        {
            dist[k] = acc;
            acc += (points[(k + 1) % count] - points[k]).magnitude;
            Vector3 tan = (points[(k + 1) % count] - points[(k - 1 + count) % count]).normalized;
            ups[k] = Vector3.ProjectOnPlane(ups[k], tan).normalized;
        }
        length = acc;
    }

    private static int Uncovered(Vector3[] verts, Vector3[] points)
    {
        var grid = new PointGrid(points);
        int far = 0;
        for (int i = 0; i < verts.Length; i += 5)
            if (grid.Near(verts[i], 1.8f).Count == 0) far++;
        return far;
    }

    // 가운데 선에서 레일 윗면까지(위쪽 방향, 단면별 최댓값의 가운데 값)
    private static float RailTop(PointGrid grid, Vector3[] points, Vector3[] ups)
    {
        var tops = new List<float>();
        int n = points.Length;
        for (int i = 0; i < n; i += 3)
        {
            Vector3 t = (points[(i + 1) % n] - points[(i - 1 + n) % n]).normalized, up = ups[i], b = Vector3.Cross(t, up);
            float top = float.MinValue;
            foreach (Vector3 v in grid.Near(points[i], 2f))
            {
                Vector3 d = v - points[i];
                if (Mathf.Abs(Vector3.Dot(d, t)) < 0.3f && Mathf.Abs(Vector3.Dot(d, up)) < 0.8f && Mathf.Abs(Vector3.Dot(d, b)) < 1.4f)
                    top = Mathf.Max(top, Vector3.Dot(d, up));
            }
            if (top > float.MinValue) tops.Add(top);
        }
        tops.Sort();
        return tops.Count > 0 ? tops[tops.Count / 2] : 0.4f;
    }

    // ---------------- speed ----------------

    private static void Speeds(Vector3[] p, Vector3[] up, out float[] times, out bool[] chain, out float lap)
    {
        int n = p.Length;
        int top = 0;
        for (int i = 1; i < n; i++) if (p[i].y > p[top].y) top = i;
        var v = new float[n];
        chain = new bool[n];
        float speed = MinSpeed;
        int k = top;
        for (int step = 0; step < 2 * n; step++) // 두 바퀴: 첫 바퀴로 시작 속도를 맞춘다
        {
            int j = (k + 1) % n;
            float seg = (p[j] - p[k]).magnitude;
            float v2 = speed * speed + 2f * Gravity * (p[k].y - p[j].y) - 2f * (Rolling * Gravity + Air * speed * speed) * seg;
            float next = Mathf.Sqrt(Mathf.Max(v2, 0f));
            float need = up[j].y < -0.3f ? MinSpeedInverted : MinSpeed;
            bool boosted = next < need;
            if (boosted) next = need;
            if (step >= n) { v[j] = next; chain[j] = boosted; }
            speed = next;
            k = j;
        }
        times = new float[n];
        float t = 0f;
        for (int i = 0; i < n; i++)
        {
            times[i] = t;
            int j = (i + 1) % n;
            t += (p[j] - p[i]).magnitude / Mathf.Max(0.5f * (v[i] + v[j]), 0.1f);
        }
        lap = t;
    }

    // 두 번째 편성의 시간 차(한 바퀴 비율): 두 열차(머리 ~ 꼬리 13점) 사이 최소 거리가 가장 큰 값
    private static float BestOffset(CoasterPathSO path, float trainLength, out float bestGap)
    {
        float best = 0.25f;
        bestGap = -1f;
        const int Samples = 240, Body = 13;
        var a = new Vector3[Body];
        var b = new Vector3[Body];
        for (float f = 0.05f; f <= 0.951f; f += 0.01f)
        {
            float gap = float.MaxValue;
            for (int s = 0; s < Samples && gap > bestGap; s++)
            {
                double t = path.LapTime * s / Samples;
                Body13(path, path.DistanceAtTime(t), trainLength, a);
                Body13(path, path.DistanceAtTime(t + f * path.LapTime), trainLength, b);
                for (int i = 0; i < Body; i++)
                    for (int j = 0; j < Body; j++)
                        gap = Mathf.Min(gap, (a[i] - b[j]).magnitude);
            }
            if (gap > bestGap) { bestGap = gap; best = f; }
        }
        return best;
    }

    private static void Body13(CoasterPathSO path, float head, float trainLength, Vector3[] into)
    {
        for (int i = 0; i < into.Length; i++)
        {
            path.Sample(head - trainLength * i / (into.Length - 1), out Vector3 pos, out _, out _);
            into[i] = pos;
        }
    }
}
