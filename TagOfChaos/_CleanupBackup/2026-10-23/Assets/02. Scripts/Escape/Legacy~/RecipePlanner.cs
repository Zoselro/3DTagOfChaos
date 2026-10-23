using System.Collections.Generic;
using UnityEngine;

// 판이 시작될 때 레시피와 필요 재료 수로 탈출 장치 칸을 정한다(EscapePlan.md §1.6, D3·D4). 순수 계산이라 테스트할 수 있다.
// - 칸 모드: 고정 칸은 항상 필요. 나머지 (N − 고정 수)개는 남은 칸 중에서 무작위로 고르고, 고르지 않은 칸은 채워진 상태로 시작.
// - 개수 모드: N개 칸, 칸마다 재료 종류를 counterItems 중에서 무작위로(모양만 다름, 어느 것이든 끼울 수 있다).
//   레시피가 FillsCapacity면 칸은 늘 capacity개(인원표의 최대 쿠키 수)이고, 무작위 N개만 필요 칸, 나머지는 채워진 칸(무작위 색).
//   칸 모드도 FillsCapacity면 모델의 칸 자리 중 고정 칸 + 무작위 자리로 capacity개만 쓴다(2026-10-03).
// - 로켓 몫(mustCover, Request1009Plan.md §22.6 M3): 스파이 로켓 칸마다 그 재료를 받는 필요 칸이 따로 하나씩 있게 한다 — 스파이는 쿠키의
//   재료를 훔쳐 로켓을 채우므로, 이번 판 필요 칸에 그 종류가 없으면 로켓을 완성할 수 없었다.
public static class RecipePlanner
{
    public struct PlannedSlot
    {
        public string Label;
        public string[] Accepts;
        public bool Required;    // 처음에 비어 있어 재료가 필요한 칸
        public string SpawnItem; // Required일 때 상자에 넣을 재료 ID
        public int Anchor;       // 모델의 칸 자리 번호(Slot_nn) — 칸 모드에서 일부 자리만 쓸 때 칸 번호와 다르다
    }

    public static List<PlannedSlot> Plan(EscapeRecipeSO recipe, int required, System.Random rng, int capacity = 0, IList<string[]> mustCover = null)
    {
        var result = new List<PlannedSlot>();
        if (recipe == null || required <= 0) return result;

        if (recipe.Mode == RecipeMode.Counter)
        {
            ItemSO[] items = recipe.CounterItems;
            var accepts = new List<string>();
            foreach (ItemSO it in items) if (it != null) accepts.Add(it.ItemId);
            if (accepts.Count == 0) return result;
            // 필요 칸은 모든 색을 받으므로 로켓 칸 수만큼 필요 칸이 있으면 로켓 몫이 저절로 지켜진다.
            // 필요 칸이 로켓 칸보다 적은 방(혼자 하는 오프라인 시험 등)만 해당 — 실제 인원표(쿠키 3명 이상)에서는 생기지 않는다.
            if (mustCover != null && mustCover.Count > required)
                Debug.Log($"[RecipePlanner] {recipe.MapName}: {mustCover.Count} rocket slots but only {required} needed slots — rocket materials are not guaranteed this game.");
            int total = recipe.FillsCapacity ? System.Math.Max(required, capacity) : required;
            var requiredSet = new HashSet<int>(PickDistinct(total, required, rng));
            for (int i = 0; i < total; i++)
            {
                string kind = accepts[rng.Next(accepts.Count)];
                bool need = requiredSet.Contains(i);
                result.Add(new PlannedSlot
                {
                    Label = recipe.CounterLabel,
                    // 필요 칸은 어느 색이든 끼울 수 있다. 채워진 칸은 보여 줄 색 하나만(셈·훔치기에서 빠진다).
                    Accepts = need ? accepts.ToArray() : new[] { kind },
                    Required = need,
                    SpawnItem = kind,
                    Anchor = i,
                });
            }
            return result;
        }

        // 칸 모드: 묶음마다 칸 자리를 만들고(자리 번호 = 묶음 순서대로), 고정 칸은 늘 필요.
        var all = new List<PlannedSlot>();
        var fixedSlots = new List<int>();
        var freeSlots = new List<int>();
        foreach (EscapeRecipeSO.SlotGroup g in recipe.Groups)
        {
            if (g == null || g.item == null) continue;
            string label = string.IsNullOrEmpty(g.label) ? g.item.DisplayName : g.label;
            for (int k = 0; k < g.slotCount; k++)
            {
                bool isFixed = k < g.fixedRequired;
                (isFixed ? fixedSlots : freeSlots).Add(all.Count);
                all.Add(new PlannedSlot
                {
                    Label = label,
                    Accepts = new[] { g.item.ItemId },
                    Required = isFixed,
                    SpawnItem = g.item.ItemId,
                    Anchor = all.Count,
                });
            }
        }

        // 로켓 몫: 이미 필요한 칸(고정)이 받으면 그 칸을, 아니면 그 재료를 받는 자유 자리 하나를 필요로 정한다(한 칸은 한 로켓 칸에만).
        var needed = new HashSet<int>(fixedSlots);
        if (mustCover != null) CoverRocketSlots(recipe, all, freeSlots, needed, mustCover, required, rng);

        // 최대 수만 쓰는 레시피(2026-10-03): 필요 칸 + 나머지 자리 중 무작위로 capacity개까지만 켠다. 나머지 자리는 만들지 않는다.
        var spare = new List<int>();
        foreach (int i in freeSlots) if (!needed.Contains(i)) spare.Add(i);
        var active = new HashSet<int>(needed);
        if (recipe.FillsCapacity && capacity > 0 && all.Count > capacity)
        {
            foreach (int pick in PickDistinct(spare.Count, System.Math.Max(0, capacity - active.Count), rng)) active.Add(spare[pick]);
            spare.RemoveAll(i => !active.Contains(i));
        }
        else foreach (int i in spare) active.Add(i);

        // 나머지 필요 칸 = 켜진 자리 중 무작위(필요 수 − 지금까지). 나머지 켜진 칸은 채워진 상태로 시작.
        foreach (int pick in PickDistinct(spare.Count, System.Math.Max(0, required - needed.Count), rng)) needed.Add(spare[pick]);
        for (int i = 0; i < all.Count; i++)
        {
            if (!active.Contains(i)) continue;
            PlannedSlot s = all[i];
            s.Required = needed.Contains(i);
            result.Add(s);
        }
        return result;
    }

    private static void CoverRocketSlots(EscapeRecipeSO recipe, List<PlannedSlot> all, List<int> freeSlots, HashSet<int> needed,
        IList<string[]> mustCover, int required, System.Random rng)
    {
        var used = new HashSet<int>();
        var candidates = new List<int>();
        foreach (string[] accepts in mustCover)
        {
            int pick = -1;
            foreach (int i in needed)
                if (!used.Contains(i) && AcceptsAny(all[i], accepts) && (pick < 0 || i < pick)) pick = i; // 같은 결과가 나오게 가장 앞 자리
            if (pick < 0)
            {
                candidates.Clear();
                foreach (int i in freeSlots) if (!needed.Contains(i) && AcceptsAny(all[i], accepts)) candidates.Add(i);
                if (candidates.Count == 0)
                {
                    // 레시피 데이터 오류 — 이 로켓 재료를 받는 장치 칸 자리가 아예 없다(EscapeReachabilityTests가 잡는다).
                    Debug.LogWarning($"[RecipePlanner] {recipe.MapName}: no device slot accepts rocket material {string.Join("/", accepts ?? new string[0])}.");
                    continue;
                }
                if (needed.Count >= required)
                {
                    // 필요 칸이 로켓 칸보다 적은 방(혼자 하는 오프라인 시험 등). 실제 인원표(쿠키 3명 이상)에서는 생기지 않는다.
                    Debug.Log($"[RecipePlanner] {recipe.MapName}: only {required} needed slot(s) — rocket material {string.Join("/", accepts ?? new string[0])} is not guaranteed this game.");
                    continue;
                }
                pick = candidates[rng.Next(candidates.Count)];
                needed.Add(pick);
            }
            used.Add(pick);
        }
    }

    private static bool AcceptsAny(PlannedSlot slot, string[] items)
    {
        if (items == null) return false;
        foreach (string a in slot.Accepts)
            if (System.Array.IndexOf(items, a) >= 0) return true;
        return false;
    }

    // 0..total-1 중 서로 다른 count개(무작위).
    private static IEnumerable<int> PickDistinct(int total, int count, System.Random rng)
    {
        var pool = new List<int>();
        for (int i = 0; i < total; i++) pool.Add(i);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int k = rng.Next(pool.Count);
            yield return pool[k];
            pool.RemoveAt(k);
        }
    }
}
