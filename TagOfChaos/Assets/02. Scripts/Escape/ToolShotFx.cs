using UnityEngine;

// 도구가 쓰이는 모습(모든 화면). 스턴건은 쏜 사람에게서 맞은 곳까지 잠깐 빛줄기를 그린다.
// 물풍선·섬광탄이 포물선으로 날아가던 옛 연출(ThrowBalloon·ThrowCanister)은 투사체(ProjectileSystem, §70.3)로 바뀌어 지웠다(§74 P1).
public class ToolShotFx : MonoBehaviour
{
    private const float BeamSeconds = 0.18f;

    private float duration, age;
    private LineRenderer beam;

    // 손 위치(오른손 뼈, 없으면 몸 앞).
    public static Vector3 HandPosition(Transform body)
    {
        var animator = body.GetComponent<Animator>();
        Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        return hand != null ? hand.position : body.position + Vector3.up * 1.2f + body.forward * 0.4f;
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
        if (beam == null) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        Color c = beam.startColor;
        c.a = 1f - t;
        beam.startColor = beam.endColor = c;
        if (t >= 1f) Destroy(gameObject);
    }
}
