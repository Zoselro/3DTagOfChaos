using UnityEngine;
using UnityEngine.Serialization;

// 맵별 탈출 방식(EscapePlan.md §1.6). 칸 모드(놀이공원·베이커리·공장)는 고정 재료 + 무작위 칸, 개수 모드(진저브레드 룬·
// 캔디숲 전지)는 필요 재료 수만큼 칸을 만든다. 스파이 로켓 칸(2개)도 여기 둔다. 코드는 맵과 상관없이 하나다.
public enum RecipeMode
{
    Slots,   // 종류별 칸 묶음. 고정 칸 + 필요한 수만큼 무작위 칸, 고르지 않은 칸은 채워진 상태로 시작
    Counter, // 필요 재료 수만큼 칸. 재료 종류는 counterItems 중에서 무작위(모양만 다름)
}

public enum EscapeExitKind
{
    RollerCoaster,
    BakeryMachine,
    ChocolateTrain,
    RuneAltar,
    CakeRocket,
}

[CreateAssetMenu(menuName = "TagOfChaos/Escape/Recipe", fileName = "Recipe_")]
public class EscapeRecipeSO : ScriptableObject
{
    [System.Serializable]
    public class SlotGroup
    {
        public ItemSO item;
        [Tooltip("오른쪽 위 표시 이름. 비우면 아이템 이름(예: 공구상자 칸은 '수리').")]
        public string label;
        [Min(1)] public int slotCount = 1;
        [Tooltip("항상 필요한 칸 수(처음부터 비어 있음).")]
        [Min(0)] public int fixedRequired;
    }

    [System.Serializable]
    public class RocketSlot
    {
        public string label;
        public ItemSO[] accepts;
    }

    [SerializeField] private string mapName;
    [SerializeField] private EscapeExitKind exitKind;
    [SerializeField] private RecipeMode mode = RecipeMode.Slots;
    [SerializeField] private SlotGroup[] groups = new SlotGroup[0];
    [Tooltip("개수 모드: 칸 이름(예: 룬).")]
    [SerializeField] private string counterLabel;
    [Tooltip("개수 모드: 칸마다 무작위로 고르는 재료(예: 알사탕 전지 4색).")]
    [SerializeField] private ItemSO[] counterItems = new ItemSO[0];
    [Tooltip("칸을 늘 최대 수(인원표의 가장 많은 쿠키 수)만큼만 쓰고, 이번 판에 필요 없는 칸은 채워진 상태로 시작한다(2026-10-03). " +
             "칸 모드는 모델의 칸 자리 중 고정 칸 + 무작위 자리를 골라 그 수만큼만 켠다.")]
    [FormerlySerializedAs("counterFillsCapacity")]
    [SerializeField] private bool fillsCapacity;
    [SerializeField] private RocketSlot[] rocketSlots = new RocketSlot[0];
    [Tooltip("스파이 1명당 일반 상자에 더 넣는 공구상자(D18). 공구상자가 탈출 재료가 아닌 맵에서 쓴다.")]
    [SerializeField] private ItemSO spyToolbox;
    [SerializeField, Min(0)] private int extraToolboxesPerSpy = 1;

    [Header("Request1009Plan.md §25 — 사분면 건물 4곳")]
    [Tooltip("건물 4곳에 넣는 재료 4종. 판마다 어느 건물에 어떤 재료를 넣을지 섞는다. 위의 칸 묶음·개수 모드·로켓 칸은 예전 규칙(쓰지 않음, 에셋 호환).")]
    [SerializeField] private ItemSO[] buildingMaterials = new ItemSO[0];
    public ItemSO[] BuildingMaterials => buildingMaterials;
    [Tooltip("켜면 섞지 않고 buildingMaterials 순서대로 Building_Q1~Q4에 준다 — 건물 간판이 재료를 말하는 맵(저주받은 놀이공원, §36).")]
    [SerializeField] private bool fixedBuildingOrder;
    public bool FixedBuildingOrder => fixedBuildingOrder;
    [Tooltip("스파이 수리키트 수(§26: 0 = 로켓 칸 수). 1개는 스파이 상자, 나머지는 일반 상자. 위의 rocketSlots가 맵별 로켓 재료다.")]
    [SerializeField, Min(0)] private int repairKitCount;
    public int RepairKitCount => repairKitCount;

    public string MapName => mapName;
    public EscapeExitKind ExitKind => exitKind;
    public RecipeMode Mode => mode;
    public SlotGroup[] Groups => groups;
    public string CounterLabel => counterLabel;
    public ItemSO[] CounterItems => counterItems;
    public bool FillsCapacity => fillsCapacity;
    public RocketSlot[] RocketSlots => rocketSlots;
    public ItemSO SpyToolbox => spyToolbox;
    public int ExtraToolboxesPerSpy => extraToolboxesPerSpy;

    // 늘어나는 칸 전체 수(고정 제외) — 필요 재료 수가 이 범위 안이어야 한다.
    public int MaxRequired
    {
        get
        {
            if (mode == RecipeMode.Counter) return int.MaxValue;
            int total = 0;
            foreach (SlotGroup g in groups) total += g.slotCount;
            return total;
        }
    }

#if UNITY_EDITOR
    public void EditorSetBuildingMaterials(ItemSO[] materials) => buildingMaterials = materials ?? new ItemSO[0];
    public void EditorSetBuildingMaterials(ItemSO[] materials, bool fixedOrder)
    {
        EditorSetBuildingMaterials(materials);
        fixedBuildingOrder = fixedOrder;
    }

    public void EditorSetup(string map, EscapeExitKind exit, RecipeMode recipeMode, SlotGroup[] slotGroups, string counterName,
        ItemSO[] counters, RocketSlot[] rocket, ItemSO toolbox, int toolboxesPerSpy, bool fillsCapacity = false)
    {
        this.fillsCapacity = fillsCapacity;
        mapName = map;
        exitKind = exit;
        mode = recipeMode;
        groups = slotGroups ?? new SlotGroup[0];
        counterLabel = counterName;
        counterItems = counters ?? new ItemSO[0];
        rocketSlots = rocket ?? new RocketSlot[0];
        spyToolbox = toolbox;
        extraToolboxesPerSpy = toolboxesPerSpy;
    }
#endif
}
