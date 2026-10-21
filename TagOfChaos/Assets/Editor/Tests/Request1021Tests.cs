using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 2026-10-21 후속(Request1009Plan.md §28.15): 술래방 안 쿠키 내보내기(E1)·흰 땅 다시 깔기(E2). §29: 놀이공원 회전목마 교체. §30: 롤러코스터 탈출구.
public class Request1021Tests
{
    private static readonly Vector3[] CornerDoors =
        { new Vector3(-9f, 2.25f, 9f), new Vector3(9f, 2.25f, 9f), new Vector3(-9f, 2.25f, -9f), new Vector3(9f, 2.25f, -9f) };

    // E1: 방 안쪽 = 벽 안쪽 면 안 + 모서리 문 면 안. 문 앞·벽 바깥은 안쪽이 아니다.
    [Test]
    public void RoomGuard_IsInside_FollowsWallsAndDoors()
    {
        Assert.IsTrue(MonsterRoomGuard.IsInside(Vector3.zero, 11.7f, CornerDoors, Vector3.zero));
        Assert.IsTrue(MonsterRoomGuard.IsInside(Vector3.zero, 11.7f, CornerDoors, new Vector3(8f, 0f, 8f)), "inside, before the door");
        Assert.IsTrue(MonsterRoomGuard.IsInside(Vector3.zero, 11.7f, CornerDoors, new Vector3(11f, 0f, -3f)), "inside, by the east wall");
        Assert.IsFalse(MonsterRoomGuard.IsInside(Vector3.zero, 11.7f, CornerDoors, new Vector3(11f, 0f, 11f)), "outside the corner door");
        Assert.IsFalse(MonsterRoomGuard.IsInside(Vector3.zero, 11.7f, CornerDoors, new Vector3(12.5f, 0f, 0f)), "hugging the east wall outside");
        Assert.IsFalse(MonsterRoomGuard.IsInside(Vector3.zero, 11.7f, CornerDoors, new Vector3(30f, 0f, 2f)));
    }

    // E2: 지형 높이 — 울타리 안은 바닥판 밑, 울타리 둘레는 평평, 탈출구 받침 앞은 더 멀리까지 평평, 바깥은 원래 언덕
    [Test]
    public void TerrainHeight_FlatAroundFence_RisesOutside()
    {
        Assert.Less(CandyForestMapBuilder.TerrainHeight(60f, 30f, 1.4f), 0f, "inside the fence");
        Assert.AreEqual(0.015f, CandyForestMapBuilder.TerrainHeight(58f, 58f, 3f), 1e-4f, "r 82 diagonal is flat");
        Assert.AreEqual(0.015f, CandyForestMapBuilder.TerrainHeight(0f, 91f, 4f), 1e-4f, "exit pad stays flat");
        Assert.Greater(CandyForestMapBuilder.TerrainHeight(64f, 64f, 4f), 1f, "r 90.5 diagonal is rising");
        Assert.AreEqual(5f, CandyForestMapBuilder.TerrainHeight(0f, 110f, 5f), 1e-4f, "far out keeps the hill");
    }

    // E1·E2 씬: 술래방 지킴이, 울타리 안에 흰 땅이 솟지 않음, 초록 원판, 울타리 밖 숲이 땅 위에
    [Test]
    public void CandyForestScene_GroundFitsAndRoomGuard()
    {
        Scene scene = SceneManager.GetSceneByPath(CandyForestMapBuilder.ScenePath);
        bool opened = !scene.isLoaded; // 이미 열려 있으면 그대로 쓰고 닫지 않는다
        if (opened) scene = EditorSceneManager.OpenScene(CandyForestMapBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;

            var guard = map.Find("MonsterRoom").GetComponent<MonsterRoomGuard>();
            Assert.NotNull(guard, "MonsterRoomGuard on MonsterRoom");
            Assert.AreEqual(11.7f, guard.InnerHalf, 0.05f);
            Assert.IsTrue(guard.IsInside(new Vector3(0f, 0.5f, 0f)));
            Assert.IsFalse(guard.IsInside(new Vector3(0f, 0.5f, 14f)));

            Transform terrain = map.Find("Background/CAN_Terrain_Decor");
            Mesh mesh = terrain.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual("CF_TerrainRing", mesh.name);
            Assert.AreSame(mesh, terrain.GetComponent<MeshCollider>().sharedMesh);
            Matrix4x4 m = terrain.localToWorldMatrix;
            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 w = m.MultiplyPoint3x4(v);
                if (new Vector2(w.x, w.z).magnitude < 86f) Assert.LessOrEqual(w.y, 0.02f, $"white ground pokes up at {w}");
            }

            Renderer disc = map.Find("Ground/GrassDisc").GetComponent<Renderer>();
            Assert.Less(disc.bounds.size.y, 0.01f, "grass disc lies flat");
            Assert.Greater(disc.bounds.size.x, 160f);

            var col = terrain.GetComponent<MeshCollider>();
            Transform forest = map.Find("Forest");
            Assert.NotNull(forest);
            int checkedTrees = 0;
            foreach (Transform tree in forest)
            {
                Vector3 p = tree.position;
                if (!col.Raycast(new Ray(new Vector3(p.x, 60f, p.z), Vector3.down), out RaycastHit hit, 120f)) continue;
                Assert.AreEqual(Mathf.Max(0f, hit.point.y), p.y, 0.05f, $"{tree.name} stands on the ground");
                checkedTrees++;
            }
            Assert.Greater(checkedTrees, 20);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    // §29: CUR_Carousel_Top = 새 모델(본체 + 목마 16마리) — 옛 메시 없음, 돌기·키네마틱 Rigidbody 그대로, 볼록 충돌이 받침 바닥까지,
    // 목마 머리(가장 높은 점: 뿔·귀)가 회전 방향(클립 −360°, 위에서 볼 때 반시계) 쪽
    [Test]
    public void CarnivalCarousel_NewModelSpinsHorsesForward()
    {
        string path = MapSceneBuilder.ScenePath("CursedCandyCarnival");
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Transform top = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).First(t => t.name == CarouselModelSwap.TopName);
            Assert.IsNull(top.GetComponent<MeshRenderer>(), "old carousel mesh removed");
            Assert.NotNull(top.GetComponent<Animator>().runtimeAnimatorController, "still spins");
            Assert.IsTrue(top.GetComponent<Rigidbody>().isKinematic);
            Transform model = top.Find(CarouselModelSwap.ModelChild);
            Assert.NotNull(model);
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).ToList();
            CollectionAssert.AreEquivalent(new[] { "CarouselBody", "CarouselHorses" }, renderers.Select(r => r.name).ToArray());

            var mc = top.GetComponent<MeshCollider>();
            Assert.IsTrue(mc.convex);
            Assert.LessOrEqual(mc.sharedMesh.triangles.Length / 3, 255, "within the convex limit");
            Bounds look = renderers.First(r => r.name == "CarouselBody").bounds;
            Physics.SyncTransforms();
            Assert.LessOrEqual(mc.bounds.min.y, look.min.y + 0.1f, "collision reaches the platform bottom");
            Assert.AreEqual(look.size.x, mc.bounds.size.x, 0.5f, "collision as wide as the body");

            // 목마: 기둥마다 한 마리 — 기둥 둘레 2.5 m 안의 가장 높은 점이 그 자리 회전 속도 방향 쪽
            MeshFilter horses = model.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == "CarouselHorses");
            Matrix4x4 toWorld = horses.transform.localToWorldMatrix;
            Vector3[] world = horses.sharedMesh.vertices.Select(v => toWorld.MultiplyPoint3x4(v)).ToArray();
            Vector3 c = top.position;
            int forward = 0;
            for (int i = 0; i < 16; i++)
            {
                float a = (11.25f + 22.5f * i) * Mathf.Deg2Rad;
                var pole = new Vector3(c.x + Mathf.Cos(a) * 11.66f, 0f, c.z + Mathf.Sin(a) * 11.66f);
                var near = world.Where(w => new Vector2(w.x - pole.x, w.z - pole.z).magnitude < 2.5f).ToList();
                if (near.Count == 0) continue;
                Vector3 head = near.OrderByDescending(w => w.y).First();
                Vector3 r = pole - c;
                var velocity = new Vector3(-r.z, 0f, r.x); // ω = (0, −w, 0) → v = (−w·z, 0, w·x)
                if (Vector3.Dot(new Vector3(head.x - pole.x, 0f, head.z - pole.z), velocity) > 0f) forward++;
            }
            Assert.AreEqual(16, forward, "every horse faces the way it moves");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    // §30: 롤러코스터 탈출구 — 예전 장치 없음, 입구·트랙 모델, Exit_N(문·트리거·열림 표시, 트리거는 맵 안 뒤 계단 아래),
    // 임시 재료 받침 4개, EscapeManager에 건물 4·탈출구 1 연결, 계단 경사 충돌(속이 찬 볼록 쐐기)
    [Test]
    public void CarnivalCoasterExit_WiredForEscape()
    {
        string path = MapSceneBuilder.ScenePath(CoasterExitBuilder.MapName);
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            Assert.IsFalse(all.Any(t => t.name == "ESC_CursedCandyCarnival_Root"), "legacy coaster device removed");
            Assert.IsTrue(all.Any(t => t.name == "CoasterEntrance"), "entrance");
            Assert.IsTrue(all.Any(t => t.name == "CoasterTrack"), "track");

            var manager = scene.GetRootGameObjects().Select(g => g.GetComponent<EscapeManager>()).First(m => m != null);
            var so = new UnityEditor.SerializedObject(manager);
            var exits = so.FindProperty("exits");
            Assert.AreEqual(1, exits.arraySize);
            var gate = (EscapeGate)exits.GetArrayElementAtIndex(0).objectReferenceValue;
            Assert.AreEqual("Exit_N", gate.name);
            var parts = gate.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
            CollectionAssert.IsSubsetOf(new[] { "Door_N", "ExitTrigger_N", "ExitOpenLook_N" }, parts);
            Transform trigger = gate.GetComponentsInChildren<Transform>(true).First(t => t.name == "ExitTrigger_N");
            Assert.Less(Mathf.Abs(trigger.position.z), 70.7f, "trigger inside the map (boundary 71.2)");
            Assert.Greater(trigger.position.z, CoasterExitBuilder.EntranceCenter.z, "trigger behind the entrance (down the back stairs)");
            Assert.NotNull(gate.GetComponentsInChildren<Transform>(true).First(t => t.name == "Door_N").GetComponent<BoxCollider>(), "closed door blocks");

            var buildings = so.FindProperty("buildings");
            Assert.AreEqual(4, buildings.arraySize);
            for (int i = 0; i < 4; i++)
            {
                var b = (QuadrantBuilding)buildings.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.NotNull(b, $"building {i}");
                var names = b.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
                Assert.IsTrue(names.Any(n => n.StartsWith("Repair_Point")) && names.Any(n => n.StartsWith("Socket_")) && names.Contains("ESC_Glow"), b.name);
            }
            foreach (string ramp in new[] { "COL_StairsFront", "COL_StairsBack" })
            {
                var mc = all.First(t => t.name == ramp).GetComponent<MeshCollider>();
                Assert.IsTrue(mc.convex, ramp);
            }
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
