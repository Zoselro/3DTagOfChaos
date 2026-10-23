using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;

// 쿠키·스파이 공용 4칸 인벤토리(EscapePlan.md §1.9, D26·D30~D32). 아이템이 어디 있는지는 방장의 EscapeState가 정하고,
// 이 컴포넌트는 내 칸을 읽어 고른 칸(SelectedSlot, 본인만 쓰는 Player Prop)을 관리한다.
// - 숫자키 1~4·마우스 휠로 칸을 고른다. 두 손 아이템을 든 동안에는 바꿀 수 없다.
// - G키: 손에 든 아이템을 떨어뜨린다. 손에 든 것이 없어지면 다음 번호 칸부터 찾아 바로 든다.
// - 마우스 왼쪽: 손에 든 도구를 쓴다(ToolUser).
// 쿠키 캐릭터(HideOrSeekPlayer, 본인)가 탈출 모드 맵에서 붙인다.
public class PlayerInventory : MonoBehaviour
{
    public const int SlotCount = EscapeAuthority.InventorySlots;

    public static PlayerInventory Local { get; private set; }

    private HideOrSeekPlayer owner;
    private EscapeManager manager;
    private ToolUser toolUser;
    private readonly int[] heldItems = { -1, -1, -1, -1 };
    private int publishedSlot = -1;

    public int Selected { get; private set; }
    public ItemSO HeldItem => ItemAt(Selected);
    public bool HandsLocked => HeldItem != null && HeldItem.LocksSlotSwitch;
    public event System.Action Changed;

    public void Init(HideOrSeekPlayer cookie, EscapeManager escape)
    {
        owner = cookie;
        manager = escape;
        Local = this;
        toolUser = gameObject.AddComponent<ToolUser>();
        toolUser.Init(this, escape);
        manager.StateChanged += OnStateChanged;
        OnStateChanged();
        Publish();
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= OnStateChanged;
        if (Local == this) Local = null;
    }

    public int HeldItemIndexAt(int slot) => slot >= 0 && slot < SlotCount ? heldItems[slot] : -1;

    public ItemSO ItemAt(int slot) => slot >= 0 && slot < SlotCount && heldItems[slot] >= 0 ? manager.ItemDef(heldItems[slot]) : null;

    // 두 손 아이템을 든 동안에는 줍지 못하고, 칸이 가득 차면 줍지 못한다(§1.9). 방장도 같은 규칙으로 다시 확인한다.
    public bool CanPickUp(ItemSO item)
    {
        if (item == null || HandsLocked) return false;
        for (int i = 0; i < SlotCount; i++) if (heldItems[i] < 0) return true;
        return false;
    }

    private void OnStateChanged()
    {
        if (manager.State == null || PhotonNetwork.LocalPlayer == null) return;
        int actor = PhotonNetwork.LocalPlayer.ActorNumber;
        int previousSelected = heldItems[Selected];
        int newTwoHandedSlot = -1;
        for (int i = 0; i < SlotCount; i++)
        {
            int before = heldItems[i];
            heldItems[i] = manager.State.HeldItemAt(actor, i);
            ItemSO def = ItemAt(i);
            if (heldItems[i] >= 0 && heldItems[i] != before && def != null && def.LocksSlotSwitch) newTwoHandedSlot = i;
        }

        if (newTwoHandedSlot >= 0) Selected = newTwoHandedSlot; // 두 손 아이템은 들고 가는 모습이 보여야 하므로 바로 든다
        else if (previousSelected >= 0 && heldItems[Selected] < 0) AutoEquipNext(); // D32

        Publish();
        Changed?.Invoke();
    }

    // 손에 든 것이 없어지면 다음 번호 칸부터 차례로 찾아 처음 나오는 아이템을 든다(끝 칸 다음은 1번부터).
    // 예: 1·3·4번에 아이템, 2번이 빔 → 1번을 떨어뜨리면 3번을 든다. 모두 비면 빈손.
    public static int NextOccupied(int[] slots, int from)
    {
        for (int step = 1; step <= slots.Length; step++)
        {
            int next = (from + step) % slots.Length;
            if (slots[next] >= 0) return next;
        }
        return from;
    }

    private void AutoEquipNext() => Selected = NextOccupied(heldItems, Selected);

    private void Update()
    {
        if (owner == null || !owner.IsMine || manager.State == null) return;
        if (owner.IsMovementLocked) return; // 잡힘·들림·기절 중에는 조작하지 않는다

        int wanted = PlayerInput.SlotKeyPressed;
        if (wanted < 0)
        {
            float wheel = PlayerInput.SlotWheelDelta;
            if (wheel > 0.01f) wanted = (Selected + SlotCount - 1) % SlotCount;
            else if (wheel < -0.01f) wanted = (Selected + 1) % SlotCount;
        }
        if (wanted >= 0) TrySelect(wanted);

        if (PlayerInput.DropPressed && heldItems[Selected] >= 0)
            manager.Request(EscapeOp.Drop, Selected, 0, owner.transform.position + owner.transform.forward * 0.8f);

        if (PlayerInput.UseToolPressed && EscapeManager.ActionsAllowed && !IsPointerOverUi() && HeldItem != null && HeldItem.IsTool)
            toolUser.Use(Selected, HeldItem);

        // 헬리콥터 모자: 들고 E — 근처에 E로 쓰는 사물이 없을 때(Request1009Plan.md §25.5 H)
        if (PlayerInput.InteractPressed && EscapeManager.ActionsAllowed && !CharacterInteractor.HasFocus
            && HeldItem != null && HeldItem.IsTool && HeldItem.Tool.Kind == ToolKind.HeliHat)
            manager.Request(EscapeOp.HatFly, Selected);
    }

    public bool TrySelect(int slot)
    {
        if (slot < 0 || slot >= SlotCount || slot == Selected || HandsLocked) return false; // 두 손 아이템을 든 동안 바꿀 수 없다
        Selected = slot;
        Publish();
        Changed?.Invoke();
        return true;
    }

    public void RequestDropHeld()
    {
        if (heldItems[Selected] >= 0) manager.Request(EscapeOp.Drop, Selected, 0, owner.transform.position);
    }

    private void Publish()
    {
        if (publishedSlot == Selected || PhotonNetwork.LocalPlayer == null) return;
        publishedSlot = Selected;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.SelectedSlot, Selected } });
    }

    private static bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
