using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 볼록(convex) 충돌체가 실제 모양보다 넓게 막는 곳 찾기(Request1009Plan.md §10). 볼록 껍질은 차양·아치·텐트 처마 아래처럼
// 쿠키가 서서 지나갈 빈 곳까지 메운다. 충돌체 경계 안을 격자 기둥으로 훑어, 실제 메시로는 비어 있고(바닥 위로 쿠키 키만큼 아무 면이 없고
// 그 점에서 어느 한 방향으로는 메시에 닿지 않고 빠져나감 = 닫힌 덩어리 속이 아님) 껍질 안에 드는 칸을 센다.
// 움직이는 물체(Animator·Rigidbody 아래 — 회전목마·관람차)는 오목으로 바꿀 수 없으므로 보지 않는다.
// 오목으로 바꾸는 것은 검토한 목록(FixPrefixes)만이다 — 나무·집도 걸리지만(가지 밑·문 안쪽) 바꾸면 괴물이 못 들어가는 숨을 곳이 생긴다.
public static class ConvexGapScanner
{
    // 놀이공원(2026-10-09 요청): 네온 부스는 차양이 카운터보다 넓어 껍질이 옆 통로까지 비스듬히 막고, 풍선 다발은 줄 아래 빈 곳을 막는다.
    public static readonly string[] FixPrefixes = { "CUR_Booth_Neon_", "CUR_BalloonCluster" };

    public static bool IsListed(string name)
    {
        foreach (string p in FixPrefixes) if (name.StartsWith(p)) return true;
        return false;
    }

    public const float CookieClearance = 2f;  // 쿠키 키(약 1.6 m) + 여유
    public const float Step = 0.75f;
    public const float MinGapArea = 1.5f;     // 이보다 넓은 빈 곳이 막힐 때만(m²)
    private const float RayMargin = 30f;

    private static readonly Vector3[] EscapeDirs =
    {
        Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
        new Vector3(1f, 0f, 1f).normalized, new Vector3(-1f, 0f, 1f).normalized, new Vector3(1f, 0f, -1f).normalized, new Vector3(-1f, 0f, -1f).normalized,
    };

    public static bool IsStaticCandidate(MeshCollider mc) =>
        mc != null && mc.convex && mc.enabled && !mc.isTrigger && mc.sharedMesh != null
        && mc.GetComponentInParent<Animator>() == null && mc.attachedRigidbody == null;

    // 껍질 안의 빈 기둥 면적(m²).
    public static float GapArea(MeshCollider convex)
    {
        Bounds b = convex.bounds;
        if (b.size.y < CookieClearance) return 0f;

        var probeGo = new GameObject("__ConvexGapProbe");
        probeGo.transform.SetParent(convex.transform, false);
        var probe = probeGo.AddComponent<MeshCollider>();
        probe.sharedMesh = convex.sharedMesh;
        probe.convex = false;
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        convex.enabled = false;
        Physics.SyncTransforms();
        int columns = 0;
        try
        {
            var meshY = new List<float>();
            for (float x = b.min.x + Step * 0.5f; x < b.max.x; x += Step)
                for (float z = b.min.z + Step * 0.5f; z < b.max.z; z += Step)
                {
                    Vector3 top = new Vector3(x, b.max.y + 0.5f, z);
                    RaycastHit[] hits = Physics.RaycastAll(top, Vector3.down, b.size.y + RayMargin, ~0, QueryTriggerInteraction.Ignore);
                    meshY.Clear();
                    float floor = float.NegativeInfinity;
                    foreach (RaycastHit h in hits)
                    {
                        if (h.collider == probe) meshY.Add(h.point.y);
                        else if (h.normal.y > 0.5f && h.point.y < b.max.y && h.point.y > floor) floor = h.point.y;
                    }
                    if (float.IsNegativeInfinity(floor)) floor = b.min.y;
                    meshY.Add(floor);
                    meshY.Sort((p, q) => q.CompareTo(p));
                    if (HasGap(meshY, convex, probe, x, z)) columns++;
                }
        }
        finally
        {
            convex.enabled = true;
            Physics.queriesHitBackfaces = backfaces;
            Object.DestroyImmediate(probeGo);
            Physics.SyncTransforms();
        }
        return columns * Step * Step;
    }

    private static bool HasGap(List<float> ys, MeshCollider convex, MeshCollider probe, float x, float z)
    {
        for (int k = 0; k + 1 < ys.Count; k++)
        {
            float upper = ys[k], lower = ys[k + 1];
            if (upper - lower < CookieClearance) continue;
            var p = new Vector3(x, lower + 1f, z);
            if (!InsideHull(convex, p)) continue;
            if (!InsideSolid(probe, p)) return true;
        }
        return false;
    }

    private static bool InsideHull(MeshCollider convex, Vector3 p)
    {
        convex.enabled = true;
        Vector3 c = Physics.ClosestPoint(p, convex, convex.transform.position, convex.transform.rotation);
        convex.enabled = false;
        return (c - p).sqrMagnitude < 1e-6f;
    }

    // 닫힌 덩어리 속이면 모든 방향 광선이 메시에 닿는다. 한 방향이라도 빠져나가면 열린 빈 곳이다.
    private static bool InsideSolid(MeshCollider probe, Vector3 p)
    {
        foreach (Vector3 d in EscapeDirs)
            if (!probe.Raycast(new Ray(p, d), out _, 200f)) return false;
        return true;
    }

    public struct Finding { public string Map, Name; public float Area; }

    public static List<Finding> Scan(string map)
    {
        var list = new List<Finding>();
        foreach (MeshCollider mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
        {
            if (!IsStaticCandidate(mc)) continue;
            float area = GapArea(mc);
            if (area >= MinGapArea) list.Add(new Finding { Map = map, Name = mc.name, Area = area });
        }
        return list;
    }

    [MenuItem("Tools/TagOfChaos/Maps/Report Convex Gaps")]
    public static void ReportAll() => Debug.Log("[ConvexGapScanner]\n" + Run(false));

    [MenuItem("Tools/TagOfChaos/Maps/Fix Convex Gaps")]
    public static void FixAll() => Debug.Log("[ConvexGapScanner]\n" + Run(true));

    // fix면 찾은 충돌체 중 목록(FixPrefixes)에 든 것만 오목(정적)으로 바꾸고 씬을 저장한다.
    public static string Run(bool fix)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "cancelled";
        var report = new List<string>();
        foreach (string map in MapScenes.MapNames)
        {
            var scene = EditorSceneManager.OpenScene(MapScenes.ScenePath(map), OpenSceneMode.Single);
            Physics.SyncTransforms();
            List<Finding> found = Scan(map);
            var names = new List<string>();
            foreach (Finding f in found) names.Add($"{f.Name} {f.Area:F1}m2{(IsListed(f.Name) ? " [fix]" : "")}");
            report.Add($"{map}: {found.Count} — {string.Join(", ", names)}");
            if (!fix || found.Count == 0) continue;
            var fixNames = new HashSet<string>();
            foreach (Finding f in found) fixNames.Add(f.Name);
            foreach (MeshCollider mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                if (IsStaticCandidate(mc) && fixNames.Contains(mc.name) && IsListed(mc.name)) { mc.convex = false; EditorUtility.SetDirty(mc); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        return string.Join("\n", report);
    }
}
