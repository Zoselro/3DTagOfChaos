using UnityEngine;

// 음성 채팅 스피커의 소리를 키운다(Request1009Plan.md §69 A2·A3). AudioSource.volume은 1이 최대라 그 이상은 여기서
// 샘플에 이득을 곱한다. 찢어지지 않게 부드럽게 눌러 ±1 안에 둔다. Gain은 메인 스레드(VoiceChat)가 바꾸고 오디오 스레드가 읽는다.
[RequireComponent(typeof(AudioSource))]
public class VoiceGain : MonoBehaviour
{
    private volatile float gain = 1f;

    public float Gain
    {
        get => gain;
        set => gain = Mathf.Max(0f, value);
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        float g = gain;
        if (Mathf.Approximately(g, 1f)) return;
        for (int i = 0; i < data.Length; i++) data[i] = SoftClip(data[i] * g);
    }

    // tanh에 가까운 부드러운 제한(순수 계산, 시험용): 작은 소리는 거의 그대로, 큰 소리는 ±1을 넘지 않는다.
    public static float SoftClip(float x)
    {
        if (x > 3f) return 1f;
        if (x < -3f) return -1f;
        float x2 = x * x;
        return x * (27f + x2) / (27f + 9f * x2);
    }
}
