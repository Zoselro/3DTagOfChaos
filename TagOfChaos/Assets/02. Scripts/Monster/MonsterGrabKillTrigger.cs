using Photon.Pun;
using UnityEngine;

// Monster 프리팹에 부착. 괴물이 조준한 쿠키(스파이 포함)를 상호작용 키(E)로 잡는다(GameFixPlan.md F1).
// 예전에는 몸 주변 구 트리거에 쿠키가 닿으면 자동으로 처형했지만, 두 쿠키가 겹쳐 있을 때 누구를 잡을지 괴물이
// 고를 수 있도록 "화면 가운데 조준 + E"로 바꿨다. 촉수 돌진도 더 이상 처형하지 않고 쿠키 앞에서 멈추기만 한다.
// E키 입력은 CharacterInteractor가 받아 문 등 다른 상호작용보다 먼저 여기로 넘긴다(F1-1).
// 맵의 AI(DecoyView)도 진짜 쿠키와 똑같이 조준(빨간 표시) + E로 잡는다 — 잡는 동작·눌림·가루도 같다(§26 H6 후속).
[RequireComponent(typeof(Collider))]
public class MonsterGrabKillTrigger : MonoBehaviour
{
    [SerializeField] private PhotonView monsterPv;
    [SerializeField] private MonsterController monsterController; // GrabKill 애니메이션 트리거용
    [Tooltip("괴물 중심에서 몸 앞면까지의 거리(m). MonsterPlayer 프리팹 측정값 약 1.87m. 잡기 거리는 몸 앞면부터 잰다.")]
    [SerializeField, Min(0f)] private float bodyFrontOffset = 1.87f;

    // 쿨다운은 처형 즉시 시작해 GrabKill 애니메이션 재생이 끝나는 시점까지 지속된다(사용자 확인,
    // GameRule.md v3.5) — ResetTrigger()는 MonsterController가 재생 시간 경과 후 호출한다.
    private bool onCooldown;
    private readonly RaycastHit[] aimHits = new RaycastHit[16];
    private readonly RaycastHit[] sightHits = new RaycastHit[16];
    private Transform eye;

    private const float CookieChestHeight = 1f;
    private const float FallbackEyeHeight = 3.27f; // MonsterPlayer 프리팹 EyeSocket 높이
    private const float HandHeight = 1.6f;          // 잡는 손이 나가는 몸통 높이 — 차양 아래 카운터 위 쿠키는 눈에서는 간판에 가려도 손은 닿는다

    private void Awake()
    {
        eye = monsterPv != null ? monsterPv.transform.Find("EyeSocket") : null;
    }

    public float BodyFrontOffset => bodyFrontOffset;

    // 지금 조준 중인 잡을 수 있는 쿠키 또는 AI(괴물 소유 클라이언트에서만 계산, 없으면 null). 조준 표시 UI가 읽는다.
    public HideOrSeekPlayer AimTarget { get; private set; }
    public DecoyView AimDecoy { get; private set; }
    public Transform AimTransform => AimTarget != null ? AimTarget.transform : AimDecoy != null ? AimDecoy.transform : null;

    private void Update()
    {
        if (monsterPv == null || !monsterPv.IsMine) return;
        AimTarget = null;
        AimDecoy = null;
        if (!onCooldown && monsterController.CanInteract) FindAimTarget();
    }

    // 화면 가운데에서 앞으로 구를 쏘아, 벽보다 가깝고 몸 앞면에서 잡기 거리 안에 있는 가장 가까운 쿠키를 고른다.
    // 3인칭이면 카메라가 괴물 뒤에 있으므로 최대 거리에 카메라~괴물 거리를 더한다.
    // 구조물 위의 쿠키(Request1009Plan.md §11): 올려다보는 조준선은 쿠키보다 구조물 모서리·윗면에 먼저 맞는다. 그래서 막힌 것으로
    // 보이는 쿠키도 괴물 눈 또는 손 높이에서 쿠키 가슴까지 직선이 비어 있으면 잡을 수 있다. 높이는 MaxGrabHeight까지.
    private void FindAimTarget()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        GameSettingsSO settings = GameSettings.Current;
        Transform self = monsterPv.transform;
        float reachFromCenter = bodyFrontOffset + settings.GrabReachFromFront;
        float cameraToMonster = Vector3.Distance(cam.transform.position, self.position);
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        int count = Physics.SphereCastNonAlloc(ray, settings.GrabAimRadius, aimHits, cameraToMonster + reachFromCenter,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // 벽 등 막는 물체가 있으면 그 뒤의 쿠키는 잡을 수 없다.
        float blockDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider c = aimHits[i].collider;
            if (c.transform.IsChildOf(self) || c.GetComponentInParent<HideOrSeekPlayer>() != null || c.GetComponentInParent<DecoyView>() != null) continue;
            if (aimHits[i].distance > 0f) blockDistance = Mathf.Min(blockDistance, aimHits[i].distance);
        }

        float bestDistance = float.MaxValue;
        Vector3 center = self.position;
        for (int i = 0; i < count; i++)
        {
            if (aimHits[i].distance >= bestDistance) continue;
            Collider hit = aimHits[i].collider;
            var cookie = hit.GetComponentInParent<HideOrSeekPlayer>();
            DecoyView decoy = cookie == null ? hit.GetComponentInParent<DecoyView>() : null;
            Transform body = cookie != null ? cookie.transform : decoy != null ? decoy.transform : null;
            if (body == null) continue;
            if (cookie != null ? !IsCatchable(cookie) : !decoy.IsCatchable) continue;
            if (!InReach(center, body.position, reachFromCenter, settings.MaxGrabHeight)) continue; // 몸 앞면에서 잡기 거리 밖·너무 높음
            if (aimHits[i].distance > blockDistance && !HasLineOfSight(self, body)) continue;

            AimTarget = cookie;
            AimDecoy = decoy;
            bestDistance = aimHits[i].distance;
        }
    }

    // 수평 거리는 몸 중심에서 reach 안, 높이는 쿠키 발이 괴물 발보다 maxHeight 이하(아래쪽은 제한 없음 — 경사·계단 아래).
    public static bool InReach(Vector3 monsterFeet, Vector3 cookieFeet, float reachFromCenter, float maxHeight)
    {
        Vector3 flat = cookieFeet - monsterFeet;
        float rise = flat.y;
        flat.y = 0f;
        return flat.sqrMagnitude <= reachFromCenter * reachFromCenter && rise <= maxHeight;
    }

    private bool HasLineOfSight(Transform self, Transform body)
    {
        Vector3 to = body.position + Vector3.up * CookieChestHeight;
        Vector3 eyePoint = eye != null ? eye.position : self.position + Vector3.up * FallbackEyeHeight;
        return LineClear(eyePoint, to, self, body) || LineClear(self.position + Vector3.up * HandHeight, to, self, body);
    }

    private bool LineClear(Vector3 from, Vector3 to, Transform self, Transform cookie)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f) return true;
        int count = Physics.RaycastNonAlloc(from, delta / length, sightHits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform t = sightHits[i].collider.transform;
            if (t.IsChildOf(self) || t.IsChildOf(cookie)) continue;
            return false;
        }
        return true;
    }

    private static bool IsCatchable(HideOrSeekPlayer cookie)
    {
        PhotonView view = cookie.View;
        if (view == null || view.Owner == null || RoomState.IsBroken(view.Owner) || cookie.IsBroken) return false;
        // 감옥 풍선 안의 쿠키는 잡지 못한다(Request1009Plan.md §25.5 J). 헬리콥터 모자로 높이 오른 쿠키는 잡기 높이 검사가 막는다.
        return !cookie.TryGetComponent(out StunReceiver stun) || !stun.IsJailed;
    }

    // 상호작용 키로 조준 대상을 잡는다. 잡기를 시작했으면 true(CharacterInteractor가 문 등보다 먼저 호출, F1-1).
    public bool TryGrabAimTarget()
    {
        if (!monsterController.CanInteract) return false;
        if (AimDecoy != null) return TryGrabDecoy(AimDecoy);
        HideOrSeekPlayer target = AimTarget;
        return target != null && TryGrabKill(target);
    }

    // AI 잡기: 진짜 쿠키와 같은 잡는 동작을 시작하고 방장에게 알린다. 방장이 잡힘(CaughtBy)을 쓰면 모든 화면의 DecoyView가
    // 이 괴물의 손을 따라가 눌리고 가루가 되어 사라진다.
    private bool TryGrabDecoy(DecoyView decoy)
    {
        if (onCooldown || decoy == null || !decoy.IsCatchable || !monsterPv.IsMine) return false;
        onCooldown = true;
        monsterController.PlayGrabKill();
        decoy.RequestCatch();
        return true;
    }

    // 처형 시작의 단일 진입점. 처형을 시작했으면 true.
    public bool TryGrabKill(HideOrSeekPlayer cookie)
    {
        if (onCooldown || cookie == null) return false;
        if (!monsterPv.IsMine) return false; // 판정 시도는 괴물 소유 클라이언트만

        var cookiePv = cookie.GetComponent<PhotonView>();
        if (cookiePv == null || cookiePv.Owner == null) return false;

        // 이미 파괴된 쿠키(렌더러만 꺼진 시체)가 쿨다운을 소모하지 않도록 제외한다(research.md §8.11).
        if (RoomState.IsBroken(cookiePv.Owner)) return false;

        onCooldown = true;
        monsterController.PlayGrabKill(); // 상태 동기화로 전원에게 GrabKill 애니메이션 전파(진행 중인 돌진은 여기서 끊긴다)

        // 전원에게 보낸다: 모든 클라이언트가 쿠키를 이 괴물의 Grab_Socket에 붙여 처형 연출을 보여야 하므로 괴물 ViewID를 함께 넘긴다.
        // 파괴 확정(HitCount)은 여전히 피해자 본인 클라이언트만 한다(소유권 원칙, Bug-fix-plan.md §36).
        cookiePv.RPC(HideOrSeekPlayer.RpcRequestGrabKill, RpcTarget.All, monsterPv.ViewID);
        // 파괴·잡은 횟수는 방장이 거리·단계를 검증한 뒤 쓴다(GrabKillAuthority, AuditFixPlan.md §8.1).
        return true;
    }

    // GrabKill 애니메이션 재생이 끝나는 시점에 MonsterController가 호출한다(확정, GameRule.md v3.5).
    public void ResetTrigger() => onCooldown = false;
}
