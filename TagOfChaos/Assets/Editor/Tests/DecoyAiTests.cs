using System.Collections.Generic;
using ExitGames.Client.Photon;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Request1009Plan.md §44 — AI(미끼)가 플레이어처럼 움직이기: 경로·판단 규칙·네트워크 키·춤·맵 NavMesh.
public class DecoyAiTests
{
    private static DecoyRoute Line(double start, float speed, params Vector3[] points) =>
        new DecoyRoute { StartAt = start, Gait = DecoyGait.Walk, Speed = speed, FaceYaw = float.NaN, Points = points, HoldUntil = 0f };

    // ---------------- 경로 ----------------

    [Test]
    public void Route_SampleWalksCornersAndStopsAtEnd()
    {
        DecoyRoute r = Line(100, 2f, new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(10, 0, 10));
        Assert.AreEqual(20f, r.Length, 1e-4f);
        Assert.AreEqual(10f, r.TravelSeconds, 1e-4f);

        DecoyRoute.Pose before = DecoyRoute.Sample(r, 90);
        Assert.AreEqual(Vector3.zero, before.Position);
        Assert.IsFalse(before.Moving, "not started yet");

        DecoyRoute.Pose mid = DecoyRoute.Sample(r, 102.5);
        Assert.AreEqual(5f, mid.Position.z, 1e-3f);
        Assert.AreEqual(0f, mid.Yaw, 1e-3f, "heading north (+z)");
        Assert.IsTrue(mid.Moving);

        DecoyRoute.Pose corner = DecoyRoute.Sample(r, 107.5);
        Assert.AreEqual(new Vector3(5, 0, 10), corner.Position);
        Assert.AreEqual(90f, corner.Yaw, 1e-3f, "heading east after the corner");

        DecoyRoute.Pose end = DecoyRoute.Sample(r, 200);
        Assert.AreEqual(new Vector3(10, 0, 10), end.Position);
        Assert.IsFalse(end.Moving);
        Assert.AreEqual(90f, end.Yaw, 1e-3f, "keeps the last segment heading");
    }

    [Test]
    public void Route_FaceYawAndHold()
    {
        DecoyRoute r = Line(0, 4f, Vector3.zero, new Vector3(0, 0, 8));
        r.FaceYaw = 200f;
        r.HoldUntil = r.TravelSeconds + 5f;
        Assert.AreEqual(7.0, r.EndsAt, 1e-6);
        Assert.AreEqual(200f, DecoyRoute.Sample(r, 3).Yaw, 1e-3f, "faces the building after arriving");

        var still = new DecoyRoute { StartAt = 0, Gait = DecoyGait.Dance, DanceId = 2, Points = new[] { new Vector3(1, 2, 3) }, HoldUntil = 6f, FaceYaw = float.NaN };
        DecoyRoute.Pose p = DecoyRoute.Sample(still, 3, 45f);
        Assert.AreEqual(new Vector3(1, 2, 3), p.Position);
        Assert.AreEqual(45f, p.Yaw, 1e-3f);
        Assert.IsFalse(p.Moving);
    }

    [Test]
    public void Route_EncodeDecodeRoundTripAndSize()
    {
        var points = new Vector3[DecoyRoute.MaxPoints];
        for (int i = 0; i < points.Length; i++) points[i] = new Vector3(-150.25f + i * 3.3f, 1.07f * i, 149.99f - i * 7.1f);
        var r = new DecoyRoute { StartAt = 12345.678, Gait = DecoyGait.Run, DanceId = 7, Speed = 26f, HoldUntil = 18.5f, FaceYaw = 123.4f, Points = points };
        byte[] data = DecoyRoute.Encode(r);
        Assert.LessOrEqual(data.Length, 18 + DecoyRoute.MaxPoints * 6, "one route stays under ~90 bytes");

        Assert.IsTrue(DecoyRoute.TryDecode(data, out DecoyRoute d));
        Assert.AreEqual(r.StartAt, d.StartAt, 1e-9);
        Assert.AreEqual(DecoyGait.Run, d.Gait);
        Assert.AreEqual(7, d.DanceId);
        Assert.AreEqual(26f, d.Speed, 0.05f);
        Assert.AreEqual(18.5f, d.HoldUntil, 0.02f);
        Assert.AreEqual(123.4f, d.FaceYaw, 0.06f);
        Assert.AreEqual(points.Length, d.Points.Length);
        for (int i = 0; i < points.Length; i++) Assert.Less(Vector3.Distance(points[i], d.Points[i]), 0.01f, "1 cm grid");

        r.FaceYaw = float.NaN;
        Assert.IsTrue(DecoyRoute.TryDecode(DecoyRoute.Encode(r), out d));
        Assert.IsTrue(float.IsNaN(d.FaceYaw));
    }

    [Test]
    public void Route_DecodeRejectsGarbage()
    {
        Assert.IsFalse(DecoyRoute.TryDecode(null, out _));
        Assert.IsFalse(DecoyRoute.TryDecode(new byte[5], out _));
        byte[] good = DecoyRoute.Encode(Line(1, 2, Vector3.zero, Vector3.one));
        byte[] cut = new byte[good.Length - 3];
        System.Array.Copy(good, cut, cut.Length);
        Assert.IsFalse(DecoyRoute.TryDecode(cut, out _), "truncated points");
        good[0] = 99;
        Assert.IsFalse(DecoyRoute.TryDecode(good, out _), "unknown format");
    }

    [Test]
    public void RouteBook_ReadsPrefixedKeysAndCutsPointsOutsideTheMap()
    {
        var book = new DecoyRouteBook(p => p.x < 50f); // x ≥ 50 = 맵 밖
        var props = new Hashtable
        {
            { DecoyRouteBook.KeyOf(4), DecoyRoute.Encode(Line(0, 2, Vector3.zero, new Vector3(40, 0, 0), new Vector3(80, 0, 0))) },
            { NetKeys.EscapeState, new byte[3] },
        };
        Assert.IsTrue(book.Read(props, full: true));
        Assert.IsTrue(book.TryGet(4, out DecoyRoute r));
        Assert.AreEqual(2, r.Points.Length, "stops before the point outside the play area");

        var d = new EscapeState.Decoy { Id = 4, Pos = new Vector3(9, 9, 9) };
        Assert.AreEqual(new Vector3(40, 0, 0), book.PositionOf(d, 1000));
        Assert.AreEqual(new Vector3(9, 9, 9), book.PositionOf(new EscapeState.Decoy { Id = 5, Pos = new Vector3(9, 9, 9) }, 1000), "no route → stored spot");

        book.Read(new Hashtable { { DecoyRouteBook.KeyOf(4), null } }, full: false);
        Assert.IsFalse(book.TryGet(4, out _), "null clears the route");
        Assert.IsTrue(DecoyRouteBook.TryParseKey("DR_123", out int id) && id == 123);
        Assert.IsFalse(DecoyRouteBook.TryParseKey("DoorStates", out _));
    }

    // ---------------- 판단 규칙 ----------------

    [Test]
    public void Flee_StartsInsideHearingRangeAndStopsAfterThreeSafeSeconds()
    {
        var calm = new DecoyRules.Personality { Fear = 1f };
        float start = DecoyRules.FleeStartDistance(20f, calm);
        Assert.AreEqual(20f, start, 1e-4f, "chase BGM audible radius");
        Assert.IsTrue(DecoyRules.ShouldFlee(Vector3.zero, new Vector3(0, 0, 19), start));
        Assert.IsFalse(DecoyRules.ShouldFlee(Vector3.zero, new Vector3(0, 0, 21), start));
        Assert.IsFalse(DecoyRules.ShouldFlee(Vector3.zero, new Vector3(0, 6, 5), start), "other floor");

        Assert.IsFalse(DecoyRules.IsSafe(Vector3.zero, new Vector3(0, 0, 24), 20f), "inside 20 + 5 m");
        Assert.IsTrue(DecoyRules.IsSafe(Vector3.zero, new Vector3(0, 0, 26), 20f));
        Assert.IsFalse(DecoyRules.CanStopFleeing(-1, 10), "never safe yet");
        Assert.IsFalse(DecoyRules.CanStopFleeing(10, 12.9));
        Assert.IsTrue(DecoyRules.CanStopFleeing(10, 13.0));
    }

    [Test]
    public void FleeScore_PrefersAwayOpenAndUncrowded()
    {
        Vector3 self = Vector3.zero, monster = new Vector3(0, 0, -10);
        float away = DecoyRules.FleeScore(self, monster, new Vector3(0, 0, 20), 1f, 0);
        float side = DecoyRules.FleeScore(self, monster, new Vector3(20, 0, 0), 1f, 0);
        float toward = DecoyRules.FleeScore(self, monster, new Vector3(0, 0, -20), 1f, 0);
        Assert.Greater(away, side);
        Assert.Greater(side, toward);
        Assert.Greater(away, DecoyRules.FleeScore(self, monster, new Vector3(0, 0, 20), 0.1f, 0), "dead end loses");
        Assert.Greater(away, DecoyRules.FleeScore(self, monster, new Vector3(0, 0, 20), 1f, 3), "crowded spot loses");
    }

    [Test]
    public void Personality_IsDeterministicAndInRange()
    {
        for (int seed = 1; seed < 400; seed += 7)
        {
            DecoyRules.Personality a = DecoyRules.FromSeed(seed), b = DecoyRules.FromSeed(seed);
            Assert.AreEqual(a.Fear, b.Fear);
            Assert.AreEqual(a.Roams, b.Roams);
            Assert.AreEqual(a.Visit, b.Visit);
            Assert.That(a.Fear, Is.InRange(0.8f, 1.2f));
            Assert.GreaterOrEqual(a.Wander, 3);
            Assert.GreaterOrEqual(a.Hide, 1);
        }
    }

    [Test]
    public void PickActivity_PrepareOnlyWandersOrRests()
    {
        DecoyRules.Personality p = DecoyRules.FromSeed(42);
        var seen = new HashSet<DecoyRules.Activity>();
        for (int i = 0; i < 100; i++)
        {
            DecoyRules.Activity prep = DecoyRules.PickActivity(p, i / 100.0, true, true, true);
            Assert.IsTrue(prep == DecoyRules.Activity.Wander || prep == DecoyRules.Activity.Rest, prep.ToString());
            seen.Add(DecoyRules.PickActivity(p, i / 100.0, false, true, true));
        }
        CollectionAssert.IsSupersetOf(seen, new[] { DecoyRules.Activity.Wander, DecoyRules.Activity.Rest, DecoyRules.Activity.Visit, DecoyRules.Activity.Hide });
        for (int i = 0; i < 100; i++)
        {
            DecoyRules.Activity a = DecoyRules.PickActivity(p, i / 100.0, false, false, false);
            Assert.IsTrue(a == DecoyRules.Activity.Wander || a == DecoyRules.Activity.Rest, "no buildings or hide spots");
        }
    }

    [Test]
    public void Dance_IsVeryRareAndCapacitiesHold()
    {
        int dances = 0;
        var rng = new System.Random(7);
        for (int i = 0; i < 10000; i++) if (DecoyRules.RollDance(rng.NextDouble())) dances++;
        Assert.That(dances, Is.InRange(200, 400), "about 3% of finished activities");
        Assert.AreEqual(3, DecoyRules.BuildingCapacity);
        Assert.AreEqual(2, DecoyHideSpot.Capacity);
        Assert.AreEqual(26f, DecoyRules.SpeedFor(DecoyGait.Run, 20f), 1e-4f, "run = walk x 1.3 like Shift");
    }

    // ---------------- 네트워크 키 ----------------

    [Test]
    public void NetKeys_RoutePrefixIsMasterOnlyAndClearedEachRound()
    {
        Assert.IsTrue(NetKeys.IsMasterKey("DR_17", NetKeys.Target.Room), "guarded like other master keys");
        Assert.IsFalse(NetKeys.IsMasterKey("DR_17", NetKeys.Target.Player));
        Assert.IsTrue(NetKeys.IsRoundPrefixedKey("DR_17", NetKeys.Target.Room), "RoundStateResetter clears it");
        Assert.IsFalse(NetKeys.IsRoundPrefixedKey(NetKeys.SkinIndex, NetKeys.Target.Player));
        Assert.IsFalse(NetKeys.IsRoundPrefixedKey("DoorStates", NetKeys.Target.Room));
    }

    // ---------------- 춤 ----------------

    [Test]
    public void DanceCatalog_HasFirstDanceAndAnimatorStates()
    {
        DanceCatalogSO catalog = DanceCatalogSO.Current;
        Assert.IsNotNull(catalog, "Resources/" + DanceCatalogSO.ResourcePath);
        DanceCatalogSO.DanceEntry first = catalog.Find(0);
        Assert.IsNotNull(first);
        Assert.IsNotNull(first.clip);
        Assert.IsTrue(first.clip.isHumanMotion && first.clip.isLooping, "humanoid loop — shared by all characters");
        Assert.AreEqual(1.2f, first.clip.length, 0.05f);
        Assert.IsTrue(catalog.TryPickForAi(0.5, out byte ai));
        Assert.IsTrue(catalog.TryPickForPlayer(0.99, out byte player));
        Assert.IsNotNull(catalog.Find(ai));
        Assert.IsNotNull(catalog.Find(player));

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DanceStateBuilder.ControllerPath);
        var parameters = new HashSet<string>();
        foreach (AnimatorControllerParameter p in controller.parameters) parameters.Add(p.name);
        Assert.IsTrue(parameters.Contains(DanceCatalogSO.IdParameter) && parameters.Contains(DanceCatalogSO.Trigger));
        Assert.IsTrue(parameters.Contains(PlayerMoveState.Dance.ToString()), "the state name is the trigger name");
        foreach (DanceCatalogSO.DanceEntry d in catalog.Dances)
        {
            bool found = false;
            foreach (ChildAnimatorState s in controller.layers[0].stateMachine.states)
                if (s.state.name == DanceCatalogSO.StateName(d.id) && s.state.motion == d.clip) found = true;
            Assert.IsTrue(found, "state " + DanceCatalogSO.StateName(d.id) + " (run Tools/Characters/Rebuild Dance States)");
        }
    }

    [Test]
    public void DanceKey_DefaultsToBWithoutConflicts()
    {
        Assert.AreEqual(KeyCode.B, KeyBindings.Default(KeyAction.Dance));
        var keys = new Dictionary<KeyAction, KeyCode>();
        foreach (KeyAction a in KeyBindings.All) keys[a] = KeyBindings.Default(a);
        Assert.IsFalse(KeyBindings.HasConflict(KeyAction.Dance, x => keys[x]));
        Assert.AreEqual(KeyAction.Dance, KeyBindings.All[KeyBindings.All.Length - 1], "appended so saved key numbers do not shift");
    }

    // ---------------- 맵 NavMesh ----------------

    [Test]
    public void Maps_HaveDecoyNavMeshCoveringChestsBuildingsAndHideSpots()
    {
        if (EditorApplication.isPlaying) Assert.Ignore("Play Mode");
        var report = new List<string>();
        foreach (string path in DecoyNavMeshBaker.MapScenes)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                DecoyNavMesh holder = null;
                EscapeManager manager = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (holder == null) holder = root.GetComponentInChildren<DecoyNavMesh>(true);
                    if (manager == null) manager = root.GetComponentInChildren<EscapeManager>(true);
                }
                Assert.IsNotNull(holder, scene.name + ": DecoyNavMesh missing (Tools/Maps/Bake Decoy NavMesh)");
                Assert.IsNotNull(holder.Data, scene.name + ": no NavMeshData");
                NavMesh.RemoveAllNavMeshData();
                NavMeshDataInstance live = NavMesh.AddNavMeshData(holder.Data);
                try
                {
                    var so = new SerializedObject(manager);
                    SerializedProperty chests = so.FindProperty("chestAnchors");
                    for (int i = 0; i < chests.arraySize; i++)
                        if (chests.GetArrayElementAtIndex(i).objectReferenceValue is Transform t)
                            Assert.IsTrue(DecoyNavMeshBaker.OnMesh(t.position), $"{scene.name}: chest {i}");
                    SerializedProperty buildings = so.FindProperty("buildings");
                    for (int i = 0; i < buildings.arraySize; i++)
                        if (buildings.GetArrayElementAtIndex(i).objectReferenceValue is QuadrantBuilding b)
                            Assert.IsTrue(DecoyNavMeshBaker.OnMesh(b.RepairPoint), $"{scene.name}: building {i} repair point");
                    int spots = 0;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (DecoyHideSpot h in root.GetComponentsInChildren<DecoyHideSpot>(true))
                    {
                        Assert.Greater(h.SpotCount, 0, $"{scene.name}: {h.name} has no spot");
                        for (int i = 0; i < h.SpotCount; i++) Assert.IsTrue(DecoyNavMeshBaker.OnMesh(h.Spot(i)), $"{scene.name}: {h.name} spot {i}");
                        spots++;
                    }
                    report.Add($"{scene.name}: hideSpots={spots}");
                }
                finally { live.Remove(); }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        Debug.Log("[DecoyAiTests] " + string.Join(", ", report));
    }
}
