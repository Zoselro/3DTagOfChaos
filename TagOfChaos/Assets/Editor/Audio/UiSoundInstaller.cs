using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 빌드 씬·UI 프리팹의 모든 Button에 UiSound를 붙인다(SoundPlan.md S2). 이미 붙은 버튼은 건드리지 않는다(조정값 유지).
// 프리팹을 먼저 처리해 씬의 프리팹 인스턴스는 프리팹에서 물려받게 하고(맵 씬 5개는 GameSceneCore 하나), 씬에만 있는 버튼은 씬에 붙인다.
// 클릭 소리는 버튼 이름으로 고른다(ClickFor) — 새 버튼 이름이 표에 없으면 일반 클릭.
public static class UiSoundInstaller
{
    private const string MenuPath = "Tools/TagOfChaos/Audio/Attach UI Sounds";
    public static readonly string[] PrefabFolders = { "Assets/Resources", "Assets/04. Prefabs" };

    public static SoundId ClickFor(string buttonName)
    {
        switch (buttonName)
        {
            case "ResetButton": return SoundId.PaintReset;
            case "StartGameButton": return SoundId.UiStart;
            case "YesButton":
            case "ApplyButton":
            case "MakeRoomButton":
            case "RandomJoinButton":
            case "JoinButton":
                return SoundId.UiConfirm;
            case "NoButton":
            case "BackButton":
            case "ResumeButton":
                return SoundId.UiCancel;
            default:
                return SoundId.UiClick;
        }
    }

    [MenuItem(MenuPath)]
    public static void AttachAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        int added = 0;

        // Resources(단독 UI 프리팹) 먼저 → 04. Prefabs(그것을 품은 큰 프리팹)
        foreach (string path in UiPrefabPaths())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int n = Attach(root.GetComponentsInChildren<Button>(true));
            if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            added += n;
            if (n > 0) Debug.Log($"[UiSound] {path}: +{n}");
        }

        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int n = 0;
            foreach (GameObject go in scene.GetRootGameObjects()) n += Attach(go.GetComponentsInChildren<Button>(true));
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[UiSound] {s.path}: +{n}");
            }
            added += n;
        }

        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log($"[UiSound] Attached {added} UiSound components.");
    }

    public static IEnumerable<string> UiPrefabPaths() =>
        PrefabFolders.SelectMany(folder => AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath))
                     .Where(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<Button>(true) != null);

    // 버튼마다 UI 소리 컴포넌트를 붙인다(이미 있으면 건너뜀). 다른 설치 도구(SoundSettingsInstaller)도 쓴다.
    public static int Attach(IEnumerable<Button> buttons)
    {
        int n = 0;
        foreach (Button b in buttons)
        {
            if (b.GetComponent<UiSound>() != null) continue;
            UiSound sound = b.gameObject.AddComponent<UiSound>();
            sound.EditorSetup(ClickFor(b.name), SoundId.UiHover);
            EditorUtility.SetDirty(b.gameObject);
            n++;
        }
        return n;
    }
}
