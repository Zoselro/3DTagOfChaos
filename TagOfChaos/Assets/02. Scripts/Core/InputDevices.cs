using System;
using UnityEngine;

// 마이크(입력 장치) 선택(Request1009Plan.md §27 K3 — §26 H10). 설정 창의 "입력 장치" 탭에서 고르고, 이 PC에 저장한다(PlayerPrefs, 빈 값 = 첫 장치).
// 녹음(VoiceRecorderPanel)과 대기실 음성 채팅(VoiceChat)이 이 장치를 쓴다. 고른 장치가 지금 없으면(뽑힘) 첫 장치를 쓴다.
public static class InputDevices
{
    public const string PrefsKey = "TOC_MicDevice";
    private static string saved;
    private static bool loaded;

    public static event Action Changed;

    public static string[] Available() => Microphone.devices ?? new string[0];

    // 실제로 쓸 장치 이름(없으면 null).
    public static string Selected => Resolve(SavedName, Available());

    public static string SavedName
    {
        get
        {
            if (!loaded) { loaded = true; try { saved = PlayerPrefs.GetString(PrefsKey, string.Empty); } catch (Exception) { saved = string.Empty; } }
            return saved;
        }
    }

    // 저장된 이름이 목록에 있으면 그것, 없으면 첫 장치, 장치가 없으면 null(순수 계산).
    public static string Resolve(string savedName, string[] available)
    {
        if (available == null || available.Length == 0) return null;
        if (!string.IsNullOrEmpty(savedName))
            foreach (string d in available) if (d == savedName) return d;
        return available[0];
    }

    public static void Select(string deviceName)
    {
        saved = deviceName ?? string.Empty;
        loaded = true;
        try { PlayerPrefs.SetString(PrefsKey, saved); PlayerPrefs.Save(); } catch (Exception) { }
        Changed?.Invoke();
    }
}
