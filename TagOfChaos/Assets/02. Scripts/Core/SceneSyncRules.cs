using System.Collections.Generic;

// 방 씬 동기화 판단(Request1009Plan.md §43). 네트워크와 분리한 순수 계산이라 EditMode로 시험한다.
public static class SceneSyncRules
{
    // 방이 가리키는 씬(curScn)과 내 씬이 정해진 시간 넘게 다르면 따라간다. 방장이어도 따라간다 —
    // PUN은 curScn이 바뀔 때 방장인 클라이언트를 따라가게 하지 않는다(PhotonNetworkPart.cs LoadLevelIfSynced).
    public static bool ShouldFollow(string roomScene, string activeScene, bool loading, float mismatchSeconds, float threshold) =>
        !string.IsNullOrEmpty(roomScene) && roomScene != activeScene && !loading && mismatchSeconds >= threshold;

    // 씬 로드 중이 아닌데 메시지 큐가 오래 멈춰 있으면 되살린다(끊긴 로드의 안전망).
    public static bool ShouldRestoreQueue(bool queueRunning, bool loading, float pausedSeconds, float threshold) =>
        !queueRunning && !loading && pausedSeconds >= threshold;

    // 방장이 방금 직접 옮기라고 한 씬에 이미 들어와 있으면, curScn 캐시가 아직 옛 값이어도(서버 응답 전) 따라가지 않는다.
    public static bool TrustOwnLoad(string ownLoadScene, string activeScene, float sinceOwnLoad, float window) =>
        !string.IsNullOrEmpty(ownLoadScene) && ownLoadScene == activeScene && sinceOwnLoad < window;

    // 방에 실제로 남아 있는 괴물 수(이미 나간 번호는 세지 않는다).
    public static int PresentCount(int[] monsters, ICollection<int> present)
    {
        if (monsters == null) return 0;
        int count = 0;
        for (int i = 0; i < monsters.Length; i++)
            if (present.Contains(monsters[i]) && System.Array.IndexOf(monsters, monsters[i]) == i) count++;
        return count;
    }
}
