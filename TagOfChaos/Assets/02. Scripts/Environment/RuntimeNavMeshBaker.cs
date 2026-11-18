using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 판 시작 때 AI 길 다시 굽기(Request1009Plan.md §89 L4). 배치가 판마다 바뀌는 맵(Heaven Of Bakery)은 에디터에서 구운 길이 맞지 않는다 —
// MapLayoutRandomizer가 옮긴 뒤 쿠키 AI 길과 거대 괴물 길을 비동기로 다시 굽는다(로딩 화면 동안). 길 계산은 방장만 하지만
// 방장이 바뀔 수 있으므로 모두 굽는다. 굽는 동안 DecoyNavMesh.Ready·GiantNavMesh.Ready는 false라 AI는 서 있는다.
public class RuntimeNavMeshBaker : MonoBehaviour
{
    public static float LastBakeSeconds { get; private set; }

    private IEnumerator Start()
    {
        while (!MapLayoutRandomizer.Applied) yield return null;
        EscapeManager manager = EscapeManager.Instance;
        while (manager == null) { yield return null; manager = EscapeManager.Instance; }
        float started = Time.realtimeSinceStartup;
        Bounds bounds = MapNavMeshRules.BoundsFor(manager.Area);

        DecoyNavMesh decoy = FindFirstObjectByType<DecoyNavMesh>();
        if (decoy != null)
        {
            NavMeshBuildSettings s = MapNavMeshRules.DecoySettings();
            var data = new NavMeshData(s.agentTypeID) { name = gameObject.scene.name + "_DecoyNavMesh_Runtime" };
            AsyncOperation op = NavMeshBuilder.UpdateNavMeshDataAsync(data, s, MapNavMeshRules.DecoySources(bounds, manager.MonsterRoom, manager.MonsterRoomHalfSize), bounds);
            while (!op.isDone) yield return null;
            decoy.Replace(data);
        }
        GiantNavMesh giant = FindFirstObjectByType<GiantNavMesh>();
        if (giant != null && GiantNavMesh.HasAgentType)
        {
            NavMeshBuildSettings s = MapNavMeshRules.GiantSettings();
            var data = new NavMeshData(s.agentTypeID) { name = gameObject.scene.name + "_GiantNavMesh_Runtime" };
            AsyncOperation op = NavMeshBuilder.UpdateNavMeshDataAsync(data, s, MapNavMeshRules.GiantSources(bounds, gameObject.scene), bounds);
            while (!op.isDone) yield return null;
            giant.Replace(data);
        }
        LastBakeSeconds = Time.realtimeSinceStartup - started;
        Debug.Log($"[RuntimeNavMesh] {gameObject.scene.name}: layout {MapLayoutRandomizer.Current.SubMapOrder}/{MapLayoutRandomizer.Current.MaterialLot} baked in {LastBakeSeconds:F2}s");
    }
}
