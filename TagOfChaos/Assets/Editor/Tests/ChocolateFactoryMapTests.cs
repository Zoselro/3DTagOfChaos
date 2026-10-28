using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 초콜릿 공장 실제 맵(Request1009Plan.md §42): 건물 재료 고정, 바닥 무늬, 숨는 창고 프리팹, 맵 씬 배치·빛, 통행 검사 보고.
public class ChocolateFactoryMapTests
{
    [Test]
    public void Recipe_ChocolateBuildingsHaveFixedMaterials()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<EscapeRecipeSO>(ChocolateFactoryMapBuilder.RecipePath);
        Assert.NotNull(recipe);
        Assert.IsTrue(recipe.FixedBuildingOrder, "signs say the material — no shuffle");
        CollectionAssert.AreEqual(ChocolateFactoryAssets.MaterialBuildings, recipe.BuildingMaterials.Select(m => m.ItemId).ToArray());
    }

    [Test]
    public void Floor_AccentsStayInsideTheirFourMetreBlock()
    {
        // 무늬마다 4 m 칸 안 타일 수(어느 방향으로 돌려도 같다)
        (ChocolateFactoryMapBuilder.Accent a, int tile, int count)[] expect =
        {
            (ChocolateFactoryMapBuilder.Accent.Jelly, 2, 4), (ChocolateFactoryMapBuilder.Accent.Choco, 1, 4),
            (ChocolateFactoryMapBuilder.Accent.Grinder, 3, 4), (ChocolateFactoryMapBuilder.Accent.Grid, 4, 2), (ChocolateFactoryMapBuilder.Accent.Plain, 0, 16),
        };
        foreach ((ChocolateFactoryMapBuilder.Accent a, int tile, int count) in expect)
            for (int side = 0; side < 4; side++)
            {
                int n = 0;
                for (int u = 0; u < 4; u++)
                    for (int v = 0; v < 4; v++)
                        if (ChocolateFactoryMapBuilder.AccentTile(a, side, u, v) == tile) n++;
                Assert.AreEqual(count, n, $"{a} side {side}");
            }
    }

    [Test]
    public void Prefabs_ShedIsAHidingPlaceWithTwoOpenDoors()
    {
        GameObject shed = ChocolateFactoryAssets.LoadProp(ChocolateFactoryAssets.ShedName);
        Assert.NotNull(shed);
        Assert.NotNull(shed.GetComponent<IndoorZone>(), "indoor sound zone");
        Assert.GreaterOrEqual(shed.GetComponentsInChildren<Light>(true).Length, 3, "inside + two exit lights");
        BoxCollider[] walls = shed.GetComponentsInChildren<BoxCollider>(true);
        Assert.Greater(walls.Length, 8, "wall boxes");
        Assert.AreEqual(0, shed.GetComponentsInChildren<MeshCollider>(true).Length, "only the COL_ boxes block");
        // 앞뒤 문 자리(로컬 ±y = Unity ±z 축 4 m, 문 폭 2.8 m)는 비어 있다
        var go = (GameObject)PrefabUtility.InstantiatePrefab(shed);
        try
        {
            Physics.SyncTransforms();
            foreach (float z in new[] { -4.2f, 4.2f })
                Assert.IsFalse(Physics.CheckCapsule(new Vector3(0f, 0.6f, z), new Vector3(0f, 1.6f, z), 0.46f, ~0, QueryTriggerInteraction.Ignore), $"door at z {z} is open for a cookie");
            Assert.IsFalse(Physics.CheckCapsule(new Vector3(0f, 0.6f, 0f), new Vector3(0f, 1.6f, 0f), 0.46f, ~0, QueryTriggerInteraction.Ignore), "room to hide inside");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Prefabs_PropsBlockButCannotBeClimbedToHide()
    {
        foreach (string name in new[] { "CHO_Crate", "CHO_Cart", "CHO_FactoryMachine", "CHO_Forklift", "CHO_PackingMachine", "CHO_ChocoShelf" })
        {
            GameObject prop = ChocolateFactoryAssets.LoadProp(name);
            Assert.NotNull(prop, name);
            BoxCollider box = prop.GetComponent<BoxCollider>();
            Assert.NotNull(box, name);
            Assert.GreaterOrEqual(box.size.y, 2.6f - 1e-3f, name + " too tall to stand on");
        }
        foreach (string name in new[] { "CHO_StreetLamp", "CHO_CCTV", "CHO_Signboard", "CHO_CakeDeco" })
            Assert.NotNull(ChocolateFactoryAssets.LoadProp(name)?.GetComponent<CapsuleCollider>(), name + " pole");
    }

    [Test]
    public void Scene_LayoutMatchesTheRequest()
    {
        Assert.IsTrue(SceneNames.IsMapScene(ChocolateFactoryMapBuilder.MapName));
        var scene = EditorSceneManager.OpenScene(ChocolateFactoryMapBuilder.ScenePath, OpenSceneMode.Single);
        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        Transform[] all = map.GetComponentsInChildren<Transform>(true);
        for (int q = 0; q < 4; q++)
        {
            Transform b = all.First(t => t.name == $"Building_Q{q + 1}");
            Transform bld = b.GetChild(0);
            Assert.AreEqual(ChocolateFactoryAssets.PrefabName(ChocolateFactoryAssets.MaterialBuildings[q]), PrefabUtility.GetCorrespondingObjectFromSource(bld.gameObject).name);
            Vector3 dir = ChocolateFactoryMapBuilder.Polar(1f, ChocolateFactoryMapBuilder.QuadDeg[q]);
            Assert.Greater(Vector3.Dot(b.position.normalized, dir), 0.99f, $"Q{q + 1} direction");
            Assert.AreEqual(1, b.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Repair_Point")), $"Q{q + 1} repair point");
            Assert.NotNull(b.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Socket_" + ChocolateFactoryAssets.MaterialBuildings[q]), $"Q{q + 1} socket");
            Assert.NotNull(b.GetComponent<QuadrantBuilding>(), $"Q{q + 1} quadrant building");
            Transform quadrant = map.Find($"Quadrant_Q{q + 1}");
            int sheds = quadrant.Cast<Transform>().Count(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == ChocolateFactoryAssets.ShedName);
            // 가운데 쪽 4 + 재료 건물·일반 건물 옆 두 구역에 ShedsPerZone씩(2026-10-28)
            Assert.AreEqual(ChocolateFactoryMapBuilder.Sheds.Length + 2 * ChocolateFactoryMapBuilder.ShedsPerZone, sheds, $"Q{q + 1} hideable sheds");
        }
        foreach ((string key, float deg) in ChocolateFactoryMapBuilder.Landmarks)
            Assert.NotNull(all.FirstOrDefault(t => t.name == "Landmark_" + key), key);
        foreach (string exit in ChocolateFactoryMapBuilder.ExitNames)
        {
            Transform root = all.First(t => t.name == exit);
            Assert.NotNull(root.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Door_")), exit + " door");
            Assert.NotNull(root.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("ExitTrigger_")), exit + " trigger");
            Assert.NotNull(root.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("ExitOpenLook_")), exit + " open look");
        }
        Assert.AreEqual(4, map.GetComponentsInChildren<MonsterRoomDoor>(true).Length, "monster room doors");
        Assert.AreEqual("CHO_MonsterRoom", PrefabUtility.GetCorrespondingObjectFromSource(map.Find("MonsterRoom/Model").gameObject)?.name);
        var manager = new SerializedObject(Object.FindFirstObjectByType<EscapeManager>());
        Assert.AreEqual(ChocolateFactoryMapBuilder.MapRadius, manager.FindProperty("mapRadius").floatValue, 1e-3f);
        // 문 ID는 씬 안에서 겹치지 않는다(건물마다 한 번)
        string[] ids = Object.FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(d => new SerializedObject(d).FindProperty("doorId").stringValue).ToArray();
        Assert.AreEqual(16, ids.Length, "8 buildings × 2 doors");
        Assert.AreEqual(ids.Length, ids.Distinct().Count(), "door ids unique");
        // 바닥: 아래판은 눕혀 있다(세로로 서면 큰 벽이 된다), 다섯 가지 타일이 다 깔렸다
        Bounds under = map.Find("GroundUnder").GetComponent<Renderer>().bounds;
        Assert.Less(under.size.y, 0.1f, "ground under plate lies flat");
        Assert.Greater(under.size.x, ChocolateFactoryMapBuilder.MapRadius * 2f);
        Transform tiles = map.Find("Tiles");
        foreach (string kind in ChocolateFactoryMapBuilder.TileKinds)
            Assert.IsTrue(tiles.Cast<Transform>().Any(t => t.name.StartsWith("Tiles_" + kind + "_")), kind);
    }

    // 건물째 45° 돌려 놓은 문도 안쪽을 바르게 안다(§42 — 월드 범위로 재던 때는 옆에서 온 캐릭터 쪽으로 열렸다). 문 루트 +Z = 방 안쪽.
    [Test]
    public void Scene_RotatedDoorsKnowWhichSideIsInside()
    {
        EditorSceneManager.OpenScene(ChocolateFactoryMapBuilder.ScenePath, OpenSceneMode.Single);
        var bind = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        InteractableDoor[] doors = Object.FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert.AreEqual(16, doors.Length);
        foreach (InteractableDoor d in doors)
        {
            typeof(InteractableDoor).GetMethod("ComputeGeometry", bind).Invoke(d, null);
            var inward = (Vector3)typeof(InteractableDoor).GetField("inwardNormal", bind).GetValue(d);
            Assert.Greater(Vector3.Dot(inward, d.transform.forward), 0.99f, d.DoorId + " inward " + inward + " vs " + d.transform.forward);
        }
    }

    [Test]
    public void Scene_EveryLampIsLitAndTheRoomGlows()
    {
        var scene = EditorSceneManager.OpenScene(ChocolateFactoryMapBuilder.ScenePath, OpenSceneMode.Single);
        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        Transform background = map.Find("Background");
        var lamps = map.GetComponentsInChildren<Transform>(true)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == "CHO_StreetLamp" && !t.IsChildOf(background)).ToList();
        Light[] lights = scene.GetRootGameObjects().First(g => g.name == "MapLighting").transform.Find("CHO_Lights").GetComponentsInChildren<Light>(true);
        Assert.Greater(lamps.Count, 100, "street lamps all over the map");
        foreach (Transform lamp in lamps)
            Assert.IsTrue(lights.Any(l => Vector3.Distance(l.transform.position, lamp.position + Vector3.up * 3.5f) < 0.1f), lamp.name + " at " + lamp.position + " has a light");
        Assert.AreEqual(8, map.Find("MonsterRoom").Cast<Transform>().Count(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == "CHO_StreetLamp"), "monster room lamps");
    }

    // 통행 검사(메뉴 Check Chocolate Factory Passability)가 남긴 보고 — 맵 전체·건물 안, 몸 크기 5가지씩 모두 CLEAN
    [Test]
    public void Passability_ReportIsCleanForAllBodies()
    {
        Assume.That(System.IO.File.Exists(ChocolateFactoryMapBuilder.PassabilityReport), "run the passability check first");
        string report = System.IO.File.ReadAllText(ChocolateFactoryMapBuilder.PassabilityReport);
        Assert.AreEqual(10, report.Split('\n').Count(l => l.StartsWith("CLEAN ")), report);
        StringAssert.DoesNotContain("ISSUES ", report);
    }
}
