using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// HideOrSeekPlayer.prefab에 부착 — 쿠키의 생존/파괴 상태를 그 캐릭터의 모습·물리에 반영한다(GameRule.md §4.4).
// (예전 이름 PlayerCrackDisplay, .meta GUID 유지로 프리팹 참조 보존.)
//
// 파괴 여부는 "속성 변경 통지 한 번"이 아니라 소유자의 현재 HitCount 값으로 계산하고, 여러 시점에 반복해서 맞춘다
// (idempotent). 예전에는 OnPlayerPropertiesUpdate 한 번에만 의존해, 통지를 놓치거나 통지 이후에 생긴 복사본은
// 콜라이더가 켜진 채 남아 파괴된 쿠키 자리에 보이지 않는 벽이 생길 수 있었다(Bug-fix-plan.md §28.3).
// 파괴 시에는 콜라이더를 끄는 것에 더해 몸 전체를 어떤 레이어와도 충돌하지 않는 BrokenCookie 레이어로 옮긴다.
// 표시물도 몸통 하나가 아니라 캐릭터의 모든 렌더러(이름표 TextMeshPro 포함)를 숨긴다 — 예전에는 몸통만 꺼서
// 파괴된 쿠키의 이름표가 공중에 남았다(Bug-fix-plan.md §30.3). 새 표시물을 붙여도 코드 수정 없이 함께 숨겨진다.
//
// 괴물에게 처형당할 때는 곧바로 사라지지 않고 "붙잡힘" 단계를 거친다(Bug-fix-plan.md §36): 충돌만 즉시 끄고, 쿠키를 그 괴물의
// Grab_Socket에 붙여 따라가게 하다가, 이 클라이언트의 괴물 애니메이션이 부서지는 순간(두 손이 맞붙는 진행도)에 이르면 숨긴다.
// 각 클라이언트가 자기 화면의 괴물 애니메이션을 기준으로 하므로 원격 화면에서도 괴물 손과 맞는다. 파괴 판정(HitCount)은 처형
// 즉시 기록되지만, 붙잡힘 동안에는 숨기기를 미룬다.
public class CookieLifeStatePresenter : MonoBehaviourPunCallbacks
{
    private const float ReconcileInterval = 0.5f; // 쿠키 수만큼 정수 비교 한 번 — 인원이 늘어도 비용이 작다
    private const float GrabKillTimeoutMargin = 1f; // 괴물 애니메이션이 끝까지 오지 않을 때(상태 누락 등) 클립 길이에 더해 기다리는 시간

    [SerializeField] private PhotonView pv;
    [SerializeField] private Renderer[] keepVisibleWhenBroken = new Renderer[0]; // 파괴 후에도 보여야 하는 표시물(기본 없음)
    [Tooltip("처형으로 부서질 때 터지는 가루(ParticleSystem, 방출은 코드가 칠한 색을 붙여 Emit). 전원 로컬 재생.")]
    [SerializeField] private GameObject breakVfxPrefab;
    [SerializeField, Min(1)] private int crumbCount = 12;
    [SerializeField] private Color fallbackCrumbColor = new Color(0.55f, 0.30f, 0.12f); // 색을 읽지 못했을 때(원본 CookieCrumb 재질 색)
    [SerializeField] private string brokenLayerName = "BrokenCookie"; // ProjectSettings 충돌 매트릭스에서 모든 레이어와 충돌 해제된 레이어

    // 원본 분쇄 연출의 눌림 비율(블렌더 Cookie_Ref: 세로 0.0045 → 0.0022, 가로 0.0045 → 0.0054).
    private const float SquashHeightScale = 0.5f;
    private const float SquashWidthScale = 1.2f;

    private enum LifeState { Unknown, Alive, Broken }

    private LifeState appliedState = LifeState.Unknown;
    private float nextReconcileTime;
    private int brokenLayer = -1;

    // 파괴 전 상태를 기억해 복원할 수 있게 한다(판 초기화 등으로 HitCount가 지워지는 경우).
    private readonly List<(Collider collider, bool enabled)> savedColliders = new List<(Collider, bool)>();
    private readonly List<(GameObject go, int layer)> savedLayers = new List<(GameObject, int)>();
    private readonly List<(Renderer renderer, bool enabled)> savedRenderers = new List<(Renderer, bool)>();

    private bool collisionDisabled;
    private bool visualsHidden;

    // 붙잡힘 단계
    private MonsterController grabbingMonster;
    private bool grabKillEnteredAnimation; // 이 클라이언트의 괴물 Animator가 GrabKill에 한 번이라도 들어갔는지(원격은 상태 동기화가 조금 늦다)
    private float grabKillDeadline;
    private float bodyCenterHeight = 1f; // 루트 캡슐 중심 높이 — 이 점을 소켓에 맞춘다
    private Vector3 originalScale = Vector3.one;
    private Color[] crumbColors;          // 몸 표면 색(칠한 색 + 스킨 색), 비동기로 채워진다
    private bool crumbsSpawned;
    private bool squashPlayed;

    public bool IsBroken => appliedState == LifeState.Broken;
    public bool IsBeingGrabKilled { get; private set; }
    // 마녀 도착 연출의 대역이 같은 가루를 쓴다(WitchFinaleStage)
    public GameObject BreakVfxPrefab => breakVfxPrefab;
    public Color CrumbColor => fallbackCrumbColor;

    // 그 사람 캐릭터의 가루 색(SkinCatalogSO, §45.10). 없으면 null.
    public static Color[] CharacterCrumbColors(Photon.Realtime.Player owner)
    {
        SkinCatalogSO catalog = GameSettings.Current != null ? GameSettings.Current.CharacterCatalog : null;
        if (catalog == null || catalog.Count == 0 || owner == null) return null;
        Color[] colors = catalog.GetCrumbColors(PlayerSkinApplier.CharacterIndexOf(owner));
        return colors != null && colors.Length > 0 ? colors : null;
    }
    public int CrumbCount => crumbCount;

    private void Awake()
    {
        brokenLayer = LayerMask.NameToLayer(brokenLayerName);
        if (brokenLayer < 0)
            Debug.LogWarning($"[CookieLife] Layer '{brokenLayerName}' not found. Broken cookies will only disable colliders.");

        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null) bodyCenterHeight = capsule.center.y * transform.lossyScale.y;
        originalScale = transform.localScale;
    }

    // 처형이 시작될 때 모든 클라이언트에서 HideOrSeekPlayer.RequestGrabKill이 호출한다. 연출을 시작했으면 true.
    // 이미 파괴돼 보이는 쿠키(늦게 도착한 RPC 등)는 그대로 둔다.
    public bool BeginGrabKill(MonsterController monster)
    {
        if (IsBeingGrabKilled || visualsHidden || monster == null || monster.GrabSocket == null) return false;

        grabbingMonster = monster;
        grabKillEnteredAnimation = false;
        grabKillDeadline = Time.time + monster.GrabKillDuration + GrabKillTimeoutMargin;
        crumbColors = null;
        crumbsSpawned = false;
        squashPlayed = false;
        IsBeingGrabKilled = true;
        DisableCollision(); // 괴물 몸·다른 쿠키와 부딪히지 않도록 즉시

        // 가루 색은 지금 몸에 보이는 색(칠한 색 + 스킨 색)으로 — 분쇄(약 1.6초 뒤) 전에 비동기로 읽어 둔다.
        // 캐릭터 가루 색이 있으면 그것(§45.10 — 새 캐릭터는 칠하기를 쓰지 않는다), 없으면 몸에 칠한 색.
        Color[] character = CharacterCrumbColors(pv != null ? pv.Owner : null);
        if (character != null) crumbColors = character;
        else
        {
            var paintCanvas = GetComponent<PlayerPaintCanvas>();
            if (paintCanvas != null) paintCanvas.SampleSurfaceColors(colors => crumbColors = colors);
        }

        FollowGrabSocket(Vector3.one);
        Debug.Log($"[CookieLife] view={pv.ViewID} grabbed by monster view={monster.View.ViewID}");
        return true;
    }

    // Animator가 이번 프레임의 본을 움직인 뒤에 소켓을 따라간다. 원본 분쇄 연출 순서: 눌림 → (가루 터짐) → 작아지며 사라짐.
    private void LateUpdate()
    {
        if (!IsBeingGrabKilled) return;

        if (grabbingMonster == null) // 괴물이 방을 나가 사라짐
        {
            FinishGrabKill("monster gone");
            return;
        }

        MonsterController monster = grabbingMonster;
        float progress = monster.GrabKillProgress;
        if (progress >= 0f) grabKillEnteredAnimation = true;

        if (progress >= monster.GrabKillCrushNormalizedTime) { FinishGrabKill("crush"); return; }
        if (grabKillEnteredAnimation && progress < 0f) { FinishGrabKill("animation ended"); return; }
        if (Time.time >= grabKillDeadline) { FinishGrabKill("timeout"); return; }

        if (!squashPlayed && progress >= monster.GrabKillSquashStart)
        {
            squashPlayed = true;
            GameAudio.PlayAt(SoundId.MonsterSquash, transform.position); // 내리찍어 눌리는 순간(SoundPlan.md S4)
        }
        if (!crumbsSpawned && progress >= monster.GrabKillCrumbTime) SpawnCrumbs();
        FollowGrabSocket(CrushScale(monster, progress));
    }

    // 진행도에 따른 몸 크기 배율: 눌림 구간에 세로는 줄고 가로는 넓어지고, 축소 구간에 전체가 0으로.
    public static Vector3 CrushScale(MonsterController monster, float progress)
    {
        if (progress < 0f) return Vector3.one;
        float squash = Mathf.InverseLerp(monster.GrabKillSquashStart, monster.GrabKillSquashEnd, progress);
        float shrink = 1f - Mathf.InverseLerp(monster.GrabKillShrinkStart, monster.GrabKillCrushNormalizedTime, progress);
        float width = Mathf.Lerp(1f, SquashWidthScale, squash) * shrink;
        float height = Mathf.Lerp(1f, SquashHeightScale, squash) * shrink;
        return new Vector3(width, height, width);
    }

    // 몸 중심(캡슐 중심)을 소켓에 맞추고 괴물을 바라본다. 크기가 줄어도 몸 중심은 소켓에 머문다.
    private void FollowGrabSocket(Vector3 scale)
    {
        Transform socket = grabbingMonster.GrabSocket;
        Vector3 toMonster = Vector3.ProjectOnPlane(grabbingMonster.transform.position - socket.position, Vector3.up);
        Quaternion facing = toMonster.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toMonster) : transform.rotation;
        transform.localScale = Vector3.Scale(originalScale, scale);
        transform.SetPositionAndRotation(socket.position - Vector3.up * (bodyCenterHeight * scale.y), facing);
    }

    // 소켓 위치에서 가루를 터뜨린다. 조각마다 몸 표면 색 중 하나를 무작위로 받는다(D4 — 칠한 색이 섞인다).
    // 가루는 괴물 정면 쪽으로 튄다(프리팹 원뿔이 +Z 방향).
    private void SpawnCrumbs()
    {
        crumbsSpawned = true;
        GameAudio.PlayAt(SoundId.CookieCrumble, transform.position); // 부서져 가루가 되는 순간(SoundPlan.md S4)
        if (breakVfxPrefab == null) return;

        Vector3 position = grabbingMonster != null ? grabbingMonster.GrabSocket.position : transform.position + Vector3.up * bodyCenterHeight;
        Vector3 forward = grabbingMonster != null ? Vector3.ProjectOnPlane(grabbingMonster.transform.forward, Vector3.up) : transform.forward;
        Quaternion rotation = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward) : Quaternion.identity;

        GameObject burst = Instantiate(breakVfxPrefab, position, rotation);
        var particles = burst.GetComponent<ParticleSystem>();
        if (particles == null) return;

        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < crumbCount; i++)
        {
            emit.startColor = crumbColors != null && crumbColors.Length > 0 ? crumbColors[Random.Range(0, crumbColors.Length)] : fallbackCrumbColor;
            particles.Emit(emit, 1);
        }
        Debug.Log($"[CookieLife] view={pv.ViewID} crumbs x{crumbCount} ({(crumbColors != null ? crumbColors.Length + " surface colors" : "fallback color")})");
    }

    // 부서지는 순간: 숨긴다. 가루가 아직이면(애니메이션이 빨리 끝남 등) 여기서 터뜨린다. 파괴 확정(방장이 쓴 HitCount)은 따로 오므로
    // 확정이 없으면(방장이 거절) 주기적 맞춤(Reconcile)이 다시 보이게 되돌린다. 관전 전환은 확정된 뒤에만(EnterSpectatorIfConfirmed).
    private void FinishGrabKill(string reason)
    {
        if (!crumbsSpawned) SpawnCrumbs();
        IsBeingGrabKilled = false;
        grabbingMonster = null;
        ApplyBroken();
        appliedState = LifeState.Broken;
        Debug.Log($"[CookieLife] view={pv.ViewID} crushed (via {reason})");
        EnterSpectatorIfConfirmed();
    }

    // 본인 쿠키가 방장 확정으로 부서졌으면 관전으로 넘어간다(같은 이유로 여러 번 불러도 SpectatorController가 한 번만 처리).
    private void EnterSpectatorIfConfirmed()
    {
        if (!EscapeManager.IsActive) return; // 대기실: 이전 판 HitCount가 초기화 전에 잠깐 남아 있어도 관전으로 넘어가지 않는다
        if (pv == null || !pv.IsMine || pv.Owner == null || !RoomState.IsBroken(pv.Owner)) return;
        if (TryGetComponent(out SpectatorController spectator)) spectator.EnterSpectatorMode(SpectatorKind.Broken);
    }

    private void Start()
    {
        Reconcile("start"); // 이미 파괴된 상태로 생성된 복사본(통지 이후 생성)도 즉시 맞춘다
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (pv == null || targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.HitCount)) return;
        Reconcile("callback");
    }

    public override void OnJoinedRoom()
    {
        Reconcile("rejoin");
    }

    private void Update()
    {
        if (Time.unscaledTime < nextReconcileTime) return;
        nextReconcileTime = Time.unscaledTime + ReconcileInterval;
        Reconcile("periodic");
    }

    private void Reconcile(string reason)
    {
        if (pv == null || pv.Owner == null) return;
        if (IsBeingGrabKilled) return; // 붙잡힘 동안에는 부서지는 순간(FinishGrabKill)까지 숨기기를 미룬다
        // 대기실에서는 늘 살아 있는 모습 — 이전 판 HitCount가 방장의 초기화 전에 잠깐 남아 있다(2026-10-12 멀티 시험).
        LifeState desired = EscapeManager.IsActive && RoomState.IsBroken(pv.Owner) ? LifeState.Broken : LifeState.Alive;
        if (desired == LifeState.Broken) EnterSpectatorIfConfirmed(); // 처형 연출이 먼저 끝나고 확정이 나중에 온 경우·마녀
        if (desired == appliedState) return;

        if (desired == LifeState.Broken)
        {
            if (reason == "callback" && DiedToWitch()) ShatterByWitch(); // 내리치는 순간 몸이 조각나며 가루로(Request1009Plan.md §23.5 X7)
            ApplyBroken();
        }
        else if (appliedState == LifeState.Broken) RestoreAlive();

        appliedState = desired;
        if (desired == LifeState.Broken || reason != "start")
            Debug.Log($"[CookieLife] view={pv.ViewID} owner={pv.Owner.ActorNumber} -> {desired} (via {reason})");
    }

    private bool DiedToWitch() =>
        RoomState.TryGetPlayerInt(pv.Owner, NetKeys.DeathCause, out int cause) && cause == (int)DeathCause.Witch;

    private void ShatterByWitch()
    {
        if (visualsHidden || WitchFinaleStage.Active) return; // 도착 연출 중에는 숨긴 실제 몸 대신 대역이 부서진다(§24.10)
        WitchShatter.ShatterBody(GetComponentsInChildren<Renderer>(), fallbackCrumbColor);
        if (!crumbsSpawned) SpawnCrumbs();
    }

    private void ApplyBroken()
    {
        DisableCollision();
        if (visualsHidden) return;
        visualsHidden = true;

        // 몸통·이름표 등 모든 표시물을 감춘다. 가루는 처형 연출(SpawnCrumbs)에서만 터진다 — 이미 파괴된 쿠키가 있는 방에
        // 늦게 들어와 여기로 오는 경우에는 터지지 않아야 한다.
        savedRenderers.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (keepVisibleWhenBroken != null && System.Array.IndexOf(keepVisibleWhenBroken, r) >= 0) continue;
            savedRenderers.Add((r, r.enabled));
            r.enabled = false;
        }
    }

    // 모든 콜라이더를 끄고, 몸 전체를 비충돌 레이어로 옮긴다 — 둘 중 하나만으로도 벽이 되지 않도록 이중으로 막는다.
    private void DisableCollision()
    {
        if (collisionDisabled) return;
        collisionDisabled = true;

        savedColliders.Clear();
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            savedColliders.Add((col, col.enabled));
            col.enabled = false;
        }

        savedLayers.Clear();
        if (brokenLayer >= 0)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                savedLayers.Add((t.gameObject, t.gameObject.layer));
                t.gameObject.layer = brokenLayer;
            }
        }
    }

    private void RestoreAlive()
    {
        collisionDisabled = false;
        visualsHidden = false;
        transform.localScale = originalScale; // 처형 연출로 눌리고 줄어든 크기 복원
        foreach (var (r, enabled) in savedRenderers)
            if (r != null) r.enabled = enabled;
        foreach (var (col, enabled) in savedColliders)
            if (col != null) col.enabled = enabled;
        foreach (var (go, layer) in savedLayers)
            if (go != null) go.layer = layer;
        savedColliders.Clear();
        savedLayers.Clear();
        savedRenderers.Clear();
    }
}
