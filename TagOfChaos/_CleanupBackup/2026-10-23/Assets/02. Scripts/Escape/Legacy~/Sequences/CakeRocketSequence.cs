using UnityEngine;

// 캔디숲 탈출 장치: 큰 케이크가 부서지고 사탕 로켓이 솟아오른다(EscapeVisualPlan.md §5.1).
//   완성 0~2초: 케이크가 떨리고 금(Cake_Cracks)이 빛난다
//   2~3.6초  : 케이크 조각(Cake_Shard_nn)이 바깥으로 튀며 작아져 사라진다
//   3~6초    : 로켓(Cake_Rocket)이 받침 아래에서 솟아오른다 → 탈 수 있음
//   출발      : 바닥 불꽃이 2초 동안 커지며 떨리고, 가속하며 하늘로 올라간다
// 모델 이름 규칙은 escape_devices.py(device_candy)와 같다.
public class CakeRocketSequence : EscapeSequence
{
    private const float CrackSeconds = 2f;
    private const float ShardSeconds = 1.6f;
    private const float RocketRiseStart = 3f;
    private const float RocketRiseSeconds = 3f;
    private const float RocketDepth = 12f;       // 완성 전 로켓은 받침 아래에 숨어 있다
    private const float IgniteSeconds = 2f;
    private const float RiseAcceleration = 5f;
    private const float HatchOpenSeconds = 1.2f;

    private Transform intact, cracks, rocket, hatch, flame;
    private Transform[] shards = new Transform[0];
    private Vector3[] shardHome = new Vector3[0];
    private Vector3[] shardDir = new Vector3[0];
    private Vector3 intactHome, rocketHome;
    private float hatchOpenUntil = -1f;
    private float hatchAngle;

    public override float BoardReadySeconds => RocketRiseStart + RocketRiseSeconds + 0.3f;
    public override float DepartureSeconds => 7f;
    public override Transform DepartureFocus => rocket != null ? rocket : BoardPoint;

    private void Awake()
    {
        intact = transform.Find("Cake_Intact");
        cracks = transform.Find("Cake_Cracks");
        rocket = transform.Find("Cake_Rocket");
        hatch = rocket != null ? rocket.Find("Hatch") : null;
        flame = rocket != null ? rocket.Find("Flame") : null;
        var list = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform) if (child.name.StartsWith("Cake_Shard_")) list.Add(child);
        shards = list.ToArray();
        shardHome = new Vector3[shards.Length];
        shardDir = new Vector3[shards.Length];
        for (int i = 0; i < shards.Length; i++)
        {
            shardHome[i] = shards[i].localPosition;
            Vector3 flat = new Vector3(shardHome[i].x, 0f, shardHome[i].z);
            shardDir[i] = (flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward) * (5f + 1.5f * (i % 3));
        }
        if (intact != null) intactHome = intact.localPosition;
        if (rocket != null) rocketHome = rocket.localPosition;

        // 소리(SoundPlan.md S6): 우르릉 → 금 가는 소리 → 펑 터짐 → 로켓 솟음 / 출발: 점화 → 발사(로켓을 따라감)
        AddCue(SoundId.CakeRumble, CueClock.Completed, 0f);
        AddCue(SoundId.CakeCrack, CueClock.Completed, CrackSeconds * 0.6f);
        AddCue(SoundId.CakeBurst, CueClock.Completed, CrackSeconds);
        AddCue(SoundId.CakeRocketRise, CueClock.Completed, RocketRiseStart, rocket);
        AddCue(SoundId.CakeIgnite, CueClock.Departed, 0f, rocket);
        AddCue(SoundId.CakeLaunch, CueClock.Departed, IgniteSeconds, rocket);
    }

    public override void Tick(EscapeState state, double now)
    {
        float c = state != null ? Since(state.CompletedAt, now) : -1f;
        float d = state != null ? Since(state.DepartedAt, now) : -1f;

        // 케이크: 완성 전엔 그대로, 0~2초 떨림, 그 뒤 사라짐
        bool intactVisible = c < CrackSeconds;
        SetActive(intact, intactVisible);
        if (intact != null && intactVisible)
            intact.localPosition = intactHome + (c >= 0f ? Shake(0.12f * Mathf.Clamp01(c / CrackSeconds)) : Vector3.zero);
        SetActive(cracks, c >= 0f && c < CrackSeconds);
        if (cracks != null && c >= 0f && c < CrackSeconds)
            cracks.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(Time.time * 30f));

        // 조각: 바깥으로 튀며 작아진다
        float s = c - CrackSeconds;
        for (int i = 0; i < shards.Length; i++)
        {
            bool flying = c >= CrackSeconds && s < ShardSeconds;
            SetActive(shards[i], flying);
            if (!flying) continue;
            Vector3 p = shardHome[i] + shardDir[i] * s + Vector3.up * (6f * s - 7f * s * s);
            shards[i].localPosition = p;
            shards[i].localRotation = Quaternion.Euler(220f * s * (i % 2 == 0 ? 1f : -1f), 0f, 160f * s);
            shards[i].localScale = Vector3.one * Mathf.Clamp01(1f - s / ShardSeconds);
        }

        // 로켓: 솟아오르고, 출발하면 점화 → 상승
        if (rocket == null) return;
        bool rocketShown = c >= RocketRiseStart;
        float rise = Ease((c - RocketRiseStart) / RocketRiseSeconds);
        Vector3 pos = rocketHome + Vector3.down * (RocketDepth * (1f - (rocketShown ? rise : 0f)));
        float flameScale = 0f;
        if (d >= 0f)
        {
            flameScale = Mathf.Clamp01(d / IgniteSeconds) * (1f + 0.12f * Mathf.Sin(Time.time * 40f));
            float up = Mathf.Max(0f, d - IgniteSeconds);
            pos += Vector3.up * (0.5f * RiseAcceleration * up * up) + (d < IgniteSeconds + 0.5f ? Shake(0.08f) : Vector3.zero);
            rocketShown = d < DepartureSeconds;
        }
        SetActive(rocket, rocketShown);
        rocket.localPosition = pos;
        if (flame != null) flame.localScale = new Vector3(flameScale, flameScale, flameScale);

        if (hatch != null)
        {
            float target = Time.time < hatchOpenUntil ? 100f : 0f;
            hatchAngle = Mathf.MoveTowards(hatchAngle, target, 220f * Time.deltaTime);
            hatch.localRotation = Quaternion.Euler(0f, hatchAngle, 0f);
        }
    }

    public override void OnCookieBoarded(Vector3 from)
    {
        hatchOpenUntil = Time.time + HatchOpenSeconds;
        BoardingFx.Play(from, hatch != null ? hatch.position + Vector3.up : BoardPoint.position, BoardingFx.Style.Hop);
    }

    private static Vector3 Shake(float amount)
    {
        Vector3 v = Random.insideUnitSphere * amount;
        v.y = 0f;
        return v;
    }

    private static void SetActive(Transform t, bool active)
    {
        if (t != null && t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
    }
}
