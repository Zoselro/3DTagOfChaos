using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 술래별 가장 최근 돌진 예고(Request1009Plan.md §70.2.5 D1). 모든 화면이 갖고 있어 방장이 바뀌어도 판정이 이어진다.
// 판정을 맡은 쪽은 받을 때 검사한다: 쿨타임보다 자주 예고할 수 없고, 출발 시각은 보낸 시각 + 준비 시간 근처여야 하고,
// 시작 위치는 기록된 그 술래의 길 근처여야 한다. 술래 화면을 더 믿는 것은 "예고한 방향으로 실제로 돌진했다"는 점뿐이다.
public static class MonsterDashLedger
{
    public const float CooldownSlack = 0.3f;   // 쿨타임 판정 여유(초)
    public const float StartTolerance = 3f;    // 시작 위치가 기록된 길에서 벗어나도 되는 거리(m)
    public const float IntentTimeSlack = 0.5f; // 출발 시각과 (보낸 시각 + 준비 시간)의 차이 허용(초)
    public const float CoverSlack = 0.5f;      // 돌진 이동 시간 앞으로 이만큼은 그 돌진으로 본다(초)
    public const float HoldAfterEnd = 1.5f;    // 돌진이 끝난 뒤 이만큼은 그 길 위에 서 있을 수 있다(후딜 0.4초 + 프레임 끊김 여유, 초)
    public const float ClaimTimeSlack = 0.1f;  // 주장 시각의 오차(한 물리 스텝 + 시계 차이)만큼 더 갈 수 있다고 본다(초)

    private static readonly Dictionary<int, DashWindow> windows = new Dictionary<int, DashWindow>();

    // 시험·재현용: 켜면 예고를 무시한다(고치기 전 판정 재현).
    public static bool DebugIgnore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => windows.Clear();

    public static void Clear() => windows.Clear();

    // 받아들여도 되는 예고인지(순수 계산). hasLast: 그 술래의 직전 예고가 있는지. startOffPath: 시작 위치가 기록된 길에서 벗어난 거리(모르면 0).
    public static bool IsAcceptable(DashWindow w, double sentAt, float windupSeconds, bool hasLast, double lastMoveStartsAt, float cooldown, float startOffPath)
    {
        if (!w.IsValid) return false;
        if (hasLast && w.MoveStartsAt < lastMoveStartsAt + cooldown - CooldownSlack) return false;
        if (System.Math.Abs(w.MoveStartsAt - (sentAt + windupSeconds)) > IntentTimeSlack) return false;
        return startOffPath <= StartTolerance;
    }

    // 예고를 적는다. validate: 판정을 맡은 쪽에서 검사할지. 거절하면 false(적지 않는다).
    public static bool Record(int viewId, DashWindow w, double sentAt, float windupSeconds, float cooldown, bool validate, float startOffPath)
    {
        bool hasLast = windows.TryGetValue(viewId, out DashWindow last);
        if (validate && !IsAcceptable(w, sentAt, windupSeconds, hasLast, last.MoveStartsAt, cooldown, startOffPath))
        {
            Debug.LogWarning($"[GrabKill] Dash intent of view {viewId} ignored (start off path {startOffPath:F1} m, starts in {w.MoveStartsAt - sentAt:F2}s).");
            return false;
        }
        windows[viewId] = w;
        return true;
    }

    public const float MaxStartDelay = 1f; // 예고보다 늦게 출발했다는 알림은 이 안까지만 받는다(프레임 끊김)

    // 예고보다 늦게 출발했다(술래 화면의 프레임이 끊겨 준비 자세가 늦게 끝남): 출발 시각만 옮긴다. 시작 위치·방향은 그대로.
    public static bool Delay(int viewId, double actualStart)
    {
        if (!windows.TryGetValue(viewId, out DashWindow w)) return false;
        double delay = actualStart - w.MoveStartsAt;
        if (delay <= 0 || delay > MaxStartDelay) return false;
        w.MoveStartsAt = actualStart;
        windows[viewId] = w;
        return true;
    }

    // 로그용: 그 술래의 마지막 예고와 at의 관계.
    public static string Describe(int viewId, double at) =>
        DebugIgnore ? "ignored" : windows.TryGetValue(viewId, out DashWindow w) ? $"last dash moves {w.MoveStartsAt - at:+0.00;-0.00}s..{w.MoveEndsAt - at:+0.00;-0.00}s from the claim" : "no intent";

    // at 시각에 그 술래가 예고한 돌진 중이었으면(앞뒤 여유 포함) 그 돌진.
    public static bool TryGet(int viewId, double at, out DashWindow w)
    {
        if (DebugIgnore) { w = default; return false; }
        return windows.TryGetValue(viewId, out w) && w.Covers(at, CoverSlack, HoldAfterEnd);
    }
}
