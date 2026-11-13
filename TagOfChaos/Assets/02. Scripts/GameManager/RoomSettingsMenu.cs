using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대기실(GameLobbyScene)에서 호스트(시작 버튼 주인, RoomState.HostActor)가 ESC → 설정으로 여는 방 설정 창. 로비에서 방을 만들 때 고른 값(인원·스파이 수·
// 마녀 타이머, Request1009Plan.md §25.5 A)을 다시 바꾼다. 입력 칸은 로비와 같은 RoomSettingField(같은 허용 범위·경고)를 쓴다.
// 스파이 수·마녀 타이머는 방 속성이라 다음 판부터 적용되고, 인원은 지금 들어와 있는 사람 수보다 적게 줄일 수 없다.
// 문구는 씬에서 입력한다(코드에 한글 금지).
public class RoomSettingsMenu : MonoBehaviour
{
    [SerializeField] private RoomSettingField playersField;
    [UnityEngine.Serialization.FormerlySerializedAs("timeLimitField")]
    [SerializeField] private RoomSettingField spyCountField;
    [UnityEngine.Serialization.FormerlySerializedAs("timeAttackField")]
    [SerializeField] private RoomSettingField witchTimerField;
    [Tooltip("서버 지역(타임어택 칸 밑) — 바꾸고 적용하면 방 전원이 새 지역의 같은 방으로 옮겨 간다(몇 초 끊김)")]
    [SerializeField] private RegionSelector regionField;
    [SerializeField] private Button applyButton;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private string playersRaisedMessage = "Players can't be fewer than the people in the room";
    [SerializeField] private string notHostMessage = "Only the host can change room settings";

    public event System.Action Closed;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        if (applyButton != null) applyButton.onClick.AddListener(Apply);
        RoomState.HideSettingRow(witchTimerField); // 마녀 타이머는 10분 고정(§74 P8)
    }

    public void Open()
    {
        gameObject.SetActive(true);
        UiSoundCues.WindowOpened();
        if (feedbackText != null) feedbackText.text = string.Empty;
        if (!PhotonNetwork.InRoom) return;
        // 지금 방의 값으로 채운다
        if (playersField != null) playersField.SubmitText(PhotonNetwork.CurrentRoom.MaxPlayers.ToString());
        if (spyCountField != null) spyCountField.SubmitText(RoomState.SpyCount().ToString());
        if (regionField != null) regionField.SetCode(PhotonRegions.Current ?? PhotonRegions.Saved);
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    // 반영은 RoomSettingsAuthority가 한다(호스트 확인·범위 맞춤·방장에게 요청). 창을 연 뒤 호스트가 바뀌었으면 안내한다.
    private void Apply()
    {
        if (!PhotonNetwork.InRoom) return;
        Photon.Realtime.Room room = PhotonNetwork.CurrentRoom;
        int players = playersField != null ? playersField.Value : room.MaxPlayers;
        int spies = spyCountField != null ? spyCountField.Value : RoomState.SpyCount();

        string region = regionField != null ? regionField.Code : RoomSettingsAuthority.KeepRegion;
        RoomSettingsAuthority.Result result = RoomSettingsAuthority.Request(players, spies, region);
        if (result == RoomSettingsAuthority.Result.PlayersRaised && playersField != null)
            playersField.SubmitText(Mathf.Max(players, room.PlayerCount).ToString());
        if (result == RoomSettingsAuthority.Result.Applied)
        {
            UiSoundCues.Saved();
            Close(); // 저장되면 ESC 메뉴로 돌아간다('뒤로' 버튼은 없앴다 — ESC로도 닫힌다)
            return;
        }
        UiSoundCues.Error(); // 인원을 올렸거나 호스트가 아니면 창에 남아 안내를 보여 준다
        if (feedbackText == null) return;
        switch (result)
        {
            case RoomSettingsAuthority.Result.PlayersRaised: feedbackText.text = playersRaisedMessage; break;
            case RoomSettingsAuthority.Result.NotHost: feedbackText.text = notHostMessage; break;
        }
    }

    private static int CurrentMinutes(string key, int fallback) => RoomState.TryGetInt(key, out int seconds) ? seconds / 60 : fallback;
}
