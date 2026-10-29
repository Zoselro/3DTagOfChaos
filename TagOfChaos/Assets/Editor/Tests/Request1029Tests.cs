using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Request1009Plan.md §45 — 캐릭터 5종·새 동작·뿅망치/스턴건/물풍선 개편.
public class Request1029Tests
{
    // ---------------- 물풍선 포물선 ----------------

    [Test]
    public void BalloonArc_ApexFlightAndEndpoints()
    {
        var from = new Vector3(0, 1.3f, 0);
        var to = new Vector3(0, 1f, 8f);
        Assert.AreEqual(1.3f + Mathf.Max(1.5f, 8f * 0.35f), ToolAim.ApexHeight(from, to), 1e-4f, "apex = higher end + max(1.5, d*0.35)");
        Assert.AreEqual(0.5f + 8f * 0.05f, ToolAim.FlightSeconds(from, to), 1e-4f);
        Assert.AreEqual(1.3f + 1.5f, ToolAim.ApexHeight(from, new Vector3(0, 1f, 2f)), 1e-4f, "short throws still rise at least 1.5 m");
        float apex = ToolAim.ApexHeight(from, to);
        Assert.Less(Vector3.Distance(ToolAim.ArcPoint(from, to, apex, 0f), from), 1e-4f);
        Assert.Less(Vector3.Distance(ToolAim.ArcPoint(from, to, apex, 1f), to), 1e-4f);
        Assert.AreEqual(apex, ToolAim.ArcPoint(from, to, apex, 0.5f).y, 1e-3f, "reaches the apex half way");
    }

    [Test]
    public void BalloonArc_NeverDipsBelowItsEnds_AtAnyDistanceUpTo10m()
    {
        for (float d = 0.5f; d <= 10f; d += 0.5f)
        for (float dy = -3f; dy <= 3f; dy += 1.5f)
        {
            var from = new Vector3(0, 1.3f, 0);
            var to = new Vector3(d * 0.6f, 1.3f + dy, d * 0.8f);
            float apex = ToolAim.ApexHeight(from, to);
            float low = Mathf.Min(from.y, to.y) - 1e-3f;
            for (float t = 0f; t <= 1f; t += 0.05f)
                Assert.GreaterOrEqual(ToolAim.ArcPoint(from, to, apex, t).y, low, $"d={d} dy={dy} t={t}");
        }
    }

    // ---------------- 대상 고르기 ----------------

    [Test]
    public void Cone_HammerFiveMetersSixtyDegrees_PicksNearest()
    {
        Vector3 o = Vector3.zero, f = Vector3.forward;
        Assert.AreEqual(0, ToolAim.PickInCone(o, f, new[] { new Vector3(0, 0, 4.9f) }, 5f, ToolAim.HammerHalfAngle));
        Assert.AreEqual(-1, ToolAim.PickInCone(o, f, new[] { new Vector3(0, 0, 5.1f) }, 5f, ToolAim.HammerHalfAngle), "beyond 5 m");
        Vector3 inside = Quaternion.Euler(0, 29f, 0) * Vector3.forward * 3f, outside = Quaternion.Euler(0, 31f, 0) * Vector3.forward * 3f;
        Assert.AreEqual(0, ToolAim.PickInCone(o, f, new[] { inside }, 5f, ToolAim.HammerHalfAngle));
        Assert.AreEqual(-1, ToolAim.PickInCone(o, f, new[] { outside }, 5f, ToolAim.HammerHalfAngle), "outside the 60 deg cone");
        Assert.AreEqual(1, ToolAim.PickInCone(o, f, new[] { new Vector3(0, 0, 4f), new Vector3(0.5f, 0, 2f) }, 5f, ToolAim.HammerHalfAngle), "nearest");
        Assert.AreEqual(-1, ToolAim.PickInCone(o, f, new[] { new Vector3(0, 6f, 2f) }, 5f, ToolAim.HammerHalfAngle), "other floor");
    }

    [Test]
    public void Cone_BalloonTenMetersFortyFiveDegrees()
    {
        Vector3 o = Vector3.zero, f = Vector3.forward;
        Assert.AreEqual(0, ToolAim.PickInCone(o, f, new[] { new Vector3(0, 0, 9.9f) }, 10f, ToolAim.BalloonHalfAngle));
        Assert.AreEqual(-1, ToolAim.PickInCone(o, f, new[] { new Vector3(0, 0, 10.1f) }, 10f, ToolAim.BalloonHalfAngle));
        Assert.AreEqual(-1, ToolAim.PickInCone(o, f, new[] { Quaternion.Euler(0, 25f, 0) * Vector3.forward * 5f }, 10f, ToolAim.BalloonHalfAngle));
    }

    // ---------------- 물방울 크기·갇힘 ----------------

    [Test]
    public void Bubble_SizeFollowsTheBody()
    {
        Assert.AreEqual(2.11f * 1.35f, ToolAim.BubbleDiameter(new Vector3(1.25f, 2.11f, 0.83f)), 1e-3f, "cookie ~ 2.85 m");
        Assert.AreEqual(6.97f * 1.35f, ToolAim.BubbleDiameter(new Vector3(6.97f, 6.47f, 3.69f)), 1e-3f, "monster ~ 9.4 m");
    }

    [Test]
    public void Trap_LiftsFiveMetersInPointSixSeconds()
    {
        Assert.AreEqual(0f, WaterTrapMotion.LiftAt(0f), 1e-4f);
        Assert.AreEqual(5f, WaterTrapMotion.LiftAt(0.6f), 1e-4f);
        Assert.AreEqual(5f, WaterTrapMotion.LiftAt(4f), 1e-4f, "stays up while trapped");
        Assert.Greater(WaterTrapMotion.LiftAt(0.3f), 1f);
    }

    // ---------------- 도구 값 ----------------

    [Test]
    public void Tools_RangesStunsAndHold()
    {
        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        ItemSO hammer = catalog.Find("Hammer"), stun = catalog.Find("StunGun"), balloon = catalog.Find("WaterBalloon");
        Assert.AreEqual(5f, hammer.Tool.Range);
        Assert.AreEqual(3f, hammer.Tool.StunSecondsCookie, "hammer stun 3 s");
        Assert.AreEqual(3f, hammer.Tool.StunSecondsMonster);
        Assert.AreEqual(HoldType.TwoHanded, hammer.HoldType, "giant hammer in both hands");
        Assert.AreEqual(3f, hammer.HeldScale, "giant while held");
        Assert.AreEqual(20f, stun.Tool.Range);
        Assert.AreEqual(10f, balloon.Tool.Range);
        Assert.AreEqual(5f, balloon.Tool.StunSecondsCookie, "trapped 5 s");
        Assert.AreEqual(5f, balloon.Tool.StunSecondsMonster);
        Assert.IsFalse(balloon.Tool.PaintsTarget, "no more paint (Q2)");
        foreach (ItemSO t in catalog.SpyRandomTools) Assert.AreNotEqual("JailBalloon", t.ItemId, "jail balloon merged into the water balloon (Q1)");
    }

    // ---------------- 캐릭터 5종 ----------------

    [Test]
    public void Characters_FiveSlotsWithHumanoidModels()
    {
        SkinCatalogSO catalog = GameSettings.Current.CharacterCatalog;
        Assert.AreEqual(5, catalog.Count);
        Assert.IsNull(catalog.GetModel(0), "slot 0 = the cookie body");
        var labels = new HashSet<string>();
        for (int i = 0; i < catalog.Count; i++)
        {
            Assert.IsFalse(string.IsNullOrEmpty(catalog.GetLabel(i)), $"slot {i} label");
            Assert.IsTrue(labels.Add(catalog.GetLabel(i)), "labels are unique");
            Assert.IsNotNull(catalog.GetCrumbColors(i));
            Assert.Greater(catalog.GetCrumbColors(i).Length, 0, $"slot {i} crumb colors");
            if (i == 0) continue;
            GameObject model = catalog.GetModel(i);
            Assert.IsNotNull(model, $"slot {i} model");
            Animator a = model.GetComponentInChildren<Animator>(true);
            Assert.IsTrue(a != null && a.avatar != null && a.avatar.isHuman && a.avatar.isValid, $"{model.name} humanoid avatar");
            int tris = 0;
            foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) tris += r.sharedMesh.triangles.Length / 3;
            Assert.LessOrEqual(tris, 25000, $"{model.name} triangles");
        }
        Assert.IsFalse(PlayerSkinSelector.IsTakenByOther(1), "duplicate check is room based (offline: free)");
    }

    [Test]
    public void ModelSwap_ReplacesAndRestoresTheCookieBody()
    {
        var prefab = Resources.Load<GameObject>("HideOrSeekPlayer");
        GameObject go = Object.Instantiate(prefab);
        try
        {
            Animator a = go.GetComponent<Animator>();
            Avatar cookie = a.avatar;
            GameObject model = GameSettings.Current.CharacterCatalog.GetModel(3);
            Assert.IsTrue(CharacterModelSwap.Apply(go.transform, model));
            Assert.IsTrue(CharacterModelSwap.HasModel(go.transform));
            Assert.AreNotEqual(cookie, a.avatar);
            Assert.IsTrue(a.avatar.isHuman);
            Assert.IsNull(go.transform.Find(CharacterModelSwap.CookieArmatureName), "cookie bones hidden (renamed) so humanoid binding is not ambiguous");
            Assert.IsFalse(go.transform.Find(CharacterModelSwap.CookieBodyName).GetComponent<Renderer>().enabled);
            Assert.IsFalse(CharacterModelSwap.Apply(go.transform, model), "same model: nothing to do");

            Assert.IsTrue(CharacterModelSwap.Apply(go.transform, null));
            Assert.IsNotNull(go.transform.Find(CharacterModelSwap.CookieArmatureName), "cookie bones back");
            Assert.IsTrue(go.transform.Find(CharacterModelSwap.CookieBodyName).GetComponent<Renderer>().enabled);
            Assert.AreEqual(cookie, a.avatar);
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ---------------- 새 동작 ----------------

    [Test]
    public void Animator_HasActionAndTrappedLayers()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DanceStateBuilder.ControllerPath);
        var layers = new Dictionary<string, AnimatorControllerLayer>();
        foreach (AnimatorControllerLayer l in controller.layers) layers[l.name] = l;
        Assert.IsTrue(layers.ContainsKey(PlayerActionPose.ActionLayer) && layers.ContainsKey(PlayerActionPose.TrappedLayer));
        Assert.IsNotNull(layers[PlayerActionPose.ActionLayer].avatarMask, "upper body only");
        var motions = new Dictionary<string, Motion>();
        foreach (ChildAnimatorState s in layers[PlayerActionPose.ActionLayer].stateMachine.states) motions[s.state.name] = s.state.motion;
        foreach (ChildAnimatorState s in layers[PlayerActionPose.TrappedLayer].stateMachine.states) motions[s.state.name] = s.state.motion;
        AssertClip(motions, PlayerActionPose.StunAimState, true, 1f);
        AssertClip(motions, PlayerActionPose.ThrowState, false, PlayerActionPose.ThrowSeconds);
        AssertClip(motions, PlayerActionPose.SwingState, false, PlayerActionPose.SwingSeconds);
        AssertClip(motions, PlayerActionPose.TrappedState, true, 1.6f);
        var names = new List<string>();
        foreach (AnimatorControllerLayer l in controller.layers) names.Add(l.name);
        Assert.Greater(names.IndexOf(PlayerActionPose.ActionLayer), names.IndexOf("Carry"), "swing covers the two-handed carry pose");
    }

    private static void AssertClip(Dictionary<string, Motion> motions, string state, bool loop, float seconds)
    {
        Assert.IsTrue(motions.TryGetValue(state, out Motion m) && m is AnimationClip, state);
        var clip = (AnimationClip)m;
        Assert.IsTrue(clip.isHumanMotion, state + " humanoid (shared by all 5 characters)");
        Assert.AreEqual(loop, clip.isLooping, state + " loop");
        Assert.AreEqual(seconds, clip.length, 0.05f, state + " length");
    }
}
