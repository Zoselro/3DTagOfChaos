using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 맵 축소(Plan.md/MapReplacePlan.md v2) 회귀 방지. 빌드 설정의 맵 씬 5개를 차례로 열어 검사한다(열려 있던 씬 구성은 끝나면 되돌린다).
public class MapCompactTests
{
    private const float DoorMinWidth = 6.5f;   // 원본 문 틈 7 m 안의 두 문짝(최소 3.35 m × 2)
    private const float DoorMinHeight = 7.4f;  // 괴물 외형 높이 이상(원본 문 높이 7.42 m~)

    private static readonly string[] Maps = MapSceneBuilder.MapNames;
    private SceneSetup[] savedSetup;

    [OneTimeSetUp]
    public void SaveSetup()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save the open scenes before running map tests.");
        savedSetup = EditorSceneManager.GetSceneManagerSetup();
    }

    [OneTimeTearDown]
    public void RestoreSetup()
    {
        if (savedSetup != null && savedSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(savedSetup);
    }

    private static Scene Open(string map) => EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);

    // 그룹 루트에 붙은 작은 메시는 제자리를 잃은 물체다(research.md R5-26 — 진저브레드 시계탑 축의 분수 보석).
    // 원점 기준의 큰 메시(배경 지형)는 맞는 자리라 허용한다.
    [TestCaseSource(nameof(Maps))]
    public void MapScene_GroupRootsHaveNoDisplacedMesh(string map)
    {
        Open(map);
        foreach (Transform group in GameObject.Find("Map").transform)
            Assert.IsFalse(MapCompactor.IsDisplacedGroupRootMesh(group),
                $"{map}: group root '{group.name}' carries a small mesh at the map origin (lost its placement). Run Compact {map}.");
    }

    [TestCaseSource(nameof(Maps))]
    public void MapScene_IsCompactedToPlayArea(string map)
    {
        Scene scene = Open(map);
        Assert.IsTrue(scene.GetRootGameObjects().Any(g => g.name == MapCompactor.MarkerRoot),
            $"{map} is not compacted. Run Tools/TagOfChaos/Maps/Compact {map}.");

        float half = MapCompactor.NewHalf;
        Transform ground = GameObject.Find("Map/Ground").transform;
        foreach (Transform t in ground)
        {
            Bounds b = t.GetComponent<Collider>().bounds;
            if (t.name.Contains("Terrain_Walkable"))
            {
                Assert.LessOrEqual(b.extents.x, half + 2f, $"{t.name} is wider than the play area.");
                Assert.LessOrEqual(b.extents.z, half + 2f, $"{t.name} is deeper than the play area.");
            }
            else if (t.name.Contains("COL_Boundary"))
            {
                float inner = b.size.x < b.size.z ? Mathf.Min(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)) : Mathf.Min(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
                Assert.That(inner, Is.InRange(half - 0.5f, half + 2f), $"{t.name} inner face {inner:F1} m is not at the play-area edge.");
            }
        }

        Vector3 cookie = GameObject.Find(SceneSpawnPoints.Cookie).transform.position;
        Vector3 monster = GameObject.Find(SceneSpawnPoints.Monster).transform.position;
        foreach (Vector3 p in new[] { cookie, monster })
            Assert.IsTrue(Mathf.Abs(p.x) <= half - 4f && Mathf.Abs(p.z) <= half - 4f, $"{map} spawn {p} is outside the play area.");
        Assert.GreaterOrEqual(new Vector2(monster.x - cookie.x, monster.z - cookie.z).magnitude, MapCompactor.MinSpawnDistance - 0.5f,
            $"{map} spawns are too close.");

        var kill = GameObject.Find("VoidKillZone").GetComponent<BoxCollider>();
        Assert.GreaterOrEqual(kill.size.x, half * 2f, "Kill zone must cover the play area.");
    }

    [TestCaseSource(nameof(Maps))]
    public void MapScene_PassabilityIsClean(string map)
    {
        MapPassabilityCheck.Result result = MapPassabilityCheck.Run(Open(map));
        Assert.IsTrue(result.Clean, result.ToString());
    }

    // 걸을 수 있는 곳이 어떤 점광원 범위의 55% 안에 들어가야 한다(GameFixPlan.md F7 — 가장자리 띠가 너무 어두웠다).
    [TestCaseSource(nameof(Maps))]
    public void MapScene_WalkableGroundIsLit(string map)
    {
        Open(map);
        Physics.SyncTransforms();
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .Where(l => l.type != LightType.Directional && l.enabled && l.gameObject.activeInHierarchy).ToList();
        int total = 0, lit = 0;
        var dark = new List<string>();
        for (int x = -66; x <= 66; x += 6)
        for (int z = -66; z <= 66; z += 6)
        {
            bool ground = Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f).Any(h => h.collider.name.Contains("Terrain_Walkable"));
            if (!ground) continue;
            total++;
            var p = new Vector2(x, z);
            if (lights.Any(l => Vector2.Distance(p, new Vector2(l.transform.position.x, l.transform.position.z)) < l.range * 0.55f)) lit++;
            else dark.Add($"({x},{z})");
        }
        Assert.That(total, Is.GreaterThan(0), $"{map}: no walkable ground found");
        Assert.That(lit / (float)total, Is.GreaterThanOrEqualTo(0.98f), $"{map}: dark cells {dark.Count}/{total}: {string.Join(" ", dark.Take(20))}");
    }

    [TestCaseSource(nameof(Maps))]
    public void MapScene_DoorOpeningsAreWideAndClear(string map)
    {
        Open(map);
        foreach (InteractableDoor door in Object.FindObjectsByType<InteractableDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Transform left = door.transform.Find(MapSceneBuilder.DoorLeafLeft);
            if (left == null) continue; // 오븐 문처럼 문짝이 하나인 문은 건물(랜드마크)과 한 덩어리로 옮긴다
            var leaves = door.GetComponentsInChildren<BoxCollider>();
            Bounds b = leaves[0].bounds;
            foreach (BoxCollider c in leaves) b.Encapsulate(c.bounds);
            float width = Mathf.Max(b.size.x, b.size.z);
            Assert.GreaterOrEqual(width, DoorMinWidth, $"{map} {door.name} opening {width:F2} m is too narrow.");
            Assert.GreaterOrEqual(b.size.y + b.min.y - door.transform.position.y, DoorMinHeight - 0.1f, $"{map} {door.name} is too low.");

            // 문짝 자리(닫힌 문의 면)에 벽·소품이 들어와 있지 않아야 한다
            bool alongX = b.size.x >= b.size.z;
            var halfExtents = new Vector3(alongX ? width * 0.5f - 0.3f : 0.1f, 3f, alongX ? 0.1f : width * 0.5f - 0.3f);
            Vector3 center = new Vector3(b.center.x, door.transform.position.y + 3.6f, b.center.z);
            var own = new HashSet<Collider>(door.GetComponentsInChildren<Collider>());
            Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            string[] blockers = hits.Where(h => !own.Contains(h) && !h.name.Contains("Terrain_Walkable") && h.GetComponentInParent<InteractableDoor>() == null)
                                    .Select(h => h.name).ToArray();
            Assert.IsEmpty(blockers, $"{map} {door.name} opening is blocked by {string.Join(", ", blockers)}.");
        }
    }
}
