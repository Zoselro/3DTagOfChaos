using UnityEngine;

// 조준 방식(Request1009Plan.md §76 F4): 기본은 우클릭 한 번 = 조준 켜기, 다시 한 번 = 끄기(토글).
// 설정 창(조작키 탭)에서 "누르고 있는 동안만"으로 바꿀 수 있다. PlayerPrefs에 저장하고 처음 읽을 때 한 번만 불러 둔다.
public static class AimModeSetting
{
    private const string PrefsKey = "Settings.AimHoldToAim";
    private static bool? cached;

    // true = 우클릭을 누르고 있는 동안만 조준, false = 우클릭으로 켜고 끔(기본).
    public static bool HoldToAim
    {
        get
        {
            if (!cached.HasValue)
            {
                int v;
                try { v = PlayerPrefs.GetInt(PrefsKey, 0); } catch (System.Exception) { v = 0; }
                cached = v != 0;
            }
            return cached.Value;
        }
    }

    public static void Set(bool holdToAim)
    {
        if (cached.HasValue && cached.Value == holdToAim) return;
        cached = holdToAim;
        try { PlayerPrefs.SetInt(PrefsKey, holdToAim ? 1 : 0); PlayerPrefs.Save(); } catch (System.Exception) { }
    }

    // 이번 프레임 조준을 켤지(순수 계산 — 시험용). wasOn = 지난 프레임, held/pressed = 우클릭.
    public static bool Next(bool wasOn, bool holdMode, bool held, bool pressed) => holdMode ? held : pressed ? !wasOn : wasOn;
}
