using UnityEngine;

// 계속 도는 기계 부품(Request1009Plan.md §41) — 초콜릿 공장 건물의 톱니바퀴·컨베이어 롤러·분쇄 롤러.
// 각도는 시각에서 바로 계산한다(누적하지 않음): 화면 밖에서 건너뛴 프레임이 있어도 어긋나지 않고, 화면마다 같은 자리에 있다.
// 충돌체가 없는 겉모습 부품이라 네트워크와 무관하다. 화면에 보일 때만 돌린다.
public sealed class SpinPart : MonoBehaviour
{
    [SerializeField] private Vector3 axis = Vector3.forward;   // 로컬 축
    [SerializeField] private float degreesPerSecond = 30f;      // 부호 = 방향
    [SerializeField] private Renderer visibility;               // 비어 있으면 자식 렌더러

    private Quaternion rest;

    public Vector3 Axis => axis;
    public float DegreesPerSecond => degreesPerSecond;

    public static float AngleAt(float time, float degreesPerSecond) => Mathf.Repeat(time * degreesPerSecond, 360f);

    private void Awake()
    {
        rest = transform.localRotation;
        if (visibility == null) visibility = GetComponentInChildren<Renderer>();
        axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
    }

    private void Update()
    {
        if (visibility != null && !visibility.isVisible) return;
        transform.localRotation = rest * Quaternion.AngleAxis(AngleAt(Time.time, degreesPerSecond), axis);
    }

#if UNITY_EDITOR
    public void EditorSetup(Vector3 localAxis, float speed) { axis = localAxis; degreesPerSecond = speed; }
#endif
}
