using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Request1009Plan.md §88 — 두 손 아이템을 가로 길이에 맞춰 잡기(TwoHandGrip·ItemGripMeasure·CarryItem 상태).
public class Request1121Tests
{
    // 쿠키 비슷한 어깨·팔(팔 0.5 m, 어깨 ±0.23)과 머리 큰 캐릭터(팔 0.39 m, 어깨 ±0.21).
    private static TwoHandGrip.Input Cookie(float halfWidth, float halfDepth, float bodyFront = 0.12f) => new TwoHandGrip.Input
    {
        ShoulderR = new Vector3(0.23f, 1.26f, 0.01f), ShoulderL = new Vector3(-0.23f, 1.26f, 0.01f), ArmR = 0.5f, ArmL = 0.5f,
        BodyFrontZ = bodyFront, HalfWidth = halfWidth, HalfDepth = halfDepth, Inset = -0.02f,
    };

    private static TwoHandGrip.Input Chibi(float halfWidth, float halfDepth, float bodyFront) => new TwoHandGrip.Input
    {
        ShoulderR = new Vector3(0.21f, 0.73f, -0.10f), ShoulderL = new Vector3(-0.21f, 0.73f, -0.10f), ArmR = 0.39f, ArmL = 0.39f,
        BodyFrontZ = bodyFront, HalfWidth = halfWidth, HalfDepth = halfDepth, Inset = -0.02f,
    };

    // 좁은 배터리(0.35 m)와 넓은 공구상자(0.73 m): 손 x = ±(가로/2 + 2 cm), 아이템 가운데는 몸 가운데.
    [Test]
    public void Grip_HandsAtItemEdges()
    {
        foreach (float half in new[] { 0.175f, 0.365f })
        {
            TwoHandGrip.Result r = TwoHandGrip.Solve(Cookie(half, 0.18f));
            Assert.AreEqual(half + 0.02f, r.HandR.x, 1e-4f, "right hand on the right edge");
            Assert.AreEqual(-(half + 0.02f), r.HandL.x, 1e-4f, "left hand on the left edge");
            Assert.AreEqual(0f, r.ItemCenter.x, 1e-4f);
            Assert.AreEqual(r.HandR.y, r.ItemCenter.y, 1e-4f, "hands at the grip height");
        }
        TwoHandGrip.Result narrow = TwoHandGrip.Solve(Cookie(0.02f, 0.1f));
        Assert.AreEqual(TwoHandGrip.MinHalfWidth + 0.02f, narrow.HandR.x, 1e-4f, "very narrow items keep the hands apart");
    }

    // 높이 규칙은 §87 뒤 값 그대로(어깨 − 팔 × 0.12).
    [Test]
    public void Grip_KeepsHeightRule()
    {
        TwoHandGrip.Result r = TwoHandGrip.Solve(Cookie(0.3f, 0.13f));
        Assert.AreEqual(1.26f - 0.5f * TwoHandGrip.Drop, r.HandR.y, 1e-4f);
    }

    // 깊은 사탕 전지(깊이 0.5 m): 아이템 뒷면이 몸 앞면 + 2 cm보다 앞.
    [Test]
    public void Grip_ItemNeverInsideBody()
    {
        TwoHandGrip.Input g = Cookie(0.25f, 0.25f, 0.2f);
        TwoHandGrip.Result r = TwoHandGrip.Solve(g);
        Assert.GreaterOrEqual(r.ItemCenter.z - g.HalfDepth, g.BodyFrontZ + TwoHandGrip.BodyGap - 1e-4f);
    }

    // 머리 큰 캐릭터 + 0.6 m 바퀴(몸 앞면이 앞이라 아이템이 멀리): 손은 옆면 뒤쪽을 잡는다(옆면 범위 안, 아이템 가운데보다 뒤).
    [Test]
    public void Grip_ShortArmsHoldSideBack()
    {
        TwoHandGrip.Input g = Chibi(0.3f, 0.13f, 0.17f);
        TwoHandGrip.Result r = TwoHandGrip.Solve(g);
        float lo = r.ItemCenter.z - g.HalfDepth + TwoHandGrip.SideMargin;
        Assert.GreaterOrEqual(r.HandR.z, lo - 1e-4f, "on the side face");
        Assert.LessOrEqual(r.HandR.z, r.ItemCenter.z + 1e-4f, "back half of the side");
        // 팔 길이 안에 있으면 닿는다고 본다
        float d = Vector3.Distance(g.ShoulderR, r.HandR);
        Assert.AreEqual(d <= g.ArmR * TwoHandGrip.MaxStretch + 1e-3f, r.Reachable);
    }

    // 렌더러 Transform을 거쳐 재므로 모델 루트 크기(마카롱 바퀴 100배)가 들어간다 — 메시 경계만 보면 0.01 m.
    [Test]
    public void Measure_UsesModelRootScale()
    {
        var socket = new GameObject("socket").transform;
        try
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(socket, false);
            model.transform.localScale = new Vector3(52f, 52f, 37f) * 0.01f * 100f / 100f; // 0.52 × 0.52 × 0.37
            var root = new GameObject("root100").transform;
            root.SetParent(socket, false);
            root.localScale = Vector3.one * 100f;
            model.transform.SetParent(root, true); // 루트가 100배, 자식 메시는 0.0052 배
            ItemGripSize s = ItemGripMeasure.Measure(root.gameObject, socket, Quaternion.identity, 0.5f);
            Assert.AreEqual(0.26f, s.HalfWidth, 0.01f);
            Assert.AreEqual(0.185f, s.HalfDepth, 0.01f);
            Assert.AreEqual(0.26f, s.Below, 0.01f);
            Assert.AreEqual(0.26f, s.Above, 0.01f);
            ItemGripSize o = ItemGripMeasure.WithOverride(s, 0.2f);
            Assert.AreEqual(0.2f, o.HalfWidth, 1e-4f, "asset override");
        }
        finally { Object.DestroyImmediate(socket.gameObject); }
    }

    // 실제 두 손 아이템 에셋: 모두 잴 수 있는 크기(0.3~0.8 m)이고 잡는 값은 기본(자동).
    [Test]
    public void TwoHandedItems_HaveMeasurableWidth()
    {
        var socket = new GameObject("socket").transform;
        try
        {
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemSO"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.HoldType != HoldType.TwoHanded || (item.IsTool && item.Tool.Kind == ToolKind.Hammer)) continue;
                GameObject model = EscapeVisuals.CreateItemModel(item, socket);
                ItemGripSize s = ItemGripMeasure.Measure(model, socket, Quaternion.Euler(item.HoldEuler), item.GripHeight);
                Object.DestroyImmediate(model);
                Assert.That(s.HalfWidth * 2f, Is.InRange(0.3f, 0.8f), item.ItemId + " width");
                n++;
            }
            Assert.GreaterOrEqual(n, 10, "two-handed materials found");
        }
        finally { Object.DestroyImmediate(socket.gameObject); }
    }

    // §88.9 Fit(낮춰 들고, 모자라면 작게): 머리가 앞으로 튀어나온 캐릭터(높이 0.8 m 위 앞면 0.39, 몸통 0.17).
    private static float MacaronFront(float lowY, float highY) => highY > 0.8f ? 0.39f : 0.17f;

    [Test]
    public void Fit_CookieKeepsNormalGrip()
    {
        TwoHandGrip.FitParams p = TwoHandGrip.Fit(Cookie(0.175f, 0.18f), 0.32f, 0.32f, (lo, hi) => 0.12f);
        Assert.AreEqual(0f, p.ExtraDrop, 1e-4f, "no lowering");
        Assert.AreEqual(1f, p.Scale, 1e-4f, "no shrinking");
        Assert.AreEqual(TwoHandGrip.SideMargin, p.Margin, 1e-4f);
    }

    [Test]
    public void Fit_BigHeadShortArms_LowersThenShrinks()
    {
        TwoHandGrip.Input g = Chibi(0.175f, 0.18f, 0f);
        Assert.IsFalse(TwoHandGrip.SolveWith(g, TwoHandGrip.FitParams.Normal, MacaronFront(0.33f, 0.97f)).Reachable, "default grip cannot reach");
        TwoHandGrip.FitParams p = TwoHandGrip.Fit(g, 0.32f, 0.32f, MacaronFront);
        Assert.Greater(p.ExtraDrop, 0f, "held lower, under the head");
        Assert.LessOrEqual(p.Scale, 1f);
        Assert.GreaterOrEqual(p.Scale, TwoHandGrip.MinScale);
        float y = 0.73f - 0.39f * TwoHandGrip.Drop - p.ExtraDrop;
        float front = MacaronFront(y - 0.32f * p.Scale, y + 0.32f * p.Scale);
        TwoHandGrip.Result r = TwoHandGrip.SolveWith(g, p, front);
        Assert.IsTrue(r.Reachable, "hands reach the item sides");
        Assert.GreaterOrEqual(r.ItemCenter.z - 0.18f * p.Scale, front + TwoHandGrip.BodyGap - 1e-4f, "item stays clear of the body");
        Assert.AreEqual(0.175f * p.Scale + 0.02f, r.HandR.x, 1e-4f, "hands on the scaled item edges");
    }

    // 높이를 낮추는 것만으로 닿으면 줄이지 않는다(작게 하는 것은 마지막 수단).
    [Test]
    public void Fit_PrefersLoweringOverShrinking()
    {
        TwoHandGrip.FitParams p = TwoHandGrip.Fit(Chibi(0.175f, 0.12f, 0f), 0.2f, 0.2f, MacaronFront);
        Assert.AreEqual(1f, p.Scale, 1e-4f);
        Assert.Greater(p.ExtraDrop, 0f);
    }

    // Carry 층: IK Pass + 물건 전용 상태 CarryItem(Cookie_CarryItem 클립), 쿠키 들기 상태는 그대로. 상태 고르기 규칙.
    [Test]
    public void Controller_HasCarryItemState()
    {
        var c = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Animation/PlayerAnimator.controller");
        bool found = false;
        foreach (var l in c.layers)
        {
            if (l.name != ActionLayerBuilder.CarryLayer) continue;
            found = true;
            Assert.IsTrue(l.iKPass, "Carry layer IK pass");
            Motion item = null, cookie = null;
            foreach (var s in l.stateMachine.states)
            {
                if (s.state.name == PlayerAnimationDriver.CarryItemState) item = s.state.motion;
                if (s.state.name == PlayerAnimationDriver.CarryCookieState) cookie = s.state.motion;
            }
            Assert.IsNotNull(item, "CarryItem state with a clip");
            Assert.AreEqual("Cookie_CarryItem", item.name);
            Assert.IsNotNull(cookie, "cookie carry state kept");
            Assert.IsTrue(((AnimationClip)item).humanMotion && ((AnimationClip)item).isLooping);
        }
        Assert.IsTrue(found);
        Assert.AreEqual(PlayerAnimationDriver.CarryItemState, PlayerAnimationDriver.CarryStateFor(CarryReason.TwoHandedItem));
        Assert.AreEqual(PlayerAnimationDriver.CarryCookieState, PlayerAnimationDriver.CarryStateFor(CarryReason.Cookie));
        Assert.AreEqual(PlayerAnimationDriver.CarryCookieState, PlayerAnimationDriver.CarryStateFor(CarryReason.Cookie | CarryReason.TwoHandedItem), "carrying a cookie wins");
    }
}
