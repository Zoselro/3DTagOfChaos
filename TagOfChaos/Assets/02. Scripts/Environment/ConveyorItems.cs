using UnityEngine;

// 컨베이어 위를 지나가는 초콜릿 조각(Request1009Plan.md §41) — 분쇄기 건물 바깥 벨트, 공장 건물 안 생산 줄.
// 시작할 때 본보기(template, 꺼 둠)를 count개 복제해 start→end 사이에 같은 간격으로 놓고, 끝에 닿으면 처음으로 돌아간다.
// 위치는 시각에서 바로 계산한다(누적 없음). 충돌체가 없는 겉모습이라 네트워크와 무관하다.
public sealed class ConveyorItems : MonoBehaviour
{
    [SerializeField] private Transform template;
    [SerializeField] private Transform start;
    [SerializeField] private Transform end;
    [SerializeField, Min(1)] private int count = 4;
    [SerializeField, Min(0.01f)] private float speed = 1.2f;     // m/s
    [SerializeField] private float edgeShrink = 0.15f;            // 처음·끝에서 작아졌다 커지는 구간(경로 비율) — 갑자기 나타나지 않게

    private Transform[] items;
    private Renderer[] renderers;  // 조각마다 하나 — 하나라도 보이면 움직인다
    private Vector3[] restScale;
    private float length = 1f;

    public int Count => items != null ? items.Length : 0;

    // 경로 위 비율(0~1): 조각 i가 시각 t에 있는 자리
    public static float PhaseAt(int index, int count, float time, float speed, float length) =>
        Mathf.Repeat(index / (float)count + time * speed / Mathf.Max(0.01f, length), 1f);

    private void Awake()
    {
        if (template == null || start == null || end == null) { enabled = false; return; }
        template.gameObject.SetActive(false);
        length = Vector3.Distance(start.position, end.position);
        items = new Transform[count];
        restScale = new Vector3[count];
        renderers = new Renderer[count];
        for (int i = 0; i < count; i++)
        {
            Transform t = Instantiate(template, transform);
            t.name = $"{template.name}_{i}";
            t.gameObject.SetActive(true);
            t.rotation = template.rotation * Quaternion.Euler(0f, i * 47f, 0f); // 조각마다 조금씩 다르게 놓인다
            items[i] = t;
            restScale[i] = t.localScale;
            renderers[i] = t.GetComponentInChildren<Renderer>();
        }
        Place(Time.time);
    }

    private void Update()
    {
        if (!AnyVisible()) return;
        Place(Time.time);
    }

    private bool AnyVisible()
    {
        foreach (Renderer r in renderers) if (r != null && r.isVisible) return true;
        return false;
    }

    private void Place(float time)
    {
        Vector3 a = start.position, b = end.position;
        for (int i = 0; i < items.Length; i++)
        {
            float p = PhaseAt(i, items.Length, time, speed, length);
            items[i].position = Vector3.Lerp(a, b, p);
            float s = Mathf.Clamp01(Mathf.Min(p, 1f - p) / Mathf.Max(0.001f, edgeShrink));
            items[i].localScale = restScale[i] * s;
        }
    }

#if UNITY_EDITOR
    public void EditorSetup(Transform item, Transform from, Transform to, int number, float metresPerSecond)
    {
        template = item; start = from; end = to; count = number; speed = metresPerSecond;
    }
#endif
}
