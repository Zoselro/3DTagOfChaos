using UnityEngine;

// 바닥에 놓인 아이템 하나의 모습과 줍기(E). EscapeManager가 풀로 만들어 두고 켜고 끈다(Instantiate/Destroy 반복 없음).
public class GroundItemView : MonoBehaviour, IInteractable, IInteractionLabel
{
    private EscapeManager manager;
    private int itemIndex = -1;
    private ItemSO shownItem;
    private GameObject model;
    private bool registered;

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => 1.8f;

    public static GroundItemView Create(EscapeManager owner, Transform parent)
    {
        var go = new GameObject("GroundItem");
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<GroundItemView>();
        view.manager = owner;
        go.SetActive(false);
        return view;
    }

    public void Show(int index, ItemSO item, Vector3 position)
    {
        itemIndex = index;
        transform.position = position;
        if (shownItem != item)
        {
            if (model != null) Destroy(model);
            model = EscapeVisuals.CreateItemModel(item, transform);
            model.transform.localPosition = Vector3.up * 0.25f;
            shownItem = item;
        }
        // §90 F4 이끼·덤불이 덮는 자리면 그 위로 띄운다(최대 1.3 m) + 쿠키·스파이 화면에 반짝임
        lift = GroundCover.LiftAt(position);
        if (model != null) model.transform.localPosition = Vector3.up * (0.25f + lift);
        if (sparkle == null) sparkle = GroundItemSparkle.Attach(transform);
        sparkle.SetHeight(0.25f + lift);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        SetRegistered(true);
    }

    public void Hide()
    {
        itemIndex = -1;
        SetRegistered(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable() => SetRegistered(false);

    private float lift;
    private GroundItemSparkle sparkle;

    private void Update()
    {
        if (model != null) model.transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World); // 바닥 아이템이 눈에 띄게 천천히 돈다
        if (model != null && lift > 0.01f) model.transform.localPosition = Vector3.up * (0.25f + lift + Mathf.Sin(Time.time * 2f + itemIndex) * 0.08f);   // §90 F4 띄운 아이템은 천천히 위아래로
    }

    private void SetRegistered(bool value)
    {
        if (registered == value) return;
        registered = value;
        if (value) InteractableRegistry.Register(this);
        else InteractableRegistry.Unregister(this);
    }

    public bool CanInteract(IGameCharacter character)
    {
        if (!EscapeManager.ActionsAllowed) return false; // 변장 시간(§1.3)
        if (itemIndex < 0 || character.Role != CharacterRole.Cookie) return false; // 괴물은 줍지 못한다(D33)
        PlayerInventory inv = PlayerInventory.Local;
        return inv != null && inv.CanPickUp(shownItem);
    }

    public void Interact(IGameCharacter character)
    {
        if (itemIndex >= 0) manager.Request(EscapeOp.PickUp, itemIndex);
    }

    public string GetLabel(IGameCharacter viewer) => shownItem != null ? shownItem.DisplayName : null;
}
