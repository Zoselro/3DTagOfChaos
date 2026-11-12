using System.Collections.Generic;

// 맵 도착 판정(Request1009Plan.md §43 F2). 괴물이 방에는 남아 있는데 맵에 들어오지 않으면 OnPlayerLeftRoom이 오지 않아
// 괴물 없는 판이 끝까지 갔다. 순수 계산이라 EditMode로 시험한다.
public static class MonsterArrival
{
    // 맵에 들어온 괴물 수. loadedScenes = 액터 → 그 사람이 알린 씬(RoomSceneFollower), 없으면 아직 도착 전.
    public static int Arrived(int[] monsters, IReadOnlyDictionary<int, string> loadedScenes, string mapScene)
    {
        if (monsters == null) return 0;
        int count = 0;
        foreach (int actor in monsters)
            if (loadedScenes.TryGetValue(actor, out string scene) && scene == mapScene) count++;
        return count;
    }

    // 방 안 모든 사람이 맵에 들어왔는지(§73 L4).
    public static int ArrivedPlayers(IEnumerable<int> actors, IReadOnlyDictionary<int, string> loadedScenes, string mapScene, out int total)
    {
        total = 0;
        int count = 0;
        foreach (int actor in actors)
        {
            total++;
            if (loadedScenes.TryGetValue(actor, out string scene) && scene == mapScene) count++;
        }
        return count;
    }

    // 모두 도착했을 때 준비 시간을 "지금 + PrepareSeconds"로 당길지(§73 L4). 시작할 때는 기다림을 포함한 넉넉한 끝 시각을 쓰고,
    // 당기기만 한다(늘리지 않음 — 준비 단계가 끝난 뒤 다시 준비로 돌아가지 않게).
    public static bool ShouldShortenPrepare(double prepareEnd, double now, float prepareSeconds) => prepareEnd > now + prepareSeconds + 0.5;

    // 도착 기다림이 끝났을 때 "괴물이 모두 떠난 것"으로 보고 대기실로 돌려보낼지: 괴물이 정해진 판인데 한 명도 오지 않았다.
    public static bool ShouldReturn(int monsterCount, int arrivedCount) => monsterCount > 0 && arrivedCount == 0;
}
