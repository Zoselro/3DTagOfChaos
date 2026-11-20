// §90 T2 술래 순간이동 규칙(순수 계산 — 시험). T → 작은 지도에서 재료 건물(이번 판 활성 4채 중, 수리 끝난 건물 포함)을 고르면
// 방장이 State.Warps에 {술래, 건물, At = 지금 + Lead}를 쓴다. At에 출발 자리에서 사라지고 그 건물 벽장(낮은 건물은 바닥 뚜껑) 안으로 옮겨
// RattleSeconds 동안 문이 덜컹거린 뒤(방 안에서 들림) OpenSeconds에 걸쳐 열리고 나온다. 쿨타임 10초, 사냥·타임어택 시간만.
public static class MonsterWarpRules
{
    public const float Lead = 0.3f;          // 요청 → 사라짐(모든 화면이 받을 여유)
    public const float RattleSeconds = 2f;   // 문 덜컹(사용자: 2초)
    public const float OpenSeconds = 0.5f;
    public const float CloseAfter = 3f;      // 연 뒤 문이 닫히기까지
    public const float Cooldown = 10f;       // §90.13 Q2

    public enum Phase { Before, Gone, Rattle, Opening, Out, Done }

    public static double OpenAt(EscapeState.Warp w) => w.At + RattleSeconds;
    public static double DoneAt(EscapeState.Warp w) => w.At + RattleSeconds + OpenSeconds;
    public static double RemoveAt(EscapeState.Warp w) => DoneAt(w) + CloseAfter;

    public static Phase PhaseAt(EscapeState.Warp w, double now)
    {
        if (now < w.At) return Phase.Before;
        if (now < OpenAt(w)) return Phase.Rattle;
        if (now < DoneAt(w)) return Phase.Opening;
        if (now < RemoveAt(w)) return Phase.Out;
        return Phase.Done;
    }

    // 문이 열린 정도(0 = 닫힘, 1 = 다 열림): 열림 → CloseAfter 동안 유지 → 0.5초 동안 닫힘.
    public static float DoorOpen(EscapeState.Warp w, double now)
    {
        double t = now - OpenAt(w);
        if (t <= 0) return 0f;
        if (t < OpenSeconds) return (float)(t / OpenSeconds);
        double close = t - OpenSeconds - CloseAfter;
        if (close <= 0) return 1f;
        return (float)System.Math.Max(0, 1 - close / OpenSeconds);
    }

    // 고를 수 있는 건물: 이번 판 활성(수리 끝난 것 포함).
    public static bool CanPick(EscapeState s, int building) =>
        s != null && building >= 0 && building < s.Buildings.Count && s.Buildings[building].Active;

    // 방장 검사(순수): 술래·사냥 시간·쿨타임·진행 중 아님·고를 수 있는 건물.
    public static bool Allows(EscapeState s, int actor, int building, bool isMonster, bool phaseOk, float now, float readyAt)
    {
        if (!isMonster || !phaseOk || !CanPick(s, building)) return false;
        if (now < readyAt) return false;
        return s.WarpIndex(actor) < 0;
    }
}
