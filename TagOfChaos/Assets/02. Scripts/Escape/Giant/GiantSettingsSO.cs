using UnityEngine;

// 거대 괴물 설정(Request1009Plan.md §64). Resources/Escape/GiantSettings에서 읽는다(ChaseAudioSettingsSO와 같은 방식).
// 밟기 방식이 바뀌면(예: 걷다가 그대로 밟고 지나가기) 동작 클립·판정 시각을 여기서 바꾼다 — 코드(GiantRules)는 기본값만 들고 있다.
[CreateAssetMenu(menuName = "TagOfChaos/GiantSettings", fileName = "GiantSettings")]
public class GiantSettingsSO : ScriptableObject
{
    public const string ResourcePath = "Escape/GiantSettings";

    [Header("Spawn")]
    [SerializeField, Range(0f, 1f)] private float spawnChance = 0.3f; // 마녀 타이머 시작 때 나올 확률(사용자 결정: 고정 30 %)

    [Header("Model (MixedMonster.fbx)")]
    [SerializeField] private GameObject model;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip walkClip;
    [SerializeField] private AnimationClip stompClip;

    [Header("Look")]
    [SerializeField] private Color circleColor = new Color(0.85f, 0.2f, 1f);
    [SerializeField, Min(0f)] private float circleGlow = 1.2f;
    [SerializeField] private Color slimeColor = new Color(1f, 0.44f, 0.71f);   // 에이드공주 분홍 슬라임(#FF6FB5)
    [Tooltip("Character slots (SkinCatalog index) that ooze slime instead of crumbs when stomped.")]
    [SerializeField] private int[] slimeCharacters = { 3 };                   // 3 = 에이드공주(CHR_Lemonade)
    [SerializeField] private Color[] syrupColors =
    {
        new Color(0.45f, 0.22f, 0.08f), new Color(0.95f, 0.45f, 0.65f), new Color(0.45f, 0.75f, 0.3f), new Color(0.95f, 0.88f, 0.75f),
    };

    public float SpawnChance => spawnChance;
    public GameObject Model => model;
    public AnimationClip IdleClip => idleClip;
    public AnimationClip WalkClip => walkClip;
    public AnimationClip StompClip => stompClip;
    public Color CircleColor => circleColor;
    public float CircleGlow => circleGlow;
    public Color SlimeColor => slimeColor;
    public Color[] SyrupColors => syrupColors;
    public bool OozesSlime(int characterIndex) => slimeCharacters != null && System.Array.IndexOf(slimeCharacters, characterIndex) >= 0;

    private static GiantSettingsSO cached;

    public static GiantSettingsSO Current
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<GiantSettingsSO>(ResourcePath);
            if (cached == null) cached = CreateInstance<GiantSettingsSO>();
            return cached;
        }
    }

#if UNITY_EDITOR
    public void EditorBind(GameObject giantModel, AnimationClip idle, AnimationClip walk, AnimationClip stomp)
    {
        model = giantModel;
        idleClip = idle;
        walkClip = walk;
        stompClip = stomp;
    }

    public void EditorSetSpawnChance(float chance) => spawnChance = Mathf.Clamp01(chance);
#endif
}
