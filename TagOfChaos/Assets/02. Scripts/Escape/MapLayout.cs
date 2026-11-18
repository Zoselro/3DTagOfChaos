using UnityEngine;

// 판마다 바뀌는 맵 배치(Request1009Plan.md §89 L1). 방장이 시작 버튼 때 한 번 뽑아 방 속성 NetKeys.MapLayout에 넣는다 —
// 씬보다 먼저 모두에게 도착하고 늦게 들어온 사람도 같은 값을 읽는다. 배치가 없는 맵은 값을 쓰지 않는다.
public static class MapLayout
{
    // 슬롯(NW, N, NE)마다 들어갈 서브 맵 묶음(0 아몬드, 1 초콜릿, 2 밀가루) — 3! = 6가지
    public static readonly int[][] Permutations =
    {
        new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
    };

    public const int LotCount = 4; // 과일 밭 자리: 재료 건물 1 + 일반 건물 3

    public struct Layout
    {
        public int SubMapOrder;   // Permutations 번호
        public int MaterialLot;   // 재료 건물이 들어갈 과일 밭 자리
    }

    public static int[] Roll(System.Random rng) => new[] { rng.Next(Permutations.Length), rng.Next(LotCount) };

    public static Layout Decode(int[] v) => v != null && v.Length >= 2
        ? new Layout { SubMapOrder = Mathf.Clamp(v[0], 0, Permutations.Length - 1), MaterialLot = Mathf.Clamp(v[1], 0, LotCount - 1) }
        : default;

    // 방 밖(에디터 테스트 모드)이거나 값이 없으면 이 기계에서 뽑는다.
    public static Layout FromRoom() =>
        RoomState.TryGetIntArray(NetKeys.MapLayout, out int[] v) ? Decode(v) : Decode(Roll(new System.Random()));
}
