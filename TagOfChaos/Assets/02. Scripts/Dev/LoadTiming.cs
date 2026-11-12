using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// 판 시작 로딩 측정(Request1009Plan.md §73 L7). 시작 누름(방장)·방 씬 바뀜(모두)부터 씬 열림·첫 프레임·모두 도착까지의 시각과
// 그때 메모리를 [LoadTiming] 로그로 남긴다(빌드는 Player.log). 첫 5초 동안 가장 긴 프레임도 남긴다.
public class LoadTiming : MonoBehaviour
{
    private const float BeginDedupSeconds = 5f; // 같은 로드를 두 번 시작으로 세지 않게
    private const float WatchFramesSeconds = 5f;

    private static LoadTiming instance;
    private static float beganAt = -100f;
    private static bool firstFramePending;
    private static float watchUntil = -1f;
    private static float longestFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(LoadTiming));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LoadTiming>();
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            Mark("scene opened " + scene.name);
            firstFramePending = true;
        };
    }

    // 로드가 시작됐다(방장: 시작 버튼, 모두: 방 씬 변경). 최근에 이미 시작했으면 시계를 그대로 둔다.
    public static void Begin(string what)
    {
        if (Time.realtimeSinceStartup - beganAt > BeginDedupSeconds) beganAt = Time.realtimeSinceStartup;
        Mark(what);
    }

    public static void Mark(string what)
    {
        StallWatch.Mark(what);
        float t = beganAt >= 0f ? Time.realtimeSinceStartup - beganAt : 0f;
        Debug.Log($"[LoadTiming] {what} at +{t:F1}s (memory {Profiler.GetTotalReservedMemoryLong() >> 20} MB reserved, " +
                  $"{Profiler.GetTotalAllocatedMemoryLong() >> 20} MB used)");
    }

    private void Update()
    {
        if (firstFramePending)
        {
            firstFramePending = false;
            Mark("first frame");
            watchUntil = Time.realtimeSinceStartup + WatchFramesSeconds;
            longestFrame = 0f;
            return;
        }
        if (watchUntil < 0f) return;
        longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
        if (Time.realtimeSinceStartup < watchUntil) return;
        watchUntil = -1f;
        Debug.Log($"[LoadTiming] longest frame in the first {WatchFramesSeconds:F0}s after loading: {longestFrame * 1000f:F0} ms");
    }
}
