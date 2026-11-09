using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameLobbyScene 상시 노출 UI에 부착. 대기실에서 언제든 다시 눌러 바꿀 수 있다(GameRule.md §1.5).
// 버튼은 캐릭터 목록(SkinCatalogSO — 마카롱 할아버지·쿠키신사·에이드공주·마들렌잉어, Request1009Plan.md §25.5 B) 수만큼 템플릿에서 만든다.
// 캐릭터는 겹쳐 고를 수 없다: 다른 사람이 고른 캐릭터 버튼은 누를 수 없게 흐리게 보인다. 동시에 눌러 겹쳐도 판이 시작될 때 방장이
// 입장 순서대로 인정하고 나머지는 남은 캐릭터로 바꾼다(CharacterAssignment). 고르지 않은 사람도 그때 남은 캐릭터를 받는다.
public class PlayerSkinSelector : MonoBehaviourPunCallbacks
{
    [SerializeField] private SkinCatalogSO catalog;
    [SerializeField] private Button buttonTemplate; // 복제 원본(보통 첫 버튼), 버튼들은 이 오브젝트의 직계 자식

    private System.Collections.Generic.List<Button> buttons;

    private void Awake()
    {
        if (catalog == null || buttonTemplate == null)
        {
            Debug.LogWarning($"[PlayerSkinSelector] Catalog or button template is not assigned on {name}.");
            return;
        }

        buttons = UiListBuilder.Sync(transform, buttonTemplate, catalog.Count);
        for (int i = 0; i < buttons.Count; i++)
        {
            int index = i; // 람다 캡처용 복사
            Button button = buttons[i];
            button.name = $"SkinButton{i}";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectSkin(index));
            button.navigation = new Navigation { mode = Navigation.Mode.None }; // 방향키(WASD)로 선택이 옮겨 가지 않게(§69 A5)

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = catalog.GetLabel(i);
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        RefreshTaken();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(NetKeys.SkinIndex)) RefreshTaken();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => RefreshTaken();
    public override void OnPlayerLeftRoom(Player otherPlayer) => RefreshTaken();
    public override void OnJoinedRoom() => RefreshTaken();

    // 다른 사람이 고른 캐릭터는 누를 수 없다(내가 고른 것은 그대로 눌린 상태로 둔다).
    private void RefreshTaken()
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Count; i++) buttons[i].interactable = !IsTakenByOther(i);
    }

    public static bool IsTakenByOther(int index)
    {
        if (!RoomState.IsInRoom() || PhotonNetwork.LocalPlayer == null) return false;
        foreach (Player p in RoomState.Players)
        {
            if (p.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber) continue;
            if (RoomState.TryGetPlayerInt(p, NetKeys.SkinIndex, out int s) && s == index) return true;
        }
        return false;
    }

    private void SelectSkin(int index)
    {
        // 누른 버튼이 UI 선택으로 남으면 스페이스(= UI 확인)가 다시 누른다 — 고르자마자 선택을 푼다(§69 A5)
        if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        if (IsTakenByOther(index)) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.SkinIndex, index } });
    }
}
