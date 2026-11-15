using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 서버 지역 고르기 줄을 만든다(2026-10-03, Request1003Plan.md §1).
// - LobbyScene: 방 만들기 설정의 타임어택 칸 밑 "방 지역"(Row_Region) + 방 목록 아래 새로고침 옆 "볼 지역"(Row_ViewRegion).
// - GameLobbyScene: 방 설정 창의 타임어택 칸 밑 "지역"(Row_Region).
// 기존 스파이 줄(Row_SpyCount, 예전에는 마녀 타이머 줄)을 복제해 모양을 맞추고, 숫자 입력칸을 지역 이름 칸으로 바꾼다. 다시 실행하면 지우고 다시 만든다.
// 문구는 영문 기본값만 넣는다(코드에 한글 금지) — 한글 이름은 씬에서 입력한다.
public static class RegionRowBuilder
{
    private const string MenuPath = "Tools/TagOfChaos/UI/Build Region Rows";
    private const string SourceRow = "Row_SpyCount"; // 예전에는 마녀 타이머 줄 — §79에서 그 줄을 없앴다
    public const string RegionRow = "Row_Region";
    public const string ViewRegionRow = "Row_ViewRegion";
    private const float RowStep = 46f;   // 기존 설정 줄 간격
    private const float ValueWidth = 120f;

    [MenuItem(MenuPath)]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        BuildLobby();
        BuildGameLobby();
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    private static void BuildLobby()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene.unity", OpenSceneMode.Single);
        Transform source = GameObject.Find(SourceRow).transform;
        Transform settings = source.parent;
        bool fresh = settings.Find(RegionRow) == null;
        RegionSelector create = MakeRow(source, settings, RegionRow, "Region");
        if (fresh)
        {
            Grow((RectTransform)settings, RowStep);                                    // 설정 상자 한 줄 늘림
            var list = (RectTransform)settings.parent.Find("RoomListScrollView");   // 방 목록 윗변을 그만큼 내림
            if (list != null) list.offsetMax -= new Vector2(0f, RowStep);
        }

        // 볼 지역: 패널 아래 새로고침 버튼 오른쪽
        Transform panel = settings.parent;
        RegionSelector view = MakeRow(source, panel, ViewRegionRow, "View");
        var refresh = (RectTransform)panel.Find("RefreshButton");
        var rt = (RectTransform)view.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(360f, 40f);
        rt.anchoredPosition = new Vector2(refresh != null ? refresh.anchoredPosition.x + refresh.sizeDelta.x + 16f : 200f, 25f);
        ((RectTransform)view.transform.Find("Label")).sizeDelta = new Vector2(90f, 40f);
        ShiftControls(view.transform, -85f); // 이름표를 줄인 만큼 당겨 방 만들기 버튼과 떨어지게

        var lobby = Object.FindFirstObjectByType<LobbyController>(FindObjectsInactive.Include);
        var so = new SerializedObject(lobby);
        so.FindProperty("createRegion").objectReferenceValue = create;
        so.FindProperty("viewRegion").objectReferenceValue = view;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildGameLobby()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameLobbyScene.unity", OpenSceneMode.Single);
        var menu = Object.FindFirstObjectByType<RoomSettingsMenu>(FindObjectsInactive.Include);
        Transform source = null;
        foreach (RoomSettingField f in menu.GetComponentsInChildren<RoomSettingField>(true)) if (f.name == SourceRow) source = f.transform;
        Transform rows = source.parent;
        bool fresh = rows.Find(RegionRow) == null;
        RegionSelector region = MakeRow(source, rows, RegionRow, "Region");
        if (fresh)
        {
            Grow((RectTransform)rows, RowStep);
            var box = (RectTransform)rows.parent;
            box.sizeDelta += new Vector2(0f, RowStep); // 창을 한 줄만큼 키운다(아래 버튼·안내 문구 자리는 그대로)
        }
        var so = new SerializedObject(menu);
        so.FindProperty("regionField").objectReferenceValue = region;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 타임어택 줄을 복제해 지역 줄로: RoomSettingField·경고 문구·입력칸을 빼고 ◀ 이름 ▶ 로.
    private static RegionSelector MakeRow(Transform source, Transform parent, string name, string label)
    {
        Transform old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject row = Object.Instantiate(source.gameObject, parent);
        row.name = name;
        var rt = (RectTransform)row.transform;
        rt.anchoredPosition = ((RectTransform)source).anchoredPosition - new Vector2(0f, RowStep);
        Object.DestroyImmediate(row.GetComponent<RoomSettingField>());
        Transform warning = row.transform.Find("Warning");
        if (warning != null) Object.DestroyImmediate(warning.gameObject);
        row.transform.Find("Label").GetComponent<TMP_Text>().text = label;

        // 입력칸 → 이름 칸(배경 이미지는 그대로, 글자 칸만 새로)
        Transform input = row.transform.Find("Input");
        TMP_Text number = input.Find("Text Area/Text") != null ? input.Find("Text Area/Text").GetComponent<TMP_Text>() : null; // 숫자 글자(색·크기)
        TMP_Text labelText = row.transform.Find("Label").GetComponent<TMP_Text>();                                           // 한글이 되는 글꼴
        Color numberColor = number != null ? number.color : Color.black; // 아래에서 입력칸 자식을 지우기 전에 읽어 둔다
        float numberSize = number != null ? number.fontSize : labelText.fontSize;
        Object.DestroyImmediate(input.GetComponent<TMP_InputField>());
        for (int i = input.childCount - 1; i >= 0; i--) Object.DestroyImmediate(input.GetChild(i).gameObject);
        input.name = "Value";
        ((RectTransform)input).sizeDelta = new Vector2(ValueWidth, ((RectTransform)input).sizeDelta.y);
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(input, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one; textRt.offsetMin = textRt.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.font = labelText.font;
        text.fontSize = numberSize;
        text.color = numberColor;
        text.alignment = TextAlignmentOptions.Center;
        text.text = "Korea";

        var increase = (RectTransform)row.transform.Find("Increase");
        increase.anchoredPosition += new Vector2(ValueWidth - 80f, 0f); // 넓어진 이름 칸만큼 오른쪽 버튼을 민다
        row.transform.Find("Decrease").GetComponentInChildren<TMP_Text>().text = "\u25C0"; // ◀ (다른 줄의 ▼▲와 같은 도형 글자)
        increase.GetComponentInChildren<TMP_Text>().text = "\u25B6";                        // ▶

        var selector = row.AddComponent<RegionSelector>();
        selector.EditorSetup(row.transform.Find("Decrease").GetComponent<Button>(), increase.GetComponent<Button>(), text);
        return selector;
    }

    private static void ShiftControls(Transform row, float dx)
    {
        foreach (string child in new[] { "Decrease", "Value", "Increase" })
            ((RectTransform)row.Find(child)).anchoredPosition += new Vector2(dx, 0f);
    }

    private static void Grow(RectTransform rt, float height) => rt.sizeDelta += new Vector2(0f, height);
}
