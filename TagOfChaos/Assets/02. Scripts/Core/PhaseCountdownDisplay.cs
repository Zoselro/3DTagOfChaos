using Photon.Pun;
using TMPro;
using UnityEngine;

// 게임 페이즈의 남은 시간을 mm:ss로 표시하는 공용 카운트다운. 어떤 페이즈를 셀지는 인스펙터에서 고른다
// (예전 이름 SurvivalTimerDisplay — 생존 시간 전용이던 것을 일반화, .meta GUID 유지로 씬 참조 보존).
// 단계 판정은 GamePhaseState 한 곳에 맡긴다(research.md §12 E2). 새 페이즈에 카운트다운이 필요하면
// CountdownPhase 항목과 ToGamePhase의 대응만 추가하면 된다(Bug-fix-plan.md §28.2).
// 자기 GameObject를 끄지 않고 CanvasGroup으로만 숨겨 Update()가 계속 돌게 한다(§23.2.1과 같은 이유).
public class PhaseCountdownDisplay : MonoBehaviour
{
    // 씬에 직렬화된 값이므로 항목 순서를 바꾸지 않는다.
    public enum CountdownPhase
    {
        Prepare,  // 준비 시간(예전 변장 시간 자리, Request1009Plan.md §25.5 C)
        Survival, // 예전 생존 시간 — 게임 제한시간을 없애 쓰이지 않는다(씬 값 보존)
    }

    [SerializeField] private CountdownPhase phase = CountdownPhase.Survival;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private string format = "{0:00}:{1:00}"; // {0}=분, {1}=초 — 앞뒤 문구는 인스펙터에서 입력

    private CanvasGroup canvasGroup;
    private bool? lastVisible;
    private int lastShownSeconds = -1;

    private void Awake()
    {
        canvasGroup = CanvasGroupVisibility.Ensure(gameObject);
        SetVisible(false);
    }

    private void Update()
    {
        if (GamePhaseState.Current == GamePhase.Result) { SetVisible(false); return; } // 결과 화면 뒤로 남은 시간이 비치지 않게

        bool running = GamePhaseState.TryGetActiveEndTime(ToGamePhase(phase), out double endTime) && PhotonNetwork.Time < endTime
            && !IsProvisional(phase, endTime);
        SetVisible(running);
        if (!running) return;

        ShowSeconds(Mathf.Max(0, Mathf.CeilToInt((float)(endTime - PhotonNetwork.Time))));
    }

    // 모두 맵에 들어오기 전의 임시 준비 끝 시각(§74 P6)은 보이지 않는다 — 남은 시간이 준비 시간보다 길면 아직 세기 전이다.
    private static bool IsProvisional(CountdownPhase countdownPhase, double endTime) =>
        countdownPhase == CountdownPhase.Prepare && endTime - PhotonNetwork.Time > GameSettings.Current.PrepareSeconds + 1f;

    private void ShowSeconds(int seconds)
    {
        if (seconds == lastShownSeconds || timerText == null) return; // 초가 바뀔 때만 문자열을 만든다

        if (lastShownSeconds >= 0) UiSoundCues.CountdownTick(seconds); // 마지막 10초 째깍(SoundPlan.md S2)
        lastShownSeconds = seconds;
        timerText.text = string.Format(format, seconds / 60, seconds % 60);
    }

    private static GamePhase ToGamePhase(CountdownPhase countdownPhase)
    {
        switch (countdownPhase)
        {
            case CountdownPhase.Prepare: return GamePhase.Prepare;
            default: return GamePhase.Hunt; // 끝 시각이 없어 보이지 않는다
        }
    }

    private void SetVisible(bool visible)
    {
        if (lastVisible == visible) return;
        lastVisible = visible;
        CanvasGroupVisibility.Set(canvasGroup, visible);
    }
}
