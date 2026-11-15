using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 로비 화면 새 모양(Request1009Plan.md §79): 사용자가 준 UI 시트에서 오려낸 그림(Assets/08. Resources/LobbyUI)을 9분할 스프라이트로 가져오고,
// LobbyScene의 기존 오브젝트(기능·연결은 그대로)에 그림·자리·글꼴을 입힌 뒤 효과(UiHoverGlow·UiGemSparkle·UiShineSweep·LobbyAmbience)를 붙인다.
// 방 목록 한 줄 프리팹(RoomListItem)도 같은 모양으로. 여러 번 실행해도 같은 결과. 한글 문구는 Texts로 받는다(코드에 한글 금지).
public static class LobbySkinInstaller
{
    public const string SpriteFolder = "Assets/08. Resources/LobbyUI";
    public const string ScenePath = "Assets/Scenes/LobbyScene.unity";
    public const string RoomItemPrefab = "Assets/Resources/UI/Scene/RoomListItem/RoomListItem.prefab";
    private const float Ppu = 2f; // 그림은 화면 크기의 2배로 오렸다 — 9분할 테두리를 절반 크기로 그린다

    public static readonly Color Cream = new Color(1f, 0.92f, 0.76f, 1f);
    public static readonly Color Ink = new Color(0.26f, 0.15f, 0.07f, 1f);
    public static readonly Color InkStrong = new Color(0.17f, 0.08f, 0.03f, 1f);
    private const float SmallPpu = 3f;  // 작은 값 칸: 장식 테두리를 더 작게
    private const float ButtonPpu = 2.6f; // 버튼: 양 끝 장식을 조금 줄여 글자 자리 확보
    public static readonly Color InkFaint = new Color(0.26f, 0.15f, 0.07f, 0.55f);
    public static readonly Color Purple = new Color(0.72f, 0.32f, 1f, 0.9f);

    public class Texts
    {
        public string userIdPlaceholder = "Enter your nickname...";
        public string roomNamePlaceholder = "Enter a room name...";
        public string makeRoom = "Make Room";
        public string randomJoin = "Random Join";
        public string refresh = "Refresh";
        public string settings = "Settings";
        public string join = "Join";
    }

    // 9분할 테두리(2배 그림 기준 px: 왼쪽, 아래, 오른쪽, 위). 없으면 단순 그림.
    private static readonly (string name, Vector4 border)[] Sliced =
    {
        ("Btn_Primary", new Vector4(150, 70, 180, 70)),
        ("Btn_Wood", new Vector4(152, 64, 180, 64)),
        ("Field", new Vector4(80, 50, 80, 50)),
        ("Frame_Main", new Vector4(130, 130, 130, 130)),
        ("Frame_List", new Vector4(100, 100, 100, 100)),
        ("Row_Room", new Vector4(190, 70, 300, 70)),
        ("ScrollTrack", new Vector4(0, 120, 0, 120)),
        ("ScrollHandle", new Vector4(0, 60, 0, 60)),
        ("Bar", new Vector4(110, 40, 110, 40)),
    };

    [MenuItem("Tools/TagOfChaos/UI/Apply Lobby Skin")]
    private static void Menu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[LobbySkin] stop Play Mode first"); return; }
        Debug.Log(Apply(new Texts()));
    }

    public static void ImportSprites()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            string n = System.IO.Path.GetFileNameWithoutExtension(path);
            bool bg = n == "BG_Lobby";
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = !bg;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            var s = Sliced.FirstOrDefault(x => x.name == n);
            ti.spriteBorder = s.name != null ? s.border : Vector4.zero;
            ti.SaveAndReimport();
        }
    }

    private static Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{n}.png");

    private static TMP_FontAsset font;
    private static TMP_FontAsset Font
    {
        get
        {
            if (font != null) return font;
            string g = AssetDatabase.FindAssets("NotoSansKR SDF t:TMP_FontAsset").FirstOrDefault();
            font = g != null ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g)) : null;
            return font;
        }
    }

    public static string Apply(Texts tx)
    {
        ImportSprites();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject canvasGo = GameObject.Find("LobbyUICanvas");
        if (canvasGo == null) return "[LobbySkin] LobbyUICanvas not found";
        Transform canvas = canvasGo.transform;
        var panel = (RectTransform)canvas.Find("LobbyPanel");

        // 배경 + 분위기
        var bg = canvas.Find("BackGround").GetComponent<Image>();
        bg.sprite = S("BG_Lobby");
        var fitter = bg.GetComponent<AspectRatioFitter>();
        if (fitter != null) fitter.aspectRatio = bg.sprite.rect.width / bg.sprite.rect.height;
        var amb = Child(bg.transform, "Ambience");
        Stretch(amb);
        Ensure<LobbyAmbience>(amb.gameObject);

        // 큰 액자
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, -10f);
        panel.sizeDelta = new Vector2(780f, 880f);
        SetImage(panel.GetComponent<Image>(), "Frame_Main");
        Ensure<UiGemSparkle>(panel.gameObject).Configure(new[] { new Vector2(0.055f, 0.965f), new Vector2(0.945f, 0.965f), new Vector2(0.055f, 0.035f), new Vector2(0.945f, 0.035f) }, 54f, 40f);

        // 맨 위 줄: 닉네임 입력 + 랜덤 입장
        var user = (RectTransform)panel.Find("UserIdInput");
        TopLeft(user, 70f, -78f, 410f, 62f);
        StyleInput(user.GetComponent<TMP_InputField>(), tx.userIdPlaceholder);
        var rnd = (RectTransform)panel.Find("RandomJoinButton");
        TopRight(rnd, -66f, -72f, 226f, 74f);
        StyleButton(rnd.GetComponent<Button>(), "Btn_Wood", "Icon_Dice", tx.randomJoin, false);

        // 방 이름
        var room = (RectTransform)panel.Find("RoomNameInput");
        TopLeft(room, 70f, -150f, 640f, 60f);
        StyleInput(room.GetComponent<TMP_InputField>(), tx.roomNamePlaceholder);

        var feedback = (RectTransform)panel.Find("FeedbackText");
        TopLeft(feedback, 70f, -214f, 640f, 26f);
        var ft = feedback.GetComponent<TMP_Text>();
        ft.font = Font; ft.fontSize = 19f; ft.color = new Color(1f, 0.75f, 0.6f); ft.alignment = TextAlignmentOptions.Center;

        // 장식선
        var div = Child(panel, "Divider");
        div.anchorMin = div.anchorMax = div.pivot = new Vector2(0.5f, 1f);
        div.anchoredPosition = new Vector2(0f, -236f); div.sizeDelta = new Vector2(600f, 34f);
        var di = Ensure<Image>(div.gameObject);
        di.sprite = S("Divider"); di.preserveAspect = true; di.raycastTarget = false; di.color = Color.white;

        // 설정 줄(인원·스파이·국가)
        var settings = (RectTransform)panel.Find("RoomSettings");
        settings.anchorMin = new Vector2(0f, 1f); settings.anchorMax = new Vector2(1f, 1f); settings.pivot = new Vector2(0.5f, 1f);
        settings.offsetMin = new Vector2(96f, settings.offsetMin.y); settings.offsetMax = new Vector2(-96f, settings.offsetMax.y);
        settings.anchoredPosition = new Vector2(0f, -276f);
        settings.sizeDelta = new Vector2(-192f, 154f);
        int row = 0;
        foreach (Transform r in settings)
        {
            if (!r.gameObject.activeSelf || !r.name.StartsWith("Row_")) continue;
            var rr = (RectTransform)r;
            rr.anchoredPosition = new Vector2(0f, -row * 52f);
            rr.sizeDelta = new Vector2(0f, 48f);
            StyleRow(r, 200f, r.name == "Row_Region" ? 150f : 110f);
            row++;
        }

        // 방 목록
        var list = (RectTransform)panel.Find("RoomListScrollView");
        list.anchorMin = Vector2.zero; list.anchorMax = Vector2.one; list.pivot = new Vector2(0.5f, 0.5f);
        list.offsetMin = new Vector2(64f, 118f);
        list.offsetMax = new Vector2(-64f, -440f);
        SetImage(list.GetComponent<Image>(), "Frame_List");
        var sr = list.GetComponent<ScrollRect>();
        var viewport = (RectTransform)list.Find("Viewport");
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.pivot = new Vector2(0f, 1f);
        viewport.offsetMin = new Vector2(34f, 30f); viewport.offsetMax = new Vector2(-62f, -30f);
        var hbar = list.Find("Scrollbar Horizontal");
        if (hbar != null) { sr.horizontalScrollbar = null; sr.horizontal = false; hbar.gameObject.SetActive(false); }
        var vbar = (RectTransform)list.Find("Scrollbar Vertical");
        if (vbar != null)
        {
            vbar.anchorMin = new Vector2(1f, 0f); vbar.anchorMax = new Vector2(1f, 1f); vbar.pivot = new Vector2(1f, 0.5f);
            vbar.anchoredPosition = new Vector2(-22f, 0f);
            vbar.sizeDelta = new Vector2(30f, -40f);
            SetImage(vbar.GetComponent<Image>(), "ScrollTrack");
            var sb = vbar.GetComponent<Scrollbar>();
            if (sb.handleRect != null)
            {
                SetImage(sb.handleRect.GetComponent<Image>(), "ScrollHandle");
                var area = (RectTransform)sb.handleRect.parent;
                area.offsetMin = new Vector2(2f, 26f); area.offsetMax = new Vector2(-2f, -26f);
                sb.handleRect.offsetMin = new Vector2(0f, sb.handleRect.offsetMin.y); sb.handleRect.offsetMax = new Vector2(0f, sb.handleRect.offsetMax.y);
            }
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            Ensure<UiGemSparkle>(vbar.gameObject).Configure(new[] { new Vector2(0.5f, 0.97f), new Vector2(0.5f, 0.03f) }, 34f, 28f);
        }
        var content = list.Find("Viewport/Content");
        var vlg = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
        if (vlg != null) { vlg.spacing = 6f; vlg.padding = new RectOffset(0, 0, 4, 4); vlg.childControlHeight = false; vlg.childForceExpandHeight = false; }

        // 아래 줄 받침 + 새로고침 · 볼 국가 · 방 만들기
        var bar = Child(panel, "BottomBar");
        bar.SetSiblingIndex(list.GetSiblingIndex() + 1);
        bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(1f, 0f); bar.pivot = new Vector2(0.5f, 0f);
        bar.offsetMin = new Vector2(46f, 26f); bar.offsetMax = new Vector2(-46f, 0f);
        bar.sizeDelta = new Vector2(-92f, 84f);
        bar.anchoredPosition = new Vector2(0f, 26f);
        SetImage(Ensure<Image>(bar.gameObject), "Bar").raycastTarget = false;

        var refresh = (RectTransform)panel.Find("RefreshButton");
        BottomLeft(refresh, 60f, 36f, 190f, 62f);
        StyleButton(refresh.GetComponent<Button>(), "Btn_Wood", "Icon_Refresh", tx.refresh, false);
        refresh.SetAsLastSibling();

        var view = (RectTransform)panel.Find("Row_ViewRegion");
        BottomLeft(view, 260f, 42f, 240f, 50f);
        StyleViewRegion(view);
        view.SetAsLastSibling();

        var make = (RectTransform)panel.Find("MakeRoomButton");
        BottomRight(make, -62f, 36f, 210f, 62f);
        StyleButton(make.GetComponent<Button>(), "Btn_Primary", "Icon_Castle", tx.makeRoom, true);
        Ensure<UiShineSweep>(make.gameObject);
        make.SetAsLastSibling();

        // 설정(오른쪽 위)
        var set = (RectTransform)canvas.Find("SettingsButton");
        set.anchorMin = set.anchorMax = set.pivot = new Vector2(1f, 1f);
        set.anchoredPosition = new Vector2(-34f, -30f);
        set.sizeDelta = new Vector2(196f, 66f);
        StyleButton(set.GetComponent<Button>(), "Btn_Wood", "Icon_Gear", tx.settings, false);

        // LobbyPanel은 프리팹 인스턴스라 바꾼 값을 덮어쓰기로 기록해야 저장된다
        foreach (Component c in canvas.GetComponentsInChildren<Component>(true))
            if (c != null && PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        // 인스턴스에서는 자식 순서를 바꿀 수 없다 — 프리팹(이 장면만 쓴다)에 반영한 뒤 프리팹 안에서 그리는 순서를 맞춘다(받침 → 버튼)
        if (PrefabUtility.IsPartOfPrefabInstance(panel.gameObject))
        {
            string prefabPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(panel.gameObject));
            PrefabUtility.ApplyPrefabInstance(panel.gameObject, InteractionMode.AutomatedAction);
            GameObject pr = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (string n in new[] { "RoomListScrollView", "BottomBar", "RefreshButton", "Row_ViewRegion", "MakeRoomButton" })
                {
                    Transform t = pr.transform.Find(n);
                    if (t != null) t.SetAsLastSibling();
                }
                PrefabUtility.SaveAsPrefabAsset(pr, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(pr); }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string item = StyleRoomItem(tx);
        return "[LobbySkin] applied — " + item;
    }

    // ---------------- 부분 꾸미기 ----------------

    private static void StyleRow(Transform row, float x0, float valueWidth)
    {
        var label = row.Find("Label") as RectTransform;
        if (label != null)
        {
            label.anchoredPosition = new Vector2(0f, 0f); label.sizeDelta = new Vector2(190f, 48f);
            var t = label.GetComponent<TMP_Text>(); t.font = Font; t.fontSize = 25f; t.color = Cream; t.alignment = TextAlignmentOptions.MidlineLeft;
        }
        var dec = row.Find("Decrease") as RectTransform;
        var val = (row.Find("Input") ?? row.Find("Value")) as RectTransform;
        var inc = row.Find("Increase") as RectTransform;
        if (dec != null) { dec.anchoredPosition = new Vector2(x0, 0f); dec.sizeDelta = new Vector2(52f, 46f); StyleArrow(dec, "Btn_ArrowL"); }
        if (val != null)
        {
            val.anchoredPosition = new Vector2(x0 + 58f, 0f); val.sizeDelta = new Vector2(valueWidth, 46f);
            SetImage(val.GetComponent<Image>(), "Field").pixelsPerUnitMultiplier = SmallPpu;
            foreach (var t in val.GetComponentsInChildren<TMP_Text>(true)) { t.font = Font; t.fontSize = 24f; t.color = InkStrong; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center; }
            var input = val.GetComponent<TMP_InputField>();
            if (input != null) NoSelectEffect(input); // 입력칸을 눌러도 빛·색 변화 없음(사용자 요청)
        }
        if (inc != null) { inc.anchoredPosition = new Vector2(x0 + 64f + valueWidth, 0f); inc.sizeDelta = new Vector2(52f, 46f); StyleArrow(inc, "Btn_ArrowR"); }
        var warn = row.Find("Warning") as RectTransform;
        if (warn != null)
        {
            warn.anchoredPosition = new Vector2(x0 + 124f + valueWidth, 0f); warn.sizeDelta = new Vector2(150f, 48f);
            var t = warn.GetComponent<TMP_Text>(); t.font = Font; t.fontSize = 16f; t.color = new Color(1f, 0.55f, 0.45f);
        }
    }

    private static void StyleViewRegion(RectTransform row)
    {
        var label = (RectTransform)row.Find("Label");
        label.anchoredPosition = Vector2.zero; label.sizeDelta = new Vector2(62f, 50f);
        var lt = label.GetComponent<TMP_Text>(); lt.font = Font; lt.fontSize = 19f; lt.color = Cream; lt.alignment = TextAlignmentOptions.MidlineLeft;
        var dec = (RectTransform)row.Find("Decrease"); dec.anchoredPosition = new Vector2(62f, 0f); dec.sizeDelta = new Vector2(40f, 40f); StyleArrow(dec, "Btn_ArrowL");
        var val = (RectTransform)row.Find("Value"); val.anchoredPosition = new Vector2(104f, 0f); val.sizeDelta = new Vector2(92f, 42f);
        SetImage(val.GetComponent<Image>(), "Field").pixelsPerUnitMultiplier = SmallPpu;
        foreach (var t in val.GetComponentsInChildren<TMP_Text>(true)) { t.font = Font; t.fontSize = 20f; t.color = InkStrong; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center; }
        var inc = (RectTransform)row.Find("Increase"); inc.anchoredPosition = new Vector2(198f, 0f); inc.sizeDelta = new Vector2(40f, 40f); StyleArrow(inc, "Btn_ArrowR");
    }

    private static void StyleArrow(RectTransform r, string sprite)
    {
        var img = r.GetComponent<Image>();
        SetImage(img, sprite);
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        foreach (var t in r.GetComponentsInChildren<TMP_Text>(true)) t.gameObject.SetActive(false); // 화살표는 그림에 있다
        Tint(r.GetComponent<Button>());
        Ensure<UiHoverGlow>(r.gameObject).Configure(Purple, 14f, false, 0f);
    }

    private static void StyleButton(Button b, string sprite, string icon, string label, bool primary)
    {
        var r = (RectTransform)b.transform;
        SetImage(b.GetComponent<Image>(), sprite).pixelsPerUnitMultiplier = ButtonPpu;
        Tint(b);
        float h = r.sizeDelta.y;
        var ic = Child(r, "Icon");
        ic.anchorMin = ic.anchorMax = new Vector2(0f, 0.5f); ic.pivot = new Vector2(0.5f, 0.5f);
        ic.anchoredPosition = new Vector2(h * 0.62f, 1f);
        ic.sizeDelta = Vector2.one * h * 0.62f;
        var ii = Ensure<Image>(ic.gameObject); ii.sprite = S(icon); ii.preserveAspect = true; ii.raycastTarget = false; ii.color = Color.white;
        var t = b.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
        if (t != null)
        {
            var tr = (RectTransform)t.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(h * 0.98f, 0f); tr.offsetMax = new Vector2(-h * 0.62f, 0f);
            t.text = label; t.font = Font; t.color = Cream;
            t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = primary ? 25f : 23f;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.fontStyle = FontStyles.Bold;
            t.outlineWidth = 0.18f; t.outlineColor = new Color32(40, 18, 10, 255);
        }
        Ensure<UiHoverGlow>(b.gameObject).Configure(Purple, primary ? 26f : 20f, false, primary ? 0.35f : 0f);
    }

    private static void StyleInput(TMP_InputField input, string placeholder)
    {
        SetImage(input.GetComponent<Image>(), "Field");
        var area = (RectTransform)input.textViewport;
        if (area != null) { area.offsetMin = new Vector2(46f, 6f); area.offsetMax = new Vector2(-46f, -6f); }
        if (input.textComponent != null) { input.textComponent.font = Font; input.textComponent.fontSize = 23f; input.textComponent.color = Ink; input.textComponent.alignment = TextAlignmentOptions.MidlineLeft; }
        if (input.placeholder is TMP_Text ph) { ph.text = placeholder; ph.font = Font; ph.fontSize = 22f; ph.color = InkFaint; ph.fontStyle = FontStyles.Italic; ph.alignment = TextAlignmentOptions.MidlineLeft; }
        input.caretColor = Ink; input.customCaretColor = true;
        input.selectionColor = new Color(0.72f, 0.32f, 1f, 0.35f);
        NoSelectEffect(input);
    }

    // 입력칸: 눌렀을 때 빛(UiHoverGlow)과 색 변화를 모두 뺀다
    private static void NoSelectEffect(TMP_InputField input)
    {
        var glow = input.GetComponent<UiHoverGlow>();
        if (glow != null) Object.DestroyImmediate(glow, true);
        input.transition = Selectable.Transition.None;
    }

    private static string StyleRoomItem(Texts tx)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RoomItemPrefab);
        try
        {
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 66f);
            var le = Ensure<LayoutElement>(root); le.preferredHeight = 66f; le.minHeight = 66f;
            SetImage(root.GetComponent<Image>(), "Row_Room");
            var name = (RectTransform)root.transform.Find("RoomNameText");
            if (name != null)
            {
                name.anchorMin = new Vector2(0f, 0f); name.anchorMax = new Vector2(0.6f, 1f);
                name.offsetMin = new Vector2(70f, 0f); name.offsetMax = new Vector2(-6f, 0f);
                var t = name.GetComponent<TMP_Text>(); t.font = Font; t.fontSize = 23f; t.color = Cream; t.alignment = TextAlignmentOptions.MidlineLeft; t.overflowMode = TextOverflowModes.Ellipsis;
            }
            var count = (RectTransform)root.transform.Find("PlayerCountText");
            if (count != null)
            {
                count.anchorMin = new Vector2(0.6f, 0f); count.anchorMax = new Vector2(0.72f, 1f); count.offsetMin = count.offsetMax = Vector2.zero;
                var t = count.GetComponent<TMP_Text>(); t.font = Font; t.fontSize = 21f; t.color = Cream; t.alignment = TextAlignmentOptions.Center;
            }
            var join = (RectTransform)root.transform.Find("JoinButton");
            if (join != null)
            {
                join.anchorMin = new Vector2(1f, 0.5f); join.anchorMax = new Vector2(1f, 0.5f); join.pivot = new Vector2(1f, 0.5f);
                join.anchoredPosition = new Vector2(-12f, 0f); join.sizeDelta = new Vector2(124f, 44f);
                var b = join.GetComponent<Button>();
                SetImage(b.GetComponent<Image>(), "Btn_Wood").pixelsPerUnitMultiplier = ButtonPpu * 1.2f;
                Tint(b);
                var t = join.GetComponentInChildren<TMP_Text>(true);
                if (t != null) { t.text = tx.join; t.font = Font; t.fontSize = 20f; t.color = Cream; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center; }
                Ensure<UiHoverGlow>(join.gameObject).Configure(Purple, 14f, false, 0f);
            }
            PrefabUtility.SaveAsPrefabAsset(root, RoomItemPrefab);
            return "room item ok";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------- 도우미 ----------------

    private static Image SetImage(Image img, string sprite)
    {
        if (img == null) return null;
        img.sprite = S(sprite);
        img.color = Color.white;
        var s = Sliced.FirstOrDefault(x => x.name == sprite);
        img.type = s.name != null ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = Ppu;
        img.fillCenter = true;
        return img;
    }

    // 눌림·비활성 색(밝아짐은 빛 효과가 맡는다)
    private static void Tint(Button b)
    {
        if (b == null) return;
        b.transition = Selectable.Transition.ColorTint;
        var c = b.colors;
        c.normalColor = Color.white; c.highlightedColor = new Color(1.06f, 1.02f, 1.08f); c.pressedColor = new Color(0.82f, 0.78f, 0.86f);
        c.selectedColor = Color.white; c.disabledColor = new Color(0.45f, 0.45f, 0.48f, 0.85f); c.fadeDuration = 0.1f;
        b.colors = c;
    }

    private static RectTransform Child(Transform parent, string n)
    {
        Transform t = parent.Find(n);
        if (t == null) { var go = new GameObject(n, typeof(RectTransform)); t = go.transform; t.SetParent(parent, false); }
        return (RectTransform)t;
    }

    private static T Ensure<T>(GameObject go) where T : Component { T c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }

    private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    private static void TopLeft(RectTransform r, float x, float y, float w, float h) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); }
    private static void TopRight(RectTransform r, float x, float y, float w, float h) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); }
    private static void BottomLeft(RectTransform r, float x, float y, float w, float h) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); }
    private static void BottomRight(RectTransform r, float x, float y, float w, float h) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); }
}
