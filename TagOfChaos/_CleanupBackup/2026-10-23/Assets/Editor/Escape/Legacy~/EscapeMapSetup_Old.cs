using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 맵 씬에 탈출 모드 배치를 만든다(EscapePlan.md §4, §5): EscapeSetup 루트(EscapeManager), 탈출 장치(ESC_<Map>_Root),
// 스파이 로켓(SPY_Rocket_<Map>_Root), 상자 자리 20곳(CHEST_Slot_nn), 마녀 위치(WITCH_Anchor).
// 걸을 수 있는 땅 위에서 주변이 트인 곳만 고른다: 장치는 맵 가운데 쪽, 로켓은 장치에서 먼 곳, 상자는 서로 멀리 흩어지게.
// 상자 주변은 2.5 m를 비워 괴물(통행 반지름 1.8 m)이 지나가는 통로를 좁히지 않는다. 배치는 맵 이름으로 시드를 정해 늘 같다.
// 맵 축소(MapCompactor.Compact)가 끝날 때 자동으로 다시 만든다. 모양은 Blender 모델(Escape/Models/*.fbx, escape_assets.py)을
// 쓰고, 모델이 없으면 같은 이름 규칙의 임시 도형으로 대신한다.
public static class EscapeMapSetup
{
    public const string RootName = "EscapeSetup";
    public const int ChestSlotCount = 20;
    private const string MaterialFolder = "Assets/09. Environment/Escape/Materials";
    private const float SampleStep = 2f;
    private const float DeviceClearance = 6.5f;  // 장치 반 길이(최대 3 m) + 괴물 통로 폭(3.6 m) — 장치 옆에 쿠키만 들어가는 틈이 생기지 않게
    private const float RocketClearance = 5f;    // 로켓 날개 반지름(1.4 m) + 괴물 통로 폭
    private const float ChestClearance = 2.5f;
    private const float MinChestSpacing = 9f;
    private const float CoasterEdgeMin = 16f, CoasterEdgeMax = 34f, TrainEdgeMin = 18f; // 롤러코스터 자리에서 외곽 벽까지(레일이 벽을 넘는 높이)
    private const float WitchDistance = MapCompactor.NewHalf + 85f;

    [MenuItem("Tools/TagOfChaos/Escape/Setup Open Map Scene")]
    public static void SetupOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        string map = SceneNames.MapOf(scene.name);
        Debug.Log(Apply(scene, map));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/TagOfChaos/Escape/Setup All Map Scenes")]
    public static void SetupAll()
    {
        foreach (string map in MapSceneBuilder.MapNames)
        {
            Scene scene = EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);
            Debug.Log(Apply(scene, map));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    public static string Apply(Scene scene, string map)
    {
        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToList()) Object.DestroyImmediate(old);
        Physics.SyncTransforms();

        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        EscapeRecipeSO recipe = catalog != null ? catalog.RecipeFor(map) : null;
        EscapeExitKind exitKind = recipe != null ? recipe.ExitKind : EscapeExitKind.CakeRocket;

        List<Vector3> ground = SampleWalkable();
        var avoid = new List<Vector3>();
        foreach (string spawn in new[] { SceneSpawnPoints.Cookie, SceneSpawnPoints.Monster })
        {
            GameObject sp = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == spawn)?.gameObject;
            if (sp != null) avoid.Add(sp.transform.position);
        }

        var rng = new System.Random(map.GetHashCode());
        Vector3? device = FixedDeviceSpot(map, out Vector3? deviceForward);
        bool vehicle = exitKind == EscapeExitKind.RollerCoaster || exitKind == EscapeExitKind.ChocolateTrain;
        if (device == null && vehicle)
        {
            device = VehicleSpot(ground, avoid, exitKind == EscapeExitKind.ChocolateTrain, out Vector3 forward);
            if (device == null) return $"[EscapeMapSetup] {map}: no open spot for the {exitKind}.";
            deviceForward = forward;
        }
        if (device == null)
            device = ground.Where(p => Far(p, avoid, 10f) && IsClear(p, DeviceClearance))
                .OrderBy(p => Flat(p).magnitude).Cast<Vector3?>().FirstOrDefault();
        if (device == null) return $"[EscapeMapSetup] {map}: no open spot for the escape device.";
        avoid.Add(device.Value);
        if (vehicle && deviceForward != null) // 레일 위·아래에는 로켓·상자를 두지 않는다
            for (float s = -6f; s < CoasterEdgeMax + 6f; s += 4f) avoid.Add(device.Value + deviceForward.Value * s);
        if (exitKind == EscapeExitKind.RuneAltar && deviceForward != null) // 시계탑 문 앞은 비운다
            for (float z = 8f; z <= 16f; z += 4f) avoid.Add(device.Value + deviceForward.Value * z);
        if (exitKind == EscapeExitKind.BakeryMachine && deviceForward != null) // 오븐 앞 경사 발판·기계 칸 앞은 비운다
        {
            Vector3 right = Vector3.Cross(Vector3.up, deviceForward.Value);
            for (float x = -22f; x <= 22f; x += 4f)
                for (float z = 13f; z <= 29f; z += 4f) avoid.Add(device.Value + deviceForward.Value * z + right * x);
        }

        Vector3? rocket = ground.Where(p => Far(p, avoid, 12f) && IsClear(p, RocketClearance))
            .OrderByDescending(p => Mathf.Min(Dist(p, device.Value), avoid.Min(a => Dist(p, a)) * 1.5f)).Cast<Vector3?>().FirstOrDefault();
        if (rocket == null) return $"[EscapeMapSetup] {map}: no open spot for the spy rocket.";
        avoid.Add(rocket.Value);

        var chestCandidates = ground.Where(p => Far(p, avoid, 6f) && IsClear(p, ChestClearance)).ToList();
        var chests = new List<Vector3>();
        if (chestCandidates.Count > 0) chests.Add(chestCandidates[rng.Next(chestCandidates.Count)]);
        while (chests.Count < ChestSlotCount && chestCandidates.Count > 0)
        {
            Vector3 best = default;
            float bestScore = -1f;
            foreach (Vector3 c in chestCandidates)
            {
                float d = chests.Min(x => Dist(x, c));
                if (d > bestScore) { bestScore = d; best = c; }
            }
            if (bestScore < MinChestSpacing * 0.5f) break;
            chests.Add(best);
        }

        // ---- build ----
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        var manager = root.AddComponent<EscapeManager>();
        BindSceneReferences(manager, map);

        EscapeDevice deviceComp = BuildDevice(root.transform, map, exitKind, device.Value, avoid.Count > 0 ? avoid[0] : Vector3.back * 50f, deviceForward);
        if (exitKind == EscapeExitKind.ChocolateTrain && deviceForward != null) FitTrainTunnel(deviceComp.transform, deviceForward.Value);
        if (exitKind == EscapeExitKind.RuneAltar && deviceComp.GetComponent<RuneAltarSequence>() != null) OpenUnderground(map, deviceComp.transform);
        SpyRocket rocketComp = BuildRocket(root.transform, map, rocket.Value, device.Value);
        var anchors = new Transform[chests.Count];
        for (int i = 0; i < chests.Count; i++) anchors[i] = BuildChest(root.transform, i, chests[i], device.Value);

        var witch = new GameObject("WITCH_Anchor");
        witch.transform.SetParent(root.transform, false);
        Vector3 away = Flat(rocket.Value - device.Value);
        Vector3 dir = away.sqrMagnitude > 1f ? away.normalized : Vector3.forward;
        witch.transform.position = dir * WitchDistance;
        GameObject witchModel = InstantiateModel("WITCH", witch.transform, "Model");
        if (witchModel != null) witchModel.transform.localRotation = Quaternion.identity;
        WitchPresenter witchComp = witch.AddComponent<WitchPresenter>();

        manager.EditorBind(anchors, deviceComp, rocketComp, witchComp);
        BindSceneReferences(manager, map);
        return $"[EscapeMapSetup] {map}: device {Fmt(device.Value)}, rocket {Fmt(rocket.Value)} ({Dist(device.Value, rocket.Value):F0} m apart), " +
               $"chest slots {chests.Count}/{ChestSlotCount} from {chestCandidates.Count} candidates, witch {Fmt(witch.transform.position)}";
    }

    // 맵의 주인공인 장치는 정해진 자리에 놓는다(EscapeVisualPlan.md §5): 캔디숲 케이크 = 광장 한가운데(랜드마크를 비운 자리),
    // 베이커리 = 마법 오븐 랜드마크 그 자체, 진저브레드 = 시계탑(지하 유적까지 시계탑 기준 좌표로 만들어져 있다).
    private static Vector3? FixedDeviceSpot(string map, out Vector3? forward)
    {
        forward = null;
        if (map == "CandyForest")
            return TryWalkable(0f, 0f, out float y, out _) ? new Vector3(0f, y, 0f) : (Vector3?)null;
        string landmark = map == "HauntedBakery" ? "HAU_Landmark_MagicOven" : map == "GingerbreadVillage" ? "GIN_Landmark_ClockTower" : null;
        if (landmark == null) return null;
        GameObject found = GameObject.Find(landmark);
        if (found == null) return null;
        forward = found.transform.forward;
        return found.transform.position;
    }

    // 씬 이름·오브젝트 이름 규칙에 기대지 않도록 레시피와 오븐 문을 직접 연결한다(AuditFixPlan.md F-21).
    // 장치 연출(OvenSequence)은 장치가 만들어진 뒤에 붙으므로 Apply 끝에서도 한 번 더 부른다.
    public static void BindSceneReferences(EscapeManager manager, string map)
    {
        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        var so = new SerializedObject(manager);
        so.FindProperty("recipeOverride").objectReferenceValue = catalog != null ? catalog.RecipeFor(map) : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        foreach (OvenSequence oven in manager.GetComponentsInChildren<OvenSequence>(true))
        {
            GameObject door = GameObject.Find("HAU_MagicOven_Door");
            var ovenSo = new SerializedObject(oven);
            ovenSo.FindProperty("mapDoor").objectReferenceValue = door != null ? door.transform : null;
            ovenSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // 맵별 탈출 연출(EscapeSequence)을 장치에 붙인다.
    private static void AddSequence(GameObject device, EscapeExitKind kind)
    {
        switch (kind)
        {
            case EscapeExitKind.CakeRocket: device.AddComponent<CakeRocketSequence>(); break;
            case EscapeExitKind.RollerCoaster: device.AddComponent<CoasterSequence>(); break;
            case EscapeExitKind.ChocolateTrain: device.AddComponent<TrainSequence>(); break;
            case EscapeExitKind.BakeryMachine: device.AddComponent<OvenSequence>(); break;
            case EscapeExitKind.RuneAltar: device.AddComponent<RuneAltarSequence>(); break;
        }
    }

    // 롤러코스터(§5.2)·기차(§5.4)는 레일이 맵 밖으로 나가야 한다: 외곽 벽까지 CoasterEdgeMin~Max m인 평평한 곳에서 벽 쪽을 보고,
    // 승강장 둘레(괴물 통로 포함)와 레일이 지나갈 길(그 자리의 레일 높이 기준)이 비어 있는 곳. 벽 가까이·가장자리 가운데를 고른다.
    private static Vector3? VehicleSpot(List<Vector3> ground, List<Vector3> avoid, bool train, out Vector3 forward)
    {
        forward = Vector3.forward;
        Vector3? best = null;
        float bestScore = float.MaxValue;
        foreach (Vector3 dir in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
            foreach (Vector3 p in ground)
            {
                float edge = MapCompactor.NewHalf - Vector3.Dot(p, dir);
                if (edge < (train ? TrainEdgeMin : CoasterEdgeMin) || edge > CoasterEdgeMax) continue;
                float lateral = Mathf.Abs(Vector3.Dot(p, Vector3.Cross(Vector3.up, dir)));
                float score = Mathf.Abs(edge - 22f) + 0.15f * lateral;
                if (score >= bestScore || !Far(p, avoid, 15f) || !VehicleFits(p, dir, edge, train)) continue;
                best = p;
                bestScore = score;
                forward = dir;
            }
        return best;
    }

    private static bool VehicleFits(Vector3 p, Vector3 dir, float edge, bool train)
    {
        Quaternion rot = Quaternion.LookRotation(dir);
        // 승강장: 모델 기준 롤러코스터 x -4.6~2.3, z -6~9 / 기차 x -5.5~1.1, z -10~10(발판은 왼쪽)에 괴물 통로 2 m를 더한 상자.
        // 바닥도 평평해야 한다.
        Vector3 center = train ? new Vector3(-2.25f, 3.3f, -1f) : new Vector3(-1.15f, 3.3f, 1.5f);
        Vector3 half = train ? new Vector3(5.25f, 3f, 11f) : new Vector3(5.5f, 3f, 9.5f);
        if (Blocked(p + rot * center, half, rot)) return false;
        foreach (float x in train ? new[] { -5.5f, 0f, 1.2f } : new[] { -4.6f, 0f, 1.2f })
            foreach (float z in train ? new[] { -10f, 0f, 10f } : new[] { -6f, 0f, 9f })
            {
                Vector3 q = p + rot * new Vector3(x, 0f, z);
                if (!TryWalkable(q.x, q.z, out float y, out _) || Mathf.Abs(y - p.y) > 0.5f) return false;
            }
        // 기차: 레일은 터널 입구까지 비어 있어야 하고, 터널(입구에서 10 m)은 외곽 벽 속으로 들어가도 된다
        if (train)
        {
            float portal = TrainPortal(edge);
            for (float s = 11f; s < portal; s += 2f)
                if (Blocked(p + dir * s + Vector3.up * 1.9f, new Vector3(1.6f, 1.6f, 1f), rot)) return false;
            return !Blocked(p + dir * (portal + 5f) + Vector3.up * 3.1f, new Vector3(3.4f, 2.8f, 5f), rot, allowWalls: true);
        }
        // 롤러코스터: 벽을 넘을 때까지 레일 높이에서 차가 지나갈 단면이 비어 있어야 한다
        for (float s = 10f; s < edge + 4f; s += 2f)
            if (Blocked(p + dir * s + Vector3.up * (CoasterHeight(s) + 1.9f), new Vector3(1.6f, 1.6f, 1f), rot)) return false;
        return true;
    }

    // escape_devices.py coaster_height(-s)와 같은 레일 높이(승강장 → 오르막 → 꼭대기 → 내리막 → 직선).
    private static float CoasterHeight(float s)
    {
        if (s < 9f) return 0f;
        if (s < 37f) return 14f * Mathf.SmoothStep(0f, 1f, (s - 9f) / 28f);
        if (s < 55f) return 14f + 0.6f * Mathf.Sin(Mathf.PI * (s - 37f) / 18f);
        if (s < 70f) return 14f - 10f * Mathf.SmoothStep(0f, 1f, (s - 55f) / 15f);
        return 4f;
    }

    // 기차 터널 입구 거리(장치 기준 앞쪽): 외곽 벽 4 m 앞. 터널 몸통(10 m)이 벽 속으로 들어간다.
    private static float TrainPortal(float edge) => edge - 4f;

    private static bool Blocked(Vector3 center, Vector3 half, Quaternion rot, bool allowWalls = false)
    {
        foreach (Collider c in Physics.OverlapBox(center, half, rot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (c.name.Contains("Terrain") || c.name.Contains("Ground") || c.name.Contains("COL_Boundary")) continue;
            if (allowWalls && c.name.Contains("Wall")) continue;
            return true;
        }
        return false;
    }

    // 기차(§5.4): 레일을 터널 입구까지 늘리고 터널을 놓는다(터널은 통째로 막힌 상자 충돌체).
    private static void FitTrainTunnel(Transform device, Vector3 forward)
    {
        Transform tunnel = device.Find("Tunnel"), track = device.Find("Track");
        if (tunnel == null) return;
        float edge = MapCompactor.NewHalf - Vector3.Dot(device.position, forward);
        float portal = TrainPortal(edge);
        tunnel.localPosition = new Vector3(0f, 0f, portal);
        if (track != null) track.localScale = new Vector3(1f, 1f, Mathf.Max(0.1f, (portal - track.localPosition.z) / 20f));
        var box = tunnel.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 2.8f, 5f);
        box.size = new Vector3(6.4f, 5.6f, 10f);
    }

    // ---------------- GingerbreadVillage underground (EscapeVisualPlan.md §5.5) ----------------

    private const float TowerShaftRadius = 5f;   // escape_devices.py GIN_R1
    private const float UndergroundKillY = -30f;

    // 시계탑 아래 땅에 나선 경사로 구멍을 내고(걷는 지형 메시에서 반지름 5 m 안의 삼각형을 지운 사본), 추락 처리 높이를 지하보다 아래로 내린다.
    // 지형 밑의 맵 전체 바닥판(큰 삼각형 몇 개, 지형 아래 약 -2.4 m)은 지하 터널·홀을 가로지르므로, 같은 높이의 격자로 다시 깔되
    // 지하 구역(장치 Body의 수평 범위) 칸은 비운다. 다시 실행해도 같은 결과다.
    private static void OpenUnderground(string map, Transform device)
    {
        Vector3 c = device.position;
        Transform bodyPart = device.Find("Body");
        Bounds under = bodyPart != null && bodyPart.GetComponent<Renderer>() != null ? bodyPart.GetComponent<Renderer>().bounds : new Bounds(c, Vector3.one * 12f);
        Rect footprint = Rect.MinMaxRect(under.min.x - 0.5f, under.min.z - 0.5f, under.max.x + 0.5f, under.max.z + 0.5f);
        foreach (MeshCollider col in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
        {
            if (!col.name.Contains("Terrain_Walkable") || col.sharedMesh == null) continue;
            MeshFilter filter = col.GetComponent<MeshFilter>();
            Mesh source = col.sharedMesh;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (sourcePath.EndsWith("_Underground.asset")) // 다시 실행: 항상 축소된 원본 지형에서 새로 자른다
            {
                var original = AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath.Replace("_Underground.asset", ".asset"));
                if (original != null) source = original;
            }
            Mesh cut = CutHole(source, col.transform, c, TowerShaftRadius, footprint);
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets")) folder = $"Assets/Maps/{map}/Compact";
            string path = $"{folder}/{col.name}_Underground.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(cut, path);
            else { EditorUtility.CopySerialized(cut, existing); cut = existing; } // 다시 실행: 같은 에셋을 갱신
            col.sharedMesh = null; // 같은 메시 에셋을 고쳐 쓴 경우에도 충돌체가 다시 굽도록
            col.sharedMesh = cut;
            if (filter != null) filter.sharedMesh = cut;
        }
        AssetDatabase.SaveAssets();

        var kill = GameObject.Find("VoidKillZone");
        if (kill != null) kill.transform.position = new Vector3(kill.transform.position.x, UndergroundKillY, kill.transform.position.z);

        // 시계탑은 문으로 들어가는 속 빈 건물이다: convex 껍질이면 문까지 막히므로 오목 충돌체로 둔다
        GameObject tower = GameObject.Find("GIN_Landmark_ClockTower");
        if (tower != null)
            foreach (MeshCollider mc in tower.GetComponentsInChildren<MeshCollider>(true)) mc.convex = false;
    }

    private const float SlabCell = 8f;          // 다시 까는 바닥판 격자 크기(m)
    private const float SlabMinEdge = 40f;      // 이보다 긴 변을 가진 수평 삼각형 = 맵 전체 바닥판(맵을 가로지르는 긴 띠)

    private static Mesh CutHole(Mesh source, Transform t, Vector3 center, float radius, Rect footprint)
    {
        var mesh = Object.Instantiate(source);
        mesh.name = source.name.Replace("(Clone)", string.Empty);
        var v = new List<Vector3>(source.vertices);
        var n = new List<Vector3>(source.normals);
        var uv = new List<Vector2>(source.uv);
        var inside = new bool[v.Count];
        for (int i = 0; i < v.Count; i++)
        {
            Vector3 w = t.TransformPoint(v[i]);
            inside[i] = new Vector2(w.x - center.x, w.z - center.z).magnitude < radius;
        }
        var subs = new List<int>[source.subMeshCount];
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] tris = source.GetTriangles(sub);
            var kept = new List<int>(tris.Length);
            var slabUp = new List<int>();
            var slabDown = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], cc = tris[i + 2];
                if (inside[a] || inside[b] || inside[cc]) continue;
                Vector3 wa = t.TransformPoint(v[a]), wb = t.TransformPoint(v[b]), wc = t.TransformPoint(v[cc]);
                bool flat = Mathf.Abs(wa.y - wb.y) < 0.01f && Mathf.Abs(wa.y - wc.y) < 0.01f;
                float longest = Mathf.Max((wb - wa).magnitude, Mathf.Max((wc - wb).magnitude, (wa - wc).magnitude));
                if (flat && longest > SlabMinEdge)
                {
                    List<int> slab = Vector3.Cross(wb - wa, wc - wa).y > 0f ? slabUp : slabDown;
                    slab.Add(a); slab.Add(b); slab.Add(cc);
                    continue;
                }
                kept.Add(a); kept.Add(b); kept.Add(cc);
            }
            if (slabUp.Count > 0) RelaySlab(slabUp, true, v, n, uv, kept, t, footprint);
            if (slabDown.Count > 0) RelaySlab(slabDown, false, v, n, uv, kept, t, footprint);
            subs[sub] = kept;
        }
        mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : mesh.indexFormat;
        mesh.SetVertices(v);
        if (n.Count == v.Count) mesh.SetNormals(n);
        if (uv.Count == v.Count) mesh.SetUVs(0, uv);
        for (int sub = 0; sub < subs.Length; sub++) mesh.SetTriangles(subs[sub], sub);
        mesh.RecalculateBounds();
        return mesh;
    }

    // 바닥판 삼각형들을 같은 높이·같은 면 방향(up = 위를 봄)의 격자 칸으로 바꿔 깐다(지하 구역 칸은 뺀다).
    private static void RelaySlab(List<int> slab, bool up, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> kept, Transform t, Rect footprint)
    {
        Vector3 first = t.TransformPoint(v[slab[0]]);
        bool hasNormals = n.Count == v.Count, hasUv = uv.Count == v.Count;
        Vector3 normal = hasNormals ? n[slab[0]] : Vector3.up;
        Vector2 uv0 = hasUv ? uv[slab[0]] : Vector2.zero;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (int i in slab)
        {
            Vector3 w = t.TransformPoint(v[i]);
            minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x); minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
        }
        for (float x = minX; x < maxX - 1e-3f; x += SlabCell)
            for (float z = minZ; z < maxZ - 1e-3f; z += SlabCell)
            {
                float x1 = Mathf.Min(maxX, x + SlabCell), z1 = Mathf.Min(maxZ, z + SlabCell);
                if (footprint.Overlaps(Rect.MinMaxRect(x, z, x1, z1))) continue;
                int b = v.Count;
                foreach (Vector3 w in new[] { new Vector3(x, first.y, z), new Vector3(x1, first.y, z), new Vector3(x1, first.y, z1), new Vector3(x, first.y, z1) })
                {
                    v.Add(t.InverseTransformPoint(w));
                    if (hasNormals) n.Add(normal);
                    if (hasUv) uv.Add(uv0);
                }
                if (up) kept.AddRange(new[] { b, b + 3, b + 2, b, b + 2, b + 1 });
                else kept.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
    }

    // ---------------- sampling ----------------

    private static List<Vector3> SampleWalkable()
    {
        var list = new List<Vector3>();
        float half = MapCompactor.NewHalf - 5f;
        for (float x = -half; x <= half; x += SampleStep)
        for (float z = -half; z <= half; z += SampleStep)
        {
            if (!TryWalkable(x, z, out float y, out Vector3 normal)) continue;
            if (Vector3.Angle(normal, Vector3.up) > 12f) continue; // 평평한 곳만
            list.Add(new Vector3(x, y, z));
        }
        return list;
    }

    private static bool TryWalkable(float x, float z, out float y, out Vector3 normal)
    {
        y = 0f;
        normal = Vector3.up;
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 600f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return false;
        RaycastHit top = hits.OrderByDescending(h => h.point.y).First();
        if (!top.collider.name.Contains("Terrain_Walkable")) return false; // 지붕·소품 위가 아닌 땅
        y = top.point.y;
        normal = top.normal;
        return true;
    }

    // 주변 반지름 r 안(원기둥, 발목 0.3 m부터 4 m 높이까지)에 땅 말고 다른 충돌체가 없는지. 반지름이 커도 땅 가까이를 제대로 본다.
    private static bool IsClear(Vector3 p, float r)
    {
        const float bottom = 0.3f, top = 4f;
        Vector3 center = p + Vector3.up * ((bottom + top) * 0.5f);
        foreach (Collider c in Physics.OverlapBox(center, new Vector3(r, (top - bottom) * 0.5f, r), Quaternion.identity,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (c.name.Contains("Terrain") || c.name.Contains("Ground")) continue;
            Vector3 nearest = c.bounds.ClosestPoint(center); // 축 정렬 상자 기준이라 조금 보수적이다
            if (Flat(nearest - p).magnitude <= r) return false;
        }
        return true;
    }

    private static bool Far(Vector3 p, List<Vector3> others, float min) => others.All(o => Dist(p, o) >= min);
    private static float Dist(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    private static string Fmt(Vector3 v) => $"({v.x:F0}, {v.y:F1}, {v.z:F0})";

    // ---------------- placeholder visuals ----------------

    private static EscapeDevice BuildDevice(Transform parent, string map, EscapeExitKind kind, Vector3 pos, Vector3 faceToward, Vector3? forward)
    {
        GameObject model = InstantiateModel($"ESC_{map}", parent, $"ESC_{map}_Root");
        if (model != null)
        {
            model.transform.position = pos;
            // 앞(타는 곳·칸)이 쿠키 출발 지점 쪽을 보게 한다
            // (탈것처럼 나가는 방향이 정해진 장치는 그 방향을 본다)
            Vector3 face = forward ?? Flat(faceToward - pos);
            if (face.sqrMagnitude > 1e-4f) model.transform.rotation = Quaternion.LookRotation(face.normalized);
            foreach (string hidden in new[] { "ESC_Glow", "Cake_Cracks", "Oven_Light", "Portal_Ring", "Portal_Swirl" })
            {
                Transform t = model.transform.Find(hidden);
                if (t != null) t.gameObject.SetActive(false); // 완성 연출이 켠다
            }
            foreach (Transform t in model.transform)
                if (t.name.StartsWith("Cake_Shard_") || t.name.StartsWith("Bulb_")) t.gameObject.SetActive(false);
            // 케이크가 부서진 뒤에는 로켓이 막는다. 롤러코스터 차는 승강장에 있는 동안 막는다(레일 오르막은 그림만).
            AddMeshColliders(model, "Body", "Ramp", "Cake_Intact", "Cake_Rocket", "Car_0", "Car_1", "Car_2", "Loco", "Coach");
            AddSequence(model, kind);
            return model.AddComponent<EscapeDevice>();
        }

        var root = new GameObject($"ESC_{map}_Root");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        Shape(root.transform, "ESC_Base", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(2.6f, 0.25f, 2.6f), Mat("ESC_Base", new Color(0.35f, 0.3f, 0.35f)), true);
        switch (kind)
        {
            case EscapeExitKind.CakeRocket:
                var cake = new GameObject("ESC_Cake_Intact").transform;
                cake.SetParent(root.transform, false);
                Shape(cake, "Layer1", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(2.2f, 0.4f, 2.2f), Mat("ESC_Cake", new Color(1f, 0.85f, 0.9f)), true);
                Shape(cake, "Layer2", PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(1.5f, 0.3f, 1.5f), Mat("ESC_CakeCream", new Color(1f, 0.6f, 0.75f)), false);
                var cakeRocket = new GameObject("ESC_Cake_Rocket").transform;
                cakeRocket.SetParent(root.transform, false);
                Shape(cakeRocket, "Body", PrimitiveType.Capsule, new Vector3(0f, 2.2f, 0f), new Vector3(1f, 1.8f, 1f), Mat("ESC_CakeRocket", new Color(0.95f, 0.95f, 1f)), false);
                cakeRocket.gameObject.SetActive(false);
                break;
            case EscapeExitKind.RuneAltar:
                Shape(root.transform, "ESC_Altar", PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f), new Vector3(1.6f, 0.6f, 1.6f), Mat("ESC_Stone", new Color(0.55f, 0.5f, 0.45f)), true);
                break;
            case EscapeExitKind.ChocolateTrain:
                Shape(root.transform, "ESC_Train", PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(1.8f, 1.6f, 3.2f), Mat("ESC_Chocolate", new Color(0.35f, 0.2f, 0.1f)), true);
                break;
            case EscapeExitKind.BakeryMachine:
                Shape(root.transform, "ESC_Machine", PrimitiveType.Cube, new Vector3(0f, 1.4f, 0f), new Vector3(2f, 2.2f, 1.6f), Mat("ESC_Iron", new Color(0.45f, 0.45f, 0.5f)), true);
                break;
            default:
                Shape(root.transform, "ESC_Cart", PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(1.6f, 1.2f, 2.6f), Mat("ESC_Cart", new Color(0.8f, 0.15f, 0.3f)), true);
                break;
        }
        return root.AddComponent<EscapeDevice>();
    }

    private static SpyRocket BuildRocket(Transform parent, string map, Vector3 pos, Vector3 device)
    {
        GameObject model = InstantiateModel($"SPY_Rocket_{map}", parent, $"SPY_Rocket_{map}_Root");
        if (model != null)
        {
            model.transform.position = pos;
            Vector3 toDevice = Flat(device - pos);
            if (toDevice.sqrMagnitude > 0.01f) model.transform.rotation = Quaternion.LookRotation(toDevice.normalized); // 끼우는 칸(앞면)이 맵 안쪽을 본다
            var capsule = model.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 2.5f, 0f);
            capsule.radius = 1f;
            capsule.height = 5f;
            return model.AddComponent<SpyRocket>();
        }

        var root = new GameObject($"SPY_Rocket_{map}_Root");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        Material body = Mat("SPY_Rocket", new Color(0.25f, 0.25f, 0.3f));
        Shape(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 2.2f, 0f), new Vector3(1.4f, 2f, 1.4f), body, true);
        Shape(root.transform, "FinA", PrimitiveType.Cube, new Vector3(0.9f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.9f), Mat("SPY_Fin", new Color(0.7f, 0.1f, 0.15f)), false);
        Shape(root.transform, "FinB", PrimitiveType.Cube, new Vector3(-0.9f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.9f), Mat("SPY_Fin", new Color(0.7f, 0.1f, 0.15f)), false);
        for (int i = 0; i < 2; i++)
        {
            var slot = new GameObject($"Slot_{i:00}").transform;
            slot.SetParent(root.transform, false);
            slot.localPosition = new Vector3(i == 0 ? -0.6f : 0.6f, 1.6f, 0.75f);
        }
        return root.AddComponent<SpyRocket>();
    }

    private static Transform BuildChest(Transform parent, int index, Vector3 pos, Vector3 device)
    {
        Vector3 face = Flat(device - pos);
        Quaternion facing = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face.normalized) : Quaternion.identity;
        GameObject model = InstantiateModel("CHEST", parent, $"CHEST_Slot_{index:00}");
        if (model != null)
        {
            model.transform.SetPositionAndRotation(pos, facing);
            var collider = model.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.size = new Vector3(1f, 0.7f, 0.7f);
            model.AddComponent<MaterialChest>();
            return model.transform;
        }

        var root = new GameObject($"CHEST_Slot_{index:00}");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        root.transform.rotation = facing;
        Material wood = Mat("CHEST_Wood", new Color(0.55f, 0.33f, 0.18f));
        Shape(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 0.7f), wood, false);
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.35f, 0f);
        box.size = new Vector3(1f, 0.7f, 0.7f);
        var lid = new GameObject("Lid").transform;
        lid.SetParent(root.transform, false);
        lid.localPosition = new Vector3(0f, 0.6f, -0.35f);
        Shape(lid, "LidMesh", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0.35f), new Vector3(1.04f, 0.12f, 0.74f), Mat("CHEST_Lid", new Color(0.45f, 0.26f, 0.14f)), false);
        root.AddComponent<MaterialChest>();
        return root.transform;
    }

    // ---------------- Blender models ----------------

    private static GameObject InstantiateModel(string unit, Transform parent, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{EscapeModelImportPostprocessor.ModelFolder}/{unit}.fbx");
        if (asset == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = name;
        return go;
    }

    // 장치 모양 그대로의 충돌체(경사진 받침은 괴물·쿠키 모두 오를 수 있고, 레일처럼 낮은 부분은 넘어갈 수 있다).
    private static void AddMeshColliders(GameObject root, params string[] parts)
    {
        foreach (string part in parts)
        {
            Transform t = root.transform.Find(part);
            if (t == null) continue;
            foreach (MeshFilter filter in t.GetComponentsInChildren<MeshFilter>(true))
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
    }

    private static void Shape(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material material, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    private static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/09. Environment/Escape")) AssetDatabase.CreateFolder("Assets/09. Environment", "Escape");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/09. Environment/Escape", "Materials");
        string path = $"{MaterialFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
