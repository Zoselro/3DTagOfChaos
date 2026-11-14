using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Request1009Plan.md §76(AI도 도구에 맞음·우클릭 1인칭 조준)·§77(FPS 1인칭 팔·반동·던지기 준비).
public class Request1116Tests
{
    private static ItemSO FindItem(string id)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ItemSO"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && item.ItemId == id) return item;
        }
        return null;
    }

    // A1: AI 대상 번호는 음수(-AI 번호) — PhotonView 번호(양수)와 겹치지 않는다.
    [Test]
    public void DecoyHit_EncodesNegativeTargets()
    {
        Assert.AreEqual(-7, DecoyHit.ToTarget(7));
        Assert.IsTrue(DecoyHit.IsDecoy(-7));
        Assert.IsFalse(DecoyHit.IsDecoy(1001));
        Assert.AreEqual(7, DecoyHit.DecoyId(DecoyHit.ToTarget(7)));
    }

    // 요청 1: AI가 맞는 도구는 스턴건·물풍선·섬광탄뿐.
    [Test]
    public void DecoyHit_OnlyStunBalloonFlash()
    {
        Assert.IsTrue(DecoyHit.CanHit(ToolKind.StunGun));
        Assert.IsTrue(DecoyHit.CanHit(ToolKind.WaterBalloon));
        Assert.IsTrue(DecoyHit.CanHit(ToolKind.Flashbang));
        Assert.IsFalse(DecoyHit.CanHit(ToolKind.Hammer));
        Assert.IsFalse(DecoyHit.CanHit(ToolKind.BoxingGlove));
        Assert.IsFalse(DecoyHit.CanHit(ToolKind.SearchBomb));
    }

    // A6: AI가 서 있는 시간 = 쿠키 기절 시간, 물풍선은 갇힘 + 떨어짐.
    [Test]
    public void DecoyHit_HoldSecondsMatchCookie()
    {
        ItemSO stun = FindItem("StunGun"), balloon = FindItem("WaterBalloon"), flash = FindItem("Flashbang");
        Assert.IsNotNull(stun); Assert.IsNotNull(balloon); Assert.IsNotNull(flash);
        Assert.AreEqual(stun.Tool.StunSecondsCookie, DecoyHit.HoldSeconds(stun.Tool), 1e-4f);
        Assert.AreEqual(flash.Tool.StunSecondsCookie, DecoyHit.HoldSeconds(flash.Tool), 1e-4f);
        Assert.AreEqual(balloon.Tool.StunSecondsCookie + StunReceiver.FallStunSeconds, DecoyHit.HoldSeconds(balloon.Tool), 1e-4f);
        Assert.AreEqual(0f, DecoyHit.HoldSeconds(null));
    }

    // 요청 3: 물풍선은 한 번 얻으면 3개.
    [Test]
    public void WaterBalloon_HasThreeCharges()
    {
        ItemSO balloon = FindItem("WaterBalloon");
        Assert.IsNotNull(balloon);
        Assert.AreEqual(3, balloon.Tool.Charges);
    }

    // Q1: 기본 = 우클릭 한 번에 켜기/끄기, 설정 = 누르고 있는 동안.
    [Test]
    public void AimMode_ToggleAndHold()
    {
        Assert.IsTrue(AimModeSetting.Next(false, false, true, true), "toggle: press turns on");
        Assert.IsFalse(AimModeSetting.Next(true, false, true, true), "toggle: press again turns off");
        Assert.IsTrue(AimModeSetting.Next(true, false, true, false), "toggle: holding without a new press keeps it");
        Assert.IsFalse(AimModeSetting.Next(false, false, false, false));
        Assert.IsTrue(AimModeSetting.Next(false, true, true, true), "hold: on while held");
        Assert.IsTrue(AimModeSetting.Next(true, true, true, false));
        Assert.IsFalse(AimModeSetting.Next(true, true, false, false), "hold: off on release");
    }

    // 요청 2: 우클릭 1인칭 조준은 스턴건·물풍선·섬광탄.
    [Test]
    public void AimTools_AreStunBalloonFlash()
    {
        Assert.IsTrue(ToolUser.IsAimTool(ToolKind.StunGun));
        Assert.IsTrue(ToolUser.IsAimTool(ToolKind.WaterBalloon));
        Assert.IsTrue(ToolUser.IsAimTool(ToolKind.Flashbang));
        Assert.IsFalse(ToolUser.IsAimTool(ToolKind.Hammer));
        Assert.AreEqual(0.85f, ToolUser.AimZoom, 1e-4f);
        Assert.AreEqual(0.5f, CookieAimCamera.BlendSeconds, 1e-4f);
    }

    // §77 TP2: 반동은 0.18초 — 빨리 올라 천천히 돌아오고 끝나면 0.
    [Test]
    public void Kick_RisesFastAndSettles()
    {
        Assert.AreEqual(0f, PlayerActionPose.KickWeight(-0.1f));
        Assert.AreEqual(1f, PlayerActionPose.KickWeight(0.25f), 1e-4f);
        Assert.Greater(PlayerActionPose.KickWeight(0.1f), 0f);
        Assert.Greater(PlayerActionPose.KickWeight(0.5f), PlayerActionPose.KickWeight(0.9f));
        Assert.AreEqual(0f, PlayerActionPose.KickWeight(1f));
    }

    // §77 TP1: 던지기 준비 자세는 팔을 젖힌 순간 — 손을 떠나는 순간보다 앞.
    [Test]
    public void ThrowReady_IsBeforeRelease()
    {
        float release = PlayerActionPose.ThrowRelease / PlayerActionPose.ThrowSeconds;
        Assert.Greater(PlayerActionPose.ThrowReadyAt, 0f);
        Assert.Less(PlayerActionPose.ThrowReadyAt, release);
    }

    // §77 FP1: 1인칭 팔 = 위팔 두 개와 그 아래 뼈만(가슴·머리는 아니다).
    [Test]
    public void FirstPersonArms_KeepsUpperArmsAndBelow()
    {
        var root = new GameObject("Root").transform;
        try
        {
            Transform chest = new GameObject("Chest").transform; chest.SetParent(root);
            Transform head = new GameObject("Head").transform; head.SetParent(chest);
            Transform lu = new GameObject("LUpper").transform; lu.SetParent(chest);
            Transform ll = new GameObject("LLower").transform; ll.SetParent(lu);
            Transform ru = new GameObject("RUpper").transform; ru.SetParent(chest);
            Transform rh = new GameObject("RHand").transform; rh.SetParent(ru);
            Transform finger = new GameObject("RFinger").transform; finger.SetParent(rh);
            bool[] keep = CookieFirstPersonArms.ArmBones(new[] { chest, head, lu, ll, ru, rh, finger, null }, lu, ru);
            CollectionAssert.AreEqual(new[] { false, false, true, true, true, true, true, false }, keep);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }

    // F4: 설정 창 조작키 탭에 조준 방식 줄이 있고, 조작키 목록과 겹치지 않는다.
    [Test]
    public void SettingsPrefab_HasAimModeRowClearOfKeyList()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SettingsMenuInstaller.PrefabPath);
        try
        {
            KeyBindingsPage page = root.GetComponentInChildren<KeyBindingsPage>(true);
            Assert.IsNotNull(page);
            var row = page.transform.Find("AimModeRow") as RectTransform;
            var scroll = page.transform.Find("Scroll") as RectTransform;
            Assert.IsNotNull(row, "AimModeRow");
            Assert.IsNotNull(row.GetComponent<AimModeRow>());
            Assert.IsNotNull(scroll);
            float rowTop = row.anchoredPosition.y + row.sizeDelta.y;
            Assert.GreaterOrEqual(scroll.offsetMin.y, rowTop, "key list bottom must sit above the aim row");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
