using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 오프라인 개발 부트스트랩. 구 4라운드 색상 미니게임(ColorSelectionManager)이 GameRule.md v3.6로
// 완전히 대체되면서 자동 시작 기능은 일단 방 생성까지만 남겨뒀다 — 새 게임 룰(가마솥→색칠→GrabKill)
// 자동 시작은 §14.3 범위 밖의 별도 결정 사항.
public class OfflineModeBootstrap : MonoBehaviourPunCallbacks
{
    [SerializeField] private string testRoomName = "OfflineTestRoom";
    [SerializeField] private bool autoCreateRoom = false;
    [SerializeField] private bool spawnAsMonster = false;
    [Tooltip("혼자 테스트할 때 스파이로 시작한다(방에 들어가면 자기를 스파이 목록에 넣고 탈출 배치를 다시 짠다 — 스파이 상자·로켓 칸이 생긴다).")]
    [SerializeField] private bool spawnAsSpy = false;
    [Header("Giant monster test (Request1009Plan.md §64)")]
    [Tooltip("On: the giant monster always appears when the witch timer starts. Off: it never appears (offline room only — online uses the 30% roll).")]
    [SerializeField] private bool spawnGiant = true;
    [Tooltip("Witch time (time attack) in seconds for the offline room. The giant's speed steps up every (this / 10) seconds.")]
    [SerializeField, Min(10)] private int witchTimeSeconds = 60;
    [Tooltip("Start the witch timer as soon as the hunt begins (normally it starts on the first escape), so the giant can be tested right away.")]
    [SerializeField] private bool startWitchTimerOnHunt = true;

    // 아이템 시험(오프라인 방에서만 — 방장 판정을 그대로 거친다): 사냥이 시작되면 시작 아이템을 칸 1~4에 쥐여 주고,
    // 키로 선택한 칸의 아이템을 바꾸거나(전체 아이템 순서대로) 횟수를 다시 채우거나 비운다. 무한 사용을 켜면 다 쓴 아이템이 그 칸에 다시 생긴다.
    [Header("Item test (offline room only)")]
    [Tooltip("Give items and enable the item test keys below.")]
    [SerializeField] private bool itemTest = true;
    [Tooltip("Item IDs put in slots 1-4 when the hunt starts (empty = leave the slot). IDs: ColaCannon, Pumpkin, Flashbang, BoxingGlove, Skates, Recorder, HeliHat, StunGun, Hammer, WaterBalloon, Dismantle, RepairKit, ...")]
    [SerializeField] private string[] startItems = { "ColaCannon", "Pumpkin", "Flashbang", "BoxingGlove" };
    [Tooltip("A used-up item comes back in the same slot after a moment (endless testing).")]
    [SerializeField] private bool infiniteItems = true;
    [SerializeField] private KeyCode nextItemKey = KeyCode.F5;    // 선택한 칸 → 다음 아이템
    [SerializeField] private KeyCode previousItemKey = KeyCode.F6; // 선택한 칸 → 이전 아이템
    [SerializeField] private KeyCode refillKey = KeyCode.F7;      // 횟수·쿨타임 다시 채우기
    [SerializeField] private KeyCode clearKey = KeyCode.F8;       // 들고 있는 것 모두 비우기
    [Tooltip("Show the item test keys and the selected item at the top-left of the game view.")]
    [SerializeField] private bool showItemHelp = true;

    private const float RefillDelay = 1f;
    private bool witchTimerStarted, itemsGiven, itemsCleared;
    private readonly string[] slotItems = new string[EscapeAuthority.InventorySlots]; // 무한 사용: 칸마다 마지막으로 준 아이템
    private readonly float[] emptySince = new float[EscapeAuthority.InventorySlots];
    private List<ItemSO> itemCycle;
    private GUIStyle helpStyle;

    // PlayerSpawner/MonsterTestSpawner가 참조하는 개발용 플래그 — 씬 배치 순서에 의존하지 않도록 static.
    public static bool SpawnAsMonster { get; private set; }
    // 오프라인 방에서 거대 괴물이 나올지(체크박스). 이 부트스트랩이 없는 씬·온라인에서는 null(확률로 정한다).
    public static bool? GiantOverride { get; private set; }

    private void Awake()
    {
        PhotonNetwork.OfflineMode = true;
        SpawnAsMonster = spawnAsMonster;
        GiantOverride = spawnGiant;
    }

    // 혼자 스파이로 시험할 때: 방에 들어가면 자기를 스파이 목록에 넣는다.
    // 마녀 시간(초)은 늘 이 값으로 정한다(§64 — 거대 괴물 속도 단계 = 마녀 시간 ÷ 10).
    public override void OnJoinedRoom()
    {
        if (!PhotonNetwork.OfflineMode) return;
        var props = new ExitGames.Client.Photon.Hashtable { [NetKeys.TimeAttackDuration] = witchTimeSeconds };
        if (spawnAsSpy && !spawnAsMonster)
        {
            props[NetKeys.SpyActorNumbers] = new[] { PhotonNetwork.LocalPlayer.ActorNumber };
            props[NetKeys.EscapeState] = null; // 스파이가 있는 판으로 탈출 배치를 다시 짠다
        }
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // PhotonNetwork.OfflineMode와 SpawnAsMonster는 static이라 씬이 바뀌어도 남는다. 에디터에서 이 테스트 씬 다음에
    // 다른 씬을 실행할 때 오프라인 상태가 이어지지 않도록 되돌린다(research.md §8.23).
    private void OnDestroy()
    {
        SpawnAsMonster = false;
        GiantOverride = null;
        if (PhotonNetwork.OfflineMode && !PhotonNetwork.InRoom) PhotonNetwork.OfflineMode = false;
    }

    // 사냥이 시작되면 마녀 타이머를 바로 시작한다(시험용 — 원래는 첫 탈출 때). 쿠키 자리를 앞 샷으로.
    private void Update()
    {
        if (itemTest) UpdateItemTest();
        if (!startWitchTimerOnHunt || witchTimerStarted || !PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient) return;
        if (GamePhaseState.Current != GamePhase.Hunt || EscapeManager.Instance == null || EscapeManager.Instance.State == null) return;
        witchTimerStarted = true;
        HideOrSeekPlayer cookie = FindFirstObjectByType<HideOrSeekPlayer>();
        EscapeManager.Instance.StartWitchTimer(cookie != null ? cookie.transform.position : Vector3.zero);
    }

    // ---------------- 아이템 시험 ----------------

    private void UpdateItemTest()
    {
        if (!TryItemContext(out EscapeAuthority auth, out int actor)) return;
        if (!itemsGiven)
        {
            itemsGiven = true;
            for (int slot = 0; slot < slotItems.Length && slot < startItems.Length; slot++)
                if (!string.IsNullOrEmpty(startItems[slot]) && auth.DevGive(actor, startItems[slot], slot)) slotItems[slot] = startItems[slot];
        }
        int selected = PlayerInventory.Local != null ? PlayerInventory.Local.Selected : 0;
        if (Input.GetKeyDown(nextItemKey)) GiveCycled(auth, actor, selected, +1);
        if (Input.GetKeyDown(previousItemKey)) GiveCycled(auth, actor, selected, -1);
        if (Input.GetKeyDown(refillKey)) auth.DevRefill(actor);
        if (Input.GetKeyDown(clearKey))
        {
            auth.DevClear(actor);
            for (int i = 0; i < slotItems.Length; i++) slotItems[i] = null; // 비운 칸은 무한 사용으로 다시 채우지 않는다
        }
        if (infiniteItems) RefillUsedUp(auth, actor);
    }

    // 오프라인 방에서 사냥 중이고 내 캐릭터가 쿠키·스파이일 때만.
    private bool TryItemContext(out EscapeAuthority auth, out int actor)
    {
        auth = null;
        actor = -1;
        if (SpawnAsMonster || EscapeManager.Instance == null || EscapeManager.Instance.State == null || PhotonNetwork.LocalPlayer == null) return false;
        GamePhase phase = GamePhaseState.Current;
        if (phase != GamePhase.Hunt && phase != GamePhase.TimeAttack) return false;
        auth = EscapeManager.Instance.OfflineAuthority;
        actor = PhotonNetwork.LocalPlayer.ActorNumber;
        return auth != null && !RoomState.IsMonster(actor);
    }

    // 선택한 칸의 아이템을 전체 아이템 목록에서 다음(이전) 것으로 바꾼다. 도구가 먼저, 그다음 재료.
    private void GiveCycled(EscapeAuthority auth, int actor, int slot, int step)
    {
        List<ItemSO> cycle = ItemCycle();
        if (cycle.Count == 0) return;
        int current = cycle.FindIndex(i => i.ItemId == slotItems[slot]);
        int next = current < 0 ? (step > 0 ? 0 : cycle.Count - 1) : (current + step + cycle.Count) % cycle.Count;
        if (auth.DevGive(actor, cycle[next].ItemId, slot)) slotItems[slot] = cycle[next].ItemId;
    }

    // 무한 사용: 내가 준 아이템이 다 쓰여 칸이 비면 RefillDelay 뒤 다시 준다.
    private void RefillUsedUp(EscapeAuthority auth, int actor)
    {
        EscapeState s = EscapeManager.Instance.State;
        for (int slot = 0; slot < slotItems.Length; slot++)
        {
            if (string.IsNullOrEmpty(slotItems[slot])) continue;
            int held = s.HeldItemAt(actor, slot);
            if (held >= 0)
            {
                emptySince[slot] = 0f;
                if (s.Items[held].Id != slotItems[slot]) slotItems[slot] = s.Items[held].Id; // 주운 것으로 바뀌었으면 그것을 기억
                continue;
            }
            if (emptySince[slot] <= 0f) { emptySince[slot] = Time.time; continue; }
            if (Time.time - emptySince[slot] < RefillDelay) continue;
            emptySince[slot] = 0f;
            auth.DevGive(actor, slotItems[slot], slot);
        }
    }

    private List<ItemSO> ItemCycle()
    {
        if (itemCycle != null) return itemCycle;
        itemCycle = new List<ItemSO>();
        EscapeCatalogSO catalog = EscapeManager.Instance != null ? EscapeManager.Instance.Catalog : null;
        if (catalog == null) return itemCycle;
        foreach (ItemSO item in catalog.Items)
        {
            if (item == null) continue;
            // 괴물 스킬(사이렌 함정·수색 폭탄)은 손에 드는 아이템이 아니다
            if (item.IsTool && (item.Tool.Kind == ToolKind.NoiseBomb || item.Tool.Kind == ToolKind.SearchBomb)) continue;
            itemCycle.Add(item);
        }
        itemCycle.Sort((a, b) => b.IsTool.CompareTo(a.IsTool));
        return itemCycle;
    }

    private void OnGUI()
    {
        if (!itemTest || !showItemHelp || !TryItemContext(out _, out int actor)) return;
        if (helpStyle == null) helpStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true };
        int selected = PlayerInventory.Local != null ? PlayerInventory.Local.Selected : 0;
        ItemSO item = PlayerInventory.Local != null ? PlayerInventory.Local.ItemAt(selected) : null;
        string text = "<b>Item test</b>  slot " + (selected + 1) + ": " + (item != null ? item.ItemId : "(empty)")
            + "\n" + nextItemKey + "/" + previousItemKey + " next/prev item   " + refillKey + " refill   " + clearKey + " clear"
            + (infiniteItems ? "   (infinite)" : "");
        GUI.Box(new Rect(10f, 10f, 520f, 44f), text, helpStyle);
    }

    private void Start()
    {
        if (!autoCreateRoom) return;
        if (!PhotonNetwork.OfflineMode) return;

        PhotonNetwork.CreateRoom(testRoomName);
    }
}
