using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// 진저브레드 마을 실제 맵(Request1009Plan.md §51): 탈출 배치·두 층 구성·시계탑 → 지하 연결.
public class GingerbreadVillageMapTests
{
    [OneTimeSetUp]
    public void Open()
    {
        Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(GingerbreadVillageMapBuilder.ScenePath) != null, "build the map scene first");
        EditorSceneManager.OpenScene(GingerbreadVillageMapBuilder.ScenePath, OpenSceneMode.Single);
    }

    [Test]
    public void EscapeSetup_Complete()
    {
        var manager = Object.FindFirstObjectByType<EscapeManager>();
        Assert.NotNull(manager);
        var so = new SerializedObject(manager);
        Assert.AreEqual(4, so.FindProperty("buildings").arraySize);
        Assert.AreEqual(4, so.FindProperty("exits").arraySize);
        Assert.AreEqual(EscapeMapSetup.ChestSlotCount, so.FindProperty("chestAnchors").arraySize);
        Assert.NotNull(so.FindProperty("monsterRoom").objectReferenceValue);
        for (int q = 1; q <= 4; q++)
        {
            Transform b = GameObject.Find($"Building_Q{q}").transform;
            Assert.NotNull(b.Find("GIN_RuneBuilding"), b.name);
            Assert.Greater(b.position.y, -1f, b.name + " on the ground level");
            Assert.NotNull(GameObject.Find("Spawn_Cookie_Q" + q));
        }
    }

    // 출구 4는 지하 12·3·6·9시, 지하에는 술래방이 없다
    [Test]
    public void Exits_AreUnderground()
    {
        foreach ((string n, Vector3 d) in new[] { ("N", Vector3.forward), ("E", Vector3.right), ("S", Vector3.back), ("W", Vector3.left) })
        {
            Transform e = GameObject.Find("Exit_" + n).transform;
            Assert.AreEqual(GingerbreadVillageMapBuilder.UgY, e.position.y, 0.5f, n);
            Assert.AreEqual(GingerbreadVillageMapBuilder.UgRadius, Vector3.Dot(e.position, d), 0.5f, n);
        }
        Assert.AreEqual(1, Object.FindObjectsByType<MonsterRoomGuard>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
    }

    [Test]
    public void BuildingCounts()
    {
        string[] sources = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).Select(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name ?? "").ToArray();
        Assert.AreEqual(8, sources.Count(s => s.StartsWith("GIN_BLD_")), "ground buildings");
        string[] ug = { "GIN_UG_CandyCaneCottage", "GIN_UG_CrookedChocoCottage", "GIN_UG_GearworksGate", "GIN_UG_GloomgumFortress", "GIN_UG_MacaronCottage" };
        Assert.AreEqual(12, sources.Count(s => ug.Contains(s)), "underground buildings");
        Assert.AreEqual(2, sources.Count(s => s == "GIN_ClockTower"), "clock towers at 11 and 5 o'clock");
        // 숨는 건물(§54): 사분면마다 지상 3 · 지하 2
        Transform[] hides = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && (PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name ?? "").StartsWith("GIN_HD_")).ToArray();
        Assert.AreEqual(12, hides.Count(t => t.position.y > -1f), "ground hide buildings (3 per quadrant)");
        Assert.AreEqual(8, hides.Count(t => Mathf.Abs(t.position.y - GingerbreadVillageMapBuilder.UgY) < 0.5f), "underground hide buildings (2 per quadrant)");
        foreach (Transform h in hides.Where(t => t.position.y < -1f))
            Assert.Less(new Vector2(h.position.x, h.position.z).magnitude + 14f, GingerbreadVillageMapBuilder.UgRadius, h.name + " inside the cave");
    }

    // 쿠키 스폰(지상)에서 시계탑 계단·나선 내리막으로 지하 출구 4곳까지 NavMesh 길이 있다
    [Test]
    public void NavMesh_GroundToUndergroundExits()
    {
        var holder = Object.FindFirstObjectByType<DecoyNavMesh>();
        Assert.NotNull(holder);
        var data = new SerializedObject(holder).FindProperty("data").objectReferenceValue as NavMeshData;
        Assert.NotNull(data, "baked");
        NavMesh.RemoveAllNavMeshData();
        NavMeshDataInstance live = NavMesh.AddNavMeshData(data);
        try
        {
            Assert.IsTrue(NavMesh.SamplePosition(GameObject.Find("Spawn_Cookie_Q1").transform.position, out NavMeshHit from, 3f, NavMesh.AllAreas));
            foreach (string n in new[] { "N", "E", "S", "W" })
            {
                Vector3 e = GameObject.Find("Exit_" + n).transform.position;
                Vector3 before = e - new Vector3(e.x, 0f, e.z).normalized * 3f;
                Assert.IsTrue(NavMesh.SamplePosition(before, out NavMeshHit to, 3f, NavMesh.AllAreas), n);
                var path = new NavMeshPath();
                // 지상 → 시계탑 나선 → 지하 끝은 길이 길어 한 번 탐색 한도를 넘는다 — 게임과 같은 판정(NavPath)으로 본다
                Assert.IsTrue(NavPath.Reaches(from.position, to.position, path), n);
            }
        }
        finally { live.Remove(); }
    }
}
