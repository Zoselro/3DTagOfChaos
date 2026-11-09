using UnityEngine;

// 술래 수색 폭탄 연출(Request1009Plan.md §68 GP5). 모든 화면: 떨어진 자리에 보라 연기가 둥글게 퍼진다.
// 둘레의 진짜 사람(방장이 고름 — AI는 빠짐)은 CoughCount번 기침한다: 머리 위 회색 연기 + 기침 소리 + 몸이 움찔.
public class SearchBombFx : MonoBehaviour
{
    public const int CoughCount = 3;
    public const float CoughInterval = 0.8f;
    public const float MonsterHearAt = 30f; // 이보다 멀면 술래 화면에서 2D로도 낸다(Cough 3D 최대 35 m)

    private static Vector3 AudioListenerPosition()
    {
        AudioListener l = FindAnyObjectByType<AudioListener>();
        return l != null ? l.transform.position : Vector3.zero;
    }

    private Transform body;
    private float next;
    private int left;

    public static void Burst(Vector3 point, float radius)
    {
        ParticleSystem smoke = EscapeVisuals.CreateParticles("SearchBombSmoke", null, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(1.4f, 2.2f), Speed = new Vector2(radius * 0.5f, radius * 0.9f), Size = new Vector2(1.6f, 3f),
            Color = new Color(0.55f, 0.3f, 0.75f, 0.7f), MaxParticles = 60, EmissionOff = true,
            Shape = ParticleSystemShapeType.Circle, Radius = 0.6f, ShapeRotation = new Vector3(90f, 0f, 0f),
            GrowTo = 2.2f, FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 0.7f, Once = true,
        });
        smoke.transform.position = point + Vector3.up * 0.4f;
        smoke.Emit(50);
        Destroy(smoke.gameObject, 3f);
        GameAudio.PlayAt(SoundId.PortalOpen, point);
    }

    // 이 사람이 기침한다(모든 화면).
    public static void Cough(Transform target, float delay)
    {
        if (target == null) return;
        var go = new GameObject("Cough");
        var fx = go.AddComponent<SearchBombFx>();
        fx.body = target;
        fx.next = Time.time + delay;
        fx.left = CoughCount;
    }

    private void Update()
    {
        if (body == null) { Destroy(gameObject); return; }
        if (Time.time < next) return;
        if (left <= 0) { Destroy(gameObject); return; } // 마지막 움찔이 끝난 뒤
        next = Time.time + CoughInterval;
        left--;
        Vector3 head = body.position + Vector3.up * 1.7f + body.forward * 0.3f;
        ParticleSystem puff = EscapeVisuals.CreateParticles("CoughPuff", null, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(0.6f, 0.9f), Speed = new Vector2(1f, 2f), Size = new Vector2(0.25f, 0.45f),
            Color = new Color(0.8f, 0.8f, 0.8f, 0.8f), MaxParticles = 12, EmissionOff = true,
            Shape = ParticleSystemShapeType.Cone, Angle = 25f, Radius = 0.05f,
            GrowTo = 2f, FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 0.8f, Once = true,
        });
        puff.transform.SetPositionAndRotation(head, Quaternion.LookRotation(body.forward + Vector3.up * 0.3f));
        puff.Emit(8);
        Destroy(puff.gameObject, 1.2f);
        GameAudio.PlayAt(SoundId.Cough, head); // 콜록콜록(35 m까지)
        // 술래 본인은 멀리서도 듣는다(§69 F6) — 3D 소리가 닿지 않는 거리면 화면 소리로 함께.
        if (RoomState.IsLocalMonster() && Vector3.Distance(AudioListenerPosition(), head) > MonsterHearAt) GameAudio.Play(SoundId.Cough);
        StartCoroutineOn(body);
    }

    // 몸이 움찔(살짝 숙였다 폄) — 본인 화면은 카메라가 흔들리지 않게 겉모습 자식만.
    private void StartCoroutineOn(Transform t)
    {
        Transform model = t.childCount > 0 ? t.GetChild(0) : null;
        if (model != null) StartCoroutine(Jolt(model));
    }

    private System.Collections.IEnumerator Jolt(Transform model)
    {
        Quaternion rest = model.localRotation;
        for (float e = 0f; e < 0.3f && model != null; e += Time.deltaTime)
        {
            model.localRotation = rest * Quaternion.Euler(Mathf.Sin(e / 0.3f * Mathf.PI) * 18f, 0f, 0f);
            yield return null;
        }
        if (model != null) model.localRotation = rest;
    }
}
