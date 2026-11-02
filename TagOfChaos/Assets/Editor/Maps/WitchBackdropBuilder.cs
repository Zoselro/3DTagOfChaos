using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 마녀가 멀리서 걸어오는 배경(Request1009Plan.md §23.5 X6). 맵 씬의 Map/Background 밖에 Map/FarBackdrop을 만든다. 여러 번 실행해도 같은 결과.
// - 바닥: 기존 배경 바닥(가장 넓은 평평한 렌더러) 바깥 가장자리부터 반지름 OuterRadius까지 고리 모양. 재질은 그 배경 바닥 재질.
//   멀수록 낮은 언덕이 조금씩 생기고, 마녀가 걷는 길(앵커 방향 ±PathHalfWidth)은 평평하게 둔다.
// - 소품: 그 맵 Background의 소품 메시(없으면 Decoration)를 복제해 흩뿌린다. 멀수록 드물고 크게. 마녀가 걷는 길은 비운다.
// - 콜라이더 없음(쿠키는 지금처럼 Ground 경계 안에서만), 그림자 끔, 정적 배칭. 맵을 바꿔도 Map/Ground·Map/Background·WITCH_Anchor만 있으면 된다.
public static class WitchBackdropBuilder
{
    public const string RootName = WitchPath.FarBackdropName;
    public const float OuterRadius = 900f;
    public const float PathHalfWidth = 30f;
    public const int PropCount = 150;
    private const string MeshFolder = "Assets/09. Environment/WitchBackdrop";
    private const int Segments = 128, Rings = 24;
    private static readonly string[] Maps = MapScenes.MapNames;

    [MenuItem("Tools/TagOfChaos/Escape/Build Witch Backdrops")]
    public static void BuildMenu() => Debug.Log("[WitchBackdrop] " + BuildAll());

    public static string BuildAll()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var report = new List<string>();
        foreach (string map in Maps)
        {
            Scene scene = EditorSceneManager.OpenScene(MapScenes.ScenePath(map), OpenSceneMode.Single);
            report.Add(map + ": " + Build(scene, map));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        return string.Join(" | ", report);
    }

    public static string Build(Scene scene, string map)
    {
        Transform mapRoot = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Map");
        Transform background = mapRoot != null ? mapRoot.Find("Background") : null;
        WitchPresenter witch = Object.FindFirstObjectByType<WitchPresenter>(FindObjectsInactive.Include);
        if (mapRoot == null || background == null || witch == null || !WitchPath.TryGetGroundRect(scene, out Rect ground))
            return "skipped (needs Map/Background, Map/Ground and WITCH_Anchor)";

        Transform old = mapRoot.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(RootName).transform;
        root.SetParent(mapRoot, false);

        Vector3 center = new Vector3(ground.center.x, 0f, ground.center.y);
        Vector3 outward = WitchPath.OutwardDirection(center, witch.transform.position);
        MeshRenderer floor = background.GetComponentsInChildren<MeshRenderer>(true)
            .OrderByDescending(r => r.bounds.size.x * r.bounds.size.z).First();
        float inner = Mathf.Max(floor.bounds.extents.x, floor.bounds.extents.z) * 0.98f;
        float baseY = floor.bounds.min.y;

        var rng = new System.Random(map.GetHashCode());
        Mesh mesh = BuildRing(map, center, inner, baseY, outward, rng);
        var ringGo = new GameObject("FarGround", typeof(MeshFilter), typeof(MeshRenderer));
        ringGo.transform.SetParent(root, false);
        ringGo.GetComponent<MeshFilter>().sharedMesh = mesh;
        var ringRenderer = ringGo.GetComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = floor.sharedMaterial;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(ringGo, StaticEditorFlags.BatchingStatic);

        List<MeshRenderer> sources = PropSources(background, floor);
        if (sources.Count == 0 && mapRoot.Find("Decoration") != null) sources = PropSources(mapRoot.Find("Decoration"), null);
        int props = 0, tries = 0;
        while (props < PropCount && sources.Count > 0 && tries++ < PropCount * 20)
        {
            float u = (float)rng.NextDouble();
            float r = Mathf.Lerp(inner + 15f, OuterRadius - 40f, u * u);            // 가까운 곳이 촘촘
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 p = center + dir * r;
            if (OnWitchPath(p, center, outward, PathHalfWidth + 25f)) continue;   // 마녀가 걷는 길은 비운다(발이 뚫지 않게, 가리지 않게)
            MeshRenderer src = sources[rng.Next(sources.Count)];
            var go = new GameObject(src.name + "_far", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterials = src.sharedMaterials;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float scale = Mathf.Lerp(1f, 2.4f, (r - inner) / (OuterRadius - inner)) * Mathf.Lerp(0.8f, 1.2f, (float)rng.NextDouble());
            go.transform.localScale = src.transform.lossyScale * scale;
            go.transform.SetPositionAndRotation(new Vector3(p.x, HeightAt(p, center, inner, baseY, outward), p.z),
                Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * src.transform.rotation);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            props++;
        }
        return $"ring {inner:F0}~{OuterRadius:F0} m, {props} props";
    }

    // 배경 소품: 평평한 바닥(가장 넓은 것)을 뺀 메시들. 같은 메시는 하나만.
    private static List<MeshRenderer> PropSources(Transform parent, MeshRenderer floor)
    {
        var byMesh = new Dictionary<Mesh, MeshRenderer>();
        foreach (MeshRenderer r in parent.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == floor) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || r.bounds.size.y < 2f) continue;
            if (!byMesh.ContainsKey(mf.sharedMesh)) byMesh[mf.sharedMesh] = r;
        }
        return byMesh.Values.ToList();
    }

    public static bool OnWitchPath(Vector3 p, Vector3 center, Vector3 outward, float halfWidth)
    {
        Vector3 d = p - center;
        d.y = 0f;
        float along = Vector3.Dot(d, outward);
        if (along < 0f) return false;
        return (d - outward * along).magnitude < halfWidth;
    }

    // 고리 바닥 높이: 안쪽 가장자리는 배경 바닥과 같은 높이, 멀수록 낮은 언덕. 마녀가 걷는 길은 평평.
    public static float HeightAt(Vector3 p, Vector3 center, float inner, float baseY, Vector3 outward)
    {
        float r = new Vector2(p.x - center.x, p.z - center.z).magnitude;
        float k = Mathf.Clamp01((r - inner) / (OuterRadius - inner));
        float hills = (Mathf.PerlinNoise(p.x * 0.008f + 31f, p.z * 0.008f + 17f) - 0.35f) * 30f * k * k;
        Vector3 d = p - center; d.y = 0f;
        float along = Vector3.Dot(d, outward);
        float off = along > 0f ? (d - outward * along).magnitude : float.MaxValue;
        float flat = Mathf.Clamp01((off - PathHalfWidth) / 40f); // 길 가운데 0 → 바깥 1
        return baseY - 0.05f + Mathf.Max(0f, hills) * flat;
    }

    private static Mesh BuildRing(string map, Vector3 center, float inner, float baseY, Vector3 outward, System.Random rng)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int ring = 0; ring <= Rings; ring++)
        {
            float r = Mathf.Lerp(inner, OuterRadius, Mathf.Pow(ring / (float)Rings, 1.5f)); // 안쪽이 촘촘
            for (int s = 0; s < Segments; s++)
            {
                float a = s * Mathf.PI * 2f / Segments;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                verts.Add(new Vector3(p.x, HeightAt(p, center, inner, baseY, outward), p.z));
            }
        }
        for (int ring = 0; ring < Rings; ring++)
            for (int s = 0; s < Segments; s++)
            {
                int a = ring * Segments + s, b = ring * Segments + (s + 1) % Segments;
                int c = a + Segments, d = b + Segments;
                tris.AddRange(new[] { a, b, c, b, d, c }); // 위에서 볼 때 시계 방향 = 위를 보는 면
            }
        var mesh = new Mesh { name = map + "_FarGround" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Directory.CreateDirectory(MeshFolder);
        string path = $"{MeshFolder}/{map}_FarGround.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
