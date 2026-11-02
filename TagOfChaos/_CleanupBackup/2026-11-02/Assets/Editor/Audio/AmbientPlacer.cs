using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 맵 환경음 배치(SoundPlan.md S7): 맵 씬마다 "Ambience" 루트 아래에 AMB_ 오브젝트를 표대로 만든다.
// - Table: 랜드마크 제자리 3D 소리(AmbientEmitter) — 공장 기계, 회전목마
// - Zones: 영역 안 2D 소리 + 배경음 낮춤(AmbientZone) — 진저브레드 지하 동굴
// - Beds : 맵 바탕 2D 소리(AmbientZone, 배경음은 그대로) — 맵마다 하나, 땅 위 전체. 공장은 기계 소리가 바탕을 대신한다.
// 다시 실행하면 기존 AMB_를 지우고 표대로 다시 만든다(위치는 여기서만 고친다).
public static class AmbientPlacer
{
    private const string MenuPath = "Tools/TagOfChaos/Audio/Place Ambient Emitters";
    public const string RootName = "Ambience";

    public struct Spot
    {
        public string Name;
        public Vector3 Position;
        public SoundId Sound;
        public Spot(string name, Vector3 position, SoundId sound) { Name = name; Position = position; Sound = sound; }
    }

    public static readonly Dictionary<string, Spot[]> Table = new Dictionary<string, Spot[]>
    {
        {
            "Game_ChocolateFactory", new[]
            {
                new Spot("AMB_MachineRoom_East", new Vector3(52f, 3f, 30f), SoundId.AmbFactory),
                new Spot("AMB_MachineRoom_West", new Vector3(-52f, 3f, -30f), SoundId.AmbFactory),
                new Spot("AMB_PressRow_North", new Vector3(4f, 3f, 56f), SoundId.AmbFactory),
                new Spot("AMB_PipeHub", new Vector3(0f, 14f, 0f), SoundId.AmbFactory),
            }
        },
        {
            "Game_CursedCandyCarnival", new[]
            {
                new Spot("AMB_Carousel", new Vector3(0f, 4f, 0f), SoundId.CarouselSpin), // CUR_Carousel_Top 중심
            }
        },
    };

    public struct Zone
    {
        public string Name;
        public SoundId Sound;
        public Bounds[] Boxes; // 월드 좌표
        public Zone(string name, SoundId sound, params Bounds[] boxes) { Name = name; Sound = sound; Boxes = boxes; }
    }

    // 진저브레드 지하: 유적 홀(escape_devices.py GIN_HALL, 시계탑 기준 −X 쪽) + 터널·나선 통로. 땅 위(y > −1)는 넣지 않는다.
    public static readonly Dictionary<string, Zone[]> Zones = new Dictionary<string, Zone[]>
    {
        {
            "Game_GingerbreadVillage", new[]
            {
                new Zone("AMB_Underground", SoundId.AmbCave,
                    new Bounds(new Vector3(-34f, -5.75f, 0f), new Vector3(41.5f, 9.5f, 41.5f)),
                    new Bounds(new Vector3(-4.7f, -5.75f, 0f), new Vector3(20.6f, 9.5f, 11.2f))),
            }
        },
    };

    // 맵 바탕 상자: 맵은 ±132 m(공장은 바닥이 더 넓지만 걷는 곳은 같음). 진저브레드는 지하(y < −1)를 빼 동굴 소리와 나눈다.
    public const float MapHalfSize = 150f;
    private static Bounds Surface(float floorY) => new Bounds(new Vector3(0f, (floorY + 200f) * 0.5f, 0f), new Vector3(MapHalfSize * 2f, 200f - floorY, MapHalfSize * 2f));

    public static readonly Dictionary<string, Zone> Beds = new Dictionary<string, Zone>
    {
        { "Game_CandyForest", new Zone("AMB_Bed", SoundId.AmbCandyForest, Surface(-20f)) },
        { "Game_GingerbreadVillage", new Zone("AMB_Bed", SoundId.AmbGingerbread, Surface(-1f)) },
        { "Game_CursedCandyCarnival", new Zone("AMB_Bed", SoundId.AmbCarnival, Surface(-20f)) },
        { "Game_HauntedBakery", new Zone("AMB_Bed", SoundId.AmbBakery, Surface(-20f)) },
    };

    private const float UndergroundMusicGain = 0.15f;

    [MenuItem(MenuPath)]
    public static void PlaceAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        var maps = new HashSet<string>(Table.Keys);
        maps.UnionWith(Zones.Keys);
        maps.UnionWith(Beds.Keys);
        foreach (string map in maps)
        {
            var scene = EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map.Replace("Game_", "")), OpenSceneMode.Single);
            Table.TryGetValue(map, out Spot[] spots);
            Zones.TryGetValue(map, out Zone[] zones);
            bool hasBed = Beds.TryGetValue(map, out Zone bed);
            Place(scene, spots ?? new Spot[0], zones ?? new Zone[0], hasBed ? new[] { bed } : new Zone[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Ambience] {map}: {(spots ?? new Spot[0]).Length} emitters, {(zones ?? new Zone[0]).Length} zones, bed {(hasBed ? bed.Sound.ToString() : "-")}");
        }
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    private static void Place(UnityEngine.SceneManagement.Scene scene, Spot[] spots, Zone[] zones, Zone[] beds)
    {
        GameObject root = null;
        foreach (GameObject go in scene.GetRootGameObjects()) if (go.name == RootName) root = go;
        if (root == null)
        {
            root = new GameObject(RootName);
            EditorSceneManager.MoveGameObjectToScene(root, scene);
        }
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = root.transform.GetChild(i);
            if (child.name.StartsWith("AMB_")) Object.DestroyImmediate(child.gameObject);
        }
        foreach (Spot spot in spots)
        {
            var go = new GameObject(spot.Name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = spot.Position;
            go.AddComponent<AmbientEmitter>().EditorSetup(spot.Sound);
        }
        foreach (Zone zone in zones)
        {
            var go = new GameObject(zone.Name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<AmbientZone>().EditorSetup(zone.Sound, zone.Boxes, UndergroundMusicGain);
        }
        foreach (Zone bed in beds)
        {
            var go = new GameObject(bed.Name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<AmbientZone>().EditorSetup(bed.Sound, bed.Boxes, 1f);
        }
    }
}
