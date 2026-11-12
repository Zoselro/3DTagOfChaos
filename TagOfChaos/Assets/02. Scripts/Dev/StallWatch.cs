using System.Text;
using System.Threading;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.LowLevel;

// 화면 멈춤 감시(Request1009Plan.md §73.10). 메인 스레드가 StallSeconds 넘게 한 프레임을 끝내지 못하면(창 "응답 없음") 다른 스레드가
// [Stall] 로그를 남긴다 — 멈춘 단계(PlayerLoop: FixedUpdate·Update·PreLateUpdate …), 멈춘 동안 처리한 Photon 이벤트 수(코드별)와 마지막 이벤트.
// 시험에서 클라이언트들이 맵을 연 뒤 첫 프레임에서 54초 멈췄는데(방장 화면은 정상) 에디터 한 대로는 재현되지 않아, 빌드에서 원인 자리를 찍는다.
public static class StallWatch
{
    public const float StallSeconds = 3f;

    private static volatile string phase = "-";
    private static int beat;
    private static Thread watcher;
    private static readonly int[] eventCounts = new int[256];
    private static volatile int lastEventCode = -1;
    private static volatile string lastScene = "-";
    private static volatile bool stopping;

    // 표시(Mark): 메인 스레드가 지나간 자리. 멈추면 마지막 표시와, 풀렸을 때 멈춘 동안 지나간 표시들을 남긴다.
    private const int MarkCapacity = 32;
    private static readonly object markLock = new object();
    private static readonly string[] marks = new string[MarkCapacity];
    private static readonly double[] markTimes = new double[MarkCapacity];
    private static int markCount;
    private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

    public static void Mark(string what)
    {
        lock (markLock)
        {
            int i = markCount++ % MarkCapacity;
            marks[i] = what;
            markTimes[i] = clock.Elapsed.TotalSeconds;
        }
    }

    private static string LastMark()
    {
        lock (markLock)
        {
            if (markCount == 0) return "-";
            int i = (markCount - 1) % MarkCapacity;
            return $"{marks[i]} ({clock.Elapsed.TotalSeconds - markTimes[i]:F1}s ago)";
        }
    }

    private static string MarksSince(double since)
    {
        var sb = new StringBuilder();
        lock (markLock)
        {
            int n = System.Math.Min(markCount, MarkCapacity);
            double prev = since;
            for (int k = markCount - n; k < markCount; k++)
            {
                int i = k % MarkCapacity;
                if (markTimes[i] < since) continue;
                sb.Append(marks[i]).Append(" +").Append((markTimes[i] - prev).ToString("F1")).Append("s; ");
                prev = markTimes[i];
            }
        }
        return sb.Length > 0 ? sb.ToString() : "none";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (watcher != null) return;
        PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
        for (int i = 0; i < root.subSystemList.Length; i++)
        {
            PlayerLoopSystem stage = root.subSystemList[i];
            if (stage.subSystemList != null && stage.subSystemList.Length > 0 && stage.subSystemList[0].type == typeof(StallWatch)) continue; // 에디터에서 다시 Play
            string name = stage.type != null ? stage.type.Name : "stage" + i;
            var list = new PlayerLoopSystem[(stage.subSystemList?.Length ?? 0) + 1];
            list[0] = new PlayerLoopSystem { type = typeof(StallWatch), updateDelegate = () => { phase = name; Interlocked.Increment(ref beat); } };
            if (stage.subSystemList != null) System.Array.Copy(stage.subSystemList, 0, list, 1, stage.subSystemList.Length);
            stage.subSystemList = list;
            root.subSystemList[i] = stage;
        }
        PlayerLoop.SetPlayerLoop(root);
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => lastScene = s.name;
        stopping = false;
        lastScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Application.quitting += () => { stopping = true; watcher = null; PhotonNetwork.NetworkingClient.EventReceived -= OnEvent; }; // 에디터 Play 끝
        watcher = new Thread(Watch) { IsBackground = true, Name = nameof(StallWatch) };
        watcher.Start();
    }

    private static void OnEvent(EventData e)
    {
        Interlocked.Increment(ref eventCounts[e.Code]);
        lastEventCode = e.Code;
    }

    private static void Watch()
    {
        int seen = Volatile.Read(ref beat);
        var since = System.Diagnostics.Stopwatch.StartNew();
        bool reported = false;
        int[] atStart = new int[256];
        double stallStart = 0;
        while (!stopping)
        {
            Thread.Sleep(250);
            int now = Volatile.Read(ref beat);
            if (now != seen)
            {
                if (reported)
                    Debug.LogWarning($"[Stall] main thread resumed after {since.Elapsed.TotalSeconds:F1}s (scene {lastScene}); " +
                                     $"Photon events during the stall: {Diff(atStart)}; marks passed: {MarksSince(stallStart)}");
                seen = now;
                since.Restart();
                reported = false;
                continue;
            }
            if (reported || since.Elapsed.TotalSeconds < StallSeconds) continue;
            reported = true;
            stallStart = clock.Elapsed.TotalSeconds - since.Elapsed.TotalSeconds;
            for (int i = 0; i < 256; i++) atStart[i] = Volatile.Read(ref eventCounts[i]);
            Debug.LogWarning($"[Stall] main thread has not moved for {since.Elapsed.TotalSeconds:F1}s — stuck in {phase} (scene {lastScene}, " +
                             $"last mark {LastMark()}, last Photon event {lastEventCode}, message queue {(PhotonNetwork.IsMessageQueueRunning ? "on" : "off")})");
        }
    }

    // 멈춘 동안 늘어난 이벤트 수(코드별). 253 = 방·플레이어 속성 변경, 200 = RPC, 201/206 = 위치 동기화.
    private static string Diff(int[] atStart)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 256; i++)
        {
            int d = Volatile.Read(ref eventCounts[i]) - atStart[i];
            if (d > 0) sb.Append(i).Append('×').Append(d).Append(' ');
        }
        return sb.Length > 0 ? sb.ToString() : "none";
    }
}
