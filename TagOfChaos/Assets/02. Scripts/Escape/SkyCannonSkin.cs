using UnityEngine;

// §91 하늘 대포 모습(사용자 Meshy 대포 6종) — 판마다 서브 맵(아몬드·초콜릿·밀)이 세 섬 자리를 바꿔 앉으므로,
// 이 대포가 선 섬의 테마(출발)와 착지 자리 섬의 테마(도착)를 보고 그 쌍의 모델로 "Model" 자식을 바꾼다.
// 모든 화면이 같은 배치(MapLayoutRandomizer)를 쓰므로 따로 동기화하지 않는다. 바꾼 뒤 SkyCannonView가 포신·해치를 다시 찾는다.
[RequireComponent(typeof(SkyCannonView))]
public class SkyCannonSkin : MonoBehaviour
{
    public enum Theme { Wheat, Almond, Choco }
    public const string ModelChild = "Model";
    public const float CheckInterval = 0.5f;

    // 순서: 밀→아몬드, 밀→초콜릿, 아몬드→밀, 아몬드→초콜릿, 초콜릿→밀, 초콜릿→아몬드
    [SerializeField] private GameObject[] variants = new GameObject[6];

    private SkyCannonView view;
    private int shown = -1;
    private float nextCheck;

    public int ShownVariant => shown;

    public static int VariantIndex(Theme from, Theme to)
    {
        if (from == to) return -1;
        switch (from)
        {
            case Theme.Wheat: return to == Theme.Almond ? 0 : 1;
            case Theme.Almond: return to == Theme.Wheat ? 2 : 3;
            default: return to == Theme.Wheat ? 4 : 5;
        }
    }

    // 서브 맵 이름(SubMap_Almond·SubMap_Chocolate·SubMap_Flour)으로 테마를 안다.
    public static bool TryTheme(string name, out Theme theme)
    {
        theme = Theme.Wheat;
        if (string.IsNullOrEmpty(name)) return false;
        if (name.Contains("Almond")) { theme = Theme.Almond; return true; }
        if (name.Contains("Choco")) { theme = Theme.Choco; return true; }
        if (name.Contains("Flour") || name.Contains("Wheat")) { theme = Theme.Wheat; return true; }
        return false;
    }

    // 그 자리에 가장 가까운(수평) 서브 맵의 테마.
    public static bool TryThemeAt(Vector3 p, out Theme theme)
    {
        theme = Theme.Wheat;
        float best = float.MaxValue;
        bool found = false;
        foreach (SubMapTheme s in Object.FindObjectsByType<SubMapTheme>(FindObjectsSortMode.None))
        {
            if (!TryTheme(s.name, out Theme t)) continue;
            Vector3 d = s.transform.position - p;
            d.y = 0f;
            if (d.sqrMagnitude < best) { best = d.sqrMagnitude; theme = t; found = true; }
        }
        return found;
    }

    private void Awake() => view = GetComponent<SkyCannonView>();

    private void Start() => Refresh();

    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + CheckInterval;
        Refresh();
    }

    public void Refresh()
    {
        if (view == null) view = GetComponent<SkyCannonView>();
        if (!TryThemeAt(transform.position, out Theme from) || !TryThemeAt(view.Landing, out Theme to)) return;
        int v = VariantIndex(from, to);
        if (v < 0 || v == shown || variants == null || v >= variants.Length || variants[v] == null) return;
        Show(v);
    }

    private void Show(int v)
    {
        Transform old = transform.Find(ModelChild);
        GameObject model = Instantiate(variants[v], transform, false);
        model.name = ModelChild;
        if (old != null)
        {
            old.name = ModelChild + "_Old";
            old.gameObject.SetActive(false);
            old.SetParent(null, false); // 포신·해치를 다시 찾을 때 옛 모델이 걸리지 않게
            Destroy(old.gameObject);
        }
        shown = v;
        view.RebindParts();
    }

#if UNITY_EDITOR
    public void EditorSetVariants(GameObject[] prefabs) => variants = prefabs;
    public void EditorShow(int v)
    {
        Transform old = transform.Find(ModelChild);
        var model = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(variants[v], transform);
        model.name = ModelChild;
        if (old != null) Object.DestroyImmediate(old.gameObject);
        shown = v;
    }
#endif
}
