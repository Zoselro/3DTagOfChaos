using UnityEngine;

public enum ToolKind
{
    // 번호는 ToolSO 에셋에 저장되므로 고정한다. 3(감옥 풍선)은 없앴다(Request1009Plan.md §47 CL4) — 다시 쓰지 않는다.
    Hammer = 0,
    StunGun = 1,
    WaterBalloon = 2,
    Skates = 4,      // 인라인스케이트: 잠시 이동속도 +100%(§25.5 I)
    Pumpkin = 5,     // 호박마차: 맵 안 무작위 지점으로 이동
    Recorder = 6,    // 접착 목소리 녹음기: 내 AI 하나가 내 목소리를 내며 걷는다
    Dismantle = 7,   // 해체 도구: 가동된 건물에서 재료를 뺀다(누르기, 왼쪽 클릭으로 쓰지 않음)
    HeliHat = 8,     // 헬리콥터 모자: 들고 E — 떠올라 맵 밖으로(§25.5 H)
    NoiseBomb = 9,   // 괴물 소음폭탄이 터질 때의 기절 수치(아이템으로 줍지 않는다)
}

// 도구 하나의 수치(EscapePlan.md §1.8, D23). 코드를 고치지 않고 에셋 값만 바꿔 밸런스를 테스트한다.
[CreateAssetMenu(menuName = "TagOfChaos/Escape/Tool", fileName = "Tool_")]
public class ToolSO : ScriptableObject
{
    [SerializeField] private ToolKind kind;
    [SerializeField, Min(1)] private int charges = 3;
    [Tooltip("사거리(m). 뿅망치는 근접 거리, 스턴건은 사거리, 물풍선은 던지는 거리.")]
    [SerializeField, Min(0.5f)] private float range = 2f;
    [Tooltip("맞힘 판정 반지름(m). 물풍선은 터지는 범위.")]
    [SerializeField, Min(0.05f)] private float hitRadius = 0.6f;
    [SerializeField, Min(0f)] private float stunSecondsCookie = 1.5f;
    [SerializeField, Min(0f)] private float stunSecondsMonster = 1f;
    [SerializeField, Min(0f)] private float cooldownSeconds = 3f;
    [Tooltip("맞은 쿠키를 무작위 색으로 칠한다(물풍선).")]
    [SerializeField] private bool paintsTarget;
    [Tooltip("효과 시간(초): 스케이트 빠르기, 녹음기 AI가 걷는 시간.")]
    [SerializeField, Min(0f)] private float effectSeconds = 5f;
    [Tooltip("스케이트 빠르기 배율(1 = 그대로, 2 = +100%).")]
    [SerializeField, Min(1f)] private float speedMultiplier = 2f;

    public ToolKind Kind => kind;
    // 괴물을 맞힐 수 있는 도구는 스턴건뿐이다(Request1009Plan.md §59 AT1). 뿅망치·물풍선 등은 괴물을 건너뛴다 — 쓰는 쪽·방장·받는 쪽이 모두 이 규칙을 본다.
    public bool CanHitMonster => CanHitMonsterKind(kind);
    public static bool CanHitMonsterKind(ToolKind k) => k == ToolKind.StunGun;
    public int Charges => charges;
    public float Range => range;
    public float HitRadius => hitRadius;
    public float StunSecondsCookie => stunSecondsCookie;
    public float StunSecondsMonster => stunSecondsMonster;
    public float CooldownSeconds => cooldownSeconds;
    public bool PaintsTarget => paintsTarget;
    public float EffectSeconds => effectSeconds;
    public float SpeedMultiplier => speedMultiplier;

    // 왼쪽 클릭으로 쓰는 도구인지(해체 도구는 누르기, 모자는 E, 소음폭탄은 괴물 스킬).
    public bool UsedByClick => kind != ToolKind.Dismantle && kind != ToolKind.HeliHat && kind != ToolKind.NoiseBomb;

#if UNITY_EDITOR
    public void EditorSetup(ToolKind toolKind, int count, float rangeMeters, float radius, float cookieStun, float monsterStun, float cooldown, bool paints,
        float effect = 5f, float speed = 2f)
    {
        effectSeconds = effect;
        speedMultiplier = speed;
        kind = toolKind;
        charges = count;
        range = rangeMeters;
        hitRadius = radius;
        stunSecondsCookie = cookieStun;
        stunSecondsMonster = monsterStun;
        cooldownSeconds = cooldown;
        paintsTarget = paints;
    }
#endif
}
