using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// 탈출 재료·칸 전수 검사를 시험으로 옮긴 것(Request1009Plan.md §22.2·§22.7). 모델·씬·레시피가 바뀌어 쿠키가 닿지 못하는 칸이 생기거나
// 스파이 로켓 재료가 빠지는 판이 생기면 바로 실패한다. 예전 공장 원유 칸(기관차 지붕 2.9 m)이 이런 경우였다.
public class EscapeReachabilityTests
{
    private static readonly string[] Maps = { "CandyForest", "GingerbreadVillage", "ChocolateFactory", "CursedCandyCarnival", "HauntedBakery" };

    // 쿠키 3명(4·5명 방)부터 최대 5명까지 — 인원표(GameSettings)의 쿠키 수 범위.
    private const int MinCookies = 3;
    private const int Seeds = 1000;

    // ---- 칸·상자 자리에 닿는가(씬) ----

    [TestCaseSource(nameof(Maps))]
    public void DeviceSlotsAndChests_AreReachableFromWalkableGround(string map)
    {
        EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);
        Physics.SyncTransforms();
        EscapeDevice device = Object.FindFirstObjectByType<EscapeDevice>(FindObjectsInactive.Include);
        EscapeManager manager = Object.FindFirstObjectByType<EscapeManager>(FindObjectsInactive.Include);
        Assert.NotNull(device, map);
        Assert.NotNull(manager, map);

        var failures = new List<string>();
        int slots = 0;
        for (int i = 0; i < 16; i++)
        {
            if (!device.TryAnchorPosition(i, out Vector3 p)) continue;
            slots++;
            float gap = EscapeReach.BestStandHeightGap(p, device.InteractionRange, EscapeReach.ReferenceGround(p));
            if (gap > EscapeReach.MaxHeightDifference) failures.Add($"Slot_{i:00} at {p} (height gap {gap:F2} m)");
        }
        Assert.Greater(slots, 0, $"{map}: device has model slot anchors");

        Transform[] chests = ChestAnchors(manager);
        Assert.Greater(chests.Length, 0, $"{map}: chest anchors");
        float chestRange = ChestRange();
        for (int i = 0; i < chests.Length; i++)
        {
            Vector3 p = chests[i].position;
            float gap = EscapeReach.BestStandHeightGap(p, chestRange, EscapeReach.ReferenceGround(p));
            if (gap > EscapeReach.MaxHeightDifference) failures.Add($"Chest {i} at {p} (height gap {gap:F2} m)");
        }
        Assert.IsEmpty(failures, $"{map}: unreachable spots:\n" + string.Join("\n", failures));
    }

    // 빈 상자가 없을 때 재료를 놓는 장치 옆 바닥(B5)도 쿠키가 서는 바닥 높이이고, 장치 칸 근처다.
    [TestCaseSource(nameof(Maps))]
    public void DeviceDropPoint_IsOnWalkableGroundNextToTheDevice(string map)
    {
        EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);
        Physics.SyncTransforms();
        EscapeManager manager = Object.FindFirstObjectByType<EscapeManager>(FindObjectsInactive.Include);
        EscapeDevice device = manager.Device;
        EscapeRecipeSO recipe = EscapeCatalogSO.Current.RecipeFor(map);
        var slots = RecipePlanner.Plan(recipe, MinCookies, new System.Random(3), GameSettings.Current.MaxCookieCount)
            .Select(p => new EscapeState.Slot { Accepts = p.Accepts, Anchor = p.Anchor, ItemIndex = -1 }).ToList();
        for (int roll = 0; roll < slots.Count; roll++)
        {
            Vector3 drop = manager.DeviceDropPoint(slots, roll);
            float reference = EscapeReach.ReferenceGround(drop);
            Assert.LessOrEqual(Mathf.Abs(drop.y - reference), EscapeReach.GroundTolerance, $"{map} roll {roll}: {drop} is on the ground (ref {reference:F2})");
            Assert.LessOrEqual(device.DistanceTo(drop), device.InteractionRange, $"{map} roll {roll}: {drop} is next to the device");
        }
    }

    // ---- 스파이 로켓 재료(레시피, Q1 A) ----

    [TestCaseSource(nameof(Maps))]
    public void RocketMaterials_AreAlwaysAmongTheNeededSlots(string map)
    {
        EscapeRecipeSO recipe = EscapeCatalogSO.Current.RecipeFor(map);
        List<string[]> cover = recipe.RocketSlots.Select(r => r.accepts.Where(a => a != null).Select(a => a.ItemId).ToArray()).ToList();
        Assert.IsNotEmpty(cover, map);
        int capacity = GameSettings.Current.MaxCookieCount;
        for (int cookies = MinCookies; cookies <= capacity; cookies++)
        {
            for (int seed = 0; seed < Seeds; seed++)
            {
                List<RecipePlanner.PlannedSlot> slots = RecipePlanner.Plan(recipe, cookies, new System.Random(seed), capacity, cover);
                List<RecipePlanner.PlannedSlot> needed = slots.Where(s => s.Required).ToList();
                Assert.AreEqual(cookies, needed.Count, $"{map} cookies {cookies} seed {seed}: needed = cookies");
                Assert.IsTrue(CoversEach(needed, cover), $"{map} cookies {cookies} seed {seed}: every rocket slot has its own needed slot");
                if (recipe.Mode == RecipeMode.Slots)
                    foreach (int anchor in FixedAnchors(recipe))
                        Assert.IsTrue(needed.Any(s => s.Anchor == anchor), $"{map} cookies {cookies} seed {seed}: fixed slot {anchor} stays needed");
            }
        }
        LogAssert.NoUnexpectedReceived(); // RecipePlanner의 "로켓 몫이 없음" 경고가 한 번도 없어야 한다
    }

    // Q4 다(2026-10-12): 공장 3쿠키에서도 원유·기어를 모두 넣을 수 있게 쿠키바퀴는 고정이 아니다 — 에셋과 만드는 도구 둘 다.
    [Test]
    public void ChocolateFactory_OnlyTheMacaronWheelIsFixed()
    {
        EscapeRecipeSO recipe = EscapeCatalogSO.Current.RecipeFor("ChocolateFactory");
        Assert.AreEqual(1, recipe.Groups.Single(g => g.item.ItemId == "MacaronWheel").fixedRequired);
        Assert.AreEqual(0, recipe.Groups.Single(g => g.item.ItemId == "CookieWheel").fixedRequired);
        string builder = System.IO.File.ReadAllText("Assets/Editor/Escape/EscapeAssetsBuilder.cs");
        StringAssert.Contains("G(cookieWheel, 2, 0)", builder, "the data builder must not restore the old fixed cookie wheel");

        for (int seed = 0; seed < 50; seed++)
        {
            var needed = RecipePlanner.Plan(recipe, MinCookies, new System.Random(seed), GameSettings.Current.MaxCookieCount,
                    recipe.RocketSlots.Select(r => r.accepts.Select(a => a.ItemId).ToArray()).ToList())
                .Where(s => s.Required).Select(s => s.Accepts[0]).OrderBy(id => id).ToArray();
            CollectionAssert.AreEqual(new[] { "ChocolateOil", "Gear", "MacaronWheel" }, needed, $"seed {seed}");
        }
    }

    // 로켓 몫이 없는 기존 호출은 예전과 같은 결과를 낸다(같은 시드 → 같은 칸).
    [Test]
    public void Planner_WithoutRocketCover_IsUnchangedByTheNewArgument()
    {
        foreach (string map in Maps)
        {
            EscapeRecipeSO recipe = EscapeCatalogSO.Current.RecipeFor(map);
            for (int seed = 0; seed < 20; seed++)
            {
                var a = RecipePlanner.Plan(recipe, 4, new System.Random(seed), 5);
                var b = RecipePlanner.Plan(recipe, 4, new System.Random(seed), 5, null);
                CollectionAssert.AreEqual(a.Select(s => s.Anchor + ":" + s.Required), b.Select(s => s.Anchor + ":" + s.Required), $"{map} seed {seed}");
            }
        }
    }

    // ---- 도우미 ----

    private static bool CoversEach(List<RecipePlanner.PlannedSlot> needed, List<string[]> cover)
    {
        // 로켓 칸마다 서로 다른 필요 칸 하나(작은 수라 모든 배정을 시도한다)
        return Assign(needed, cover, 0, new bool[needed.Count]);
    }

    private static bool Assign(List<RecipePlanner.PlannedSlot> needed, List<string[]> cover, int k, bool[] used)
    {
        if (k == cover.Count) return true;
        for (int i = 0; i < needed.Count; i++)
        {
            if (used[i] || !needed[i].Accepts.Any(a => cover[k].Contains(a))) continue;
            used[i] = true;
            if (Assign(needed, cover, k + 1, used)) return true;
            used[i] = false;
        }
        return false;
    }

    private static List<int> FixedAnchors(EscapeRecipeSO recipe)
    {
        var list = new List<int>();
        int index = 0;
        foreach (EscapeRecipeSO.SlotGroup g in recipe.Groups)
        {
            for (int k = 0; k < g.fixedRequired; k++) list.Add(index + k);
            index += g.slotCount;
        }
        return list;
    }

    private static Transform[] ChestAnchors(EscapeManager manager)
    {
        SerializedProperty prop = new SerializedObject(manager).FindProperty("chestAnchors");
        var list = new List<Transform>();
        for (int i = 0; i < prop.arraySize; i++) list.Add((Transform)prop.GetArrayElementAtIndex(i).objectReferenceValue);
        return list.Where(t => t != null).ToArray();
    }

    private static float ChestRange()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            MaterialChest chest = go != null ? go.GetComponentInChildren<MaterialChest>(true) : null;
            if (chest != null) return chest.InteractionRange;
        }
        return 2.2f;
    }
}

// 쿠키가 서서 E를 누를 수 있는지 판정(CharacterInteractor와 같은 조건: 수평 범위 + 높이 차 ≤ 2.5 m). 설 자리는 걸을 수 있는 바닥
// (주변 5~8 m 바닥 높이의 중앙값 ±0.7 m — 기관차 지붕·차체 위 같은 곳은 제외)이고 캡슐이 들어갈 공간이 있어야 한다.
public static class EscapeReach
{
    public const float MaxHeightDifference = 2.5f;
    public const float GroundTolerance = 0.7f;
    private const float CapsuleRadius = 0.4f;
    private const float CapsuleHeight = 2f;
    private const float Step = 0.25f;

    public static float ReferenceGround(Vector3 p)
    {
        var heights = new List<float>();
        for (int a = 0; a < 24; a++)
            for (float r = 5f; r <= 8f; r += 1.5f)
            {
                Vector3 q = p + Quaternion.Euler(0f, a * 15f, 0f) * Vector3.forward * r;
                if (TryGround(q, p.y + 3f, out float g)) heights.Add(g);
            }
        heights.Sort();
        return heights.Count > 0 ? heights[heights.Count / 2] : p.y;
    }

    // 범위 안의 설 수 있는 자리 중 목표와의 높이 차가 가장 작은 값(없으면 MaxValue).
    public static float BestStandHeightGap(Vector3 p, float range, float reference)
    {
        float best = float.MaxValue;
        for (float x = -range; x <= range; x += Step)
            for (float z = -range; z <= range; z += Step)
            {
                if (x * x + z * z > range * range) continue;
                var q = new Vector3(p.x + x, 0f, p.z + z);
                if (!TryGround(q, p.y + 3f, out float g) || Mathf.Abs(g - reference) > GroundTolerance) continue;
                var foot = new Vector3(q.x, g, q.z);
                if (Physics.CheckCapsule(foot + Vector3.up * (CapsuleRadius + 0.1f), foot + Vector3.up * (CapsuleHeight - CapsuleRadius), CapsuleRadius,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                best = Mathf.Min(best, Mathf.Abs(p.y - g));
            }
        return best;
    }

    private static bool TryGround(Vector3 xz, float fromY, out float y)
    {
        y = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(xz.x, fromY, xz.z), Vector3.down, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (h.normal.y >= 0.7f && h.point.y > y) y = h.point.y;
        return !float.IsNegativeInfinity(y);
    }
}
