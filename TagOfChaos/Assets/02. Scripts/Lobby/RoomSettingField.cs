using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 방 만들기 설정 한 줄(EscapePlan.md §1.7, Request1009Plan.md §25.5 A — 인원·스파이 수·마녀 타이머): ▼ 버튼, 숫자 입력칸, ▲ 버튼, 빨간 경고 문구.
// 범위를 벗어나면 가장 가까운 허용값으로 되돌리고 경고를 잠시 보여준다. 경고 문구는 인스펙터에서 입력한다(코드에 한글 금지).
public class RoomSettingField : MonoBehaviour
{
    // 씬에 직렬화된 값이므로 순서를 바꾸지 않는다(1은 예전 제한시간 칸 자리 → 스파이 수).
    public enum Kind { Players, SpyCount, WitchTimerMinutes }

    [SerializeField] private Kind kind;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private string outOfRangeMessage = "Out of range";
    [SerializeField, Min(0f)] private float warningSeconds = 2.5f;

    private RoomSettingStepper stepper;
    private float hideWarningAt;

    public int Value => Stepper.Value;

    private RoomSettingStepper Stepper
    {
        get
        {
            if (stepper == null) stepper = CreateStepper(kind);
            return stepper;
        }
    }

    public static RoomSettingStepper CreateStepper(Kind kind)
    {
        GameSettingsSO s = GameSettings.Current;
        switch (kind)
        {
            case Kind.SpyCount: return new RoomSettingStepper(0, s.MaxSpies, s.DefaultSpies);
            case Kind.WitchTimerMinutes: return new RoomSettingStepper(s.MinTimeAttackMinutes, s.MaxTimeAttackMinutes, s.DefaultTimeAttackMinutes);
            default: return new RoomSettingStepper(s.MinPlayers, s.MaxPlayers, s.DefaultPlayers);
        }
    }

    private void Awake()
    {
        if (decreaseButton != null) decreaseButton.onClick.AddListener(() => Apply(Stepper.Step(-1), byUser: true));
        if (increaseButton != null) increaseButton.onClick.AddListener(() => Apply(Stepper.Step(+1), byUser: true));
        if (input != null)
        {
            input.contentType = TMP_InputField.ContentType.IntegerNumber; // 숫자 외 문자는 입력되지 않는다
            input.characterValidation = TMP_InputField.CharacterValidation.Digit;
            input.onEndEdit.AddListener(text => Apply(Stepper.SetFromText(text), byUser: true));
        }
        SetWarning(false);
        Refresh();
    }

    private void Update()
    {
        if (warningText != null && warningText.enabled && Time.unscaledTime >= hideWarningAt) SetWarning(false);
    }

    // 테스트·다른 UI에서 값을 넣을 때도 같은 규칙을 쓴다.
    public RoomSettingStepper.Outcome SubmitText(string text)
    {
        RoomSettingStepper.Outcome outcome = Stepper.SetFromText(text);
        Apply(outcome, byUser: false);
        return outcome;
    }

    // byUser: 버튼·입력으로 바꾼 경우에만 경고음을 낸다(창을 열며 코드가 값을 채울 때는 문구만).
    private void Apply(RoomSettingStepper.Outcome outcome, bool byUser)
    {
        if (outcome == RoomSettingStepper.Outcome.OutOfRange)
        {
            SetWarning(true);
            if (byUser) UiSoundCues.Error();
        }
        Refresh();
    }

    private void Refresh()
    {
        if (input != null) input.SetTextWithoutNotify(Stepper.Value.ToString());
        if (decreaseButton != null) decreaseButton.interactable = true; // 범위 끝에서도 누르면 경고를 보여줘야 한다
        if (increaseButton != null) increaseButton.interactable = true;
    }

    private void SetWarning(bool show)
    {
        if (warningText == null) return;
        warningText.enabled = show;
        if (show)
        {
            warningText.text = outOfRangeMessage;
            hideWarningAt = Time.unscaledTime + warningSeconds;
        }
    }
}
