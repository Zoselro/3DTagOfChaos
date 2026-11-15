using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Request1009Plan.md §81 — 스킨 교체 때 동작 유지(AnimatorSnapshot)·거대 괴물이 큰 건물·계단 구역 밖에서만 나옴(GiantSpawnRules).
public class Request1118Tests
{
    // S2: 들고 달리며 망치를 든 채(바닥 Run + Carry 층 1 + Action HammerHold) 모델을 바꿔도(쿠키 → 모델 → 다른 모델 → 쿠키) 그대로.
    [Test]
    public void SkinSwap_KeepsRunCarryAndActionPose()
    {
        var prefab = Resources.Load<GameObject>("HideOrSeekPlayer");
        GameObject go = Object.Instantiate(prefab);
        try
        {
            Animator a = go.GetComponent<Animator>();
            a.Rebind();
            a.Update(0f);
            int carry = a.GetLayerIndex("Carry"), action = a.GetLayerIndex("Action");
            Assert.GreaterOrEqual(carry, 0);
            Assert.GreaterOrEqual(action, 0);
            a.Play("Run", 0, 0.3f);
            a.SetLayerWeight(carry, 1f);
            a.Play("Carrying", carry, 0f);
            a.SetLayerWeight(action, 1f);
            a.Play("HammerHold", action, 0f);
            a.SetInteger("DanceId", 3);
            a.Update(0.05f);
            Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("Run"), "set up: running");

            SkinCatalogSO catalog = GameSettings.Current.CharacterCatalog;
            foreach (GameObject model in new[] { catalog.GetModel(3), catalog.GetModel(1), null })
            {
                string label = model != null ? model.name : "cookie";
                Assert.IsTrue(CharacterModelSwap.Apply(go.transform, model), label + ": swapped");
                a.Update(0.05f);
                Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("Run"), label + ": still running (not Idle)");
                Assert.AreEqual(1f, a.GetLayerWeight(carry), 1e-4f, label + ": carry pose kept");
                Assert.IsTrue(a.GetCurrentAnimatorStateInfo(carry).IsName("Carrying"), label + ": carry state");
                Assert.AreEqual(1f, a.GetLayerWeight(action), 1e-4f, label + ": action layer kept");
                Assert.IsTrue(a.GetCurrentAnimatorStateInfo(action).IsName("HammerHold"), label + ": hammer pose");
                Assert.AreEqual(3, a.GetInteger("DanceId"), label + ": int parameter kept");
            }
        }
        finally { Object.DestroyImmediate(go); }
    }

    // S2: 한 번도 돌지 않은 Animator(막 생긴 캐릭터)는 기억할 것이 없다 — 지금처럼 기본 상태(Idle)부터.
    [Test]
    public void Snapshot_NullForUninitializedAnimator()
    {
        var go = new GameObject("Snap", typeof(Animator));
        try { Assert.IsNull(AnimatorSnapshot.Take(go.GetComponent<Animator>())); }
        finally { Object.DestroyImmediate(go); }
    }

    private static DestructibleUnit Unit(DestructibleGrade grade, Vector3 center, Vector3 size, int level = 0)
    {
        var b = new Bounds(center, size);
        return new DestructibleUnit { Grade = grade, Bounds = b, Zone = b, Level = level };
    }

    // G2: 큰 건물·계단 구역과 몸(반지름 + 여유)이 겹치면 안 됨. 텐트·소품·무너진 건물·건물 위 높이는 막지 않는다(Q3).
    [Test]
    public void GiantSpawn_AvoidsBuildingsAndStairsOnly()
    {
        var units = new List<DestructibleUnit>
        {
            Unit(DestructibleGrade.Building, new Vector3(0f, 5f, 0f), new Vector3(20f, 10f, 20f)),
            Unit(DestructibleGrade.Tent, new Vector3(60f, 3f, 0f), new Vector3(10f, 6f, 10f)),
            Unit(DestructibleGrade.Prop, new Vector3(0f, 1f, 60f), new Vector3(4f, 2f, 4f)),
            Unit(DestructibleGrade.Stairs, new Vector3(-60f, -15f, 0f), new Vector3(16f, 40f, 16f)),
            Unit(DestructibleGrade.Building, new Vector3(0f, 5f, -60f), new Vector3(20f, 10f, 20f), (int)DestructibleGrade.Building), // 무너짐
        };
        float c = GiantSpawnRules.BodyClearance;
        Assert.IsFalse(GiantSpawnRules.ClearOfBuildings(new Vector3(0f, 0f, 0f), units), "inside a building");
        Assert.IsFalse(GiantSpawnRules.ClearOfBuildings(new Vector3(10f + c - 0.5f, 0f, 0f), units), "body would overlap the wall");
        Assert.IsTrue(GiantSpawnRules.ClearOfBuildings(new Vector3(10f + c + 0.5f, 0f, 0f), units), "clear of the wall");
        Assert.IsTrue(GiantSpawnRules.ClearOfBuildings(new Vector3(60f, 0f, 0f), units), "tents are allowed (Q3)");
        Assert.IsTrue(GiantSpawnRules.ClearOfBuildings(new Vector3(0f, 0f, 60f), units), "props are allowed");
        Assert.IsFalse(GiantSpawnRules.ClearOfBuildings(new Vector3(-60f, 0f, 0f), units), "stairs zone (clock tower spiral)");
        Assert.IsTrue(GiantSpawnRules.ClearOfBuildings(new Vector3(0f, 0f, -60f), units), "broken building no longer blocks");
        Assert.IsTrue(GiantSpawnRules.ClearOfBuildings(new Vector3(0f, 12f, 0f), units), "above the building's height range");
        Assert.IsTrue(GiantSpawnRules.ClearOfBuildings(new Vector3(0f, 0f, 0f), null), "no catalog: nothing blocks");
    }

    // R: 술래방 무너짐 — 남기는 것은 바닥(위 끝이 방 바닥 + FloorTop 아래)과 엘리베이터뿐, 벽·지붕·문은 무너진다.
    [Test]
    public void MonsterRoomBreak_KeepsFloorAndElevatorOnly()
    {
        float floor = 2f;
        Assert.IsTrue(MonsterRoomBreak.Keep(new Bounds(new Vector3(0f, floor - 0.1f, 0f), new Vector3(26f, 0.4f, 26f)), floor, false), "floor slab stays");
        Assert.IsFalse(MonsterRoomBreak.Keep(new Bounds(new Vector3(12f, floor + 4f, 0f), new Vector3(1f, 8f, 26f)), floor, false), "wall falls");
        Assert.IsFalse(MonsterRoomBreak.Keep(new Bounds(new Vector3(0f, floor + 9f, 0f), new Vector3(26f, 1f, 26f)), floor, false), "roof falls");
        Assert.IsTrue(MonsterRoomBreak.Keep(new Bounds(new Vector3(0f, floor + 3f, 0f), new Vector3(4f, 6f, 4f)), floor, true), "elevator stays");
    }

    // R: 술래방 전체 = 목록 밖 건물 하나(Id −1), 처음부터 무너진 단계로 보여 준다(빈 방이면 숨길 것 없음).
    [Test]
    public void MonsterRoomBreak_BuildsOneBrokenUnit()
    {
        var go = new GameObject("MonsterRoom");
        try
        {
            go.transform.position = new Vector3(10f, 0f, -20f);
            DestructibleUnit u = MonsterRoomBreak.BuildUnit(go.transform);
            Assert.AreEqual(-1, u.Id, "outside the catalog");
            Assert.IsTrue(u.Broken, "presented as broken at once");
            Assert.AreEqual(0, u.Renderers.Length);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
