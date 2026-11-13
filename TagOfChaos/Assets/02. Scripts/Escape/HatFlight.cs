using Photon.Pun;
using UnityEngine;

// 헬리콥터 모자 비행(Request1009Plan.md §25.5 H, §25.3.1 R6·S3). 쿠키 캐릭터(스파이 포함)마다 붙고, 상태의 Flights에 내 번호가 있으면 난다.
// - 모든 화면: 머리에 무지개 모자 + 도는 프로펠러.
// - 본인 화면: 제자리에서 초당 RiseSpeed로 CruiseHeight까지 떠오른 뒤(이동 잠금, 낮을 때는 괴물이 잡을 수 있다),
//   그 높이에서 가장 가까운 맵 가장자리로 초당 CruiseSpeed로 곧장 날아간다(입력 없음, §26 H8). 맵 경계 + EscapeMargin을 넘으면 방장에게 탈출을 요청한다.
//   위치는 지금 캐릭터 동기화로 다른 화면에 보인다.
public class HatFlight : MonoBehaviour
{
    public const float RiseSpeed = 5f;      // 2 → 5 m/s: 40 m까지 8초(§69 B7 — 사용자: 더 빠르게)
    public const float CruiseHeight = 40f;
    public const float CruiseSpeed = 18f;   // 10 → 18 m/s
    public const float EscapeMargin = 10f;
    private const float RequestInterval = 1f;

    private EscapeManager manager;
    private HideOrSeekPlayer cookie;
    private Rigidbody body;
    private Transform hat, propeller;
    private Vector3 spinAxis = Vector3.up; // 프로펠러 자기 좌표에서 모자 위쪽 방향(모델은 Blender Z-up이라 Y가 아닐 수 있다)
    private const float HeadTopOffset = 0.30f; // 정수리 뼈가 없을 때: 머리뼈에서 모자 자리까지
    private const float HatSink = 0.12f;       // 정수리에서 모자를 내려 씌우는 깊이
    private const float HatModelScale = 1.8f; // 손에 드는 모델(폭 0.28 m)을 머리에 맞는 크기로
    private bool flying;
    private float startY;
    private float nextRequest;
    private Vector3 cruiseDirection;

    public bool IsFlying => flying;

    public void Init(EscapeManager escape, HideOrSeekPlayer owner, Rigidbody rb)
    {
        manager = escape;
        cookie = owner;
        body = rb;
    }

    private int Actor => cookie != null && cookie.View != null && cookie.View.Owner != null ? cookie.View.Owner.ActorNumber : -1;

    private void Update()
    {
        EscapeState s = manager != null ? manager.State : null;
        int f = s != null ? s.FlightIndex(Actor) : -1;
        bool nowFlying = f >= 0;
        if (nowFlying != flying)
        {
            flying = nowFlying;
            SetHat(flying);
            if (flying) { startY = transform.position.y; cruiseDirection = Vector3.zero; }
            if (cookie.IsMine) SetPhysics(!flying);
        }
        if (propeller != null && flying) propeller.Rotate(spinAxis, 900f * Time.deltaTime, Space.Self);
        if (!flying || !cookie.IsMine) return;

        float since = (float)(PhotonNetwork.Time - s.Flights[f].StartAt);
        float y = startY + Mathf.Min(CruiseHeight, since * RiseSpeed);
        Vector3 p = transform.position;
        if (since * RiseSpeed >= CruiseHeight)
        {
            if (cruiseDirection == Vector3.zero) cruiseDirection = manager.Area.EdgeDirection(p);
            p += cruiseDirection * CruiseSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(cruiseDirection);
        }
        p.y = y;
        body.position = p;
        transform.position = p;

        if (manager.IsOutsideMap(p, EscapeMargin) && Time.unscaledTime >= nextRequest)
        {
            nextRequest = Time.unscaledTime + RequestInterval;
            manager.Request(EscapeOp.HatEscape);
        }
    }

    // 맵(x·z 사각형)에서 이 점과 가장 가까운 가장자리로 향하는 수평 방향(순수 계산, 시험용). 원 맵은 MapArea.EdgeDirection.
    public static Vector3 NearestEdgeDirection(Rect map, Vector3 p) => MapArea.RectEdgeDirection(map, p);

    // 나는 동안은 중력·충돌 없이(키네마틱) 직접 옮긴다. 끝나면(잡힘 등) 되돌린다.
    private void SetPhysics(bool on)
    {
        if (body == null) return;
        if (!on)
        {
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        else if (!EscapeManager.HasLeftMap(Actor))
        {
            body.isKinematic = false;
        }
    }

    // 머리 위 모자: 손에 드는 것과 같은 Blender 모델(ITEM_HeliHat)을 씌우고 Propeller 노드를 돌린다(§74 P14). 모델이 없으면 기본 도형.
    private void SetHat(bool on)
    {
        if (on && hat == null) BuildHat();
        if (hat != null) hat.gameObject.SetActive(on);
    }

    private void BuildHat()
    {
        // 머리뼈(Humanoid Head — 쿠키 뼈 이름은 mixamorig:Head라 예전 "Head" 찾기는 늘 실패해 몸 기준 2.1 m에 떠 있었다, §74 P14).
        Animator animator = GetComponentInChildren<Animator>();
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        hat = new GameObject("HeliHat").transform;
        hat.SetParent(head != null ? head : transform, false);
        if (head != null)
        {
            // 뼈의 크기·방향과 상관없이 세상 기준으로 맞춘다: 머리뼈 위 HeadTopOffset, 몸 방향, 크기 1.
            // 정수리 뼈(HeadTop_End)가 있으면 그 조금 아래(모자가 머리를 덮게), 없으면 머리뼈 위 HeadTopOffset.
            Transform top = null;
            foreach (Transform c in head) if (c.name.EndsWith("HeadTop_End")) top = c;
            Vector3 at = top != null ? top.position - transform.up * HatSink : head.position + transform.up * HeadTopOffset;
            hat.SetPositionAndRotation(at, transform.rotation);
            Vector3 ls = head.lossyScale;
            hat.localScale = new Vector3(1f / Mathf.Max(1e-4f, ls.x), 1f / Mathf.Max(1e-4f, ls.y), 1f / Mathf.Max(1e-4f, ls.z));
        }
        else hat.localPosition = Vector3.up * 2.1f;
        if (BuildModelHat()) return;
        var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(cap.GetComponent<Collider>());
        cap.transform.SetParent(hat, false);
        cap.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
        EscapeVisuals.Tint(cap, new Color(1f, 0.45f, 0.75f), 0.6f);
        propeller = new GameObject("Propeller").transform;
        propeller.SetParent(hat, false);
        propeller.localPosition = Vector3.up * 0.25f;
        for (int i = 0; i < 2; i++)
        {
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(blade.GetComponent<Collider>());
            blade.transform.SetParent(propeller, false);
            blade.transform.localScale = new Vector3(1.1f, 0.03f, 0.12f);
            blade.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            EscapeVisuals.Tint(blade, i == 0 ? new Color(0.3f, 0.8f, 1f) : new Color(1f, 0.9f, 0.2f), 0.5f);
        }
    }

    // 아이템 모델로 모자를 만든다. 모델이 없으면 false(기본 도형으로).
    private bool BuildModelHat()
    {
        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        ItemSO def = catalog != null ? catalog.HeliHat : null;
        if (def == null || def.ModelPrefab == null) return false;
        GameObject model = EscapeVisuals.CreateItemModel(def, hat);
        model.transform.localScale *= HatModelScale;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        propeller = EscapeCue.FindDeep(model.transform, "Propeller");
        if (propeller == null) return true; // 프로펠러 노드가 없는 옛 모델: 돌지 않을 뿐
        spinAxis = propeller.InverseTransformDirection(hat.up).normalized;
        return true;
    }
}
