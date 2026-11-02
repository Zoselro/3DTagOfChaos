// 개편 시험 맵 Test_<Map> 씬의 경로(Request1009Plan.md §25.5 O, 단계 G1에서 시작).
// Test_GingerbreadVillage·Test_HauntedBakery는 판에 쓰고, 나머지 Test_ 씬은 실제 모델 맵 빌더(CandyForest·Carnival·ChocolateFactory)의 원본 틀이다.
// 예전에는 옛 Game_<Map> 씬을 복사해 이 씬들을 다시 만들었지만, Game_ 맵을 없애며 그 부분도 없앴다(Request1009Plan.md §47 CL5).
// 지금 Test_ 씬은 이미 완성된 원본이므로 다시 만들지 않는다.
public static class TestMapBuilder
{
    public const string SceneFolder = "Assets/Scenes/Maps/Test";
    public const string ScenePrefix = "Test_";

    public static string ScenePath(string map) => $"{SceneFolder}/{ScenePrefix}{map}.unity";
    public static string SceneName(string map) => ScenePrefix + map;
}
