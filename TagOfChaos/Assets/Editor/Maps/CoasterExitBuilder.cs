using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 저주받은 놀이공원 롤러코스터 탈출구(Request1009Plan.md §30, 단계 R2).
// Blender에서 조립한 입구(괴물 입 성 — 앞 계단 → 입 안 통로 → 뒤 계단)와 트랙(빨간 레일 + 보라 지지대)을
// Game_CursedCandyCarnival 북쪽 큰길 끝에 놓고, 예전 롤러코스터 탈출 장치(예전 방식, 스크립트 없음)를 지운다.
// - 탈출구 Exit_N(EscapeGate): 앞 계단 아래 창살 문 Door_N이 열리면 땅속으로 내려가고, 뒤 계단 아래 ExitTrigger_N에서 탈출.
// - 걷는 충돌: 계단 경사(속이 찬 쐐기 — 계단 밑에 숨을 수 없게)·통로 바닥·건물 덩어리(통로만 비움).
// - 임시 재료 꽂는 곳 Building_Q1~Q4(QuadrantBuilding): 입구 앞 빈 자리. 나중에 캔디숲처럼 사분면으로 옮긴다.
// - EscapeManager에는 건물·탈출구만 연결한다(상자·로켓·마녀는 그대로).
public static class CoasterExitBuilder
{
    public const string MapName = "CursedCandyCarnival";
    public const string Folder = "Assets/Maps/CursedCandyCarnival/Models/Coaster";
    public const string EntrancePath = Folder + "/CUR_CoasterEntrance.fbx";
    public const string TrackPath = Folder + "/CUR_CoasterTrack.fbx";
    private const string MetaPath = Folder + "/coaster.json";
    private const string RampMeshPath = Folder + "/CUR_CoasterRamp.asset";
    public const string RootName = "Coaster";
    public const string SocketsName = "TempSockets";
    private const string LegacyDevice = "ESC_CursedCandyCarnival_Root";
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";

    public static readonly Vector3 EntranceCenter = new Vector3(0f, 0f, 55f); // 앞(계단)이 맵 가운데(−Z)를 본다
    private const float TrackZ = 76f;          // 맵 경계(71.2) 밖 배경
    private const float BodyHeight = 14f;      // 건물 덩어리 충돌 높이(탑은 겉모습만)
    private const float FacadeDepth = 2.4f;    // 입 앞면이 건물 중심에서 앞으로 나온 거리(Blender 재기, m)
    private const float SocketClearance = 2.2f, SocketSpacing = 4.5f;
    private const float ApproachLength = 5f;
    private const float SpawnClearance = 4f;   // 괴물·쿠키 출발 자리 둘레는 비워 둔다
    private static readonly string[] SpawnNames = { "MonsterSpawnPos", "PlayerSpawnPos" };
    private static List<Vector3> spawns = new List<Vector3>();
    private static readonly string[] Categories = { "MainStructures", "GameplayProps", "Decoration" };

    [Serializable] private class Meta { public EntranceMeta entrance; }
    [Serializable] private class EntranceMeta { public float floor, pass_half, pass_height, stair_half, front_foot, mouth, back_wall, back_foot, half_x; }

    [MenuItem(MenuRoot + "Build Coaster Exit (Cursed Candy Carnival)")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(MapName), OpenSceneMode.Single);
        Debug.Log("[CoasterExitBuilder] " + Apply(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static string Apply(Scene scene)
    {
        var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(MetaPath));
        EntranceMeta e = meta.entrance;
        GameObject entranceModel = Import(EntrancePath), trackModel = Import(TrackPath);
        if (entranceModel == null || trackModel == null) return "models missing";
        Transform map = scene.GetRootGameObjects().First(g => g.name == "Map").transform;
        GameObject escape = scene.GetRootGameObjects().First(g => g.name == EscapeMapSetup.RootName);
        var report = new List<string>();

        // 예전 장치·예전 결과 지우기
        Transform legacy = escape.transform.Find(LegacyDevice);
        if (legacy != null) { Object.DestroyImmediate(legacy.gameObject); report.Add("legacy coaster device removed"); }
        foreach (string old in new[] { RootName, SocketsName, "Exits" })
        {
            Transform t = map.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        Physics.SyncTransforms();

        // 입구가 설 자리(앞 계단 끝 ~ 뒤 계단 끝)에 걸리는 장식은 지우고 상자는 옮긴다
        Vector3 c = EntranceCenter;
        var footprint = new Bounds(new Vector3(c.x, 6f, c.z + (e.front_foot + e.back_foot) / 2f), new Vector3(e.half_x * 2f + 1f, 12f, e.back_foot - e.front_foot + 1f));
        var removed = new List<string>();
        foreach (Transform cat in Categories.Select(n => map.Find(n)).Where(t => t != null))
            foreach (Transform child in cat.Cast<Transform>().ToList())
                if (child.GetComponentsInChildren<Collider>(true).Any(col => col.bounds.Intersects(footprint))) { removed.Add(child.name); Object.DestroyImmediate(child.gameObject); }
        if (removed.Count > 0) report.Add("cleared " + string.Join(", ", removed));
        Physics.SyncTransforms();

        Transform root = Group(map, RootName);
        var entrance = (GameObject)PrefabUtility.InstantiatePrefab(entranceModel, root);
        entrance.name = "CoasterEntrance";
        entrance.transform.SetPositionAndRotation(c, Quaternion.identity); // FBX가 축 변환을 구워 넣어 앞(계단)이 이미 −Z
        foreach (MeshRenderer r in entrance.GetComponentsInChildren<MeshRenderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select((m, i) => i == 0 ? EntranceMaterial() : TunnelMaterial()).ToArray();
        var track = (GameObject)PrefabUtility.InstantiatePrefab(trackModel, root);
        track.name = "CoasterTrack";
        track.transform.SetPositionAndRotation(new Vector3(c.x, 0f, TrackZ), Quaternion.identity);
        foreach (MeshRenderer r in track.GetComponentsInChildren<MeshRenderer>(true))
            r.sharedMaterial = TrackMaterial(r.name.Contains("Rail") ? "CoasterRail" : "CoasterSupports", r.name.Contains("Rail") ? new Color(0.86f, 0.07f, 0.12f) : new Color(0.33f, 0.18f, 0.82f));

        BuildColliders(Group(root, "Colliders"), e, c);
        BuildBackyardFences(Group(root, "BackyardFence"), e, c);
        EscapeGate gate = BuildExit(Group(map, "Exits"), e, c);
        Physics.SyncTransforms();
        spawns = scene.GetRootGameObjects().Where(g => SpawnNames.Contains(g.name)).Select(g => g.transform.position).ToList();
        // 상자·받침은 입구 자리와 앞 계단으로 오르는 길(계단 앞 ApproachLength)·출발 자리를 비워 둔다
        var keepOut = new Bounds(footprint.center + Vector3.back * ApproachLength / 2f, footprint.size + Vector3.forward * ApproachLength);
        report.Add(MoveChests(escape.transform, keepOut));
        QuadrantBuilding[] buildings = BuildSockets(Group(map, SocketsName), c + Vector3.forward * e.front_foot, keepOut, out string socketReport);
        report.Add(socketReport);

        var manager = escape.GetComponent<EscapeManager>();
        var so = new SerializedObject(manager);
        SetArray(so.FindProperty("buildings"), buildings);
        SetArray(so.FindProperty("exits"), new Object[] { gate });
        so.ApplyModifiedPropertiesWithoutUndo();
        report.Add($"manager: buildings {buildings.Count(b => b != null)}, exits 1");
        return string.Join(" | ", report);
    }

    // ---------------- colliders ----------------

    // 입구 중심 기준(앞 = −Z). Blender y(앞 −) → 월드 z 그대로.
    private static void BuildColliders(Transform parent, EntranceMeta e, Vector3 c)
    {
        float facade = c.z - FacadeDepth, mouth = c.z + e.mouth, back = c.z + e.back_wall, half = e.pass_half, top = e.floor + e.pass_height;
        Box(parent, "COL_BodyLeft", new Vector3(c.x - (e.half_x + half) / 2f, BodyHeight / 2f, (facade + back) / 2f), new Vector3(e.half_x - half, BodyHeight, back - facade));
        Box(parent, "COL_BodyRight", new Vector3(c.x + (e.half_x + half) / 2f, BodyHeight / 2f, (facade + back) / 2f), new Vector3(e.half_x - half, BodyHeight, back - facade));
        Box(parent, "COL_PassRoof", new Vector3(c.x, (top + BodyHeight) / 2f, (facade + back) / 2f), new Vector3(half * 2f, BodyHeight - top, back - facade));
        Box(parent, "COL_PassFloor", new Vector3(c.x, e.floor / 2f, (mouth + back) / 2f), new Vector3(half * 2f, e.floor, back - mouth));
        // 계단: 땅 → 통로 바닥(속이 찬 쐐기). 뒤 계단은 뒤 벽에서 뒤쪽으로 내려간다
        Ramp(parent, "COL_StairsFront", new Vector3(c.x, 0f, c.z + e.front_foot), 0f, e.stair_half * 2f, e.floor, e.mouth - e.front_foot);
        Ramp(parent, "COL_StairsBack", new Vector3(c.x, 0f, c.z + e.back_foot), 180f, e.stair_half * 2f, e.floor, e.back_foot - e.back_wall);
        // 앞 계단 난간 자리: 옆에서 계단으로 뛰어오르지 못하게(계단 아래 문으로만 오른다)
        float foot = c.z + e.front_foot;
        foreach (float side in new[] { -1f, 1f })
            Box(parent, side < 0 ? "COL_StairRailL" : "COL_StairRailR", new Vector3(c.x + side * (e.stair_half + 0.15f), StairWallHeight / 2f, (foot + facade) / 2f),
                new Vector3(0.3f, StairWallHeight, facade - foot));
    }

    private const float StairWallHeight = 7f; // 통로 바닥(4 m) + 쿠키가 뛰어 넘지 못할 높이

    // 뒤 계단 마당은 통로로만 — 건물 뒤 모서리에서 맵 경계벽까지 쇠창살 울타리(옆으로 돌아 뒤로 가지 못하게)
    private const float BoundaryZ = 71.4f, FenceHeight = 3.6f;

    private static void BuildBackyardFences(Transform parent, EntranceMeta e, Vector3 c)
    {
        Material iron = PlainMaterial("M_CUR_CoasterGate", new Color(0.12f, 0.08f, 0.14f), Color.black, 0.6f);
        float z0 = c.z + e.back_wall - 1.5f, x = e.half_x + 0.3f;
        foreach (float side in new[] { -1f, 1f })
        {
            var fence = new GameObject(side < 0 ? "Fence_L" : "Fence_R").transform;
            fence.SetParent(parent, false);
            fence.position = new Vector3(c.x + side * x, 0f, (z0 + BoundaryZ) / 2f);
            float len = BoundaryZ - z0;
            var box = fence.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, FenceHeight / 2f, 0f);
            box.size = new Vector3(0.35f, FenceHeight, len);
            int posts = Mathf.CeilToInt(len / 1.2f);
            for (int i = 0; i <= posts; i++)
                Bar(fence, $"Post_{i}", new Vector3(0f, FenceHeight / 2f, -len / 2f + i * len / posts), new Vector3(0.12f, FenceHeight, 0.12f), iron);
            Bar(fence, "Rail_Top", new Vector3(0f, FenceHeight - 0.3f, 0f), new Vector3(0.1f, 0.12f, len), iron);
            Bar(fence, "Rail_Low", new Vector3(0f, 0.5f, 0f), new Vector3(0.1f, 0.12f, len), iron);
        }
    }

    private static void Box(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = center;
        t.gameObject.AddComponent<BoxCollider>().size = size;
        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
    }

    // 단위 쐐기(폭 x −0.5~0.5, 길이 z 0~1에서 높이 y 0 → 1로 올라감)를 늘려 쓴다
    private static void Ramp(Transform parent, string name, Vector3 foot, float yaw, float width, float height, float run)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(foot, Quaternion.Euler(0f, yaw, 0f));
        t.localScale = new Vector3(width, height, run);
        var mc = t.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = RampMesh();
        mc.convex = true;
    }

    private static Mesh RampMesh()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RampMeshPath);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "CUR_CoasterRamp" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f),
            new Vector3(-0.5f, 1f, 1f), new Vector3(0.5f, 1f, 1f),
        };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 4, 1, 5, 4, 2, 4, 3, 3, 4, 5, 0, 4, 2, 1, 3, 5 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, RampMeshPath);
        return mesh;
    }

    // ---------------- exit ----------------

    private const float GateHeight = 3.6f; // 쿠키 점프(1.8 m)로 넘지 못한다

    private static EscapeGate BuildExit(Transform exits, EntranceMeta e, Vector3 c)
    {
        var exit = new GameObject("Exit_N").transform;
        exit.SetParent(exits, false);
        exit.SetPositionAndRotation(new Vector3(c.x, 0f, c.z), Quaternion.identity);

        // 앞 계단 아래 창살 문: 닫혀 있으면 계단에 오르지 못하고(괴물은 32° 계단을 못 올라 계단 위가 쿠키만의 피난처가 되지 않게),
        // 열리면 땅속으로 내려간다(EscapeGate)
        float width = e.stair_half * 2f + 0.6f;
        var door = new GameObject("Door_N").transform;
        door.SetParent(exit, false);
        door.position = new Vector3(c.x, 0f, c.z + e.front_foot - 0.4f);
        var col = door.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, GateHeight / 2f, 0f);
        col.size = new Vector3(width, GateHeight, 0.4f);
        Material iron = PlainMaterial("M_CUR_CoasterGate", new Color(0.12f, 0.08f, 0.14f), Color.black, 0.6f);
        int bars = 13;
        for (int i = 0; i < bars; i++)
            Bar(door, $"Bar_{i}", new Vector3(-width / 2f + 0.2f + i * (width - 0.4f) / (bars - 1), GateHeight / 2f, 0f), new Vector3(0.14f, GateHeight, 0.14f), iron);
        Bar(door, "Rail_Top", new Vector3(0f, GateHeight - 0.25f, 0f), new Vector3(width, 0.16f, 0.18f), iron);
        Bar(door, "Rail_Mid", new Vector3(0f, GateHeight * 0.45f, 0f), new Vector3(width, 0.16f, 0.18f), iron);

        // 열렸을 때: 통로 양옆 초록 빛줄 + 초록 빛
        var look = new GameObject("ExitOpenLook_N").transform;
        look.SetParent(exit, false);
        Material glow = PlainMaterial("M_CUR_CoasterExitGlow", new Color(0.2f, 1f, 0.45f), new Color(0.3f, 1.6f, 0.6f), 0.2f);
        float len = e.back_wall - e.mouth;
        foreach (float side in new[] { -1f, 1f })
            Bar(look, side < 0 ? "Glow_L" : "Glow_R", new Vector3(c.x + side * (e.pass_half - 0.08f), e.floor + 0.08f, c.z + (e.mouth + e.back_wall) / 2f), new Vector3(0.1f, 0.1f, len), glow, world: true);
        var lightT = new GameObject("Glow_Light").transform;
        lightT.SetParent(look, false);
        lightT.position = new Vector3(c.x, e.floor + 2.2f, c.z + (e.mouth + e.back_wall) / 2f);
        var light = lightT.gameObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(0.35f, 1f, 0.5f); light.range = 12f; light.intensity = 2.5f;
        look.gameObject.SetActive(false);

        // 탈출 구역 표시(충돌체 없음, 위치·크기만): 앞 계단~통로~뒤 마당 — 문이 닫혀 있으면 아무도 못 들어가고, 열리면 쿠키가 나가는 길
        // (괴물 몸(구 1.8 m)은 32° 계단을 못 오른다). 통행 검사가 이 구역을 쿠키 전용으로 따지지 않는다(MapPassabilityCheck.ExitZonePrefix)
        var zone = new GameObject(MapPassabilityCheck.ExitZonePrefix + "N").transform;
        zone.SetParent(exit, false);
        float zoneFront = c.z + e.front_foot - 0.5f;
        zone.position = new Vector3(c.x, 5f, (zoneFront + BoundaryZ) / 2f);
        zone.localScale = new Vector3(e.half_x * 2f + 1f, 10f, BoundaryZ - zoneFront);

        var trigger = new GameObject("ExitTrigger_N").transform;
        trigger.SetParent(exit, false);
        trigger.position = new Vector3(c.x, 1f, c.z + e.back_foot + 1.2f); // 뒤 계단을 다 내려온 곳
        return exit.gameObject.AddComponent<EscapeGate>();
    }

    private static void Bar(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool world = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        if (world) go.transform.position = pos; else go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ---------------- temporary sockets & chests ----------------

    // 임시 재료 꽂는 곳 4개: 앞 계단 앞 반원 안의 빈 자리(가까운 순, 서로 SocketSpacing 이상)
    private static QuadrantBuilding[] BuildSockets(Transform parent, Vector3 stairFoot, Bounds keepOut, out string report)
    {
        var spots = FreeSpots(stairFoot, 4f, 18f, SocketClearance, SocketSpacing, keepOut, 4, toward: Vector3.back);
        Material socketMat = PlainMaterial("M_CUR_TempSocket", new Color(0.25f, 0.2f, 0.3f), Color.black, 0.3f);
        Material beam = PlainMaterial("M_CUR_TempSocketGlow", new Color(0.4f, 1f, 0.6f), new Color(0.6f, 2f, 0.9f), 0.2f);
        var buildings = new QuadrantBuilding[4];
        for (int q = 0; q < spots.Count; q++)
        {
            var b = new GameObject($"Building_Q{q + 1}").transform;
            b.SetParent(parent, false);
            b.position = spots[q];
            var socket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socket.name = $"Socket_Q{q + 1}";
            socket.transform.SetParent(b, false);
            socket.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            socket.transform.localScale = new Vector3(1.6f, 0.5f, 1.6f);
            socket.GetComponent<Renderer>().sharedMaterial = socketMat;
            var repair = new GameObject("Repair_Point").transform;
            repair.SetParent(b, false);
            repair.localPosition = new Vector3(0f, 1.2f, 0f);
            var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.name = "ESC_Glow";
            glow.transform.SetParent(b, false);
            glow.transform.localPosition = new Vector3(0f, 16f, 0f);
            glow.transform.localScale = new Vector3(0.6f, 15f, 0.6f);
            glow.GetComponent<Renderer>().sharedMaterial = beam;
            glow.SetActive(false);
            buildings[q] = b.gameObject.AddComponent<QuadrantBuilding>();
        }
        report = $"temp sockets {spots.Count}: " + string.Join(" ", spots.Select(p => $"({p.x:F0},{p.z:F0})"));
        return buildings;
    }

    private static string MoveChests(Transform escape, Bounds keepOut)
    {
        var moved = new List<string>();
        foreach (MaterialChest chest in escape.GetComponentsInChildren<MaterialChest>(true))
        {
            Bounds b = chest.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
            if (!b.Intersects(keepOut)) continue;
            Vector3 from = chest.transform.position;
            foreach (Collider col in chest.GetComponentsInChildren<Collider>(true)) col.enabled = false; // 자기 자신은 빈 자리 검사에서 뺀다
            List<Vector3> spot = FreeSpots(from, 3f, 25f, 1.6f, 0f, keepOut, 1, toward: Vector3.zero);
            foreach (Collider col in chest.GetComponentsInChildren<Collider>(true)) col.enabled = true;
            if (spot.Count == 0) continue;
            chest.transform.position = spot[0];
            chest.transform.rotation = Quaternion.LookRotation(Flat(-spot[0]).normalized);
            moved.Add($"{chest.name} {from:F0}->{spot[0]:F0}");
        }
        Physics.SyncTransforms();
        return moved.Count > 0 ? "chests moved: " + string.Join(", ", moved) : "chests untouched";
    }

    // origin 둘레(minR~maxR) 격자 중 바닥이 걸을 수 있는 땅이고 clearance 반지름 안에 다른 충돌체가 없는 곳. toward 방향 쪽을 먼저.
    private static List<Vector3> FreeSpots(Vector3 origin, float minR, float maxR, float clearance, float spacing, Bounds keepOut, int count, Vector3 toward)
    {
        var cands = new List<Vector3>();
        for (float x = -maxR; x <= maxR; x += 1f)
            for (float z = -maxR; z <= maxR; z += 1f)
            {
                var p = new Vector3(origin.x + x, 0f, origin.z + z);
                float r = new Vector2(x, z).magnitude;
                if (r < minR || r > maxR) continue;
                if (keepOut.Contains(new Vector3(p.x, keepOut.center.y, p.z))) continue;
                if (spawns.Any(sp => Flat(sp - p).magnitude < SpawnClearance + clearance)) continue;
                cands.Add(p);
            }
        var picked = new List<Vector3>();
        foreach (Vector3 p in cands.OrderBy(p => (p - origin).magnitude - (toward == Vector3.zero ? 0f : Vector3.Dot((p - origin).normalized, toward) * 3f)))
        {
            if (picked.Count >= count) break;
            if (picked.Any(q => (q - p).magnitude < spacing)) continue;
            if (!Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            if (!hit.collider.name.Contains("Terrain_Walkable") || hit.point.y > 0.3f) continue;
            Vector3 feet = hit.point;
            bool blocked = Physics.OverlapCapsule(feet + Vector3.up * (clearance + 0.3f), feet + Vector3.up * 3f, clearance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Any(col => !col.name.Contains("Terrain"));
            if (!blocked) picked.Add(feet);
        }
        return picked;
    }

    // ---------------- assets ----------------

    private static GameObject Import(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return null;
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.animationType != ModelImporterAnimationType.None
            || importer.importAnimation || importer.addCollider || !importer.bakeAxisConversion)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.bakeAxisConversion = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static Texture2D Texture(string name, bool normal)
    {
        string path = $"{Folder}/{name}.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != type || ti.anisoLevel != 4)
            {
                ti.textureType = type;
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material EntranceMaterial()
    {
        Material m = LoadOrCreate("M_CUR_CoasterEntrance");
        m.mainTexture = Texture("CoasterEntrance_albedo", false);
        m.SetTexture("_BumpMap", Texture("CoasterEntrance_normal", true));
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Glossiness", 0.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material TunnelMaterial() => PlainMaterial("M_CUR_CoasterTunnel", new Color(0.08f, 0.03f, 0.09f), Color.black, 0.1f);

    // 레일·지지대: 색(참고 그림) + 구운 그늘(AO)
    private static Material TrackMaterial(string part, Color color)
    {
        Material m = LoadOrCreate("M_CUR_" + part);
        m.color = color;
        m.SetTexture("_OcclusionMap", Texture(part + "_ao", false));
        m.SetFloat("_OcclusionStrength", 1f);
        m.SetFloat("_Glossiness", part.Contains("Rail") ? 0.65f : 0.45f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material PlainMaterial(string name, Color color, Color emission, float gloss)
    {
        Material m = LoadOrCreate(name);
        m.color = color;
        m.SetFloat("_Glossiness", gloss);
        if (emission != Color.black)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", emission);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material LoadOrCreate(string name)
    {
        string path = $"{Folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    private static Transform Group(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static void SetArray(SerializedProperty array, IReadOnlyList<Object> items)
    {
        array.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
