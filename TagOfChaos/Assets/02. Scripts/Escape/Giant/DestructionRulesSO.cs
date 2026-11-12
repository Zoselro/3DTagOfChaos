using UnityEngine;

// 거대 괴물이 부수는 물체를 맵에서 고르는 규칙(Request1009Plan.md §66.2·66.3). Resources/Escape/DestructionRules에서 읽는다.
// 씬에 표시를 붙이지 않고 모든 화면이 같은 규칙으로 같은 순서(계층 순서)로 고른다 — 맵을 다시 만들어도(MapSceneBuilder) 그대로 맞는다.
[CreateAssetMenu(menuName = "TagOfChaos/DestructionRules", fileName = "DestructionRules")]
public class DestructionRulesSO : ScriptableObject
{
    public const string ResourcePath = "Escape/DestructionRules";

    [Tooltip("Map/ 아래에서 안으로 들어가 그 아이들을 물체로 보는 묶음 이름(정확히 같음). Quadrant_로 시작하는 묶음도 들어간다.")]
    [SerializeField] private string[] containers =
    {
        "Buildings", "HideBuildings", "Landmarks", "Underground", "Deco", "Rides", "Edge", "Roads", "Fence", "Forest", "Background",
    };
    [SerializeField] private string containerPrefix = "Quadrant_";

    [Tooltip("부서지지 않는 물체 이름 앞부분(시계탑 몸체·롤러코스터 입구(탈출구)·탈출구·엘리베이터).")]
    [SerializeField] private string[] keepUnits = { "GIN_ClockTower", "CoasterEntrance", "Exit_", "Elevator" };

    [Tooltip("2번에 부서지는 텐트 등급 이름 앞부분.")]
    [SerializeField] private string[] tentPrefixes = { "MO_Tent", "CF_Tent", "CHO_Shed" };

    [Tooltip("10번에 부서지는 계단(진저브레드 시계탑 아래 나선 내리막) 이름 앞부분 — §68 GP6.")]
    [SerializeField] private string[] stairsPrefixes = { "Helix_" };
    [Tooltip("계단과 한 길목으로 보는 부서지지 않는 몸체(시계탑) 이름 앞부분 — 그 안의 사람도 계단 구역으로 본다.")]
    [SerializeField] private string stairsLinkPrefix = "GIN_ClockTower";
    [SerializeField, Min(1f)] private float stairsLinkRadius = 30f;

    [Tooltip("조각 모델이 없어도 4번에 부서지는 건물 묶음.")]
    [SerializeField] private string[] buildingContainers = { "Buildings", "HideBuildings", "Landmarks", "Rides" };

    [Tooltip("물체 안에서 무너져도 남기는 부분(재료 꽂는 곳·빛기둥·수리 자리) 이름 앞부분.")]
    [SerializeField] private string[] keepParts = { "Socket_", "ESC_Glow", "Repair_Point" };

    [Tooltip("가로·세로가 이보다 크고 조각 모델이 없는 덩어리(땅·숲 배경)는 고르지 않는다.")]
    [SerializeField, Min(10f)] private float lumpFootprint = 60f;

    [Tooltip("Blender로 미리 조각낸 건물의 원본 이름(<원본 이름>_Fractured). 모델은 Resources/Escape/Fractured/<이름>에서 필요할 때만 읽는다(§73.11).")]
    [SerializeField] private string[] fracturedNames = new string[0];

    [Tooltip("금 간 데칼 재질 3단계(작은 금 → 퍼진 금 → 깨진 구멍). Standard Fade + 노멀 — 에셋이라 빌드에 셰이더 변형이 들어간다.")]
    [SerializeField] private Material[] crackMaterials = new Material[3];

    public string[] Containers => containers;
    public string ContainerPrefix => containerPrefix;
    public string[] KeepUnits => keepUnits;
    public string[] TentPrefixes => tentPrefixes;
    public string[] StairsPrefixes => stairsPrefixes;
    public string StairsLinkPrefix => stairsLinkPrefix;
    public float StairsLinkRadius => stairsLinkRadius;
    public string[] BuildingContainers => buildingContainers;
    public string[] KeepParts => keepParts;
    public float LumpFootprint => lumpFootprint;
    public string[] FracturedNames => fracturedNames;
    public Material CrackMaterial(int stage) => crackMaterials != null && crackMaterials.Length > 0 ? crackMaterials[Mathf.Clamp(stage - 1, 0, crackMaterials.Length - 1)] : null;

    private static DestructionRulesSO cached;

    public static DestructionRulesSO Current
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<DestructionRulesSO>(ResourcePath);
            if (cached == null) cached = CreateInstance<DestructionRulesSO>();
            return cached;
        }
    }

#if UNITY_EDITOR
    public void EditorSetFracturedNames(string[] names) => fracturedNames = names;
    public void EditorSetCrackMaterials(Material[] mats) => crackMaterials = mats;
#endif
}
