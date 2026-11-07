using UnityEngine;
using UnityEngine.AI;

// AI(미끼)가 다니는 길(Request1009Plan.md §44.7). 맵마다 에디터 도구(DecoyNavMeshBaker)가 쿠키 몸 크기로 구운 NavMesh를 들고 있다가
// 씬이 켜지면 NavMesh 시스템에 올린다. 길 계산(NavMesh.CalculatePath)은 방장의 DecoyBrain만 한다.
public class DecoyNavMesh : MonoBehaviour
{
    // 쿠키 캡슐(반지름 0.46, 높이 2)에 맞춘 굽기 값 — 에디터 굽기와 시험이 같은 값을 쓴다.
    public const float AgentRadius = 0.45f;
    public const float AgentHeight = 2f;
    public const float AgentClimb = 0.45f;
    public const float AgentSlope = 40f;

    [SerializeField] private NavMeshData data;

    private NavMeshDataInstance instance;

    public static bool Ready { get; private set; }

    // 쿠키 길만 찾는 조건(에이전트 종류 0). 거대 괴물 길(GiantNavMesh, 종류 "Giant", §64)도 함께 올라와 있으므로
    // 쿠키·AI 길 찾기는 늘 이것을 넘긴다 — 종류를 정하지 않으면 두 길이 섞이고 Unity가 경고한다.
    public static NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
    public NavMeshData Data => data;

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
