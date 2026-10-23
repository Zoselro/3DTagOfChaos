using UnityEngine;

// 쿠키가 탈것에 타는 순간의 짧은 연출(EscapeVisualPlan.md §4.3). 진짜 쿠키 몸은 그 순간 숨겨지므로, 쿠키 모양 인형이
// 타는 곳까지 날아간다. Hop = 폴짝 뛰어 자리에 들어감(로켓·롤러코스터·기차), Suck = 빛 속으로 빨려 들어가며 작아짐(오븐·포탈).
// 인형 모델은 EscapeCatalogSO.PassengerModel(없으면 캡슐). 연출이 끝나면 스스로 사라진다.
public class BoardingFx : MonoBehaviour
{
    public enum Style { Hop, Suck }

    private const float HopSeconds = 0.7f;
    private const float SuckSeconds = 0.9f;

    private Vector3 from;
    private Vector3 to;
    private Style style;
    private float started;
    private Light glow;

    public static void Play(Vector3 from, Vector3 to, Style style)
    {
        var go = new GameObject("BoardingFx");
        var fx = go.AddComponent<BoardingFx>();
        fx.from = from;
        fx.to = to;
        fx.style = style;
        fx.started = Time.time;

        EscapeVisuals.CreatePassenger(go.transform, null);
        if (style == Style.Suck)
        {
            fx.glow = go.AddComponent<Light>();
            fx.glow.type = LightType.Point;
            fx.glow.color = new Color(1f, 0.95f, 0.8f);
            fx.glow.range = 6f;
            fx.glow.intensity = 0f;
        }
        go.transform.position = from;
    }

    private void Update()
    {
        float duration = style == Style.Hop ? HopSeconds : SuckSeconds;
        float t = Mathf.Clamp01((Time.time - started) / duration);
        Vector3 p = Vector3.Lerp(from, to, style == Style.Hop ? t : t * t); // 빨려 들어갈 때는 점점 빨라진다
        if (style == Style.Hop) p.y += Mathf.Sin(t * Mathf.PI) * 1.5f;
        transform.position = p;

        Vector3 flat = to - from;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat.normalized);
        float scale = style == Style.Hop ? Mathf.Lerp(1f, 0.85f, t) : Mathf.Lerp(1f, 0.05f, t * t);
        transform.localScale = Vector3.one * scale;
        if (glow != null) glow.intensity = Mathf.Sin(t * Mathf.PI) * 4f;

        if (t >= 1f) Destroy(gameObject);
    }
}
