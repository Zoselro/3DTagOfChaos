using UnityEngine;

// 베이커리 탈출 장치: 마법 오븐 = 작동하는 기계(EscapeVisualPlan.md §5.3).
//   완성 0~3초 : 톱니가 돌기 시작하고 피스톤이 오르내리며 굴뚝에서 연기, 오븐 불빛이 밝아진다
//   3~6초      : 맵의 둥근 오븐 문(HAU_MagicOven_Door)이 옆으로 굴러가며 서서히 열리고, 안이 하얗게 빛난다 → 탈 수 있음
//   탑승        : 쿠키가 경사 발판을 올라 문 앞에서 타면 빛 속으로 빨려 들어간다(BoardingFx.Suck)
//   출발        : 문이 굴러 돌아와 닫히고, 오븐 안에서 큰 빛이 번쩍인다
// 소리: 완성 전에는 고장 난 오븐 반복음(OvenBroken — 걸렸다 꺼지는 모터·헛도는 톱니·스파크, Request1009Plan.md §16.7),
// 완성되는 순간 멈추고 지금 소리(톱니 반복음·피스톤·문)로 넘어간다.
// 문은 아무나 열 수 없는 일반 물체다(MapSceneBuilder). 모델 이름 규칙은 escape_devices.py(device_bakery)와 같다.
public class OvenSequence : EscapeSequence
{
    private const string DoorName = "HAU_MagicOven_Door";
    private const float WarmupSeconds = 3f;
    private const float DoorSeconds = 3f;
    private const float CloseSeconds = 1.6f;
    private const float FlashAt = 1.8f;
    private const float FlashSeconds = 1f;
    private const float RollDistance = 16f;      // 문이 옆으로 굴러가는 거리(m)
    private const float GearSpeed = 70f;         // 큰 톱니 각속도(도/초)
    private const float GearRatio = 12f / 8f;    // 큰 톱니 12개 : 작은 톱니 8개
    public const float BrightnessScale = 0.5f;   // 문이 열렸을 때 너무 밝다는 의견으로 빛 전체를 절반으로(Request1009Plan.md §6)
    private const int FlashParticles = 60;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Tooltip("맵의 둥근 오븐 문(HAU_MagicOven_Door). 맵 도구(EscapeMapSetup)가 채운다 — 비어 있으면 이름으로 찾는다(F-21).")]
    [SerializeField] private Transform mapDoor;

    private Transform gearA, gearB, piston, needle, ovenLight, mouth, smokePoint;
    private Quaternion gearAHome, gearBHome, needleHome;
    private Vector3 pistonHome;
    private Transform doorPivot;
    private Vector3 doorPivotHome;
    private Quaternion doorPivotRotation;
    private float doorRadius = 7.6f;
    private Light glow;
    private ParticleSystem smoke, flash;
    private float gearAngle;
    private bool flashed;

    public override float BoardReadySeconds => WarmupSeconds + DoorSeconds + 0.2f;
    public override float DepartureSeconds => FlashAt + FlashSeconds + 0.6f;
    public override Transform DepartureFocus => mouth != null ? mouth : BoardPoint;
    public override SoundId BoardSound => SoundId.BoardSuck;

    private void Awake()
    {
        gearA = FindDeep(transform, "Gear_A");
        gearB = FindDeep(transform, "Gear_B");
        piston = FindDeep(transform, "Piston_Rod");
        needle = FindDeep(transform, "Gauge_Needle");
        ovenLight = FindDeep(transform, "Oven_Light");
        DimOvenLight();
        mouth = FindDeep(transform, "Mouth") ?? transform;
        smokePoint = FindDeep(transform, "Smoke");
        if (gearA != null) gearAHome = gearA.localRotation;
        if (gearB != null) gearBHome = gearB.localRotation;
        if (needle != null) needleHome = needle.localRotation;
        if (piston != null) pistonHome = piston.localPosition;
        SetupDoor();

        glow = new GameObject("OvenGlow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.position = mouth.position + transform.forward * 4f;
        glow.type = LightType.Point;
        glow.range = 30f;
        glow.intensity = 0f;

        // 소리(SoundPlan.md S6): 기계가 도는 동안 톱니 반복음, 시동 때 피스톤 칙, 문 구르는 소리(열림·닫힘), 출발 때 번쩍
        AddLoop(SoundId.OvenBroken, (c, d) => c < 0f, gearA); // 완성 전(c = -1)
        AddLoop(SoundId.OvenGears, (c, d) => c >= 0f && d < DepartureSeconds, gearA);
        AddCue(SoundId.OvenPiston, CueClock.Completed, 0f, piston);
        AddCue(SoundId.OvenDoorRoll, CueClock.Completed, WarmupSeconds, doorPivot);
        AddCue(SoundId.OvenDoorRoll, CueClock.Departed, 0f, doorPivot);
        AddCue(SoundId.OvenFlash, CueClock.Departed, FlashAt, mouth);
    }

    // 문은 경첩이 원점인 맵 물체다. 굴러가게 하려고 둥근 문 한가운데에 축(피벗)을 만들어 그 아래로 옮긴다.
    private void SetupDoor()
    {
        Transform door = mapDoor;
        if (door == null)
        {
            GameObject found = GameObject.Find(DoorName);
            if (found == null) { Debug.LogWarning($"[OvenSequence] Door '{DoorName}' not found — the oven opens without its door."); return; }
            door = found.transform;
        }
        var renderer = door.GetComponent<Renderer>();
        Vector3 center = renderer != null ? renderer.bounds.center : door.position;
        if (renderer != null) doorRadius = Mathf.Max(1f, renderer.bounds.extents.y);
        doorPivot = new GameObject("MagicOven_DoorPivot").transform;
        doorPivot.SetParent(door.parent, false);
        doorPivot.position = center;
        doorPivot.rotation = Quaternion.identity;
        door.SetParent(doorPivot, true);
        doorPivotHome = doorPivot.position;
        doorPivotRotation = doorPivot.rotation;
    }

    public override void Tick(EscapeState state, double now)
    {
        float c = state != null ? Since(state.CompletedAt, now) : -1f;
        float d = state != null ? Since(state.DepartedAt, now) : -1f;

        // 기계: 완성되면 3초에 걸쳐 힘이 오른다
        float power = c < 0f ? 0f : Mathf.Clamp01(c / WarmupSeconds);
        gearAngle += GearSpeed * power * Time.deltaTime;
        if (gearA != null) gearA.localRotation = gearAHome * Quaternion.Euler(0f, 0f, gearAngle);
        if (gearB != null) gearB.localRotation = gearBHome * Quaternion.Euler(0f, 0f, -gearAngle * GearRatio + 11f);
        if (piston != null) piston.localPosition = pistonHome + Vector3.up * (power * 0.9f * (0.5f + 0.5f * Mathf.Sin(Time.time * 7f)));
        if (needle != null) needle.localRotation = needleHome * Quaternion.Euler(0f, 0f, Mathf.Lerp(70f, -60f, power) + power * 6f * Mathf.Sin(Time.time * 23f));
        UpdateSmoke(power);

        // 문: 3초 뒤부터 3초 동안 옆으로 굴러 열리고, 출발하면 굴러 돌아와 닫힌다
        float open = c < WarmupSeconds ? 0f : Ease((c - WarmupSeconds) / DoorSeconds);
        if (d >= 0f) open *= 1f - Ease(d / CloseSeconds);
        if (doorPivot != null)
        {
            float roll = RollDistance * open;
            doorPivot.position = doorPivotHome + transform.right * roll;
            doorPivot.rotation = Quaternion.AngleAxis(-roll / doorRadius * Mathf.Rad2Deg, transform.forward) * doorPivotRotation;
        }

        // 빛: 불이 밝아짐 → 문이 열리며 하얀 빛 → 출발하면 크게 번쩍
        if (ovenLight != null)
        {
            bool lit = open > 0.02f;
            if (ovenLight.gameObject.activeSelf != lit) ovenLight.gameObject.SetActive(lit);
            if (lit) ovenLight.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, open);
        }
        float burst = d >= FlashAt ? Mathf.Sin(Mathf.Clamp01((d - FlashAt) / FlashSeconds) * Mathf.PI) : 0f;
        glow.color = Color.Lerp(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.97f, 0.9f), Mathf.Max(open, burst));
        glow.intensity = (power * 2.5f + open * 4f + burst * 14f) * BrightnessScale;
        glow.range = 30f + burst * 25f;
        if (d >= FlashAt && !flashed) { flashed = true; Flash(); }
        if (d < 0f) flashed = false;
    }

    // 문 안의 하얀 빛(Oven_Light): 재질 ME_Glow_White는 다른 탈출 장치와 공유하므로 이 렌더러만 MaterialPropertyBlock으로 발광을 줄인다.
    private void DimOvenLight()
    {
        if (ovenLight == null) return;
        var renderer = ovenLight.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(EmissionColorId)) return;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(EmissionColorId, renderer.sharedMaterial.GetColor(EmissionColorId) * BrightnessScale);
        renderer.SetPropertyBlock(block);
    }

    public override void OnCookieBoarded(Vector3 from) => BoardingFx.Play(from, mouth.position, BoardingFx.Style.Suck);

    private void UpdateSmoke(float power)
    {
        if (smokePoint == null) return;
        if (power > 0.2f && smoke == null) smoke = CreateSmoke();
        if (smoke == null) return;
        var emission = smoke.emission;
        emission.rateOverTime = 14f * power;
    }

    // 굴뚝 연기: 위로 천천히 오르며 커지는 퍼프.
    private ParticleSystem CreateSmoke()
    {
        ParticleSystem ps = EscapeVisuals.CreateParticles("MachineSmoke", smokePoint, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(2.5f, 2.5f), Speed = new Vector2(2.2f, 2.2f), Size = new Vector2(0.9f, 1.6f),
            Color = new Color(0.7f, 0.66f, 0.75f, 0.55f), MaxParticles = 80,
            Shape = ParticleSystemShapeType.Cone, Angle = 12f, Radius = 0.4f, GrowTo = 2.2f,
            FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 0.55f,
        });
        ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ps.Play();
        return ps;
    }

    // 출발 순간 오븐 앞에서 하얀 빛 가루가 터진다.
    private void Flash()
    {
        if (flash == null)
        {
            flash = EscapeVisuals.CreateParticles("OvenFlash", transform, new EscapeVisuals.ParticleStyle
            {
                Lifetime = new Vector2(1.2f, 1.2f), Speed = new Vector2(4f, 11f), Size = new Vector2(0.3f, 0.8f),
                Color = new Color(1f, 0.97f, 0.85f, 1f), MaxParticles = 150, Rate = 0f, Manual = true,
                Shape = ParticleSystemShapeType.Sphere, Radius = 3f,
                FadeFrom = Color.white, FadeTo = new Color(1f, 0.8f, 0.5f), AlphaFrom = 1f,
            });
            flash.transform.position = mouth.position + transform.forward * 3f;
        }
        flash.Emit(FlashParticles);
    }
}
