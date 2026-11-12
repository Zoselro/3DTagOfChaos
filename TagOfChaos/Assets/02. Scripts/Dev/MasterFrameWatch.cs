using Photon.Pun;
using UnityEngine;

// 방장 저속 경고(Request1009Plan.md §72 E1). 방장 화면의 프레임이 오래 떨어지면(창이 뒤에 있는 에디터, 한 PC에 빌드 여러 개 등)
// 방장이 하는 판정·AI 경로·상태 쓰기가 모두 늦어져 다른 화면에서 순간이동·늦은 판정으로 보인다. 그때 로그를 남긴다(빌드는 Player.log).
// 판정은 순수 계산(IsSlow)이라 시험할 수 있다.
public class MasterFrameWatch : MonoBehaviour
{
    public const float WindowSeconds = 2f;     // 이 시간 동안의 평균 프레임으로 본다
    public const float SlowFrameSeconds = 0.066f; // 평균 15 fps 아래
    public const float RepeatSeconds = 10f;    // 경고를 다시 남기는 간격
    public const float NoticeRepeatSeconds = 60f; // 대기실에서 방장 본인에게 안내를 다시 띄우는 간격(§73 L6)

    private static MasterFrameWatch instance;
    private float windowStart;
    private int frames;
    private float lastWarnAt = -100f;
    private float lastNoticeAt = -1000f;

    public static int WarnCount { get; private set; } // 시험·로그용

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(MasterFrameWatch));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MasterFrameWatch>();
    }

    public static bool IsSlow(float seconds, int frameCount) => frameCount > 0 && seconds / frameCount > SlowFrameSeconds;

    private void Update()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient)
        {
            frames = 0;
            windowStart = Time.unscaledTime;
            return;
        }
        frames++;
        float elapsed = Time.unscaledTime - windowStart;
        if (elapsed < WindowSeconds) return;
        if (IsSlow(elapsed, frames) && Time.unscaledTime - lastWarnAt >= RepeatSeconds)
        {
            lastWarnAt = Time.unscaledTime;
            WarnCount++;
            Debug.LogWarning($"[MasterFrame] This client is the master but runs at {frames / elapsed:F0} fps — other players will see late judgments and jumps. " +
                             $"Keep the master window in front, or let a faster PC host.");
            // 대기실에서는 방장 본인에게 안내만 한다(§73 L6, 사용자 결정 — 방장을 자동으로 넘기지 않음)
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneNames.GameLobby && Time.unscaledTime - lastNoticeAt >= NoticeRepeatSeconds)
            {
                lastNoticeAt = Time.unscaledTime;
                ScreenToast.Show(EscapeTextsSO.Current.masterSlow, 6f);
            }
        }
        frames = 0;
        windowStart = Time.unscaledTime;
    }
}
