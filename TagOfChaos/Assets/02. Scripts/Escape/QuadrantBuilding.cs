using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 사분면 건물 하나(Request1009Plan.md §25.5 E). 테스트 맵의 Building_Q1~Q4(동서남북 문 4개, 가운데 Repair_Point·Socket·ESC_Glow)에 붙는다.
// - 쿠키: 그 건물 재료를 들고 E를 누르고 있으면 10초 게이지가 찬다. 떼면 게이지는 방장이 천천히 줄인다(초당 1%). 다시 누르면 이어서 찬다.
//   게이지는 방장이 들고 있다 — 누르기 시작(RepairBegin)·뗌(RepairStop)·다 참(RepairFinish)만 보내고, 화면은 상태의 값으로 그린다.
// - 스파이: 재료를 들고 있으면 어느 건물에서든 E가 뜨고, 맞지 않는 재료면 바로 "맞지 않는 재료"(§25 결정 2). 맞으면 쿠키와 같다.
//   해체 도구를 들고 가동된 건물을 20초 누르면 재료를 빼낸다(§25.3 Q8).
// - 필요한 재료(아이콘·꽂는 자리 색)는 쿠키에게만 보인다. 스파이·괴물에게는 어두운 받침과 "?"(§25 요청).
// - 건물 겉모습 바꾸기(§50 B안): 자식 RuneVariants 아래 Var_<재료 ItemId>·Var_Neutral이 있으면, 쿠키에게는 이번 판 재료의 건물을,
//   스파이·괴물(그리고 아직 재료가 정해지지 않았거나 쉬는 건물)에게는 Var_Neutral을 켠다. 충돌체·문·받침은 공통이라 바꾸지 않는다.
public class QuadrantBuilding : MonoBehaviour, IHoldInteractable, IHoldStartListener, IHoldEndListener, IHoldProgressDisplay, IInteractionLabel
{
    [SerializeField, Min(0.5f)] private float interactionRange = 3.2f;

    private const float GaugeWidth = 3f;
    private const float GaugeHeight = 0.3f;
    private const float GaugeLift = 3.4f;
    private static readonly Color SocketHidden = new Color(0.08f, 0.06f, 0.1f);

    private EscapeManager manager;
    private int index = -1;
    private bool registered;
    private Transform repairPoint;
    private Renderer socket;
    private GameObject glow;
    private GameObject placed;      // 넣은 재료 모양
    private string placedId;
    private GameObject hint;        // 쿠키에게만 보이는 "필요한 재료" 모양(받침 위에서 돈다)
    private string hintId;
    private Transform gauge, gaugeFill;
    private float localStartProgress = -1f; // 누르기 시작할 때의 게이지(방장이 받아 주기 전까지 링에 보인다)
    private bool dismantling;
    private float nextRetryTime;
    private int retries;
    private readonly Dictionary<string, GameObject> variants = new Dictionary<string, GameObject>();
    private GameObject neutralVariant;
    private string shownVariant = "?";

    // 수리는 뗄 때까지 누르고 있고, 다 찼는지는 방장 Tick이 정한다(§26 H2) — 내 화면 시간으로 끝내지 않는다.
    private const float RepairHoldSeconds = 3600f;
    private const float RetryInterval = 0.6f;
    private const int MaxRetries = 3;

    public int Index => index;
    public Vector3 RepairPoint => repairPoint != null ? repairPoint.position : transform.position;
    public Vector3 InteractionPoint => RepairPoint;
    public float InteractionRange => interactionRange;

    public void Bind(EscapeManager owner, int buildingIndex)
    {
        manager = owner;
        index = buildingIndex;
        repairPoint = FindPrefix("Repair_Point"); // 건물 2~4는 Blender가 이름 뒤에 .001 등을 붙인다
        Transform socketT = FindPrefix("Socket_");
        socket = socketT != null ? socketT.GetComponent<Renderer>() : null;
        Transform glowT = FindPrefix("ESC_Glow");
        glow = glowT != null ? glowT.gameObject : null;
        if (glow != null) glow.SetActive(false);
        CollectVariants();
        ShowVariant(null);
        BuildGauge();
        SetRegistered(true);
    }

    private void CollectVariants()
    {
        variants.Clear(); neutralVariant = null;
        Transform root = FindPrefix("RuneVariants");
        if (root == null) return;
        foreach (Transform t in root)
        {
            if (!t.name.StartsWith("Var_")) continue;
            string id = t.name.Substring(4);
            if (id == "Neutral") neutralVariant = t.gameObject;
            else variants[id] = t.gameObject;
        }
    }

    // materialId == null → 기본(무채색) 건물
    private void ShowVariant(string materialId)
    {
        if (variants.Count == 0 && neutralVariant == null) return;
        string want = materialId != null && variants.ContainsKey(materialId) ? materialId : "Neutral";
        if (want == shownVariant) return;
        shownVariant = want;
        foreach (KeyValuePair<string, GameObject> kv in variants) kv.Value.SetActive(kv.Key == want);
        if (neutralVariant != null) neutralVariant.SetActive(want == "Neutral");
    }

    private Transform FindPrefix(string prefix)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith(prefix)) return t;
        return null;
    }

    private void OnDisable() => SetRegistered(false);
    private void OnEnable() { if (manager != null) SetRegistered(true); }

    private void SetRegistered(bool value)
    {
        if (registered == value) return;
        registered = value;
        if (value) InteractableRegistry.Register(this);
        else InteractableRegistry.Unregister(this);
    }

    private bool TryGetState(out EscapeState.Building building)
    {
        building = default;
        EscapeState s = manager != null ? manager.State : null;
        if (s == null || index < 0 || index >= s.Buildings.Count) return false;
        building = s.Buildings[index];
        return true;
    }

    // 쿠키(스파이 제외)에게만 필요한 재료가 보인다.
    private static bool LocalSeesMaterials => !RoomState.IsLocalMonster() && !RoomState.IsLocalSpy();

    // ---------------- view ----------------

    public void Refresh()
    {
        if (!TryGetState(out EscapeState.Building b)) return;
        ItemSO material = manager.Catalog.Find(b.MaterialId);
        bool repaired = b.Active && b.ItemIndex >= 0;
        if (glow != null && glow.activeSelf != b.Repaired) glow.SetActive(b.Repaired);
        ShowVariant(LocalSeesMaterials && material != null && b.Active ? material.ItemId : null);

        // 받침 색: 쿠키에게는 재료 색(룬·캔디 꽂는 자리 색), 그 밖에는 어두운 받침
        if (socket != null)
        {
            Color c = LocalSeesMaterials && material != null && b.Active ? material.Tint : SocketHidden;
            EscapeVisuals.Tint(socket.gameObject, c, LocalSeesMaterials && b.Active ? 0.4f : 0f);
        }

        // 넣은 재료(모두에게 보인다 — 건물이 가동된 모습)
        string wantPlaced = repaired ? b.MaterialId : null;
        if (wantPlaced != placedId)
        {
            if (placed != null) Destroy(placed);
            placed = null;
            placedId = wantPlaced;
            if (wantPlaced != null && material != null)
            {
                placed = EscapeVisuals.CreateItemModel(material, transform);
                placed.transform.position = RepairPoint + Vector3.up * 0.2f;
            }
        }

        // 필요한 재료 힌트(쿠키에게만, 아직 가동 전)
        string wantHint = b.Active && !b.Repaired && LocalSeesMaterials ? b.MaterialId : null;
        if (wantHint != hintId)
        {
            if (hint != null) Destroy(hint);
            hint = null;
            hintId = wantHint;
            if (wantHint != null && material != null)
            {
                hint = EscapeVisuals.CreateItemModel(material, transform);
                hint.transform.position = RepairPoint + Vector3.up * 1.6f;
                hint.transform.localScale *= 0.6f;
            }
        }
    }

    private void BuildGauge()
    {
        if (gauge != null) return;
        gauge = new GameObject("RepairGauge").transform;
        gauge.SetParent(transform, false);
        var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(bg.GetComponent<Collider>());
        bg.transform.SetParent(gauge, false);
        bg.transform.localScale = new Vector3(GaugeWidth, GaugeHeight, 0.05f);
        EscapeVisuals.Tint(bg, new Color(0.1f, 0.1f, 0.1f), 0f);
        var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(fill.GetComponent<Collider>());
        fill.transform.SetParent(gauge, false);
        fill.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        EscapeVisuals.Tint(fill, new Color(0.35f, 1f, 0.45f), 0.8f);
        gaugeFill = fill.transform;
        gauge.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (gauge == null || !TryGetState(out EscapeState.Building b)) return;
        float p = b.ProgressAtTime(PhotonNetwork.Time);
        bool show = b.Active && !b.Repaired && p > 0.001f; // 수리 중인 게이지는 모두에게 보인다(괴물도 알 수 있다)
        if (gauge.gameObject.activeSelf != show) gauge.gameObject.SetActive(show);
        if (hint != null) hint.transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        if (!show) return;
        gauge.position = RepairPoint + Vector3.up * GaugeLift;
        Camera cam = Camera.main;
        if (cam != null) gauge.rotation = Quaternion.LookRotation(gauge.position - cam.transform.position);
        gaugeFill.localScale = new Vector3(GaugeWidth * p, GaugeHeight * 0.8f, 0.05f);
        gaugeFill.localPosition = new Vector3(-GaugeWidth * (1f - p) * 0.5f, 0f, -0.04f);
    }

    // ---------------- interaction ----------------

    private enum Mode { None, Repair, WrongMaterial, Dismantle, Busy }

    private Mode CurrentMode()
    {
        PlayerInventory inv = PlayerInventory.Local;
        if (inv == null || !TryGetState(out EscapeState.Building b) || !b.Active) return Mode.None;
        ItemSO held = inv.HeldItem;
        if (held == null) return Mode.None;
        bool spy = RoomState.IsLocalSpy();
        if (b.Repaired)
            return spy && held.IsTool && held.Tool.Kind == ToolKind.Dismantle && b.ItemIndex >= 0 && !manager.State.AnyExitOpen ? Mode.Dismantle : Mode.None;
        if (held.Category != ItemCategory.Material) return Mode.None;
        bool fits = held.ItemId == b.MaterialId;
        if (!fits) return spy ? Mode.WrongMaterial : Mode.None; // 쿠키에게는 맞는 건물에서만 E가 뜬다
        int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        return b.Repairer != 0 && b.Repairer != me ? Mode.Busy : Mode.Repair;
    }

    public bool CanInteract(IGameCharacter character)
    {
        if (!EscapeManager.ActionsAllowed || character.Role != CharacterRole.Cookie) return false;
        Mode mode = CurrentMode();
        return mode != Mode.None && mode != Mode.Busy;
    }

    public float HoldSecondsFor(IGameCharacter character)
    {
        switch (CurrentMode())
        {
            case Mode.Dismantle: return EscapeState.DismantleSeconds;
            case Mode.Repair:
                return RepairHoldSeconds;
            default: return 0f; // 맞지 않는 재료는 바로 안내
        }
    }

    public void OnHoldStarted(IGameCharacter character)
    {
        Mode mode = CurrentMode();
        dismantling = mode == Mode.Dismantle;
        if (mode == Mode.Repair)
        {
            TryGetState(out EscapeState.Building b);
            localStartProgress = b.ProgressAtTime(PhotonNetwork.Time);
            retries = 0;
            nextRetryTime = Time.time + RetryInterval;
            manager.Request(EscapeOp.RepairBegin, index, PlayerInventory.Local.Selected);
        }
        else if (dismantling) manager.Request(EscapeOp.DismantleBegin, index);
    }

    public void OnHoldEnded(IGameCharacter character, bool completed)
    {
        if (!completed && !dismantling && localStartProgress >= 0f) manager.Request(EscapeOp.RepairStop, index);
        localStartProgress = -1f;
        dismantling = false;
    }

    // 링에는 누른 시간이 아니라 방장이 들고 있는 건물 게이지를 보여 준다(이어서 찬다). 해체는 누른 시간 그대로.
    // 방장이 아직 나를 수리하는 사람으로 받지 않았으면(요청 유실·위치 검사 등) 0.6초마다 다시 보내고, 3번 실패하면 안내한다.
    public float DisplayProgress(float holdProgress)
    {
        if (dismantling || localStartProgress < 0f) return holdProgress;
        if (!TryGetState(out EscapeState.Building b)) return localStartProgress;
        int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        if (b.Repairer == me) return b.ProgressAtTime(PhotonNetwork.Time);
        if (Time.time >= nextRetryTime && retries <= MaxRetries)
        {
            nextRetryTime = Time.time + RetryInterval;
            if (retries++ < MaxRetries && PlayerInventory.Local != null) manager.Request(EscapeOp.RepairBegin, index, PlayerInventory.Local.Selected);
            else { EscapeHud.Toast(EscapeTextsSO.Current.repairRetry); UiSoundCues.Error(); }
        }
        return b.ProgressAtTime(PhotonNetwork.Time);
    }

    public void Interact(IGameCharacter character)
    {
        Mode mode = CurrentMode();
        if (mode == Mode.WrongMaterial) { EscapeHud.Toast(EscapeTextsSO.Current.wrongMaterial); UiSoundCues.Error(); return; }
        if (dismantling || mode == Mode.Dismantle) manager.Request(EscapeOp.Dismantle, index);
        else if (localStartProgress >= 0f) manager.Request(EscapeOp.RepairFinish, index);
        localStartProgress = -1f;
        dismantling = false;
    }

    public string GetLabel(IGameCharacter viewer)
    {
        if (!TryGetState(out EscapeState.Building b)) return null;
        EscapeTextsSO t = EscapeTextsSO.Current;
        if (!LocalSeesMaterials) return t.unknownMaterial;
        ItemSO material = manager.Catalog.Find(b.MaterialId);
        int percent = Mathf.RoundToInt(b.ProgressAtTime(PhotonNetwork.Time) * 100f);
        return material != null ? $"{material.DisplayName} {percent}%" : null;
    }
}
