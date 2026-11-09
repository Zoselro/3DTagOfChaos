using TMPro;
using UnityEngine;

// 화면 위쪽에 잠깐 뜨는 알림(Request1009Plan.md §69 A4 — 대기실처럼 EscapeHud가 없는 씬용). 같은 때 하나만 보인다(새 알림이 앞 알림을 바꾼다).
public class ScreenToast : MonoBehaviour
{
    private const float FadeSeconds = 0.4f;
    private static ScreenToast instance;

    private TMP_Text text;
    private CanvasGroup group;
    private float shownAt, seconds;

    public static void Show(string message, float durationSeconds = 3f)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (instance == null) instance = Create();
        instance.text.text = message;
        instance.shownAt = Time.unscaledTime;
        instance.seconds = durationSeconds;
        instance.gameObject.SetActive(true);
        UiSoundCues.Toast(false);
    }

    private static ScreenToast Create()
    {
        var root = new GameObject("ScreenToast", typeof(Canvas), typeof(CanvasGroup));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        var bg = new GameObject("Back", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        bg.transform.SetParent(root.transform, false);
        var brt = (RectTransform)bg.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -90f);
        brt.sizeDelta = new Vector2(620f, 56f);
        var img = bg.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        img.raycastTarget = false;
        var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(bg.transform, false);
        var lrt = (RectTransform)label.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = label.GetComponent<TextMeshProUGUI>();
        TMP_Text sample = FindFirstObjectByType<TMP_Text>(); // 씬에서 쓰는 글꼴(한글 포함)을 따른다
        if (sample != null && sample.font != null) tmp.font = sample.font;
        tmp.fontSize = 26f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var toast = root.AddComponent<ScreenToast>();
        toast.text = tmp;
        toast.group = root.GetComponent<CanvasGroup>();
        toast.group.blocksRaycasts = false;
        return toast;
    }

    private void Update()
    {
        float age = Time.unscaledTime - shownAt;
        group.alpha = age < seconds ? 1f : 1f - Mathf.Clamp01((age - seconds) / FadeSeconds);
        if (age >= seconds + FadeSeconds) gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
