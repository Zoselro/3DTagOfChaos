using System.Collections.Generic;
using UnityEngine;

// 음성 채팅 값(Request1009Plan.md §26 H11, 전역 SO → Resources/VoiceChatSettings). 거리 소리: fullVolumeDistance까지 그대로, silentDistance에서 0.
// 지금은 GameLobbyScene에서만 켠다 — 나중에 맵에서도 쓰려면 enabledScenes에 씬 이름을 더하면 된다.
[CreateAssetMenu(menuName = "TagOfChaos/VoiceChatSettings", fileName = "VoiceChatSettings")]
public class VoiceChatSettingsSO : ScriptableObject
{
    [SerializeField, Min(0f)] private float fullVolumeDistance = 3f;
    [SerializeField, Min(0.1f)] private float silentDistance = 25f;
    [SerializeField] private List<string> enabledScenes = new List<string> { SceneNames.GameLobby };
    [Tooltip("말하는 사람 머리 위치(캐릭터 바닥 기준 높이)")]
    [SerializeField] private float mouthHeight = 1.5f;
    [Tooltip("상대 목소리 이득(1 = 그대로). AudioSource 최대 음량 위로 키운다(§69 A2 — 사용자: 3배).")]
    [SerializeField, Min(0f)] private float remoteGain = 3f;

    public float FullVolumeDistance => fullVolumeDistance;
    public float SilentDistance => Mathf.Max(silentDistance, fullVolumeDistance + 0.1f);
    public float MouthHeight => mouthHeight;
    public float RemoteGain => remoteGain;
    public bool IsEnabledIn(string sceneName) => enabledScenes.Contains(sceneName);

    private static VoiceChatSettingsSO current;
    public static VoiceChatSettingsSO Current
    {
        get
        {
            if (current == null) current = Resources.Load<VoiceChatSettingsSO>("VoiceChatSettings");
            if (current == null) current = CreateInstance<VoiceChatSettingsSO>();
            return current;
        }
    }
}
