using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 맵 축소(Plan.md/MapReplacePlan.md v2). 원본 맵 씬(외곽 벽 안쪽 ±172 m)을 ±70 m(한 변 140 m)로 재배치해 같은 경로에 저장한다.
// - 게임플레이 오브젝트는 크기를 바꾸지 않고 위치만 옮긴다. 4 m 간격을 지킬 수 없으면 뺀다(결정 Q3·Q4).
// - 지형·물·경계·배경 지형 메시는 정점을 새 좌표로 옮긴다(높이도 같은 비율 → 경사 유지).
// - 건축 요소는 벽·보·지붕의 길이만 맞추고 문 폭·높이는 그대로 둔다.
// 원본은 Assets/Scenes/Maps/Original/에 두고 항상 원본에서 다시 만든다(반복 실행해도 결과가 같다).
public static class MapCompactor
{
    private const string LogTag = "[MapCompact]";
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";
    public const string OriginalFolder = "Assets/Scenes/Maps/Original";
    public const string MarkerRoot = "MapCompactInfo";

    public const float OldHalf = 172f;              // 원본 외곽 벽 안쪽
    public const float NewHalf = 70f;               // 축소 후 외곽 벽 안쪽(한 변 140 m, 결정 Q1)
    public const float Ratio = NewHalf / OldHalf;   // 지형 높이·기본 가로 비율
    public const float MinGap = 4f;                 // 서로 다른 오브젝트 충돌 발자국 사이(결정 Q4)
    public const float BoundaryGap = 4f;
    public const float CookieSpawnZ = -38f, MonsterSpawnZ = 38f, MinSpawnDistance = 56f;
    private const float FlatHeight = 0.15f;         // 지면 위 이 높이 이하는 밟고 지나간다(괴물이 넘는 턱 높이, MapPassabilityCheck.MonsterStep)
    private const float ClearHeight = 7f;           // 괴물 외형 높이까지 막힘으로 본다
    private const float AttachRange = 3f;
    private const float MaxHostSize = 30f;          // 이보다 큰 오브젝트(지붕 등)에는 조명·이펙트를 붙이지 않는다
    private const float BoundaryBand = 6f;          // 원래 외곽에서 이 거리 안이면 외곽 기준으로 옮긴다
    private const float CellSize = 8f;
    private const float StretchMinLength = 12f;     // 늘이는 조각에서 이보다 짧은 축(파이프 굵기·보 폭)은 그대로

    private static readonly string[] Categories =
        { "Ground", "Terrain", "MainStructures", "GameplayProps", "Decoration", "Background", "Lighting", "Effects", "Water" };

    public static string OriginalPath(string map) => $"{OriginalFolder}/{MapSceneBuilder.SceneName(map)}.unity";

    // ---------------- menu ----------------

    [MenuItem(MenuRoot + "Compact All Map Scenes")]
    public static void CompactAll()
    {
        foreach (string map in MapSceneBuilder.MapNames) Compact(map);
    }

    [MenuItem(MenuRoot + "Compact CandyForest")] private static void C1() => Compact("CandyForest");
    [MenuItem(MenuRoot + "Compact GingerbreadVillage")] private static void C2() => Compact("GingerbreadVillage");
    [MenuItem(MenuRoot + "Compact ChocolateFactory")] private static void C3() => Compact("ChocolateFactory");
    [MenuItem(MenuRoot + "Compact CursedCandyCarnival")] private static void C4() => Compact("CursedCandyCarnival");
    [MenuItem(MenuRoot + "Compact HauntedBakery")] private static void C5() => Compact("HauntedBakery");

    // MapSceneBuilder가 원본 크기 씬을 새로 만든 직후 부른다: 원본을 갱신하고 곧바로 축소한다.
    public static void OnMapBuilt(string map)
    {
        EnsureFolder(OriginalFolder);
        string original = OriginalPath(map);
        if (File.Exists(original)) AssetDatabase.DeleteAsset(original);
        if (!AssetDatabase.CopyAsset(MapSceneBuilder.ScenePath(map), original))
        {
            Debug.LogError($"{LogTag} Could not refresh original {original}");
            return;
        }
        Compact(map);
    }

    // ---------------- config ----------------

    // 축별 구간 선형 함수. 끝점 밖은 기본 비율로 이어진다(배경).
    private sealed class Axis
    {
        private readonly float[] from, to;
        public Axis(float[] from, float[] to) { this.from = from; this.to = to; }
        public static Axis Uniform() => new Axis(new[] { -OldHalf, OldHalf }, new[] { -NewHalf, NewHalf });

        public float Map(float v)
        {
            int n = from.Length;
            if (v <= from[0]) return to[0] + (v - from[0]) * Ratio;
            if (v >= from[n - 1]) return to[n - 1] + (v - from[n - 1]) * Ratio;
            for (int i = 1; i < n; i++)
                if (v <= from[i]) return Mathf.Lerp(to[i - 1], to[i], (v - from[i - 1]) / (from[i] - from[i - 1]));
            return v;
        }
    }

    // 한 건물만 따로 줄일 때(인형극장): From 사각형을 To 사각형으로 옮긴다.
    private sealed class LocalFit
    {
        public Regex Members;
        public Rect From, To;
        public Vector2 Map(Vector2 p) => new Vector2(
            To.xMin + (p.x - From.xMin) / From.width * To.width,
            To.yMin + (p.y - From.yMin) / From.height * To.height);
    }

    private sealed class Placement { public Vector2 OldPoint, NewCenter; public float RotY; }
    private sealed class AnchorRef { public Vector2 OldPoint, Ref; }

    private sealed class MapConfig
    {
        public Axis X = Axis.Uniform(), Z = Axis.Uniform();
        public Regex[] Composites = new Regex[0];  // 같은 규칙에 걸리고 1.5 m 안에 붙은 것끼리 한 덩어리로 옮긴다
        public Regex Fixed;                        // 검사 없이 먼저 놓는다(랜드마크)
        public Regex Remove;                       // 처음부터 뺀다
        public Regex Walls, Lintels, Stretch, WallDecor, Pinned;
        public float WallDecorSpacing = 1f;
        public float MaxUnitSize = 60f;
        public readonly List<Vector2> RemoveUnitsAt = new List<Vector2>();
        public readonly List<Placement> Placements = new List<Placement>();
        public readonly List<AnchorRef> Anchors = new List<AnchorRef>();
        public readonly List<LocalFit> LocalFits = new List<LocalFit>();
        public Action<Transform> PreProcess;
        public Action<Transform> PostProcess;   // 배치가 끝난 뒤(빠질 오브젝트를 지우기 전) Map 루트에 대해

        public Vector2 MapGlobal(Vector2 p) => new Vector2(X.Map(p.x), Z.Map(p.y));

        public Func<Vector2, Vector2> MapFor(string name)
        {
            foreach (LocalFit fit in LocalFits)
                if (fit.Members.IsMatch(name)) return fit.Map;
            return MapGlobal;
        }
    }

    private static Regex R(string pattern) => new Regex(pattern, RegexOptions.Compiled);

    private static MapConfig ConfigFor(string map)
    {
        var c = new MapConfig();
        switch (map)
        {
            case "CandyForest":
                c.Composites = new[] { R("LollipopTree") };
                c.Fixed = R("LollipopTree");
                break;

            case "GingerbreadVillage":
                c.Composites = new[] { R("^GIN_CookieFountain_"), R("^GIN_SquareFountain") };
                c.Fixed = R("^GIN_Landmark_");
                // 개울 둑을 완만하게 만들어 걸어서 건너므로 다리는 뺀다(다리 밑은 괴물이 들어가지 못하는 숨을 곳이 됐다).
                c.Remove = R("^GIN_CookieBridge");
                break;

            case "ChocolateFactory":
                // 본관 홀 ±93 → ±34, 홀 밖 링 79 m → 36 m (결정 Q8: 홀을 줄이고 바깥 건물 2동)
                c.X = new Axis(new[] { -172f, -93f, 93f, 172f }, new[] { -70f, -34f, 34f, 70f });
                c.Z = new Axis(new[] { -172f, -93f, 93f, 172f }, new[] { -70f, -34f, 34f, 70f });
                c.Composites = new[]
                {
                    R("^CHO_Landmark_ChocolateTank|^CHO_ChocolateTank_"), R("^CHO_MachineRoom_"), R("^CHO_StorageShed"),
                    R("^CHO_WorkPlatform"), R("^CHO_PipeRack_"), R("^CHO_ChocolatePipe_(Down|Valve)"), R("^CHO_CookieConveyor"),
                };
                c.Fixed = R("^CHO_Landmark_|^CHO_ChocolateTank_");
                c.Remove = R(@"^CHO_(Warehouse|ProductionHall|TankFarm|SmallTank)|^CHO_CookieConveyor(_Belt)?_(08|09|10)$");
                c.RemoveUnitsAt.Add(new Vector2(125f, 150f)); // 기계실 3·4동
                c.RemoveUnitsAt.Add(new Vector2(156f, 156f));
                c.Placements.Add(new Placement { OldPoint = new Vector2(120f, 120f), NewCenter = new Vector2(52f, 30f), RotY = 0f });
                c.Placements.Add(new Placement { OldPoint = new Vector2(150f, 124f), NewCenter = new Vector2(-52f, -30f), RotY = 180f });
                c.Walls = R(@"^CHO_MainHall_Wall(_\d+)?$");
                c.Lintels = R("^CHO_MainHall_Wall_Lintel");
                c.Stretch = R("^CHO_(FactoryGlassVault|FactoryRoofArch|MainHall_TieBeam|ChocolatePipe_Main|FactoryWall)");
                c.WallDecor = R("^CHO_ArchWindow");
                c.Pinned = R("^CHO_ChocolatePipe_(Stripe|WallCollar)");
                // 공장 담장은 외곽 투명 벽 바로 안쪽에 놓여 모서리마다 쿠키만 들어가는 틈이 생겼다 → 외곽 바깥으로 민다(보이기만 한다).
                c.PostProcess = mapRoot => PushOutsideBoundary(mapRoot, R("^CHO_FactoryWall"));
                break;

            case "CursedCandyCarnival":
                c.Composites = new[]
                {
                    R("Carousel"), R("^CUR_CandyFerrisWheel_"), R("^CUR_DropTower"), R("^CUR_SwingRide_"),
                };
                c.Fixed = R("Carousel");
                c.Remove = R("^CUR_BumperArena_|^CUR_BumperCar");
                // 인형극장 47×35 → 26×22, 동쪽 외곽에 붙인다(무대 쪽 벽이 외곽).
                c.LocalFits.Add(new LocalFit
                {
                    Members = R("^CUR_PuppetTheatre_|^CUR_FX_TheatreFog"),
                    From = Rect.MinMaxRect(114.5f, -17.5f, 161.5f, 17.5f),
                    To = Rect.MinMaxRect(44f, -11f, 70f, 11f),
                });
                c.Walls = R(@"^CUR_PuppetTheatre_Wall(_\d+)?$");
                c.Lintels = R("^CUR_PuppetTheatre_Wall_Lintel");
                c.Stretch = R("^CUR_PuppetTheatre_(Roof|Stage|Curtain)");
                // 회전목마 바닥(0.5 m)·인형극 무대(0.3 m)는 괴물이 넘지 못하는 턱이라 쿠키만 올라가는 자리가 됐다 → 윗면이 지면 위 0.1 m가 되게 내린다.
                c.PostProcess = mapRoot =>
                {
                    SinkToGround(mapRoot, R("Carousel"), R("^CUR_Carousel_Base$"));
                    SinkToGround(mapRoot, R("^CUR_PuppetTheatre_Stage$"), R("^CUR_PuppetTheatre_Stage$"));
                };
                break;

            case "HauntedBakery":
                // 건물 외벽 x ±90.8 → ±44, 가게·창고 벽 ±60 → ±31, z −80.8 → −38, 50.8 → 32.
                // 외벽 문(±52.5~59.5)과 가게·창고 벽(±60) 사이 0.5 m가 줄면 벽이 문 틈을 막으므로 ±52.5~60 구간은 줄이지 않는다.
                c.X = new Axis(new[] { -172f, -90.8f, -60f, -52.5f, 52.5f, 60f, 90.8f, 172f }, new[] { -70f, -44f, -31f, -23.5f, 23.5f, 31f, 44f, 70f });
                c.Z = new Axis(new[] { -172f, -80.8f, 50.8f, 172f }, new[] { -70f, -38f, 32f, 70f });
                c.Composites = new[] { R("^HAU_Landmark_MagicOven|^HAU_MagicOven_Door"), R("^HAU_FlourSilo"), R("^HAU_Pillar") };
                c.Fixed = R("^HAU_Landmark_|^HAU_MagicOven_Door");
                c.Remove = R("^HAU_Corridor_Wall");
                c.Anchors.Add(new AnchorRef { OldPoint = new Vector2(0f, -70f), Ref = new Vector2(0f, -80.8f) }); // 오븐은 남쪽 벽 기준
                c.Walls = R(@"^HAU_(Bakery_OuterWall|Shop_Wall|Shop_Divider|Storage_Wall|Storage_Divider)(_\d+)?$");
                c.Lintels = R("^HAU_(Bakery_OuterWall|Shop_Wall|Shop_Divider|Storage_Wall|Storage_Divider)_Lintel");
                c.Stretch = R("^HAU_Bakery_(Ceiling|Roof|WallBeam|FloorPlanks)");
                c.WallDecor = R("^HAU_(DisplayWindow|Bakery_BeamPost|Bakery_GlowWindow|Chalkboard_Awning)");
                c.WallDecorSpacing = 6f;
                break;
        }
        return c;
    }

    // reference 충돌체 윗면이 그 아래 걷는 지형보다 SinkTop 높게 되도록 group 전체를 같이 내린다(크기는 그대로).
    private const float SinkTop = 0.1f;

    // 지면과 같거나 낮은 수면은 지면에 가려 깨져 보이므로 지면 위로 살짝 올린다(분지 안의 물은 그대로).
    //private const float WaterLift = 0.03f;

    // 그룹 FBX에 물체가 하나뿐이면 임포트 때 그 물체가 그룹 루트(Map 바로 아래) 자체가 되고, 빌더가 루트 위치를 0으로 맞추면서
    // 제자리를 잃는다 — 진저브레드 분수 꼭대기 보석이 분수 없이 맵 한가운데(시계탑 지하 경사로 축)에 보이는 장애물로 남았다
    // (research.md R5-26). 원점 기준으로 만든 큰 메시(배경 지형 등)는 그대로 맞는 자리이므로, 플레이 구역보다 작은 메시만
    // 제자리를 잃은 물체로 보고 걷어낸다(위치를 되살릴 수 없다).
    public static bool IsDisplacedGroupRootMesh(Transform group)
    {
        Bounds? b = null;
        if (group.TryGetComponent(out Renderer r)) b = r.bounds;
        else if (group.TryGetComponent(out Collider c)) b = c.bounds;
        return b.HasValue && b.Value.extents.x < NewHalf && b.Value.extents.z < NewHalf;
    }

    public static int StripGroupRootMeshes(Transform mapRoot)
    {
        int stripped = 0;
        foreach (Transform group in mapRoot)
        {
            if (!IsDisplacedGroupRootMesh(group)) continue;
            bool had = false;
            foreach (Collider c in group.GetComponents<Collider>()) { Object.DestroyImmediate(c); had = true; }
            if (group.TryGetComponent(out MeshRenderer renderer)) { Object.DestroyImmediate(renderer); had = true; }
            if (group.TryGetComponent(out MeshFilter filter)) { Object.DestroyImmediate(filter); had = true; }
            if (!had) continue;
            stripped++;
            Debug.Log($"{LogTag} Stripped the mesh on group root '{group.name}' (single-object group FBX lost its placement).");
        }
        return stripped;
    }

    private static void StripGroupRootMeshes(Context ctx)
    {
        int stripped = StripGroupRootMeshes(ctx.MapRoot);
        if (stripped > 0) ctx.Report.Add($"group root meshes stripped: {stripped}");
    }

    private static void LiftWaterAboveGround(Transform mapRoot)
    {
        foreach (MeshRenderer mr in mapRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!mr.name.Contains("Water")) continue;
            Bounds b = mr.bounds;
            float maxGround = float.NegativeInfinity;
            int covered = 0, samples = 0;
            for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
            {
                var p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, (i + 0.5f) / 5f), 500f, Mathf.Lerp(b.min.z, b.max.z, (j + 0.5f) / 5f));
                float ground = float.NegativeInfinity;
                foreach (RaycastHit h in Physics.RaycastAll(p, Vector3.down, 1000f))
                    if (h.collider.name.Contains("Terrain_Walkable")) ground = Mathf.Max(ground, h.point.y);
                if (float.IsNegativeInfinity(ground)) continue;
                samples++;
                maxGround = Mathf.Max(maxGround, ground);
                if (ground >= b.max.y - 0.01f) covered++;
            }
            if (samples == 0 || covered * 2 < samples) continue;
            mr.transform.position += Vector3.up * (maxGround /*+ WaterLift*/ - b.max.y);
        }
        Physics.SyncTransforms();
    }

    private static void SinkToGround(Transform mapRoot, Regex group, Regex reference)
    {
        Transform[] all = mapRoot.GetComponentsInChildren<Transform>(true);
        Transform refT = all.FirstOrDefault(t => reference.IsMatch(t.name) && t.GetComponent<Collider>() != null);
        if (refT == null) return;
        Bounds b = refT.GetComponent<Collider>().bounds;
        float ground = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(b.center.x, 500f, b.center.z), Vector3.down, 1000f))
            if (h.collider.name.Contains("Terrain_Walkable")) ground = Mathf.Max(ground, h.point.y);
        if (float.IsNegativeInfinity(ground)) return;
        float excess = b.max.y - (ground + SinkTop);
        if (excess <= 0f) return;
        foreach (Transform t in all)
            if (group.IsMatch(t.name) && (t.parent == null || !group.IsMatch(t.parent.name))) t.position += Vector3.down * excess;
        Physics.SyncTransforms();
    }

    private static void PushOutsideBoundary(Transform mapRoot, Regex names)
    {
        const float clearance = 1.5f; // 외곽 투명 벽(안쪽 면 ≈ NewHalf + 1) 바깥
        foreach (Transform t in mapRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!names.IsMatch(t.name)) continue;
            var r = t.GetComponent<Renderer>();
            if (r == null) continue;
            Bounds b = r.bounds;
            Vector3 d = Vector3.zero;
            if (Mathf.Abs(b.center.x) >= Mathf.Abs(b.center.z))
                d.x = b.center.x > 0f ? NewHalf + clearance - b.min.x : -NewHalf - clearance - b.max.x;
            else
                d.z = b.center.z > 0f ? NewHalf + clearance - b.min.z : -NewHalf - clearance - b.max.z;
            t.position += d;
        }
    }

    // ---------------- data ----------------

    private sealed class Item
    {
        public Transform T;
        public string Category;
        public Rect Visual;               // 원본 렌더러 XZ
        public bool HasVisual;
        public readonly List<Rect> Foot = new List<Rect>(); // 원본 막힘 발자국(지면 +0.35 ~ +7 m)
        public readonly List<(Rect rect, float top)> Tops = new List<(Rect, float)>(); // 충돌체별 윗면 높이(원본)
        public bool HasCollider;
        public float GroundOld;           // 원본 지면 높이(중심)
        public Vector2 Center => HasVisual ? Visual.center : new Vector2(T.position.x, T.position.z);
    }

    private sealed class Unit
    {
        public readonly List<Item> Items = new List<Item>();
        public string Name;
        public int Priority;
        public bool Fixed, Background;
        public Rect Visual;
        public readonly List<Rect> Foot = new List<Rect>();
        public Vector2 Ref, Target;
        public float RotY;
        public Vector2 GroundPoint;   // 높이 기준점(가장 큰 조각의 중심, 원본)
        public float GroundOld;       // 그 점의 원본 지면 높이(지형 재매핑 전에 잰 값)
        public bool HasCollider => Items.Any(i => i.HasCollider);
        public bool Flat => HasCollider && Foot.Count == 0;
    }

    private sealed class Context
    {
        public string Map;
        public MapConfig Config;
        public Transform MapRoot;
        public readonly List<Collider> GroundColliders = new List<Collider>();
        public readonly Dictionary<Transform, Matrix4x4> Moved = new Dictionary<Transform, Matrix4x4>();
        public readonly HashSet<Transform> Removed = new HashSet<Transform>();
        public readonly Dictionary<Transform, Item> Items = new Dictionary<Transform, Item>();
        public readonly RectIndex Blocking = new RectIndex();
        public readonly RectIndex Visuals = new RectIndex();   // 충돌체 없는 장식끼리
        public readonly RectIndex Flats = new RectIndex();
        public readonly RectIndex Outside = new RectIndex();   // 경계 밖 배경끼리
        public readonly List<WallSpan> Spans = new List<WallSpan>();
        public readonly List<string> Report = new List<string>();
        public readonly Dictionary<string, int[]> Counts = new Dictionary<string, int[]>(); // 범주별 [남김, 뺌]

        public void Count(string category, bool kept)
        {
            if (!Counts.TryGetValue(category, out int[] c)) Counts[category] = c = new int[2];
            c[kept ? 0 : 1]++;
        }
    }

    // 벽 조각의 새 위치(창·기둥을 벽 위에 남길지 판단)
    private sealed class WallSpan { public Vector2 A, B; }

    private sealed class RectIndex
    {
        private readonly Dictionary<long, List<Rect>> cells = new Dictionary<long, List<Rect>>();

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        public void Add(Rect r)
        {
            ForCells(r, key =>
            {
                if (!cells.TryGetValue(key, out List<Rect> list)) cells[key] = list = new List<Rect>();
                list.Add(r);
                return false;
            });
        }

        // gap 안으로 들어오는(거리 < gap) 사각형이 있으면 true
        public bool Hits(Rect r, float gap)
        {
            Rect e = Expand(r, gap);
            return ForCells(e, key =>
            {
                if (!cells.TryGetValue(key, out List<Rect> list)) return false;
                foreach (Rect o in list)
                    if (e.Overlaps(o)) return true;
                return false;
            });
        }

        private static bool ForCells(Rect r, Func<long, bool> visit)
        {
            int x0 = Mathf.FloorToInt(r.xMin / CellSize), x1 = Mathf.FloorToInt(r.xMax / CellSize);
            int z0 = Mathf.FloorToInt(r.yMin / CellSize), z1 = Mathf.FloorToInt(r.yMax / CellSize);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    if (visit(Key(x, z))) return true;
            return false;
        }
    }

    // ---------------- entry ----------------

    public static bool Compact(string map)
    {
        var config = ConfigFor(map);
        string target = MapSceneBuilder.ScenePath(map);
        string original = OriginalPath(map);
        if (!EnsureOriginal(target, original)) return false;

        EnsureFieldMeshesReadable(map);
        // 원본이 이미 열려 있어도 디스크에서 새로 읽도록 빈 씬을 거친다
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        GameObject mapGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Map");
        if (mapGo == null)
        {
            Debug.LogError($"{LogTag} {original} has no Map root");
            return false;
        }

        var ctx = new Context { Map = map, Config = config, MapRoot = mapGo.transform };
        config.PreProcess?.Invoke(ctx.MapRoot);
        Physics.SyncTransforms();

        CollectGround(ctx);
        List<Item> all = CollectItems(ctx);
        var attachments = CollectAttachments(ctx, scene);
        all = all.Where(i => ctx.Items.ContainsKey(i.T)).ToList();
        List<Item> fields = all.Where(IsField).ToList();

        RemoveMatches(ctx, all);
        RemapFields(ctx, fields);
        Physics.SyncTransforms();

        List<Item> rest = all.Where(i => !IsField(i) && !ctx.Removed.Contains(i.T)).ToList();
        PlaceArchitecture(ctx, rest);
        Physics.SyncTransforms();
        PlaceUnits(ctx, rest.Where(i => !ctx.Moved.ContainsKey(i.T) && !ctx.Removed.Contains(i.T)).ToList());
        ApplyAttachments(ctx, attachments);
        config.PostProcess?.Invoke(ctx.MapRoot);
        SyncGroundFixes(ctx, scene);
        LiftWaterAboveGround(ctx.MapRoot);

        foreach (Transform t in ctx.Removed)
            if (t != null) Object.DestroyImmediate(t.gameObject);
        StripGroupRootMeshes(ctx);
        Physics.SyncTransforms();

        UpdateProbes(scene);
        int filled = FillDarkAreas(scene);
        if (filled > 0) ctx.Report.Add($"fill lights added: {filled}");
        PlaceSpawnsAndKillZone(ctx);
        ctx.Report.Add(EscapeMapSetup.Apply(scene, map)); // 탈출 장치·로켓·상자 자리·마녀 위치(EscapePlan.md §4)
        AddMarker(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, target, false))
        {
            Debug.LogError($"{LogTag} Could not save {target}");
            return false;
        }

        AssetDatabase.SaveAssets(); // Compact/*.asset 메시
        string counts = string.Join(", ", ctx.Counts.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value[0]}/{k.Value[0] + k.Value[1]}"));
        Debug.Log($"{LogTag} {map} -> {target} (±{NewHalf} m)\n  kept/total: {counts}\n  " + string.Join("\n  ", ctx.Report));
        return true;
    }

    // 정점을 다시 쓰는 면 메시(지형·경계·물·배경 지형)가 든 FBX는 Read/Write를 켠다 — 꺼져 있으면 씬을 새로 연 직후
    // 정점을 읽을 수 없다(MeshData도 같은 제한).
    private static readonly string[] FieldModelCategories = { "Ground", "Water", "Background" };

    private static void EnsureFieldMeshesReadable(string map)
    {
        foreach (string category in FieldModelCategories)
        {
            string path = $"Assets/Maps/{map}/Models/{map}_{category}.fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || importer.isReadable) continue;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private static bool EnsureOriginal(string target, string original)
    {
        if (File.Exists(original)) return true;
        if (!File.Exists(target))
        {
            Debug.LogError($"{LogTag} {target} not found. Build the map first.");
            return false;
        }
        Scene open = EditorSceneManager.OpenScene(target, OpenSceneMode.Single);
        if (open.GetRootGameObjects().Any(g => g.name == MarkerRoot))
        {
            Debug.LogError($"{LogTag} {target} is already compacted and {original} is missing. Rebuild the map with MapSceneBuilder.");
            return false;
        }
        EnsureFolder(OriginalFolder);
        if (!AssetDatabase.CopyAsset(target, original))
        {
            Debug.LogError($"{LogTag} Could not copy {target} to {original}");
            return false;
        }
        return true;
    }

    // ---------------- collect ----------------

    private static void CollectGround(Context ctx)
    {
        foreach (Transform cat in ctx.MapRoot)
            foreach (Transform t in cat)
                if (t.name.Contains("Terrain_Walkable") || t.name.Contains("Terrain_Decor"))
                    ctx.GroundColliders.AddRange(t.GetComponents<Collider>());
    }

    private static float Ground(Context ctx, Vector2 p, float fallback)
    {
        var ray = new Ray(new Vector3(p.x, 500f, p.y), Vector3.down);
        float best = float.NegativeInfinity;
        foreach (Collider c in ctx.GroundColliders)
            if (c != null && c.enabled && c.Raycast(ray, out RaycastHit hit, 1000f)) best = Mathf.Max(best, hit.point.y);
        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    private static List<Item> CollectItems(Context ctx)
    {
        var items = new List<Item>();
        foreach (string category in Categories)
        {
            Transform cat = ctx.MapRoot.Find(category);
            if (cat == null) continue;
            foreach (Transform t in cat)
            {
                var item = new Item { T = t, Category = category };
                Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                    item.Visual = XZ(b);
                    item.HasVisual = true;
                }
                item.GroundOld = Ground(ctx, item.Center, 0f);
                foreach (Collider c in t.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger) continue;
                    item.HasCollider = true;
                    Bounds b = c.bounds;
                    item.Tops.Add((XZ(b), b.max.y));
                    if (b.max.y > item.GroundOld + FlatHeight && b.min.y < item.GroundOld + ClearHeight) item.Foot.Add(XZ(b));
                }
                items.Add(item);
                ctx.Items[t] = item;
            }
        }
        return items;
    }

    private static bool IsField(Item i) =>
        i.Category == "Ground" || i.Category == "Water" || i.T.name.Contains("Terrain_Decor");

    // 조명·렌더러 없는 이펙트·문은 원본에서 가까운 오브젝트에 붙는다(그 오브젝트와 같이 움직이고 같이 빠진다).
    private sealed class Attachment { public Transform T; public Transform Host; public float GroundOld; }

    private static List<Attachment> CollectAttachments(Context ctx, Scene scene)
    {
        var hosts = ctx.Items.Values.Where(i => i.HasVisual && !IsField(i) && i.Category != "Effects"
                                                && i.Visual.width <= MaxHostSize && i.Visual.height <= MaxHostSize).ToList();
        var list = new List<Attachment>();

        void Add(Transform t, Func<Item, bool> filter, float range)
        {
            Vector2 p = new Vector2(t.position.x, t.position.z);
            Item best = null;
            float bestD = range, bestArea = float.MaxValue;
            foreach (Item h in hosts)
            {
                if (filter != null && !filter(h)) continue;
                float d = Distance(h.Visual, p);
                float area = h.Visual.width * h.Visual.height;
                if (d < bestD - 0.01f || (d <= bestD + 0.01f && area < bestArea)) { best = h; bestD = d; bestArea = area; }
            }
            list.Add(new Attachment { T = t, Host = best?.T, GroundOld = Ground(ctx, p, 0f) });
        }

        foreach (Item i in ctx.Items.Values.Where(i => i.Category == "Effects" || !i.HasVisual).ToList())
        {
            if (!i.HasVisual && i.T.childCount > 0) continue; // 자식이 있는 피벗은 단위로 옮긴다
            ctx.Items.Remove(i.T);
            Add(i.T, null, AttachRange);
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "MapLighting")
            {
                Transform points = root.transform.Find("PointLights");
                if (points != null) foreach (Transform t in points) Add(t, null, AttachRange);
            }
            else if (root.name == "MapDoors")
            {
                foreach (Transform t in root.transform) Add(t, h => h.T.name.Contains("Lintel"), AttachRange);
            }
        }
        return list;
    }

    private static void RemoveMatches(Context ctx, List<Item> all)
    {
        Regex remove = ctx.Config.Remove;
        if (remove == null) return;
        foreach (Item i in all)
            if (remove.IsMatch(i.T.name)) Remove(ctx, i);
    }

    private static void Remove(Context ctx, Item i)
    {
        if (ctx.Removed.Add(i.T)) ctx.Count(i.Category, false);
    }

    private static void Keep(Context ctx, Item i, Matrix4x4 m)
    {
        ctx.Moved[i.T] = m;
        ctx.Count(i.Category, true);
    }

    // ---------------- fields (terrain, water, boundary) ----------------

    private static void RemapFields(Context ctx, List<Item> fields)
    {
        string folder = $"Assets/Maps/{ctx.Map}/Compact";
        EnsureFolder(folder);
        foreach (Item i in fields)
        {
            var mf = i.T.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Keep(ctx, i, Matrix4x4.identity); continue; }
            bool keepHeight = i.T.name.Contains("COL_Boundary");
            Mesh mesh = RemapMesh(ctx, mf.sharedMesh, i.T, keepHeight);
            mesh.name = i.T.name; // 에셋 파일 이름과 같아야 한다
            mesh = SaveMesh(mesh, $"{folder}/{i.T.name}.asset");
            mf.sharedMesh = mesh;
            foreach (MeshCollider mc in i.T.GetComponents<MeshCollider>()) mc.sharedMesh = mesh;
            Keep(ctx, i, Matrix4x4.identity);
        }
        ctx.Report.Add($"fields remapped: {fields.Count}");
    }

    private static Mesh RemapMesh(Context ctx, Mesh source, Transform t, bool keepHeight)
    {
        Mesh mesh = ReadableCopy(source);
        Vector3[] v = mesh.vertices;
        var world = new Vector3[v.Length];
        for (int k = 0; k < v.Length; k++)
        {
            Vector3 w = t.TransformPoint(v[k]);
            Vector2 m = ctx.Config.MapGlobal(new Vector2(w.x, w.z));
            world[k] = new Vector3(m.x, keepHeight ? w.y : w.y * Ratio, m.y);
        }
        if (t.name.Contains("Terrain_Walkable")) ctx.Report.Add(LimitSlope(world, mesh, t, WaterCaps(ctx)));
        for (int k = 0; k < v.Length; k++) v[k] = t.InverseTransformPoint(world[k]);
        mesh.vertices = v;
        mesh.RecalculateNormals();
        if (mesh.HasVertexAttribute(VertexAttribute.Tangent)) mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 걷는 지형의 급경사(개울 둑·단)를 없앤다: 윗면 정점끼리 이은 변마다 기울기가 MaxTerrainSlope를 넘으면 낮은 쪽을 올린다
    // (내리지는 않는다). 괴물은 뛰지 못하고 충돌체가 작아(r 0.31 m) 1 m 둑도 넘지 못했다 — 원본 맵에도 있던 문제.
    private const float MaxTerrainSlope = 0.7f; // 약 35°

    // 물 표면 아래 바닥은 수면보다 WaterClearance 낮게까지만 올린다(수면과 같은 높이가 되면 물·지면이 겹쳐 깨져 보였다 — 공장 강).
    private const float WaterClearance = 0.3f;

    private static List<(Rect rect, float y)> WaterCaps(Context ctx)
    {
        var caps = new List<(Rect, float)>();
        Transform water = ctx.MapRoot.Find("Water");
        if (water == null) return caps;
        foreach (Renderer r in water.GetComponentsInChildren<Renderer>(true))
        {
            Bounds b = r.bounds; // 원본 좌표(물은 지형 다음에 다시 매핑된다)
            Vector2 a = ctx.Config.MapGlobal(new Vector2(b.min.x, b.min.z)), c = ctx.Config.MapGlobal(new Vector2(b.max.x, b.max.z));
            caps.Add((Rect.MinMaxRect(a.x, a.y, c.x, c.y), b.max.y * Ratio - WaterClearance));
        }
        return caps;
    }

    private static string LimitSlope(Vector3[] world, Mesh source, Transform t, List<(Rect rect, float y)> waterCaps)
    {
        Vector3[] normals = source.normals;
        var groupOf = new int[world.Length];
        var keys = new Dictionary<(int, int, int), int>();
        var heights = new List<float>();
        var xz = new List<Vector2>();
        var top = new List<bool>();
        for (int i = 0; i < world.Length; i++)
        {
            var key = (Mathf.RoundToInt(world[i].x * 100f), Mathf.RoundToInt(world[i].y * 100f), Mathf.RoundToInt(world[i].z * 100f));
            if (!keys.TryGetValue(key, out int g))
            {
                g = heights.Count;
                keys[key] = g;
                heights.Add(world[i].y);
                xz.Add(new Vector2(world[i].x, world[i].z));
                top.Add(false);
            }
            groupOf[i] = g;
            if (normals.Length == world.Length && t.TransformDirection(normals[i]).y > 0.2f) top[g] = true;
        }
        int baseExcluded = ExcludeBaseSlab(world, source, heights, top);

        var edges = new HashSet<(int, int)>();
        for (int s = 0; s < source.subMeshCount; s++)
        {
            int[] tris = source.GetTriangles(s);
            for (int k = 0; k + 2 < tris.Length; k += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = groupOf[tris[k + e]], b = groupOf[tris[k + (e + 1) % 3]];
                    if (a == b || !top[a] || !top[b]) continue;
                    edges.Add(a < b ? (a, b) : (b, a));
                }
        }
        var list = edges.Select(e => (e.Item1, e.Item2, (xz[e.Item1] - xz[e.Item2]).magnitude)).ToArray(); // 수직 벽(길이 0)은 낮은 쪽을 높은 쪽까지 올린다

        float[] y = heights.ToArray();
        int passes = 0, raised = 0;
        for (bool changed = true; changed && passes < 400; passes++)
        {
            changed = false;
            foreach (var (a, b, len) in list)
            {
                float limit = MaxTerrainSlope * len;
                if (y[a] < y[b] - limit - 0.001f) { y[a] = y[b] - limit; changed = true; }
                else if (y[b] < y[a] - limit - 0.001f) { y[b] = y[a] - limit; changed = true; }
            }
        }
        for (int g = 0; g < y.Length; g++)
            foreach (var (rect, capY) in waterCaps)
                if (rect.Contains(xz[g]) && y[g] > capY) y[g] = Mathf.Max(heights[g], capY);
        for (int g = 0; g < y.Length; g++) if (y[g] > heights[g] + 0.001f) raised++;
        for (int i = 0; i < world.Length; i++) world[i].y = y[groupOf[i]];
        return $"terrain slope limit {MaxTerrainSlope}: raised {raised}/{y.Length} vertices in {passes} passes (base slab excluded {baseExcluded})";
    }

    // 걷는 지형 FBX에는 맵 전체(350 m)를 덮는 받침판이 원래 y −6 m에 깔려 있다. 받침판 정점은 옆면과 법선이 평균돼 위를 향한 것(y 0.58~0.71)으로
    // 보여 경사 제한 대상이 됐고, "수직 벽의 낮은 쪽을 올린다" 규칙에 끌려 윗면 높이까지 올라가 바닥과 같은 높이로 겹쳤다 — 공장 바닥이
    // z-fighting으로 깨져 보인 원인(다른 맵도 7~20% 지점에서 겹침). 실제 윗면(작고 위를 향한 삼각형)의 최저 높이보다 BaseSlabClearance
    // 넘게 아래 있는 정점은 지형 윗면이 아니므로 경사 제한에서 뺀다(받침판은 다시 매핑된 높이 −6 × Ratio에 남는다).
    private const float SurfaceTriangleMaxEdge = 30f;
    private const float BaseSlabClearance = 1f;

    private static int ExcludeBaseSlab(Vector3[] world, Mesh source, List<float> heights, List<bool> top)
    {
        // 받침판 높이 = 맵을 가로지르는 큰 수평 삼각형의 최저 높이. 받침판에는 작은 삼각형도 섞여 있어(공장 14개) 크기만으로는 거를 수 없다.
        float baseY = float.PositiveInfinity;
        ForEachFlatTriangle(world, source, (a, b, c, edge) =>
        {
            if (edge > SurfaceTriangleMaxEdge) baseY = Mathf.Min(baseY, Mathf.Min(world[a].y, Mathf.Min(world[b].y, world[c].y)));
        });
        if (float.IsPositiveInfinity(baseY)) return 0;

        // 윗면 최저 높이 = 받침판 높이에 있지 않은 작은 수평 삼각형의 최저 높이.
        float surfaceMin = float.PositiveInfinity;
        ForEachFlatTriangle(world, source, (a, b, c, edge) =>
        {
            float lowest = Mathf.Min(world[a].y, Mathf.Min(world[b].y, world[c].y));
            if (edge <= SurfaceTriangleMaxEdge && lowest > baseY + 0.01f) surfaceMin = Mathf.Min(surfaceMin, lowest);
        });
        if (float.IsPositiveInfinity(surfaceMin) || baseY > surfaceMin - BaseSlabClearance) return 0; // 받침판이 윗면과 떨어져 있지 않으면 건드리지 않는다

        int excluded = 0;
        for (int g = 0; g < heights.Count; g++)
        {
            if (!top[g] || heights[g] >= surfaceMin - BaseSlabClearance) continue;
            top[g] = false;
            excluded++;
        }
        return excluded;
    }

    // 수평에 가까운 삼각형(면 법선 |y| ≥ 0.5 — 가장자리 옆면 제외)마다 (a, b, c, 가장 긴 변)을 넘긴다.
    private static void ForEachFlatTriangle(Vector3[] world, Mesh source, System.Action<int, int, int, float> visit)
    {
        for (int s = 0; s < source.subMeshCount; s++)
        {
            int[] tris = source.GetTriangles(s);
            for (int k = 0; k + 2 < tris.Length; k += 3)
            {
                int a = tris[k], b = tris[k + 1], c = tris[k + 2];
                Vector3 faceNormal = Vector3.Cross(world[b] - world[a], world[c] - world[a]).normalized;
                if (Mathf.Abs(faceNormal.y) < 0.5f) continue;
                float edge = Mathf.Max((world[a] - world[b]).magnitude, (world[b] - world[c]).magnitude, (world[c] - world[a]).magnitude);
                visit(a, b, c, edge);
            }
        }
    }

    // 기존 에셋이 있으면 GUID를 유지한 채 데이터만 바꾼다(EditorUtility.CopySerialized는 API로 바꾼 정점을 옮기지 못했다).
    // FBX 메시는 Read/Write가 꺼져 있어 씬을 새로 연 직후에는 vertices를 읽을 수 없다. 에디터에서는 MeshData로 읽을 수 있으므로
    // 읽기 가능한 사본을 만든다(임포트 설정은 건드리지 않는다).
    private static Mesh ReadableCopy(Mesh source)
    {
        using (Mesh.MeshDataArray array = Mesh.AcquireReadOnlyMeshData(source))
        {
            Mesh.MeshData d = array[0];
            int n = d.vertexCount;
            var mesh = new Mesh { name = source.name, indexFormat = n > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            using (var a = new NativeArray<Vector3>(n, Allocator.Temp)) { d.GetVertices(a); mesh.SetVertices(a); }
            if (d.HasVertexAttribute(VertexAttribute.Normal))
                using (var a = new NativeArray<Vector3>(n, Allocator.Temp)) { d.GetNormals(a); mesh.SetNormals(a); }
            if (d.HasVertexAttribute(VertexAttribute.Tangent))
                using (var a = new NativeArray<Vector4>(n, Allocator.Temp)) { d.GetTangents(a); mesh.SetTangents(a); }
            if (d.HasVertexAttribute(VertexAttribute.Color))
                using (var a = new NativeArray<Color>(n, Allocator.Temp)) { d.GetColors(a); mesh.SetColors(a); }
            for (int ch = 0; ch < 8; ch++)
            {
                if (!d.HasVertexAttribute(VertexAttribute.TexCoord0 + ch)) continue;
                using (var a = new NativeArray<Vector2>(n, Allocator.Temp)) { d.GetUVs(ch, a); mesh.SetUVs(ch, a); }
            }
            mesh.subMeshCount = d.subMeshCount;
            for (int s = 0; s < d.subMeshCount; s++)
                using (var idx = new NativeArray<int>(d.GetSubMesh(s).indexCount, Allocator.Temp))
                {
                    d.GetIndices(idx, s);
                    mesh.SetIndices(idx, MeshTopology.Triangles, s);
                }
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // 매번 새로 만든다(이 메시는 축소한 씬만 가리키고, 그 씬도 매번 다시 저장한다).
    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ---------------- architecture (walls, lintels, stretched pieces) ----------------

    private sealed class Piece
    {
        public Item Item;
        public Vector3 Center;    // 월드 메시 중심
        public Vector3 Half;      // 길이 방향 반벡터(수평)
        public int Axis;          // 길이 방향 로컬 축
        public Vector2 A => new Vector2(Center.x - Half.x, Center.z - Half.z);
        public Vector2 B => new Vector2(Center.x + Half.x, Center.z + Half.z);
        public Vector2 NewA, NewB;
    }

    private static void PlaceArchitecture(Context ctx, List<Item> items)
    {
        MapConfig c = ctx.Config;
        var lintels = new List<Piece>();
        var walls = new List<Piece>();

        foreach (Item i in items)
        {
            string n = i.T.name;
            if (c.Lintels != null && c.Lintels.IsMatch(n)) lintels.Add(MakePiece(i));
            else if (c.Walls != null && c.Walls.IsMatch(n)) walls.Add(MakePiece(i));
        }

        // 인방(문 위): 중심만 옮기고 폭 유지 → 문 폭 그대로
        foreach (Piece p in lintels)
        {
            Vector2 center = c.MapFor(p.Item.T.name)(XZ(p.Center));
            Vector2 delta = center - XZ(p.Center);
            p.NewA = p.A + delta;
            p.NewB = p.B + delta;
            Translate(ctx, p.Item, center, XZ(p.Center));
        }

        // 벽: 끝점이 인방 끝과 맞닿으면 인방의 새 끝으로, 아니면 사상한 점으로 맞추고 길이 방향만 늘이거나 줄인다
        int wallsRemoved = 0;
        foreach (Piece p in walls)
        {
            Func<Vector2, Vector2> map = c.MapFor(p.Item.T.name);
            Vector2 a = Snap(p.A, p, lintels) ?? map(p.A);
            Vector2 b = Snap(p.B, p, lintels) ?? map(p.B);
            float oldLen = (p.B - p.A).magnitude, newLen = (b - a).magnitude;
            if (newLen < 1f) { Remove(ctx, p.Item); wallsRemoved++; continue; }
            ScaleAxis(p.Item.T, p.Axis, newLen / oldLen);
            MoveMeshCenter(ctx, p.Item, (a + b) * 0.5f);
            p.NewA = a;
            p.NewB = b;
            ctx.Spans.Add(new WallSpan { A = a, B = b });
        }

        int stretched = 0, pinned = 0;
        foreach (Item i in items)
        {
            if (ctx.Moved.ContainsKey(i.T) || ctx.Removed.Contains(i.T)) continue;
            string n = i.T.name;
            if (c.Stretch != null && c.Stretch.IsMatch(n)) { StretchPiece(ctx, i, c.MapFor(n)); stretched++; }
            else if (c.Pinned != null && c.Pinned.IsMatch(n)) { Translate(ctx, i, c.MapFor(n)(i.Center), i.Center); pinned++; }
        }

        // 창·기둥: 남은 벽 위(문 틈이 아닌 곳)에만 두고, 너무 붙으면 뺀다
        int decorKept = 0, decorRemoved = 0;
        if (c.WallDecor != null)
        {
            var placedOnSpan = new Dictionary<WallSpan, List<(float t, float half)>>();
            foreach (Item i in items.Where(i => c.WallDecor.IsMatch(i.T.name) && !ctx.Moved.ContainsKey(i.T) && !ctx.Removed.Contains(i.T))
                                    .OrderBy(i => i.T.name))
            {
                Vector2 p = c.MapFor(i.T.name)(i.Center);
                if (TryFindSpan(ctx, p, i.Visual, out WallSpan span, out float along, out float half)
                    && !TooClose(placedOnSpan, span, along, half, c.WallDecorSpacing))
                {
                    if (!placedOnSpan.TryGetValue(span, out var list)) placedOnSpan[span] = list = new List<(float, float)>();
                    list.Add((along, half));
                    Translate(ctx, i, p, i.Center);
                    decorKept++;
                }
                else { Remove(ctx, i); decorRemoved++; }
            }
        }

        Physics.SyncTransforms();
        foreach (Item i in items)
            if (ctx.Moved.ContainsKey(i.T)) AddBlocking(ctx, i);
        foreach (Piece p in lintels) AddDoorway(ctx, p.Item.T); // 문 틈 앞뒤도 비워 둔다(인방은 머리 위라 발자국이 없다)

        ctx.Report.Add($"architecture: lintels {lintels.Count}, walls {walls.Count - wallsRemoved}/{walls.Count}, stretched {stretched}, pinned {pinned}, wall decor {decorKept}/{decorKept + decorRemoved}");
    }

    private static Piece MakePiece(Item i)
    {
        var mf = i.T.GetComponent<MeshFilter>();
        Bounds lb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        var p = new Piece { Item = i, Center = i.T.TransformPoint(lb.center) };
        float best = -1f;
        for (int axis = 0; axis < 3; axis++)
        {
            Vector3 dir = i.T.TransformVector(AxisVector(axis) * lb.extents[axis]);
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (Mathf.Abs(dir.y) > flat.magnitude) continue; // 세로 축
            if (flat.magnitude > best) { best = flat.magnitude; p.Axis = axis; p.Half = flat; }
        }
        return p;
    }

    // 같은 벽 줄(평행하고 옆으로 0.6 m 안)에 있는 인방의 끝과 맞닿은 끝만 맞춘다 — 직각으로 만나는 다른 벽의 인방에 붙으면 벽이 틀어진다.
    private static Vector2? Snap(Vector2 end, Piece wall, List<Piece> lintels)
    {
        Vector2 dir = XZ(wall.Half).normalized;
        var normal = new Vector2(-dir.y, dir.x);
        foreach (Piece l in lintels)
        {
            if (Mathf.Abs(Vector2.Dot(XZ(l.Half).normalized, dir)) < 0.9f) continue;
            if (Mathf.Abs(Vector2.Dot(XZ(l.Center) - XZ(wall.Center), normal)) > 0.6f) continue;
            if ((l.A - end).sqrMagnitude < 0.75f * 0.75f) return l.NewA;
            if ((l.B - end).sqrMagnitude < 0.75f * 0.75f) return l.NewB;
        }
        return null;
    }

    private static void StretchPiece(Context ctx, Item i, Func<Vector2, Vector2> map)
    {
        var mf = i.T.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) { Translate(ctx, i, map(i.Center), i.Center); return; }
        Bounds lb = mf.sharedMesh.bounds;
        Vector3 center = i.T.TransformPoint(lb.center);
        for (int axis = 0; axis < 3; axis++)
        {
            Vector3 dir = i.T.TransformVector(AxisVector(axis) * lb.extents[axis]);
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (Mathf.Abs(dir.y) > flat.magnitude || flat.magnitude * 2f < StretchMinLength) continue; // 세로 축·두께 방향은 그대로
            Vector2 a = map(XZ(center - flat)), b = map(XZ(center + flat));
            float along = Vector2.Dot(b - a, XZ(flat).normalized);
            ScaleAxis(i.T, axis, Mathf.Max(0.05f, along / (flat.magnitude * 2f)));
        }
        MoveMeshCenter(ctx, i, map(XZ(center)));
    }

    private static void ScaleAxis(Transform t, int axis, float factor)
    {
        Vector3 s = t.localScale;
        s[axis] *= factor;
        t.localScale = s;
    }

    // 메시 중심을 newCenter(XZ)로 옮기고 지면 높이 변화만큼 올리거나 내린다.
    private static void MoveMeshCenter(Context ctx, Item i, Vector2 newCenter)
    {
        var mf = i.T.GetComponent<MeshFilter>();
        Vector3 local = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.zero;
        Vector3 now = i.T.TransformPoint(local);
        float dy = Ground(ctx, newCenter, i.GroundOld * Ratio) - i.GroundOld;
        Vector3 before = i.T.position;
        i.T.position += new Vector3(newCenter.x - now.x, dy, newCenter.y - now.z);
        Keep(ctx, i, Matrix4x4.Translate(i.T.position - before));
    }

    private static void Translate(Context ctx, Item i, Vector2 newCenter, Vector2 oldCenter)
    {
        float dy = Ground(ctx, newCenter, i.GroundOld * Ratio) - i.GroundOld;
        Vector3 d = new Vector3(newCenter.x - oldCenter.x, dy, newCenter.y - oldCenter.y);
        i.T.position += d;
        Keep(ctx, i, Matrix4x4.Translate(d));
    }

    private static bool TryFindSpan(Context ctx, Vector2 p, Rect visual, out WallSpan found, out float along, out float half)
    {
        found = null;
        along = half = 0f;
        float bestDist = 2.5f;
        foreach (WallSpan s in ctx.Spans)
        {
            Vector2 dir = s.B - s.A;
            float len = dir.magnitude;
            if (len < 0.01f) continue;
            dir /= len;
            float t = Vector2.Dot(p - s.A, dir);
            float dist = Mathf.Abs(Vector2.Dot(p - s.A, new Vector2(-dir.y, dir.x)));
            float h = 0.5f * (Mathf.Abs(dir.x) * visual.width + Mathf.Abs(dir.y) * visual.height);
            if (dist > bestDist || t < h - 0.2f || t > len - h + 0.2f) continue;
            bestDist = dist;
            found = s;
            along = t;
            half = h;
        }
        return found != null;
    }

    private static bool TooClose(Dictionary<WallSpan, List<(float t, float half)>> placed, WallSpan span, float along, float half, float spacing)
    {
        if (!placed.TryGetValue(span, out var list)) return false;
        foreach (var (t, h) in list)
            if (Mathf.Abs(t - along) < h + half + spacing) return true;
        return false;
    }

    // 인방 아래 문 틈(인방의 XZ 발자국)을 막힘으로 넣는다 → 다른 오브젝트는 문 틈에서 MinGap 이상 떨어진다(규칙 R8).
    // 인방 아래 문 틈과 문짝이 도는 자리(틈 앞뒤로 문짝 길이 = 틈 폭의 절반)를 막힘으로 넣는다 → 다른 오브젝트는 그 둘레에서
    // MinGap 이상 떨어진다(규칙 R8). 문짝이 도는 자리를 빼먹으면 열린 문짝과 소품 사이가 좁아져 괴물이 끼었다(베이커리 가게 문).
    private static void AddDoorway(Context ctx, Transform lintel)
    {
        var r = lintel.GetComponent<Renderer>();
        if (r == null) return;
        Rect gap = XZ(r.bounds);
        float leaf = Mathf.Max(gap.width, gap.height) * 0.5f;
        Rect swing = gap.width >= gap.height
            ? Rect.MinMaxRect(gap.xMin, gap.yMin - leaf, gap.xMax, gap.yMax + leaf)
            : Rect.MinMaxRect(gap.xMin - leaf, gap.yMin, gap.xMax + leaf, gap.yMax);
        ctx.Blocking.Add(swing);
    }

    private static void AddBlocking(Context ctx, Item i)
    {
        foreach (Collider col in i.T.GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger) continue;
            Bounds b = col.bounds;
            float g = Ground(ctx, XZ(b.center), 0f);
            if (b.max.y > g + FlatHeight && b.min.y < g + ClearHeight) ctx.Blocking.Add(XZ(b));
        }
    }

    // ---------------- units ----------------

    private static void PlaceUnits(Context ctx, List<Item> items)
    {
        List<Unit> units = BuildUnits(ctx, items);
        MapConfig c = ctx.Config;

        foreach (Vector2 p in c.RemoveUnitsAt)
        {
            Unit u = SmallestAt(units, p);
            if (u != null) { RemoveUnit(ctx, u); ctx.Report.Add($"removed by rule: {u.Name} ({u.Items.Count} parts)"); }
        }
        foreach (Unit u in units)
        {
            if (ctx.Removed.Contains(u.Items[0].T)) continue;
            if (!u.Fixed && !u.Background && u.Foot.Count > 0 && Math.Max(Envelope(u.Foot).width, Envelope(u.Foot).height) > c.MaxUnitSize)
            {
                RemoveUnit(ctx, u);
                ctx.Report.Add($"too large: {u.Name}");
            }
        }
        // 숨을 수 있는 넓은 윗면은 Fits에서 새 지형 기준으로 거른다(IsHidingPlatform)
        units = units.Where(u => !ctx.Removed.Contains(u.Items[0].T)).ToList();

        // 지정 배치·랜드마크 → 건물 → ... 순. 같은 등급은 큰 것부터.
        units = units.OrderBy(u => u.Priority).ThenByDescending(u => Area(u)).ThenBy(u => u.Name).ToList();
        List<Vector2> offsets = NudgeOffsets(6f);
        int placed = 0, nudged = 0, dropped = 0;
        foreach (Unit u in units)
        {
            if (u.Fixed) { Commit(ctx, u, u.Target); placed++; continue; }
            if (u.Background) { if (PlaceOutside(ctx, u)) placed++; else dropped++; continue; }

            float reach = u.Priority <= 4 ? 6f : 2f;
            bool ok = false;
            foreach (Vector2 o in offsets)
            {
                if (o.magnitude > reach) break;
                if (!Fits(ctx, u, u.Target + o)) continue;
                Commit(ctx, u, u.Target + o);
                if (o != Vector2.zero) nudged++;
                ok = true;
                break;
            }
            if (ok) placed++;
            else { RemoveUnit(ctx, u); dropped++; }
        }
        ctx.Report.Add($"units: {units.Count}, placed {placed} (nudged {nudged}), dropped {dropped}");
    }

    // 쿠키는 뛰어 오르지만 괴물은 못 오르는 높이(0.15~3.2 m)의 넓은 윗면(가로·세로 4 m 초과) — 가운데 서면 괴물 손이 닿지 않는다.
    // CookieClimb 3.2: 점프 1.8 m + 둥근 윗면(젤리 덤불 등)을 기어오르는 몫. GrabSpan 4: 괴물 손(2.5 m 안팎)이 가운데에 닿지 않는 폭.
    private const float MonsterClimb = FlatHeight, CookieClimb = 3.2f, GrabSpan = 4f;

    // 단위를 target에 둘 때의 높이 변화: 기준점 아래 새 지면 − 원래 지면(원래 지면 위 높이를 유지)
    private static float HeightShift(Context ctx, Unit u, Vector2 target)
    {
        Vector2 p = Moved(new Rect(u.GroundPoint, Vector2.zero), u, target).position;
        return Ground(ctx, p, u.GroundOld * Ratio) - u.GroundOld;
    }

    private static bool IsHidingPlatform(Context ctx, Unit u, Vector2 target)
    {
        float dy = HeightShift(ctx, u, target);
        foreach (Item i in u.Items)
            foreach (var (rect, top) in i.Tops)
            {
                if (Mathf.Min(rect.width, rect.height) <= GrabSpan) continue;
                Rect m = Moved(rect, u, target);
                float rel = top + dy - Ground(ctx, m.center, 0f);
                if (rel > MonsterClimb && rel <= CookieClimb) return true;
            }
        return false;
    }

    private static List<Unit> BuildUnits(Context ctx, List<Item> items)
    {
        MapConfig c = ctx.Config;
        var groups = new List<List<Item>>();
        var used = new HashSet<Item>();

        foreach (Regex rule in c.Composites)
        {
            List<Item> members = items.Where(i => !used.Contains(i) && rule.IsMatch(i.T.name)).ToList();
            var parent = Enumerable.Range(0, members.Count).ToArray();
            int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
            for (int a = 0; a < members.Count; a++)
                for (int b = a + 1; b < members.Count; b++)
                    if (Distance(members[a].Visual, members[b].Visual) <= 1.5f) parent[Find(a)] = Find(b);
            foreach (var g in members.Select((m, k) => (m, k)).GroupBy(x => Find(x.k)))
            {
                groups.Add(g.Select(x => x.m).ToList());
                foreach (var x in g) used.Add(x.m);
            }
        }
        foreach (Item i in items)
            if (!used.Contains(i)) groups.Add(new List<Item> { i });

        var units = new List<Unit>();
        foreach (List<Item> g in groups)
        {
            var u = new Unit();
            u.Items.AddRange(g);
            Item main = g.OrderByDescending(i => i.Visual.width * i.Visual.height).First();
            u.Name = main.T.name;
            u.GroundPoint = main.Center;
            u.GroundOld = main.GroundOld;
            u.Visual = g[0].Visual;
            foreach (Item i in g) { u.Visual = Union(u.Visual, i.Visual); u.Foot.AddRange(i.Foot); }
            u.Priority = g.Min(i => PriorityOf(i.Category));
            u.Fixed = c.Fixed != null && g.Any(i => c.Fixed.IsMatch(i.T.name));
            if (u.Fixed) u.Priority = 0;

            Vector2 center = u.Visual.center;
            u.Ref = center;
            u.Background = Mathf.Abs(center.x) > OldHalf || Mathf.Abs(center.y) > OldHalf;
            if (!u.Background)
            {
                // 원래 외곽 근처였으면 외곽 기준으로 옮겨 외곽과의 간격을 유지한다
                if (u.Visual.xMax > OldHalf - BoundaryBand) u.Ref.x = OldHalf;
                else if (u.Visual.xMin < -OldHalf + BoundaryBand) u.Ref.x = -OldHalf;
                if (u.Visual.yMax > OldHalf - BoundaryBand) u.Ref.y = OldHalf;
                else if (u.Visual.yMin < -OldHalf + BoundaryBand) u.Ref.y = -OldHalf;
            }
            u.Target = c.MapFor(u.Name)(u.Ref);
            units.Add(u);
        }

        foreach (AnchorRef a in c.Anchors)
        {
            Unit u = SmallestAt(units, a.OldPoint);
            if (u == null) continue;
            u.Ref = a.Ref;
            u.Target = c.MapFor(u.Name)(a.Ref);
        }
        foreach (Placement p in c.Placements)
        {
            Unit u = SmallestAt(units, p.OldPoint);
            if (u == null) continue;
            u.Ref = u.Visual.center;
            u.Target = p.NewCenter;
            u.RotY = p.RotY;
            u.Fixed = true;
            u.Priority = 0;
            u.Background = false;
            ctx.Report.Add($"placed by rule: {u.Name} ({u.Items.Count} parts) -> {p.NewCenter}, rot {p.RotY}");
        }
        return units;
    }

    // 규칙 좌표를 품은 단위 중 여러 조각으로 된 것(건물·놀이기구)을 먼저, 그중 가장 작은 것
    private static Unit SmallestAt(List<Unit> units, Vector2 p) =>
        units.Where(u => u.Visual.Contains(p)).OrderBy(u => u.Items.Count > 1 ? 0 : 1)
             .ThenBy(u => u.Visual.width * u.Visual.height).FirstOrDefault();

    private static int PriorityOf(string category)
    {
        switch (category)
        {
            case "MainStructures": return 2;
            case "Terrain": return 3;
            case "GameplayProps": return 4;
            case "Lighting": return 5;
            case "Decoration": return 6;
            case "Effects": return 6;
            default: return 7; // Background(플레이 영역 안)
        }
    }

    private static float Area(Unit u) => u.Foot.Count > 0 ? u.Foot.Sum(r => r.width * r.height) : u.Visual.width * u.Visual.height * 0.01f;

    private static List<Vector2> NudgeOffsets(float radius)
    {
        var list = new List<Vector2> { Vector2.zero };
        for (float r = 1f; r <= radius; r += 1f)
        {
            int steps = Mathf.CeilToInt(2f * Mathf.PI * r / 1.5f);
            for (int k = 0; k < steps; k++)
            {
                float a = k * Mathf.PI * 2f / steps;
                list.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
        }
        return list;
    }

    private static Rect Moved(Rect r, Unit u, Vector2 target)
    {
        if (Mathf.Approximately(u.RotY, 0f)) return new Rect(r.position + (target - u.Ref), r.size);
        Quaternion q = Quaternion.Euler(0f, u.RotY, 0f);
        Vector3 a = q * new Vector3(r.xMin - u.Ref.x, 0f, r.yMin - u.Ref.y);
        Vector3 b = q * new Vector3(r.xMax - u.Ref.x, 0f, r.yMax - u.Ref.y);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x) + target.x, Mathf.Min(a.z, b.z) + target.y,
                               Mathf.Max(a.x, b.x) + target.x, Mathf.Max(a.z, b.z) + target.y);
    }

    private static bool Fits(Context ctx, Unit u, Vector2 target)
    {
        Rect visual = Moved(u.Visual, u, target);
        if (u.Foot.Count > 0)
        {
            foreach (Rect r in u.Foot)
            {
                Rect m = Moved(r, u, target);
                if (!Inside(m, BoundaryGap) || ctx.Blocking.Hits(m, MinGap)) return false;
            }
            return !IsHidingPlatform(ctx, u, target);
        }
        if (!Inside(visual, 0.5f)) return false;
        if (u.Flat) return !ctx.Flats.Hits(visual, 0.5f);
        return !ctx.Blocking.Hits(visual, 0.3f) && !ctx.Visuals.Hits(visual, 0f);
    }

    private static bool Inside(Rect r, float gap) =>
        r.xMin >= -NewHalf + gap && r.xMax <= NewHalf - gap && r.yMin >= -NewHalf + gap && r.yMax <= NewHalf - gap;

    private static void Commit(Context ctx, Unit u, Vector2 target)
    {
        float dy = HeightShift(ctx, u, target);
        Quaternion q = Quaternion.Euler(0f, u.RotY, 0f);
        Matrix4x4 m = Matrix4x4.TRS(new Vector3(target.x, dy, target.y), q, Vector3.one)
                      * Matrix4x4.Translate(new Vector3(-u.Ref.x, 0f, -u.Ref.y));
        foreach (Item i in u.Items)
        {
            i.T.position = m.MultiplyPoint3x4(i.T.position);
            i.T.rotation = q * i.T.rotation;
            Keep(ctx, i, m);
        }

        if (u.Background) { ctx.Outside.Add(Moved(u.Visual, u, target)); return; }
        foreach (Item i in u.Items)
            if (i.T.name.Contains("Lintel")) AddDoorway(ctx, i.T); // 통째로 옮긴 건물(기계실 등)의 문 틈
        if (u.Foot.Count > 0) foreach (Rect r in u.Foot) ctx.Blocking.Add(Moved(r, u, target));
        else if (u.Flat) ctx.Flats.Add(Moved(u.Visual, u, target));
        else ctx.Visuals.Add(Moved(u.Visual, u, target));
    }

    // 경계 밖 배경: 사상한 자리에 두되 플레이 영역에 걸치면 바깥으로 밀고, 다른 배경과 겹치면 뺀다.
    private static bool PlaceOutside(Context ctx, Unit u)
    {
        Vector2 target = u.Target;
        Rect r = Moved(u.Visual, u, target);
        float margin = NewHalf + 1f;
        if (r.xMin < margin && r.xMax > -margin && r.yMin < margin && r.yMax > -margin)
        {
            float pushX = target.x >= 0f ? margin - r.xMin : -margin - r.xMax;
            float pushZ = target.y >= 0f ? margin - r.yMin : -margin - r.yMax;
            if (Mathf.Abs(pushX) <= Mathf.Abs(pushZ)) target.x += pushX; else target.y += pushZ;
            r = Moved(u.Visual, u, target);
        }
        if (ctx.Outside.Hits(r, 0f)) { RemoveUnit(ctx, u); return false; }
        Commit(ctx, u, target);
        return true;
    }

    private static void RemoveUnit(Context ctx, Unit u)
    {
        foreach (Item i in u.Items) Remove(ctx, i);
    }

    // ---------------- attachments, ground fixes ----------------

    // 붙은 오브젝트가 남으면 같이 옮기고, 빠지면 이펙트·문은 같이 빼고 점광원은 자유 배치로 돌린다.
    // 자유 배치 점광원은 좁아진 맵에서 몰리지 않게 LightSpacing 안에 이미 불이 있으면 뺀다.
    private const float LightSpacing = 8f;

    private static void ApplyAttachments(Context ctx, List<Attachment> list)
    {
        int moved = 0, removed = 0, free = 0, thinned = 0;
        var lights = new List<Vector3>();
        var pending = new List<Attachment>();
        foreach (Attachment a in list)
        {
            if (a.T == null) continue;
            bool isLight = a.T.GetComponent<Light>() != null;
            if (a.Host != null && !ctx.Removed.Contains(a.Host) && ctx.Moved.TryGetValue(a.Host, out Matrix4x4 m))
            {
                a.T.position = m.MultiplyPoint3x4(a.T.position);
                a.T.rotation = m.rotation * a.T.rotation;
                if (isLight) lights.Add(a.T.position);
                moved++;
                continue;
            }
            if (a.Host != null && !isLight)
            {
                Object.DestroyImmediate(a.T.gameObject);
                removed++;
                continue;
            }
            pending.Add(a);
        }

        foreach (Attachment a in pending)
        {
            Vector2 p = new Vector2(a.T.position.x, a.T.position.z);
            Vector2 n = ctx.Config.MapFor(a.T.name)(p);
            float dy = Ground(ctx, n, a.GroundOld * Ratio) - a.GroundOld;
            Vector3 pos = new Vector3(n.x, a.T.position.y + dy, n.y);
            if (a.T.GetComponent<Light>() != null)
            {
                if (lights.Any(l => Horizontal(l - pos) < LightSpacing)) { Object.DestroyImmediate(a.T.gameObject); thinned++; continue; }
                lights.Add(pos);
            }
            a.T.position = pos;
            free++;
        }
        ctx.Report.Add($"attachments (lights, effects, doors): with host {moved}, removed {removed}, mapped {free}, lights thinned {thinned}");
    }

    // MapSceneBuilder.FixGround가 만든 COLFIX_* 충돌체를 원본 오브젝트에 다시 맞춘다.
    private static void SyncGroundFixes(Context ctx, Scene scene)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MapColliders");
        if (root == null) return;
        int synced = 0, removed = 0;
        foreach (Transform fix in root.transform.Cast<Transform>().ToList())
        {
            string sourceName = fix.name.StartsWith("COLFIX_") ? fix.name.Substring(7) : fix.name;
            Transform source = ctx.Items.Keys.FirstOrDefault(t => t != null && t.name == sourceName);
            if (source == null || ctx.Removed.Contains(source))
            {
                Object.DestroyImmediate(fix.gameObject);
                removed++;
                continue;
            }
            fix.SetPositionAndRotation(source.position, source.rotation);
            fix.localScale = source.lossyScale;
            var mf = source.GetComponent<MeshFilter>();
            var mc = fix.GetComponent<MeshCollider>();
            if (mf != null && mc != null) mc.sharedMesh = mf.sharedMesh;
            synced++;
        }
        ctx.Report.Add($"ground fixes: synced {synced}, removed {removed}");
    }

    // ---------------- fill lights (GameFixPlan.md F7) ----------------

    // 맵을 줄이며 조명을 솎아 내고(LightSpacing), 높은 외곽 벽이 해를 가리는 가장자리 띠가 너무 어두웠다
    // (HauntedBakery (54, 0, 20) 등, 6 m 격자 529곳 중 130곳이 어떤 조명 범위의 80% 밖). 걸을 수 있는 곳을 격자로 훑어
    // 조명이 충분히 닿지 않는 곳에 맵의 기존 랜턴과 같은 색의 보충 조명을 둔다. 그림자는 끄고 렌더 모드는 Auto로 둔다(최적화).
    private const float FillGrid = 7f; // 14 m 격자는 (54, 0, 20) 같은 벽 옆 띠를 건너뛰었다
    // 점광원은 범위 끝으로 갈수록 거의 닿지 않는다 — 범위의 80%를 기준으로 하면 가장자리가 여전히 어두웠다(Play Mode 확인).
    private const float FillCoverage = 0.55f;
    private const float FillHeight = 3.3f;
    private const float FillIntensityScale = 1.1f;

    private static int FillDarkAreas(Scene scene)
    {
        GameObject lighting = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MapLighting");
        Transform points = lighting != null ? lighting.transform.Find("PointLights") : null;
        if (points == null) return 0;

        List<Light> lights = points.GetComponentsInChildren<Light>(true).Where(l => l.type != LightType.Directional).ToList();
        if (lights.Count == 0) return 0;
        Light sample = lights[0]; // 맵의 랜턴 색·범위를 그대로 따른다

        Physics.SyncTransforms();
        int added = 0;
        for (float x = -NewHalf + FillGrid / 2f; x < NewHalf; x += FillGrid)
        for (float z = -NewHalf + FillGrid / 2f; z < NewHalf; z += FillGrid)
        {
            if (!TryWalkableHeight(x, z, out float y)) continue;
            var p = new Vector2(x, z);
            if (lights.Any(l => Vector2.Distance(p, new Vector2(l.transform.position.x, l.transform.position.z)) < l.range * FillCoverage)) continue;

            var go = new GameObject($"LGT_Fill_{added:00}");
            go.transform.SetParent(points, false);
            go.transform.position = new Vector3(x, y + FillHeight, z);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = sample.color;
            light.range = sample.range;
            light.intensity = sample.intensity * FillIntensityScale;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            lights.Add(light);
            added++;
        }
        return added;
    }

    private static bool TryWalkableHeight(float x, float z, out float y)
    {
        y = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f))
            if (h.collider.name.Contains("Terrain_Walkable")) y = Mathf.Max(y, h.point.y);
        return !float.IsNegativeInfinity(y);
    }

    // ---------------- probes, spawns, kill zone ----------------

    private static void UpdateProbes(Scene scene)
    {
        GameObject lighting = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MapLighting");
        Transform probes = lighting != null ? lighting.transform.Find("ReflectionProbes") : null;
        if (probes == null) return;
        GameObject map = scene.GetRootGameObjects().First(g => g.name == "Map");
        Transform water = map.transform.Find("Water");
        foreach (ReflectionProbe probe in probes.GetComponentsInChildren<ReflectionProbe>(true))
        {
            if (probe.name == "Probe_Map")
            {
                probe.size = new Vector3(NewHalf * 2f + 20f, probe.size.y, NewHalf * 2f + 20f);
                continue;
            }
            string waterName = probe.name.StartsWith("Probe_") ? probe.name.Substring(6) : probe.name;
            Transform w = water != null ? water.Find(waterName) : null;
            Renderer r = w != null ? w.GetComponent<Renderer>() : null;
            if (r == null) { Object.DestroyImmediate(probe.gameObject); continue; }
            Bounds b = r.bounds;
            probe.transform.position = b.center + Vector3.up * 3f;
            probe.size = b.size + new Vector3(10f, 30f, 10f);
        }
    }

    private static void PlaceSpawnsAndKillZone(Context ctx)
    {
        Vector3 cookie = FindSpawn(new Vector3(0f, 0f, CookieSpawnZ), 2.5f);
        Vector3 monster = FindSpawn(new Vector3(0f, 0f, MonsterSpawnZ), 2.5f);
        if (Horizontal(monster - cookie) < MinSpawnDistance)
            monster = FindSpawn(new Vector3(-cookie.x, 0f, -cookie.z), 2.5f);
        SetPosition(SceneSpawnPoints.Cookie, cookie);
        SetPosition(SceneSpawnPoints.Monster, monster);

        float lowest = 0f;
        Transform ground = ctx.MapRoot.Find("Ground");
        if (ground != null)
            foreach (Renderer r in ground.GetComponentsInChildren<Renderer>(true))
                if (!r.name.Contains("COL_Boundary")) lowest = Mathf.Min(lowest, r.bounds.min.y);
        var kill = GameObject.Find("VoidKillZone");
        if (kill != null)
        {
            kill.transform.position = new Vector3(0f, lowest - 10f, 0f);
            var box = kill.GetComponent<BoxCollider>();
            if (box != null) { box.size = new Vector3(NewHalf * 2f + 40f, 4f, NewHalf * 2f + 40f); box.center = Vector3.zero; box.isTrigger = true; }
        }
        ctx.Report.Add($"spawns: cookie {cookie:F1}, monster {monster:F1} (distance {Horizontal(monster - cookie):F0} m), kill zone y {lowest - 10f:F1}");
    }

    private static float Horizontal(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    // 목표점에서 가까운 순으로, 평평하고(법선 y ≥ 0.95) 반경 안이 비어 있는 걷는 지면 점(MapSceneBuilder.FindSpawn과 같은 기준)
    public static Vector3 FindSpawn(Vector3 target, float clearRadius)
    {
        for (float r = 0f; r <= 40f; r += 2f)
        {
            int steps = r <= 0f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r / 2f);
            for (int k = 0; k < steps; k++)
            {
                float a = k * Mathf.PI * 2f / steps;
                var p = new Vector3(target.x + r * Mathf.Cos(a), 0f, target.z + r * Mathf.Sin(a));
                if (Mathf.Abs(p.x) > NewHalf - 6f || Mathf.Abs(p.z) > NewHalf - 6f) continue;
                if (!Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.95f || !IsGround(hit.collider)) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * (clearRadius + 0.3f), clearRadius, ~0, QueryTriggerInteraction.Collide)) continue;
                return hit.point;
            }
        }
        Debug.LogWarning($"{LogTag} No clear spawn near {target}. Using target.");
        return target;
    }

    private static bool IsGround(Collider c)
    {
        Transform t = c.transform;
        while (t.parent != null && t.parent.name != "Map" && t.parent.name != "MapColliders") t = t.parent;
        return t.name == "Ground" || t.name == "Terrain" || (t.parent != null && t.parent.name == "MapColliders");
    }

    private static void SetPosition(string name, Vector3 position)
    {
        var go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        go.transform.SetParent(null);
        go.transform.position = position;
    }

    private static void AddMarker(Scene scene)
    {
        foreach (GameObject g in scene.GetRootGameObjects())
            if (g.name == MarkerRoot) Object.DestroyImmediate(g);
        var marker = new GameObject(MarkerRoot) { tag = "EditorOnly" };
        SceneManager.MoveGameObjectToScene(marker, scene);
    }

    // ---------------- helpers ----------------

    private static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
    private static Rect XZ(Bounds b) => Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
    private static Vector3 AxisVector(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

    private static Rect Expand(Rect r, float d) => Rect.MinMaxRect(r.xMin - d, r.yMin - d, r.xMax + d, r.yMax + d);

    private static Rect Union(Rect a, Rect b) =>
        Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    private static Rect Envelope(List<Rect> rects)
    {
        Rect r = rects[0];
        foreach (Rect o in rects) r = Union(r, o);
        return r;
    }

    private static float Distance(Rect r, Vector2 p)
    {
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
        float dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float Distance(Rect a, Rect b)
    {
        float dx = Mathf.Max(a.xMin - b.xMax, 0f, b.xMin - a.xMax);
        float dz = Mathf.Max(a.yMin - b.yMax, 0f, b.yMin - a.yMax);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
