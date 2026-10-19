using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using S = SoundSettingsInstaller;

// 설정 창 설치(Request1009Plan.md §27 K5·K6). Tools/TagOfChaos/UI/Install Settings Menu.
// 1) 프리팹 Assets/04. Prefabs/UI/SettingsMenu.prefab: 왼쪽 탭(조작키·출력 장치·입력 장치) + 내용. 출력 장치 탭은 소리 설정 창(SoundSettingsPanel 프리팹)의
//    막대·장치 줄을 그대로 옮겨 쓴다. 이미 있으면 rebuild가 true일 때만 다시 만든다.
// 2) ESC 메뉴(GameSceneCore 프리팹, GameLobbyScene): "설정" → "방 설정", "소리" → "설정"(이 창), 예전 소리 창은 지운다. 버튼 순서 계속하기·방 설정·설정·나가기.
// 3) LobbyScene: ESC 메뉴(계속하기·설정·게임 종료 + 확인창)를 만들고, 위쪽 "소리" 버튼을 "설정"으로 바꾼다. 예전 소리 창은 지운다.
// 문구는 Texts로 받는다(코드에는 영어 기본값만 — 한글은 한 번 쓰는 설치 스크립트가 \u로 넘긴다).
public static class SettingsMenuInstaller
{
    public const string PrefabPath = "Assets/04. Prefabs/UI/SettingsMenu.prefab";
    private const float RowHeight = 54f;

    public class Texts
    {
        public string title = "Settings";
        public string tabControls = "Controls", tabOutput = "Output device", tabInput = "Input device";
        public string close = "Close", reset = "Reset to defaults", listening = "Press a key (Esc: cancel)";
        public string resetConfirm = "Reset all keys to their defaults?";
        public string[] actionLabels = new string[0];
        public string micLabel = "Microphone", refresh = "Refresh", levelLabel = "Input level", talkModeLabel = "Talk mode";
        public string noMicrophone = "No microphone", pushToTalk = "Push to talk", alwaysOn = "Always on", keyHintFormat = "Talk key: {0}";
        public string roomSettings = "Room settings", settings = "Settings";
        public string menuTitle = "Menu", resume = "Resume", quit = "Quit game", quitConfirm = "Quit the game?", yes = "Yes", no = "No";
    }

    [MenuItem("Tools/TagOfChaos/UI/Install Settings Menu")]
    public static void InstallMenu() => Debug.Log("[SettingsMenu] " + InstallAll(new Texts(), false));

    public static string InstallAll(Texts texts, bool rebuild)
    {
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        GameObject core = PrefabUtility.LoadPrefabContents(S.CorePrefabPath);
        S.Style style = S.ReadStyle(core.GetComponentInChildren<EscMenu>(true));
        EnsurePrefab(style, texts, rebuild);
        SettingsMenu prefab = null; // 프리팹은 쓸 때마다 경로로 다시 읽는다(저장 직후 참조가 바뀔 수 있다)
        string coreResult = InstallInEscMenu(core.GetComponentInChildren<EscMenu>(true), prefab, texts);
        PrefabUtility.SaveAsPrefabAsset(core, S.CorePrefabPath);
        PrefabUtility.UnloadPrefabContents(core);

        var gameLobby = EditorSceneManager.OpenScene(S.GameLobbyScenePath, OpenSceneMode.Single);
        EscMenu lobbyEsc = Object.FindFirstObjectByType<EscMenu>(FindObjectsInactive.Include);
        string gameLobbyResult = lobbyEsc != null ? InstallInEscMenu(lobbyEsc, prefab, texts) : "no EscMenu";
        EditorSceneManager.MarkSceneDirty(gameLobby);
        EditorSceneManager.SaveScene(gameLobby);

        var lobby = EditorSceneManager.OpenScene(S.LobbyScenePath, OpenSceneMode.Single);
        string lobbyResult = InstallInLobby(prefab, style, texts);
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);

        AssetDatabase.SaveAssets();
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        return $"GameSceneCore: {coreResult}, GameLobbyScene: {gameLobbyResult}, LobbyScene: {lobbyResult}";
    }

    // ---------------- prefab ----------------

    private static SettingsMenu EnsurePrefab(S.Style style, Texts tx, bool rebuild)
    {
        var existing = AssetDatabase.LoadAssetAtPath<SettingsMenu>(PrefabPath);
        if (existing != null && !rebuild) return existing;

        var root = new GameObject("SettingsMenu", typeof(RectTransform), typeof(Image));
        S.Stretch((RectTransform)root.transform);
        root.GetComponent<Image>().color = new Color(style.dimColor.r, style.dimColor.g, style.dimColor.b, Mathf.Max(0.6f, style.dimColor.a));

        RectTransform box = S.Child(root.transform, "Box", typeof(Image));
        S.SetRect(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 800f));
        var boxImage = box.GetComponent<Image>();
        boxImage.sprite = style.boxSprite;
        boxImage.type = style.boxSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        boxImage.color = new Color(style.boxColor.r, style.boxColor.g, style.boxColor.b, 1f);

        TMP_Text title = S.Text(box, "Title", tx.title, 40f, style.font, style.textColor, TextAlignmentOptions.Center);
        S.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(0f, 56f), new Vector2(0.5f, 1f));

        // 왼쪽 탭
        string[] tabNames = { "Tab_Controls", "Tab_Output", "Tab_Input" };
        string[] tabLabels = { tx.tabControls, tx.tabOutput, tx.tabInput };
        var tabButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            tabButtons[i] = S.CreateButton(box, tabNames[i], tabLabels[i], style);
            S.SetRect((RectTransform)tabButtons[i].transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -100f - i * 76f), new Vector2(220f, 64f), new Vector2(0f, 1f));
        }

        RectTransform pages = S.Child(box, "Pages");
        S.SetRect(pages, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        pages.offsetMin = new Vector2(280f, 110f);
        pages.offsetMax = new Vector2(-30f, -90f);

        GameObject controls = BuildControlsPage(pages, style, tx);
        GameObject output = BuildOutputPage(pages);
        GameObject input = BuildInputPage(pages, style, tx);

        Button close = S.CreateButton(box, "BackButton", tx.close, style);
        S.SetRect((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(240f, 60f), new Vector2(0.5f, 0f));

        var menu = root.AddComponent<SettingsMenu>();
        var so = new SerializedObject(menu);
        SerializedProperty tabs = so.FindProperty("tabs");
        GameObject[] pageObjects = { controls, output, input };
        tabs.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            tabs.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue = tabButtons[i];
            tabs.GetArrayElementAtIndex(i).FindPropertyRelative("page").objectReferenceValue = pageObjects[i];
        }
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("tabNormal").colorValue = style.buttonColor;
        so.ApplyModifiedPropertiesWithoutUndo();
        output.SetActive(false);
        input.SetActive(false);

        UiSoundInstaller.Attach(root.GetComponentsInChildren<Button>(true));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<SettingsMenu>(PrefabPath);
    }

    private static GameObject BuildControlsPage(Transform pages, S.Style style, Texts tx)
    {
        RectTransform page = S.Child(pages, "ControlsPage");
        S.Stretch(page);

        RectTransform scroll = S.Child(page, "Scroll", typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        S.SetRect(scroll, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        scroll.offsetMin = new Vector2(0f, 70f);
        scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        RectTransform contentRt = S.Child(scroll, "Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        S.SetRect(contentRt, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
        var layout = contentRt.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 24, 10, 10);
        layout.spacing = 6f;
        layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        contentRt.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = scroll.GetComponent<ScrollRect>();
        sr.content = contentRt; sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 30f; sr.movementType = ScrollRect.MovementType.Clamped;

        // 줄 틀(꺼 둠): 이름 + 키 칸
        RectTransform row = S.Child(page, "RowTemplate", typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = RowHeight;
        TMP_Text label = S.Text(row, "Label", "Action", 26f, style.font, style.textColor, TextAlignmentOptions.MidlineLeft);
        S.SetRect(label.rectTransform, Vector2.zero, new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero);
        Button key = S.CreateButton(row, "KeyButton", "-", style);
        S.SetRect((RectTransform)key.transform, new Vector2(0.62f, 0.08f), new Vector2(1f, 0.92f), Vector2.zero, Vector2.zero);
        row.gameObject.SetActive(false);

        Button reset = S.CreateButton(page, "ResetButton", tx.reset, style);
        S.SetRect((RectTransform)reset.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(300f, 56f), new Vector2(1f, 0f));

        var comp = page.gameObject.AddComponent<KeyBindingsPage>();
        var so = new SerializedObject(comp);
        so.FindProperty("content").objectReferenceValue = contentRt;
        so.FindProperty("rowTemplate").objectReferenceValue = row.gameObject;
        so.FindProperty("resetButton").objectReferenceValue = reset;
        AddResetConfirm(page, so, style, tx);
        so.FindProperty("listeningText").stringValue = tx.listening;
        so.FindProperty("normalKeyColor").colorValue = style.labelColor;
        SerializedProperty labels = so.FindProperty("actionLabels");
        labels.arraySize = tx.actionLabels.Length;
        for (int i = 0; i < tx.actionLabels.Length; i++) labels.GetArrayElementAtIndex(i).stringValue = tx.actionLabels[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return page.gameObject;
    }

    // "기본값으로" 확인창(§27 후속): 조작키 탭 위를 어둡게 덮는 판 + 문구 + 예/아니오.
    private static void AddResetConfirm(RectTransform page, SerializedObject so, S.Style style, Texts tx)
    {
        Transform old = page.Find("ResetConfirm");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        RectTransform dim = S.Child(page, "ResetConfirm", typeof(Image));
        S.Stretch(dim);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform box = Box(dim, style, new Vector2(620f, 260f));
        TMP_Text message = S.Text(box, "Message", tx.resetConfirm, 30f, style.font, style.textColor, TextAlignmentOptions.Center);
        S.SetRect(message.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(-40f, 90f), new Vector2(0.5f, 1f));
        Button yes = S.CreateButton(box, "YesButton", tx.yes, style);
        Button no = S.CreateButton(box, "BackButton", tx.no, style);
        S.SetRect((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 34f), new Vector2(200f, 64f), new Vector2(0.5f, 0f));
        S.SetRect((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120f, 34f), new Vector2(200f, 64f), new Vector2(0.5f, 0f));
        UiSoundInstaller.Attach(new[] { yes, no });
        dim.SetAsLastSibling();
        dim.gameObject.SetActive(false);
        so.FindProperty("resetConfirm").objectReferenceValue = dim.gameObject;
        so.FindProperty("resetYesButton").objectReferenceValue = yes;
        so.FindProperty("resetNoButton").objectReferenceValue = no;
    }

    // 이미 있는 프리팹의 조작키 탭에 확인창만 넣는다(프리팹 전체를 다시 만들지 않는다 — 다른 문구·배치 보존).
    public static string AddResetConfirmToPrefab(Texts tx)
    {
        GameObject core = PrefabUtility.LoadPrefabContents(S.CorePrefabPath);
        S.Style style = S.ReadStyle(core.GetComponentInChildren<EscMenu>(true));
        PrefabUtility.UnloadPrefabContents(core);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            KeyBindingsPage page = root.GetComponentInChildren<KeyBindingsPage>(true);
            if (page == null) return "no KeyBindingsPage";
            var so = new SerializedObject(page);
            AddResetConfirm((RectTransform)page.transform, so, style, tx);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "ok";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 소리 설정 창 프리팹을 풀어 막대 4줄 + 출력 장치 줄만 남긴다(문구·장치 줄은 그 프리팹 것을 그대로).
    private static GameObject BuildOutputPage(Transform pages)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(S.PrefabPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(source, pages);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "OutputPage";
        S.Stretch((RectTransform)inst.transform);
        Object.DestroyImmediate(inst.GetComponent<Image>());
        Transform box = inst.transform.Find("Box");
        if (box != null)
        {
            Object.DestroyImmediate(box.GetComponent<Image>());
            S.Stretch((RectTransform)box);
            foreach (string n in new[] { "Title", "BackButton" }) { Transform t = box.Find(n); if (t != null) Object.DestroyImmediate(t.gameObject); }
            foreach (Transform r in box) ((RectTransform)r).anchoredPosition += new Vector2(0f, 80f); // 제목이 없어진 만큼 위로
        }
        var so = new SerializedObject(inst.GetComponent<SoundSettingsPanel>());
        so.FindProperty("closeButton").objectReferenceValue = null;
        so.FindProperty("closeOnEscape").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        return inst;
    }

    private static GameObject BuildInputPage(Transform pages, S.Style style, Texts tx)
    {
        RectTransform page = S.Child(pages, "InputPage");
        S.Stretch(page);
        float y = -20f;
        TMP_Text Label(string name, string text)
        {
            TMP_Text t = S.Text(page, name, text, 28f, style.font, style.textColor, TextAlignmentOptions.MidlineLeft);
            S.SetRect(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(220f, 56f), new Vector2(0f, 1f));
            return t;
        }

        Label("MicLabel", tx.micLabel);
        TMP_Dropdown mic = S.CreateDropdown(page, style);
        mic.name = "MicDropdown";
        S.SetRect((RectTransform)mic.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(240f, y - 3f), new Vector2(480f, 50f), new Vector2(0f, 1f));
        Button refresh = S.CreateButton(page, "RefreshButton", tx.refresh, style);
        S.SetRect((RectTransform)refresh.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(740f, y - 3f), new Vector2(160f, 50f), new Vector2(0f, 1f));
        refresh.GetComponentInChildren<TMP_Text>().fontSize = 22f;

        y -= 80f;
        Label("LevelLabel", tx.levelLabel);
        RectTransform bg = S.Child(page, "LevelBar", typeof(Image));
        S.SetRect(bg, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(240f, y - 15f), new Vector2(660f, 26f), new Vector2(0f, 1f));
        bg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);
        RectTransform fill = S.Child(bg, "Fill", typeof(Image));
        S.Stretch(fill);
        var fillImage = fill.GetComponent<Image>();
        fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fillImage.type = Image.Type.Filled; fillImage.fillMethod = Image.FillMethod.Horizontal; fillImage.fillAmount = 0f;
        fillImage.color = new Color(0.4f, 1f, 0.5f);

        y -= 80f;
        Label("TalkModeLabel", tx.talkModeLabel);
        TMP_Dropdown talk = S.CreateDropdown(page, style);
        talk.name = "TalkModeDropdown";
        S.SetRect((RectTransform)talk.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(240f, y - 3f), new Vector2(480f, 50f), new Vector2(0f, 1f));

        y -= 80f;
        TMP_Text hint = S.Text(page, "KeyHint", string.Format(tx.keyHintFormat, "T"), 26f, style.font, style.textColor, TextAlignmentOptions.MidlineLeft);
        S.SetRect(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(250f, y), new Vector2(-260f, 56f), new Vector2(0f, 1f));

        var comp = page.gameObject.AddComponent<InputDevicePage>();
        var so = new SerializedObject(comp);
        so.FindProperty("micDropdown").objectReferenceValue = mic;
        so.FindProperty("refreshButton").objectReferenceValue = refresh;
        so.FindProperty("levelFill").objectReferenceValue = fillImage;
        so.FindProperty("talkModeDropdown").objectReferenceValue = talk;
        so.FindProperty("keyHint").objectReferenceValue = hint;
        so.FindProperty("noMicrophoneLabel").stringValue = tx.noMicrophone;
        so.FindProperty("keyHintFormat").stringValue = tx.keyHintFormat;
        SerializedProperty modes = so.FindProperty("talkModeLabels");
        modes.arraySize = 2;
        modes.GetArrayElementAtIndex(0).stringValue = tx.pushToTalk;
        modes.GetArrayElementAtIndex(1).stringValue = tx.alwaysOn;
        so.ApplyModifiedPropertiesWithoutUndo();
        return page.gameObject;
    }

    // ---------------- install ----------------

    private static string InstallInEscMenu(EscMenu esc, SettingsMenu prefab, Texts tx)
    {
        if (esc == null) return "no EscMenu";
        var so = new SerializedObject(esc);
        Transform buttons = esc.transform.Find("Panel/Box/Buttons");
        if (buttons == null) return "no Buttons";

        foreach (SoundSettingsPanel old in esc.GetComponentsInChildren<SoundSettingsPanel>(true))
            if (old.GetComponentInParent<SettingsMenu>(true) == null) Object.DestroyImmediate(old.gameObject);

        Transform options = buttons.Find("OptionsButton") ?? buttons.Find(S.ButtonName);
        if (options == null) return "no SoundButton";
        options.name = "OptionsButton";
        SetLabel(options, tx.settings);
        var room = so.FindProperty("settingsButton").objectReferenceValue as Button;
        if (room != null) SetLabel(room.transform, tx.roomSettings);

        // 순서: 계속하기 · 방 설정 · 설정 · 나가기
        var resume = so.FindProperty("resumeButton").objectReferenceValue as Button;
        var exit = so.FindProperty("exitButton").objectReferenceValue as Button;
        int i = 0;
        foreach (Component c in new Component[] { resume, room, options, exit })
            if (c != null && c.transform.parent == buttons) c.transform.SetSiblingIndex(i++);

        SettingsMenu menu = esc.GetComponentInChildren<SettingsMenu>(true);
        if (menu == null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), esc.transform);
            S.Stretch((RectTransform)inst.transform);
            inst.transform.SetAsLastSibling();
            inst.SetActive(false);
            menu = inst.GetComponent<SettingsMenu>();
        }
        so.FindProperty("optionsButton").objectReferenceValue = options.GetComponent<Button>();
        so.FindProperty("settingsMenu").objectReferenceValue = menu;
        so.ApplyModifiedPropertiesWithoutUndo();
        return "ok";
    }

    private static string InstallInLobby(SettingsMenu prefab, S.Style style, Texts tx)
    {
        GameObject canvasGo = GameObject.Find("LobbyUICanvas");
        if (canvasGo == null) return "no LobbyUICanvas";
        Transform canvas = canvasGo.transform;
        foreach (SoundSettingsPanel old in canvas.GetComponentsInChildren<SoundSettingsPanel>(true))
            if (old.GetComponentInParent<SettingsMenu>(true) == null) Object.DestroyImmediate(old.gameObject);

        LobbyEscMenu esc = canvas.GetComponentInChildren<LobbyEscMenu>(true);
        if (esc != null) Object.DestroyImmediate(esc.gameObject); // 다시 만든다(문구·배치를 한곳에서)
        SettingsMenu menu = canvas.GetComponentInChildren<SettingsMenu>(true);
        if (menu == null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), canvas);
            S.Stretch((RectTransform)inst.transform);
            menu = inst.GetComponent<SettingsMenu>();
        }

        RectTransform rootRt = S.Child(canvas, "LobbyEscMenu");
        S.Stretch(rootRt);
        esc = rootRt.gameObject.AddComponent<LobbyEscMenu>();

        RectTransform panel = S.Child(rootRt, "Panel", typeof(Image));
        S.Stretch(panel);
        panel.GetComponent<Image>().color = style.dimColor;
        RectTransform box = Box(panel, style, new Vector2(520f, 420f));
        TMP_Text title = S.Text(box, "Title", tx.menuTitle, 40f, style.font, style.textColor, TextAlignmentOptions.Center);
        S.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -20f), new Vector2(0f, 56f), new Vector2(0.5f, 1f));
        Button resume = S.CreateButton(box, "ResumeButton", tx.resume, style);
        Button settings = S.CreateButton(box, "OptionsButton", tx.settings, style);
        Button quit = S.CreateButton(box, "QuitButton", tx.quit, style);
        Button[] list = { resume, settings, quit };
        for (int i = 0; i < list.Length; i++)
            S.SetRect((RectTransform)list[i].transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f - i * 90f), new Vector2(340f, 70f), new Vector2(0.5f, 1f));

        RectTransform confirm = S.Child(rootRt, "QuitConfirm", typeof(Image));
        S.Stretch(confirm);
        confirm.GetComponent<Image>().color = style.dimColor;
        RectTransform cbox = Box(confirm, style, new Vector2(560f, 260f));
        TMP_Text message = S.Text(cbox, "Message", tx.quitConfirm, 32f, style.font, style.textColor, TextAlignmentOptions.Center);
        S.SetRect(message.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(-40f, 80f), new Vector2(0.5f, 1f));
        Button yes = S.CreateButton(cbox, "YesButton", tx.yes, style);
        Button no = S.CreateButton(cbox, "BackButton", tx.no, style);
        S.SetRect((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 34f), new Vector2(200f, 64f), new Vector2(0.5f, 0f));
        S.SetRect((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120f, 34f), new Vector2(200f, 64f), new Vector2(0.5f, 0f));

        var so = new SerializedObject(esc);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("resumeButton").objectReferenceValue = resume;
        so.FindProperty("settingsButton").objectReferenceValue = settings;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("quitConfirm").objectReferenceValue = confirm.gameObject;
        so.FindProperty("quitYesButton").objectReferenceValue = yes;
        so.FindProperty("quitNoButton").objectReferenceValue = no;
        so.FindProperty("settingsMenu").objectReferenceValue = menu;
        so.ApplyModifiedPropertiesWithoutUndo();
        UiSoundInstaller.Attach(rootRt.GetComponentsInChildren<Button>(true));
        panel.gameObject.SetActive(false);
        confirm.gameObject.SetActive(false);

        // 위쪽 "소리" 버튼 → "설정"
        Transform top = canvas.Find(S.ButtonName) ?? canvas.Find("SettingsButton");
        if (top != null)
        {
            top.name = "SettingsButton";
            SetLabel(top, tx.settings);
            var b = top.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(b.onClick, esc.OpenSettings);
        }
        rootRt.SetAsLastSibling();
        menu.transform.SetAsLastSibling();
        menu.gameObject.SetActive(false);
        return top != null ? "ok" : "ok (no top button)";
    }

    private static RectTransform Box(Transform parent, S.Style style, Vector2 size)
    {
        RectTransform box = S.Child(parent, "Box", typeof(Image));
        S.SetRect(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        var img = box.GetComponent<Image>();
        img.sprite = style.boxSprite;
        img.type = style.boxSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = new Color(style.boxColor.r, style.boxColor.g, style.boxColor.b, 1f);
        return box;
    }

    private static void SetLabel(Transform button, string text)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }
}
