using UnityEngine;

// 드는 방식(EscapePlan.md §1.9, D31). 두 손 아이템은 한 번에 1개만 가질 수 있고, 들고 있는 동안 칸 바꾸기와 줍기가 막힌다.
// 새 아이템이 생겨도 이 값만 정하면 된다.
public enum HoldType
{
    OneHanded,
    TwoHanded,
}

public enum ItemCategory
{
    Material, // 탈출 재료(공구상자 포함)
    Tool,     // 뿅망치·스턴건·물풍선
}

// 아이템 하나의 데이터(EscapePlan.md §5.10). 새 아이템은 에셋만 추가하고 EscapeCatalogSO에 등록한다.
// 표시 이름은 에셋에 입력한다(코드에 한글 금지).
[CreateAssetMenu(menuName = "TagOfChaos/Escape/Item", fileName = "Item_")]
public class ItemSO : ScriptableObject
{
    [Tooltip("네트워크로 주고받는 ID(영문, 겹치면 안 된다).")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [Tooltip("바닥·손·장치에 보이는 모델. 비어 있으면 tint 색의 기본 도형을 쓴다.")]
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private HoldType holdType = HoldType.TwoHanded;
    [SerializeField] private ItemCategory category = ItemCategory.Material;
    [Tooltip("도구일 때만: 도구 수치.")]
    [SerializeField] private ToolSO tool;
    [Tooltip("손에 들었을 때 몸 방향 기준 추가 위치·각도(미세 조정, Request1009Plan.md §16.4). 한 손 = 오른손, 두 손 = 두 손 가운데 기준.")]
    [SerializeField] private Vector3 holdOffset;
    [SerializeField] private Vector3 holdEuler;

    public string ItemId => itemId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public GameObject ModelPrefab => modelPrefab;
    public Color Tint => tint;
    public HoldType HoldType => holdType;
    public ItemCategory Category => category;
    public ToolSO Tool => tool;
    public Vector3 HoldOffset => holdOffset;
    public Vector3 HoldEuler => holdEuler;
    public bool LocksSlotSwitch => holdType == HoldType.TwoHanded;
    public bool IsTool => category == ItemCategory.Tool && tool != null;

#if UNITY_EDITOR
    // 에셋 빌더 전용(EscapeAssetsBuilder).
    public void EditorSetup(string id, string name, Color color, HoldType hold, ItemCategory cat, ToolSO toolData)
    {
        itemId = id;
        displayName = name;
        tint = color;
        holdType = hold;
        category = cat;
        tool = toolData;
    }

    public void EditorSetVisuals(Sprite iconSprite, GameObject model)
    {
        icon = iconSprite;
        modelPrefab = model;
    }
#endif
}
