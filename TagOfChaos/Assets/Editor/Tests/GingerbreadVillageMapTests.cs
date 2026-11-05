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
            Assert.NotNull(b.GetComponent<QuadrantBuilding>(), b.name);   // 층은 RuneBuildings_GroundQ2Q3_UndergroundQ1Q4가 본다(§58)
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
        Assert.AreEqual(6, sources.Count(s => s.StartsWith("GIN_BLD_")), "ground buildings (§58)");
        Assert.AreEqual(5, sources.Where(s => s.StartsWith("GIN_BLD_")).Distinct().Count(), "all five ground building kinds");
        string[] ug = { "GIN_UG_CandyCaneCottage", "GIN_UG_CrookedChocoCottage", "GIN_UG_GearworksGate", "GIN_UG_GloomgumFortress", "GIN_UG_MacaronCottage" };
        Assert.AreEqual(5, sources.Count(s => ug.Contains(s)), "underground buildings — one of each kind (§58)");
        Assert.AreEqual(5, sources.Where(s => ug.Contains(s)).Distinct().Count(), "all five underground building kinds");
        Assert.AreEqual(2, sources.Count(s => s == "GIN_ClockTower"), "two clock towers");
        // 숨는 건물(§58): 지상 4 · 지하 1
        Transform[] hides = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && (PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name ?? "").StartsWith("GIN_HD_")).ToArray();
        Assert.AreEqual(4, hides.Count(t => t.position.y > -1f), "ground hide buildings");
        Assert.AreEqual(1, hides.Count(t => Mathf.Abs(t.position.y - GingerbreadVillageMapBuilder.UgY) < 0.5f), "underground hide buildings");
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
    // 건물 문은 누른 사람 반대쪽으로 연다(§56): 바깥 열림 파라미터가 있고, 건물 안쪽 점은 "안"으로 판정된다(돌려 놓은 건물 포함).
    [Test]
    public void BuildingDoors_OpenAwayFromPresser()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var outwardField = typeof(InteractableDoor).GetField("outwardParameter", flags);
        var compute = typeof(InteractableDoor).GetMethod("ComputeGeometry", flags);
        var isInside = typeof(InteractableDoor).GetMethod("IsInside", flags);
        int checkedDoors = 0;
        foreach (var door in Object.FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None))
        {
            Transform root = door.transform.parent;
            while (root != null && root.parent != null && root.parent.name != "Buildings" && root.parent.name != "Landmarks" && root.parent.name != "HideBuildings") root = root.parent;
            if (root == null || root.parent == null || root.name.StartsWith("GIN_ClockTower")) continue;
            string label = root.name + "/" + door.name;
            Assert.AreEqual(InteractableDoor.DefaultOutwardParameter, (string)outwardField.GetValue(door), label);
            var controller = door.GetComponent<Animator>().runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            Assert.IsTrue(controller != null && controller.parameters.Any(p => p.name == InteractableDoor.DefaultOutwardParameter && p.type == AnimatorControllerParameterType.Bool), label);
            compute.Invoke(door, null);
            Vector3 c = door.transform.TransformPoint(new Vector3(3.3f, 0f, 0f));
            Vector3 toRoot = root.position - c; toRoot.y = 0f;
            Vector3 axis = Vector3.Dot(door.transform.forward, toRoot) >= 0f ? door.transform.forward : -door.transform.forward;
            Assert.IsTrue((bool)isInside.Invoke(door, new object[] { c + axis * 3f }), label + " inside point");
            Assert.IsFalse((bool)isInside.Invoke(door, new object[] { c - axis * 3f }), label + " outside point");
            checkedDoors++;
        }
        Assert.AreEqual(40, checkedDoors);   // 건물 20채 × 앞뒤(§58)
    }
    // 술래방 판정은 높이도 본다(§57): 술래방 바로 밑 지하는 방 밖, 지상 방 안은 방 안.
    [Test]
    public void MonsterRoom_UndergroundBelowIsOutside()
    {
        var guard = Object.FindFirstObjectByType<MonsterRoomGuard>(FindObjectsInactive.Include);
        var manager = Object.FindFirstObjectByType<EscapeManager>(FindObjectsInactive.Include);
        Vector3 room = guard.transform.position;
        Vector3 below = new Vector3(room.x, room.y - 36f, room.z - 4f);
        Vector3 inside = new Vector3(room.x, room.y + 0.1f, room.z - 4f);
        Assert.IsFalse(guard.IsInside(below), "underground below the room");
        Assert.IsTrue(guard.IsInside(inside), "room floor");
        Assert.IsFalse(manager.InMonsterRoom(below), "underground below the room (EscapeManager)");
        Assert.IsTrue(manager.InMonsterRoom(inside), "room floor (EscapeManager)");
    }
    // 룬(재료) 건물(§58): 지상 Q2·Q3, 지하 UQ1·UQ4 — 사분면마다 지상 3채·지하 2채
    [Test]
    public void RuneBuildings_GroundQ2Q3_UndergroundQ1Q4()
    {
        foreach ((int q, bool under) in new[] { (1, true), (2, false), (3, false), (4, true) })
        {
            Transform b = GameObject.Find("Building_Q" + q).transform;
            Assert.AreEqual(under ? GingerbreadVillageMapBuilder.UgY : 0f, b.position.y, 0.5f, b.name);
            float deg = Mathf.Repeat(Mathf.Atan2(b.position.z, b.position.x) * Mathf.Rad2Deg, 360f);
            Assert.AreEqual(q, deg < 90f ? 2 : deg < 180f ? 1 : deg < 270f ? 3 : 4, b.name + " quadrant");
        }
        var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && (PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name ?? "").StartsWith("GIN_")
                        && !(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name ?? "").StartsWith("GIN_ClockTower")
                        && t.GetComponentInChildren<InteractableDoor>(true) != null).ToArray();
        foreach (bool under in new[] { false, true })
            for (int q = 1; q <= 4; q++)
            {
                int n = roots.Count(t => (t.position.y < -1f) == under && Quadrant(t.position) == q);
                Assert.AreEqual(under ? 2 : 3, n, (under ? "UQ" : "Q") + q);
            }
    }

    private static int Quadrant(Vector3 p)
    {
        float deg = Mathf.Repeat(Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg, 360f);
        return deg < 90f ? 2 : deg < 180f ? 1 : deg < 270f ? 3 : 4;
    }

    // 괴물 엘리베이터(§58): 술래방 가운데(지상) ↔ 지하 동굴 가운데, 서로를 가리킨다. 지하 발판에서 출구까지 길이 있다.
    [Test]
    public void MonsterElevator_LinksRoomAndCave()
    {
        var pads = Object.FindObjectsByType<MonsterElevator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.AreEqual(2, pads.Length);
        MonsterElevator top = pads.First(p => p.transform.position.y > -1f), bottom = pads.First(p => p.transform.position.y < -1f);
        Assert.AreSame(bottom, top.Destination); Assert.AreSame(top, bottom.Destination);
        Assert.Less(new Vector2(top.transform.position.x, top.transform.position.z).magnitude, 0.5f);
        Assert.AreEqual(GingerbreadVillageMapBuilder.UgY, bottom.transform.position.y, 0.5f);
        Assert.IsTrue(Object.FindFirstObjectByType<MonsterRoomGuard>().IsInside(top.transform.position + Vector3.up * 0.1f), "top pad inside the monster room");
        Assert.IsFalse(Physics.CheckCapsule(bottom.ArrivalPoint + Vector3.up * 1.2f, bottom.ArrivalPoint + Vector3.up * 3f, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), "bottom arrival clear");
        Assert.IsFalse(Physics.CheckCapsule(top.ArrivalPoint + Vector3.up * 1.2f, top.ArrivalPoint + Vector3.up * 3f, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), "top arrival clear");
    }
}
