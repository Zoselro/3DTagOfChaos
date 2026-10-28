using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 초콜릿 공장 건물 8종(Request1009Plan.md §41): 움직이는 부품 계산, 건물 프리팹(문·충돌·움직임·소리·재료 받침), 확인용 씬.
public class ChocolateFactoryBuildingTests
{
    [Test]
    public void Spin_AngleComesFromTimeOnly()
    {
        Assert.AreEqual(0f, SpinPart.AngleAt(0f, 30f), 1e-4f);
        Assert.AreEqual(90f, SpinPart.AngleAt(3f, 30f), 1e-4f);
        Assert.AreEqual(270f, SpinPart.AngleAt(3f, -30f), 1e-4f, "negative speed turns the other way, kept in 0..360");
        Assert.AreEqual(SpinPart.AngleAt(1234.5f, 47f), SpinPart.AngleAt(1234.5f, 47f), "same time → same angle on every screen");
    }

    [Test]
    public void Scroll_And_Conveyor_WrapAround()
    {
        Vector2 o = TextureScroll.OffsetAt(new Vector2(0.9f, 0.5f), new Vector2(0f, 1.4f), 1f);
        Assert.AreEqual(0.9f, o.x, 1e-4f);
        Assert.AreEqual(0.9f, o.y, 1e-4f);
        for (float t = 0f; t < 20f; t += 0.37f)
            for (int i = 0; i < 5; i++)
            {
                float p = ConveyorItems.PhaseAt(i, 5, t, 1.2f, 10f);
                Assert.GreaterOrEqual(p, 0f);
                Assert.Less(p, 1f);
            }
        // 조각끼리 간격은 늘 같다(1/count)
        float a = ConveyorItems.PhaseAt(0, 4, 3.3f, 1.2f, 10f), b = ConveyorItems.PhaseAt(1, 4, 3.3f, 1.2f, 10f);
        Assert.AreEqual(0.25f, Mathf.Repeat(b - a, 1f), 1e-4f);
    }

    [Test]
    public void Prefabs_HaveTwoWorkingDoubleDoors()
    {
        foreach (string key in ChocolateFactoryAssets.AllBuildings)
        {
            GameObject prefab = Load(key);
            InteractableDoor[] doors = prefab.GetComponentsInChildren<InteractableDoor>(true);
            Assert.AreEqual(2, doors.Length, key + ": front + back door");
            Assert.AreEqual(2, doors.Select(d => d.DoorId).Distinct().Count(), key + ": door ids differ");
            foreach (InteractableDoor d in doors)
            {
                StringAssert.StartsWith(ChocolateFactoryAssets.PrefabName(key), d.DoorId, "id names the building (unique in a scene)");
                Animator animator = d.GetComponent<Animator>();
                Assert.IsNotNull(animator.runtimeAnimatorController, key + ": shared MapDoor controller");
                Assert.IsTrue(d.GetComponent<Rigidbody>().isKinematic);
                foreach (string leaf in new[] { ChocolateFactoryAssets.DoorLeafLeft, ChocolateFactoryAssets.DoorLeafRight })
                {
                    Transform t = d.transform.Find(leaf);
                    Assert.IsNotNull(t, $"{key} {d.name}: {leaf}");
                    var box = t.GetComponent<BoxCollider>();
                    Assert.IsNotNull(box, $"{key} {d.name}: {leaf} blocks");
                    Assert.GreaterOrEqual(box.size.x, 2.3f, "leaf 2.4 m wide (door 4.8 m ≥ 2.8 m rule, §42)");
                    Assert.GreaterOrEqual(box.size.y, 5f, "leaf taller than the monster");
                    Assert.IsNotNull(t.GetComponentInChildren<MeshRenderer>(), $"{key} {d.name}: {leaf} has its Blender mesh");
                }
                float gap = Vector3.Distance(d.transform.Find(ChocolateFactoryAssets.DoorLeafLeft).position, d.transform.Find(ChocolateFactoryAssets.DoorLeafRight).position);
                Assert.AreEqual(4.8f, gap, 0.05f, key + ": hinges 4.8 m apart — open leaves leave a 4.6 m corridor (§42 MP3)");
            }
        }
    }

    [Test]
    public void Prefabs_HaveColliders_Sounds_AndNoWalkableExterior()
    {
        foreach (string key in ChocolateFactoryAssets.AllBuildings)
        {
            GameObject prefab = Load(key);
            Assert.IsNotNull(prefab.transform.Find("COL_Fill"), key + ": building footprint filled");
            Assert.Greater(prefab.transform.Find("COL_Fill").childCount, 20, key);
            Assert.IsTrue(prefab.GetComponentsInChildren<MeshCollider>(true).Any(c => c.name.StartsWith("COL_Interior")), key + ": room walls collide");
            Assert.IsFalse(prefab.GetComponentsInChildren<MeshCollider>(true).Any(c => c.name.StartsWith("COL_Exterior") || c.name.StartsWith("Exterior")),
                key + ": the received model is not a climbable mesh collider");
            AmbientEmitter[] sounds = prefab.GetComponentsInChildren<AmbientEmitter>(true);
            Assert.GreaterOrEqual(sounds.Length, 1, key + ": factory sound");
            foreach (AmbientEmitter e in sounds)
                Assert.AreEqual((int)SoundId.AmbFactory, new SerializedObject(e).FindProperty("sound").intValue, key + ": old Game_ChocolateFactory machine sound");
            Assert.IsNotNull(prefab.transform.Find("Light_Inside"), key);
        }
    }

    [Test]
    public void Prefabs_MovingParts_MatchTheRequest()
    {
        int Spins(string key) => Load(key).GetComponentsInChildren<SpinPart>(true).Length;
        Assert.GreaterOrEqual(Spins("Gear"), 3, "Gear building: big + small front wheels + back wheel");
        Assert.GreaterOrEqual(Spins("Cogworks"), 6, "Cogworks: wheels + grinder rollers turn");
        // 2026-10-28: 덧붙였던 검은 벨트(롤러·조각·경로)가 받은 모델의 초콜릿 활송로와 겹쳐 지웠다 — 활송로만 남는다
        Assert.AreEqual(0, Load("Cogworks").GetComponentsInChildren<ConveyorItems>(true).Length, "no extra belt over the received chocolate chute");
        Assert.GreaterOrEqual(Load("Cogworks").GetComponentsInChildren<TextureScroll>(true).Length, 1, "chocolate pours out");
        Assert.GreaterOrEqual(Load("Cauldron").GetComponentsInChildren<TextureScroll>(true).Length, 2, "cauldron drain stream + pool");
        Assert.IsTrue(Load("Cauldron").GetComponentsInChildren<ParticleSystem>(true).Any(p => p.name == "Splash"), "cauldron splash");
        ParticleSystem[] smoke = Load("Factory").GetComponentsInChildren<ParticleSystem>(true).Where(p => p.name == "Smoke").ToArray();
        Assert.AreEqual(3, smoke.Length, "three chimneys smoke");
        foreach (ParticleSystem p in smoke)
        {
            Assert.IsTrue(p.main.loop, "smoke never stops");
            Assert.IsTrue(p.main.prewarm, "already smoking when the scene opens");
        }
    }

    // 문을 연 채로(문짝 충돌 끔) 바깥 3 m → 문 → 방 벽 → 방 안 2 m, 그리고 앞문 안쪽 → 뒷문 안쪽을 쿠키(반경 0.46, 키 2)와
    // 괴물(넉넉히 반경 0.9, 키 3 — 실제 충돌체 0.31)이 막힘 없이 지나가는지 캡슐로 쓸어 본다.
    [Test]
    public void Prefabs_DoorsLeadIntoTheRoom_ForCookieAndMonster()
    {
        var bodies = new[] { (name: "cookie", r: 0.46f, h: 2f), (name: "monster", r: 0.9f, h: 3f) };
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // 열린 맵 씬(원점의 술래방 등)과 겹치지 않게 빈 씬에서 잰다
        foreach (string key in ChocolateFactoryAssets.AllBuildings)
        {
            ChocolateFactoryAssets.Meta meta = ChocolateFactoryAssets.LoadMeta(key);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Load(key));
            try
            {
                foreach (InteractableDoor d in go.GetComponentsInChildren<InteractableDoor>())
                    foreach (Collider c in d.GetComponentsInChildren<Collider>()) c.enabled = false;
                Physics.SyncTransforms();
                Vector3 lo = V(meta.room.min), hi = V(meta.room.max);
                var inside = new System.Collections.Generic.List<Vector3>();
                foreach (ChocolateFactoryAssets.DoorMeta d in meta.doors)
                {
                    Vector3 inward = Quaternion.Euler(0f, d.yaw, 0f) * Vector3.forward;
                    Vector3 centre = V(d.centre);
                    var wall = new Vector3(centre.x, 0f, inward.z < 0f ? hi.z : lo.z);
                    Vector3 entry = wall + inward * 2f;
                    inside.Add(entry);
                    foreach (var b in bodies)
                        AssertClear($"{key} {d.side} {b.name}", b.r, b.h, centre - inward * 3f, centre, wall, entry);
                }
                foreach (var b in bodies) AssertClear($"{key} room {b.name}", b.r, b.h, inside[0], inside[1]);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }

    private static void AssertClear(string label, float r, float h, params Vector3[] path)
    {
        Vector3 Bottom(Vector3 p) => p + Vector3.up * (r + 0.35f); // 0.35 m 문턱·바닥 판은 넘는다
        Vector3 Top(Vector3 p) => p + Vector3.up * Mathf.Max(r + 0.35f, h - r);
        for (int i = 0; i < path.Length; i++)
        {
            Collider[] at = Physics.OverlapCapsule(Bottom(path[i]), Top(path[i]), r, ~0, QueryTriggerInteraction.Ignore);
            Assert.IsEmpty(at, $"{label}: blocked at point {i} {path[i]} by {string.Join(",", at.Select(c => c.name))}");
            if (i == 0) continue;
            Vector3 d = path[i] - path[i - 1];
            if (d.magnitude < 0.01f) continue;
            bool hit = Physics.CapsuleCast(Bottom(path[i - 1]), Top(path[i - 1]), r, d.normalized, out RaycastHit info, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Assert.IsFalse(hit, $"{label}: blocked between {path[i - 1]} and {path[i]} by {(hit ? info.collider.name : "")} at {(hit ? info.point : Vector3.zero)}");
        }
    }

    private static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

    [Test]
    public void MaterialBuildings_ShowWhichMaterialGoesIn()
    {
        foreach (string key in ChocolateFactoryAssets.MaterialBuildings)
        {
            GameObject prefab = Load(key);
            Transform socket = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Socket_"));
            Transform repair = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Repair_Point"));
            Assert.IsNotNull(socket, key + ": socket (tinted by QuadrantBuilding)");
            Assert.IsNotNull(socket.GetComponent<Renderer>(), key);
            Assert.AreEqual("Socket_" + key, socket.name, "socket named after the recipe material");
            Assert.IsNotNull(repair, key + ": repair point");
            Assert.AreEqual(1.3f, repair.position.y, 0.05f, "cookie hand height");
            Assert.IsNotNull(prefab.transform.Find("ESC_Glow"), key);
            Assert.IsTrue(prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(m => m != null && m.name.Contains("Sign_" + key)),
                key + ": material sign over the doors");
        }
        foreach (string key in ChocolateFactoryAssets.GeneralBuildings)
            Assert.IsFalse(Load(key).GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Socket_")), key + " takes no material");
    }

    [Test]
    public void TestScene_HasAllEightBuildings()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(ChocolateFactoryBuildingsScene.ScenePath, OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == ChocolateFactoryBuildingsScene.RootName);
            Assert.IsNotNull(root);
            foreach (string key in ChocolateFactoryAssets.AllBuildings)
                Assert.IsNotNull(root.transform.Find(ChocolateFactoryAssets.PrefabName(key)), key);
            Assert.AreEqual(16, root.GetComponentsInChildren<InteractableDoor>(true).Select(d => d.DoorId).Distinct().Count(), "16 doors, unique ids");
        }
        finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    private static GameObject Load(string key)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChocolateFactoryAssets.PrefabPath(key));
        Assert.IsNotNull(prefab, ChocolateFactoryAssets.PrefabPath(key));
        return prefab;
    }
}
