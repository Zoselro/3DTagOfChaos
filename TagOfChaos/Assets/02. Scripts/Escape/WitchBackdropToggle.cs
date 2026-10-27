using UnityEngine;

// 마녀 연출 때만 보이는 배경(2026-10-27 요청). 평소에는 자식 오브젝트를 모두 꺼 두고, 마녀 시간(WitchTimeline.Mode != None —
// 첫 탈출 뒤 로켓·탈출구 앞 샷부터 마녀 등장·접근·결과까지)에만 켠다. 대기실로 돌아가면 씬이 다시 로드되므로 다음 판은 다시 꺼진 채 시작한다.
// 초콜릿 공장 펜스 밖 장식 고리(Map/Background/CHO_Backdrop, 충돌 없음)에 붙는다 — ChocolateFactoryMapBuilder가 붙인다.
public class WitchBackdropToggle : MonoBehaviour
{
    private const float CheckSeconds = 0.25f;
    private bool? shown;
    private float nextCheck;

    private void Awake() => Apply(false);

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckSeconds;
        Apply(WitchTimeline.Watches(WitchTimeline.Mode));
    }

    private void Apply(bool show)
    {
        if (shown == show) return;
        shown = show;
        foreach (Transform child in transform) child.gameObject.SetActive(show);
    }
}
