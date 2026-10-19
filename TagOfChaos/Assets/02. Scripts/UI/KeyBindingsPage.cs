using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 창 "조작키" 탭(§27 K2): 조작마다 한 줄(이름 + 키 칸). 키 칸을 누르면 다음에 누른 키·마우스 버튼으로 바뀌고 바로 적용된다(Esc = 취소).
// 같은 묶음(쿠키·괴물·관전)에서 겹치는 키는 빨간색으로 보인다(막지는 않는다). "기본값으로"는 확인창(예/아니오)을 거쳐 모든 키를 되돌린다.
// 줄은 프리팹의 줄 틀(rowTemplate)을 복제해 열 때 만든다. 조작 이름은 actionLabels(KeyAction 순서)에 프리팹에서 입력한다.
public class KeyBindingsPage : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private GameObject rowTemplate;   // Label(TMP) + KeyButton(Button + TMP)
    [SerializeField] private Button resetButton;
    [Tooltip("기본값으로 되돌릴지 묻는 창(예/아니오). 없으면 바로 되돌린다.")]
    [SerializeField] private GameObject resetConfirm;
    [SerializeField] private Button resetYesButton;
    [SerializeField] private Button resetNoButton;
    [SerializeField] private string[] actionLabels = new string[0];
    [SerializeField] private string listeningText = "Press a key";
    [SerializeField] private Color normalKeyColor = Color.black;
    [SerializeField] private Color conflictKeyColor = new Color(0.9f, 0.1f, 0.1f);

    private struct Row { public KeyAction Action; public TMP_Text Key; public Image Background; }

    private readonly List<Row> rows = new List<Row>();
    private static KeyBindingsPage listening;
    private static int escapeFrame = -1;
    private KeyAction listeningAction;
    private int listenStartFrame;
    private static KeyCode[] allKeys;

    private static KeyBindingsPage confirming;

    // 키 입력을 기다리는 중이거나 초기화 확인창이 떠 있으면 ESC는 그것을 취소하는 데 쓰인다 — ESC 메뉴·설정 창이 닫히지 않게 확인한다.
    public static bool ConsumesEscape => listening != null || confirming != null || escapeFrame == Time.frameCount;
    public bool IsConfirmingReset => resetConfirm != null && resetConfirm.activeSelf;

    private void Awake()
    {
        if (rowTemplate != null) rowTemplate.SetActive(false);
        if (resetButton != null) resetButton.onClick.AddListener(AskReset);
        if (resetYesButton != null) resetYesButton.onClick.AddListener(ConfirmReset);
        if (resetNoButton != null) resetNoButton.onClick.AddListener(CancelReset);
        if (resetConfirm != null) resetConfirm.SetActive(false);
        BuildRows();
    }

    private void OnEnable()
    {
        KeyBindings.Changed += RefreshKeys;
        RefreshKeys();
    }

    private void OnDisable()
    {
        KeyBindings.Changed -= RefreshKeys;
        StopListening();
        CancelReset();
    }

    public void AskReset()
    {
        StopListening();
        if (resetConfirm == null) { KeyBindings.ResetAll(); return; }
        resetConfirm.SetActive(true);
        confirming = this;
    }

    public void ConfirmReset()
    {
        KeyBindings.ResetAll();
        CancelReset();
    }

    public void CancelReset()
    {
        if (resetConfirm != null && resetConfirm.activeSelf) resetConfirm.SetActive(false);
        if (confirming == this) confirming = null;
    }

    private void BuildRows()
    {
        if (rowTemplate == null || content == null || rows.Count > 0) return;
        foreach (KeyAction action in KeyBindings.All)
        {
            GameObject go = Instantiate(rowTemplate, content);
            go.name = "Row_" + action;
            go.SetActive(true);
            TMP_Text label = go.transform.Find("Label").GetComponent<TMP_Text>();
            int i = (int)action;
            label.text = i < actionLabels.Length && !string.IsNullOrEmpty(actionLabels[i]) ? actionLabels[i] : action.ToString();
            Button keyButton = go.transform.Find("KeyButton").GetComponent<Button>();
            KeyAction captured = action;
            keyButton.onClick.AddListener(() => StartListening(captured));
            rows.Add(new Row { Action = action, Key = keyButton.GetComponentInChildren<TMP_Text>(true), Background = keyButton.targetGraphic as Image });
        }
    }

    private void RefreshKeys()
    {
        foreach (Row row in rows)
        {
            bool waiting = listening == this && listeningAction == row.Action;
            row.Key.text = waiting ? listeningText : KeyBindings.DisplayName(KeyBindings.Get(row.Action));
            row.Key.color = !waiting && KeyBindings.HasConflict(row.Action) ? conflictKeyColor : normalKeyColor;
        }
    }

    private void StartListening(KeyAction action)
    {
        listening = this;
        listeningAction = action;
        listenStartFrame = Time.frameCount; // 칸을 누른 그 클릭은 새 키로 받지 않는다
        RefreshKeys();
    }

    private void StopListening()
    {
        if (listening != this) return;
        listening = null;
        RefreshKeys();
    }

    private void Update()
    {
        if (confirming == this && Input.GetKeyDown(KeyCode.Escape))
        {
            escapeFrame = Time.frameCount;
            CancelReset();
            return;
        }
        if (listening != this || Time.frameCount == listenStartFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            escapeFrame = Time.frameCount;
            StopListening();
            return;
        }
        KeyCode pressed = PressedKey();
        if (pressed == KeyCode.None) return;
        listening = null;
        KeyBindings.Set(listeningAction, pressed); // Changed → RefreshKeys
        RefreshKeys();
        UiSoundCues.Toast(false);
    }

    // 이번 프레임에 눌린 키(키보드·마우스 0~6). 조이스틱 버튼은 받지 않는다.
    private static KeyCode PressedKey()
    {
        if (!Input.anyKeyDown) return KeyCode.None;
        if (allKeys == null)
        {
            var list = new List<KeyCode>();
            foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
                if (k < KeyCode.JoystickButton0 && KeyBindings.CanBind(k)) list.Add(k);
            allKeys = list.ToArray();
        }
        foreach (KeyCode k in allKeys) if (Input.GetKeyDown(k)) return k;
        return KeyCode.None;
    }

    // 자동 검증용: 칸을 누르고 그 키를 누른 것처럼.
    public void EditorBind(KeyAction action, KeyCode key)
    {
        StartListening(action);
        listening = null;
        KeyBindings.Set(action, key);
        RefreshKeys();
    }

    public string EditorKeyText(KeyAction action)
    {
        foreach (Row row in rows) if (row.Action == action) return row.Key.text + "|" + (row.Key.color == conflictKeyColor ? "red" : "normal");
        return null;
    }
}
