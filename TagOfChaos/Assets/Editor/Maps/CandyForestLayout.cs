using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 캔디숲 실제 모델 맵의 배치 규칙(Request1009Plan.md §28.6~28.11). 위치표(CandyForestLayoutSO)를 만든다.
// - 둥근 맵(반지름 80 m), 가운데 술래방(24 m), 동서남북 십자 큰길(폭 10 m) 끝에 탈출구.
// - 큰길 양쪽 가장자리에 소품 줄(울타리 문·가로등·지팡이 사탕) + 지나갈 틈 5 m(F1, S1: 틈은 4 m 이상).
// - 사분면 대각선에 재료 건물(앞문이 맵 가운데를 본다), 가장자리 다섯 무리(괴물집·천막·지팡이 사탕, G4).
// - 사분면 꾸미기는 그림(§28.7)대로, 겹치지 않게 원(충돌 반지름 + 2 m)으로 자리를 잡는다 — 충돌체끼리 틈이 늘 4 m 이상.
public static class CandyForestLayout
{
    public const float MapRadius = 80f;
    public const float FenceInner = 79.6f;     // 울타리 충돌 안쪽 면
    public const float RoomHalf = 12f;
    public const float RoadHalf = 5f;
    public const float RowOffset = 6.2f;       // 큰길 가장자리 소품 줄(길 가운데에서)
    public const float RowHalfDepth = 0.4f;
    public const float BuildingRadius = 42f; // 바닥 장식까지 충돌(약 13 m) — 가장자리 무리(66~68 m)와 4 m 넘게
    public const float ArchRadius = 42f;
    public const float HalfGap = 2f;            // 충돌체 사이 4 m의 절반

    // 사분면 방향(1 북서 · 2 북동 · 3 남서 · 4 남동 — 그림 번호, 테스트 맵과 같다)
    public static readonly Vector2[] QuadrantDir = { new Vector2(-1, 1).normalized, new Vector2(1, 1).normalized, new Vector2(-1, -1).normalized, new Vector2(1, -1).normalized };
    public static readonly Vector2[] RoadDir = { Vector2.up, Vector2.right, Vector2.down, Vector2.left }; // N E S W
    public static readonly string[] ExitNames = { "Exit_N", "Exit_E", "Exit_S", "Exit_W" };

    public struct Footprint
    {
        public float Collider; // 충돌체가 닿는 가장 먼 수평 거리(0 = 충돌 없음)
        public float Visual;   // 겉모습 반지름
    }

    // 원 자리 잡기
    private class Solver
    {
        private readonly List<(Vector2 c, float r)> taken = new List<(Vector2, float)>();
        public void Take(Vector2 c, float r) => taken.Add((c, r));

        public bool Free(Vector2 c, float keep, bool solid)
        {
            // 충돌체가 있으면 울타리 안쪽 면과도 4 m(keep에 2 m가 들어 있다), 겉모습만이면 울타리 안
            if (c.magnitude + keep > (solid ? FenceInner - HalfGap : MapRadius - 1f)) return false;
            float band = RowOffset + RowHalfDepth + keep;
            if (Mathf.Abs(c.x) < band || Mathf.Abs(c.y) < band) return false;           // 큰길·소품 줄
            if (Mathf.Abs(c.x) < RoomHalf + 2f + keep && Mathf.Abs(c.y) < RoomHalf + 2f + keep) return false; // 술래방
            foreach ((Vector2 tc, float tr) in taken) if ((tc - c).sqrMagnitude < (tr + keep) * (tr + keep)) return false;
            return true;
        }

        // 원하는 자리가 안 되는 까닭(경고용)
        public string Why(Vector2 c, float keep, bool solid)
        {
            if (c.magnitude + keep > (solid ? FenceInner - HalfGap : MapRadius - 1f)) return "fence";
            float band = RowOffset + RowHalfDepth + keep;
            if (Mathf.Abs(c.x) < band || Mathf.Abs(c.y) < band) return "road";
            if (Mathf.Abs(c.x) < RoomHalf + 2f + keep && Mathf.Abs(c.y) < RoomHalf + 2f + keep) return "room";
            foreach ((Vector2 tc, float tr) in taken) if ((tc - c).sqrMagnitude < (tr + keep) * (tr + keep)) return $"near {tc:F0} r {tr:F1}";
            return "free";
        }

        // 원하는 자리에서 나선으로 가장 가까운 빈자리
        public bool Place(Vector2 want, float keep, bool solid, out Vector2 at)
        {
            for (float r = 0f; r <= 30f; r += 1f)
            {
                int steps = r < 0.5f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r / 1.5f);
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    Vector2 c = want + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!Free(c, keep, solid)) continue;
                    Take(c, keep);
                    at = c;
                    return true;
                }
            }
            at = want;
            return false;
        }
    }

    public static Vector2 Polar(float radius, float degrees) =>
        new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;

    // 맵 가운데를 보는 회전(앞 = +Z)
    public static float YawToCenter(Vector2 p) => Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg;

    public static List<CandyForestLayoutSO.Placement> Generate(System.Func<string, Footprint> footprint, out List<string> warnings)
    {
        var list = new List<CandyForestLayoutSO.Placement>();
        var warn = new List<string>();
        var solver = new Solver();
        var rng = new System.Random(28);

        void Add(string asset, string group, Vector2 p, float yaw, bool noCol = false) =>
            list.Add(new CandyForestLayoutSO.Placement { asset = asset, group = group, position = new Vector3(p.x, 0f, p.y), yaw = yaw, noColliders = noCol });

        float Keep(string asset)
        {
            Footprint f = footprint(asset);
            return f.Collider > 0f ? f.Collider + HalfGap : f.Visual * 0.75f;
        }
        bool Solid(string asset) => footprint(asset).Collider > 0f;

        bool Put(string asset, string group, Vector2 want, float? yaw = null)
        {
            if (!solver.Place(want, Keep(asset), Solid(asset), out Vector2 at)) { warn.Add($"{asset} near {want} (keep {Keep(asset):F1}): no room — {solver.Why(want, Keep(asset), Solid(asset))}"); return false; }
            Add(asset, group, at, yaw ?? YawToCenter(at));
            return true;
        }

        // 1) 재료 건물 4채(사분면 대각선, 앞문이 가운데를 본다) + 앞뒤 문 앞 빈터
        for (int q = 0; q < 4; q++)
        {
            Vector2 c = QuadrantDir[q] * BuildingRadius;
            string asset = $"CF_Bld_Q{q + 1}";
            solver.Take(c, Keep(asset));
            Add(asset, "Buildings", c, YawToCenter(c));
            solver.Take(QuadrantDir[q] * (BuildingRadius - 15f), 3f);  // 앞문 앞
            solver.Take(QuadrantDir[q] * (BuildingRadius + 15f), 3f);  // 뒷문 앞
        }

        // 2) 가장자리 다섯 무리: 괴물집 1 + 천막 1 + 지팡이 사탕 2(G4). 울타리 안쪽 둘레(반지름 66~68 m)에
        //    큰길·재료 건물과 4 m 넘게 떨어진 각도로 번갈아 둔다(2사분면에 두 무리 — 1사분면은 케이크 집 자리).
        //    괴물집·천막 앞(가운데 쪽) 문 앞은 비워 둔다.
        (float house, float tent)[] groups = { (18f, 36f), (72f, 54f), (108f, 126f), (198f, 216f), (288f, 306f) };
        foreach ((float house, float tent) in groups)
        {
            foreach ((string asset, float deg, float radius, float apron) in new[] { ("CF_MonsterHouse", house, 66f, 12f), ("CF_Tent", tent, 68f, 10.5f) })
            {
                if (!Put(asset, "Edge", Polar(radius, deg))) continue;
                Vector3 at3 = list[list.Count - 1].position;
                var at = new Vector2(at3.x, at3.z);
                solver.Take(at.normalized * (at.magnitude - apron), 3f);
            }
            float mid = (house + tent) / 2f;
            Put("CF_CandyCane", "Edge", Polar(74f, mid));
            Put("CF_CandyCane", "Edge", Polar(54f, mid));
        }

        // 3) 큰길: 탈출구 앞까지 양쪽 소품 줄 + 길마다 아치 하나
        for (int d = 0; d < 4; d++)
        {
            Vector2 dir = RoadDir[d], perp = new Vector2(dir.y, -dir.x);
            float roadYaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            Add("CF_Arch", "Roads", dir * ArchRadius, roadYaw);   // 아치 너비(모델 x)가 길을 가로지른다 — 다리는 길 양옆
            foreach (int s in new[] { -1, 1 })
            {
                float t = 17f;
                int k = 0;
                while (t < 74f)
                {
                    bool fence = k % 2 == 0;
                    float len = fence ? 6f : 1.2f;
                    float mid = t + len / 2f;
                    if (Mathf.Abs(mid - ArchRadius) < 2.7f + 4f + len / 2f) { t = ArchRadius + 2.7f + 4f; continue; } // 아치 다리와 4 m
                    if (mid + len / 2f > 74f) break;
                    Vector2 p = dir * mid + perp * (s * RowOffset);
                    string asset = fence ? "CF_FenceGate" : (k % 4 == 1 ? "CF_Lamp" : "CF_CandyCane");
                    Add(asset, "Roads", p, fence ? roadYaw + 90f + (s > 0 ? 180f : 0f) : 0f);
                    t += len + 5f;
                    k++;
                }
            }
        }

        // 4) 사분면 꾸미기(그림 §28.7). 원하는 자리 = 사분면 방향 기준 (반지름, 각도 차이)
        void Quad(int q, string asset, float radius, float deltaDeg)
        {
            float baseDeg = Mathf.Atan2(QuadrantDir[q].y, QuadrantDir[q].x) * Mathf.Rad2Deg;
            Put(asset, $"Quadrant_Q{q + 1}", Polar(radius, baseDeg + deltaDeg));
        }
        // 1 빨강(북서): 체리 케이크 집 · 막대사탕 나무 · 솜사탕 나무 · 가로등 · 벤치
        Quad(0, "CF_Cottage", 60f, 20f);
        Quad(0, "CF_LollipopTree", 28f, 22f); Quad(0, "CF_LollipopTree", 64f, -6f); Quad(0, "CF_LollipopTree", 36f, 30f);
        Quad(0, "CF_CottonTree", 62f, 14f); Quad(0, "CF_CottonTree", 30f, -38f);
        Quad(0, "CF_Lamp", 22f, 8f); Quad(0, "CF_Bench", 24f, 14f); Quad(0, "CF_Mushrooms", 70f, 10f);
        // 2 파랑(북동): 솜사탕 나무 · 버섯 · 보라 나무 · 둥근 울타리 · 이정표
        Quad(1, "CF_CottonTree", 26f, 20f); Quad(1, "CF_CottonTree", 28f, -22f); Quad(1, "CF_CottonTree", 63f, 8f);
        Quad(1, "CF_PurpleTree", 34f, -32f); Quad(1, "CF_Mushrooms", 22f, -2f);
        Quad(1, "CF_Signpost", 20f, 12f);
        // 3 초록(남서): 연못 · 보라 나무 · 버섯 · 막대사탕 · 이정표 · 벤치
        Quad(2, "CF_Pond", 26f, 26f);
        Quad(2, "CF_PurpleTree", 62f, 12f); Quad(2, "CF_PurpleTree", 64f, -12f);
        Quad(2, "CF_Mushrooms", 30f, 40f); Quad(2, "CF_LollipopTree", 36f, -34f); Quad(2, "CF_Signpost", 20f, -8f); Quad(2, "CF_Bench", 22f, 36f);
        // 4 노랑(남동): 사탕 가게 · 캔디 더미 · 나무 · 둥근 울타리 · 가로등
        Quad(3, "CF_Shop", 58f, 25f);
        Quad(3, "CF_CandyPile", 26f, 24f); Quad(3, "CF_CandyPile", 64f, 10f);
        Quad(3, "CF_CottonTree", 62f, -12f);
        Quad(3, "CF_RingFence", 22f, 4f); Quad(3, "CF_Lamp", 20f, -12f);

        // 5) 울타리 밖 숲(겉모습만, 충돌 없음 — §28.6)
        string[] trees = { "CF_LollipopTree", "CF_CottonTree", "CF_PurpleTree" };
        for (int i = 0; i < 64; i++)
        {
            float deg = i * 360f / 64f + (float)rng.NextDouble() * 3f;
            float radius = 87f + (float)rng.NextDouble() * 22f;
            bool nearExit = Enumerable.Range(0, 4).Any(e => Mathf.Abs(Mathf.DeltaAngle(deg, e * 90f)) < 8f);
            if (nearExit) radius += 12f; // 탈출구 바깥 받침과 겹치지 않게
            Add(trees[i % trees.Length], "Forest", Polar(radius, deg), (float)rng.NextDouble() * 360f, true);
        }

        warnings = warn;
        return list;
    }

    // ---------------------------------------------------------------- 표시(상자·로켓·무지개·스폰) 자리

    public struct MarkerSpec
    {
        public string Prefix;
        public int PerQuadrant;
        public float Clear;     // 이만큼 둘레가 비어야 함(충돌 검사 반지름)
        public float Spacing;   // 같은 종류끼리 떨어진 거리
        public float MinRadius, MaxRadius;
    }

    public static readonly MarkerSpec[] Markers =
    {
        new MarkerSpec { Prefix = "Spawn_Cookie_Q", PerQuadrant = 1, Clear = 2.5f, Spacing = 10f, MinRadius = 22f, MaxRadius = 32f },
        new MarkerSpec { Prefix = "RocketSpawn_", PerQuadrant = 2, Clear = 3.5f, Spacing = 14f, MinRadius = 22f, MaxRadius = 72f },
        new MarkerSpec { Prefix = "RainbowSpawn_", PerQuadrant = 2, Clear = 2f, Spacing = 12f, MinRadius = 24f, MaxRadius = 66f },
        new MarkerSpec { Prefix = "CHEST_Slot_", PerQuadrant = 10, Clear = 1.4f, Spacing = 7f, MinRadius = 18f, MaxRadius = 72f },
    };

    // 사분면 안 무작위 후보 중 isClear(점, 반지름)이 참이고 다른 표시와 떨어진 자리. 로켓은 6개만 쓴다.
    public static List<(string name, Vector3 pos)> PlaceMarkers(System.Func<Vector3, float, bool> isClear)
    {
        var rng = new System.Random(2028);
        var result = new List<(string, Vector3)>();
        var placed = new List<(Vector3 p, float r)>();
        foreach (MarkerSpec spec in Markers)
        {
            int index = 0;
            for (int q = 0; q < 4; q++)
            {
                int want = spec.Prefix == "RocketSpawn_" && q >= 2 ? 1 : spec.PerQuadrant; // 로켓 6 = 2 + 2 + 1 + 1
                int got = 0;
                for (int tries = 0; tries < 4000 && got < want; tries++)
                {
                    float radius = Mathf.Lerp(spec.MinRadius, spec.MaxRadius, (float)rng.NextDouble());
                    float baseDeg = Mathf.Atan2(QuadrantDir[q].y, QuadrantDir[q].x) * Mathf.Rad2Deg;
                    Vector2 p2 = Polar(radius, baseDeg + Mathf.Lerp(-40f, 40f, (float)rng.NextDouble()));
                    float band = RowOffset + RowHalfDepth + spec.Clear + 1.5f;
                    if (Mathf.Abs(p2.x) < band || Mathf.Abs(p2.y) < band) continue;
                    var p = new Vector3(p2.x, 0f, p2.y);
                    if (placed.Any(o => (o.p - p).sqrMagnitude < Mathf.Pow(Mathf.Max(o.r, spec.Spacing), 2))) continue;
                    if (!isClear(p, spec.Clear)) continue;
                    placed.Add((p, spec.Spacing));
                    string name = spec.Prefix == "Spawn_Cookie_Q" ? $"{spec.Prefix}{q + 1}" : $"{spec.Prefix}{index:00}";
                    result.Add((name, p));
                    index++;
                    got++;
                }
            }
        }
        return result;
    }
}
