using UnityEngine;

// 물풍선에 갇힌 몸 띄우기(Request1009Plan.md §45.7 WB2) — 그 캐릭터의 주인 화면에서만(다른 화면은 위치 동기화로 본다).
// 맞은 자리에서 0.6초 동안 5 m 떠올라 머물고, 풀리면 중력으로 떨어진다. 캐릭터 이동 코드(기절 중 속도 0) 뒤에 속도를 덮어쓴다.
[DefaultExecutionOrder(200)]
public class WaterTrapMotion : MonoBehaviour
{
    public const float LiftHeight = 5f;
    public const float RiseSeconds = 0.6f;

    private Rigidbody body;
    private Vector3 basePosition;
    private float startedAt;
    private float releaseAt;
    private bool hadGravity;

    public static void Begin(GameObject owner, float startTime, float releaseTime)
    {
        var rb = owner.GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic) return;
        WaterTrapMotion m = owner.GetComponent<WaterTrapMotion>();
        if (m == null)
        {
            m = owner.AddComponent<WaterTrapMotion>();
            m.body = rb;
            m.basePosition = rb.position;
            m.hadGravity = rb.useGravity;
        }
        m.startedAt = startTime;
        m.releaseAt = releaseTime;
        rb.useGravity = false;
    }

    // 지금 떠 있어야 하는 높이(0 → 5 m, 순수 계산).
    public static float LiftAt(float elapsed) => LiftHeight * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / RiseSeconds));

    private void FixedUpdate()
    {
        if (body == null) { Destroy(this); return; }
        if (Time.time >= releaseAt)
        {
            body.useGravity = hadGravity; // 풀림: 그 자리에서 떨어진다
            Destroy(this);
            return;
        }
        Vector3 goal = basePosition + Vector3.up * LiftAt(Time.time - startedAt);
        body.linearVelocity = (goal - body.position) / Time.fixedDeltaTime;
    }

    private void OnDestroy()
    {
        if (body != null) body.useGravity = hadGravity;
    }
}
