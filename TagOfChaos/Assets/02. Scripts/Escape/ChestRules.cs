using System.Collections.Generic;

// §90 C8 상자 아이템 규칙(순수 계산 — 시험). 상자는 판 시작 때 비어 있고, 여는 순간 지금 게임 상태를 보고 내용을 정한다.
// 일반 상자: 수리 안 된 건물의 재료 중 쿠키가 닿는 곳(상자 위·바닥·쿠키 손)에 2개 미만인 것 → 가장 모자란 것, 없으면 쿠키 아이템.
// 스파이 상자(하나에 하나): 그 스파이의 첫 상자 = 해체 도구, 그 뒤 로켓 부품 2 : 수리키트 1, 둘 다 필요 없으면 스파이 무작위 도구.
// 리셋: 상자가 모두 열렸을 때마다 + 사냥 시작 8분에 한 번 — 모든 상자가 닫히고 비며, 그 뒤에는 수리 끝난 재료도 다시 나온다.
public static class ChestRules
{
    public const int NeedThreshold = 2;        // 쿠키가 닿는 곳에 이보다 적으면 "필요한 재료"(사용자 결정)
    public const float ResetSeconds = 480f;    // 사냥 시작(준비 끝) 8분 뒤 한 번
    public const int NormalChestsPerCookie = 8;
    public const int SpyChestsPerSpy = 8;      // §90.13 Q8 스파이 × 5 → × 8(하나에 하나씩)

    // 일반 상자에서 줄 재료(없으면 null → 쿠키 아이템). reachable = 쿠키가 닿는 곳에 있는 수.
    public static string NormalPick(EscapeState s, System.Func<string, int> reachable, System.Random rng)
    {
        if (s == null) return null;
        bool afterReset = s.ChestResets > 0;
        string best = null;
        int bestCount = int.MaxValue;
        bool bestRepaired = true;
        int ties = 0;
        var seen = new HashSet<string>();
        foreach (EscapeState.Building b in s.Buildings)
        {
            if (!b.Active || string.IsNullOrEmpty(b.MaterialId) || !seen.Add(b.MaterialId)) continue;
            bool repaired = b.Repaired;
            if (repaired && !afterReset) continue;                       // 수리 끝난 재료는 더 나오지 않는다(리셋 전)
            int n = reachable != null ? reachable(b.MaterialId) : 0;
            if (n >= NeedThreshold) continue;
            // 수리 안 된 것 먼저, 그다음 가장 모자란 것, 같으면 무작위
            bool better = best == null || (bestRepaired && !repaired) || (bestRepaired == repaired && n < bestCount);
            bool same = best != null && bestRepaired == repaired && n == bestCount;
            if (better && !same) { best = b.MaterialId; bestCount = n; bestRepaired = repaired; ties = 1; }
            else if (same && rng != null && rng.Next(++ties) == 0) best = b.MaterialId;
        }
        return best;
    }

    // 로켓 빈 칸 재료 중 스파이가 닿는 곳(상자·바닥·스파이 손)에 없는 것. 없으면 null.
    public static string MissingRocketPart(EscapeState s, System.Func<string, int> reachableForSpy)
    {
        if (s == null) return null;
        foreach (EscapeState.Slot slot in s.RocketSlots)
        {
            if (slot.Filled || slot.Accepts == null || slot.Accepts.Length == 0) continue;
            int found = 0;
            foreach (string id in slot.Accepts) found += reachableForSpy != null ? reachableForSpy(id) : 0;
            if (found == 0) return slot.Accepts[0];
        }
        return null;
    }

    public static bool RocketNeedsKit(EscapeState s)
    {
        if (s == null) return false;
        foreach (EscapeState.Slot slot in s.RocketSlots) if (!slot.Filled) return true;
        return false;
    }

    // 스파이 상자 하나 = 아이템 하나(§90.13 Q8). gift는 그 스파이의 차례 기록(바뀐 값을 돌려준다).
    public static string SpyPick(ref EscapeState.SpyGift gift, string missingPart, bool rocketNeedsKit, string kitId, string dismantleId,
        IReadOnlyList<string> randomTools, System.Random rng)
    {
        if (!gift.Dismantle && !string.IsNullOrEmpty(dismantleId)) { gift.Dismantle = true; return dismantleId; }   // 그 스파이의 첫 상자
        bool kitTurn = gift.Kits * 2 < gift.Parts;                                                                       // 부품 2 : 수리키트 1
        if (missingPart != null && !kitTurn) { gift.Parts++; return missingPart; }
        if (!string.IsNullOrEmpty(kitId) && rocketNeedsKit) { gift.Kits++; return kitId; }
        if (missingPart != null) { gift.Parts++; return missingPart; }
        if (randomTools != null && randomTools.Count > 0) return randomTools[rng != null ? rng.Next(randomTools.Count) : 0];
        return null;
    }

    // 상자가 모두 열렸는지 — 스파이 상자는 스파이가 아직 뛸 때만 센다(스파이가 없으면 열 수 없으니까).
    public static bool AllOpened(EscapeState s, bool spyPlaying)
    {
        if (s == null || s.Chests.Count == 0) return false;
        bool any = false;
        foreach (EscapeState.Chest c in s.Chests)
        {
            if (c.Spy && !spyPlaying) continue;
            any = true;
            if (!c.Opened) return false;
        }
        return any;
    }

    public static bool Reset8Due(EscapeState s, bool hasHuntStart, double huntStart, double now) =>
        s != null && !s.ChestReset8Done && hasHuntStart && now >= huntStart + ResetSeconds;

    // 닫혀 있고 내용을 아직 정하지 않은 상자가 있는지(안전장치: 있으면 다음에 열리는 상자에서 필요한 재료가 먼저 나온다 — §90.14 R1).
    public static bool HasClosedUnfilled(EscapeState s, bool spy)
    {
        if (s == null) return false;
        for (int c = 0; c < s.Chests.Count; c++)
            if (s.Chests[c].Spy == spy && !s.Chests[c].Opened && !s.Chests[c].Filled && s.ItemInChest(c) < 0) return true;
        return false;
    }
}
