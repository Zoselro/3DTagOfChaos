using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// 부서지는 등급(Request1009Plan.md §66.2) — 값 = 부서질 때까지 맞는 횟수.
public enum DestructibleGrade : byte
{
    Prop = 1,
    Tent = 2,
    Building = 4,
    Stairs = 10, // 진저브레드 시계탑 아래 나선 내리막(§68 GP6)
}

// 부서지는 물체 하나. 번호 = 맵 안 계층 순서(모든 화면이 같다).
public sealed class DestructibleUnit
{
    public int Id;
    public DestructibleGrade Grade;
    public Transform Root;
    public Transform ModelRoot;          // 조각 모델을 맞출 자리(조각 모델을 만든 원본 노드)
    public string FractureName;          // 건물만: 조각 모델 이름(없으면 코드 연출) — 모델은 FracturedModels가 필요할 때 읽는다(§73.11)
    public GameObject FracturePrefab;    // 읽은 조각 모델(읽기 전 null)
    public Bounds Bounds;                // 세계 경계(남기는 부분 제외)
    public Bounds Zone;                  // 안에 있으면 "여기 숨었다"로 보는 구역 = Bounds(+ 계단은 이어진 시계탑 몸체)
    public Bounds Link;                  // 계단과 이어진 부서지지 않는 몸체의 경계
    public bool HasLink;
    public Renderer[] Renderers;         // 무너질 때 숨김(남기는 부분 제외)
    public Collider[] Colliders;         // 무너질 때 끔(남기는 부분 제외)
    public Light[] Lights;
    public int Level;                    // 이 화면에 적용된 맞은 횟수

    public int HitsToBreak => (int)Grade;
    public bool Broken => Level >= HitsToBreak;
    public Vector3 Center => Bounds.center;
}

// 맵에서 부서지는 물체를 규칙(DestructionRulesSO)으로 고른다. 씬이 바뀔 때 한 번 만든다.
public sealed class DestructibleCatalog
{
    private static readonly Regex CopySuffix = new Regex(@"( \(\d+\))$");

    private readonly List<DestructibleUnit> units = new List<DestructibleUnit>();
    private readonly Dictionary<Collider, DestructibleUnit> byCollider = new Dictionary<Collider, DestructibleUnit>();
    private List<Bounds> links = new List<Bounds>();

    public IReadOnlyList<DestructibleUnit> Units => units;
    public int Count => units.Count;

    public DestructibleUnit Get(int id) => id >= 0 && id < units.Count ? units[id] : null;
    public bool TryGet(Collider c, out DestructibleUnit u) => byCollider.TryGetValue(c, out u);

    public static DestructibleCatalog Build(Transform map, DestructionRulesSO rules)
    {
        var catalog = new DestructibleCatalog();
        if (map == null || rules == null) return catalog;
        catalog.links = FindLinks(map, rules);
        var models = new HashSet<string>();
        foreach (string f in rules.FracturedNames)
            if (!string.IsNullOrEmpty(f)) models.Add(f);
        catalog.Scan(map, rules, models, false, 0);
        return catalog;
    }

    public static Transform FindMap(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "Map") return r.transform;
        return null;
    }

    private void Scan(Transform group, DestructionRulesSO rules, HashSet<string> models, bool buildingGroup, int depth)
    {
        foreach (Transform child in group)
        {
            if (depth < 3 && IsContainer(child.name, rules))
            {
                Scan(child, rules, models, buildingGroup || Contains(rules.BuildingContainers, child.name), depth + 1);
                continue;
            }
            if (depth == 0) continue; // Map 바로 아래의 묶음이 아닌 것(바닥·술래방·탈출구·타일·표시)은 고르지 않는다
            TryAdd(child, rules, models, buildingGroup);
        }
    }

    private void TryAdd(Transform root, DestructionRulesSO rules, HashSet<string> models, bool buildingGroup)
    {
        if (StartsWithAny(root.name, rules.KeepUnits) || HasProtected(root)) return;

        var renderers = new List<Renderer>();
        var colliders = new List<Collider>();
        var lights = new List<Light>();
        Collect(root, rules, renderers, colliders, lights);
        if (renderers.Count == 0) return;

        Bounds b = WorldBounds(renderers);

        Transform modelRoot = FindModel(root, models, out string model);
        if (model == null && Mathf.Max(b.size.x, b.size.z) > rules.LumpFootprint) return; // 땅·숲 덩어리

        DestructibleGrade grade = StartsWithAny(root.name, rules.StairsPrefixes) ? DestructibleGrade.Stairs
            : model != null || buildingGroup ? DestructibleGrade.Building
            : StartsWithAny(root.name, rules.TentPrefixes) ? DestructibleGrade.Tent
            : DestructibleGrade.Prop;

        var unit = new DestructibleUnit
        {
            Id = units.Count,
            Grade = grade,
            Root = root,
            ModelRoot = modelRoot != null ? modelRoot : root,
            FractureName = model,
            Bounds = b,
            Renderers = renderers.ToArray(),
            Colliders = colliders.ToArray(),
            Lights = lights.ToArray(),
        };
        if (grade == DestructibleGrade.Stairs) unit.HasLink = NearestLink(b.center, rules.StairsLinkRadius, out unit.Link);
        unit.Zone = ZoneOf(unit);
        units.Add(unit);
        foreach (Collider c in unit.Colliders) byCollider[c] = unit;
    }

    // 켜진 렌더러만으로 잰다(꺼진 렌더러의 bounds는 원점·옛 자리라 상자가 터무니없이 커진다 — 룬 색 변형 등). 켜진 것이 없으면 모두로.
    private static Bounds WorldBounds(IList<Renderer> renderers)
    {
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in renderers)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            Bounds rb = r.bounds;
            if (rb.extents == Vector3.zero) continue; // 방금 켜진 렌더러는 한 프레임 동안 원점의 빈 상자를 돌려준다
            if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
        }
        if (any) return b;
        foreach (Renderer r in renderers)
        {
            Bounds rb = r.bounds;
            if (rb.extents == Vector3.zero) continue;
            if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
        }
        return any ? b : new Bounds(renderers[0].transform.position, Vector3.one);
    }

    // 맵이 준비된 뒤(건물 겉모습 바꾸기 등) 경계 상자를 다시 잰다 — 카탈로그는 씬이 막 켜질 때 만들어 켜진 렌더러가 다를 수 있다.
    public void RefreshBounds()
    {
        foreach (DestructibleUnit u in units)
            if (u.Level == 0 && u.Renderers.Length > 0)
            {
                u.Bounds = WorldBounds(u.Renderers); // 맞기 시작한 뒤에는 그대로(흔들림·숨김)
                u.Zone = ZoneOf(u);
            }
    }

    private static Bounds ZoneOf(DestructibleUnit u)
    {
        Bounds z = u.Bounds;
        if (u.HasLink) z.Encapsulate(u.Link);
        return z;
    }

    // 계단과 이어진 몸체(시계탑) 후보: Map 아래 4단계 안의 이름 앞부분이 같은 노드마다 켜진 렌더러 경계.
    private static List<Bounds> FindLinks(Transform map, DestructionRulesSO rules)
    {
        var list = new List<Bounds>();
        if (string.IsNullOrEmpty(rules.StairsLinkPrefix)) return list;
        foreach (Transform t in map.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(rules.StairsLinkPrefix) || (t.parent != null && t.parent.name.StartsWith(rules.StairsLinkPrefix))) continue;
            var rs = new List<Renderer>(t.GetComponentsInChildren<Renderer>(true));
            if (rs.Count > 0) list.Add(WorldBounds(rs));
        }
        return list;
    }

    private bool NearestLink(Vector3 at, float radius, out Bounds link)
    {
        link = default;
        float best = radius;
        bool found = false;
        foreach (Bounds b in links)
        {
            Vector3 d = b.center - at; d.y = 0f;
            if (d.magnitude > best) continue;
            best = d.magnitude; link = b; found = true;
        }
        return found;
    }

    // 남기는 부분(재료 꽂는 곳 등) 아래는 건너뛴다.
    private static void Collect(Transform t, DestructionRulesSO rules, List<Renderer> renderers, List<Collider> colliders, List<Light> lights)
    {
        if (StartsWithAny(t.name, rules.KeepParts)) return;
        foreach (Renderer r in t.GetComponents<Renderer>()) if (r is MeshRenderer || r is SkinnedMeshRenderer) renderers.Add(r);
        foreach (Collider c in t.GetComponents<Collider>()) colliders.Add(c);
        foreach (Light l in t.GetComponents<Light>()) lights.Add(l);
        foreach (Transform c in t) Collect(c, rules, renderers, colliders, lights);
    }

    // 조각 모델 짝: 물체 뿌리부터 3단계 아래까지 이름(복사 꼬리 " (n)" 뺌)이 같은 첫 노드.
    private static Transform FindModel(Transform root, HashSet<string> models, out string model)
    {
        model = null;
        var queue = new Queue<(Transform t, int d)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var (t, d) = queue.Dequeue();
            string name = CopySuffix.Replace(t.name, "");
            if (models.Contains(name)) { model = name; return t; }
            if (d < 3) foreach (Transform c in t) queue.Enqueue((c, d + 1));
        }
        model = null;
        return null;
    }

    // 게임 진행에 필요한 것(탈출구·엘리베이터·재료 상자)이 들어 있으면 부수지 않는다.
    private static bool HasProtected(Transform root) =>
        root.GetComponentInChildren<EscapeGate>(true) != null
        || root.GetComponentInChildren<MonsterElevator>(true) != null
        || root.GetComponentInChildren<MaterialChest>(true) != null;

    private static bool IsContainer(string name, DestructionRulesSO rules) =>
        Contains(rules.Containers, name) || (!string.IsNullOrEmpty(rules.ContainerPrefix) && name.StartsWith(rules.ContainerPrefix));

    private static bool Contains(string[] list, string name)
    {
        foreach (string s in list) if (s == name) return true;
        return false;
    }

    private static bool StartsWithAny(string name, string[] prefixes)
    {
        foreach (string p in prefixes) if (!string.IsNullOrEmpty(p) && name.StartsWith(p)) return true;
        return false;
    }
}
