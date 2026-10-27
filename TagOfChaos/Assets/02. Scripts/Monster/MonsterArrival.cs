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

    // 도착 기다림이 끝났을 때 "괴물이 모두 떠난 것"으로 보고 대기실로 돌려보낼지: 괴물이 정해진 판인데 한 명도 오지 않았다.
    public static bool ShouldReturn(int monsterCount, int arrivedCount) => monsterCount > 0 && arrivedCount == 0;
}
