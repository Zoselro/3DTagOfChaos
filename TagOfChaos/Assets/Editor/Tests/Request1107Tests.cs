using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Request1009Plan.md §64 거대 괴물(MixedMonster ×5) 시험.
public class Request1107Tests
{
    // ---------------- 속도 ----------------

    [Test]
    public void Speed_StartsAtThree_AndStepsEveryTenthOfWitchTime()
    {
        Assert.AreEqual(3f, GiantRules.Speed(0, 60), 1e-4f);
        Assert.AreEqual(3f, GiantRules.Speed(5.9, 60), 1e-4f, "60 s witch time -> first step at 6 s");
        Assert.AreEqual(4f, GiantRules.Speed(6.6, 60), 1e-4f);
        Assert.AreEqual(5f, GiantRules.Speed(12.6, 60), 1e-4f);
        Assert.AreEqual(12f, GiantRules.Speed(59, 60), 1e-4f);
        Assert.AreEqual(4f, GiantRules.Speed(30.6, 300), 1e-4f, "5 min -> every 30 s");
        Assert.AreEqual(3.5f, GiantRules.Speed(6.25, 60), 1e-3f, "ramps smoothly over RampSeconds");
    }

    [Test]
    public void Distance_IsTheIntegralOfSpeed()
    {
        double sum = 0, dt = 0.001;
        for (double t = 0; t < 40; t += dt) sum += GiantRules.Speed(t + dt * 0.5, 60) * dt;
        Assert.AreEqual(sum, GiantRules.Distance(40, 60), 0.05);
        Assert.AreEqual(0, GiantRules.Distance(-3, 60), 1e-9);
    }

    [Test]
    public void WalkAnimation_SpeedsUpWithWalkSpeed()
    {
        Assert.AreEqual(1f, GiantRules.WalkAnimRate(GiantRules.NaturalWalkSpeed), 1e-4f);
        Assert.Less(GiantRules.WalkAnimRate(3f), GiantRules.WalkAnimRate(8f));
        Assert.AreEqual(12f / 6.3f, GiantRules.WalkAnimRate(12f), 1e-3f);
    }

    // ---------------- 밟기 ----------------

    [Test]
    public void Stomp_HitsInsideTheCircleOnly()
    {
        Vector3 center = Vector3.zero;
        Assert.IsTrue(GiantRules.InStompRange(center, new Vector3(3.4f, 0f, 0f)));
        Assert.IsFalse(GiantRules.InStompRange(center, new Vector3(3.6f, 0f, 0f)));
        Assert.IsFalse(GiantRules.InStompRange(center, new Vector3(0f, 3.5f, 0f)), "a floor above is safe");
        Assert.IsFalse(GiantRules.InStompRange(center, new Vector3(0f, -36f, 0f)), "underground is safe");
    }

    [Test]
    public void Stomp_StartsWhenTheTargetIsNearTheFrontPaws()
    {
        Vector3 body = Vector3.zero;
        Assert.IsTrue(GiantRules.ShouldStomp(body, new Vector3(0f, 0f, GiantRules.FrontReach + 2.5f)));
        Assert.IsFalse(GiantRules.ShouldStomp(body, new Vector3(0f, 0f, GiantRules.FrontReach + 3.5f)));
        Assert.IsTrue(GiantRules.ShouldStomp(body, new Vector3(5f, 0f, 0f)), "turns toward a target at the side");
        Assert.AreEqual(0.4f, GiantRules.StompImpactSeconds, 1e-4f, "user decision: fast stomp, paws land at 0.4 s");
    }

    [Test]
    public void StompClip_MatchesTheImpactTime()
    {
        AnimationClip stomp = GiantSettingsSO.Current.StompClip;
        Assert.IsNotNull(stomp);
        Assert.AreEqual(GiantRules.StompSeconds, stomp.length, 0.05f);
        Assert.AreEqual(12f, GiantRules.StompImpactSeconds * stomp.frameRate, 0.01f, "Blender frame 13 = Unity frame 12");
        Assert.IsTrue(GiantSettingsSO.Current.WalkClip.isLooping && GiantSettingsSO.Current.IdleClip.isLooping);
    }

    // ---------------- 시각표 ----------------

    [Test]
    public void Timeline_GiantInsertsCircleAndRevealBeforeReturn()
    {
        const double appear = 100, launch = 97;
        double end = WitchTimeline.TimeAttackEnd(appear, 60, true);
        Assert.AreEqual(appear + 10 + 5 + 60, end, 1e-6, "witch intro 10 + giant 5 + witch time");
        WitchShot At(double t, bool giant) => WitchTimeline.ShotAt(WitchMode.TimeAttack, appear + t, true, launch, appear, end, false, giant);
        Assert.AreEqual(WitchShot.Face, At(6.5, true));
        Assert.AreEqual(WitchShot.GiantCircle, At(7.5, true));
        Assert.AreEqual(WitchShot.GiantReveal, At(10.5, true));
        Assert.AreEqual(WitchShot.Return, At(12.5, true));
        Assert.AreEqual(WitchShot.None, At(15.5, true));
        Assert.AreEqual(WitchShot.Return, At(7.5, false), "no giant: unchanged");
        Assert.AreEqual(appear + 15, WitchTimeline.WalkStart(appear, true), 1e-6);
        Assert.AreEqual(appear + 10, WitchTimeline.WalkStart(appear, false), 1e-6);
    }

    [Test]
    public void Circle_DrawsRingThenStarInThreeSeconds()
    {
        const double appear = 0;
        double start = GiantRules.CircleStart(appear);
        Assert.AreEqual(7, start, 1e-6);
        Assert.AreEqual(0.5f, GiantRules.CircleRingProgress(start + 0.75, appear), 1e-4f);
        Assert.AreEqual(0f, GiantRules.CircleStarProgress(start + 1.4, appear), 1e-4f);
        Assert.AreEqual(1f, GiantRules.CircleStarProgress(start + 3, appear), 1e-4f);
        Assert.AreEqual(0.5f, GiantRules.RevealProgress(start + 4, appear), 1e-4f);
    }

    // ---------------- 상태(방 속성) ----------------

    [Test]
    public void State_RoundTripsAndFollowsThePath()
    {
        var s = new GiantState
        {
            Spawn = new Vector3(1, 0, 2), SpawnYaw = 45, Seq = 7, MoveAt = 20, Yaw = 90,
            Points = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(10, 0, 10) },
            StompAt = 33.5, StompCenter = new Vector3(3, 0, 4), WarpAt = 0,
        };
        Assert.IsTrue(GiantState.TryDecode(s.Encode(), out GiantState d));
        Assert.AreEqual(3, d.Points.Length);
        Assert.AreEqual(s.Points[2], d.Points[2]);
        Assert.AreEqual(s.StompAt, d.StompAt, 1e-9);
        Assert.AreEqual(s.Seq, d.Seq);
        Assert.Less(s.Encode().Length, 200, "small room property");

        // 걷기 시작 20 s, 속도 3 → 4초에 12 m(첫 구간 10 m + 둘째 구간 2 m), 둘째 구간 방향 = +x(90도)
        GiantState.Pose p = d.Sample(24, 20, 60);
        Assert.AreEqual(2f, p.Position.x, 1e-3f);
        Assert.AreEqual(10f, p.Position.z, 1e-3f);
        Assert.AreEqual(90f, p.Yaw, 1e-3f);
        Assert.AreEqual(3f, p.Speed, 1e-4f);
        GiantState.Pose done = d.Sample(40, 20, 60);
        Assert.AreEqual(new Vector3(10, 0, 10), done.Position);
        Assert.AreEqual(0f, done.Speed);
    }

    [Test]
    public void State_WarpSinksThenRisesElsewhere()
    {
        var s = new GiantState { WarpAt = 50, WarpFrom = new Vector3(0, 0, 0), WarpTo = new Vector3(5, -36, 5), Points = new[] { new Vector3(5, -36, 5) }, MoveAt = 55 };
        Assert.AreEqual(0f, s.Sample(50.5, 0, 60).Sink, 1e-4f, "circle draws first");
        Assert.Greater(s.Sample(52.45, 0, 60).Sink, 0.95f, "sunk before the rise");
        Assert.AreEqual(Vector3.zero, s.Sample(52.4, 0, 60).Position);
        Assert.AreEqual(new Vector3(5, -36, 5), s.Sample(53, 0, 60).Position);
        Assert.AreEqual(0f, s.Sample(55.1, 0, 60).Sink, 1e-4f);
    }

    [Test]
    public void NetKeys_GiantIsClearedEveryRound()
    {
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.Giant);
        Assert.IsTrue(NetKeys.IsMasterKey(NetKeys.Giant, NetKeys.Target.Room));
        Assert.AreEqual(3, (int)DeathCause.Giant);
    }

    // ---------------- 에셋 ----------------

    [Test]
    public void Settings_HaveModelClipsAndSlimeCharacter()
    {
        GiantSettingsSO s = GiantSettingsSO.Current;
        Assert.IsNotNull(s.Model);
        Assert.IsNotNull(s.IdleClip);
        Assert.IsNotNull(s.WalkClip);
        Assert.AreEqual(0.3f, s.SpawnChance, 1e-4f, "user decision: fixed 30 %");
        Assert.IsTrue(s.OozesSlime(3), "Princess Ade (slot 3) oozes slime");
        Assert.IsFalse(s.OozesSlime(0));
        var catalog = AssetDatabase.LoadAssetAtPath<SkinCatalogSO>("Assets/03. SO/Unit/SkinCatalog.asset");
        StringAssert.Contains("Lemonade", catalog.GetModel(3).name);
    }

    [Test]
    public void GiantNavMesh_BakedForEveryMap()
    {
        Assert.IsTrue(GiantNavMesh.HasAgentType, "agent type 'Giant' in ProjectSettings/NavMeshAreas");
        Assert.AreNotEqual(0, GiantNavMesh.AgentTypeId);
        foreach (string scene in DecoyNavMeshBaker.MapScenes)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(scene);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>($"{GiantNavMeshBaker.AssetFolder}/{name}.asset"), name);
            StringAssert.Contains(GiantNavMeshBaker.RootName, System.IO.File.ReadAllText(scene), name + " holds the giant NavMesh");
        }
    }

    [Test]
    public void ResultRow_HasStompedLabel()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/Scene/PlayerResultRow/PlayerResultRow.prefab");
        var row = prefab.GetComponent<PlayerResultRow>();
        Assert.IsFalse(string.IsNullOrEmpty(new SerializedObject(row).FindProperty("giantKilledLabel").stringValue));
    }
}
