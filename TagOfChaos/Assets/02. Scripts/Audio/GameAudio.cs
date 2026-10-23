using UnityEngine;

// 게임 코드가 소리를 내는 유일한 창구(SoundPlan.md §2.1-4 — PlayerInput과 같은 방식). 다른 도메인은 이 클래스와
// SoundId·MusicId만 안다. 클립·거리·음량·쿨다운은 카탈로그 SO에 있고, 재생 칸·교차 전환은 AudioRuntime이 맡는다.
// 에디터에서 재생 중이 아니거나 클립이 아직 없으면 아무 소리도 내지 않는다(호출하는 쪽은 신경 쓰지 않아도 된다).
public static class GameAudio
{
    // 2D(UI·본인 전용 소리). 돌려받은 핸들로 멈출 수 있다(무시해도 된다).
    public static AudioHandle Play(SoundId id)
    {
        AudioRuntime runtime = Runtime();
        return runtime != null ? runtime.Play(id, false, Vector3.zero, null, false) : default;
    }

    // 3D 한 번, 그 자리에서. 카탈로그가 2D 소리로 정했으면 2D로 난다.
    public static void PlayAt(SoundId id, Vector3 position)
    {
        AudioRuntime runtime = Runtime();
        if (runtime != null) runtime.Play(id, true, position, null, false);
    }

    // 3D 한 번, 대상을 따라가며(달리는 캐릭터 등).
    public static void PlayOn(SoundId id, Transform target)
    {
        AudioRuntime runtime = Runtime();
        if (runtime == null) return;
        if (target == null) runtime.Play(id, false, Vector3.zero, null, false);
        else runtime.Play(id, true, target.position, target, false);
    }

    // 캐릭터가 내는 소리(DistanceFadePlan.md §2.6): 내 캐릭터면 2D(듣는 위치가 내 머리라 3D면 좌우가 흔들린다), 남의 캐릭터면 그 몸을 따라가는 3D.
    public static void PlayCharacter(SoundId id, IGameCharacter character)
    {
        if (character == null || !CharacterRegistry.IsAlive(character)) return;
        if (character.View != null && character.View.IsMine) Play(id);
        else PlayOn(id, character.gameObject.transform);
    }

    // 반복음. 대상이 없으면 2D. 돌려받은 핸들로 멈춘다(대상이 파괴되면 저절로 멈춘다).
    public static AudioHandle StartLoop(SoundId id, Transform target)
    {
        AudioRuntime runtime = Runtime();
        if (runtime == null) return default;
        return target == null
            ? runtime.Play(id, false, Vector3.zero, null, true)
            : runtime.Play(id, true, target.position, target, true);
    }

    // 반복음의 음량 배율(0~1)을 바꾼다(서서히 키우고 줄일 때).
    public static void SetLoopFade(AudioHandle handle, float fade)
    {
        AudioRuntime runtime = Runtime();
        if (runtime != null) runtime.SetFade(handle, fade);
    }

    // 장소에 따른 배경음 배율(0~1, 1 = 그대로).
    public static void SetMusicZoneGain(float gain)
    {
        AudioRuntime runtime = Runtime();
        if (runtime != null) runtime.Music.ZoneGain = Mathf.Clamp01(gain);
    }

    // 같은 곡이면 그대로 둔다. 다른 곡이면 fadeSeconds에 걸쳐 교차 전환.
    public static void PlayMusic(MusicId id, float fadeSeconds = 2f)
    {
        AudioRuntime runtime = Runtime();
        if (runtime != null) runtime.Music.Play(id, fadeSeconds);
    }

    // 스팅어·징글: 한 번 재생하고 그동안 배경음을 낮춘다.
    public static void Stinger(MusicId id)
    {
        AudioRuntime runtime = Runtime();
        if (runtime != null) runtime.Music.Stinger(id);
    }

    private static AudioRuntime Runtime() => Application.isPlaying ? AudioRuntime.Instance : null;
}
