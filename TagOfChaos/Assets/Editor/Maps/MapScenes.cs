using System;

// 판에 쓰는 맵 씬 경로(에디터 도구·시험 공용). 옛 1세대 Game_<Map> 씬과 그 빌더(MapSceneBuilder·MapCompactor)는
// Request1009Plan.md §47 CL5에서 없앴다. 지금 판의 맵은 실제 모델 맵(SceneNames.RealMapScenes)과 나머지 Test_<Map>이다.
public static class MapScenes
{
    public static readonly string[] MapNames = { "CandyForest", "GingerbreadVillage", "ChocolateFactory", "CursedCandyCarnival", "HauntedBakery" };

    // 통행 격자의 기본 반경(m) — 옛 축소 맵 기준(한 변 140 m). 실제 모델 맵 빌더는 맵마다 Options.Half를 따로 준다.
    public const float DefaultGridHalf = 70f;

    public static bool IsRealMap(string map) => Array.IndexOf(SceneNames.RealMapScenes, map) >= 0;

    // 그 맵이 판에서 쓰는 씬: 실제 모델 맵이면 Assets/Scenes/Maps/<Map>.unity, 아니면 Test_<Map>.
    public static string ScenePath(string map) => IsRealMap(map) ? $"Assets/Scenes/Maps/{map}.unity" : TestMapBuilder.ScenePath(map);
}
