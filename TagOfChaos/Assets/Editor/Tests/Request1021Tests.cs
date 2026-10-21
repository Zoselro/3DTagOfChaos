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

    // E2: 지형 높이 — 울타리 안은 바닥판 밑, 울타리 둘레는 평평, 탈출구 받침 앞은 더 멀리까지 평평, 바깥은 원래 언덕(§32: 맵 반지름 기준)
    [Test]
    public void TerrainHeight_FlatAroundFence_RisesOutside()
    {
        float r = CandyForestLayout.MapRadius, d = 1f / Mathf.Sqrt(2f);
        Assert.Less(CandyForestMapBuilder.TerrainHeight((r - 20f) * 0.9f, (r - 20f) * 0.44f, 1.4f), 0f, "inside the fence");
        Assert.AreEqual(0.015f, CandyForestMapBuilder.TerrainHeight((r + 2f) * d, (r + 2f) * d, 3f), 1e-4f, "just outside the fence (diagonal) is flat");
        Assert.AreEqual(0.015f, CandyForestMapBuilder.TerrainHeight(0f, r + 11f, 4f), 1e-4f, "exit pad stays flat");
        Assert.Greater(CandyForestMapBuilder.TerrainHeight((r + 10.5f) * d, (r + 10.5f) * d, 4f), 1f, "diagonal is rising");
        Assert.AreEqual(5f, CandyForestMapBuilder.TerrainHeight(0f, r + 30f, 5f), 1e-4f, "far out keeps the hill");
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
                if (new Vector2(w.x, w.z).magnitude < CandyForestLayout.MapRadius + 6f) Assert.LessOrEqual(w.y, 0.02f, $"white ground pokes up at {w}");
            }

            Renderer disc = map.Find("Ground/GrassDisc").GetComponent<Renderer>();
            Assert.Less(disc.bounds.size.y, 0.01f, "grass disc lies flat");
            Assert.Greater(disc.bounds.size.x, CandyForestLayout.MapRadius * 2f);

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

    // §32: 재료 건물 (±82.2, ±82.2) · 맵 반지름 155 m · 술래방 문 동서남북(대각선은 벽으로 막힘) · 쿠키 내보내기 판정이 네모 방
    [Test]
    public void CandyForest_FarBuildingsAndCardinalRoomDoors()
    {
        Assert.AreEqual(155f, CandyForestLayout.MapRadius);
        Scene scene = SceneManager.GetSceneByPath(CandyForestMapBuilder.ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(CandyForestMapBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
            for (int q = 0; q < 4; q++)
            {
                Transform b = map.GetComponentsInChildren<Transform>(true).First(t => t.name == "Building_Q" + (q + 1));
                Vector2 want = CandyForestLayout.QuadrantDir[q] * CandyForestLayout.BuildingRadius;
                Assert.Less((new Vector2(b.position.x, b.position.z) - want).magnitude, 0.05f, b.name);
            }
            Assert.AreEqual(82.2f, Mathf.Abs(CandyForestLayout.QuadrantDir[0].x * CandyForestLayout.BuildingRadius), 0.05f);

            Transform room = map.Find("MonsterRoom");
            Assert.IsFalse(room.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("RoomCorner_")), "diagonal corners removed");
            var doors = room.GetComponentsInChildren<MonsterRoomDoor>(true).OrderBy(d => d.name).ToArray();
            Assert.AreEqual(4, doors.Length);
            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = CandyForestLayout.RoadDir[i];
                Vector3 want = new Vector3(dir.x, 0f, dir.y) * CandyForestLayout.RoomHalf;
                Assert.Less(new Vector2(doors[i].transform.position.x - want.x, doors[i].transform.position.z - want.z).magnitude, 0.05f, doors[i].name + " at 12/3/6/9 o'clock");
            }
            Physics.SyncTransforms();
            foreach (Vector2 diag in CandyForestLayout.QuadrantDir)
            {
                // 다른 씬이 같이 열려 있을 수 있다 — 이 씬의 충돌체 중 가장 가까운 것
                var hits = Physics.RaycastAll(new Vector3(0f, 1.5f, 0f), new Vector3(diag.x, 0f, diag.y), 30f)
                    .Where(h => h.collider.gameObject.scene == scene).OrderBy(h => h.distance).ToList();
                Assert.IsNotEmpty(hits, $"diagonal {diag} is walled");
                Assert.IsTrue(hits[0].collider.name.StartsWith("RoomWall_"), hits[0].collider.name);
                Assert.Less(hits[0].distance, 18f, "wall at the corner");
            }
            var guard = room.GetComponent<MonsterRoomGuard>();
            Assert.IsTrue(guard.IsInside(new Vector3(11f, 0.5f, 11f)), "square room: corner is inside");
            Assert.IsFalse(guard.IsInside(new Vector3(0f, 0.5f, 12.5f)), "in the north doorway outside");
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

    // §31 롤러코스터 경로: 닫힌 고리·고른 간격·위쪽이 진행 방향에 직교(고리 꼭대기에서 뒤집힘)·시간표가 늘어남
    [Test]
    public void CoasterPath_ClosedSmoothAndTimed()
    {
        var path = UnityEditor.AssetDatabase.LoadAssetAtPath<CoasterPathSO>(CoasterPathBaker.AssetPath);
        Assert.NotNull(path, "baked path asset");
        Assert.IsTrue(path.IsValid);
        Assert.Greater(path.Length, 150f, "both loops + the shared section twice");
        bool inverted = false;
        for (int i = 0; i < path.Count; i++)
        {
            int j = (i + 1) % path.Count;
            float gap = (path.Point(j) - path.Point(i)).magnitude;
            Assert.Less(gap, 0.6f, $"spacing at {i} (closed loop when j = 0)");
            Assert.AreEqual(1f, path.Up(i).magnitude, 1e-3f);
            if (j > 0) Assert.Greater(path.TimeAt(j), path.TimeAt(i), $"time increases at {i}");
            if (path.Up(i).y < -0.9f) inverted = true;
        }
        Assert.IsTrue(inverted, "upside down at the loop tops");
        Vector3 prev = Vector3.zero;
        for (float s = 0f; s <= path.Length; s += 0.05f)
        {
            path.Sample(s, out Vector3 pos, out Vector3 fwd, out Vector3 up);
            Assert.Less(Mathf.Abs(Vector3.Dot(fwd, up)), 1e-3f, $"up orthogonal at {s}");
            if (s > 0f) Assert.Less((pos - prev).magnitude, 0.12f, $"continuous at {s}");
            prev = pos;
        }
        Assert.AreEqual(path.DistanceAtTime(1.5), path.DistanceAtTime(1.5 + path.LapTime), 1e-2f, "wraps every lap");
    }

    // §31 Test 씬 놀이기구: 열차 2편성(칸마다 키네마틱 Rigidbody + 상자 충돌)·레일/지지대 고정 충돌·울타리·회전목마 회전·
    // 두 열차가 한 바퀴 동안 서로 떨어져 있고 칸이 레일 위에 있음·옮긴 상자/표시가 놀이기구 자리에 없음
    [Test]
    public void TestCarnivalRides_CoasterAndCarousel()
    {
        Scene scene = SceneManager.GetSceneByPath(CarnivalRidesBuilder.ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(CarnivalRidesBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            var ride = all.Select(t => t.GetComponent<CoasterRide>()).First(r => r != null);
            Assert.AreEqual(2, ride.TrainCount);
            for (int t = 0; t < 2; t++)
            {
                CoasterTrain train = ride.Train(t);
                Assert.AreEqual(7, train.CarCount);
                Assert.NotNull(train.GetComponent<CoasterTrainAudio>());
                for (int c = 0; c < train.CarCount; c++)
                {
                    Assert.IsTrue(train.CarBody(c).isKinematic);
                    Assert.NotNull(train.CarBody(c).GetComponent<BoxCollider>());
                }
            }
            foreach (string part in new[] { "CoasterRail", "CoasterSupports" })
            {
                var mc = all.First(x => x.name == part).GetComponent<MeshCollider>();
                Assert.NotNull(mc, part);
                Assert.IsFalse(mc.convex, part);
            }
            Assert.NotNull(all.First(x => x.name == "Fence_E").GetComponent<BoxCollider>());
            Assert.NotNull(all.First(x => x.name == "Fence_N").GetComponent<BoxCollider>());
            foreach (string gate in new[] { "Gate_Front", "Gate_Back" }) Assert.NotNull(all.First(x => x.name == gate).GetComponent<BoxCollider>(), gate);

            CoasterPathSO path = ride.Path;
            Transform track = ride.transform;
            float minGap = float.MaxValue;
            for (int k = 0; k < 120; k++)
            {
                double time = path.LapTime * k / 120;
                ride.EditorPlaceAt(time);
                var a = Enumerable.Range(0, 7).Select(c => ride.Train(0).CarBody(c).transform.position).ToList();
                var b = Enumerable.Range(0, 7).Select(c => ride.Train(1).CarBody(c).transform.position).ToList();
                foreach (Vector3 p in a) foreach (Vector3 q in b) minGap = Mathf.Min(minGap, (p - q).magnitude);
                // 칸 기준점(바퀴 사이)은 레일 윗면 가운데 선에서 가깝다
                foreach (Vector3 p in a)
                {
                    Vector3 local = track.InverseTransformPoint(p);
                    float best = float.MaxValue;
                    for (int i = 0; i < path.Count; i++) best = Mathf.Min(best, (path.Point(i) + path.Up(i) * path.RailTop - local).magnitude);
                    Assert.Less(best, 0.6f, $"car on the rail at t {time:F1}");
                }
            }
            Assert.Greater(minGap, 3f, "two trains never meet");
            ride.EditorPlaceAt(0d);

            Transform top = all.First(x => x.name == CarouselModelSwap.TopName);
            Assert.NotNull(top.GetComponent<Animator>().runtimeAnimatorController, "carousel spins");
            Assert.IsTrue(top.GetComponent<Rigidbody>().isKinematic);
            Assert.IsTrue(top.GetComponent<MeshCollider>().convex);
            Vector3 amb = all.First(x => x.name == "AMB_Carousel").position;
            Assert.Less(new Vector2(amb.x - CarnivalRidesBuilder.CarouselCenter.x, amb.z - CarnivalRidesBuilder.CarouselCenter.z).magnitude, 0.1f, "carousel sound at the carousel");

            foreach (Transform x in all.Where(x => x.GetComponent<MaterialChest>() != null || x.name.StartsWith("RocketSpawn_") || x.name.StartsWith("RainbowSpawn_") || x.name.StartsWith("Spawn_Cookie_")))
            {
                Vector3 p = x.position;
                Assert.IsFalse(p.x < CarnivalRidesBuilder.FenceEastX && p.z < CarnivalRidesBuilder.FenceNorthZ, x.name + " inside the coaster fence");
                Assert.Greater(new Vector2(p.x - CarnivalRidesBuilder.CarouselCenter.x, p.z - CarnivalRidesBuilder.CarouselCenter.z).magnitude, CarnivalRidesBuilder.CarouselRadius, x.name + " under the carousel");
            }
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
