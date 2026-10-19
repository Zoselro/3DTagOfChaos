using Photon.Pun;
using UnityEngine;

// 헬리콥터 모자 비행(Request1009Plan.md §25.5 H, §25.3.1 R6·S3). 쿠키 캐릭터(스파이 포함)마다 붙고, 상태의 Flights에 내 번호가 있으면 난다.
// - 모든 화면: 머리에 무지개 모자 + 도는 프로펠러.
// - 본인 화면: 제자리에서 초당 RiseSpeed로 CruiseHeight까지 떠오른 뒤(이동 잠금, 낮을 때는 괴물이 잡을 수 있다),
//   그 높이에서 가장 가까운 맵 가장자리로 초당 CruiseSpeed로 곧장 날아간다(입력 없음, §26 H8). 맵 경계 + EscapeMargin을 넘으면 방장에게 탈출을 요청한다.
//   위치는 지금 캐릭터 동기화로 다른 화면에 보인다.
public class HatFlight : MonoBehaviour
{
    public const float RiseSpeed = 2f;
    public const float CruiseHeight = 40f;
    public const float CruiseSpeed = 10f;
    public const float EscapeMargin = 10f;
    private const float RequestInterval = 1f;

    private EscapeManager manager;
    private HideOrSeekPlayer cookie;
    private Rigidbody body;
    private Transform hat, propeller;
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
        if (propeller != null && flying) propeller.Rotate(0f, 900f * Time.deltaTime, 0f, Space.Self);
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

    // 머리 위 모자(무지개 원뿔 + 프로펠러). 모델이 생기면 ItemSO 모델로 바꾼다.
    private void SetHat(bool on)
    {
        if (on && hat == null) BuildHat();
        if (hat != null) hat.gameObject.SetActive(on);
    }

    private void BuildHat()
    {
        Transform head = EscapeCue.FindDeep(transform, "Head");
        hat = new GameObject("HeliHat").transform;
        hat.SetParent(head != null ? head : transform, false);
        hat.localPosition = head != null ? Vector3.up * 0.25f : Vector3.up * 2.1f;
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
}
