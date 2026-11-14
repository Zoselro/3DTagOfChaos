using NUnit.Framework;
using UnityEngine;

// Request1009Plan.md §70.3 — 던지기 계산(ThrowSolver)·궤적 계산(BallisticSim). 가짜 바닥(y = 0 평면)과 벽으로 시험한다.
public class Request1112ThrowTests
{
    private ProjectileConfigSO config;

    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<ProjectileConfigSO>();
        config.baseRange = 12f;
        config.maxRange = 20f;
        config.launchAngle = 45f;
        config.radius = 0.15f;
        config.gravity = 9.81f;
        config.bounciness = 0.4f;
        config.surfaceFriction = 0.2f;
        config.rollDeceleration = 4f;
        config.restSpeed = 0.35f;
        config.fuseAfterImpact = 2f;
        config.maxLifeSeconds = 5f;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(config);

    // 땅(y = 0)과, wallX가 있으면 x = wallX에 선 벽.
    private static BallisticSim.Sweep World(float wallX = float.NaN)
    {
        return (Vector3 from, Vector3 dir, float distance, float radius, out Vector3 at, out Vector3 normal, out Collider col) =>
        {
            col = null;
            float best = float.MaxValue;
            at = from;
            normal = Vector3.up;
            if (dir.y < 0f)
            {
                float t = (from.y - radius) / -dir.y;
                if (t >= 0f && t <= distance) { best = t; at = from + dir * t; normal = Vector3.up; }
            }
            if (!float.IsNaN(wallX) && dir.x > 0f)
            {
                float t = (wallX - radius - from.x) / dir.x;
                if (t >= 0f && t <= distance && t < best) { best = t; at = from + dir * t; normal = Vector3.left; }
            }
            return best < float.MaxValue;
        };
    }

    private static float Apex(Vector3 origin, Vector3 v, float g) => origin.y + v.y * v.y / (2f * g);

    [Test]
    public void LaunchVelocity_FlatTargetsLandAtTheTargetWithQuarterApex()
    {
        foreach (float d in new[] { 8f, 12f, 20f })
        {
            Vector3 origin = Vector3.zero, target = new Vector3(0f, 0f, d);
            Vector3 v = ThrowSolver.LaunchVelocity(origin, target, 45f, 9.81f);
            Assert.AreEqual(d / 4f, Apex(origin, v, 9.81f), 0.02f, "45 degrees: apex = distance / 4");
            float flight = 2f * v.y / 9.81f;
            Assert.AreEqual(d, new Vector2(v.x, v.z).magnitude * flight, 0.05f);
        }
    }

    [Test]
    public void LaunchVelocity_HigherTargetIsReachedWithSteeperAngle()
    {
        Vector3 origin = Vector3.zero, target = new Vector3(0f, 6f, 6f); // 45° 위
        Vector3 v = ThrowSolver.LaunchVelocity(origin, target, 45f, 9.81f);
        float angle = Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;
        Assert.Greater(angle, 50f);
        // 그 수평 거리에 왔을 때 높이가 목표 높이
        float horizontal = new Vector2(v.x, v.z).magnitude, t = 6f / horizontal;
        Assert.AreEqual(6f, v.y * t - 0.5f * 9.81f * t * t, 0.05f);
    }

    [Test]
    public void Sim_FlatThrowFirstTouchesGroundNearTheTarget()
    {
        Vector3 origin = new Vector3(0f, 1.3f, 0f), target = new Vector3(0f, 0f, 12f);
        var s = ProjectileState.Launch(origin, ThrowSolver.LaunchVelocity(origin, target, 45f, 9.81f));
        BallisticSim.Sweep world = World();
        for (int i = 0; i < 600 && s.FirstImpactAt < 0f; i++) BallisticSim.Advance(ref s, config, world, out _);
        Assert.GreaterOrEqual(s.FirstImpactAt, 0f);
        Assert.AreEqual(12f, s.Position.z, 0.6f, "lands where the aim pointed (sphere radius + step)");
    }

    [Test]
    public void Sim_SameInputGivesTheSameTrajectory()
    {
        Vector3 origin = new Vector3(1f, 1.3f, -2f), v = new Vector3(3f, 7f, 8f);
        var a = ProjectileState.Launch(origin, v);
        var b = ProjectileState.Launch(origin, v);
        BallisticSim.Sweep world = World(6f);
        for (int i = 0; i < 300; i++)
        {
            BallisticSim.Advance(ref a, config, world, out _);
            BallisticSim.Advance(ref b, config, world, out _);
        }
        Assert.AreEqual(a.Position, b.Position);
        Assert.AreEqual(a.Bounces, b.Bounces);
    }

    [Test]
    public void Sim_BouncesLoseSpeedThenRollsAndRests()
    {
        var s = ProjectileState.Launch(new Vector3(0f, 1.3f, 0f), new Vector3(0f, 6f, 8f));
        BallisticSim.Sweep world = World();
        float lastBounceSpeed = float.MaxValue;
        int bounces = 0;
        for (int i = 0; i < 600 && !s.Resting; i++)
        {
            float before = s.Velocity.magnitude;
            BallisticSim.Advance(ref s, config, world, out _);
            if (s.Bounces > bounces)
            {
                bounces = s.Bounces;
                Assert.Less(before, lastBounceSpeed + 1e-3f, "each bounce comes in slower");
                lastBounceSpeed = before;
            }
        }
        Assert.GreaterOrEqual(bounces, 1);
        Assert.IsTrue(s.Resting, "comes to rest on the ground");
        Assert.AreEqual(config.radius, s.Position.y, 0.05f, "rests on the ground");
        Assert.Greater(s.Position.z, 8f, "rolled forward after the first bounce");
    }

    [Test]
    public void Sim_WallBouncesBack()
    {
        var s = ProjectileState.Launch(new Vector3(0f, 2f, 0f), new Vector3(10f, 4f, 0f));
        BallisticSim.Sweep world = World(4f);
        for (int i = 0; i < 300 && s.Bounces == 0; i++) BallisticSim.Advance(ref s, config, world, out _);
        Assert.AreEqual(1, s.Bounces);
        Assert.Less(s.Velocity.x, 0f, "moves away from the wall");
        Assert.LessOrEqual(s.Position.x, 4f - config.radius + 0.01f);
    }

    [Test]
    public void Fuse_GoesOffTwoSecondsAfterFirstImpactOrAtMaxLife()
    {
        var s = new ProjectileState { FirstImpactAt = -1f, Age = 4.9f };
        Assert.IsFalse(BallisticSim.FuseDone(s, config));
        s.Age = 5f;
        Assert.IsTrue(BallisticSim.FuseDone(s, config), "max life");
        s = new ProjectileState { FirstImpactAt = 1f, Age = 2.9f };
        Assert.IsFalse(BallisticSim.FuseDone(s, config));
        s.Age = 3.01f;
        Assert.IsTrue(BallisticSim.FuseDone(s, config), "2 s after first touch");
    }

    [Test]
    public void Predict_StopsAtFirstImpactForBalloonsAndFollowsBouncesForBombs()
    {
        Vector3 origin = new Vector3(0f, 1.3f, 0f);
        Vector3 v = ThrowSolver.LaunchVelocity(origin, new Vector3(0f, 0f, 10f), 45f, 9.81f);
        var points = new Vector3[120];
        int n = BallisticSim.Predict(origin, v, config, World(), points, true, out int first, out Vector3 end);
        Assert.Greater(n, 5);
        Assert.AreEqual(n - 1, first, "balloon path ends at the first touch");
        Assert.AreEqual(10f, end.z, 0.6f);
        int m = BallisticSim.Predict(origin, v, config, World(), points, false, out int first2, out Vector3 end2);
        Assert.Greater(m, n);
        Assert.Greater(end2.z, end.z, "the bomb bounces and rolls further");
        Assert.GreaterOrEqual(first2, 0);
    }

    [Test]
    public void MaxSpeed_CoversMaxRangeButNotMuchMore()
    {
        float flat = ThrowSolver.FlatSpeed(config);
        Assert.AreEqual(Mathf.Sqrt(9.81f * 20f), flat, 0.05f, "45 degrees: v = sqrt(g d)");
        Assert.AreEqual(flat * ThrowSolver.SpeedSlack, ThrowSolver.MaxSpeed(config), 1e-3f);
    }

    // §71 ①: 물풍선 20° + 중력 14.2는 예전(45° + 9.81)보다 정확히 1.5배 빠르고, 같은 자리에 떨어진다.
    [Test]
    public void Balloon_TwentyDegreesWithHeavierGravityIsOneAndAHalfTimesFaster()
    {
        foreach (float d in new[] { 6f, 12f, 20f })
        {
            Vector3 o = Vector3.zero, t = new Vector3(0f, 0f, d);
            float before = ThrowSolver.LaunchVelocity(o, t, 45f, 9.81f).magnitude;
            Vector3 v = ThrowSolver.LaunchVelocity(o, t, 20f, 14.2f);
            Assert.AreEqual(1.5f, v.magnitude / before, 0.01f);
            float flight = 2f * v.y / 14.2f;
            Assert.AreEqual(d, new Vector2(v.x, v.z).magnitude * flight, 0.05f, "still lands on the aimed spot");
        }
    }

    // §71 추가: 수색 폭탄도 물풍선과 같은 방식(20°·중력 14.2)으로 던진다. 튕기고 1초 뒤 터지는 성질은 그대로.
    [Test]
    public void SearchBomb_IsThrownLikeTheBalloon()
    {
        var balloon = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectileConfigSO>("Assets/03. SO/Escape/Projectiles/Projectile_WaterBalloon.asset");
        var search = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectileConfigSO>("Assets/03. SO/Escape/Projectiles/Projectile_SearchBomb.asset");
        Assert.IsNotNull(balloon);
        Assert.IsNotNull(search);
        Assert.AreEqual(balloon.launchAngle, search.launchAngle, 1e-4f);
        Assert.AreEqual(14.2f, search.gravity, 1e-4f, "search bomb keeps the §71 throw");
        // 2026-11-14: 물풍선만 1.5배 더 빠르게 — 같은 각도에서 중력 × 2.25 → 속도 × 1.5, 떨어지는 자리는 같다
        Assert.AreEqual(14.2f * 2.25f, balloon.gravity, 1e-3f);
        foreach (float d in new[] { 6f, 12f, 20f })
        {
            Vector3 t = new Vector3(0f, 0f, d);
            float before = ThrowSolver.LaunchVelocity(Vector3.zero, t, 20f, 14.2f).magnitude;
            Vector3 v = ThrowSolver.LaunchVelocity(Vector3.zero, t, balloon.launchAngle, balloon.gravity);
            Assert.AreEqual(1.5f, v.magnitude / before, 0.01f);
            Assert.AreEqual(d, new Vector2(v.x, v.z).magnitude * (2f * v.y / balloon.gravity), 0.05f, "still lands on the aimed spot");
        }
        Assert.IsFalse(search.burstOnImpact);
        Assert.AreEqual(1f, search.fuseAfterImpact, 1e-4f);
    }
}
