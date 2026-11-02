using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

// 뒤틀린 과자 동화 개편(TwistedCandyPlan.md V1~V4) 회귀 방지: 로비·대기실 분위기, 실내 소리 영역 계산.
// 옛 Game_ 맵의 분위기·소품·숨을 곳·지하 소리·충돌체 시험은 그 맵과 함께 없앴다(Request1009Plan.md §47 CL5).
public class TwistedTests
{
    private SceneSetup[] savedSetup;

    [OneTimeSetUp]
    public void SaveSetup()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save the open scenes before running map tests.");
        savedSetup = EditorSceneManager.GetSceneManagerSetup();
    }

    [OneTimeTearDown]
    public void RestoreSetup()
    {
        if (savedSetup != null && savedSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(savedSetup);
    }

    [Test]
    public void Lobbies_HaveTwistedLook()
    {
        EditorSceneManager.OpenScene(TwistedAtmosphere.GameLobbyScene, OpenSceneMode.Single);
        var volume = Object.FindFirstObjectByType<PostProcessVolume>();
        Assert.NotNull(volume, "Waiting room needs its PostFX volume.");
        Assert.AreEqual(TwistedAtmosphere.GameLobbyProfile, AssetDatabase.GetAssetPath(volume.sharedProfile));
        Assert.NotNull(Camera.main.GetComponent<PostProcessLayer>());
        Assert.IsFalse(volume.sharedProfile.TryGetSettings(out Vignette _), "Waiting room vignette must stay removed (Request1009Plan.md §16.5).");

        EditorSceneManager.OpenScene(TwistedAtmosphere.LobbyScene, OpenSceneMode.Single);
        var bg = Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(i => i.name == "BackGround");
        Assert.AreEqual(TwistedAtmosphere.LobbyBackground, AssetDatabase.GetAssetPath(bg.sprite));
    }

    [Test]
    public void IndoorZone_StepFadesTowardsTarget()
    {
        float v = IndoorZone.Step(0f, true, IndoorZone.FadeSeconds * 0.5f);
        Assert.AreEqual(0.5f, v, 1e-4f);
        Assert.AreEqual(1f, IndoorZone.Step(v, true, 10f));
        Assert.AreEqual(0f, IndoorZone.Step(v, false, 10f));
        Assert.Less(IndoorZone.WallCutoffHz, SpatialGain.FarCutoffHz, "Through-wall muffle must be darker than plain distance muffle.");
    }
}
