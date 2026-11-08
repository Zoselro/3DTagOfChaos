using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Request1009Plan.md §68.7 호박 마차(소환 → E 문 열기 → 탑승 → 문 닫힘 → 순간이동) 시험.
public class Request1108PcTests
{
    private const double T0 = 100.0;

    [Test]
    public void Timeline_DoorOpensEnterClosesThenDeparts()
    {
        const double board = T0 + 3.0;
        Assert.AreEqual(PumpkinCarriageRules.Stage.Waiting, PumpkinCarriageRules.StageAt(T0, 0, T0 + 1));
        Assert.AreEqual(0f, PumpkinCarriageRules.DoorAngle(0, T0 + 1), "closed before boarding");
        Assert.AreEqual(PumpkinCarriageRules.Stage.Opening, PumpkinCarriageRules.StageAt(T0, board, board + 0.2));
        Assert.AreEqual(PumpkinCarriageRules.DoorOpenDegrees, PumpkinCarriageRules.DoorAngle(board, board + 0.7), 1e-3f, "fully open while walking in");
        Assert.AreEqual(PumpkinCarriageRules.Stage.Entering, PumpkinCarriageRules.StageAt(T0, board, board + 0.7));
        Assert.AreEqual(PumpkinCarriageRules.Stage.Closing, PumpkinCarriageRules.StageAt(T0, board, board + 1.2));
        Assert.AreEqual(0f, PumpkinCarriageRules.DoorAngle(board, board + 1.5), 1e-3f, "closed before teleport");
        Assert.AreEqual(PumpkinCarriageRules.Stage.Departing, PumpkinCarriageRules.StageAt(T0, board, board + 1.6));
        Assert.AreEqual(PumpkinCarriageRules.Stage.Gone, PumpkinCarriageRules.StageAt(T0, board, board + PumpkinCarriageRules.TeleportAt));
        Assert.AreEqual(1f, PumpkinCarriageRules.EnterProgress(board, board + PumpkinCarriageRules.EnterEnd), 1e-3f, "inside when the door starts closing");
        Assert.Less(PumpkinCarriageRules.DoorOpenEnd, PumpkinCarriageRules.EnterEnd);
        Assert.Less(PumpkinCarriageRules.DoorCloseEnd, PumpkinCarriageRules.TeleportAt);
        Assert.Less(PumpkinCarriageRules.TeleportAt, PumpkinCarriageRules.RemoveAt);
    }

    [Test]
    public void NotRidden_DisappearsAfterTenSeconds()
    {
        Assert.AreEqual(10f, PumpkinCarriageRules.IdleSeconds, 1e-4f, "default plan 2");
        Assert.AreEqual(PumpkinCarriageRules.Stage.Waiting, PumpkinCarriageRules.StageAt(T0, 0, T0 + 9.9));
        Assert.AreEqual(PumpkinCarriageRules.Stage.Gone, PumpkinCarriageRules.StageAt(T0, 0, T0 + 10.0));
    }

    [Test]
    public void Grab_AllowedUntilDoorClosed_ThenProtected()
    {
        const double board = T0;
        Assert.IsFalse(PumpkinCarriageRules.Protected(0, T0), "not boarded");
        Assert.IsFalse(PumpkinCarriageRules.Protected(board, board + 0.5), "door still open — can be grabbed (default plan 3)");
        Assert.IsTrue(PumpkinCarriageRules.Protected(board, board + PumpkinCarriageRules.DoorCloseEnd + 0.01));
        Assert.IsFalse(PumpkinCarriageRules.Protected(board, board + PumpkinCarriageRules.TeleportAt + 1.0), "after arrival");
        Assert.IsTrue(PumpkinCarriageRules.Riding(board, board + 0.1), "movement locked from E");
        Assert.IsFalse(PumpkinCarriageRules.Riding(board, board + PumpkinCarriageRules.TeleportAt), "free on arrival");
    }

    [Test]
    public void SummonPoint_DoorFacesTheUser()
    {
        Vector3 user = new Vector3(10f, 0f, -4f);
        foreach (Vector3 fwd in new[] { Vector3.forward, Vector3.right, new Vector3(-1f, 0f, 1f), Vector3.back })
        {
            Vector3 at = PumpkinCarriageRules.SummonPoint(user, fwd, 0f, out float yaw);
            Assert.AreEqual(PumpkinCarriageRules.SummonDistance, Vector3.Distance(user, at), 1e-3f);
            Vector3 door = PumpkinCarriageRules.DoorPoint(at, yaw);
            Assert.Less(Vector3.Distance(door, user), Vector3.Distance(at, user), "door side is toward the user: " + fwd);
            Assert.Less(Vector3.Distance(door, user), 1.5f, "door step close to the user");
        }
        Assert.LessOrEqual(PumpkinCarriageRules.SummonDistance, PumpkinCarriageRules.SummonReach, "authority accepts the summon spot");
    }

    [Test]
    public void State_RoundTripsCarriages()
    {
        var s = new EscapeState();
        s.Carriages.Add(new EscapeState.Carriage { Id = 7, Owner = 3, Pos = new Vector3(1, 2, 3), Yaw = 45f, SummonedAt = 12.5, BoardAt = 14.0, Dest = new Vector3(-5, 0, 9) });
        s.Carriages.Add(new EscapeState.Carriage { Id = 8, Owner = 4, Pos = Vector3.one, Yaw = -90f, SummonedAt = 13 });
        EscapeState d = EscapeState.Decode(s.Encode());
        Assert.IsNotNull(d);
        Assert.GreaterOrEqual(EscapeState.FormatVersion, 8, "v8 added carriages (v9 §68.9 cannons)");
        Assert.AreEqual(2, d.Carriages.Count);
        Assert.AreEqual(45f, d.Carriages[0].Yaw);
        Assert.AreEqual(14.0, d.Carriages[0].BoardAt);
        Assert.AreEqual(new Vector3(-5, 0, 9), d.Carriages[0].Dest);
        Assert.IsTrue(d.Carriages[0].Boarded);
        Assert.IsFalse(d.Carriages[1].Boarded);
        Assert.AreEqual(0, d.RiddenCarriageOf(3));
        Assert.AreEqual(-1, d.RiddenCarriageOf(4), "summoned but not boarded");
        Assert.AreEqual(1, d.CarriageIndex(8));
    }

    [Test]
    public void Prefab_HasModelDoorCollidersAndView()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Escape/PumpkinCarriage.prefab");
        Assert.IsNotNull(prefab, "run TagOfChaos/Escape/Build Pumpkin Carriage Prefab");
        Assert.IsNotNull(prefab.GetComponent<PumpkinCarriageView>());
        Transform door = null;
        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true)) if (t.name == PumpkinCarriageView.DoorName) door = t;
        Assert.IsNotNull(door, "separate door object");
        Assert.IsNotNull(door.GetComponent<MeshCollider>());
        Assert.GreaterOrEqual(prefab.GetComponentsInChildren<MeshCollider>(true).Length, 2, "body + door");
        Bounds b = new Bounds(prefab.transform.position, Vector3.zero);
        foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
        Assert.Greater(b.size.y, 3f, "big enough for a 2 m cookie");
        Assert.Less(door.GetComponent<Renderer>().bounds.center.x, 0f, "door on local -X (PumpkinCarriageRules.DoorLocal)");
    }
}
