using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 재사용 가능한 예/아니오 확인창. 특정 기능(나가기 등)에 종속되지 않도록 콜백을 인자로 받는다.
public class ConfirmDialog : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private Action onYesConfirmed;

    // 떠 있는 확인창 수 — 마우스 자유 시점이 커서를 풀어 둔다(CursorPolicy, Request1009Plan.md §59 CL).
    private static int openCount;
    public static bool AnyOpen => openCount > 0;
    private void OnEnable() => openCount++;
    private void OnDisable() => openCount = Mathf.Max(0, openCount - 1);

    private void Awake()
    {
        yesButton.onClick.AddListener(OnYesClicked);
        noButton.onClick.AddListener(Hide);
        gameObject.SetActive(false); // 평소에는 숨겨둠
    }

    public void Show(string message, Action onYes)
    {
        messageText.text = message;
        onYesConfirmed = onYes;
        gameObject.SetActive(true);
        UiSoundCues.WindowOpened();
    }

    private void OnYesClicked()
    {
        Hide();
        onYesConfirmed?.Invoke();
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
