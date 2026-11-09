using System;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대기실 참가자 목록 한 줄: 이름 + 녹음 표시(목소리를 녹음한 사람, §26 H11) + (호스트 화면에만) 강퇴 버튼(Request1009Plan.md §19.6)
// + 남의 줄에는 그 사람 목소리 음량 슬라이더(0~200 %, §69 A3 — 이 PC에 저장). 슬라이더는 프리팹에 없으면 처음 쓸 때 만든다.
public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private Button kickButton;

    [SerializeField] private string recordedMark = " <color=#66ff88>\u25CF</color>";

    public void SetNickname(string nickname, bool recorded = false)
    {
        nicknameText.text = recorded ? nickname + recordedMark : nickname;
    }

    public void SetKick(bool visible, Action onKick)
    {
        if (kickButton == null) return;
        kickButton.gameObject.SetActive(visible);
        kickButton.onClick.RemoveAllListeners();
        if (visible && onKick != null) kickButton.onClick.AddListener(() => onKick());
        LayoutVolume();
    }

    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeLabel;
    private const float SliderWidth = 130f, LabelWidth = 52f;

    // 그 사람 목소리 음량 슬라이더(내 줄은 숨김).
    public void SetVolume(Player player, bool isLocal)
    {
        if (volumeSlider == null) BuildVolumeSlider();
        volumeSlider.gameObject.SetActive(!isLocal);
        volumeLabel.gameObject.SetActive(!isLocal);
        volumeSlider.onValueChanged.RemoveAllListeners();
        volumeSlider.SetValueWithoutNotify(VoiceVolumes.Get(player));
        ShowPercent(volumeSlider.value);
        volumeSlider.onValueChanged.AddListener(v => { VoiceVolumes.Set(player, v); ShowPercent(v); });
        LayoutVolume();
    }

    private void ShowPercent(float v) => volumeLabel.text = Mathf.RoundToInt(v * 100f) + "%";

    // 강퇴 버튼이 보이면 그 왼쪽, 아니면 오른쪽 끝. 이름 칸은 남은 너비.
    private void LayoutVolume()
    {
        if (volumeSlider == null) return;
        bool kick = kickButton != null && kickButton.gameObject.activeSelf;
        float right = kick ? 12f + ((RectTransform)kickButton.transform).sizeDelta.x + 10f : 12f;
        var lrt = (RectTransform)volumeLabel.transform;
        lrt.anchoredPosition = new Vector2(-right, 0f);
        var srt = (RectTransform)volumeSlider.transform;
        srt.anchoredPosition = new Vector2(-right - LabelWidth - 6f, 0f);
        if (nicknameText != null)
        {
            var nrt = (RectTransform)nicknameText.transform;
            float used = volumeSlider.gameObject.activeSelf ? right + LabelWidth + SliderWidth + 16f : right + 10f;
            nrt.offsetMax = new Vector2(-used, nrt.offsetMax.y);
        }
    }

    private void BuildVolumeSlider()
    {
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = "VolumeSlider";
        go.transform.SetParent(transform, false);
        var srt = (RectTransform)go.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(1f, 0.5f);
        srt.pivot = new Vector2(1f, 0.5f);
        srt.sizeDelta = new Vector2(SliderWidth, 20f);
        volumeSlider = go.GetComponent<Slider>();
        volumeSlider.minValue = VoiceVolumes.Min;
        volumeSlider.maxValue = VoiceVolumes.Max;
        volumeSlider.navigation = new Navigation { mode = Navigation.Mode.None }; // 스페이스·방향키가 슬라이더를 움직이지 않게(§69 A5)
        foreach (Image img in go.GetComponentsInChildren<Image>(true))
        {
            if (img.name == "Background") img.color = new Color(1f, 1f, 1f, 0.25f);
            else if (img.name == "Fill") img.color = new Color(0.4f, 1f, 0.55f, 0.9f);
            else if (img.name == "Handle") img.color = Color.white;
        }
        var label = new GameObject("VolumeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(transform, false);
        var lrt = (RectTransform)label.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 0.5f);
        lrt.pivot = new Vector2(1f, 0.5f);
        lrt.sizeDelta = new Vector2(LabelWidth, 30f);
        volumeLabel = label.GetComponent<TextMeshProUGUI>();
        if (nicknameText != null) { volumeLabel.font = nicknameText.font; volumeLabel.color = nicknameText.color; }
        volumeLabel.fontSize = 18f;
        volumeLabel.alignment = TextAlignmentOptions.MidlineRight;
        volumeLabel.raycastTarget = false;
    }
}
