using UnityEngine;

// 영역 환경음(2026-10-03, Request1003Plan.md §4): 듣는 위치(AudioListenerAnchor — 캐릭터 머리)가 상자 안에 들어오면 2D 반복음을 서서히 키우고
// 배경음을 낮춘다. 나가면 반대로. 진저브레드 지하(나선 통로·터널·유적 홀)의 바람·물방울 소리에 쓴다.
// 상자는 월드 좌표(여러 개 = 합집합). 배치는 각 맵 빌더가 한다(옛 Game_ 맵용 AmbientPlacer는 §47 CL5에서 없앴다).
// 맵 바탕 환경음(SoundPlan.md S7 — 새·바람·멀리 오르골 등)도 같은 컴포넌트로, 맵 전체(땅 위) 상자에 배경음 배율 1(건드리지 않음)로 둔다.
// 배경음 배율은 1보다 작을 때만 바꾼다 — 바탕 환경음과 지하 동굴이 겹쳐 전환될 때 서로 덮어쓰지 않게.
public class AmbientZone : MonoBehaviour
{
    [SerializeField] private SoundId sound = SoundId.None;
    [SerializeField] private Bounds[] boxes = new Bounds[0];
    [SerializeField, Min(0.05f)] private float fadeSeconds = 1.5f;
    [Tooltip("영역 안에서 배경음 배율(0 = 끔)")]
    [SerializeField, Range(0f, 1f)] private float musicGainInside = 0.15f;

    private AudioHandle handle;
    private float level;

    private bool DucksMusic => musicGainInside < 1f;

    public bool Contains(Vector3 point)
    {
        foreach (Bounds b in boxes) if (b.Contains(point)) return true;
        return false;
    }

    private void Update()
    {
        bool inside = Contains(AudioListenerAnchor.Position); // 듣는 위치 = 캐릭터 머리(DistanceFadePlan.md §2.1)
        float target = inside ? 1f : 0f;
        if (Mathf.Approximately(level, target) && (target == 0f || handle.IsPlaying)) return;

        level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / fadeSeconds);
        if (level > 0f && !handle.IsPlaying) handle = GameAudio.StartLoop(sound, null);
        GameAudio.SetLoopFade(handle, level);
        if (DucksMusic) GameAudio.SetMusicZoneGain(Mathf.Lerp(1f, musicGainInside, level));
        if (level <= 0f) Stop();
    }

    private void OnDisable()
    {
        level = 0f;
        Stop();
        if (DucksMusic) GameAudio.SetMusicZoneGain(1f);
    }

    private void Stop()
    {
        handle.Stop();
        handle = default;
    }

#if UNITY_EDITOR
    public void EditorSetup(SoundId value, Bounds[] worldBoxes, float musicGain)
    {
        sound = value;
        boxes = worldBoxes;
        musicGainInside = musicGain;
    }
#endif
}
