using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Request1009Plan.md §89.24·§89.25 — Heaven Of Bakery 하늘 대포(섬마다 2대·쿠키·AI만·한 대 한 명·착지 2초 뒤)와 이 맵 한정 콜라 대포 빼기.
public class Request1124Tests
{
    private const string ScenePath = "Assets/Scenes/Maps/HeavenOfBakery.unity";
    private const string HobRecipe = "Assets/03. SO/Escape/Recipes/Recipe_HeavenOfBakery.asset";

    private static EscapeState.Cannon Ride(int id, int gun, int owner, double boardAt, float flight) => new EscapeState.Cannon
    {
        Id = id, Owner = owner, Sky = (sbyte)(gun + 1), BoardAt = boardAt, SummonedAt = boardAt, Flight = flight, Dest = new Vector3(1, 48, 2),
    };

    [Test]
    public void Busy_UntilTwoSecondsAfterLanding()
    {
        var s = new EscapeState();
        s.Cannons.Add(Ride(3, 2, 7, 100, 6f));
        double land = 100 + ColaCannonRules.LandAt(6f);
        Assert.IsTrue(SkyCannonRules.Busy(s, 2, 100.1), "just boarded");
        Assert.IsTrue(SkyCannonRules.Busy(s, 2, land + 1.9), "inside the 2 s cooldown");
        Assert.IsFalse(SkyCannonRules.Busy(s, 2, land + SkyCannonRules.Cooldown + 0.01), "free after landing + 2 s");
        Assert.IsFalse(SkyCannonRules.Busy(s, 1, 101), "other cannons are free");
        Assert.AreEqual(2f, SkyCannonRules.Cooldown);
        Assert.IsTrue(SkyCannonRules.TryActive(s, 2, 103, out EscapeState.Cannon r) && r.Id == 3);
    }

    [Test]
    public void ColaCannonEntries_AreNotSky()
    {
        var cola = new EscapeState.Cannon { Id = 1, Owner = 2, SummonedAt = 5 };
        Assert.IsFalse(cola.IsSky, "default Sky 0 = cola cannon item (old code stays correct)");
        var s = new EscapeState();
        s.Cannons.Add(new EscapeState.Cannon { Id = 1, Owner = 2, SummonedAt = 5, BoardAt = 6, Flight = 3f });
        Assert.IsFalse(SkyCannonRules.Busy(s, 0, 7), "a boarded cola cannon is not sky cannon 0");
    }

    [Test]
    public void State_RoundTripsSkyField_AndLatestRideWins()
    {
        Assert.GreaterOrEqual(EscapeState.FormatVersion, 12, "v12 added Cannon.Sky");
        var s = new EscapeState();
        s.Cannons.Add(Ride(4, 5, 9, 10, 6.5f));
        s.Cannons.Add(Ride(8, 1, 9, 30, 6.6f));
        s.Cannons.Add(Ride(9, 3, -12, 31, 6.4f));
        EscapeState d = EscapeState.Decode(s.Encode());
        Assert.IsNotNull(d);
        Assert.AreEqual(3, d.Cannons.Count);
        Assert.AreEqual(6, d.Cannons[0].Sky);
        Assert.AreEqual(5, d.Cannons[0].SkyIndex);
        Assert.AreEqual(-12, d.Cannons[2].Owner, "AI rider = −decoy id");
        Assert.AreEqual(1, d.RiddenCannonOf(9), "the newest ride, not the one still in cooldown");
    }

    [Test]
    public void DecoyFlyRoute_SerializesAndSamples()
    {
        Vector3 pos = new Vector3(10, 48, 20);
        Vector3 dest = new Vector3(-100, 48, 150);
        float flight = SkyCannonRules.FlightSeconds(pos, 30f, dest);
        Assert.That(flight, Is.InRange(1f, 7f), "flight capped at 7 s");
        Vector3[] pts = SkyCannonRules.DecoyFlightPoints(pos, 30f, dest).Select(DecoyRoute.Quantize).ToArray();
        var r = new DecoyRoute { StartAt = 50, Gait = DecoyGait.Fly, Speed = flight, FaceYaw = 30f, HoldUntil = ColaCannonRules.LandAt(flight) + 0.3f, Points = pts };
        Assert.IsTrue(DecoyRoute.TryDecode(DecoyRoute.Encode(r), out DecoyRoute back));
        Assert.AreEqual(DecoyGait.Fly, back.Gait);
        Assert.AreEqual(4, back.Points.Length);
        Assert.AreEqual(flight, back.Speed, 0.01f);
        Assert.AreEqual(0f, back.TravelSeconds, "fly time lives in HoldUntil");
        Assert.AreEqual(50 + r.HoldUntil, back.EndsAt, 0.01);

        Assert.IsTrue(DecoyRoute.Sample(back, 50 + ColaCannonRules.EnterEnd + 0.1).Hidden, "inside the barrel");
        DecoyRoute.Pose mid = DecoyRoute.Sample(back, 50 + ColaCannonRules.FireAt + flight / 2f);
        Assert.IsTrue(mid.Flying);
        Assert.IsFalse(mid.Hidden);
        Assert.Greater(mid.Position.y, pos.y + 10f, "arcs high above the islands");
        DecoyRoute.Pose end = DecoyRoute.Sample(back, 50 + ColaCannonRules.LandAt(flight) + 0.2);
        Assert.IsFalse(end.Flying);
        Assert.Less(Vector3.Distance(end.Position, dest), 0.05f, "lands on the landing spot");
    }

    [Test]
    public void HobRecipe_ExcludesColaCannon_OtherMapsDoNot()
    {
        var hob = AssetDatabase.LoadAssetAtPath<EscapeRecipeSO>(HobRecipe);
        Assert.IsNotNull(hob);
        Assert.IsTrue(hob.Excludes("ColaCannon"));
        Assert.IsTrue(hob.Excludes(ToolKind.ColaCannon));
        foreach (string g in AssetDatabase.FindAssets("t:EscapeRecipeSO"))
        {
            var r = AssetDatabase.LoadAssetAtPath<EscapeRecipeSO>(AssetDatabase.GUIDToAssetPath(g));
            if (r != hob) Assert.IsFalse(r.Excludes("ColaCannon"), r.name + " keeps the cola cannon");
        }
    }

    [Test]
    public void ChestPick_SkipsExcludedItem_KeepsOtherRatios()
    {
        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        Assert.IsNotNull(catalog);
        var hob = AssetDatabase.LoadAssetAtPath<EscapeRecipeSO>(HobRecipe);
        var auth = (EscapeAuthority)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(EscapeAuthority));
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        System.Reflection.FieldInfo rngField = typeof(EscapeAuthority).GetField("rng", flags);
        Assert.IsNotNull(rngField, "EscapeAuthority.rng");
        rngField.SetValue(auth, new System.Random(42));
        System.Reflection.MethodInfo pick = typeof(EscapeAuthority).GetMethod("PickWeighted", flags);
        var counts = new Dictionary<string, int>();
        for (int k = 0; k < 2000; k++)
        {
            var it = (ItemSO)pick.Invoke(auth, new object[] { catalog.CookieItems, catalog.CookieItemWeights, hob });
            Assert.IsNotNull(it);
            counts.TryGetValue(it.ItemId, out int n);
            counts[it.ItemId] = n + 1;
        }
        Assert.IsFalse(counts.ContainsKey("ColaCannon"), "never from a Heaven Of Bakery chest");
        Assert.Greater(counts.Count, 3, "the other cookie items still appear");
        int withCola = 0;
        for (int k = 0; k < 2000; k++)
            if (((ItemSO)pick.Invoke(auth, new object[] { catalog.CookieItems, catalog.CookieItemWeights, null })).ItemId == "ColaCannon") withCola++;
        Assert.Greater(withCola, 0, "other maps still roll it");
    }

    [Test]
    public void Scene_SixCannons_EachAimsAtAnotherIsland()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            SkyCannonView[] guns = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkyCannonView>(true)).ToArray();
            Assert.AreEqual(6, guns.Length);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 6), guns.Select(g => g.Index));
            Transform[] slots = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name.StartsWith("SubSlot_")).ToArray();
            Assert.AreEqual(3, slots.Length);
            int Island(Vector3 p) => System.Array.IndexOf(slots, slots.OrderBy(s => (Flat(s.position) - Flat(p)).sqrMagnitude).First());
            var pairs = new HashSet<string>();
            foreach (SkyCannonView g in guns)
            {
                int from = Island(g.Pos), to = Island(g.Landing);
                Assert.AreNotEqual(from, to, g.name + " lands on another island");
                Assert.AreEqual(slots[to].position.y, g.Landing.y, 0.5f, "landing on the island top");
                Vector3 aim = Flat(g.Landing - g.Pos).normalized;
                Assert.Greater(Vector3.Dot(aim, SkyCannonRules.Rotation(g.Yaw) * Vector3.forward), 0.99f, "muzzle faces the landing spot");
                Assert.LessOrEqual(SkyCannonRules.FlightSeconds(g.Pos, g.Yaw, g.Landing), 7f);
                pairs.Add(from + ">" + to);
            }
            Assert.AreEqual(6, pairs.Count, "every island has one cannon to each other island");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
