using System.Collections.Generic;
using UnityEngine;

// 진저브레드 탈출 장치: 시계탑 아래 쿠키 유적의 룬 제단 → 포탈(EscapeVisualPlan.md §5.5).
//   평소       : 제단 빛은 희미한 보라, 낡은 등불(Lantern_nn)이 깜빡인다, 돌 유적은 푸른 채움 빛(CaveLight_nn)
//   완성 0~2초 : 제단이 무지개빛으로 빛나며 점점 밝아진다
//   2~4초      : 제단 위에 포탈(무지개 고리 + 소용돌이)이 열린다 → 탈 수 있음
//   탑승        : 쿠키가 포탈 속으로 빨려 들어가(BoardingFx.Suck) 사라진다 — 포탈 안에 떠 있는 인형은 두지 않는다(Request1009Plan.md §18.1)
//   출발        : 포탈이 무지개빛으로 크게 번쩍이며 닫힌다
// 모델 이름 규칙은 escape_devices.py(device_gingerbread)와 같다.
public class RuneAltarSequence : EscapeSequence
{
    private const float GlowSeconds = 2f;
    private const float PortalOpenSeconds = 2f;
    private const float FlashSeconds = 1.2f;
    private const float CloseSeconds = 1.2f;
    private const float HueSpeed = 0.35f;        // 무지개가 한 바퀴 도는 빠르기(초당)
    private const float FlickerSoundGap = 3f;    // 등불 하나가 지직거리는 소리의 최소 간격(초)

    private Transform ring, swirl, mouth;
    private Renderer[] glowRenderers = new Renderer[0];
    private Renderer[] portalRenderers = new Renderer[0];
    private readonly List<Light> lanterns = new List<Light>();
    private readonly List<float> lanternSeed = new List<float>();
    private readonly List<bool> lanternDim = new List<bool>();
    private readonly List<float> lanternSoundAt = new List<float>();
    private Light altarLight;
    private MaterialPropertyBlock block;

    public override float BoardReadySeconds => GlowSeconds + PortalOpenSeconds + 0.2f;
    public override float DepartureSeconds => FlashSeconds + CloseSeconds + 0.4f;
    public override Transform DepartureFocus => mouth != null ? mouth : BoardPoint;
    public override SoundId BoardSound => SoundId.BoardSuck;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        ring = FindDeep(transform, "Portal_Ring");
        swirl = FindDeep(transform, "Portal_Swirl");
        mouth = FindDeep(transform, "Mouth") ?? transform;
        Transform glow = FindDeep(transform, "Altar_Glow");
        if (glow != null) glowRenderers = glow.GetComponentsInChildren<Renderer>(true);
        var portal = new List<Renderer>();
        foreach (Transform t in new[] { ring, swirl })
            if (t != null) portal.AddRange(t.GetComponentsInChildren<Renderer>(true));
        portalRenderers = portal.ToArray();

        for (int i = 0; ; i++)
        {
            Transform anchor = FindDeep(transform, $"Lantern_{i:00}");
            if (anchor == null) break;
            var light = anchor.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 11f;
            light.intensity = 1.6f;
            lanterns.Add(light);
            lanternSeed.Add(i * 1.7f);
            lanternDim.Add(false);
            lanternSoundAt.Add(0f);
        }
        // 돌 유적의 은은한 푸른 채움 빛(2026-10-03) — 깜빡이지 않는다. 길과 돌 색이 보일 만큼만.
        for (int i = 0; ; i++)
        {
            Transform anchor = FindDeep(transform, $"CaveLight_{i:00}");
            if (anchor == null) break;
            var fill = anchor.gameObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.62f, 0.72f, 0.95f);
            fill.range = 17f;
            fill.intensity = 1.3f;
            fill.shadows = LightShadows.None;
        }
        altarLight = new GameObject("AltarLight").AddComponent<Light>();
        altarLight.transform.SetParent(transform, false);
        altarLight.transform.position = mouth.position;
        altarLight.type = LightType.Point;
        altarLight.range = 22f;

        // 소리(SoundPlan.md S6): 완성되면 제단이 울리기 시작(출발 끝까지 반복) + 빛나는 소리 → 포탈 열림 / 출발: 번쩍 → 닫힘
        AddLoop(SoundId.AltarHum, (c, d) => c >= 0f && d < DepartureSeconds, mouth);
        AddCue(SoundId.AltarGlow, CueClock.Completed, 0f, mouth);
        AddCue(SoundId.PortalOpen, CueClock.Completed, GlowSeconds, mouth);
        AddCue(SoundId.PortalFlash, CueClock.Departed, 0f, mouth);
        AddCue(SoundId.PortalClose, CueClock.Departed, FlashSeconds, mouth);
    }

    public override void Tick(EscapeState state, double now)
    {
        float c = state != null ? Since(state.CompletedAt, now) : -1f;
        float d = state != null ? Since(state.DepartedAt, now) : -1f;

        // 낡은 등불: 저마다 다른 박자로 깜빡인다
        for (int i = 0; i < lanterns.Count; i++)
        {
            float n = Mathf.PerlinNoise(Time.time * 3f + lanternSeed[i], lanternSeed[i]);
            bool dim = n < 0.25f;
            lanterns[i].intensity = dim ? 0.3f : 1.2f + n;
            if (dim && !lanternDim[i] && Time.time >= lanternSoundAt[i])
            {
                lanternSoundAt[i] = Time.time + FlickerSoundGap;
                GameAudio.PlayAt(SoundId.LanternFlicker, lanterns[i].transform.position); // 꺼질 듯 지직(가까이서만)
            }
            lanternDim[i] = dim;
        }

        // 제단 빛: 평소 희미한 보라 → 완성되면 무지개빛으로 밝아짐
        float power = c < 0f ? 0f : Mathf.Clamp01(c / GlowSeconds);
        Color rainbow = Color.HSVToRGB(Mathf.Repeat(Time.time * HueSpeed, 1f), 0.75f, 1f);
        Color altar = Color.Lerp(new Color(0.45f, 0.25f, 0.6f), rainbow, power);
        float intensity = Mathf.Lerp(0.6f, 3f, power);
        Tint(glowRenderers, altar, intensity);

        // 포탈: 열림(크기 0 → 1) → 출발 때 크게 번쩍인 뒤 닫힘
        float open = c < GlowSeconds ? 0f : Ease((c - GlowSeconds) / PortalOpenSeconds);
        float flash = d >= 0f ? Mathf.Sin(Mathf.Clamp01(d / FlashSeconds) * Mathf.PI) : 0f;
        if (d >= FlashSeconds) open *= 1f - Ease((d - FlashSeconds) / CloseSeconds);
        bool portalShown = open > 0.01f;
        foreach (Transform t in new[] { ring, swirl })
        {
            if (t == null) continue;
            if (t.gameObject.activeSelf != portalShown) t.gameObject.SetActive(portalShown);
            t.localScale = Vector3.one * (open * (1f + 0.25f * flash));
        }
        if (swirl != null) swirl.localRotation = Quaternion.Euler(Time.time * 120f, 0f, 0f);
        Color portalColor = Color.HSVToRGB(Mathf.Repeat(Time.time * HueSpeed + 0.5f, 1f), 0.6f, 1f);
        Tint(portalRenderers, Color.Lerp(portalColor, Color.white, flash), 1.1f + flash * 3f);

        altarLight.color = Color.Lerp(altar, Color.white, flash);
        altarLight.intensity = power * 1.8f + open * 1.5f + flash * 4f;
        altarLight.range = 20f + flash * 10f;

    }

    public override void OnCookieBoarded(Vector3 from) => BoardingFx.Play(from, mouth.position, BoardingFx.Style.Suck);

    private void Tint(Renderer[] renderers, Color color, float intensity)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * intensity);
            r.SetPropertyBlock(block);
        }
    }
}
