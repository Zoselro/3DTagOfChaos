using System;
using UnityEngine;

// 도구가 날아가는 모습(모든 화면). 물풍선(Request1009Plan.md §45.7 WB1·WB5): 던지는 사람 손에 머물다가 손을 떠나는 순간(맞는 시각 − 비행 시간)부터
// 위로 솟는 포물선(꼭대기 = 시작·끝 중 높은 쪽 + max(1.5 m, 거리 × 0.35))으로 날아가, 방장이 정한 맞는 시각(hitAt)에 대상 몸 가운데에 닿는다.
// 대상이 움직이면 끝점이 따라간다(유도). 스턴건은 쏜 사람에게서 맞은 곳까지 잠깐 빛줄기를 그린다.
public class ToolShotFx : MonoBehaviour
{
    private const float BeamSeconds = 0.18f;
    private const float SmallBalloon = 0.42f; // 날아가는 물풍선 지름

    private Vector3 from, to;
    private float duration, age;
    private Action onArrive;
    private LineRenderer beam;
    private Transform thrower, target;
    private float releaseAt, hitTime;
    private bool released;

    // 손 위치(오른손 뼈, 없으면 몸 앞).
    public static Vector3 HandPosition(Transform body)
    {
        var animator = body.GetComponent<Animator>();
        Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        return hand != null ? hand.position : body.position + Vector3.up * 1.2f + body.forward * 0.4f;
    }

    public static void ThrowBalloon(Transform thrower, Transform target, Vector3 point, double hitAt, Action onArrive)
    {
        var go = new GameObject("WaterBalloonShot");
        var fx = go.AddComponent<ToolShotFx>();
        fx.thrower = thrower;
        fx.target = target;
        fx.to = point + Vector3.up;
        fx.onArrive = onArrive;
        fx.hitTime = Time.time + (float)(hitAt - Photon.Pun.PhotonNetwork.Time);
        Vector3 start = thrower != null ? HandPosition(thrower) : point;
        fx.releaseAt = fx.hitTime - ToolAim.FlightSeconds(start, fx.TargetPoint());
        fx.from = start;
        WaterBubbleView.CreateSmall(go.transform, SmallBalloon);
        go.transform.position = start;
    }

    private Vector3 TargetPoint()
    {
        if (target == null) return to;
        return WaterBubbleView.BodyBounds(target).center;
    }

    public static void Beam(Vector3 from, Vector3 to, Color color)
    {
        var go = new GameObject("StunBeam");
        var fx = go.AddComponent<ToolShotFx>();
        fx.duration = BeamSeconds;
        fx.beam = go.AddComponent<LineRenderer>();
        fx.beam.positionCount = 2;
        fx.beam.SetPosition(0, from);
        fx.beam.SetPosition(1, to);
        fx.beam.widthMultiplier = 0.12f;
        fx.beam.material = EscapeVisuals.SoftParticleMaterial();
        fx.beam.startColor = fx.beam.endColor = color;
    }

    private void Update()
    {
        if (beam != null)
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            Color c = beam.startColor;
            c.a = 1f - t;
            beam.startColor = beam.endColor = c;
            if (t >= 1f) Destroy(gameObject);
            return;
        }
        if (Time.time < releaseAt)
        {
            if (thrower != null) from = HandPosition(thrower); // 손을 떠나기 전에는 손에 붙어 있다
            transform.position = from;
            return;
        }
        if (!released) released = true;
        Vector3 end = TargetPoint();
        float flight = Mathf.Max(0.05f, hitTime - releaseAt);
        float k = Mathf.Clamp01((Time.time - releaseAt) / flight);
        transform.position = ToolAim.ArcPoint(from, end, ToolAim.ApexHeight(from, end), k);
        transform.Rotate(0f, 0f, 360f * Time.deltaTime, Space.Self);
        if (k < 1f) return;
        onArrive?.Invoke();
        onArrive = null;
        Destroy(gameObject);
    }
}
