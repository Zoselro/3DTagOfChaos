using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// 캔디숲 실제 모델 맵(Request1009Plan.md §28) 시험. 통행 검사(몸 5가지)는 오래 걸려 메뉴
// Tools/TagOfChaos/CandyForest/3 Check Passability로 따로 돌린다(결과 §28.13).
public class Request1019Tests
{
    // §28.6: 둥근 맵 — 맵 밖 판정·가장자리 방향(헬리콥터 모자)·안쪽 무작위 지점
    [Test]
    public void MapArea_Circle_OutsideEdgeAndRandom()
    {
        var area = new MapArea(Vector2.zero, 80f);
        Assert.IsFalse(area.IsOutside(new Vector3(56f, 0f, 56f), 0f), "r 79.2 is inside");
        Assert.IsTrue(area.IsOutside(new Vector3(60f, 0f, 60f), 0f), "r 84.9 is outside");
        Assert.IsFalse(area.IsOutside(new Vector3(60f, 0f, 60f), 10f), "inside with margin");
        Vector3 dir = area.EdgeDirection(new Vector3(-30f, 5f, 40f));
        Assert.AreEqual(new Vector3(-0.6f, 0f, 0.8f).x, dir.x, 1e-4f);
        Assert.AreEqual(0.8f, dir.z, 1e-4f);
        Assert.AreEqual(Vector3.forward, area.EdgeDirection(Vector3.zero));
        var rng = new System.Random(3);
        for (int i = 0; i < 200; i++) Assert.LessOrEqual(area.RandomPoint(rng, 4f).magnitude, 76f + 1e-3f);
    }

    [Test]
    public void MapArea_Rect_MatchesOldRules()
    {
        var area = new MapArea(new Rect(-70f, -70f, 140f, 140f));
        Assert.IsTrue(area.IsOutside(new Vector3(71f, 0f, 0f), 0f));
        Assert.IsFalse(area.IsOutside(new Vector3(69f, 0f, 69f), 0f));
        Assert.AreEqual(HatFlight.NearestEdgeDirection(area.Rect, new Vector3(60f, 0f, 10f)), area.EdgeDirection(new Vector3(60f, 0f, 10f)));
        Assert.AreEqual(Vector3.right, area.EdgeDirection(new Vector3(60f, 0f, 10f)));
    }

    // §28.11 Q11: 새 씬 CandyForest가 맵 씬으로 인식되고 빌드·판 맵 목록에 들어 있다
    [Test]
    public void CandyForestScene_IsRegisteredMapScene()
    {
        Assert.IsTrue(SceneNames.IsMapScene("CandyForest"));
        Assert.AreEqual("CandyForest", SceneNames.MapOf("CandyForest"));
        Assert.AreEqual(MusicScene.CandyForest, MusicRules.SceneFromName("CandyForest"));
        Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(CandyForestMapBuilder.ScenePath));
        Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.path == CandyForestMapBuilder.ScenePath && s.enabled), "in build");
        CollectionAssert.Contains(GameSettings.Current.GameMapScenes.ToList(), "CandyForest");
    }

    // §28.11.3~7: 재료 건물 4 · 가장자리 괴물집 5 · 천막 5 · 지팡이 사탕 10, 건물 앞문이 맵 가운데를 본다
    [Test]
    public void Layout_HasPlannedGroups()
    {
        var layout = AssetDatabase.LoadAssetAtPath<CandyForestLayoutSO>(CandyForestMapBuilder.LayoutPath);
        Assert.NotNull(layout);
        Assert.AreEqual(4, layout.placements.Count(p => p.asset.StartsWith("CF_Bld_")));
        Assert.AreEqual(5, layout.placements.Count(p => p.asset == "CF_MonsterHouse"));
        Assert.AreEqual(5, layout.placements.Count(p => p.asset == "CF_Tent"));
        Assert.AreEqual(10, layout.placements.Count(p => p.asset == "CF_CandyCane" && p.group == "Edge"));
        foreach (var p in layout.placements.Where(p => p.asset.StartsWith("CF_Bld_") || p.group == "Edge"))
        {
            Vector3 forward = Quaternion.Euler(0f, p.yaw, 0f) * Vector3.forward;
            Assert.Less(Vector3.Dot(forward, p.position.normalized), -0.99f, p.asset + " front faces the center");
            Assert.LessOrEqual(new Vector2(p.position.x, p.position.z).magnitude, CandyForestLayout.MapRadius - 4f, p.asset + " inside the fence");
        }
    }

    // §28.5: 캔디숲 재료 = 막대사탕 4색(빨강·파랑·초록·노랑), 주황은 없다
    [Test]
    public void CandyForestMaterials_AreFourLollipops()
    {
        EscapeRecipeSO recipe = EscapeCatalogSO.Current.RecipeFor("CandyForest");
        string[] ids = recipe.BuildingMaterials.Select(i => i.ItemId).OrderBy(x => x).ToArray();
        CollectionAssert.AreEqual(new[] { "CandyCell_Blue", "CandyCell_Green", "CandyCell_Red", "CandyCell_Yellow" }, ids);
        foreach (ItemSO item in recipe.BuildingMaterials)
        {
            Assert.NotNull(item.ModelPrefab, item.ItemId);
            StringAssert.Contains("ITEM_CandyCell_", item.ModelPrefab.name);
            Assert.NotNull(item.Icon, item.ItemId);
        }
    }

    // §28.4·§28.11.7: 재료 건물 수리 자리는 쿠키 손 높이(상호작용 높이 차 2.5 m 안), 괴물집·천막 뒤 구멍은 1.4 m(쿠키만)
    [Test]
    public void Prefabs_RepairPointReachable_AndCookieHole()
    {
        for (int q = 1; q <= 4; q++)
        {
            GameObject b = CandyForestAssets.LoadPrefab("CF_Bld_Q" + q);
            Assert.NotNull(b);
            Transform rp = b.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Repair_Point"));
            Assert.Less(rp.position.y, 2.5f, "repair point height");
            Assert.NotNull(b.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Socket_")));
            Assert.NotNull(b.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("ESC_Glow")));
        }
        foreach (string shell in new[] { "CF_MonsterHouse", "CF_Tent" })
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(CandyForestAssets.LoadPrefab(shell));
            try
            {
                Collider holeBlock = go.GetComponentInChildren<CookieHole>().GetComponent<Collider>();
                Physics.SyncTransforms();
                float r = go.GetComponentsInChildren<BoxCollider>().Where(c => c.name.StartsWith("COL_Wall_")).Min(c => new Vector2(c.transform.position.x, c.transform.position.z).magnitude);
                Assert.IsFalse(Physics.CheckSphere(new Vector3(0f, 1.2f, r), 2f), shell + " front door is monster wide");
                Assert.IsTrue(Physics.CheckSphere(new Vector3(0f, 1.2f, -r), 0.3f), shell + " back hole blocks the monster");
                holeBlock.enabled = false; // 쿠키에게는 열린 구멍
                Physics.SyncTransforms();
                Assert.IsFalse(Physics.CheckSphere(new Vector3(0f, 1.2f, -r), 0.6f), shell + " back hole fits a 0.6 m body");
                Assert.Less(go.GetComponentsInChildren<BoxCollider>().Where(c => c.name.StartsWith("COL_Wall_")).Max(c => c.size.z), 1f, "hole depth under 1 m");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
