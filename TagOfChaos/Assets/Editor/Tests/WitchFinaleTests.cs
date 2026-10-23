using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// 마녀 연출 다시 짜기(Request1009Plan.md §23) — 마녀가 걷는 길(Ground 경계에 닿는 자리), 넓힌 배경, 박살 대상(맵을 바꿔도 동작하는 규칙).
public class WitchFinaleTests
{
    private static readonly string[] Maps = { "CandyForest", "GingerbreadVillage", "ChocolateFactory", "CursedCandyCarnival", "HauntedBakery" };

    // ---- 내리칠 자리: 변에 닿든 모서리에 닿든 발끝이 Ground 경계에 닿는 곳(§23.4 Q6) ----

    [Test]
    public void StrikePoint_StopsWhereTheFootTouchesAnEdge()
    {
        var ground = Rect.MinMaxRect(-71f, -71f, 71f, 71f);
        Vector3 outward = Vector3.forward;                        // 마녀는 +Z 쪽에서 온다
        Vector3 start = outward * 450f;
        Vector3 feet = WitchPath.StrikePoint(ground, start, outward, 11f, out Vector3 contact);
        Assert.AreEqual(71f, contact.z, 1e-3f, "touches the +Z edge");
        Assert.AreEqual(0f, contact.x, 1e-3f);
        Assert.AreEqual(71f + 11f, feet.z, 1e-3f, "feet stand one foot-reach outside");
    }

    [Test]
    public void StrikePoint_StopsAtACornerWhenComingDiagonally()
    {
        var ground = Rect.MinMaxRect(-71f, -71f, 71f, 71f);
        Vector3 outward = new Vector3(-1f, 0f, -1f).normalized;
        Vector3 feet = WitchPath.StrikePoint(ground, outward * 450f, outward, 11f, out Vector3 contact);
        Assert.AreEqual(-71f, contact.x, 1e-2f);
        Assert.AreEqual(-71f, contact.z, 1e-2f, "the corner");
        Assert.AreEqual(11f, (feet - contact).magnitude, 1e-2f);
        Assert.IsFalse(ground.Contains(new Vector2(feet.x, feet.z)), "the witch never steps inside the Ground");
    }

    [Test]
    public void StrikePoint_WorksForAnOffCentreRectangle()
    {
        var ground = Rect.MinMaxRect(10f, -20f, 90f, 40f);       // 새 맵: 중심이 원점이 아니어도
        var center = new Vector3(ground.center.x, 0f, ground.center.y);
        Vector3 outward = WitchPath.OutwardDirection(center, center + Vector3.right * 100f);
        WitchPath.StrikePoint(ground, center + outward * 450f, outward, 5f, out Vector3 contact);
        Assert.AreEqual(90f, contact.x, 1e-3f);
    }

    // ---- 맵 5개(씬) ----

    [TestCaseSource(nameof(Maps))]
    public void Map_HasGroundPathBackdropAndBreakables(string map)
    {
        var scene = EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);
        Assert.IsTrue(WitchPath.TryGetGroundRect(scene, out Rect ground), $"{map}: Map/Ground");
        WitchPresenter witch = Object.FindFirstObjectByType<WitchPresenter>(FindObjectsInactive.Include);
        Assert.NotNull(witch, $"{map}: WITCH_Anchor");
        Assert.AreEqual(WitchPresenter.DefaultHeight, witch.Height, 1e-3f, $"{map}: witch is 3x (§24.10)");
        var center = new Vector3(ground.center.x, 0f, ground.center.y);
        Vector3 outward = WitchPath.OutwardDirection(center, witch.transform.position);
        Vector3 start = center + outward * WitchPath.StartDistance;
        Vector3 feet = WitchPath.StrikePoint(ground, start, outward, 90f * WitchPath.FootReachRatio, out Vector3 contact);
        Assert.IsFalse(ground.Contains(new Vector2(feet.x, feet.z)), $"{map}: strikes from outside the Ground");
        Assert.Less(Vector3.Distance(feet, center), Vector3.Distance(start, center), $"{map}: walks toward the map");

        // 넓힌 배경: 콜라이더 없음, 걷는 길에 소품 없음
        Transform backdrop = scene.GetRootGameObjects().First(g => g.name == "Map").transform.Find(WitchBackdropBuilder.RootName);
        Assert.NotNull(backdrop, $"{map}: Map/{WitchBackdropBuilder.RootName}");
        Assert.IsEmpty(backdrop.GetComponentsInChildren<Collider>(true), $"{map}: far backdrop has no colliders");
        Bounds far = backdrop.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        Assert.Greater(far.extents.x, WitchPath.StartDistance, $"{map}: ground reaches past where the witch appears");
        foreach (Transform prop in backdrop.Cast<Transform>().Where(t => t.name != "FarGround"))
            Assert.IsFalse(WitchBackdropBuilder.OnWitchPath(prop.position, center, outward, WitchBackdropBuilder.PathHalfWidth), $"{map}: {prop.name} stands on the witch path");

        // 박살 대상: 있고, 바닥·배경·캐릭터는 아니다
        var groups = WitchShatter.FindTargetGroups(scene, ground);
        Assert.Greater(groups.Count, 20, $"{map}: something to smash");
        foreach (var g in groups)
            foreach (Renderer r in g.renderers)
            {
                for (Transform t = r.transform; t != null; t = t.parent)
                    Assert.That(t.name, Is.Not.EqualTo(WitchPath.GroundName).And.Not.EqualTo("Background").And.Not.EqualTo(WitchBackdropBuilder.RootName), $"{map}: {r.name}");
                Assert.IsNull(r.GetComponentInParent<WitchPresenter>(), $"{map}: {r.name}");
            }
    }

    // ---- §24.10 도착 3테이크 ----

    [Test]
    public void FinaleTimeline_ThreeTakesThenResult()
    {
        Assert.AreEqual(WitchShot.None, WitchTimeline.FinaleShotAt(-0.1));
        Assert.AreEqual(WitchShot.FinaleLowAngle, WitchTimeline.FinaleShotAt(0.0));
        Assert.AreEqual(WitchShot.FinaleLowAngle, WitchTimeline.FinaleShotAt(5.9));
        Assert.AreEqual(WitchShot.FinaleStomp, WitchTimeline.FinaleShotAt(6.1));
        Assert.AreEqual(WitchShot.FinaleStomp, WitchTimeline.FinaleShotAt(9.9));
        Assert.AreEqual(WitchShot.FinaleEnd, WitchTimeline.FinaleShotAt(10.1));
        Assert.AreEqual(10f, WitchTimeline.FinaleImpactSeconds, "deaths when the sole lands");
        Assert.AreEqual(15f, EscapeRules.WitchSlamSeconds, "result 5 s after the stomp");
        Assert.IsFalse(WitchTimeline.InFinale(20, 10, resultDecided: true), "result screen ends the finale");
        Assert.IsTrue(WitchTimeline.InFinale(20, 10, resultDecided: false));

        // 방장: 도착하면 기록, 밟은 뒤 5초(기록 + 15초)에 끝
        var ta = new List<EscapeRules.PlayerInfo> { new EscapeRules.PlayerInfo { IsMonster = true }, new EscapeRules.PlayerInfo() };
        Assert.AreEqual(EscapeRules.Decision.StrikeWitch, EscapeRules.Evaluate(ta, true, 600, 600, -1f));
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(ta, true, 612, 600, 12f), "still showing the finale");
        Assert.AreEqual(EscapeRules.Decision.EndWitchStrike, EscapeRules.Evaluate(ta, true, 615, 600, 15f));
    }

    [Test]
    public void Stomp_LiftsThenLowersTheSoleSlowlyAndLandsAtImpact()
    {
        Assert.AreEqual(0f, WitchPresenter.StompLegAngle(-1f));
        Assert.AreEqual(WitchPresenter.StompLiftDegrees, WitchPresenter.StompLegAngle(WitchPresenter.StompLiftSeconds), 1e-3f, "knee up");
        float prev = WitchPresenter.StompLiftDegrees;
        for (float t = WitchPresenter.StompLiftSeconds + 0.2f; t < WitchTimeline.FinaleStompSeconds; t += 0.2f)
        {
            float a = WitchPresenter.StompLegAngle(t);
            Assert.Less(a, prev + 1e-4f, "the sole only comes down");
            prev = a;
        }
        Assert.AreEqual(0f, WitchPresenter.StompLegAngle(WitchTimeline.FinaleStompSeconds), 1e-3f, "lands at the impact");
        float early = WitchPresenter.StompLegAngle(WitchPresenter.StompLiftSeconds) - WitchPresenter.StompLegAngle(WitchPresenter.StompLiftSeconds + 0.5f);
        float late = WitchPresenter.StompLegAngle(WitchTimeline.FinaleStompSeconds - 0.5f);
        Assert.Less(early, late, "slow at first, fast at the end");
        Assert.AreEqual(0f, WitchPresenter.StompShiftWeight(0f));
        Assert.AreEqual(1f, WitchPresenter.StompShiftWeight(WitchPresenter.StompLiftSeconds + 1f));
    }

    private static bool InView(Vector3 pos, Quaternion rot, float fov, Vector3 p)
    {
        var go = new GameObject("__finaleCam");
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov; cam.aspect = 16f / 9f;
            Vector3 v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.x > -0.01f && v.x < 1.01f && v.y > -0.01f && v.y < 1.01f;
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void FinaleCamera_LowAngleRisesThenPullsBackToTheWitchFace()
    {
        const float h = WitchPresenter.DefaultHeight;
        Vector3 stage = new Vector3(0f, 0f, 50f), inward = Vector3.back;
        Vector3 face = new Vector3(0f, h * 0.8f, 130f);
        System.Func<Vector3, float> flat = p => 0f;
        for (float t = 0f; t <= WitchTimeline.FinaleLowAngleSeconds; t += 0.5f)
        {
            WitchRevealCamera.LowAnglePose(stage, inward, 7f, face, h, t, flat, out Vector3 pos, out Quaternion rot, out float fov);
            Assert.LessOrEqual(pos.y, WitchRevealCamera.LowEndEye + 1e-3f, $"low angle at {t}");
            Assert.Greater(Vector3.Dot(rot * Vector3.forward, Vector3.up), 0f, $"looks up at {t}");
            Assert.That(fov, Is.InRange(WitchRevealCamera.MinFinaleFov, WitchRevealCamera.MaxFinaleFov));
            Assert.IsTrue(InView(pos, rot, fov, stage + Vector3.up * 1f), $"stand-ins in view at {t}");
        }
        WitchRevealCamera.LowAnglePose(stage, inward, 7f, face, h, 0f, flat, out Vector3 p0, out _, out _);
        Assert.AreEqual(WitchRevealCamera.LowStartEye, p0.y, 1e-3f, "starts at the ground");
        WitchRevealCamera.LowAnglePose(stage, inward, 7f, face, h, WitchTimeline.FinaleLowAngleSeconds, flat, out Vector3 p6, out Quaternion r6, out float f6);
        Assert.AreEqual(h * WitchRevealCamera.LowEndDistanceRatio, Vector3.Distance(new Vector3(p6.x, 0f, p6.z), stage), 1f, "pulled back");
        Assert.IsTrue(InView(p6, r6, f6, face), "the witch face at the end");
    }

    [Test]
    public void FinaleCamera_StompLooksUpAtTheSoleAndEndShowsTheWitch()
    {
        const float h = WitchPresenter.DefaultHeight;
        Vector3 stage = new Vector3(0f, 0f, 50f), inward = Vector3.back;
        Vector3 target = stage + inward * WitchPresenter.StompInset;
        WitchRevealCamera.StompPose(stage, inward, h, p => 0f, out Vector3 pos, out Quaternion rot, out float fov);
        Assert.AreEqual(WitchRevealCamera.StompCameraEye, pos.y, 1e-3f);
        foreach (float sole in new[] { 50f, 30f, 15f })
            Assert.IsTrue(InView(pos, rot, fov, target + Vector3.up * sole), $"sole {sole} m above the stomp target");

        Vector3 face = target + Vector3.up * (h * 0.8f) - inward * 20f;
        WitchRevealCamera.EndPose(target, inward, h, face, out Vector3 ePos, out Quaternion eRot, out float eFov);
        Assert.Greater(ePos.y, target.y + h * 0.3f, "from above");
        Assert.Greater(Vector3.Dot(Vector3.ProjectOnPlane(ePos - target, Vector3.up).normalized, inward), 0.9f, "in front (map side)");
        Assert.IsTrue(InView(ePos, eRot, eFov, target), "the stomped ground");
        Assert.IsTrue(InView(ePos, eRot, eFov, face), "the witch");
    }

    [Test]
    public void FinaleFade_BlackAtStartAndAtTheStomp()
    {
        Assert.AreEqual(1f, WitchRevealCamera.FinaleFadeAlpha(0f));
        Assert.AreEqual(0f, WitchRevealCamera.FinaleFadeAlpha(2f));
        Assert.AreEqual(0f, WitchRevealCamera.FinaleFadeAlpha(8f));
        Assert.Greater(WitchRevealCamera.FinaleFadeAlpha(9.9f), 0.5f, "darkens as the sole lands");
        Assert.AreEqual(1f, WitchRevealCamera.FinaleFadeAlpha(10.2f), "black after the stomp");
        Assert.AreEqual(0f, WitchRevealCamera.FinaleFadeAlpha(11.5f), "then the end shot");
    }

    [Test]
    public void FinaleStage_ChoosesAndArrangesStandIns()
    {
        var people = new List<(int, bool, bool, bool)>
        {
            (4, false, false, false), // 쿠키
            (2, true, false, false),  // 괴물
            (3, false, true, false),  // 탈출한 쿠키
            (5, false, false, true),  // 부서진 쿠키
            (6, false, false, false),  // 스파이
            (1, false, false, false), // 쿠키
        };
        var ta = WitchFinaleStage.Choose(people);
        CollectionAssert.AreEqual(new[] { 1, 2, 4, 6 }, ta.Select(m => m.Actor).ToArray(), "time attack: monsters + remaining cookies and spies, by actor");
        Assert.AreEqual(1, ta.First(m => m.Actor == 2).Index, "keeps which character it was");

        Vector3 stage = new Vector3(10f, 0f, 20f), inward = Vector3.back;
        var places = WitchFinaleStage.Arrange(ta, stage, inward);
        Assert.AreEqual(4, places.Count);
        var monster = places.Single(p => p.IsMonster);
        Assert.AreEqual(stage, monster.Position);
        Assert.Greater(Vector3.Dot(monster.Rotation * Vector3.forward, inward), 0.99f, "monster turns its back to the witch");
        var held = places.Where(p => p.HeldBy == 0).ToList();
        Assert.AreEqual(1, held.Count, "one cookie per monster");
        Assert.AreEqual(1, held[0].Actor, "lowest actor is held");
        foreach (var p in places.Where(p => !p.IsMonster && p.HeldBy < 0))
            Assert.AreEqual(WitchFinaleStage.CookieRingRadius, Vector3.Distance(p.Position, stage), 1e-3f, "others stand in the ring");

        var two = WitchFinaleStage.Arrange(new List<WitchFinaleStage.Member> { new WitchFinaleStage.Member { Actor = 1, IsMonster = true }, new WitchFinaleStage.Member { Actor = 2, IsMonster = true } }, stage, inward);
        // 같은 사람의 쿠키·괴물(오프라인 시험)도 둘 다 선다
        var same = WitchFinaleStage.Choose(new List<(int, bool, bool, bool)> { (1, false, false, false), (1, true, false, false) });
        Assert.AreEqual(2, same.Count);
        Assert.IsTrue(same[0].IsMonster, "monster first on a tie");
        Assert.AreEqual(WitchFinaleStage.MonsterSpacing, Vector3.Distance(two[0].Position, two[1].Position), 1e-3f);
    }

    [TestCase("HideOrSeekPlayer")]
    [TestCase("MonsterPlayer")]
    public void FinaleStage_CloneKeepsOnlyVisuals(string prefabName)
    {
        var holder = new GameObject("__holder");
        holder.SetActive(false);
        try
        {
            GameObject copy = Object.Instantiate(Resources.Load<GameObject>(prefabName), holder.transform);
            WitchFinaleStage.StripToVisuals(copy);
            foreach (Component c in copy.GetComponentsInChildren<Component>(true))
                Assert.That(c is Transform || c is Animator || c is Renderer || c is MeshFilter, Is.True, $"{prefabName}: {c.GetType().Name} left");
            Assert.IsNull(copy.transform.Find("Nameplate"), "nameplate removed");
            Assert.IsNotEmpty(copy.GetComponentsInChildren<SkinnedMeshRenderer>(true), "body kept");
            Assert.NotNull(copy.GetComponent<Animator>());
            LogAssert.NoUnexpectedReceived();
        }
        finally { Object.DestroyImmediate(holder); }
    }

    [Test]
    public void WitchModel_IsThreeTimesBigger()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WitchModelSetup.PrefabPath);
        Transform model = prefab.transform.Find("Witch");
        Assert.NotNull(model);
        Assert.AreEqual(270f, WitchModelSetup.TargetHeight, 1e-3f);
        Assert.AreEqual(WitchModelSetup.TargetHeight / WitchModelSetup.SourceHeight, model.localScale.y, 1e-2f, "Blender height 1.643 m -> 270 m");
        Assert.AreEqual(3f * 90f / WitchModelSetup.SourceHeight, model.localScale.x, 1e-2f, "3x the old 90 m witch");
    }
}
