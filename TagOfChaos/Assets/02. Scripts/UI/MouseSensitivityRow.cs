using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 창 조작키 탭의 "마우스 감도" 줄(§74 P5): 막대를 움직이면 바로 저장·적용되고 오른쪽에 배율(예: 1.0)을 보여 준다.
// 줄은 SettingsMenuInstaller가 만들고 이름 문구는 프리팹에서 입력한다.
public class MouseSensitivityRow : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private string valueFormat = "{0:0.0}";

    private void Awake()
    {
        if (slider == null) return;
        slider.minValue = MouseSensitivity.Min;
        slider.maxValue = MouseSensitivity.Max;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(OnChanged);
    }

    private void OnEnable()
    {
        float v = MouseSensitivity.Value;
        if (slider != null) slider.SetValueWithoutNotify(v);
        ShowValue(v);
    }

    private void OnDestroy()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(float value)
    {
        MouseSensitivity.Set(value);
        ShowValue(MouseSensitivity.Value);
    }

    private void ShowValue(float value)
    {
        if (valueText != null) valueText.text = string.Format(valueFormat, value);
    }
}
