using System;
using UnityEngine;
using UnityEngine.UI;

// 설정 창(Request1009Plan.md §27): 왼쪽 탭 3개(조작키 · 출력 장치 · 입력 장치)와 오른쪽 내용. LobbyScene·GameLobbyScene·맵(GameSceneCore)의
// ESC 메뉴 "설정" 버튼이 연다(로비는 위쪽 "설정" 버튼도). 닫으면 Closed로 메뉴에 돌아간다. 프리팹은 SettingsMenuInstaller가 만들고 문구는 프리팹에서 입력한다.
public class SettingsMenu : MonoBehaviour
{
    [Serializable]
    private struct Tab
    {
        public Button button;
        public GameObject page;
    }

    [SerializeField] private Tab[] tabs = new Tab[0];
    [SerializeField] private Button closeButton;
    [SerializeField] private Color tabSelected = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private Color tabNormal = Color.white;
    [Tooltip("ESC 메뉴가 없는 화면에서 창이 직접 ESC로 닫힌다")]
    [SerializeField] private bool closeOnEscape;

    private int current;

    public event Action Closed;
    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => ShowTab(index));
        }
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (!closeOnEscape || !Input.GetKeyDown(KeyCode.Escape) || KeyBindingsPage.ConsumesEscape) return;
        Close();
        UiSoundCues.WindowClosed();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        UiSoundCues.WindowOpened();
        ShowTab(current);
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    // 다른 창이 함께 닫을 때(ESC 메뉴 전체 닫기) — 메뉴로 돌아가지 않는다.
    public void Hide() => gameObject.SetActive(false);

    public void ShowTab(int index)
    {
        if (tabs.Length == 0) return;
        current = Mathf.Clamp(index, 0, tabs.Length - 1);
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].page != null && tabs[i].page.activeSelf != (i == current)) tabs[i].page.SetActive(i == current);
            if (tabs[i].button != null && tabs[i].button.targetGraphic != null) tabs[i].button.targetGraphic.color = i == current ? tabSelected : tabNormal;
        }
    }
}
