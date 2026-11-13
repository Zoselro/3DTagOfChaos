using UnityEngine;

// 마우스 감도(§74 P5) — 쿠키·괴물이 함께 쓰는 하나의 값. 시점 회전 입력(PlayerInput.CameraRotateDelta)에 곱한다.
// PlayerPrefs에 저장하고 처음 읽을 때 한 번만 불러 둔다(매 프레임 PlayerPrefs를 읽지 않는다).
public static class MouseSensitivity
{
    public const float Min = 0.2f, Max = 3f, Default = 1f;
    private const string PrefsKey = "Settings.MouseSensitivity";

    private static float? cached;

    public static float Value
    {
        get
        {
            if (!cached.HasValue)
            {
                float v;
                try { v = PlayerPrefs.GetFloat(PrefsKey, Default); } catch (System.Exception) { v = Default; }
                cached = Clamp(v);
            }
            return cached.Value;
        }
    }

    public static float Clamp(float value) => float.IsNaN(value) ? Default : Mathf.Clamp(value, Min, Max);

    public static void Set(float value)
    {
        float v = Clamp(value);
        if (cached.HasValue && Mathf.Approximately(cached.Value, v)) return;
        cached = v;
        try { PlayerPrefs.SetFloat(PrefsKey, v); PlayerPrefs.Save(); } catch (System.Exception) { }
    }
}
