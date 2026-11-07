using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 파괴된 쿠키(hitCount>=2)·탈출한 쿠키의 관전 모드(GameRule.md §6.3). HideOrSeekPlayer.prefab에 부착, 로컬 소유자 전용 —
// 부서지는 순간(HideOrSeekPlayer·CookieLifeStatePresenter)·마녀에게 죽는 순간은 EnterSpectatorMode(Broken),
// 탈것 탑승·탈출 순간(EscapeCharacterState)은 EnterSpectatorMode(Escaped)를 부른다. 누구를 볼 수 있는지는 SpectateRules(Request1009Plan.md §9).
//
// 이 컴포넌트는 "누구를 볼지"만 정하고, 카메라 조작은 쿠키·괴물과 같은 Camera_Ctrl에 맡긴다. 예전에는
// Camera_Ctrl을 끄고 대상 등 뒤 고정 위치로 카메라를 직접 옮겨 우클릭 드래그 회전이 불가능했고, 대상 목록이
// Space를 누를 때만 갱신돼 보던 쿠키가 파괴되면 시체를 따라가거나, 퇴장으로 오브젝트가 사라지면 예외가 났다
// (Bug-fix-plan.md §28.4).
//
// 후보는 캐릭터 공통 계약(IGameCharacter)과 CharacterRegistry로만 찾는다 — 예전에는 쿠키·괴물 타입을 각각
// FindObjectsByType로 훑고 카메라 설정도 타입으로 분기했다(research.md §12 E1). 새 캐릭터 종류가 생겨도 수정할 필요가 없다.
public class SpectatorController : MonoBehaviour
{
    // 부서진 쿠키가 괴물까지 볼 수 있는지(프리팹 설정). 탈출한 쿠키는 이 값과 상관없이 괴물·스파이를 보지 않는다(SpectateRules).
    public enum SpectateTargets
    {
        AliveCookies,             // 살아 있는 다른 쿠키만(기본, GameRule.md §6.3)
        AliveCookiesAndMonsters,  // 괴물 시점도 관전
    }

    // 관전 대상이 바뀔 때 알린다(null = 관전 대상 없음/종료). 씬 UI(SpectatorLabel)가 구독한다 — 프리팹이 씬
    // 오브젝트를 직접 참조하지 않도록 이벤트로 분리했다.
    public static event System.Action<string> SpectateTargetChanged;
    // 자기가 탄 탈것을 보는 중인지(true면 안내에 "Space: 다른 사람 보기").
    public static event System.Action<bool> SpectatingVehicleChanged;

    [SerializeField] private PhotonView pv;
    [SerializeField] private SpectateTargets targets = SpectateTargets.AliveCookies;

    // 볼 대상이 하나도 없을 때 후보 재탐색 간격 — 매 프레임 후보를 다시 모으지 않도록 한다.
    private const float EmptyRetryInterval = 0.5f;
    private float nextEmptyRetryTime;

    private readonly List<IGameCharacter> candidates = new List<IGameCharacter>();
    private IGameCharacter currentTarget;
    private bool isSpectating;
    private SpectatorKind kind;
    private bool vehicleDismissed; // 탈것을 보다가 Space를 누르면 다시 탈것으로 돌아가지 않는다
    private bool watchingVehicle;
    private bool wasCutscene;
    private Camera_Ctrl camCtrl;

    public SpectatorKind Kind => kind;

    // 코드로 붙일 때(판 도중 죽은 술래 — MonsterEscapeState, §64.6).
    public void Init(PhotonView view) => pv = view;

    public void EnterSpectatorMode(SpectatorKind reason)
    {
        if (!pv.IsMine) return;
        if (isSpectating)
        {
            // 탈것에서 기다리다 마녀에게 죽는 등 이유가 바뀌면 후보 규칙만 바꾸고, 보던 대상이 더는 후보가 아니면 넘긴다.
            SpectatorKind merged = SpectateRules.Merge(kind, reason);
            if (merged == kind) return;
            kind = merged;
            if (camCtrl != null && !watchingVehicle && !IsCandidate(currentTarget)) SwitchToNext();
            return;
        }
        isSpectating = true;
        kind = reason;

        camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
        if (camCtrl == null)
        {
            Debug.LogError("[Spectator] Camera_Ctrl not found on Main Camera.");
            return;
        }
        camCtrl.enabled = true; // 끄지 않는다 — 우클릭 드래그 회전·커서 잠금을 그대로 쓴다
        SwitchToNext();
    }

    private void Update()
    {
        if (!pv.IsMine || !isSpectating || camCtrl == null) return;

        // 연출(마녀 등장 등)이 끝나면 카메라를 다시 보던 사람에게 붙이고, 그 사이 대상이 사라졌으면 바로 넘긴다(§26 H15).
        bool cutscene = PlayerInput.CutsceneActive;
        if (wasCutscene && !cutscene) Revalidate();
        wasCutscene = cutscene;
        if (cutscene) return;

        // Space(InputBindings)는 언제나 먼저 — 탈것을 보는 중이어도 다음 사람으로 넘어간다(Request1009Plan.md §9).
        bool next = PlayerInput.SpectateNextPressed;
        Transform vehicle = vehicleDismissed ? null : VehicleFocus();
        if (vehicle != null && next)
        {
            vehicleDismissed = true;
            vehicle = null;
        }

        // 탈것에 탄 쿠키는 기다리는 동안·출발하는 동안 자기가 탄 탈것을 본다(EscapeVisualPlan.md §4.3)
        SetWatchingVehicle(vehicle != null);
        if (vehicle != null)
        {
            if (camCtrl.FollowTarget != vehicle.gameObject)
            {
                currentTarget = null;
                camCtrl.SetFollowTarget(vehicle.gameObject, VehicleTargetHeight, VehicleCameraDistance, keepRotation: true);
                SpectateTargetChanged?.Invoke(null);
            }
            return;
        }
        if (!next && currentTarget == null && camCtrl.FollowTarget != null && Time.unscaledTime >= nextEmptyRetryTime)
        {
            SwitchToNext(); // 탈것을 보다가 끝났으면 다시 다른 쿠키를 본다
            nextEmptyRetryTime = Time.unscaledTime + EmptyRetryInterval;
        }

        if (next)
        {
            SwitchToNext(); // 수동 전환(키는 InputBindings)
        }
        else if (!IsCandidate(currentTarget) && Time.unscaledTime >= nextEmptyRetryTime)
        {
            SwitchToNext(); // 보던 대상이 파괴·퇴장했으면 자동 전환(대상이 없으면 0.5초마다 재시도)
            if (currentTarget == null) nextEmptyRetryTime = Time.unscaledTime + EmptyRetryInterval;
        }
    }

    private const float VehicleTargetHeight = 2f;
    private const float VehicleCameraDistance = 13f;

    // 스파이 로켓이 날아오르는 동안(약 7초)은 관전 중인 모두가 로켓을 본다(§26 H15). 그 밖에는 null.
    private static Transform VehicleFocus()
    {
        EscapeManager manager = EscapeManager.Instance;
        if (manager == null || manager.State == null) return null;
        if (manager.Rocket != null && RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out double launchedAt)
            && PhotonNetwork.Time < launchedAt + EscapeManager.RocketLaunchShowSeconds + 1f)
            return manager.Rocket.transform;
        return null;
    }

    private void SetWatchingVehicle(bool value)
    {
        if (watchingVehicle == value) return;
        watchingVehicle = value;
        SpectatingVehicleChanged?.Invoke(value);
    }

    private void OnDestroy()
    {
        if (!isSpectating) return;
        SpectateTargetChanged?.Invoke(null);
        if (watchingVehicle) SpectatingVehicleChanged?.Invoke(false);
    }

    private void Revalidate()
    {
        if (watchingVehicle) return;
        if (!IsCandidate(currentTarget)) { SwitchToNext(); return; }
        camCtrl.SetFollowTarget(currentTarget.gameObject, currentTarget.CameraTargetHeight, currentTarget.CameraDistance, keepRotation: true);
    }

    private void SwitchToNext()
    {
        RefreshCandidates();
        if (candidates.Count == 0)
        {
            // 볼 대상이 없으면 카메라는 마지막 위치에 머문다(추적 대상 해제).
            if (currentTarget != null || camCtrl.FollowTarget != null)
            {
                currentTarget = null;
                camCtrl.SetFollowTarget(null, Camera_Ctrl.CookieTargetHeight, -1f, keepRotation: true);
                SpectateTargetChanged?.Invoke(null);
                Debug.Log($"[Spectator] {kind}: no one to watch.");
            }
            return;
        }

        int currentIndex = currentTarget != null ? candidates.IndexOf(currentTarget) : -1;
        IGameCharacter next = candidates[(currentIndex + 1) % candidates.Count];
        if (next == currentTarget) return;

        currentTarget = next;
        camCtrl.SetFollowTarget(next.gameObject, next.CameraTargetHeight, next.CameraDistance, keepRotation: true);
        SpectateTargetChanged?.Invoke(GetDisplayName(next));
        Debug.Log($"[Spectator] {kind}: now watching actor {GetActorNumber(next)} ({next.Role}).");
    }

    private void RefreshCandidates()
    {
        candidates.Clear();
        foreach (IGameCharacter character in CharacterRegistry.All)
            if (IsCandidate(character)) candidates.Add(character);

        // 입장 순서로 정렬해 순환 순서를 모든 상황에서 일정하게 유지한다.
        candidates.Sort((a, b) => GetActorNumber(a).CompareTo(GetActorNumber(b)));
    }

    private bool IsCandidate(IGameCharacter character)
    {
        if (character == null || !CharacterRegistry.IsAlive(character)) return false; // 퇴장 등으로 파괴됨
        PhotonView view = character.View;
        if (view == null || view.Owner == null || view.IsMine) return false; // 자기 자신(이미 파괴됨) 제외
        return SpectateRules.IsCandidate(kind, character.Role, RoomState.IsSpy(view.Owner.ActorNumber), character.IsSpectatable,
            targets == SpectateTargets.AliveCookiesAndMonsters);
    }

    private static int GetActorNumber(IGameCharacter character)
    {
        PhotonView view = character.View;
        return view != null && view.Owner != null ? view.Owner.ActorNumber : int.MaxValue;
    }

    private static string GetDisplayName(IGameCharacter character)
    {
        var owner = character.View != null ? character.View.Owner : null;
        if (owner == null) return string.Empty;
        return string.IsNullOrEmpty(owner.NickName) ? "#" + owner.ActorNumber : owner.NickName;
    }
}
