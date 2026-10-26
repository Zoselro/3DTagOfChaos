using Photon.Pun;
using UnityEngine;

// 스파이 로켓(EscapePlan.md §1.5, §5.6). 맵마다 모양이 다르고 재료 2개가 필요하다. 스파이만 보고 쓸 수 있다(쿠키·괴물에게는
// 아이콘이 뜨지 않는다). 재료나 공구상자를 들고 누르면 끼우고, 2개가 차면 빈손으로 눌러 탄다. 남은 스파이가 모두 타면 떠난다.
// 모델(EscapeVisualPlan.md §3.4): 칸마다 Slot_nn_Empty / Slot_nn_Filled 중 하나를 보여주고(이름이 Spin으로 시작하는 부품은 돈다),
// 스파이가 타면 Hatch가 열렸다 닫힌다. 출발 시각(SpyEscapedAt)부터 바닥 Flame이 서서히 커지고, 떨리다가 가속하며 떠오른다.
// 모든 화면이 공통 시계(PhotonNetwork.Time)로 같은 장면을 본다.
// 재료 넣기는 장치와 같이 E를 holdSeconds(2초) 누르고 있어야 하고, 완성 뒤 타기는 즉시(Request1009Plan.md §1).
public class SpyRocket : MonoBehaviour, IHoldInteractable, IInteractionLabel
{
    [SerializeField, Min(0f)] private float holdSeconds = 2f;
    [SerializeField, Min(0.5f)] private float interactionRange = 3f;

    private EscapeManager manager;
    private bool registered;
    private readonly GameObject[] slotParts = new GameObject[4];

    private const float IgniteSeconds = 2f;      // 불꽃이 다 커지기까지
    private const float RiseAcceleration = 5f;   // m/s² (4초 뒤 40 m)
    private const float HideAfterSeconds = 7f;   // 이만큼 지나면 하늘로 사라진 것으로 보고 숨긴다
    private const float HatchOpenSeconds = 1.4f;
    private const float HatchOpenAngle = 100f;

    private Transform flame;
    private Transform hatch;
    private Vector3 basePosition;
    private bool baseSaved;
    private int seenBoarded;
    private float hatchOpenUntil = -1f;
    private float hatchAngle;
    private ParticleSystem smoke;
    private Renderer[] renderers;
    private Collider[] colliders;
    private bool launchedVisible = true;
    private float lastLaunchTime = -1f;

    private void Awake()
    {
        flame = transform.Find("Flame");
        hatch = transform.Find("Hatch");
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        if (flame != null) flame.localScale = Vector3.zero;
        // 도는 부품(Spin…)을 한 번만 찾아 둔다 — 예전에는 매 프레임 이름 문자열을 만들어 찾았다(F-15).
        for (int i = 0; i < spinParts.Length; i++)
        {
            filledLooks[i] = transform.Find($"Slot_{i:00}_Filled");
            var spins = new System.Collections.Generic.List<Transform>();
            if (filledLooks[i] != null)
                foreach (Transform t in filledLooks[i]) if (t.name.StartsWith("Spin")) spins.Add(t);
            spinParts[i] = spins.ToArray();
        }
    }

    private readonly Transform[] filledLooks = new Transform[2];
    private readonly Transform[][] spinParts = new Transform[2][];

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => interactionRange;

    private void OnEnable() => SetRegistered(true);
    private void OnDisable() => SetRegistered(false);

    private void SetRegistered(bool value)
    {
        if (registered == value) return;
        registered = value;
        if (value) InteractableRegistry.Register(this);
        else InteractableRegistry.Unregister(this);
    }

    // 이번 판 로켓 자리(후보 중 무작위, Request1009Plan.md §25.5 J). 발사 전에만 옮긴다.
    public void PlaceAt(Transform spot)
    {
        if (spot == null || baseSaved) return;
        if ((transform.position - spot.position).sqrMagnitude < 0.01f) return;
        transform.SetPositionAndRotation(spot.position, Quaternion.Euler(0f, spot.eulerAngles.y, 0f));
        Physics.SyncTransforms();
    }

    private XRayMarker xray;
    private const float RocketXRayFill = 0.55f, RocketXRayHideWithin = 14f;

    public void Refresh(EscapeManager owner)
    {
        manager = owner;
        EscapeState s = owner.State;
        if (s == null) return;
        // 스파이는 자기 로켓 위치를 투시로 본다(§25.5 J). 멀리서도 로켓 모양으로 보이게 안쪽을 진하게 채운다(§42).
        if (xray == null) { xray = gameObject.AddComponent<XRayMarker>(); xray.Init(new Color(1f, 0.55f, 0.2f), RocketXRayFill); xray.HideWithin(RocketXRayHideWithin); }
        xray.SetVisible(RoomState.IsLocalSpy() && !RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out _));
        if (s.Boarded.Count > seenBoarded) hatchOpenUntil = Time.time + HatchOpenSeconds; // 누군가 탔다 — 해치 열림
        seenBoarded = s.Boarded.Count;
        for (int i = 0; i < s.RocketSlots.Count && i < slotParts.Length; i++)
        {
            bool filled = s.RocketSlots[i].Filled;
            Transform emptyLook = transform.Find($"Slot_{i:00}_Empty");
            Transform filledLook = transform.Find($"Slot_{i:00}_Filled");
            if (emptyLook != null || filledLook != null)
            {
                // 모델에 칸 모양이 있으면 빈/찬 모양을 바꿔 끼운다(공구상자로 채워도 같은 찬 모양 = 수리된 모습)
                if (emptyLook != null && emptyLook.gameObject.activeSelf == filled) emptyLook.gameObject.SetActive(!filled);
                if (filledLook != null && filledLook.gameObject.activeSelf != filled) filledLook.gameObject.SetActive(filled);
                continue;
            }
            Transform anchor = transform.Find($"Slot_{i:00}");
            if (anchor == null) continue;
            if (filled && slotParts[i] == null)
            {
                ItemSO def = s.RocketSlots[i].Accepts.Length > 0 ? owner.Catalog.Find(s.RocketSlots[i].Accepts[0]) : null;
                slotParts[i] = EscapeVisuals.CreateItemModel(def, anchor);
            }
            else if (!filled && slotParts[i] != null)
            {
                Destroy(slotParts[i]);
                slotParts[i] = null;
            }
        }
    }

    private void Update()
    {
        // 끼운 기어처럼 이름이 Spin으로 시작하는 부품은 돈다
        for (int i = 0; i < filledLooks.Length; i++)
        {
            Transform filledLook = filledLooks[i];
            if (filledLook == null || !filledLook.gameObject.activeInHierarchy) continue;
            foreach (Transform t in spinParts[i]) t.Rotate(180f * Time.deltaTime, 0f, 0f, Space.Self);
        }

        // 해치: 탈 때 잠깐 열린다
        if (hatch != null)
        {
            float target = Time.time < hatchOpenUntil ? HatchOpenAngle : 0f;
            hatchAngle = Mathf.MoveTowards(hatchAngle, target, 220f * Time.deltaTime);
            hatch.localRotation = Quaternion.Euler(0f, hatchAngle, 0f);
        }
        AnimateLaunch();
    }

    // 출발 시각부터: 불꽃이 서서히 커지며 떨림·연기(2초) → 가속하며 떠오름 → 하늘에서 사라짐.
    private void AnimateLaunch()
    {
        if (!RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out double escapedAt))
        {
            if (!launchedVisible) SetVisible(true);
            if (baseSaved) transform.position = basePosition;
            lastLaunchTime = -1f;
            return;
        }
        if (!baseSaved) { basePosition = transform.position; baseSaved = true; }
        float t = (float)(PhotonNetwork.Time - escapedAt);
        // 소리(SoundPlan.md S6): 점화 → 떠오름. 맵 연출과 같은 경계 규칙(늦게 들어온 사람은 지난 소리를 듣지 않음)
        if (EscapeCue.ShouldCue(0f, lastLaunchTime, t)) GameAudio.PlayOn(SoundId.RocketIgnite, transform);
        if (EscapeCue.ShouldCue(IgniteSeconds, lastLaunchTime, t)) GameAudio.PlayOn(SoundId.RocketLiftoff, transform);
        lastLaunchTime = t;
        if (t < 0f) return;

        foreach (Collider c in colliders) if (c != null && c.enabled) c.enabled = false; // 떠오르는 로켓에 걸리지 않게
        if (flame != null)
        {
            float grow = Mathf.Clamp01(t / IgniteSeconds);
            float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 40f);
            flame.localScale = new Vector3(grow, grow * flicker, grow);
        }
        if (smoke == null) smoke = CreateSmoke();

        float rise = Mathf.Max(0f, t - IgniteSeconds);
        Vector3 shake = t < IgniteSeconds + 0.5f ? Random.insideUnitSphere * 0.06f * Mathf.Clamp01(t) : Vector3.zero;
        shake.y = 0f;
        transform.position = basePosition + Vector3.up * (0.5f * RiseAcceleration * rise * rise) + shake;
        if (t > HideAfterSeconds && launchedVisible) SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        launchedVisible = visible;
        foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
        if (!visible && smoke != null) smoke.Stop();
    }

    // 점화 연기: 바닥에서 옆으로 퍼지는 회색 퍼프(코드로 만든 파티클, 텍스처 없음).
    private ParticleSystem CreateSmoke()
    {
        ParticleSystem ps = EscapeVisuals.CreateParticles("LaunchSmoke", transform, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(1.6f, 1.6f), Speed = new Vector2(3f, 3f), Size = new Vector2(1.2f, 2.4f),
            Color = new Color(0.75f, 0.72f, 0.8f, 0.6f), MaxParticles = 120, Rate = 40f,
            Shape = ParticleSystemShapeType.Cone, Angle = 70f, Radius = 0.6f, GrowTo = 1.6f,
            FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 0.6f,
        });
        ps.transform.localPosition = Vector3.up * 0.2f;
        ps.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ps.Play();
        return ps;
    }

    // 이 아이템을 로켓에 넣을 수 있는지: 빈 칸에 맞는 재료이거나, 빈 칸이 남은 동안의 수리키트(§26 H4 — 50%씩 채운다).
    public static bool RocketNeeds(EscapeState s, EscapeCatalogSO catalog, string itemId)
    {
        bool kit = catalog.RepairKit != null && itemId == catalog.RepairKit.ItemId;
        foreach (EscapeState.Slot slot in s.RocketSlots)
            if (!slot.Filled && (kit || slot.Allows(itemId))) return true;
        return false;
    }

    public bool CanInteract(IGameCharacter character)
    {
        if (!EscapeManager.ActionsAllowed) return false; // 변장 시간(§1.3)
        if (manager == null || manager.State == null || character.Role != CharacterRole.Cookie || !RoomState.IsLocalSpy()) return false;
        PlayerInventory inv = PlayerInventory.Local;
        if (inv == null) return false;
        if (inv.HeldItem != null && RocketNeeds(manager.State, manager.Catalog, inv.HeldItem.ItemId)) return true;
        return manager.State.RocketComplete && !inv.HandsLocked; // 두 손 아이템을 든 채로는 탈 수 없다(§1.5)
    }

    public float HoldSecondsFor(IGameCharacter character)
    {
        PlayerInventory inv = PlayerInventory.Local;
        return manager != null && manager.State != null && inv != null && inv.HeldItem != null && RocketNeeds(manager.State, manager.Catalog, inv.HeldItem.ItemId) ? holdSeconds : 0f;
    }

    public void Interact(IGameCharacter character)
    {
        PlayerInventory inv = PlayerInventory.Local;
        if (inv == null || manager == null) return;
        if (inv.HeldItem != null && RocketNeeds(manager.State, manager.Catalog, inv.HeldItem.ItemId))
            manager.Request(EscapeOp.RocketInsert, inv.Selected);
        else if (manager.State.RocketComplete)
            manager.Request(EscapeOp.RocketBoard);
    }

    // 로켓에 (n/2)처럼 진행 상황을 보여준다(스파이에게만 보이는 사물이라 그대로 띄운다).
    public string GetLabel(IGameCharacter viewer)
    {
        if (manager == null || manager.State == null) return null;
        int filled = 0;
        foreach (EscapeState.Slot s in manager.State.RocketSlots) if (s.Filled) filled++;
        return $"{EscapeTextsSO.Current.spyRocket} ({filled}/{manager.State.RocketSlots.Count})";
    }
}
