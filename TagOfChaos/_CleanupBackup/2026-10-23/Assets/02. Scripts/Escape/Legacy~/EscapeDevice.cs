using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 쿠키 탈출 장치(EscapePlan.md §1.6, §5.5): 재료 칸 표시, 설치, 스파이 훔치기, 완성 뒤 쿠키 탈출구.
// 칸 모양은 자식 "Slot_00".."Slot_nn"이 있으면 그 위치를 쓰고(Blender 모델), 없으면 장치 둘레에 만든다.
// 빈 칸 = 어두운 받침(Socket), 채워진 칸 = 재료 모양(Part). 완성되면 "ESC_Glow"를 켜고 맵별 연출을 한다.
// 재료 넣기는 E를 holdSeconds(2초), 스파이 빼기는 EscapeAuthority.StealHoldSeconds(10초) 누르고 있어야 한다(Request1009Plan.md §19.5) —
// 빼기는 누르기 시작할 때 StealBegin을 보내 방장도 10초를 검사한다(조작한 클라이언트가 바로 빼지 못하게).
// 완성 뒤 타기는 지금처럼 누르는 즉시(Request1009Plan.md §1).
public class EscapeDevice : MonoBehaviour, IHoldInteractable, IHoldStartListener, IInteractionLabel
{
    [SerializeField, Min(0f)] private float holdSeconds = 2f;
    [SerializeField, Min(0.5f)] private float interactionRange = 3.8f; // 모델 본체(최대 6 m 길이) 끝에서도 닿게
    [SerializeField, Min(0.5f)] private float slotRingRadius = 1.6f;
    [SerializeField] private float slotHeight = 1.2f;

    private EscapeManager manager;
    private readonly List<GameObject> slotParts = new List<GameObject>();
    private readonly List<GameObject> slotSockets = new List<GameObject>();
    private readonly List<string> shownItems = new List<string>();
    private readonly List<bool> modelAnchors = new List<bool>(); // 칸 자리가 모델에 있으면 재료를 그 자리에 정확히 놓는다
    private bool registered;
    private bool complete;
    private Transform glow;
    private Renderer[] glowRenderers = new Renderer[0];
    private readonly HashSet<int> seenWaiting = new HashSet<int>();
    private readonly List<Transform> anchorCache = new List<Transform>();

    // 맵별 탈출 연출(있으면). 완성되면 타는 곳(Board)이 상호작용 위치가 된다.
    public EscapeSequence Sequence { get; private set; }

    public Vector3 InteractionPoint
    {
        get
        {
            if (complete && Sequence != null) return Sequence.BoardPoint.position;
            int slot = ActionSlot(out _);
            return slot >= 0 && slot < slotSockets.Count && slotSockets[slot] != null ? slotSockets[slot].transform.parent.position : transform.position;
        }
    }

    // 지금 로컬 쿠키가 손댈 수 있는 칸 중 가장 가까운 칸(없으면 -1): 들고 있는 재료를 끼울 수 있는 빈 칸, 또는 스파이가 뺄 수 있는 칸.
    // 상호작용 위치가 이 칸이라, 이미 채워진 칸(처음부터 채워진 칸 포함) 앞에서는 E가 뜨지 않고, 누르면 바로 그 칸에 끼운다(2026-10-03 —
    // 예전에는 가장 가까운 칸이면 채워진 칸이어도 E가 떴고, 누르면 다른 빈 칸에 들어갔다).
    private int ActionSlot(out bool steal)
    {
        steal = false;
        PlayerInventory inv = PlayerInventory.Local;
        if (manager == null || manager.State == null || inv == null) return -1;
        Vector3 me = LocalCookiePosition();
        List<EscapeState.Slot> slots = manager.State.DeviceSlots;
        int best = -1;
        float bestDist = float.MaxValue;
        ItemSO held = inv.HeldItem;
        bool spySteal = held == null && RoomState.IsLocalSpy();
        for (int i = 0; i < slots.Count && i < slotSockets.Count; i++)
        {
            bool usable = held != null ? !slots[i].Filled && slots[i].Allows(held.ItemId) : spySteal && IsStealable(i);
            if (!usable || slotSockets[i] == null || !slotSockets[i].transform.parent.gameObject.activeInHierarchy) continue;
            float d = Flat(slotSockets[i].transform.parent.position - me);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        steal = best >= 0 && held == null;
        return best;
    }

    private Vector3 LocalCookiePosition()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.View != null && c.View.IsMine && c.Role == CharacterRole.Cookie) return c.gameObject.transform.position;
        return transform.position;
    }

    // 칸 자리와 이만큼 넘게 높이가 다르면 닿지 않는다(시계탑 지하 유적의 제단을 지상에서 만지지 못하게).
    public const float MaxReachHeight = 4f;

    // 장치까지의 수평 거리(이번 판에 쓰는 칸 자리 중 가장 가까운 곳, 칸 자리가 없으면 중심). 방장이 설치·훔치기 거리를 잴 때 쓴다.
    // 칸 모드는 모델 자리 중 일부만 쓰므로(Request1003bPlan.md §1) 상태가 있으면 쓰는 자리만 본다.
    public float DistanceTo(Vector3 p)
    {
        float best = float.MaxValue;
        bool anyAnchor = false;
        List<EscapeState.Slot> slots = manager != null && manager.State != null ? manager.State.DeviceSlots : null;
        int count = slots != null ? slots.Count : 16;
        for (int i = 0; i < count; i++)
        {
            Transform anchor = SlotAnchor(slots != null ? slots[i].Anchor : i);
            if (anchor == null) { if (slots == null) break; continue; }
            anyAnchor = true;
            if (Mathf.Abs(anchor.position.y - p.y) > MaxReachHeight) continue;
            best = Mathf.Min(best, Flat(anchor.position - p));
        }
        return anyAnchor ? best : Flat(transform.position - p);
    }

    // 칸 자리(모델의 Slot_nn). 탈것 칸은 움직이는 차 아래에 있어서 깊이 찾고, 찾은 결과는 기억한다.
    private Transform SlotAnchor(int i)
    {
        while (anchorCache.Count <= i) anchorCache.Add(EscapeSequence.FindDeep(transform, $"Slot_{anchorCache.Count:00}"));
        return anchorCache[i];
    }

    // 모델의 칸 자리(Slot_nn) 위치. 모델에 그 자리가 없으면 false.
    public bool TryAnchorPosition(int anchorIndex, out Vector3 position)
    {
        Transform anchor = anchorIndex >= 0 ? SlotAnchor(anchorIndex) : null;
        position = anchor != null ? anchor.position : transform.position;
        return anchor != null;
    }

    // 장치 칸 i(상태의 칸 번호)의 월드 위치 — 끼우기·빼기 소리를 그 자리에서 낸다. 칸 자리가 없으면 장치 중심.
    public Vector3 SlotPosition(int i)
    {
        List<EscapeState.Slot> slots = manager != null && manager.State != null ? manager.State.DeviceSlots : null;
        Transform anchor = slots != null && i >= 0 && i < slots.Count ? SlotAnchor(slots[i].Anchor) : null;
        if (anchor == null && i >= 0 && i < slotSockets.Count && slotSockets[i] != null) anchor = slotSockets[i].transform.parent;
        return anchor != null ? anchor.position : transform.position;
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    public float InteractionRange => interactionRange;

    private void Awake() => Sequence = GetComponent<EscapeSequence>();

    private void OnEnable() => SetRegistered(true);
    private void OnDisable() => SetRegistered(false);

    private void SetRegistered(bool value)
    {
        if (registered == value) return;
        registered = value;
        if (value) InteractableRegistry.Register(this);
        else InteractableRegistry.Unregister(this);
    }

    public void Refresh(EscapeManager owner)
    {
        manager = owner;
        EscapeState s = owner.State;
        if (s == null) return;
        EnsureSlots(s.DeviceSlots);
        for (int i = 0; i < s.DeviceSlots.Count; i++)
        {
            EscapeState.Slot slot = s.DeviceSlots[i];
            string itemId = slot.ItemIndex >= 0 ? s.Items[slot.ItemIndex].Id : (slot.Prefilled && slot.Accepts.Length > 0 ? slot.Accepts[0] : null);
            SetSlotVisual(i, itemId, slot.Filled);
        }

        bool nowComplete = s.DeviceComplete;
        if (nowComplete != complete)
        {
            complete = nowComplete;
            if (Sequence == null) EscapeExitFx.SetComplete(transform, owner.Recipe.ExitKind, complete); // 맵 연출이 없을 때의 빛 기둥
        }
        PlayBoardingForNewPassengers(s);
    }

    // 새로 탄 쿠키마다 타는 연출을 한 번씩(모든 클라이언트). 그 쿠키 몸은 같은 순간 숨겨지므로 마지막 위치에서 시작한다.
    private void PlayBoardingForNewPassengers(EscapeState s)
    {
        if (s.Waiting.Count == 0) { seenWaiting.Clear(); return; }
        foreach (int actor in s.Waiting)
        {
            if (!seenWaiting.Add(actor)) continue;
            Vector3 from = BoardPositionOf(actor);
            if (Sequence != null) Sequence.OnCookieBoarded(from);
            else BoardingFx.Play(from, transform.position, BoardingFx.Style.Hop);
        }
    }

    private Vector3 BoardPositionOf(int actor)
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.Role == CharacterRole.Cookie && c.View != null && c.View.Owner != null && c.View.Owner.ActorNumber == actor)
                return c.gameObject.transform.position + Vector3.up;
        return InteractionPoint + Vector3.back * 2f;
    }

    private void Update()
    {
        if (Sequence != null) Sequence.Step(manager != null ? manager.State : null, PhotonNetwork.Time);
        else if (complete) EscapeExitFx.Tick(transform, manager != null ? manager.Recipe.ExitKind : EscapeExitKind.CakeRocket);
    }

    // 완성 뒤 연출이 탈 수 있는 단계에 도착했고, 아직 출발하지 않았는지.
    private bool BoardingOpen()
    {
        EscapeState s = manager != null ? manager.State : null;
        return s != null && s.CompletedAt > 0 && s.DepartedAt <= 0 && PhotonNetwork.Time >= s.CompletedAt + manager.BoardReadySeconds;
    }

    // 칸 i의 받침은 그 칸의 모델 자리(Slot.Anchor)에 만든다. 칸 모드는 자리 8개 중 이번 판에 쓰는 자리만 칸이 된다.
    private void EnsureSlots(List<EscapeState.Slot> slots)
    {
        int count = slots.Count;
        while (slotSockets.Count < count)
        {
            int i = slotSockets.Count;
            int anchorIndex = slots[i].Anchor;
            Transform anchor = SlotAnchor(anchorIndex);
            modelAnchors.Add(anchor != null);
            if (anchor == null)
            {
                anchor = new GameObject($"Slot_{anchorIndex:00}").transform;
                anchor.SetParent(transform, false);
                float angle = (count <= 1 ? 0f : i * 360f / count);
                anchor.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * slotRingRadius + Vector3.up * slotHeight;
                anchorCache[anchorIndex] = anchor;
            }
            var socket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(socket.GetComponent<Collider>());
            socket.name = "Socket";
            socket.transform.SetParent(anchor, false);
            socket.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
            EscapeVisuals.Tint(socket, new Color(0.08f, 0.06f, 0.1f), 0f);
            slotSockets.Add(socket);
            slotParts.Add(null);
            shownItems.Add(null);
        }
        for (int i = 0; i < slotSockets.Count; i++) slotSockets[i].transform.parent.gameObject.SetActive(i < count);
    }

    private void SetSlotVisual(int i, string itemId, bool filled)
    {
        string wanted = filled ? itemId : null;
        if (shownItems[i] == wanted) return;
        if (slotParts[i] != null) Destroy(slotParts[i]);
        slotParts[i] = null;
        shownItems[i] = wanted;
        if (wanted == null) return;
        ItemSO def = manager.Catalog.Find(wanted);
        GameObject part = EscapeVisuals.CreateItemModel(def, slotSockets[i].transform.parent);
        part.transform.localPosition = modelAnchors[i] ? Vector3.zero : Vector3.up * 0.25f;
        slotParts[i] = part;
    }

    public bool CanInteract(IGameCharacter character)
    {
        if (!EscapeManager.ActionsAllowed) return false; // 변장 시간(§1.3)
        if (manager == null || manager.State == null || character.Role != CharacterRole.Cookie) return false; // 괴물 X(D33)
        if (complete) return BoardingOpen(); // 쿠키는 탑승, 스파이는 "들어갈 수 없음"(같은 아이콘 — 정체가 드러나지 않게, D35)
        return ActionSlot(out _) >= 0;
    }

    public float HoldSecondsFor(IGameCharacter character)
    {
        if (complete) return 0f;
        return ActionSlot(out bool steal) >= 0 && steal ? EscapeAuthority.StealHoldSeconds : holdSeconds;
    }

    // 빼기를 누르기 시작했다 — 방장에게 그 칸과 시각을 알린다(다 누르면 지금처럼 Steal).
    public void OnHoldStarted(IGameCharacter character)
    {
        if (complete || manager == null) return;
        int slot = ActionSlot(out bool steal);
        if (slot >= 0 && steal) manager.Request(EscapeOp.StealBegin, slot);
    }

    public void Interact(IGameCharacter character)
    {
        PlayerInventory inv = PlayerInventory.Local;
        if (manager == null || inv == null) return;
        if (complete)
        {
            if (RoomState.IsLocalSpy()) EscapeHud.Toast(EscapeTextsSO.Current.cannotEnter);
            else manager.Request(EscapeOp.Exit);
            return;
        }
        int slot = ActionSlot(out bool steal);
        if (slot < 0 || Flat(slotSockets[slot].transform.parent.position - LocalCookiePosition()) > interactionRange) return; // 손 닿는 칸만(먼 빈 칸에 끼워지지 않게)
        if (steal) manager.Request(EscapeOp.Steal, slot);
        else manager.Request(EscapeOp.Install, inv.Selected, EscapeNet.EncodeSlot(slot)); // 고른 칸에 끼운다
    }

    public string GetLabel(IGameCharacter viewer) => complete && BoardingOpen() ? EscapeTextsSO.Current.escapeDevice : null;

    // 스파이가 뺄 수 있는 칸: 쿠키가 끼운 재료 중 자기 로켓에 아직 필요한 종류(D27). 처음부터 채워진 칸은 뺄 수 없다.
    private bool IsStealable(int i)
    {
        EscapeState s = manager.State;
        EscapeState.Slot slot = s.DeviceSlots[i];
        if (slot.Prefilled || slot.ItemIndex < 0) return false;
        return SpyRocket.RocketNeeds(s, manager.Catalog, s.Items[slot.ItemIndex].Id);
    }
}
