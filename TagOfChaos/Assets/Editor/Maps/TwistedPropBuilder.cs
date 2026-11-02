using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 소품·숨을 곳 프리팹 공용 규칙(TwistedCandyPlan.md §3·V3·V4에서 시작). 실제 맵 빌더(초콜릿 공장·놀이공원)가 쓴다.
// 오브젝트 이름 규칙으로 충돌체·빛·실내 소리를 붙인다:
//    COL_*   → 보이지 않는 BoxCollider(숨을 곳 벽·바닥처럼 정확한 모양이 필요한 곳)
//    Decal_* → 충돌체 없음(바닥 자국)
//    그 밖   → COL_가 하나도 없으면 오브젝트마다 자기 메시 그대로의 MeshCollider(정적 오목)(Request1009Plan.md §10).
//    Light_* → 점광원, Zone_* → 실내 소리 IndoorZone
// 옛 Game_ 맵용 뒤틀린 소품(TW_*) 프리팹·팔레트 재질 만들기는 Request1009Plan.md §47 CL5에서 없앴다.
public static class TwistedPropBuilder
{
    public const string CollisionPrefix = "COL_";
    public const string DecalPrefix = "Decal_";
    public const string LightPrefix = "Light_";
    public const string ZonePrefix = "Zone_";

    // 숨을 곳(V4): Light_* 표시 → 점광원(안쪽은 어둑한 따뜻한 빛, 출구는 주황 — 출구가 보이게 §2.3), Zone_* 표시(크기 = 상자) → 실내 소리 IndoorZone.
    public static void AddShelterParts(GameObject root)
    {
        var zones = new List<Bounds>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (t.name.StartsWith(LightPrefix))
            {
                var light = t.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                bool exit = t.name.Contains("Exit");
                light.color = exit ? new Color(1f, 0.6f, 0.25f) : new Color(1f, 0.78f, 0.55f);
                light.range = exit ? 7f : 13f;
                light.intensity = exit ? 2f : 1.3f;
                light.shadows = LightShadows.None;
                if (!exit) t.gameObject.AddComponent<FlickerLight>().EditorSetup(light.intensity, 0.1f, new Vector2(5f, 14f));
            }
            else if (t.name.StartsWith(ZonePrefix))
            {
                Vector3 size = t.localScale;
                zones.Add(new Bounds(t.localPosition, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z))));
                Object.DestroyImmediate(t.gameObject);
            }
        }
        if (zones.Count > 0) root.AddComponent<IndoorZone>().EditorSetup(zones.ToArray());
    }

    public static void AddColliders(GameObject root)
    {
        var parts = root.GetComponentsInChildren<MeshFilter>(true);
        bool explicitCollision = parts.Any(f => f.name.StartsWith(CollisionPrefix));
        foreach (MeshFilter f in parts)
        {
            if (f.sharedMesh == null || f.name.StartsWith(DecalPrefix)) continue;
            bool col = f.name.StartsWith(CollisionPrefix);
            if (explicitCollision && !col) continue;
            if (!col)
            {
                var mesh = f.gameObject.AddComponent<MeshCollider>();
                mesh.sharedMesh = f.sharedMesh;
                mesh.convex = false;
                continue;
            }
            Bounds b = f.sharedMesh.bounds;
            var box = f.gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            Object.DestroyImmediate(f.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(f);
        }
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
