using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

// 빌드에 들어가는 씬에 개발용 도구가 섞이지 않게 막는다(research.md R4.4-9 — 검증 도구가 맵 씬 5개에
// `__OfflineBoot`(OfflineModeBootstrap)를 저장해 둔 적이 있다). 씬 파일을 열지 않고 의존성만 보므로 빠르고,
// 꺼진 오브젝트나 프리팹 안에 숨은 컴포넌트도 잡는다.
public class BuildSceneTests
{
    private const string DevScriptFolder = "Assets/02. Scripts/Dev/";
    private const string DevOnlyScene = "Assets/Scenes/PlayerTestScene.unity"; // 개발용 시험 씬은 허용

    private static IEnumerable<string> ReleaseScenes() =>
        EditorBuildSettings.scenes.Where(s => s.enabled && s.path != DevOnlyScene).Select(s => s.path);

    [Test]
    public void ReleaseScenes_Exist()
    {
        Assert.IsNotEmpty(ReleaseScenes().ToList(), "No release scenes in the build settings.");
    }

    [TestCaseSource(nameof(ReleaseScenes))]
    public void ReleaseScene_HasNoDevScripts(string scenePath)
    {
        string[] dev = AssetDatabase.GetDependencies(scenePath, true)
                                    .Where(p => p.StartsWith(DevScriptFolder))
                                    .ToArray();
        Assert.IsEmpty(dev, $"{scenePath} uses dev-only scripts: {string.Join(", ", dev)}");
    }

    // 코드가 Shader.Find로만 찾는 셰이더는 "항상 포함" 목록에 있어야 빌드에 들어간다(research.md N-01 — 물풍선 물방울, §47 CL3).
    [Test]
    public void RuntimeFoundShaders_AreAlwaysIncluded()
    {
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        SerializedProperty list = settings.FindProperty("m_AlwaysIncludedShaders");
        var names = new List<string>();
        for (int i = 0; i < list.arraySize; i++)
        {
            var shader = list.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.Shader;
            if (shader != null) names.Add(shader.name);
        }
        CollectionAssert.Contains(names, WaterBubbleView.ShaderName);
    }
}
