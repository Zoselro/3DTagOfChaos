using System.Collections.Generic;

// 괴물 목록 계산(AuditFixPlan.md F-03·F-04). 방장이 서버 응답 전에 다음 요청을 받아도 이어서 계산할 수 있도록
// "방장이 마지막으로 보낸 목록"(AuthorityProps)을 기준으로 하고, 방에 실제로 남은 사람과 교집합을 낸다. 순수 계산이라 EditMode로 시험한다.
public static class MonsterRoster
{
    // 방장 기준 현재 괴물 목록: 마지막으로 보낸 값 → 없으면 방 캐시.
    public static int[] Current()
    {
        if (AuthorityProps.TryGetExpectedRoom(NetKeys.MonsterActorNumbers, out object sent))
            return sent as int[] ?? new int[0];
        return RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] cached) ? cached : new int[0];
    }

    // 방에 남아 있는 괴물만(퇴장한 사람이 여럿이어도 한 번에 정확한 목록).
    public static int[] Remaining(int[] monsters, ICollection<int> present)
    {
        if (monsters == null || monsters.Length == 0) return new int[0];
        var list = new List<int>(monsters.Length);
        foreach (int a in monsters)
            if (present.Contains(a) && !list.Contains(a)) list.Add(a);
        return list.ToArray();
    }

    // 괴물이 모두 나갔을 때 대기실로 돌려보낼지(2026-10-12 요청, Request1009Plan.md §21). 결과가 났으면 결과 화면이 처리하고,
    // 첫 탈출로 마녀 타이머가 켜진 뒤면 괴물 없이도 판을 끝까지 진행한다. 그 전(사냥 중)이면 돌려보낸다.
    public static bool ReturnWhenMonstersGone(bool spyRocketLaunched, bool resultDecided) =>
        !resultDecided && !spyRocketLaunched;

    // 지원자를 덧붙인 목록. 이미 괴물이거나 자리가 없으면 null.
    public static int[] TryAppend(int[] monsters, int claimant, int capacity)
    {
        monsters = monsters ?? new int[0];
        if (claimant <= 0 || monsters.Length >= capacity || System.Array.IndexOf(monsters, claimant) >= 0) return null;
        var next = new int[monsters.Length + 1];
        monsters.CopyTo(next, 0);
        next[monsters.Length] = claimant;
        return next;
    }
}
