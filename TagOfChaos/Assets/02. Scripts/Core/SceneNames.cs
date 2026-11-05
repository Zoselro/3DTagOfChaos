// 씬 전환 시 쓰이는 씬 이름 상수. GameManager/ColorTag/Lobby 세 도메인이 공통으로 참조하므로
// 특정 도메인에 두지 않고 Core/에 별도로 둔다 (architecture-review.md §4/§11.2).
public static class SceneNames
{
    public const string Lobby = "LobbyScene";
    public const string GameLobby = "GameLobbyScene";
    // 게임 씬은 판마다 GameSettings.gameMapScenes에서 고른다(예전 상수 Game = "GameScene"은 빌드에서 빠져 제거, Bug-fix-plan.md §41 ㊷).
    // 옛 1세대 맵 접두사 "Game_"은 그 맵과 함께 없앴다(Request1009Plan.md §47 CL5).
    public const string TestMapScenePrefix = "Test_"; // 개편 시험 맵(Request1009Plan.md §25 G1)

    // 맵 씬 이름 → 맵 이름(레시피·연출이 쓰는 이름). Test_CandyForest·CandyForest → CandyForest.
    public static string MapOf(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return sceneName;
        if (sceneName.StartsWith(TestMapScenePrefix)) return sceneName.Substring(TestMapScenePrefix.Length);
        return sceneName;
    }

    public static bool IsTestMap(string sceneName) => sceneName != null && sceneName.StartsWith(TestMapScenePrefix);

    // 실제 모델 맵 씬은 맵 이름 그대로(예: CandyForest, Request1009Plan.md §28.11 Q11 · CursedCandyCarnival §36 · ChocolateFactory §42 · GingerbreadVillage §51).
    public static readonly string[] RealMapScenes = { "CandyForest", "CursedCandyCarnival", "ChocolateFactory", "GingerbreadVillage" };

    // 판에 쓰는 맵 씬인가(Test_·실제 모델 맵).
    public static bool IsMapScene(string sceneName) =>
        sceneName != null && (IsTestMap(sceneName) || System.Array.IndexOf(RealMapScenes, sceneName) >= 0);
}
