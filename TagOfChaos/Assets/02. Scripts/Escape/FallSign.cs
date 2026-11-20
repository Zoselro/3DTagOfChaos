using TMPro;
using UnityEngine;

// §90 W1 Heaven Of Bakery 낙하 주의 표지판(경사로 12 m 지점·두 반환점·위 입구). 쿠키·스파이 화면에만 보인다(술래는 해당 없음).
// 글은 EscapeTextsSO(fallSign·fallSignSmall)에서 — 나중에 바꾸기 쉽게.
public class FallSign : MonoBehaviour
{
    [SerializeField] private TMP_Text big;
    [SerializeField] private TMP_Text small;
    private Renderer[] renderers;
    private float nextCheck;

#if UNITY_EDITOR
    public void EditorSetup(TMP_Text title, TMP_Text sub) { big = title; small = sub; }
#endif

    private void Start()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        EscapeTextsSO t = EscapeTextsSO.Current;
        if (big != null) big.text = t.fallSign;
        if (small != null) small.text = t.fallSignSmall;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        bool show = !RoomState.IsLocalMonster();
        foreach (Renderer r in renderers) if (r != null && r.enabled != show) r.enabled = show;
    }
}
