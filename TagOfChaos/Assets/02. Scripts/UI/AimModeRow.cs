using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 창 조작키 탭의 "조준 방식" 줄(§76 F4): 버튼을 누를 때마다 토글(기본) ↔ 누르고 있기로 바뀌고 바로 저장된다.
// 줄은 SettingsMenuInstaller가 만들고 문구는 프리팹(이 컴포넌트 필드)에서 입력한다.
public class AimModeRow : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private string toggleText = "Click to toggle";
    [SerializeField] private string holdText = "Hold to aim";

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnEnable() => Show();

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        AimModeSetting.Set(!AimModeSetting.HoldToAim);
        Show();
    }

    private void Show()
    {
        if (valueText != null) valueText.text = AimModeSetting.HoldToAim ? holdText : toggleText;
    }
}
