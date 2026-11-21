using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Request1009Plan.md §92 (2026-11-22 요청) 시험.
public class Request1122Tests
{
    // ---------------- §92-2 거대 괴물이 경사로(다리)를 건넌다 ----------------

    private static GiantRampLinks.Ramp SampleRamp()
    {
        // 본섬(0,0,0) → 경사로 꺾임 3개 → 고원(0,30,40)
        return GiantRampLinks.EditorMakeRamp("Ramp_T", new[]
        {
            new Vector3(0, 0, 0), new Vector3(0, 0.1f, 3), new Vector3(0, 10, 25), new Vector3(10, 10, 27),
            new Vector3(10, 29.9f, 37), new Vector3(0, 30, 40),
        });
    }

    [Test]
    public void Expand_InsertsTheRampLineBetweenLinkEnds_BothWays()
    {
        var list = new List<GiantRampLinks.Ramp> { SampleRamp() };
        Vector3[] up = GiantRampLinks.Expand(new[] { new Vector3(-20, 0, 0), new Vector3(0.5f, 0, 0.3f), new Vector3(0.2f, 30, 39.6f), new Vector3(5, 30, 50) }, list);
        Assert.AreEqual(8, up.Length, "4 corners + 4 ramp points");
        Assert.AreEqual(new Vector3(0, 0.1f, 3), up[2]);
        Assert.AreEqual(new Vector3(10, 29.9f, 37), up[5]);
        Vector3[] down = GiantRampLinks.Expand(new[] { new Vector3(0, 30, 40), new Vector3(0, 0, 0) }, list);
        Assert.AreEqual(6, down.Length);
        Assert.AreEqual(new Vector3(10, 29.9f, 37), down[1], "walks the line backwards");
        Assert.AreEqual(new Vector3(0, 0.1f, 3), down[4]);
        Vector3[] other = { new Vector3(50, 0, 0), new Vector3(60, 0, 0) };
        Assert.AreEqual(2, GiantRampLinks.Expand(other, list).Length, "paths that do not use the link are unchanged");
    }

    [Test]
    public void OnRamp_FindsTheSegment_AndLeadsToEitherEnd()
    {
        var list = new List<GiantRampLinks.Ramp> { SampleRamp() };
        Assert.IsTrue(GiantRampLinks.OnRamp(new Vector3(5, 10, 26), list, out GiantRampLinks.Ramp r, out int seg));
        Assert.AreEqual(2, seg);
        List<Vector3> toTop = GiantRampLinks.LeadTo(r, seg, true);
        Assert.AreEqual(new Vector3(10, 10, 27), toTop[0]);
        Assert.AreEqual(r.Top, toTop[toTop.Count - 1]);
        List<Vector3> toBottom = GiantRampLinks.LeadTo(r, seg, false);
        Assert.AreEqual(new Vector3(0, 10, 25), toBottom[0]);
        Assert.AreEqual(r.Bottom, toBottom[toBottom.Count - 1]);
        Assert.IsFalse(GiantRampLinks.OnRamp(new Vector3(-30, 0, -30), list, out _, out _), "island ground is not the ramp");
        Assert.IsFalse(GiantRampLinks.OnRamp(new Vector3(0, 0, -1.5f), list, out _, out _), "the end stub on the island path is not the ramp");
    }

    [Test]
    public void Simplify_KeepsOnlyTheBends()
    {
        var pts = new List<Vector3>();
        for (int i = 0; i <= 20; i++) pts.Add(new Vector3(0, i * 0.4f, i));       // 곧은 오르막
        for (int i = 1; i <= 10; i++) pts.Add(new Vector3(i, 8f, 20));          // 평평한 층계참
        List<Vector3> s = GiantRampLinks.Simplify(pts, 0.4f);
        Assert.AreEqual(3, s.Count);
        Assert.AreEqual(new Vector3(0, 8, 20), s[1]);
    }

    [Test]
    public void GiantState_CarriesALongRampRoute()
    {
        Assert.GreaterOrEqual(GiantState.MaxPoints, 24);
        var pts = new Vector3[GiantState.MaxPoints];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(i, i * 0.5f, 0);
        var s = new GiantState { Points = pts, MoveAt = 1 };
        Assert.IsTrue(GiantState.TryDecode(s.Encode(), out GiantState d));
        Assert.AreEqual(GiantState.MaxPoints, d.Points.Length);
        Assert.AreEqual(pts[pts.Length - 1], d.Points[pts.Length - 1]);
    }

    // ---------------- §92-5 순간이동: 문(뚜껑)이 열린 뒤에 술래가 나온다 ----------------

    [Test]
    public void Hatch_MonsterWaitsBelowAndRisesOnlyAfterTheLidOpens()
    {
        var w = new EscapeState.Warp { Actor = 1, Building = 0, At = 100 };
        double open = MonsterWarpRules.OpenAt(w);
        Assert.IsTrue(MonsterWarpRules.BodyHidden(w, 100.5, true), "rattling: hidden");
        Assert.IsTrue(MonsterWarpRules.BodyHidden(w, open + 0.1, true), "lid just started opening: still hidden below");
        Assert.IsFalse(MonsterWarpRules.BodyHidden(w, open + MonsterWarpRules.RiseDelay + 0.01, true));
        Assert.IsFalse(MonsterWarpRules.BodyHidden(w, open + 0.1, false), "closet doors hide the body themselves");
        Assert.AreEqual(0f, MonsterWarpRules.Rise01(w, open + 0.1), 1e-6f);
        Assert.AreEqual(1f, MonsterWarpRules.Rise01(w, MonsterWarpRules.RiseEndAt(w)), 1e-6f);
        float mid = MonsterWarpRules.Rise01(w, open + MonsterWarpRules.RiseDelay + MonsterWarpRules.RiseSeconds * 0.5);
        Assert.That(mid, Is.InRange(0.3f, 0.7f));
        Assert.Greater(MonsterWarpRules.DoorOpen(w, open + MonsterWarpRules.RiseDelay), 0.3f, "lid is well open before the body shows");
        Assert.AreEqual(MonsterWarpRules.RiseEndAt(w), MonsterWarpRules.LockUntil(w, true), 1e-9, "cannot walk until fully up");
        Assert.AreEqual(MonsterWarpRules.DoneAt(w), MonsterWarpRules.LockUntil(w, false), 1e-9);
        Assert.Less(MonsterWarpRules.RiseEndAt(w), MonsterWarpRules.RemoveAt(w), "rise ends before the warp is cleared");
    }

    // ---------------- §92-10 본섬 사물 배치(배치도) · 컵케이크 뺌 ----------------

    private static Transform OpenFieldDeco(out List<Transform> props)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Maps/HeavenOfBakery.unity", OpenSceneMode.Single);
        Transform deco = GameObject.Find("Map").transform.Find("FieldDeco");
        props = deco.Cast<Transform>().ToList();
        return deco;
    }

    [Test]
    public void PropLayout_HasNoCupcakes_AndKeepsTheDrawingGroups()
    {
        OpenFieldDeco(out List<Transform> props);
        Assert.IsFalse(props.Any(t => t.name.Contains("Cupcake")), "cupcakes are removed");
        Assert.GreaterOrEqual(props.Count(t => t.name.StartsWith("HOB_BananaTree")), 24, "6 banana groves");
        Assert.AreEqual(3, props.Count(t => t.name.StartsWith("HOB_Greenhouse")), "greenhouses stay");
        Assert.AreEqual(1, props.Count(t => t.name.StartsWith("HOB_House_Strawberry")), "strawberry house stays");
        // 탈출구마다 문지기 딸기 2개
        foreach (Transform exit in GameObject.Find("Map").transform.Find("Exits"))
        {
            Vector3 e = exit.position;
            int n = props.Count(t => t.name.StartsWith("HOB_Strawberry") && !t.name.StartsWith("HOB_StrawberryRow") && t.position.y < 0.2f
                                     && Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(e.x, e.z)) < 18f);
            Assert.GreaterOrEqual(n, 2, exit.name + " gatekeepers");
        }
        // 작은 장터: 노점 3 · 수레 3 · 벤치 2가 한데(지름 30 m 안)
        var market = props.Where(t => t.name.StartsWith("HOB_MarketStall") || t.name.StartsWith("HOB_FruitCart") || t.name.StartsWith("HOB_Bench")).ToList();
        Assert.AreEqual(8, market.Count);
        Vector3 c = market.Aggregate(Vector3.zero, (a, t) => a + t.position) / market.Count;
        Assert.IsTrue(market.All(t => (t.position - c).magnitude < 15f), "market is one cluster");
    }

    [Test]
    public void PropLayout_LeavesChestsExitsAndPathsClear()
    {
        Transform deco = OpenFieldDeco(out List<Transform> props);
        var greenhouses = props.Where(t => t.name.StartsWith("HOB_Greenhouse")).Select(t => t.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; })).ToList();
        bool Inside(Transform t) => greenhouses.Any(b => Mathf.Abs(t.position.x - b.center.x) < b.extents.x && Mathf.Abs(t.position.z - b.center.z) < b.extents.z);
        var outdoor = props.Where(t => !t.name.StartsWith("HOB_Greenhouse") && !t.name.StartsWith("HOB_House_") && !Inside(t)).ToList();
        Vector2 F(Vector3 v) => new Vector2(v.x, v.z);
        foreach (Transform chest in GameObject.Find("EscapeSetup").transform.Cast<Transform>().Where(t => t.name.StartsWith("CHEST_Slot_")))
            foreach (Transform t in outdoor)
                Assert.GreaterOrEqual((F(t.position) - F(chest.position)).magnitude, 3f, $"{t.name} too close to {chest.name}");
        foreach (Transform exit in GameObject.Find("Map").transform.Find("Exits"))
            foreach (Transform t in outdoor)
                Assert.GreaterOrEqual((F(t.position) - F(exit.position)).magnitude, 5.5f, $"{t.name} blocks {exit.name}");
        var tiles = GameObject.Find("Map").transform.Find("Paths").GetComponentsInChildren<Renderer>().Where(r => r.bounds.size.y < 1f).Select(r => r.bounds).ToList();
        foreach (Transform t in outdoor)
            Assert.IsFalse(tiles.Any(b => Mathf.Abs(t.position.x - b.center.x) < b.extents.x && Mathf.Abs(t.position.z - b.center.z) < b.extents.z), t.name + " stands on a path tile");
        Assert.IsNotNull(deco);
    }

    // ---------------- §92-4 물풍선: 손에 든 것·날아가는 것 모두 물이 든 새 모델 ----------------

    [Test]
    public void WaterBalloon_UsesTheWaterFilledModel_InHandAndInFlight()
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/03. SO/Escape/Items/Item_WaterBalloon.asset");
        Assert.IsNotNull(item.ModelPrefab);
        Assert.AreEqual("ITEM_WaterBalloon", item.ModelPrefab.name);
        var names = item.ModelPrefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).ToList();
        CollectionAssert.Contains(names, "ME_BalloonWater", "water inside the skin");
        CollectionAssert.Contains(names, "ME_BalloonSkin");
        var parent = new GameObject("_BalloonTest").transform;
        try
        {
            GameObject held = EscapeVisuals.CreateItemModel(item, parent);
            Assert.IsTrue(held.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(m => m != null && m.name == "ME_BalloonWater")));
            Assert.AreEqual(0, held.GetComponentsInChildren<Collider>().Length);
        }
        finally { Object.DestroyImmediate(parent.gameObject); }
        string held2 = System.IO.File.ReadAllText("Assets/02. Scripts/Escape/HeldItemPresenter.cs");
        string proj = System.IO.File.ReadAllText("Assets/02. Scripts/Escape/Projectile/ProjectileSystem.cs");
        Assert.IsFalse(held2.Split('\n').Any(l => l.Contains("WaterBubbleView.CreateSmall") && !l.TrimStart().StartsWith("//")), "held balloon is not the old bubble");
        Assert.IsFalse(proj.Split('\n').Any(l => l.Contains("WaterBubbleView.CreateSmall") && !l.TrimStart().StartsWith("//")), "thrown balloon is not the old bubble");
        Assert.Greater(ProjectileSystem.BalloonFlightScale, 1f);
    }
}
