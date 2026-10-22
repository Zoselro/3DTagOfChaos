using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 저주받은 놀이공원 실제 맵(Request1009Plan.md §36): 바이킹 흔들림 계산, 건물 재료 고정, 재료 건물 프리팹, 맵 씬 배치, 바이킹 소리.
public class CarnivalMapTests
{
    [Test]
    public void Viking_AngleIsDeterministicBoundedAndRestsBetweenRides()
    {
        var root = new GameObject("VikingTest");
        var ship = new GameObject("Ship").transform;
        ship.SetParent(root.transform, false);
        var ride = root.AddComponent<VikingRide>();
        ride.EditorSetup(ship);
        try
        {
            float peak = 0f;
            for (double t = 0; t < ride.Cycle * 2; t += 0.05)
            {
                float a = ride.AngleAt(t);
                Assert.AreEqual(a, ride.AngleAt(t), 1e-5f, "same time → same angle on every screen");
                Assert.LessOrEqual(Mathf.Abs(a), 70.01f);
                peak = Mathf.Max(peak, Mathf.Abs(a));
            }
            Assert.Greater(peak, 65f, "reaches a full swing");
            Assert.AreEqual(0f, ride.AngleAt(1.0), 1e-5f, "rests at the start of each cycle");
            Assert.AreEqual(0f, ride.AngleAt(ride.Cycle + 1.0), 1e-5f);
            // 진폭은 부드럽게 바뀐다(0.1초에 3° 넘게 변하지 않음)
            for (double t = 0; t < ride.Cycle; t += 0.1)
                Assert.Less(Mathf.Abs(ride.AmplitudeAt(t + 0.1) - ride.AmplitudeAt(t)), 3f);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void Recipe_CarnivalBuildingsHaveFixedMaterials()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<EscapeRecipeSO>("Assets/03. SO/Escape/Recipes/Recipe_CursedCandyCarnival.asset");
        Assert.NotNull(recipe);
        Assert.IsTrue(recipe.FixedBuildingOrder, "signs say the material — no shuffle");
        CollectionAssert.AreEqual(new[] { "Seatbelt", "Battery", "Toolbox", "RedButton" }, recipe.BuildingMaterials.Select(m => m.ItemId).ToArray());
    }

    [Test]
    public void Prefabs_MaterialBuildingsHaveOneRepairPointAtHandHeightAndATintedSocket()
    {
        foreach (string key in CarnivalMapAssets.BuildingKeys)
        {
            GameObject prefab = CarnivalMapAssets.LoadPrefab("CUR_Bld_" + key);
            Assert.NotNull(prefab, key);
            Transform[] all = prefab.GetComponentsInChildren<Transform>(true);
            Transform[] repair = all.Where(t => t.name.StartsWith("Repair_Point")).ToArray();
            Assert.AreEqual(1, repair.Length, key + " repair points");
            Assert.AreEqual(CarnivalMapAssets.RepairHeight, repair[0].localPosition.y, 1e-3f, key);
            Transform socket = all.FirstOrDefault(t => t.name.StartsWith("Socket_"));
            Assert.NotNull(socket, key + " socket");
            Assert.NotNull(socket.GetComponent<Renderer>(), key + " socket renderer (QuadrantBuilding tints it)");
            Assert.Less(Vector2.Distance(new Vector2(socket.position.x, socket.position.z), new Vector2(repair[0].position.x, repair[0].position.z)), 0.5f, key + " item sits on the socket");
            Assert.NotNull(all.FirstOrDefault(t => t.name == "ESC_Glow"), key + " glow");
            Assert.Greater(prefab.GetComponentsInChildren<MeshCollider>(true).Length, 0, key + " colliders");
        }
    }

    [Test]
    public void Scene_LayoutMatchesTheRequest()
    {
        Assert.IsTrue(SceneNames.IsMapScene(CarnivalMapBuilder.MapName));
        var scene = EditorSceneManager.OpenScene(CarnivalMapBuilder.ScenePath, OpenSceneMode.Single);
        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        for (int q = 0; q < 4; q++)
        {
            Transform b = map.GetComponentsInChildren<Transform>(true).First(t => t.name == $"Building_Q{q + 1}");
            Transform bld = b.GetChild(0);
            StringAssert.EndsWith(CarnivalMapAssets.BuildingKeys[q], PrefabUtility.GetCorrespondingObjectFromSource(bld.gameObject).name);
            Vector3 dir = CarnivalMapBuilder.Polar(1f, CarnivalMapBuilder.QuadDeg[q]);
            Assert.Greater(Vector3.Dot(b.position.normalized, dir), 0.99f, $"Q{q + 1} direction");
            Transform quadrant = map.Find($"Quadrant_Q{q + 1}");
            int tents = quadrant.Cast<Transform>().Count(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == "MO_Tent");
            Assert.AreEqual(4, tents, $"Q{q + 1} hideable tents");
        }
        foreach (string exit in CarnivalMapBuilder.ExitNames)
            Assert.NotNull(map.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == exit), exit);
        Assert.NotNull(Object.FindFirstObjectByType<VikingRide>(), "viking swings");
        Assert.NotNull(Object.FindFirstObjectByType<CoasterRide>(), "coaster");
        Assert.AreEqual(4, map.GetComponentsInChildren<MonsterRoomDoor>(true).Length, "monster room doors");
        var manager = new SerializedObject(Object.FindFirstObjectByType<EscapeManager>());
        Assert.AreEqual(CarnivalMapBuilder.MapRadius, manager.FindProperty("mapRadius").floatValue, 1e-3f);
    }

    // §37: 열린 탈출구 표시(EscapeGate가 켜는 ExitOpenLook_ — 안개를 받지 않는 빛기둥 + 점광원), 모든 가로등에 빛, 술래방 둘레 가로등
    [Test]
    public void Scene_ExitsHaveOpenBeaconsAndEveryLampIsLit()
    {
        var scene = EditorSceneManager.OpenScene(CarnivalMapBuilder.ScenePath, OpenSceneMode.Single);
        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        foreach (string exit in CarnivalMapBuilder.ExitNames)
        {
            Transform root = map.GetComponentsInChildren<Transform>(true).First(t => t.name == exit);
            Transform look = root.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("ExitOpenLook_"));
            Assert.NotNull(look, exit + " open look");
            Renderer beam = look.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Beam_Core");
            Assert.NotNull(beam, exit + " beam");
            Assert.AreEqual("TagOfChaos/Beacon", beam.sharedMaterial.shader.name);
            Assert.Greater(beam.bounds.max.y, 100f, exit + " beam reaches the sky");
            Assert.NotNull(look.GetComponentInChildren<Light>(true), exit + " open light");
            Assert.AreEqual(0, look.GetComponentsInChildren<Collider>(true).Length, exit + " beacon blocks nothing");
        }

        var lamps = map.GetComponentsInChildren<Transform>(true)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == "MO_StreetLamp").ToList();
        Light[] lights = scene.GetRootGameObjects().First(g => g.name == "MapLighting").transform.Find("CUR_Lights").GetComponentsInChildren<Light>(true);
        Assert.Greater(lamps.Count, 100, "street lamps all over the map");
        foreach (Transform lamp in lamps)
            Assert.IsTrue(lights.Any(l => Vector3.Distance(l.transform.position, lamp.position + Vector3.up * 4.7f) < 0.1f), lamp.name + " at " + lamp.position + " has a light");
        Transform room = map.Find("MonsterRoom");
        Assert.AreEqual(8, room.Cast<Transform>().Count(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == "MO_StreetLamp"), "monster room lamps");
        Assert.IsTrue(lights.Any(l => new Vector2(l.transform.position.x, l.transform.position.z).magnitude < 0.1f), "monster room inner light");
    }

    [Test]
    public void Audio_VikingSoundsHaveClips()
    {
        foreach (SoundId id in new[] { SoundId.VikingCreak, SoundId.VikingWhoosh, SoundId.VikingHum, SoundId.VikingBell })
        {
            Assert.IsTrue(SoundCatalogSO.Current.TryGet(id, out SoundCatalogSO.Entry e), id.ToString());
            Assert.Greater(e.clips.Length, 0, id.ToString());
        }
        Assert.IsTrue(SoundCatalogSO.Current.TryGet(SoundId.VikingCreak, out SoundCatalogSO.Entry creak));
        Assert.AreEqual(3, creak.clips.Length, "creak variations");
    }
}
