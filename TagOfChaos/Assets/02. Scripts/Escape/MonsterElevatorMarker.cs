using UnityEngine;

// 술래 엘리베이터 표시(Request1009Plan.md §69 D4 — 22번). 괴물 본인 화면에만: 발판 옆 위아래 화살표 표지판(Resources/Escape/ElevatorSign, Blender PROP_ElevatorSign),
// 빛나는 바닥 고리, 옅은 빛기둥, 같은 층에서 MarkerRange 안이면 화면 방향 표시(벽 너머·화면 밖은 가장자리 화살표). 쿠키·스파이 화면에는 아무것도 없다.
[RequireComponent(typeof(MonsterElevator))]
public class MonsterElevatorMarker : MonoBehaviour
{
    public const string SignPath = "Escape/ElevatorSign";
    private const float RefreshSeconds = 0.5f;
    private const float MarkerRange = 70f;
    private const float FloorGap = 8f;
    private static readonly Color Glow = new Color(1f, 0.2f, 0.25f);

    private static ScreenEdgeMarkers markers; // 엘리베이터 둘이 함께 쓴다
    private static int markersFrame = -1;

    private MonsterElevator elevator;
    private GameObject visuals;
    private float nextRefresh;
    private bool showFor;

    private void Awake() => elevator = GetComponent<MonsterElevator>();

    private void Update()
    {
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + RefreshSeconds;
            showFor = RoomState.IsLocalMonster() && EscapeManager.IsActive;
            if (showFor && visuals == null) Build();
            if (visuals != null && visuals.activeSelf != showFor) visuals.SetActive(showFor);
        }
        UpdateScreenMarker();
    }

    private void UpdateScreenMarker()
    {
        if (markers == null) markers = ScreenEdgeMarkers.Create("ElevatorMarkers", Glow);
        if (markersFrame != Time.frameCount) { markersFrame = Time.frameCount; markers.Begin(); }
        Camera cam = Camera.main;
        if (showFor && cam != null)
        {
            Vector3 me = cam.transform.position, at = transform.position;
            if (Mathf.Abs(me.y - at.y) < FloorGap + 4f && Vector3.Distance(me, at) < MarkerRange) markers.Add(at + Vector3.up * 3.5f);
        }
        markers.End();
    }

    private void Build()
    {
        visuals = new GameObject("ElevatorMarker");
        visuals.transform.SetParent(transform, false);
        var sign = Resources.Load<GameObject>(SignPath);
        if (sign != null)
        {
            Transform s = Instantiate(sign, visuals.transform).transform;
            float r = elevator.InteractionRange + 0.6f;
            s.localPosition = new Vector3(r, 0f, 0f);
            s.localRotation = Quaternion.LookRotation(Vector3.left); // 판이 발판 쪽을 본다
            foreach (Collider c in s.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        else Debug.LogWarning($"[MonsterElevatorMarker] Resources/{SignPath} not found — run TagOfChaos/Escape/Build Elevator Sign Prefab.");

        // 바닥 고리
        var ring = new GameObject("Ring").AddComponent<LineRenderer>();
        ring.transform.SetParent(visuals.transform, false);
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 48;
        ring.widthMultiplier = 0.18f;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.startColor = ring.endColor = Glow;
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2f / 48f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0.06f, Mathf.Sin(a)) * 1f + Vector3.up * 0.06f);
        }
        ring.transform.localScale = new Vector3(elevator.InteractionRange, 1f, elevator.InteractionRange);

        // 옅은 빛기둥
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pillar.GetComponent<Collider>());
        pillar.name = "Pillar";
        pillar.transform.SetParent(visuals.transform, false);
        pillar.transform.localPosition = Vector3.up * 6f;
        pillar.transform.localScale = new Vector3(0.6f, 6f, 0.6f);
        EscapeVisuals.Tint(pillar, new Color(Glow.r, Glow.g, Glow.b, 0.35f), 2f);
        var mr = pillar.GetComponent<Renderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void OnDestroy()
    {
        if (markers != null) { Destroy(markers.gameObject); markers = null; }
    }
}
