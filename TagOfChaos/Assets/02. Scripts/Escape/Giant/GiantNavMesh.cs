using UnityEngine;
using UnityEngine.AI;

// 거대 괴물이 걷는 길(Request1009Plan.md §64.4). 맵마다 에디터 도구(GiantNavMeshBaker)가 거대 괴물 몸(반지름 3 m, 키 10 m)으로 구운
// NavMesh를 들고 있다가 씬이 켜지면 올린다. 건물·벽은 막고 작은 소품은 통과한다(굽기에서 뺌).
// 에이전트 종류 "Giant"(ProjectSettings/NavMeshAreas)로 구워 쿠키 AI(DecoyNavMesh, 종류 0)의 길 찾기와 섞이지 않는다.
public class GiantNavMesh : MonoBehaviour
{
    public const string AgentTypeName = "Giant";
    public const float AgentRadius = 3f;
    public const float AgentHeight = 10f;
    public const float AgentClimb = 0.6f;
    public const float AgentSlope = 30f;
    public const float PropFootprint = 5f;   // 수평으로 이보다 작은 충돌체(가로·세로 모두)는 소품 — 밟고 지나간다
    public const float PropHeight = 14f;     // 그래도 이보다 높으면(탑·큰 나무 기둥) 막는다

    [SerializeField] private NavMeshData data;

    private NavMeshDataInstance instance;

    public static bool Ready { get; private set; }
    public NavMeshData Data => data;

    private static int agentTypeId;
    private static bool looked, found;

    // "Giant" 에이전트 종류가 있는지(번호는 음수일 수도 있다).
    public static bool HasAgentType { get { Lookup(); return found; } }

    // "Giant" 에이전트 종류 번호(없으면 0 = 쿠키 종류 — HasAgentType으로 먼저 확인).
    public static int AgentTypeId { get { Lookup(); return agentTypeId; } }

    private static void Lookup()
    {
        if (looked) return;
        looked = true;
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
            if (NavMesh.GetSettingsNameFromID(id) != AgentTypeName) continue;
            agentTypeId = id;
            found = true;
            return;
        }
    }

    public static NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas };

    public static bool Sample(Vector3 p, float range, out Vector3 onMesh)
    {
        onMesh = p;
        if (!Ready || !HasAgentType || !NavMesh.SamplePosition(p, out NavMeshHit hit, range, Filter)) return false;
        onMesh = hit.position;
        return true;
    }

    private void OnEnable()
    {
        if (data == null) return;
        instance = NavMesh.AddNavMeshData(data);
        Ready = instance.valid;
    }

    private void OnDisable()
    {
        if (instance.valid) instance.Remove();
        Ready = false;
    }

#if UNITY_EDITOR
    public void EditorSetData(NavMeshData navMesh) => data = navMesh;
#endif
}
