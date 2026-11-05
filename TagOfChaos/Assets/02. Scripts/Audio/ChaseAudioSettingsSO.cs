using System;
using UnityEngine;

// 술래 접근 추격음 설정(DistanceFadePlan.md §9.2·§9.6·§9.7). 인스펙터에서 바꾸면 Play 중에도 바로 들린다(ChaseDirector가 매 프레임 읽음).
// 사용자가 바꾸는 실제 거리는 audibleRadius 하나이고, 최대 음량 반경·북 단계 경계·단계 여유는 모두 그에 대한 비율이라
// audibleRadius만 바꾸면 전체가 같은 비율로 따라 줄거나 는다.
[CreateAssetMenu(menuName = "Audio/ChaseAudioSettings")]
public class ChaseAudioSettingsSO : ScriptableObject
{
    public const string ResourcePath = "Audio/ChaseAudioSettings";

    [Serializable]
    public class DrumLevel
    {
        [Tooltip("이 단계에서 바탕과 함께 도는 층(바탕과 같은 길이여야 박자가 맞는다)")]
        public AudioClip clip;
        [Tooltip("술래가 audibleRadius × 이 비율 안에 들어오면 이 단계(1 = 들리기 시작하는 거리)")]
        [Range(0f, 1f)] public float startRatio = 1f;
        [Tooltip("참고: 지금 audibleRadius로 계산한 실제 거리(m) — 저장할 때 자동으로 채워진다")]
        public float startMeters;
    }

    [Header("Distance")]
    [Tooltip("술래가 이 거리(m) 안에 들어오면 추격음이 들리기 시작한다")]
    [SerializeField, Min(1f)] private float audibleRadius = 45f; // 2026-10-31 사용자 지시: 20 → 30 m, 2026-11-05: 30 → 45 m(§59 CS1)
    [Tooltip("audibleRadius × 이 비율 안에서는 최대 음량")]
    [SerializeField, Range(0f, 0.95f)] private float fullRadiusRatio = 0.25f;
    [Tooltip("가로 0 = 들리기 시작하는 거리, 1 = 최대 음량 거리 / 세로 = 음량 배율")]
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("술래가 다른 층(높이 차 4 m 넘음)에 있을 때 배율")]
    [SerializeField, Range(0f, 1f)] private float otherFloorGain = SpatialGain.OtherFloorGain;

    [Header("Layers")]
    [Tooltip("바탕(현) — 들리는 동안 계속")]
    [SerializeField] private AudioClip baseLayer;
    [Tooltip("북 단계(먼 것부터). 술래가 가까워질수록 다음 단계로 교차 전환")]
    [SerializeField] private DrumLevel[] drumLevels = new DrumLevel[0];
    [Tooltip("한 단계 올라간 뒤에는 audibleRadius × 이 비율만큼 더 멀어져야 내려간다(경계에서 왔다 갔다 하지 않게)")]
    [SerializeField, Range(0f, 0.2f)] private float levelHysteresisRatio = 0.05f;
    [SerializeField, Min(0.01f)] private float levelCrossfadeSeconds = 0.4f;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.68f; // 0.8 → 0.68(15 % 낮춤, §59 CS1)
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.4f;
    [SerializeField, Min(0.01f)] private float fadeOutSeconds = 1.5f;
    [Tooltip("멀 때 먹먹하게(저역 통과)")]
    [SerializeField] private bool muffleWhenFar = true;
    [SerializeField, Range(200f, 8000f)] private float farCutoffHz = 900f;
    [Tooltip("추격음이 최대일 때 배경음 배율")]
    [SerializeField, Range(0f, 1f)] private float musicDuckAtFull = 0.4f;

    public float AudibleRadius => audibleRadius;
    public float FullRadius => audibleRadius * fullRadiusRatio;
    public float OtherFloorGain => otherFloorGain;
    public AudioClip BaseLayer => baseLayer;
    public int LevelCount => drumLevels.Length;
    public AudioClip LevelClip(int i) => drumLevels[i].clip;
    public float LevelRadius(int i) => audibleRadius * drumLevels[i].startRatio;
    public float LevelHysteresis => audibleRadius * levelHysteresisRatio;
    public float LevelCrossfadeSeconds => levelCrossfadeSeconds;
    public float MaxVolume => maxVolume;
    public float FadeInSeconds => fadeInSeconds;
    public float FadeOutSeconds => fadeOutSeconds;
    public bool MuffleWhenFar => muffleWhenFar;
    public float FarCutoffHz => farCutoffHz;
    public float MusicDuckAtFull => musicDuckAtFull;

    public float Curve(float closeness) => Mathf.Clamp01(intensityCurve != null && intensityCurve.length > 0 ? intensityCurve.Evaluate(closeness) : closeness);

    // 거꾸로 넣어도 깨지지 않게: 단계는 먼 것(큰 비율)부터, 첫 단계는 1 이하. 실제 거리(m) 참고값도 채운다.
    private void OnValidate()
    {
        if (drumLevels == null) drumLevels = new DrumLevel[0];
        Array.Sort(drumLevels, (a, b) => (b?.startRatio ?? 0f).CompareTo(a?.startRatio ?? 0f));
        foreach (DrumLevel level in drumLevels)
        {
            if (level == null) continue;
            level.startRatio = Mathf.Clamp01(level.startRatio);
            level.startMeters = Mathf.Round(audibleRadius * level.startRatio * 10f) / 10f;
        }
    }

#if UNITY_EDITOR
    public void EditorSetup(AudioClip baseClip, DrumLevel[] levels)
    {
        baseLayer = baseClip;
        drumLevels = levels;
        OnValidate();
    }

    public void EditorSetMaxVolume(float volume) => maxVolume = Mathf.Clamp01(volume);

    public void EditorSetRadius(float meters)
    {
        audibleRadius = Mathf.Max(1f, meters);
        OnValidate();
    }
#endif

    private static ChaseAudioSettingsSO cached;

    // 처음 접근할 때 Resources에서 한 번 읽는다(SoundCatalogSO와 같은 방식). 없으면 기본값(클립 없음 — 소리만 나지 않는다).
    public static ChaseAudioSettingsSO Current
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<ChaseAudioSettingsSO>(ResourcePath);
            if (cached == null) cached = CreateInstance<ChaseAudioSettingsSO>();
            return cached;
        }
    }
}
