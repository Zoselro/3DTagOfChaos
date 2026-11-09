using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

// 사람별 음성 채팅 음량(Request1009Plan.md §69 A3) — 대기실 참가자 목록의 슬라이더로 정하고, 이 PC에 사람(UserId, 없으면 닉네임)별로 저장한다.
// 0 = 안 들림, 1 = 기본, 2 = 두 배. 실제 소리 = VoiceChatSettingsSO.RemoteGain × 이 값(VoiceChat이 스피커마다 곱한다).
public static class VoiceVolumes
{
    public const float Min = 0f, Max = 2f, Default = 1f;
    private const string PrefsPrefix = "TOC_VoiceVol_";
    private static readonly Dictionary<string, float> cache = new Dictionary<string, float>();

    public static string KeyOf(Player p) => p == null ? null : !string.IsNullOrEmpty(p.UserId) ? p.UserId : p.NickName;

    public static float Get(Player p)
    {
        string key = KeyOf(p);
        if (string.IsNullOrEmpty(key)) return Default;
        if (cache.TryGetValue(key, out float v)) return v;
        try { v = PlayerPrefs.GetFloat(PrefsPrefix + key, Default); } catch (System.Exception) { v = Default; }
        v = Mathf.Clamp(v, Min, Max);
        cache[key] = v;
        return v;
    }

    public static void Set(Player p, float value)
    {
        string key = KeyOf(p);
        if (string.IsNullOrEmpty(key)) return;
        value = Mathf.Clamp(value, Min, Max);
        cache[key] = value;
        try { PlayerPrefs.SetFloat(PrefsPrefix + key, value); PlayerPrefs.Save(); } catch (System.Exception) { }
    }
}
