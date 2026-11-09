using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Request1009Plan.md §68.9 콜라 대포(설치 → E로 들어감 → 해치 닫힘 → 겨눔 → 발사 → 포물선 착지) 시험.
public class Request1108CnTests
{
    private const double T0 = 50.0;

    [Test]
    public void Timeline_HatchEnterCloseAimFireLand()
    {
        const double b = T0 + 2.0;
        float flight = ColaCannonRules.FlightSeconds(40f);
        Assert.AreEqual(ColaCannonRules.Stage.Waiting, ColaCannonRules.StageAt(T0, 0, 0f, T0 + 5));
        Assert.AreEqual(ColaCannonRules.Stage.Gone, ColaCannonRules.StageAt(T0, 0, 0f, T0 + ColaCannonRules.IdleSeconds), "not ridden → gone after 10 s");
        Assert.AreEqual(ColaCannonRules.Stage.Opening, ColaCannonRules.StageAt(T0, b, flight, b + 0.2));
        Assert.AreEqual(ColaCannonRules.HatchOpenDegrees, ColaCannonRules.HatchAngle(b, b + 0.8), 1e-3f, "open while climbing in");
        Assert.AreEqual(ColaCannonRules.Stage.Entering, ColaCannonRules.StageAt(T0, b, flight, b + 0.8));
        Assert.AreEqual(ColaCannonRules.Stage.Closing, ColaCannonRules.StageAt(T0, b, flight, b + 1.3));
        Assert.AreEqual(0f, ColaCannonRules.HatchAngle(b, b + 1.6), 1e-3f, "closed before aiming");
        Assert.AreEqual(ColaCannonRules.Stage.Aiming, ColaCannonRules.StageAt(T0, b, flight, b + 2.0));
        Assert.AreEqual(ColaCannonRules.Stage.Flying, ColaCannonRules.StageAt(T0, b, flight, b + ColaCannonRules.FireAt + 0.1));
        Assert.AreEqual(ColaCannonRules.Stage.Gone, ColaCannonRules.StageAt(T0, b, flight, b + ColaCannonRules.LandAt(flight)));
        Assert.AreEqual(ColaCannonRules.RestPitch, ColaCannonRules.PitchAt(b, b + 1.0), 1e-3f, "rest while boarding");
        Assert.AreEqual(ColaCannonRules.FirePitch, ColaCannonRules.PitchAt(b, b + ColaCannonRules.FireAt), 1e-3f, "aimed at fire time");
        Assert.Greater(flight, 1.5f); Assert.Less(flight, 4f);
    }

    [Test]
    public void Turret_TurnsTheShortWayToTheAim()
    {
        Assert.AreEqual(90f, ColaCannonRules.TurretDelta(0f, 90f), 1e-3f);
        Assert.AreEqual(-30f, ColaCannonRules.TurretDelta(10f, -20f), 1e-3f);
        Assert.AreEqual(-20f, ColaCannonRules.TurretDelta(170f, 150f), 1e-3f);
        Assert.AreEqual(20f, ColaCannonRules.TurretDelta(170f, -170f), 1e-3f, "wraps across 180");
        Assert.AreEqual(0f, ColaCannonRules.TurretYawAt(0f, 90f, T0, T0 + 1.0), 1e-3f, "no turn before the hatch closes");
        Assert.AreEqual(90f, ColaCannonRules.TurretYawAt(0f, 90f, T0, T0 + ColaCannonRules.FireAt), 1e-3f);
    }

    [Test]
    public void Mouth_PointsAlongTheAim()
    {
        Vector3 pos = new Vector3(3f, 1f, -2f);
        foreach (float yaw in new[] { 0f, 90f, -135f })
        {
            Vector3 mouth = ColaCannonRules.Mouth(pos, yaw, ColaCannonRules.FirePitch);
            Vector3 flat = mouth - pos; flat.y = 0f;
            Vector3 aim = ColaCannonRules.Rotation(yaw) * Vector3.forward;
            Assert.Greater(Vector3.Dot(flat.normalized, aim), 0.99f, "yaw " + yaw);
            Assert.Greater(mouth.y, ColaCannonRules.Pivot(pos).y, "muzzle up");
            Assert.Less(ColaCannonRules.Breech(pos, yaw, ColaCannonRules.RestPitch).y, ColaCannonRules.Pivot(pos).y, "breech low (hatch side)");
        }
    }

    [Test]
    public void Flight_IsAParabolaFromMouthToLanding()
    {
        Vector3 a = new Vector3(0f, 3f, 0f), b = new Vector3(0f, 0f, 40f);
        float f = ColaCannonRules.FlightSeconds(40f);
        Assert.AreEqual(a, ColaCannonRules.FlightPoint(a, b, 0f, f));
        Assert.AreEqual(b, ColaCannonRules.FlightPoint(a, b, f, f));
        Vector3 mid = ColaCannonRules.FlightPoint(a, b, f * 0.5f, f);
        Assert.AreEqual(Mathf.Max(a.y, b.y) + ColaCannonRules.ApexHeight(40f), mid.y, 1e-3f, "apex in the middle");
        Assert.AreEqual(20f, mid.z, 1e-3f);
        Assert.Greater(ColaCannonRules.FlightPoint(a, b, f * 0.25f, f).y, a.y, "goes up first");
        Assert.AreEqual(7f, ColaCannonRules.FlightSeconds(250f), 1e-4f, "map-edge flights are capped");
        Assert.AreEqual(40f, ColaCannonRules.ApexHeight(250f), 1e-4f);
        Assert.Greater(ColaCannonRules.MaxRange, 300f, "edge search reaches across the largest map (radius 155 m)");
    }

    [Test]
    public void Riding_LockedUntilLanding_ProtectedOnceHatchCloses()
    {
        float f = 2f;
        Assert.IsTrue(ColaCannonRules.Riding(T0, f, T0 + 0.1));
        Assert.IsTrue(ColaCannonRules.Riding(T0, f, T0 + ColaCannonRules.FireAt + 1f), "in the air");
        Assert.IsFalse(ColaCannonRules.Riding(T0, f, T0 + ColaCannonRules.LandAt(f)), "free on landing");
        Assert.IsFalse(ColaCannonRules.Protected(T0, f, T0 + 0.5), "hatch still open — can be grabbed");
        Assert.IsTrue(ColaCannonRules.Protected(T0, f, T0 + ColaCannonRules.HatchCloseEnd + 0.01));
        Assert.IsTrue(ColaCannonRules.Protected(T0, f, T0 + ColaCannonRules.FireAt + 1f), "flying");
    }

    [Test]
    public void SummonSpot_StepsFaceTheUser()
    {
        Vector3 user = new Vector3(-6f, 0f, 2f);
        foreach (Vector3 fwd in new[] { Vector3.forward, Vector3.left, new Vector3(1f, 0f, -1f) })
        {
            Vector3 at = VehicleSpot.Point(user, fwd, 0f, ColaCannonRules.SummonDistance, ColaCannonRules.FaceLocal, out float yaw);
            Vector3 steps = ColaCannonRules.StepsPoint(at, yaw);
            Assert.Less(Vector3.Distance(steps, user), 1.5f, "steps next to the user: " + fwd);
        }
    }

    [Test]
    public void State_RoundTripsCannons()
    {
        var s = new EscapeState();
        s.Cannons.Add(new EscapeState.Cannon { Id = 5, Owner = 2, Pos = new Vector3(1, 0, 2), Yaw = 30f, SummonedAt = 10, BoardAt = 12, AimYaw = -60f, Dest = new Vector3(20, 1, -5), Flight = 2.4f });
        s.Cannons.Add(new EscapeState.Cannon { Id = 6, Owner = 3, SummonedAt = 11 });
        EscapeState d = EscapeState.Decode(s.Encode());
        Assert.IsNotNull(d);
        Assert.GreaterOrEqual(EscapeState.FormatVersion, 9, "v9 added cannons");
        Assert.AreEqual(2, d.Cannons.Count);
        Assert.AreEqual(-60f, d.Cannons[0].AimYaw);
        Assert.AreEqual(2.4f, d.Cannons[0].Flight, 1e-5f);
        Assert.AreEqual(new Vector3(20, 1, -5), d.Cannons[0].Dest);
        Assert.AreEqual(0, d.RiddenCannonOf(2));
        Assert.AreEqual(-1, d.RiddenCannonOf(3));
        Assert.AreEqual(1, d.CannonIndex(6));
    }

    [Test]
    public void Item_IsInTheCookieChestWithAModel()
    {
        EscapeCatalogSO c = EscapeCatalogSO.Current;
        ItemSO cannon = c.Find("ColaCannon");
        Assert.IsNotNull(cannon);
        Assert.AreEqual(ToolKind.ColaCannon, cannon.Tool.Kind);
        Assert.IsTrue(cannon.Tool.UsedByClick);
        Assert.IsFalse(cannon.Tool.CanHitMonster);
        Assert.IsNotNull(cannon.ModelPrefab, "ITEM_ColaCannon");
        Assert.IsNotNull(cannon.Icon);
        CollectionAssert.Contains(new System.Collections.Generic.List<ItemSO>(c.CookieItems), cannon, "survivor chest");
    }

    [Test]
    public void Prefab_HasMovingPartsCollidersAndView()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ColaCannonSetup.PrefabPath);
        Assert.IsNotNull(prefab, "run TagOfChaos/Escape/Build Cola Cannon Prefab");
        Assert.IsNotNull(prefab.GetComponent<ColaCannonView>());
        Transform turret = null, barrel = null, hatch = null;
        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == ColaCannonView.TurretName) turret = t;
            if (t.name == ColaCannonView.BarrelName) barrel = t;
            if (t.name == ColaCannonView.HatchName) hatch = t;
        }
        Assert.IsNotNull(turret); Assert.IsNotNull(barrel); Assert.IsNotNull(hatch);
        Assert.IsTrue(barrel.IsChildOf(turret) && hatch.IsChildOf(barrel), "turret > barrel > hatch");
        Assert.AreEqual(ColaCannonRules.PinHeight, barrel.position.y, 0.02f, "barrel pivot at the pin");
        Assert.Less(hatch.GetComponent<Renderer>().bounds.center.z, 0f, "hatch at the back (-Z)");
        Assert.GreaterOrEqual(prefab.GetComponentsInChildren<MeshCollider>(true).Length, 4);
    }
}
