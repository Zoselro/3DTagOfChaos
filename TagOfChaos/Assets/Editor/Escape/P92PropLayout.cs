using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Request1009Plan.md §92-10 HeavenOfBakery 본섬 사물 배치(승인된 배치도 HOB_props_layout.png).
// 구조(높이·탈출구 4곳·숨는 건물·상자 수·경사로·하우스 3동·길)는 그대로 두고 FieldDeco 사물만 옮긴다.
// 이미 있는 사물을 먼저 쓰고, 모자란 종류만 같은 프리팹에서 복제하며, 배치도에 없는 남는 것은 지운다.
// 비닐하우스 안(이랑·덩굴·포도 시렁·안의 과일)과 딸기 집은 건드리지 않는다.
public static class P92PropLayout
{
    private const string Hob = "Assets/04. Prefabs/Environment/HeavenOfBakery/";
    private const string Farm = "Assets/04. Prefabs/Escape/P90/Farm/";
    private const float IslandR = 81f, PlazaR = 20f;
    private const int Seed = 9210;

    private struct Slot
    {
        public string Prefab;   // 파일 이름(확장자 없이)
        public Vector3 At;
        public float Yaw, Scale, Radius;
    }

    private struct Item
    {
        public string Prefab;
        public Vector2 Offset;   // 무리 가운데 기준(x, z)
        public float Yaw, Scale;
        public bool FaceCenter, Core;   // Core: 무리가 놓이려면 반드시 들어가야 하는 것
        public float Radius;            // 0이 아니면 간격 반지름(나무: 줄기 둘레 — 잎은 서로 겹쳐도 된다)
    }

    private static readonly Dictionary<string, float> radius1 = new Dictionary<string, float>();   // 크기 1 기준 바닥 반지름
    private static readonly List<Slot> slots = new List<Slot>();
    private static readonly List<Rect> pathRects = new List<Rect>();
    private static readonly List<(Vector2 p, float r)> keep = new List<(Vector2, float)>();
    private static readonly List<Rect> blocks = new List<Rect>();
    private static Vector2[] exits;
    private static System.Random rng;
    private static Transform ground;

    [MenuItem("Tools/TagOfChaos/Maps/Heaven Of Bakery/§92 Prop Layout (approved drawing)")]
    public static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[P92PropLayout] Stop Play Mode first."); return; }
        Debug.Log(Apply(true));
    }

    public static string Apply(bool save)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Maps/HeavenOfBakery.unity", OpenSceneMode.Single);
        Transform map = GameObject.Find("Map").transform;
        Transform deco = map.Find("FieldDeco");
        ground = map.Find("Ground");
        rng = new System.Random(Seed);
        why.Clear(); slots.Clear(); pathRects.Clear(); keep.Clear(); blocks.Clear(); radius1.Clear();

        // ---- 그대로 둘 것: 비닐하우스와 그 안, 딸기 집
        var greenhouses = deco.Cast<Transform>().Where(t => t.name.StartsWith("HOB_Greenhouse")).ToList();
        var ghBounds = greenhouses.Select(Bounds).ToList();
        bool InGreenhouse(Transform t) => ghBounds.Any(b => Mathf.Abs(t.position.x - b.center.x) < b.extents.x && Mathf.Abs(t.position.z - b.center.z) < b.extents.z);
        var fixedProps = new HashSet<Transform>(deco.Cast<Transform>().Where(t => t.name.StartsWith("HOB_Greenhouse") || t.name.StartsWith("HOB_House_") || InGreenhouse(t)));
        var movable = deco.Cast<Transform>().Where(t => !fixedProps.Contains(t)).ToList();
        foreach (Transform t in movable) Measure(t);

        // ---- 막힌 곳
        Transform paths = map.Find("Paths");
        foreach (Renderer r in paths.GetComponentsInChildren<Renderer>()) pathRects.Add(Flat(r.bounds, 0.4f));
        foreach (Bounds b in ghBounds) blocks.Add(Flat(b, 1.5f));
        foreach (Transform t in fixedProps.Where(t => t.name.StartsWith("HOB_House_"))) blocks.Add(Flat(Bounds(t), 1.5f));
        Transform lots = map.Find("Lots");
        if (lots != null) foreach (Transform lot in lots) blocks.Add(Flat(Bounds(lot), 0.3f));   // 땅 58 m 안에 건물은 51 m — 테두리에 붙어도 된다
        Transform fb = map.Find("FieldBuildings");
        if (fb != null) foreach (Transform b in fb) blocks.Add(Flat(Bounds(b), 2f));
        GameObject esc = GameObject.Find("EscapeSetup");
        foreach (Transform c in esc.transform) if (c.name.StartsWith("CHEST_Slot_")) keep.Add((V2(c.position), 3f));
        foreach (Transform m in map.Find("Markers"))
            keep.Add((V2(m.position), m.name.StartsWith("RocketSpawn") ? 4f : 3f));
        exits = map.Find("Exits").Cast<Transform>().Select(e => V2(e.position)).ToArray();
        foreach (Transform r in ground.Find("Ramps")) keep.Add((V2(r.position), 12f));
        Transform falls = map.Find("Background/Waterfalls");
        if (falls != null) foreach (Transform w in falls) keep.Add((V2(w.position), 6f));

        // 옮길 것은 잠시 꺼서 땅·충돌 검사에 안 걸리게 한다.
        foreach (Transform t in movable) t.gameObject.SetActive(false);
        Physics.SyncTransforms();

        var log = new List<string>();
        try
        {
            log.Add(Gardens());
            log.Add(Gatekeepers());
            log.Add(Market());
            log.Add(Groves());
            log.Add(Junctions());
            log.Add(Baskets(greenhouses));
            log.Add(RimBand());
        }
        finally
        {
            foreach (Transform t in movable) if (t != null) t.gameObject.SetActive(true);
        }

        // ---- 있는 것 먼저 쓰고, 모자라면 복제, 남으면 지운다.
        StaticEditorFlags flags = movable.Count > 0 ? GameObjectUtility.GetStaticEditorFlags(movable[0].gameObject) : 0;
        var pool = movable.GroupBy(t => Source(t)).ToDictionary(g => g.Key, g => new Queue<Transform>(g.OrderBy(t => t.name)));
        int moved = 0, cloned = 0, removed = 0;
        foreach (Slot s in slots)
        {
            Transform t;
            if (pool.TryGetValue(s.Prefab, out Queue<Transform> q) && q.Count > 0) { t = q.Dequeue(); moved++; }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(s.Prefab));
                if (prefab == null) { log.Add("missing prefab " + s.Prefab); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, deco);
                go.name = GameObjectUtility.GetUniqueNameForSibling(deco, prefab.name);
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                t = go.transform; cloned++;
            }
            t.SetPositionAndRotation(s.At, Quaternion.Euler(0f, s.Yaw, 0f));
            t.localScale = Vector3.one * s.Scale;
        }
        var left = new List<string>();
        foreach (var kv in pool)
            while (kv.Value.Count > 0) { Transform t = kv.Value.Dequeue(); left.Add(t.name); Object.DestroyImmediate(t.gameObject); removed++; }
        Physics.SyncTransforms();

        string bake = DecoyNavMeshBaker.BakeScene(scene) + " | " + GiantNavMeshBaker.BakeScene(scene, null);
        EditorSceneManager.MarkSceneDirty(scene);
        if (save) EditorSceneManager.SaveScene(scene);
        string report = $"[P92PropLayout] slots {slots.Count} (moved {moved}, cloned {cloned}, removed {removed}: {string.Join(", ", left)}) | {string.Join(" | ", log)} | why {string.Join(", ", why.OrderByDescending(k => k.Value).Take(14).Select(k => k.Key + "=" + k.Value))} | {bake}";
        return report;
    }

    // ---------------------------------------------------------------- ① 과일 정원 7곳
    private static readonly string[] SmallA = { "HOB_Fruit_Strawberry", "HOB_Fruit_Apple", "HOB_Fruit_Orange", "HOB_Fruit_Pumpkin" };
    private static readonly string[] SmallB = { "HOB_Fruit_Grapes", "HOB_Fruit_Strawberry", "HOB_Fruit_Cherries" };

    private static string Gardens()
    {
        float[] radial = RadialAngles();
        (string prefab, float scale)[] big = { ("HOB_Strawberry", 1f), ("HOB_Apple", 1f), ("HOB_Grape", 1f), ("HOB_Fruit_Orange", 1.8f), ("HOB_Watermelon", 0.5f), ("HOB_Strawberry", 1f), ("HOB_Apple", 1f) };
        int ok = 0;
        for (int i = 0; i < radial.Length; i++)
        {
            float a0 = radial[i], a1 = radial[(i + 1) % radial.Length] + (i == radial.Length - 1 ? 360f : 0f);
            float m = (a0 + a1) * 0.5f * Mathf.Deg2Rad;
            var items = new List<Item>();
            (string bp, float bs) = big[i % big.Length];
            float br = R(bp, bs);
            items.Add(new Item { Prefab = bp, Scale = bs, Yaw = rng.Next(360), Core = true });
            string sa = SmallA[rng.Next(SmallA.Length)], sb = SmallB[rng.Next(SmallB.Length)];
            float off = br + Mathf.Max(R(sa, 1f), R(sb, 1f)) + 0.6f;
            items.Add(new Item { Prefab = sa, Scale = 1f, Offset = Dir(m + 1.6f) * off, Yaw = rng.Next(360), Core = true });
            items.Add(new Item { Prefab = sb, Scale = 1f, Offset = Dir(m - 1.6f) * off, Yaw = rng.Next(360), Core = true });
            float ring = off + 0.4f;
            for (int k = 0; k < 8; k++)
            {
                float a = m + Mathf.PI + (k - 3.5f) * 0.42f;   // 바깥 반 바퀴는 비우고 안쪽(길 쪽)으로 꽃 둘레
                items.Add(new Item { Prefab = k % 2 == 0 ? "HOB_FlowerBed" : "HOB_FlowerBush", Scale = 0.8f, Offset = Dir(a) * (ring + 1.6f), Yaw = rng.Next(360) });
            }
            if (Group(Dir(m) * 34f, items, 20f, false)) ok++;
        }
        return $"gardens {ok}/{radial.Length}";
    }

    // ---------------------------------------------------------------- ② 탈출구 문지기 딸기
    private static readonly List<string> gateWhy = new List<string>();

    private static string Gatekeepers()
    {
        gateWhy.Clear();
        // 문 양옆(안쪽 6 m·옆 6 m)이 기본. 상자·하우스·잼 공장 땅에 막히면 그 쪽 안에서 가장 가까운 빈자리(안쪽 4~15 m, 옆 3.5~10 m).
        var tries = new List<Vector2>();
        for (float i = 4f; i <= 16f; i += 0.5f) for (float t = 3.5f; t <= 11f; t += 0.5f) tries.Add(new Vector2(i, t));
        tries = tries.OrderBy(v => (v - new Vector2(6f, 6f)).sqrMagnitude).ToList();
        int ok = 0;
        foreach (Vector2 e in exits)
        {
            Vector2 inw = -e.normalized, tan = new Vector2(-inw.y, inw.x);
            int both = 0;
            foreach (int s in new[] { -1, 1 })
            {
                var before = new Dictionary<string, int>(why);
                bool done = false;
                foreach (Vector2 t in tries)
                {
                    Vector2 p = e + inw * t.x + tan * (t.y * s);
                    if (!Place(Make("HOB_Strawberry", p, rng.Next(360), 0.8f), true, false)) continue;
                    both++;
                    Place(Make("HOB_FlowerBush", p + inw * 2.8f - tan * (1.2f * s), rng.Next(360), 1f), true, true);
                    Place(Make("HOB_FlowerBed", p + inw * 1.0f + tan * (2.8f * s), rng.Next(360), 1f), true, true);
                    done = true;
                    break;
                }
                if (!done) gateWhy.Add(e + "/" + s + ":" + string.Join(",", why.Where(k => !before.TryGetValue(k.Key, out int v) || v < k.Value).Select(k => k.Key)));
            }
            if (both == 2) ok++;
        }
        return $"gatekeepers {ok}/{exits.Length} {string.Join(" ; ", gateWhy)}";
    }

    private static Slot Make(string prefab, Vector2 p, float yaw, float scale) =>
        new Slot { Prefab = prefab, At = new Vector3(p.x, 0, p.y), Yaw = yaw, Scale = scale, Radius = R(prefab, scale) };

    // ---------------------------------------------------------------- ③ 작은 장터
    private static string Market()
    {
        var items = new List<Item>();
        foreach (Vector2 o in new[] { new Vector2(-6, 4), new Vector2(0, 6), new Vector2(6, 4) })
            items.Add(new Item { Prefab = "HOB_MarketStall", Scale = 1f, Offset = o, FaceCenter = true, Core = true });
        foreach (Vector2 o in new[] { new Vector2(-7, -2), new Vector2(7, -2), new Vector2(0, -5) })
            items.Add(new Item { Prefab = "HOB_FruitCart", Scale = 1f, Offset = o, FaceCenter = true, Core = true });
        foreach (Vector2 o in new[] { new Vector2(-3, -0.5f), new Vector2(3, -0.5f) })
            items.Add(new Item { Prefab = "HOB_Bench", Scale = 1f, Offset = o, Yaw = 90f, Core = true });
        return Group(new Vector2(-44, -44), items, 12f, false) ? "market ok" : "market FAILED";
    }

    // ---------------------------------------------------------------- ④ 바나나 숲 6곳(5그루씩)
    private static string Groves()
    {
        // (14,-76)은 잼 공장 자리(판마다 건물 놓이는 땅)라 같은 남쪽 가장자리의 (56,-54)로. 가장자리 숲은 섬 끝(81 m) 안으로 당김.
        Vector2[] at = { new Vector2(-38, 60), new Vector2(46, 62), new Vector2(76, -18), new Vector2(56, -54), new Vector2(-54, -54), new Vector2(-72, -12) };   // 섬 가장자리(81 m) 안
        int ok = 0, g = 0;
        foreach (Vector2 c in at)
        {
            var items = new List<Item>();
            for (int k = 0; k < 5; k++)
            {
                string p = (k + g) % 2 == 0 ? "HOB_BananaTree_A" : "HOB_BananaTree_B";
                float s = (p.EndsWith("A") ? 0.9f : 1.0f) + (float)rng.NextDouble() * 0.15f;
                items.Add(new Item { Prefab = p, Scale = s, Offset = Dir(k * 1.2566f + 0.3f + g) * 5.2f, Yaw = rng.Next(360), Core = k < 3, Radius = 1.7f * s });
            }
            for (int k = 0; k < 4; k++)
                items.Add(new Item { Prefab = k % 2 == 0 ? "HOB_Bush_Round" : "HOB_Bush_Berry", Scale = 1f, Offset = Dir(k * 1.5708f + 0.8f + g) * 9.4f, Yaw = rng.Next(360) });
            if (Group(c, items, 22f, false) || Group(c, items, 60f, false)) ok++;   // 가까이 빈 땅이 없으면 섬 안 가장 가까운 빈 곳
            g++;
        }
        return $"groves {ok}/{at.Length}";
    }

    // ---------------------------------------------------------------- ⑦ 갈림길 덤불 2 + 꽃덤불 1(컵케이크는 뺌 — 남은 컵케이크는 아래 정리에서 지워진다)
    private static string Junctions()
    {
        // 고리 길(반지름 22~27 m, 두 줄)이 방사 길과 만나는 곳 바로 바깥, 길 양옆 가로등보다 조금 더 옆.
        int n = 0, k = 0;
        foreach (float deg in RadialAngles())
        {
            float a = deg * Mathf.Deg2Rad;
            Vector2 rad = Dir(a), tan = new Vector2(-rad.y, rad.x), c = rad * 29.2f;
            if (Place(Make("HOB_Bush_Round", c + tan * 5.4f, rng.Next(360), 1f), false, true)) n++;
            if (Place(Make("HOB_Bush_Round", c - tan * 5.4f, rng.Next(360), 1f), false, true)) n++;
            // if (Place(Make(k % 2 == 0 ? "HOB_Cupcake_Cream" : "HOB_Cupcake_Pink", c + tan * 8.2f - rad * 0.6f, rng.Next(360), 1f), false, true)) n++;
            if (Place(Make("HOB_FlowerBush", c + tan * 8.2f - rad * 0.6f, rng.Next(360), 1f), false, true)) n++;   // 컵케이크는 뺀다(사용자 요청)
            k++;
        }
        return $"junctions {n}/21";
    }

    // ---------------------------------------------------------------- ⑥ 하우스 입구 바구니(같은 과일)
    private static string Baskets(List<Transform> greenhouses)
    {
        int n = 0;
        foreach (Transform gh in greenhouses)
        {
            Bounds b = Bounds(gh);
            bool alongZ = b.size.z > b.size.x;
            string fruit = Theme(gh);
            float scale = fruit == "HOB_Watermelon" ? 0.2f : 1f;
            foreach (int end in new[] { -1, 1 })
                {
                    int side = end;   // 끝마다 문 옆 한 자리(대각선 두 자리) — 문 앞은 비운다
                    Vector2 c = V2(b.center);
                    Vector2 p = alongZ ? c + new Vector2(side * 5.2f, end * (b.extents.z + 3.4f)) : c + new Vector2(end * (b.extents.x + 3.4f), side * 5.2f);
                    if (Place(new Slot { Prefab = fruit, At = new Vector3(p.x, 0, p.y), Yaw = rng.Next(360), Scale = scale, Radius = R(fruit, scale) }, false, true)) n++;
                }
        }
        return $"baskets {n}/6";
    }

    private static string Theme(Transform gh)
    {
        Bounds b = Bounds(gh);
        foreach (Transform t in gh.parent)
        {
            if (t == gh || Mathf.Abs(t.position.x - b.center.x) > b.extents.x || Mathf.Abs(t.position.z - b.center.z) > b.extents.z) continue;
            if (t.name.StartsWith("HOB_StrawberryRow")) return "HOB_Fruit_Strawberry";
            if (t.name.StartsWith("HOB_MelonVineRow")) return "HOB_Watermelon";
            if (t.name.StartsWith("HOB_GrapeTrellis")) return "HOB_Fruit_Grapes";
        }
        return "HOB_Fruit_Apple";
    }

    // ---------------------------------------------------------------- ⑤ 울타리 안쪽 꽃 띠(빈 가장자리만)
    private static string RimBand()
    {
        string[] cycle = { "HOB_FlowerBush", "HOB_Bush_Berry", "HOB_FlowerBed", "HOB_Bush_Berry", "HOB_FlowerBush", "HOB_RockMushroom", "HOB_FlowerBed", "HOB_Bush_Round", "HOB_FlowerBush", "HOB_RockBush" };
        int n = 0, k = 0;
        for (float deg = 0f; deg < 360f; deg += 7.5f)
        {
            Vector2 p = Dir(deg * Mathf.Deg2Rad) * 79f;
            if (exits.Any(e => (e - p).magnitude < 13f)) continue;
            string prefab = cycle[k % cycle.Length];
            if (Place(new Slot { Prefab = prefab, At = new Vector3(p.x, 0, p.y), Yaw = rng.Next(360), Scale = 1f, Radius = R(prefab, 1f) }, false, true)) { n++; k++; }
            else if (Place(new Slot { Prefab = prefab, At = new Vector3(p.x * 0.95f, 0, p.y * 0.95f), Yaw = rng.Next(360), Scale = 1f, Radius = R(prefab, 1f) }, false, true)) { n++; k++; }
        }
        return $"rim {n}";
    }

    // ---------------------------------------------------------------- 무리 놓기: 가운데를 소용돌이로 비켜 가며 Core가 모두 들어가는 첫 자리
    private static bool Group(Vector2 center, List<Item> items, float search, bool nearExit)
    {
        int rings = Mathf.CeilToInt(search / 1.5f);
        for (int ri = 0; ri <= rings; ri++)
        {
            int steps = ri == 0 ? 1 : 8 + ri * 4;
            for (int si = 0; si < steps; si++)
            {
                Vector2 c = center + (ri == 0 ? Vector2.zero : Dir(si * Mathf.PI * 2f / steps) * (ri * 1.5f));
                var want = items.Select(it => ToSlot(it, c)).ToList();
                int mark = slots.Count;
                bool all = true;
                for (int i = 0; i < items.Count && all; i++)
                    if (items[i].Core && !Place(want[i], nearExit, false)) all = false;
                if (!all) { slots.RemoveRange(mark, slots.Count - mark); continue; }
                for (int i = 0; i < items.Count; i++)
                    if (!items[i].Core) Place(want[i], nearExit, true);
                return true;
            }
        }
        return false;
    }

    private static Slot ToSlot(Item it, Vector2 c)
    {
        Vector2 p = c + it.Offset;
        float yaw = it.Yaw;
        if (it.FaceCenter && it.Offset.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(-it.Offset.x, -it.Offset.y) * Mathf.Rad2Deg;
        return new Slot { Prefab = it.Prefab, At = new Vector3(p.x, 0, p.y), Yaw = yaw, Scale = it.Scale, Radius = it.Radius > 0f ? it.Radius : R(it.Prefab, it.Scale) };
    }

    // 자리가 비었으면 slots에 넣고 true. nudge면 1.2 m 안에서 조금 비켜 본다(작은 장식).
    private static bool Place(Slot s, bool nearExit, bool nudge)
    {
        for (int t = 0; t < (nudge ? 9 : 1); t++)
        {
            Slot c = s;
            if (t > 0) { Vector2 d = Dir(t * 0.785f) * 1.2f; c.At += new Vector3(d.x, 0, d.y); }
            if (!Free(c, nearExit, out float y)) continue;
            c.At.y = y;
            slots.Add(c);
            return true;
        }
        return false;
    }

    private static readonly Dictionary<string, int> why = new Dictionary<string, int>();
    private static bool No(string reason) { why[reason] = why.TryGetValue(reason, out int v) ? v + 1 : 1; return false; }

    // 시험·진단: 마지막 Apply의 막힌 곳 기준으로 한 자리가 왜 막혔는지(다른 장식과의 간격은 빼고).
    public static string Probe(float x, float z, float r, bool nearExit, bool withSlots = false)
    {
        var saved = new List<Slot>(slots); if (!withSlots) slots.Clear(); why.Clear();
        bool ok = Free(new Slot { At = new Vector3(x, 0, z), Radius = r }, nearExit, out _);
        slots.Clear(); slots.AddRange(saved);
        return ok ? "free" : string.Join(",", why.Keys);
    }

    private static bool Free(Slot s, bool nearExit, out float y)
    {
        y = 0f;
        Vector2 p = V2(s.At);
        float r = s.Radius;
        if (p.magnitude + r > IslandR || p.magnitude - r < PlazaR) return No("radius");
        foreach (Rect q in pathRects) if (CircleRect(p, r, q)) return No("path");
        foreach (Rect q in blocks) if (CircleRect(p, r, q)) return No("block");
        foreach ((Vector2 kp, float kr) in keep) if ((kp - p).magnitude < kr + r) return No("keep");
        foreach (Vector2 e in exits) if ((e - p).magnitude < (nearExit ? 5.5f : 10f) + r) return No("exit");
        foreach (Slot o in slots) if ((V2(o.At) - p).magnitude < o.Radius + r + 0.4f) return No("slot:" + o.Prefab);
        // 땅: 본섬 바닥(높이 0 근처)이어야 하고, 발자리 위에 다른 충돌체가 없어야 한다.
        foreach (Vector2 d in new[] { Vector2.zero, new Vector2(r, 0), new Vector2(-r, 0), new Vector2(0, r), new Vector2(0, -r) })
        {
            Vector3 from = new Vector3(p.x + d.x * 0.8f, 40f, p.y + d.y * 0.8f);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) return No("noground");
            if (!hit.transform.IsChildOf(ground) || hit.transform.IsChildOf(ground.Find("Ramps")) || Mathf.Abs(hit.point.y) > 0.6f) return No("ground:" + hit.transform.name + "@" + hit.point.y.ToString("F1"));
            if (d == Vector2.zero) y = hit.point.y;
        }
        Collider[] over = Physics.OverlapBox(new Vector3(p.x, y + 2.2f, p.y), new Vector3(r, 1.8f, r), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        if (over.Length > 0) return No("over:" + over[0].name);
        // slot 진단: 어느 장식과 겹쳤는지
        return true;
    }

    // ---------------------------------------------------------------- 도움
    private static float[] RadialAngles() =>
        new[] { new Vector2(60, 34), new Vector2(-60, 34), new Vector2(-35, -60), new Vector2(35, -60), new Vector2(-70, -28), new Vector2(60, -46), new Vector2(10, 74) }
            .Select(v => (Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + 360f) % 360f).OrderBy(a => a).ToArray();

    private static void Measure(Transform t)
    {
        string k = Source(t);
        if (radius1.ContainsKey(k)) return;
        Bounds b = Bounds(t);
        radius1[k] = Mathf.Max(b.extents.x, b.extents.z) / Mathf.Max(0.01f, t.localScale.x) * 0.85f;   // 둥근 모양: 네모 꼭짓점까지는 안 잰다
    }

    private static float R(string prefab, float scale)
    {
        if (!radius1.TryGetValue(prefab, out float r))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prefab));
            if (go == null) return 1.5f * scale;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(go);
            try { tmp.transform.localScale = Vector3.one; Bounds b = Bounds(tmp.transform); r = Mathf.Max(b.extents.x, b.extents.z) * 0.85f; radius1[prefab] = r; }
            finally { Object.DestroyImmediate(tmp); }
        }
        return r * scale;
    }

    private static string Source(Transform t)
    {
        GameObject src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
        return src != null ? src.name : DestructibleCatalog.KindName(t.name);
    }

    private static string PrefabPath(string prefab) =>
        System.IO.File.Exists(Farm + prefab + ".prefab") ? Farm + prefab + ".prefab" : Hob + prefab + ".prefab";

    private static Bounds Bounds(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Rect Flat(Bounds b, float pad) => Rect.MinMaxRect(b.min.x - pad, b.min.z - pad, b.max.x + pad, b.max.z + pad);
    private static Vector2 V2(Vector3 v) => new Vector2(v.x, v.z);
    private static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));

    private static bool CircleRect(Vector2 p, float r, Rect q)
    {
        float dx = Mathf.Max(q.xMin - p.x, 0f, p.x - q.xMax), dz = Mathf.Max(q.yMin - p.y, 0f, p.y - q.yMax);
        return dx * dx + dz * dz < r * r;
    }
}
