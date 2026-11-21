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
    // §92-5 바닥 뚜껑: 술래는 뚜껑 아래(HatchDepth)에서 기다리다가, 뚜껑이 RiseDelay만큼 열린 뒤 RiseSeconds 동안 솟아오른다
    // (예전에는 바닥 위에 서 있다가 열리기 시작하자마자 보여, 뚜껑이 열리기도 전에 나온 것처럼 보였다).
    public const float HatchDepth = 1.8f;
    public const float RiseDelay = 0.2f;
    public const float RiseSeconds = 0.7f;

    public enum Phase { Before, Gone, Rattle, Opening, Out, Done }

    public static double OpenAt(EscapeState.Warp w) => w.At + RattleSeconds;
    public static double DoneAt(EscapeState.Warp w) => w.At + RattleSeconds + OpenSeconds;
    public static double RemoveAt(EscapeState.Warp w) => DoneAt(w) + CloseAfter;
    public static double RiseEndAt(EscapeState.Warp w) => OpenAt(w) + RiseDelay + RiseSeconds;

    // 뚜껑 아래에서 올라온 정도(0 = 아래에서 기다림, 1 = 바닥 위).
    public static float Rise01(EscapeState.Warp w, double now)
    {
        double t = now - OpenAt(w) - RiseDelay;
        if (t <= 0) return 0f;
        if (t >= RiseSeconds) return 1f;
        return UnityEngine.Mathf.SmoothStep(0f, 1f, (float)(t / RiseSeconds));
    }

    // 술래 몸을 숨기는 동안: 벽장은 덜컹거리는 동안, 뚜껑은 올라오기 시작할 때까지.
    public static bool BodyHidden(EscapeState.Warp w, double now, bool hatch)
    {
        if (now < w.At) return false;
        if (now < OpenAt(w)) return true;
        return hatch && now < OpenAt(w) + RiseDelay;
    }

    // 본인 술래가 못 움직이는 때까지: 벽장 = 문이 다 열릴 때, 뚜껑 = 다 올라왔을 때.
    public static double LockUntil(EscapeState.Warp w, bool hatch) => hatch ? System.Math.Max(DoneAt(w), RiseEndAt(w)) : DoneAt(w);

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
