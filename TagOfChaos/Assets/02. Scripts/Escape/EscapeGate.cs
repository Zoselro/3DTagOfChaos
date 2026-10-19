using Photon.Pun;
using UnityEngine;

// 맵 가장자리 탈출구 하나(Request1009Plan.md §25.5 F). 테스트 맵의 Exit_N/E/S/W(문 Door_*, 바깥 트리거 자리 ExitTrigger_*)에 붙는다.
// 모든 건물이 가동되면 방장이 4개 중 2개를 연다 — 열린 문은 땅으로 내려가고, 쿠키가 트리거 자리로 걸어 들어가면 탈출(방장이 거리 확인).
// 열린 탈출구는 쿠키·괴물에게 적외선 투시처럼 벽 너머로도 보인다(XRayMarker). 스파이는 탈출구로 나갈 수 없다(§25.3.1 R10).
// 캔디숲 실제 모델(§28.11.3 F2): 닫힌 문(회색 돌 문 Door_)이 땅으로 내려가면 열린 모습(초록 문틀 ExitOpenLook_)이 켜진다.
public class EscapeGate : MonoBehaviour
{
    public const float ExitTouchRadius = 3.5f;
    private const float DoorDropDepth = 6.6f;
    private const float DoorOpenSeconds = 1.5f;
    private const float RequestInterval = 1f;

    private EscapeManager manager;
    private int index = -1;
    private Transform door;
    private Vector3 doorClosed;
    private float doorDrop = DoorDropDepth;
    private GameObject openLook;
    private Transform trigger;
    private XRayMarker xray;
    private float nextRequest;

    public int Index => index;
    public Vector3 TriggerPoint => trigger != null ? trigger.position : transform.position;

    public void Bind(EscapeManager owner, int exitIndex)
    {
        manager = owner;
        index = exitIndex;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Door_")) door = t;
            else if (t.name.StartsWith("ExitTrigger_")) trigger = t;
            else if (t.name.StartsWith("ExitOpenLook_")) openLook = t.gameObject;
        }
        if (door != null)
        {
            doorClosed = door.localPosition;
            // 문 모델이 높으면 끝까지 내려가도록(문 높이 + 여유, 지금 맵 문은 6.6 m 그대로)
            float height = 0f;
            foreach (Renderer r in door.GetComponentsInChildren<Renderer>(true)) height = Mathf.Max(height, r.bounds.size.y);
            doorDrop = Mathf.Max(DoorDropDepth, height + 0.3f);
        }
        if (openLook != null) openLook.SetActive(false);
        xray = gameObject.AddComponent<XRayMarker>();
        xray.Init(new Color(0.35f, 0.85f, 1f));
    }

    public void Refresh() => UpdateVisuals();

    private bool IsOpen => manager != null && manager.State != null && manager.State.ExitOpen(index);

    private void Update()
    {
        UpdateVisuals();
        if (!IsOpen || Time.unscaledTime < nextRequest) return;
        // 내 쿠키(스파이 제외)가 열린 탈출구 트리거에 닿으면 탈출을 요청한다
        if (RoomState.IsLocalSpy() || RoomState.IsLocalMonster()) return;
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (c.Role != CharacterRole.Cookie || c.View == null || !c.View.IsMine || !c.CanInteract) continue;
            Vector3 d = c.gameObject.transform.position - TriggerPoint;
            d.y = 0f;
            if (d.magnitude > ExitTouchRadius) continue;
            nextRequest = Time.unscaledTime + RequestInterval;
            manager.Request(EscapeOp.Exit, index);
        }
    }

    private void UpdateVisuals()
    {
        bool open = IsOpen;
        if (door != null)
        {
            float t = open && manager.State.ExitsOpenedAt > 0 ? Mathf.Clamp01((float)(PhotonNetwork.Time - manager.State.ExitsOpenedAt) / DoorOpenSeconds) : 0f;
            Vector3 target = doorClosed + Vector3.down * (doorDrop * Mathf.SmoothStep(0f, 1f, t));
            if ((door.localPosition - target).sqrMagnitude > 1e-6f) door.localPosition = target;
            var col = door.GetComponent<Collider>();
            if (col != null && col.enabled == open) col.enabled = !open;
        }
        if (openLook != null && openLook.activeSelf != open) openLook.SetActive(open);
        // 열린 탈출구는 쿠키(스파이 제외)·괴물에게 벽 너머로도 보인다
        bool seeThrough = open && !RoomState.IsLocalSpy();
        xray.SetVisible(seeThrough);
    }
}
