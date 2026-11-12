using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// 맵 씬마다 하나(EscapeSetup 루트, EscapeMapSetup 에디터 도구가 배치). 탈출 상태의 사본을 들고 화면(상자·바닥 아이템·
// 건물·탈출구·로켓·무지개 상자·AI·폭탄·HUD)을 맞추고, 로컬 요청을 방장에게 보낸다. 방장이면 EscapeAuthority로 요청을 처리한다(EscapePlan.md §5.2).
// 개편(Request1009Plan.md §25): 사분면 건물 4곳 + 동서남북 탈출구 4개 + 후보 자리(로켓·무지개 상자) — 테스트 맵 이름 규칙(§25.5 O)을 쓴다.
public class EscapeManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static EscapeManager Instance { get; private set; }

    [SerializeField] private Transform[] chestAnchors = new Transform[0];
    [SerializeField] private QuadrantBuilding[] buildings = new QuadrantBuilding[0];
    [SerializeField] private EscapeGate[] exits = new EscapeGate[0];
    [SerializeField] private SpyRocket rocket;
    [SerializeField] private Transform[] rocketAnchors = new Transform[0];
    [SerializeField] private Transform[] rainbowAnchors = new Transform[0];
    [SerializeField] private RainbowChest rainbowChest;
    [SerializeField] private Transform monsterRoom;
    [SerializeField] private float monsterRoomHalfSize = 13f;
    // 0이면 Ground 사각형이 맵 경계. 0보다 크면 맵 가운데를 중심으로 한 이 반지름의 원(캔디숲 실제 맵, Request1009Plan.md §28.6).
    [SerializeField, Min(0f)] private float mapRadius;
    [SerializeField] private WitchPresenter witch;
    [Tooltip("비우면 씬 이름(<Map>·Test_<Map>)으로 카탈로그에서 찾는다.")]
    [SerializeField] private EscapeRecipeSO recipeOverride;

    private EscapeCatalogSO catalog;
    private EscapeRecipeSO recipe;
    private EscapeAuthority authority;
    private MaterialChest[] chests = new MaterialChest[0];
    private readonly List<GroundItemView> groundPool = new List<GroundItemView>();
    private readonly HashSet<int> brokenReported = new HashSet<int>();
    private bool initRequested;
    private MapArea area;
    private Transform firstEscapeFocus;
    private MonsterRoomGuard roomGuard;

    public EscapeState State { get; private set; }
    public EscapeCatalogSO Catalog => catalog;
    public float MapRadius => mapRadius; // 원 맵의 반지름(0 = Ground 사각형) — 마녀 위치도 이것으로(§69 E1)
    public EscapeRecipeSO Recipe => recipe;
    public SpyRocket Rocket => rocket;
    public WitchPresenter Witch => witch;
    public MapArea Area => area;
    public event System.Action StateChanged;
    public DecoyRouteBook Routes { get; private set; } // AI 경로 "DR_<id>"(Request1009Plan.md §44)

    // "탈출했나"의 단일 기준은 방장이 쓴 EscapeState다(AuditFixPlan.md F-07·S-05). 다른 도메인은 이 두 함수만 부른다.
    public static bool HasLeftMap(int actor) => Instance != null && Instance.State != null && Instance.State.HasLeftMap(actor);
    public static bool HasEscaped(int actor) => Instance != null && Instance.State != null && Instance.State.HasEscaped(actor);

    // 호박 마차(§68.7): 탄 사람은 E를 누른 순간부터 도착할 때까지 이동이 잠기고, 문이 닫힌 뒤에는 잡히지 않는다.
    public static bool IsRidingCarriage(int actor) => CarriageTimeCheck(actor, PumpkinCarriageRules.Riding);
    public static bool IsCarriageProtected(int actor) => CarriageTimeCheck(actor, PumpkinCarriageRules.Protected);

    // 개발용(OfflineModeBootstrap 아이템 시험): 오프라인 방의 방장일 때만 방장 판정 객체를 내준다. 온라인에서는 늘 null.
    public EscapeAuthority OfflineAuthority => Photon.Pun.PhotonNetwork.OfflineMode && Photon.Pun.PhotonNetwork.IsMasterClient ? authority : null;

    // 콜라 대포(§68.9): 탄 순간부터 착지까지 이동 잠금, 해치가 닫힌 뒤 착지까지 잡히지 않는다.
    public static bool IsRidingCannon(int actor) => CannonTimeCheck(actor, ColaCannonRules.Riding);
    public static bool IsCannonProtected(int actor) => CannonTimeCheck(actor, ColaCannonRules.Protected);
    // 탈것(마차·대포) 공통 — 이동 잠금·잡기 보호가 묻는다.
    public static bool IsRidingVehicle(int actor) => IsRidingCarriage(actor) || IsRidingCannon(actor);
    public static bool IsVehicleProtected(int actor) => IsCarriageProtected(actor) || IsCannonProtected(actor);

    private static bool CannonTimeCheck(int actor, System.Func<double, float, double, bool> rule)
    {
        if (Instance == null || Instance.State == null) return false;
        int i = Instance.State.RiddenCannonOf(actor);
        if (i < 0) return false;
        EscapeState.Cannon c = Instance.State.Cannons[i];
        return rule(c.BoardAt, c.Flight, Photon.Pun.PhotonNetwork.Time);
    }

    private static bool CarriageTimeCheck(int actor, System.Func<double, double, bool> rule)
    {
        if (Instance == null || Instance.State == null) return false;
        int i = Instance.State.RiddenCarriageOf(actor);
        return i >= 0 && rule(Instance.State.Carriages[i].BoardAt, Photon.Pun.PhotonNetwork.Time);
    }

    // 쿠키가 모두 끝나 타임어택 없이 스파이 로켓만 떠나는 경우(D10): 발사 연출이 보이도록 잠시 끝 판정을 미룬다.
    public const float RocketLaunchShowSeconds = 6f;
    public bool IsDeparting(double now) =>
        RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out double at) && !RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out _)
        && now < at + RocketLaunchShowSeconds;

    public static bool IsActive => Instance != null && Instance.isActiveAndEnabled;

    // 색칠(변장) 단계가 없어져(§25.5 C) 판이 시작되면 바로 상자·도구·수리를 쓸 수 있다. 결과가 나온 뒤에는 막는다.
    public static bool ActionsAllowed => GamePhaseState.Current != GamePhase.Result;

    // 상자는 준비 시간이 끝나(술래문이 열려) 사냥이 시작되면 보인다(§26 H3 — 준비 시간에는 숨기고 열 수 없다).
    public static bool ChestsVisibleIn(GamePhase phase) => phase != GamePhase.Lobby && phase != GamePhase.Prepare;
    private const float PhaseCheckSeconds = 0.2f;
    private bool? chestsShown;
    private float nextPhaseCheck;
    private const float TrackSeconds = 0.2f; // 위치 기록 간격(F-10)
    private float nextTrack;

    // ---------------- scene geometry ----------------

    public int ExitCount => exits.Length;
    public int BuildingCount => buildings.Length;
    public int RocketAnchorCount => rocketAnchors.Length;
    public int RainbowAnchorCount => rainbowAnchors.Length;

    public Vector3 ChestPosition(int anchor) => anchor >= 0 && anchor < chestAnchors.Length ? chestAnchors[anchor].position : transform.position;
    public Vector3 ChestFront(int anchor) =>
        anchor >= 0 && anchor < chestAnchors.Length ? chestAnchors[anchor].position + chestAnchors[anchor].forward * 1.2f : transform.position;
    public Transform BuildingTransform(int b) => b >= 0 && b < buildings.Length && buildings[b] != null ? buildings[b].transform : null;
    // 재료 건물 둘레인지(사이렌 함정 설치 자리, §69 F5): 수리 지점에서 수평 radius 안이고 같은 층(높이 차 FloorGap 안).
    public const float FloorGap = 8f;
    public bool IsNearMaterialBuilding(Vector3 pos, float radius) => NearestMaterialBuilding(pos, out _) <= radius;

    public float NearestMaterialBuilding(Vector3 pos, out Vector3 repairPoint)
    {
        float best = float.MaxValue;
        repairPoint = pos;
        for (int b = 0; b < buildings.Length; b++)
        {
            if (buildings[b] == null) continue;
            Vector3 p = buildings[b].RepairPoint;
            if (Mathf.Abs(p.y - pos.y) > FloorGap) continue;
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pos.x, pos.z));
            if (d < best) { best = d; repairPoint = p; }
        }
        return best;
    }

    public Vector3 BuildingRepairPoint(int b) => b >= 0 && b < buildings.Length && buildings[b] != null ? buildings[b].RepairPoint : transform.position;
    public Vector3 ExitPosition(int k) => k >= 0 && k < exits.Length && exits[k] != null ? exits[k].TriggerPoint : transform.position;
    public Vector3 RainbowPosition(int anchor) => anchor >= 0 && anchor < rainbowAnchors.Length ? rainbowAnchors[anchor].position : transform.position;

    // 이번 판 로켓 자리(상태가 정한 후보). 정하기 전이면 로켓이 놓인 곳.
    public Vector3 RocketPosition
    {
        get
        {
            int a = State != null ? State.RocketAnchor : -1;
            if (a >= 0 && a < rocketAnchors.Length) return rocketAnchors[a].position;
            return rocket != null ? rocket.transform.position : transform.position;
        }
    }

    // 마녀 연출 앞 샷 대상(첫 탈출 지점, §25.5 G). 값이 없으면 null.
    public Transform FirstEscapeFocus
    {
        get
        {
            if (!RoomState.IsInRoom() || !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.FirstEscapePoint, out object raw) || !(raw is Vector3 p))
                return null;
            if (firstEscapeFocus == null) firstEscapeFocus = new GameObject("FirstEscapeFocus").transform;
            firstEscapeFocus.SetParent(transform, false);
            firstEscapeFocus.position = p;
            return firstEscapeFocus;
        }
    }

    // ---------------- lifecycle ----------------

    private void Awake()
    {
        Instance = this;
        catalog = EscapeCatalogSO.Current;
        string map = SceneNames.MapOf(SceneManager.GetActiveScene().name);
        recipe = recipeOverride != null ? recipeOverride : catalog != null ? catalog.RecipeFor(map) : null;
        if (catalog == null || recipe == null)
        {
            Debug.LogError($"[EscapeManager] Catalog or recipe for '{map}' not found. Escape mode disabled.");
            enabled = false;
            return;
        }

        if (mapRadius > 0f) area = new MapArea(Vector2.zero, mapRadius);
        else area = new MapArea(WitchPath.TryGetGroundRect(gameObject.scene, out Rect ground) ? ground : new Rect(-70f, -70f, 140f, 140f));
        chests = new MaterialChest[chestAnchors.Length];
        for (int i = 0; i < chestAnchors.Length; i++)
        {
            if (chestAnchors[i] == null) continue;
            chests[i] = chestAnchors[i].GetComponent<MaterialChest>();
            if (chests[i] == null) chests[i] = chestAnchors[i].gameObject.AddComponent<MaterialChest>();
            chests[i].Bind(this, i);
        }
        for (int i = 0; i < buildings.Length; i++) if (buildings[i] != null) buildings[i].Bind(this, i);
        for (int i = 0; i < exits.Length; i++) if (exits[i] != null) exits[i].Bind(this, i);
        if (rainbowChest != null) rainbowChest.Bind(this);
        authority = new EscapeAuthority(this, catalog, recipe);
        Routes = new DecoyRouteBook(p => !IsOutsideMap(p, 2f) && !InMonsterRoom(p));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        StallWatch.Mark("EscapeManager.Start EscapeHud.Create");
        EscapeHud.Create(this);
        StallWatch.Mark("EscapeManager.Start EscapeAudioPresenter");
        gameObject.AddComponent<EscapeAudioPresenter>().Init(this); // 탈출 모드 소리(상태 차이, SoundPlan.md S5)
        StallWatch.Mark("EscapeManager.Start DecoyBrain");
        gameObject.AddComponent<DecoyBrain>().Init(this);           // AI 판단 — 방장만 돈다(§44)
        StallWatch.Mark("EscapeManager.Start DecoyPresenter");
        gameObject.AddComponent<DecoyPresenter>().Init(this);       // 맵의 AI(§25.5 L)
        StallWatch.Mark("EscapeManager.Start ProjectileSystem.Create");
        ProjectileSystem.Create(this); // 던진 물체(물풍선·섬광탄·수색 폭탄, §70.3)
        StallWatch.Mark("EscapeManager.Start NoiseBombPresenter");
        gameObject.AddComponent<NoiseBombPresenter>().Init(this);   // 괴물 사이렌 함정(옛 소음폭탄 §25.5 K)·수색 폭탄 입력(§68 GP5)
        StallWatch.Mark("EscapeManager.Start SirenTrapPresenter");
        gameObject.AddComponent<SirenTrapPresenter>().Init(this);   // 사이렌 철창(§68 GP5)
        StallWatch.Mark("EscapeManager.Start PumpkinCarriagePresenter");
        gameObject.AddComponent<PumpkinCarriagePresenter>().Init(this); // 호박 마차 탈것(§68.7)
        StallWatch.Mark("EscapeManager.Start ColaCannonPresenter");
        gameObject.AddComponent<ColaCannonPresenter>().Init(this);      // 콜라 대포 탈것(§68.9)
        StallWatch.Mark("EscapeManager.Start DestructionNet");
        gameObject.AddComponent<DestructionNet>();                  // 거대 괴물이 부수는 물체 상태(§66)
        StallWatch.Mark("EscapeManager.Start DestructionPresenter");
        gameObject.AddComponent<DestructionPresenter>();            // 부서지는 연출(데칼·붕괴, §66)
        StallWatch.Mark("EscapeManager.Start GiantBrain");
        gameObject.AddComponent<GiantBrain>().Init(this);           // 거대 괴물 판단 — 방장만 돈다(§64)
        StallWatch.Mark("EscapeManager.Start GiantPresenter");
        gameObject.AddComponent<GiantPresenter>().Init(this);       // 거대 괴물 연출(§64)
        StallWatch.Mark("EscapeManager.Start VoiceNet.EnsureSent");
        VoiceNet.EnsureSent();                                      // 대기실에서 못 받은 사람에게만 목소리를 보낸다(§25.3 Q6, §72 E2)
        StallWatch.Mark("EscapeManager.Start ReadStateFromRoom");
        ReadStateFromRoom();
        StallWatch.Mark("EscapeManager.Start done");
    }

    private void Update()
    {
        if (!RoomState.IsInRoom()) return;
        if (State != null && Time.unscaledTime >= nextPhaseCheck)
        {
            nextPhaseCheck = Time.unscaledTime + PhaseCheckSeconds;
            if (ChestsVisibleIn(GamePhaseState.Current) != chestsShown) RefreshViews();
        }
        if (Time.unscaledTime >= nextTrack)
        {
            nextTrack = Time.unscaledTime + TrackSeconds;
            authority.TrackPositions(); // 방장이 아니어도 기록한다(F-10)
        }
        TickOutbox();
        if (!PhotonNetwork.IsMasterClient)
        {
            TrackBrokenPlayers(asMaster: false); // 방장이 아니어도 잡힌 사람을 기억해 둔다 — 방장이 되면 다시 알리지 않게(research.md R4.10-9)
            return;
        }
        if (adoptAt >= 0f)
        {
            if (Time.unscaledTime < adoptAt) return; // 이어받기 유예 중(§72 E4)
            FinishAdopt();
        }

        if (State == null && !initRequested && CanInitialize())
        {
            initRequested = true;
            authority.Initialize(chestAnchors.Length);
        }
        TrackBrokenPlayers(asMaster: true);
        authority.Tick();
    }

    // 판 시작 신호(PrepareEndTime)가 있고 괴물이 정해졌으면 시작한다. 오프라인 개발 방은 바로 시작한다.
    private static bool CanInitialize() =>
        PhotonNetwork.OfflineMode || (RoomState.TryGetDouble(NetKeys.PrepareEndTime, out _) && RoomState.HasMonster());

    private void TrackBrokenPlayers(bool asMaster)
    {
        foreach (Player p in RoomState.Players)
        {
            if (!RoomState.IsBroken(p) || !brokenReported.Add(p.ActorNumber)) continue;
            if (asMaster) authority.OnPlayerBroken(p);
        }
    }

    // 마녀 타이머 시작(방장 — 안전 상한, §25.3 Q4).
    public void StartWitchTimer(Vector3 point)
    {
        if (PhotonNetwork.IsMasterClient && authority != null) authority.StartWitchTimer(point);
    }

    // ---------------- state sync ----------------

    private void ReadStateFromRoom()
    {
        if (!RoomState.IsInRoom()) return;
        Routes.Read(PhotonNetwork.CurrentRoom.CustomProperties, full: true);
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.EscapeState, out object raw) && raw is byte[] data)
            ApplyLocalState(EscapeState.Decode(data));
    }

    public override void OnJoinedRoom() => ReadStateFromRoom();

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        // AI 경로: 방장은 자기가 쓴 값(DecoyBrain)이 원본이라 늦게 돌아온 옛 값으로 되돌아가지 않게 읽지 않는다(조작은 RoomPropertyGuard가 되돌린다).
        if (!PhotonNetwork.IsMasterClient) Routes.Read(changed, full: false);
        if (changed.ContainsKey(NetKeys.EscapeState))
        {
            if (changed[NetKeys.EscapeState] is byte[] data) ApplyLocalState(EscapeState.Decode(data));
            else { State = null; initRequested = false; StateChanged?.Invoke(); } // 판 초기화로 지워짐
        }
    }

    // 새 방장이 이어받는다. §72 E4: 바로 이어받으면 이전 방장이 나가기 직전에 쓴 상태가 아직 도착하지 않아 옛 상태로 덮어쓸 수 있었다 —
    // AdoptDelay초 동안 요청을 모아 두고(Tick도 쉼) 그사이 도착한 최신 상태로 이어받는다.
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        seenSeq.Clear();
        if (!PhotonNetwork.IsMasterClient)
        {
            adoptAt = -1f;
            deferred.Clear();
            return;
        }
        adoptAt = Time.unscaledTime + AdoptDelay;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (PhotonNetwork.IsMasterClient) authority.OnPlayerLeft(otherPlayer);
    }

    public void ApplyLocalState(EscapeState state)
    {
        if (state == null) return;
        // 방장 화면: 커밋 직후 바로 맞춘 상태보다 옛 상태(앞선 커밋의 서버 응답)가 늦게 오면 무시한다(F-18).
        if (PhotonNetwork.IsMasterClient && State != null && state.Revision < State.Revision) return;
        State = state;
        if (PhotonNetwork.IsMasterClient && authority.State == null) authority.Adopt(state);
        RefreshViews();
        StateChanged?.Invoke();
    }

    private void RefreshViews()
    {
        var used = new bool[chests.Length];
        bool show = ChestsVisibleIn(GamePhaseState.Current);
        bool appear = show && chestsShown == false;
        chestsShown = show;
        for (int c = 0; show && c < State.Chests.Count; c++)
        {
            int anchor = State.Chests[c].Anchor;
            if (anchor < 0 || anchor >= chests.Length || chests[anchor] == null) continue;
            used[anchor] = true;
            chests[anchor].Show(c, State.Chests[c], appear);
        }
        for (int a = 0; a < chests.Length; a++)
            if (!used[a] && chests[a] != null) chests[a].Hide();

        int g = 0;
        for (int i = 0; i < State.Items.Count; i++)
        {
            if (State.Items[i].Loc != ItemLocation.Ground) continue;
            ItemSO def = catalog.Find(State.Items[i].Id);
            if (def == null) continue;
            if (g >= groundPool.Count) groundPool.Add(GroundItemView.Create(this, transform));
            groundPool[g++].Show(i, def, State.Items[i].Pos);
        }
        for (; g < groundPool.Count; g++) groundPool[g].Hide();

        foreach (QuadrantBuilding b in buildings) if (b != null) b.Refresh();
        foreach (EscapeGate e in exits) if (e != null) e.Refresh();
        if (rocket != null)
        {
            if (State.RocketAnchor >= 0 && State.RocketAnchor < rocketAnchors.Length && !RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out _))
                rocket.PlaceAt(rocketAnchors[State.RocketAnchor]);
            rocket.Refresh(this);
        }
        if (rainbowChest != null) rainbowChest.Refresh();
    }

    // ---------------- requests ----------------

    public void Request(EscapeOp op, int a = 0, int b = 0, Vector3 pos = default, int[] targets = null)
    {
        if (!RoomState.IsInRoom() || PhotonNetwork.LocalPlayer == null) return;
        var r = new EscapeRequest { Op = op, Sender = PhotonNetwork.LocalPlayer.ActorNumber, A = a, B = b, Pos = pos, Targets = targets };
        Send(r);
    }

    // 던지기(§70.3): 손을 떠난 순간의 시작점·속도·시각과 던진 사람이 붙인 번호.
    public void RequestThrow(int slot, int color, Vector3 origin, Vector3 velocity, int stamp, int localKey)
    {
        if (!RoomState.IsInRoom() || PhotonNetwork.LocalPlayer == null) return;
        var r = new EscapeRequest { Op = EscapeOp.Throw, Sender = PhotonNetwork.LocalPlayer.ActorNumber, A = slot, B = color, Pos = origin,
                                    Targets = new[] { localKey }, Dir = velocity, Stamp = stamp };
        Send(r);
    }

    // ---------------- §72 E3·E4: 요청 번호·확인·재전송, 방장 교체 유예 ----------------

    public const float RequestRetrySeconds = 1f; // 이 안에 방장의 확인(RequestAck)이 없으면 다시 보낸다
    public const int RequestMaxTries = 3;
    public const float AdoptDelay = 0.5f;        // 새 방장: 이전 방장이 마지막으로 쓴 상태가 도착할 때까지 요청 처리를 미룬다

    private sealed class Outgoing
    {
        public EscapeRequest R;
        public float SentAt;
        public int Tries;
        public int Master;
    }

    private readonly Dictionary<int, Outgoing> outbox = new Dictionary<int, Outgoing>();
    private readonly List<int> outboxDrop = new List<int>();
    private readonly Dictionary<int, RequestSeqWindow> seenSeq = new Dictionary<int, RequestSeqWindow>();
    private readonly List<EscapeRequest> deferred = new List<EscapeRequest>();
    private int nextSeq = 1;
    private float adoptAt = -1f;

    public int PendingRequestCount => outbox.Count; // 시험·로그용
    public bool AdoptPending => adoptAt >= 0f;

    // 방장이면 바로(유예 중이면 미뤄) 처리하고, 아니면 번호를 붙여 보내고 확인을 기다린다.
    private void Send(EscapeRequest r)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            HandleAsMaster(r);
            return;
        }
        r.Seq = nextSeq++;
        outbox[r.Seq] = new Outgoing { R = r, SentAt = Time.unscaledTime, Tries = 1, Master = MasterActor() };
        Raise(r);
    }

    private static void Raise(EscapeRequest r) =>
        PhotonNetwork.RaiseEvent(NetEventCodes.EscapeRequest, r.ToPayload(),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);

    private static int MasterActor() => PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.MasterClientId : -1;

    private void HandleAsMaster(EscapeRequest r)
    {
        if (adoptAt >= 0f) { deferred.Add(r); return; }
        authority.Handle(r);
    }

    // 방장: 받은 요청을 확인해 주고(번호가 있으면), 처음 받은 번호만 처리한다.
    private void ReceiveRequest(EscapeRequest r)
    {
        if (r.Seq > 0)
        {
            EscapeAuthority.NoticeTo(r.Sender, EscapeNoticeKind.RequestAck, r.Seq.ToString());
            if (!seenSeq.TryGetValue(r.Sender, out RequestSeqWindow window)) seenSeq[r.Sender] = window = new RequestSeqWindow();
            if (!window.Add(r.Seq)) return; // 다시 보낸 요청 — 이미 처리했다
        }
        HandleAsMaster(r);
    }

    // 보낸 요청: 확인이 없으면 다시 보내고, 방장이 바뀌었으면 새 방장에게(두 번 처리돼도 되는 요청만), 내가 방장이 됐으면 직접 처리한다.
    private void TickOutbox()
    {
        if (outbox.Count == 0) return;
        float now = Time.unscaledTime;
        int master = MasterActor();
        bool iAmMaster = PhotonNetwork.IsMasterClient;
        outboxDrop.Clear();
        foreach (KeyValuePair<int, Outgoing> kv in outbox)
        {
            Outgoing o = kv.Value;
            bool masterChanged = o.Master != master;
            if (masterChanged && !EscapeRequest.IsResendSafe(o.R.Op))
            {
                outboxDrop.Add(kv.Key);
                Debug.Log($"[EscapeManager] {o.R.Op} request #{kv.Key} dropped: the master changed before it was confirmed.");
                if (EscapeRequest.NotifyOnFailure(o.R.Op)) ScreenToast.Show(EscapeTextsSO.Current.requestFailed, 2f);
                continue;
            }
            if (iAmMaster)
            {
                outboxDrop.Add(kv.Key);
                HandleAsMaster(o.R);
                continue;
            }
            if (!masterChanged && now - o.SentAt < RequestRetrySeconds) continue;
            if (o.Tries >= RequestMaxTries)
            {
                outboxDrop.Add(kv.Key);
                Debug.LogWarning($"[EscapeManager] {o.R.Op} request #{kv.Key} got no answer from the master after {o.Tries} tries.");
                if (EscapeRequest.NotifyOnFailure(o.R.Op)) ScreenToast.Show(EscapeTextsSO.Current.requestFailed, 2f);
                continue;
            }
            o.Tries++;
            o.SentAt = now;
            o.Master = master;
            Raise(o.R);
            Debug.Log($"[EscapeManager] {o.R.Op} request #{kv.Key} resent (try {o.Tries}{(masterChanged ? ", new master" : string.Empty)}).");
        }
        for (int i = 0; i < outboxDrop.Count; i++) outbox.Remove(outboxDrop[i]);
    }

    // 유예가 끝났다: 방 캐시의 최신 상태(늦게 도착한 이전 방장의 마지막 커밋 포함)를 이어받고, 미뤄 둔 요청을 처리한다.
    private void FinishAdopt()
    {
        adoptAt = -1f;
        ReadStateFromRoom();
        if (State != null)
        {
            authority.Adopt(State);
            authority.Reevaluate();
        }
        Debug.Log($"[EscapeManager] Took over as master (revision {(State != null ? State.Revision : -1)}, {deferred.Count} deferred request(s)).");
        for (int i = 0; i < deferred.Count; i++) authority.Handle(deferred[i]);
        deferred.Clear();
    }

    public void OnEvent(EventData e)
    {
        switch (e.Code)
        {
            case NetEventCodes.EscapeRequest:
                if (PhotonNetwork.IsMasterClient && EscapeRequest.TryParse(e.CustomData, e.Sender, out EscapeRequest r)) ReceiveRequest(r);
                break;
            case NetEventCodes.EscapeNotice:
                if (!SentByMaster(e)) return;
                if (!(e.CustomData is object[] n) || n.Length < 2 || !(n[0] is byte kind)) return;
                if ((EscapeNoticeKind)kind == EscapeNoticeKind.StolenFromDevice)
                {
                    if (n.Length >= 4 && n[2] is int itemIndex && n[3] is double showAt) EscapeHud.ScheduleStolen(n[1] as string, itemIndex, showAt);
                }
                else if ((EscapeNoticeKind)kind == EscapeNoticeKind.Respawned)
                {
                    if (n.Length >= 5 && n[2] is int itemIndex && n[3] is double showAt && n[4] is bool onGround)
                        EscapeHud.ScheduleRespawned(n[1] as string, itemIndex, showAt, onGround);
                }
                else if ((EscapeNoticeKind)kind == EscapeNoticeKind.ThrowRejected)
                {
                    // 내 던지기를 방장이 받지 않았다(§71 T1): "던진 사람 번호|이유"
                    string[] parts = (n[1] as string ?? string.Empty).Split('|');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int key) && byte.TryParse(parts[1], out byte why) && ProjectileSystem.Instance != null)
                        ProjectileSystem.Instance.OnThrowRejected(key, (ThrowReject)why);
                }
                else if ((EscapeNoticeKind)kind == EscapeNoticeKind.RequestAck)
                {
                    if (int.TryParse(n[1] as string, out int seq)) outbox.Remove(seq); // 방장이 받았다(§72 E3)
                }
                else if ((EscapeNoticeKind)kind == EscapeNoticeKind.DecoyCatchRejected)
                {
                    // 내 AI 잡기가 거절됐다: 빈손 잡기 동작을 끊고 바로 다시 잡을 수 있게 한다(§70.2.5 D5)
                    if (CharacterRegistry.FindByOwner(PhotonNetwork.LocalPlayer.ActorNumber) is MonsterController me) me.CancelGrabKill();
                }
                else EscapeHud.Notice((EscapeNoticeKind)kind, n[1] as string);
                break;
            case NetEventCodes.ProjectileSpawn:
                if (!SentByMaster(e)) return;
                if (ProjectileSystem.Instance != null) ProjectileSystem.Instance.OnSpawn(e.CustomData);
                if (e.CustomData is object[] sp && sp.Length > 2 && sp[1] is string spItem && sp[2] is int spOwner)
                    authority.NoteApprovedUse(spOwner, catalog.Find(spItem)); // 쿨타임을 따라 적는다(§72 E5)
                break;
            case NetEventCodes.ToolHit:
                if (!SentByMaster(e)) return;
                ToolHitReceiver.Handle(e.CustomData);
                if (e.CustomData is object[] th && th.Length > 1 && th[0] is string thItem && th[1] is int thActor)
                {
                    ItemSO used = catalog.Find(thItem);
                    if (used != null && used.IsTool && used.Tool.Projectile == null) authority.NoteApprovedUse(thActor, used); // 투사체 명중은 던질 때 이미 적었다
                }
                break;
            // VoiceClip은 VoiceNet이 씬과 무관하게 직접 받는다(§72 E2).
        }
    }

    // 알림·도구 명중은 방장만 보낸다 — 다른 클라이언트가 직접 보낸 것은 무시한다(research.md R4.10-8).
    private static bool SentByMaster(EventData e)
    {
        if (PhotonNetwork.OfflineMode) return true;
        Room room = PhotonNetwork.CurrentRoom;
        if (room != null && e.Sender == room.MasterClientId) return true;
        Debug.LogWarning($"[EscapeManager] Ignored event {e.Code} from actor {e.Sender} (not the master client).");
        return false;
    }

    // ---------------- helpers ----------------

    public ItemSO ItemDef(int index) =>
        State != null && index >= 0 && index < State.Items.Count ? catalog.Find(State.Items[index].Id) : null;

    // 술래방 안인지(수평 + 방 높이). 호박마차·AI는 술래방에 들어가지 않는다.
    // 높이도 본다(Request1009Plan.md §57): 두 층 맵(진저브레드 지하 −36 m)에서 술래방 바로 밑 지하가 방 안으로 판정됐다.
    public bool InMonsterRoom(Vector3 p)
    {
        Vector3 c = monsterRoom != null ? monsterRoom.position : Vector3.zero;
        return MonsterRoomGuard.WithinRoomHeight(c.y, p.y)
            && Mathf.Abs(p.x - c.x) < monsterRoomHalfSize && Mathf.Abs(p.z - c.z) < monsterRoomHalfSize;
    }

    // 술래방 안에서 문 바깥으로 내보내진 이동인지(MonsterRoomGuard, §28.15 E1) — 방장 이동 속도 검사 예외.
    public bool IsRoomEjectMove(Vector3 from, Vector3 to)
    {
        if (roomGuard == null && monsterRoom != null) roomGuard = monsterRoom.GetComponent<MonsterRoomGuard>();
        return roomGuard != null && roomGuard.IsEjectMove(from, to);
    }

    // 맵 경계(Ground 사각형 또는 원) 밖으로 margin m 넘게 나갔는지 — 헬리콥터 모자 탈출(§25.5 H).
    public bool IsOutsideMap(Vector3 p, float margin) => area.IsOutside(p, margin);

    private const float CapsuleRadius = 0.45f, CapsuleHeight = 1.9f, MapInset = 4f;

    // 맵 안 무작위 지점(호박마차·AI 배치, §25.3 Q7): 바닥이 있고, 술래방 밖이고, 캐릭터가 서도 아무것에도 끼지 않는 곳. 못 찾으면 false.
    public bool TryRandomMapPoint(System.Random random, out Vector3 point)
    {
        for (int i = 0; i < 30; i++)
        {
            Vector2 xz = area.RandomPoint(random, MapInset);
            point = SnapToGround(new Vector3(xz.x, 30f, xz.y));
            if (point.y > 20f || InMonsterRoom(point)) continue;
            if (IsClearForCharacter(point) && IsWalkableFromSpawn(point)) return true;
        }
        point = default;
        return false;
    }

    // 쿠키가 걸어서 갈 수 있는 바닥인지(2026-10-31 — 문 기둥 위 들보 같은 올라갈 수 없는 곳에 AI가 생겼다). 위에서 쏜 광선은 가장 높은 면에
    // 닿으므로, 그 자리가 AI NavMesh 위이고 쿠키 스폰 자리에서 그곳까지 길이 이어지는지 본다. NavMesh가 없는 맵·씬은 막지 않는다.
    private const float WalkableSnapHeight = 0.6f;
    private NavMeshPath reachPath;
    private Vector3 reachAnchor;
    private bool reachAnchorSet;

    public bool IsWalkableFromSpawn(Vector3 feet)
    {
        if (!DecoyNavMesh.Ready) return true;
        if (!NavMesh.SamplePosition(feet, out NavMeshHit hit, 1.2f, DecoyNavMesh.Filter) || Mathf.Abs(hit.position.y - feet.y) > WalkableSnapHeight) return false;
        if (!TryReachAnchor(out Vector3 anchor)) return true;
        if (reachPath == null) reachPath = new NavMeshPath();
        return NavPath.Reaches(anchor, hit.position, reachPath);   // 긴 길은 탐색 한도를 넘으므로 끝에서 이어 찾는다(§54)
    }

    private bool TryReachAnchor(out Vector3 anchor)
    {
        if (!reachAnchorSet)
        {
            GameObject spawn = GameObject.Find(SceneSpawnPoints.Cookie);
            if (spawn != null && NavMesh.SamplePosition(spawn.transform.position, out NavMeshHit hit, 6f, DecoyNavMesh.Filter))
            {
                reachAnchor = hit.position;
                reachAnchorSet = true;
            }
        }
        anchor = reachAnchor;
        return reachAnchorSet;
    }

    // 이동할 곳이 맞는지(방장 검사): 맵 안, 술래방 밖, 캐릭터가 끼지 않는다.
    public bool IsValidTeleport(Vector3 p) => !IsOutsideMap(p, -1f) && !InMonsterRoom(p) && IsClearForCharacter(p);

    // ---------------- 재료 안전장치(Request1009Plan.md §26 A~D) ----------------

    private const float ReachableHeight = 2.5f; // 줍기 화면 판정(CharacterInteractor.MaxHeightDifference)과 같게

    // 쿠키·스파이가 주울 수 있는 바닥인가: 맵 안, 술래방 밖, 그 아래 바닥에서 손이 닿는 높이.
    public bool IsReachableGround(Vector3 p)
    {
        if (IsOutsideMap(p, -0.5f) || InMonsterRoom(p)) return false;
        Vector3 floor = SnapToGround(new Vector3(p.x, p.y - 0.05f, p.z));
        return p.y - floor.y <= ReachableHeight + 0.5f;
    }

    // 바닥에 놓을 자리: 요청 자리가 닿는 곳이면 그대로(바닥 맞춤), 아니면 가장 가까운 건물 수리 자리 둘레.
    public Vector3 SafeGroundPoint(Vector3 requested, int roll)
    {
        Vector3 p = SnapToGround(requested);
        if (IsReachableGround(p)) return p;
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int b = 0; b < buildings.Length; b++)
        {
            float d = (BuildingRepairPoint(b) - requested).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = b; }
        }
        Debug.Log($"[EscapeManager] Drop point {requested} is not reachable — moved next to building {best}.");
        return DropPointNear(BuildingRepairPoint(best), roll);
    }

    // 위치를 모를 때 아이템을 둘 곳: 쿠키 스폰 근처(원점은 술래방 한가운데라 쓰지 않는다).
    public Vector3 FallbackDropPoint(int roll)
    {
        GameObject spawn = GameObject.Find(SceneSpawnPoints.Cookie);
        return DropPointNear(spawn != null ? spawn.transform.position : BuildingRepairPoint(0), roll);
    }

    // 맵 밖으로 떨어져 쿠키 스폰 자리로 돌아온 위치인지(이동 검사 예외).
    public bool IsNearCookieSpawn(Vector3 p)
    {
        GameObject spawn = GameObject.Find(SceneSpawnPoints.Cookie);
        return spawn != null && Vector3.Distance(spawn.transform.position, p) <= GameSettings.Current.CookieSpawnRange + 2f;
    }

    private static bool IsClearForCharacter(Vector3 feet)
    {
        Vector3 bottom = feet + Vector3.up * (CapsuleRadius + 0.05f);
        Vector3 top = feet + Vector3.up * (CapsuleHeight - CapsuleRadius);
        foreach (Collider c in Physics.OverlapCapsule(bottom, top, CapsuleRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (c.GetComponentInParent<IGameCharacter>() == null) return false;
        return true;
    }

    // 어떤 자리 둘레 바닥(상자가 모자랄 때 아이템을 놓는 곳).
    public Vector3 DropPointNear(Vector3 center, int roll)
    {
        Vector3 dir = Quaternion.Euler(0f, (roll & 0xFFFF) / 65536f * 360f, 0f) * Vector3.forward;
        for (int i = 1; i <= 4; i++)
        {
            Vector3 p = SnapToGround(center + dir * (2f * i) + Vector3.up * 2f);
            if (IsClearForCharacter(p)) return p;
        }
        return SnapToGround(center + dir * 2.5f + Vector3.up * 2f);
    }

    // pos 위로 천장(막힌 면)까지의 월드 높이, 없으면 0(§69 E3 — 지하 동굴에서 콜라 대포 비행 한계).
    public float CeilingAbove(Vector3 pos, float maxUp)
    {
        float best = 0f;
        foreach (RaycastHit h in Physics.RaycastAll(pos + Vector3.up * 0.5f, Vector3.up, maxUp, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<IGameCharacter>() != null || h.collider.GetComponentInParent<DecoyView>() != null) continue;
            if (best <= 0f || h.point.y < best) best = h.point.y;
        }
        return best;
    }

    // from에서 아래로 처음 닿는 바닥(캐릭터·AI 몸은 건너뜀). SnapToGround와 달리 시작 높이 위는 보지 않는다 — 천장 위층 바닥이 잡히지 않게.
    public bool TryGroundBelow(Vector3 from, float maxDown, out Vector3 ground)
    {
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, maxDown, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<IGameCharacter>() != null || h.collider.GetComponentInParent<DecoyView>() != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        ground = new Vector3(from.x, best, from.z);
        return !float.IsNegativeInfinity(best);
    }

    public Vector3 SnapToGround(Vector3 pos)
    {
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(pos + Vector3.up * 3f, Vector3.down, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<IGameCharacter>() != null || h.collider.GetComponentInParent<DecoyView>() != null) continue; // 캐릭터·AI 몸 위에 서지 않는다
            if (h.point.y > best && h.point.y <= pos.y + 1.5f) best = h.point.y;
        }
        return float.IsNegativeInfinity(best) ? pos : new Vector3(pos.x, best, pos.z);
    }

#if UNITY_EDITOR
    public void EditorBind(Transform[] chestSlots, QuadrantBuilding[] buildingViews, EscapeGate[] exitViews, SpyRocket spyRocket,
        Transform[] rocketSpots, Transform[] rainbowSpots, RainbowChest rainbow, Transform room, WitchPresenter witchPresenter)
    {
        chestAnchors = chestSlots;
        buildings = buildingViews;
        exits = exitViews;
        rocket = spyRocket;
        rocketAnchors = rocketSpots;
        rainbowAnchors = rainbowSpots;
        rainbowChest = rainbow;
        monsterRoom = room;
        witch = witchPresenter;
    }

    public void EditorSetMapRadius(float radius) => mapRadius = Mathf.Max(0f, radius);
#endif
}
