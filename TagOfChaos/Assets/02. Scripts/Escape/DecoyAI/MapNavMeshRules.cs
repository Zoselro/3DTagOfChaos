using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// NavMesh 굽기 규칙(Request1009Plan.md §89 L4) — 에디터 굽기(DecoyNavMeshBaker·GiantNavMeshBaker)와 판 시작 때 다시 굽기(RuntimeNavMeshBaker)가
// 같은 값·같은 빼기 규칙을 쓰게 한 곳에 모았다.
public static class MapNavMeshRules
{
    public const int NotWalkableArea = 1;

    public static NavMeshBuildSettings DecoySettings()
    {
        NavMeshBuildSettings s = NavMesh.GetSettingsByID(0);
        s.agentRadius = DecoyNavMesh.AgentRadius;
        s.agentHeight = DecoyNavMesh.AgentHeight;
        s.agentClimb = DecoyNavMesh.AgentClimb;
        s.agentSlope = DecoyNavMesh.AgentSlope;
        s.minRegionArea = 4f;
        return s;
    }

    public static NavMeshBuildSettings GiantSettings()
    {
        NavMeshBuildSettings s = NavMesh.GetSettingsByID(GiantNavMesh.AgentTypeId);
        s.agentRadius = GiantNavMesh.AgentRadius;
        s.agentHeight = GiantNavMesh.AgentHeight;
        s.agentClimb = GiantNavMesh.AgentClimb;
        s.agentSlope = GiantNavMesh.AgentSlope;
        s.minRegionArea = 30f;
        return s;
    }

    // 쿠키 AI: 문 잎(열린 것처럼)·트리거·캐릭터 몸은 길을 막지 않는다.
    public static bool DecoyExcluded(NavMeshBuildSource s)
    {
        var c = s.component as Collider;
        if (c == null) return false;
        if (c.isTrigger) return true;
        return c.GetComponentInParent<InteractableDoor>() != null || c.GetComponentInParent<IGameCharacter>() != null;
    }

    // 거대 괴물: 문 잎·트리거·캐릭터 몸, 작은 소품, 부서지는 술래방 벽·지붕은 막지 않는다.
    public static bool GiantPassable(NavMeshBuildSource s)
    {
        var c = s.component as Collider;
        if (c == null) return false;
        if (c.isTrigger || c.GetComponentInParent<InteractableDoor>() != null || c.GetComponentInParent<IGameCharacter>() != null) return true;
        Transform room = MonsterRoomOf(c.transform);
        if (room != null && !MonsterRoomBreak.Keep(c, c.bounds, room.position.y)) return true;
        Vector3 size = c.bounds.size;
        return size.x < GiantNavMesh.PropFootprint && size.z < GiantNavMesh.PropFootprint && size.y < GiantNavMesh.PropHeight;
    }

    public static Transform MonsterRoomOf(Transform t)
    {
        for (; t != null; t = t.parent) if (t.name == "MonsterRoom") return t;
        return null;
    }

    // 맵 넓이 기준 굽기 범위: 땅 위 65 m ~ 땅 아래 80 m.
    public static Bounds BoundsFor(MapArea area) =>
        new Bounds(new Vector3(area.Center.x, -7.5f, area.Center.y), new Vector3(area.Rect.width + 8f, 145f, area.Rect.height + 8f));

    // 쿠키 AI 굽기 재료: 물리 충돌체 + 술래방 못 걷는 상자.
    public static List<NavMeshBuildSource> DecoySources(Bounds bounds, Transform room, float roomHalf)
    {
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        sources.RemoveAll(DecoyExcluded);
        if (room != null)
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                transform = Matrix4x4.TRS(room.position, Quaternion.identity, Vector3.one),
                size = new Vector3(roomHalf * 2f + 1f, 60f, roomHalf * 2f + 1f),
                area = NotWalkableArea,
            });
        return sources;
    }

    // 거대 괴물 굽기 재료: 물리 충돌체 − 지나가는 것 − 부서지는 물체(계단 빼고).
    public static List<NavMeshBuildSource> GiantSources(Bounds bounds, UnityEngine.SceneManagement.Scene scene)
    {
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        sources.RemoveAll(s => s.component != null && s.component.gameObject.scene != scene);
        var destructible = new HashSet<Collider>();
        Transform map = DestructibleCatalog.FindMap(scene);
        if (map != null && DestructionRulesSO.Current != null)
            foreach (DestructibleUnit u in DestructibleCatalog.Build(map, DestructionRulesSO.Current).Units)
                if (u.Grade != DestructibleGrade.Stairs) foreach (Collider c in u.Colliders) destructible.Add(c);
        sources.RemoveAll(s => GiantPassable(s) || (s.component is Collider col && destructible.Contains(col)));
        return sources;
    }
}
