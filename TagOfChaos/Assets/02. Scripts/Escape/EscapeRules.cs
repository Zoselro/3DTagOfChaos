using System.Collections.Generic;

// 탈출 모드의 게임 끝 판정(EscapePlan.md §1.2, Request1009Plan.md §25.5 D). 네트워크와 분리한 순수 계산이라 테스트할 수 있다.
// 방장의 GameRuleController가 매 프레임 부른다. 게임 제한시간은 없다 — 끝나는 길은 셋:
//   1) 괴물이 아닌 사람(쿠키·스파이)이 모두 탈출하거나 잡혔다 → 바로 끝(마녀 타이머 중이어도)
//   2) 첫 탈출로 시작된 마녀 타이머가 끝났다 → 마녀가 밟는다 → 박살을 보여 준 뒤 끝
//   3) (안전 상한) 아무도 탈출하지 못한 채 오래 지나면 방장이 마녀 타이머를 강제로 시작한다 — 2)로 이어진다
public static class EscapeRules
{
    public struct PlayerInfo
    {
        public bool IsMonster;
        public bool IsSpy;
        public bool Escaped;  // 쿠키 탈출구·헬리콥터 모자·스파이 로켓
        public bool Broken;   // 괴물에게 잡힘 또는 마녀
    }

    public enum Decision
    {
        None,
        StrikeWitch,   // 마녀 타이머가 끝났다 — 마녀가 밟는다
        EndAllResolved,
        EndTimeUp,     // 쓰지 않음(게임 제한시간을 없앴다). 값 보존
        EndWitchStrike,
    }

    // 내리침(도착) 기록 뒤 결과까지: 도착 연출 3테이크 — 밟는 순간(FinaleImpactSeconds)까지 + 박살을 보여 주는 5초(Request1009Plan.md §24.10)
    public const float WitchSlamSeconds = WitchTimeline.FinaleImpactSeconds + WitchTimeline.FinaleEndSeconds;

    // witchTimer: 마녀 타이머가 돌고 있는가(첫 탈출 뒤). witchStruckFor: 마녀가 밟은 뒤 지난 시간(초, 안 밟았으면 음수).
    // departing: 로켓 발사 연출 중 — 연출이 끝날 때까지 끝 판정을 미룬다(EscapeVisualPlan.md §4.3).
    public static Decision Evaluate(IReadOnlyList<PlayerInfo> players, bool witchTimer, double now, double witchTimerEnd, float witchStruckFor, bool departing = false)
    {
        if (witchStruckFor >= WitchSlamSeconds) return Decision.EndWitchStrike;
        if (witchStruckFor >= 0f) return Decision.None; // 밟는 중
        if (departing) return Decision.None;

        int active = 0;
        foreach (PlayerInfo p in players)
            if (!p.IsMonster && !p.Escaped && !p.Broken) active++;
        if (active == 0) return Decision.EndAllResolved;

        if (witchTimer && now >= witchTimerEnd) return Decision.StrikeWitch;
        return Decision.None;
    }

    // 안전 상한(§25.3 Q4): 준비가 끝난 뒤 capSeconds가 지났는데 마녀 타이머가 아직 없으면 true.
    public static bool ShouldForceWitchTimer(double now, double prepareEnd, double capSeconds, bool witchTimer) =>
        !witchTimer && now >= prepareEnd + capSeconds;

    // ---------------- 사이렌 함정 놓기(§74 P12) ----------------

    public enum BombPlace : byte { Ok = 0, NoBuilding = 1, InactiveBuilding = 2, MaterialInstalled = 3, AlreadyThere = 4 }

    // 그 재료 건물(site)에 함정을 놓을 수 있는지: 이번 판에 쓰는 건물이고, 재료가 꽂혀 있지 않고, 그 건물에 놓인 함정(철창 포함)이 없다.
    // 스파이가 재료를 빼면(ItemIndex = -1) 다시 놓을 수 있다.
    public static BombPlace CanPlaceBomb(EscapeState s, int site)
    {
        if (s == null || site < 0 || site >= s.Buildings.Count) return BombPlace.NoBuilding;
        EscapeState.Building b = s.Buildings[site];
        if (!b.Active) return BombPlace.InactiveBuilding;
        if (b.ItemIndex >= 0 || b.Repaired) return BombPlace.MaterialInstalled;
        foreach (EscapeState.Bomb other in s.Bombs)
            if (!other.Spent && other.Site == site) return BombPlace.AlreadyThere;
        return BombPlace.Ok;
    }

    // ---------------- 스파이 로켓 칸 재료(§74 P3) ----------------

    // 칸 하나의 재료를 고른다: ① 아직 고르지 않았고 쿠키 건물이 쓰지 않는 것 ② 아직 고르지 않은 것 ③ 아무거나. 같은 단계 안에서는 roll로.
    public static string PickRocketMaterial(IList<string> options, ICollection<string> taken, ICollection<string> usedByBuildings, int roll)
    {
        if (options == null || options.Count == 0) return null;
        var pool = new List<string>();
        foreach (string o in options) if (!taken.Contains(o) && !usedByBuildings.Contains(o)) pool.Add(o);
        if (pool.Count == 0) foreach (string o in options) if (!taken.Contains(o)) pool.Add(o);
        if (pool.Count == 0) pool.AddRange(options);
        int i = (roll % pool.Count + pool.Count) % pool.Count;
        return pool[i];
    }
}
