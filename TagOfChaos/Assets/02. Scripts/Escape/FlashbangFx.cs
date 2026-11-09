using UnityEngine;
using UnityEngine.UI;

// 섬광탄 연출(Request1009Plan.md §68 GP2). 모든 화면: 터지는 자리의 흰 빛(구·점광원)이 번쩍 커졌다 사라진다.
// 맞은 사람 본인 화면: 화면 전체가 하얗게 BlindHold초 덮였다가 남은 시간 동안 서서히 걷힌다(시야 방해).
public class FlashbangFx : MonoBehaviour
{
    public const float BlindHold = 2f;      // 완전히 하얀 시간(§69 B6: 0.8 → 2초)
    public const float BlindSeconds = 5f;   // 흰 화면 전체(사용자 확정: 5초, 기절은 3초 그대로)

    private float age, life;
    private Light flash;
    private Image cover;
    private Renderer ball;
    private float radius;

    public static void Burst(Vector3 point, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "FlashbangBurst";
        go.transform.position = point + Vector3.up * 0.8f;
        EscapeVisuals.Tint(go, Color.white, 6f);
        var fx = go.AddComponent<FlashbangFx>();
        fx.life = 0.45f;
        fx.radius = radius;
        fx.ball = go.GetComponent<Renderer>();
        fx.ball.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fx.flash = go.AddComponent<Light>();
        fx.flash.type = LightType.Point;
        fx.flash.range = radius * 3f;
        fx.flash.intensity = 12f;
        fx.flash.color = new Color(1f, 0.97f, 0.9f);
    }

    // 본인 화면을 seconds 동안 하얗게(앞 BlindHold초는 완전히, 그 뒤 서서히 걷힘).
    public static void Blind(float seconds)
    {
        var root = new GameObject("FlashbangBlind", typeof(Canvas));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // HUD 위
        var img = new GameObject("White", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(root.transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var fx = root.AddComponent<FlashbangFx>();
        fx.cover = img.GetComponent<Image>();
        fx.cover.raycastTarget = false;
        fx.cover.color = Color.white;
        fx.life = Mathf.Max(BlindHold + 0.1f, seconds);
        GameAudio.Play(SoundId.FlashRing); // 귀울림 '삐~' — 맞은 사람 본인에게만(2D)
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (cover != null)
        {
            float k = age <= BlindHold ? 1f : 1f - Mathf.Clamp01((age - BlindHold) / (life - BlindHold));
            cover.color = new Color(1f, 1f, 1f, k * k);
        }
        else
        {
            float t = Mathf.Clamp01(age / life);
            transform.localScale = Vector3.one * Mathf.Lerp(0.5f, radius * 1.4f, Mathf.Sqrt(t));
            if (flash != null) flash.intensity = 12f * (1f - t);
            if (ball != null) ball.enabled = t < 0.7f;
        }
        if (age >= life) Destroy(gameObject);
    }
}
