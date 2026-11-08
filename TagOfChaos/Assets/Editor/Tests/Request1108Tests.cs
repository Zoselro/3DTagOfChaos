using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Request1009Plan.md §66 거대 괴물이 건물·오브젝트를 부순다 — 시험(GD8).
public class Request1108Tests
{
    // ---------------- 상태(방 속성) ----------------

    [Test]
    public void State_PacksFourBitsPerObject()
    {
        var data = new byte[DestructionState.ByteLength(5)];
        Assert.AreEqual(3, data.Length);
        DestructionState.Set(data, 0, 1);
        DestructionState.Set(data, 1, 4);
        DestructionState.Set(data, 4, 2);
        Assert.AreEqual(1, DestructionState.Get(data, 0));
        Assert.AreEqual(4, DestructionState.Get(data, 1), "neighbours in the same byte stay apart");
        Assert.AreEqual(2, DestructionState.Get(data, 4));
        Assert.AreEqual(0, DestructionState.Get(data, 3));
        DestructionState.Set(data, 2, 99);
        Assert.AreEqual(DestructionState.MaxLevel, DestructionState.Get(data, 2), "clamped");
        Assert.AreEqual(0, DestructionState.Get(data, 40), "out of range reads 0");
        byte[] grown = DestructionState.Resize(data, 9);
        Assert.AreEqual(5, grown.Length);
        Assert.AreEqual(4, DestructionState.Get(grown, 1));
        Assert.Less(DestructionState.ByteLength(800), 450, "small room property for a whole map");
    }

    [Test]
    public void Grades_BreakAfterOneTwoFourHits()
    {
        Assert.AreEqual(1, new DestructibleUnit { Grade = DestructibleGrade.Prop }.HitsToBreak);
        Assert.AreEqual(2, new DestructibleUnit { Grade = DestructibleGrade.Tent }.HitsToBreak);
        var b = new DestructibleUnit { Grade = DestructibleGrade.Building, Level = 3 };
        Assert.IsFalse(b.Broken);
        b.Level = 4;
        Assert.IsTrue(b.Broken);
    }

    [Test]
    public void NetKey_IsMasterOnlyAndClearedEveryRound()
    {
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.Destruction);
        Assert.IsTrue(NetKeys.IsMasterKey(NetKeys.Destruction, NetKeys.Target.Room));
    }

    // ---------------- 사람 판정·어그로 ----------------

    private static DestructibleUnit Box(DestructibleGrade g, Vector3 center, Vector3 size, int level = 0) =>
        new DestructibleUnit { Grade = g, Bounds = new Bounds(center, size), Zone = new Bounds(center, size), Level = level, Colliders = new Collider[0], Renderers = new Renderer[0] };

    [Test]
    public void Walls_ProtectOnlyWhileStanding()
    {
        var house = Box(DestructibleGrade.Building, new Vector3(0, 5, 0), new Vector3(20, 10, 20));
        var units = new List<DestructibleUnit> { house, Box(DestructibleGrade.Prop, new Vector3(30, 1, 0), new Vector3(4, 2, 4)) };
        Assert.IsTrue(GiantDestruction.IsProtected(new Vector3(2, 0.1f, 2), units, out DestructibleUnit s));
        Assert.AreSame(house, s);
        Assert.IsFalse(GiantDestruction.IsProtected(new Vector3(9.9f, 0.1f, 0), units, out _), "standing in the doorway is not inside");
        Assert.IsFalse(GiantDestruction.IsProtected(new Vector3(30, 0.1f, 0), units, out _), "props never protect");
        house.Level = 3;
        Assert.IsTrue(GiantDestruction.IsProtected(new Vector3(2, 0.1f, 2), units, out _), "the collapsing stomp is judged before the hit");
        house.Level = 4;
        Assert.IsFalse(GiantDestruction.IsProtected(new Vector3(2, 0.1f, 2), units, out _), "exposed after the collapse");
    }

    [Test]
    public void Aggro_AddsTheTimeToBreakTheShelter()
    {
        var tent = Box(DestructibleGrade.Tent, Vector3.up * 2, new Vector3(8, 4, 8), 1);
        var units = new List<DestructibleUnit> { tent };
        float perHit = (GiantRules.StompSeconds + GiantRules.StompCooldown) * 10f;
        Assert.AreEqual(perHit, GiantDestruction.AggroExtra(Vector3.zero, units, 10f), 1e-4f, "one hit left at 10 m/s");
        Assert.AreEqual(0f, GiantDestruction.AggroExtra(new Vector3(20, 0, 0), units, 10f), "outside costs nothing");
        Assert.Greater(GiantDestruction.Ahead(12f), GiantDestruction.Ahead(3f), "looks further ahead when faster");
    }

    // ---------------- 계단(§68 GP6)·10초 포기(§65) ----------------

    [Test]
    public void Stairs_TakeTenHitsAndCoverTheLinkedTower()
    {
        var stairs = new DestructibleUnit { Grade = DestructibleGrade.Stairs, Bounds = new Bounds(new Vector3(0, -18, 0), new Vector3(30, 36, 30)), Colliders = new Collider[0], Renderers = new Renderer[0] };
        stairs.Zone = stairs.Bounds;
        stairs.Zone.Encapsulate(new Bounds(new Vector3(5, 9, -18), new Vector3(19, 27, 12))); // 이어진 시계탑
        Assert.AreEqual(10, stairs.HitsToBreak);
        var data = new byte[1];
        DestructionState.Set(data, 0, 10);
        Assert.AreEqual(10, DestructionState.Get(data, 0), "10 fits in four bits");
        var units = new List<DestructibleUnit> { stairs };
        Assert.AreSame(stairs, GiantDestruction.StairsOf(new Vector3(5, 1, -18), units), "inside the tower counts as the stairs");
        Assert.AreSame(stairs, GiantDestruction.StairsOf(new Vector3(0, -30, 0), units), "down the spiral too");
        Assert.IsNull(GiantDestruction.StairsOf(new Vector3(60, 0.5f, 0), units));
        Assert.IsTrue(GiantDestruction.IsProtected(new Vector3(5, 1, -18), units, out _), "the stairs shelter while standing");
        Assert.IsTrue(GiantDestruction.CanHitStairs(new Vector3(5, 0.5f, -31), stairs), "giant on the ground just outside the tower");
        Assert.IsFalse(GiantDestruction.CanHitStairs(new Vector3(5, 0.5f, -50), stairs), "too far");
        stairs.Level = 10;
        Assert.IsNull(GiantDestruction.StairsOf(new Vector3(0, -30, 0), units), "broken stairs no longer count");
    }

    [Test]
    public void Patience_GivesUpAfterTenSecondsOfWaiting()
    {
        var p = new GiantPatience();
        Vector3 at = new Vector3(3, 0, 4);
        Assert.IsFalse(p.Tick(7, false, at, 0f), "walking is not waiting");
        Assert.IsFalse(p.Tick(7, true, at, 1f));
        Assert.IsFalse(p.Tick(7, true, at, 10.9f), "9.9 s");
        Assert.IsTrue(p.Tick(7, true, at, 11.1f), "10 s at the goal -> give up");
        Assert.IsTrue(p.IsIgnored(7, at, 12f));
        Assert.IsFalse(p.IsIgnored(8, at, 12f), "others are still chased");
        Assert.IsFalse(p.IsIgnored(7, at, 11.1f + GiantPatience.IgnoreSeconds + 0.1f), "ignored only for a while");

        var q = new GiantPatience();
        q.Tick(7, true, at, 0f);
        Assert.IsFalse(q.Tick(7, false, at, 5f), "moving or stomping resets the wait");
        Assert.IsFalse(q.Tick(7, true, at, 6f));
        Assert.IsFalse(q.Tick(7, true, at, 15f), "only 9 s since it started waiting again");
        Assert.IsTrue(q.Tick(7, true, at, 16.5f));
        Assert.IsFalse(q.IsIgnored(7, at + new Vector3(GiantPatience.MovedDistance + 1f, 0, 0), 17f), "moved away -> chase again");
    }

    [Test]
    public void Gingerbread_SpiralsAreStairsLinkedToTheClockTowers()
    {
        const string path = "Assets/Scenes/Maps/GingerbreadVillage.unity";
        Scene open = SceneManager.GetSceneByPath(path);
        bool was = open.IsValid() && open.isLoaded;
        Scene scene = was ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Transform map = DestructibleCatalog.FindMap(scene);
            DestructibleCatalog cat = DestructibleCatalog.Build(map, DestructionRulesSO.Current);
            int stairs = 0;
            foreach (DestructibleUnit u in cat.Units)
            {
                if (!u.Root.name.StartsWith("Helix_")) continue;
                stairs++;
                Assert.AreEqual(DestructibleGrade.Stairs, u.Grade, u.Root.name);
                Assert.IsTrue(u.HasLink, u.Root.name + " linked to a clock tower");
                Assert.IsTrue(u.Zone.Contains(u.Link.center) && u.Zone.Contains(u.Bounds.center));
            }
            Assert.AreEqual(2, stairs, "two spirals (11 and 5 o'clock towers)");
        }
        finally
        {
            if (!was) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ---------------- 맵별 고르기(규칙 에셋 + 실제 맵) ----------------

    [Test]
    public void Rules_HaveFracturedModelsAndCrackDecals()
    {
        DestructionRulesSO rules = DestructionRulesSO.Current;
        Assert.AreEqual(38, rules.Fractured.Length, "GD2: 38 fractured buildings");
        for (int stage = 1; stage <= 3; stage++)
        {
            Material m = rules.CrackMaterial(stage);
            Assert.IsNotNull(m, "crack " + stage);
            Assert.IsNotNull(m.mainTexture, "crack texture " + stage);
            Assert.IsNotNull(m.GetTexture("_BumpMap"), "crack normal " + stage);
        }
    }

    [TestCase("Assets/Scenes/Maps/GingerbreadVillage.unity", 20, 0)]
    [TestCase("Assets/Scenes/Maps/CursedCandyCarnival.unity", 8, 16)]
    [TestCase("Assets/Scenes/Maps/ChocolateFactory.unity", 8, 48)]
    [TestCase("Assets/Scenes/Maps/CandyForest.unity", 28, 21)]
    public void Catalog_PicksBuildingsTentsAndProps(string path, int buildings, int tents)
    {
        Scene open = SceneManager.GetSceneByPath(path);
        bool was = open.IsValid() && open.isLoaded;
        Scene scene = was ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            DestructibleCatalog a = DestructibleCatalog.Build(DestructibleCatalog.FindMap(scene), DestructionRulesSO.Current);
            DestructibleCatalog b = DestructibleCatalog.Build(DestructibleCatalog.FindMap(scene), DestructionRulesSO.Current);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreSame(a.Get(i).Root, b.Get(i).Root, "same numbering on every screen");

            int bld = 0, tent = 0, props = 0;
            foreach (DestructibleUnit u in a.Units)
            {
                if (u.Grade == DestructibleGrade.Building) { bld++; Assert.IsNotNull(u.FracturePrefab, u.Root.name + " has a fractured model"); }
                else if (u.Grade == DestructibleGrade.Tent) tent++;
                else if (u.Grade == DestructibleGrade.Stairs) continue;
                else props++;
                // 부서지지 않는 것
                StringAssert.DoesNotStartWith("GIN_ClockTower", u.Root.name);
                StringAssert.DoesNotStartWith("Exit_", u.Root.name);
                StringAssert.DoesNotStartWith("CoasterEntrance", u.Root.name);
                Assert.IsNull(u.Root.GetComponentInChildren<EscapeGate>(true), u.Root.name);
                Assert.IsNull(u.Root.GetComponentInChildren<MonsterElevator>(true), u.Root.name);
                // 재료 꽂는 곳·빛기둥·수리 자리는 숨기지 않는다
                foreach (Renderer r in u.Renderers)
                    for (Transform t = r.transform; t != null && t != u.Root; t = t.parent)
                    {
                        StringAssert.DoesNotStartWith("Socket_", t.name);
                        StringAssert.DoesNotStartWith("ESC_Glow", t.name);
                        StringAssert.DoesNotStartWith("Repair_Point", t.name);
                    }
            }
            Assert.AreEqual(buildings, bld, "buildings");
            Assert.AreEqual(tents, tent, "tents");
            Assert.Greater(props, 300, "props");
            Assert.Less(DestructionState.ByteLength(a.Count), 400);

        }
        finally
        {
            if (!was) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
