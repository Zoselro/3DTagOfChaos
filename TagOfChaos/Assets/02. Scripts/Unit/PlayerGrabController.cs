using System.Collections;
using Photon.Pun;
using UnityEngine;

// 쿠키↔쿠키 그랩/캐리 시작·해제(그랩버 측, GameRule.md §4.1). 소유권 이전 없이 들린 쿠키는 자기 PhotonView를 그대로 유지하고,
// 모든 화면이 그 몸을 그랩버의 두 손 위에 놓는다(CarriedBodyPresenter, Request1009Plan.md §16.3 — 예전의 등 뒤 carrySocket은 쓰지 않는다).
// 그랩버 화면은 들린 쿠키의 동기화가 오기 전에도 바로 손 위에 놓이도록 그 쿠키에 로컬로 알려 둔다.
[RequireComponent(typeof(PhotonView))]
public class PlayerGrabController : MonoBehaviour
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private HideOrSeekPlayer self;
    [SerializeField] private float grabRange = 1.5f;
    [SerializeField] private LayerMask cookieLayer; // 프리팹에서 Cookie 레이어(쿠키 루트 캡슐)로 설정(research.md §8.6)

    // 내려놓은 직후에는 들렸던 쿠키의 원격 복사본이 아직 내 등(소켓) 위치에 겹쳐 있다 — 새 위치가 동기화되기
    // 전에 충돌을 되살리면 내 몸이 밀려나므로 잠시 뒤에 복원한다.
    private const float RestoreCollisionDelay = 0.5f;

    private HideOrSeekPlayer carriedPlayer;
    private readonly Collider[] overlapBuffer = new Collider[16]; // OverlapSphere 할당 제거

    public bool IsCarrying => carriedPlayer != null;
    public HideOrSeekPlayer CarriedPlayer => carriedPlayer;

    // 들린 쪽이 좌우 연타로 빠져나간 뒤 바로 다시 들지 못하게(§69 C2).
    private const float RegrabBlockSeconds = 1.5f;
    private float grabBlockedUntil = -1f;
    public float GrabRange => grabRange;

    // 들고 있던 쿠키가 방을 나가 파괴되면 Unity의 == null 비교는 true가 되지만 참조는 남아 있다. 정리가 필요한지는
    // 참조 존재(ReferenceEquals)로, 살아 있는지는 Unity bool로 구분한다(Bug-fix-plan.md §41 ㊸).
    private bool HasCarryReference => !ReferenceEquals(carriedPlayer, null);

    private void Update()
    {
        if (!pv.IsMine) return;

        // 들고 있던 쿠키가 파괴됐거나 방을 나가 사라졌으면 캐리 상태를 정리한다.
        if (HasCarryReference && (!carriedPlayer || RoomState.IsBroken(carriedPlayer.View.Owner)))
            Release();
        // 탈것에 타거나 탈출하면(몸이 숨겨진다) 들고 있던 쿠키를 그 자리에 내려놓는다 — 들린 채로 남아 아무것도 못 하게 되지 않게
        if (HasCarryReference && pv.Owner != null && EscapeManager.HasLeftMap(pv.Owner.ActorNumber))
            Release();

        if (!PlayerInput.GrabPressed) return;
        if (self != null && (self.IsBroken || self.IsMovementLocked)) return; // 파괴·들림 중에는 그랩 불가(채팅 중에는 GrabPressed가 억제된다)

        if (carriedPlayer == null)
        {
            if (Time.time < grabBlockedUntil) return;
            if (PlayerInventory.Local != null && PlayerInventory.Local.HeldItem != null) return; // 물건을 든 손으로는 쿠키를 들 수 없다
            TryGrab();
        }
        else Release();
    }

    private void TryGrab()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, grabRange, overlapBuffer, cookieLayer);
        for (int i = 0; i < count; i++)
        {
            var target = overlapBuffer[i].GetComponentInParent<HideOrSeekPlayer>();
            if (target == null || target.gameObject == gameObject) continue;
            var targetPv = target.View;
            if (targetPv == null || targetPv.Owner == null) continue;
            if (target.IsBroken) continue; // 맵에서만 파괴로 본다(대기실의 이전 판 값 무시)
            // 이미 누군가에게 들렸거나 누군가를 들고 있는 쿠키, 탈것에 탄 쿠키는 들 수 없다(F-01 — 두 명이 같은 쿠키를 들거나 연쇄로 드는 것 방지).
            // 받는 쪽도 같은 규칙으로 거절한다(HideOrSeekPlayer.OnGrabbedByOwner) — 동시에 들려는 경합은 그쪽에서 정리된다.
            if (target.CurrentCarrierViewId >= 0 || target.IsCarryingSomeone || EscapeManager.HasLeftMap(targetPv.Owner.ActorNumber)) continue;

            targetPv.RPC(HideOrSeekPlayer.RpcOnGrabbedByOwner, targetPv.Owner, pv.ViewID);
            carriedPlayer = target;
            target.SetLocalCarrierOverride(pv.ViewID);
            // 들고 있는 쿠키(이 클라이언트에서는 원격·키네마틱)가 내 몸을 밀어내지 않도록 서로의 충돌을 무시한다.
            SetCollisionIgnored(gameObject, target.gameObject, true);
            if (self != null) self.SetCarry(CarryReason.Cookie, true);
            return;
        }
    }

    // 들리는 쪽이 거절했다(이미 들렸거나 누군가를 들고 있음 등): RPC 없이 이 화면의 들기만 되돌린다.
    public void OnGrabRejected(int targetViewId)
    {
        if (!HasCarryReference || !carriedPlayer || carriedPlayer.View == null || carriedPlayer.View.ViewID != targetViewId) return;
        carriedPlayer.SetLocalCarrierOverride(-1);
        StartCoroutine(RestoreCollisionLater(carriedPlayer.gameObject));
        carriedPlayer = null;
        if (self != null) self.SetCarry(CarryReason.Cookie, false);
        Debug.Log($"[PlayerGrabController] Grab of view {targetViewId} was rejected.");
    }

    public void Release()
    {
        if (!HasCarryReference) return;

        if (carriedPlayer) // 방을 나가 파괴된 경우에는 RPC/충돌 복구를 건너뛰고, 캐리 상태·자세만 정리한다
        {
            var targetPv = carriedPlayer.View;
            // 내려놓을 자리는 내 화면(드는 사람)이 정한다 — 탈것을 타기 직전 자리 기준(§71 B1·§72 E6)
            Vector3 dropAt = transform.position + transform.forward * PlayerCarryFollower.ReleaseDistance;
            if (targetPv != null && targetPv.Owner != null)
                targetPv.RPC(HideOrSeekPlayer.RpcOnReleased, targetPv.Owner, dropAt);
            carriedPlayer.SetLocalCarrierOverride(-1);
            StartCoroutine(RestoreCollisionLater(carriedPlayer.gameObject));
        }

        carriedPlayer = null;
        if (self != null) self.SetCarry(CarryReason.Cookie, false);
    }

    public void ReleaseAfterBreakFree()
    {
        Release();
        grabBlockedUntil = Time.time + RegrabBlockSeconds;
    }

    private IEnumerator RestoreCollisionLater(GameObject released)
    {
        yield return new WaitForSeconds(RestoreCollisionDelay);
        if (released != null && carriedPlayer == null) // 그 사이 다시 들었다면 무시 상태를 유지한다
            SetCollisionIgnored(gameObject, released, false);
    }

    // 두 캐릭터의 모든 콜라이더 쌍에 대해 충돌 무시 여부를 설정한다(들린 쪽/드는 쪽 양쪽에서 사용).
    public static void SetCollisionIgnored(GameObject a, GameObject b, bool ignore)
    {
        if (a == null || b == null) return;
        var aColliders = a.GetComponentsInChildren<Collider>();
        var bColliders = b.GetComponentsInChildren<Collider>();
        foreach (var ca in aColliders)
            foreach (var cb in bColliders)
                Physics.IgnoreCollision(ca, cb, ignore);
    }
}
