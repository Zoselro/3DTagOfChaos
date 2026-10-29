using System.Collections.Generic;
using UnityEngine;

// 손에 든 도구 쓰기(EscapePlan.md §1.8). 맞힌 대상은 쓰는 사람 화면에서 고르고, 방장이 횟수·쿨다운을 확인해
// 모두에게 명중(ToolHit)을 알린다 — 기절·떨어뜨리기·색칠은 맞은 사람 본인이 처리한다(ToolHitReceiver).
// - 뿅망치(§45.5): 두 손으로 휘두르고, 망치가 닿는 순간(0.32초) 전방 5 m·60° 안에서 가장 가까운 대상 한 명.
// - 스턴건: 카메라가 보는 방향 사거리(20 m, §45.6) 안 첫 대상. 우클릭을 누르고 있는 동안만 조준(조준점 + 약간 확대 + 조준 자세)되고, 그때 좌클릭해야 쏜다.
// - 물풍선(§45.7): 전방 10 m·45° 안에서 가장 가까운 대상 한 명을 향해 위로 던진다(포물선 높이는 거리로 그때그때 계산). 들고 있는 동안
//   대상 표시와 예상 궤적이 보인다(BalloonAimPreview). 대상이 없으면 보는 곳 앞 땅에 떨어진다.
// 괴물은 충돌 캡슐(반지름 약 0.31 m)이 몸 겉모습보다 훨씬 작으므로, 몸 크기의 트리거(잡기 범위 구)도 맞힘 판정에 넣는다.
public class ToolUser : MonoBehaviour
{
    private PlayerInventory inventory;
    private EscapeManager manager;
    private float readyAt;
    private readonly Collider[] overlap = new Collider[16];
    private const float AimZoom = 0.72f;          // 조준 중 시야각 배율(약간 확대)
    private const float AimZoomSpeed = 8f;
    private float baseFov = -1f;
    private float zoom = 1f;
    private GameObject crosshair;

    // 로컬 플레이어가 스턴건을 조준하는 중인지.
    public static bool Aiming { get; private set; }

    private AudioHandle aimHum;
    private PlayerActionPose pose;

    public void Init(PlayerInventory owner, EscapeManager escape)
    {
        inventory = owner;
        manager = escape;
    }

    private void Update()
    {
        if (inventory == null || PlayerInventory.Local != inventory) return;
        ItemSO held = inventory.HeldItem;
        bool canAim = held != null && held.IsTool && held.Tool.Kind == ToolKind.StunGun
                      && !PlayerInput.IsGameplaySuppressed && EscapeManager.ActionsAllowed;
        Aiming = canAim && PlayerInput.CameraRotateHeld; // 우클릭(시점 회전 버튼)을 누르고 있는 동안
        if (pose == null) pose = GetComponent<PlayerActionPose>();
        if (pose != null) pose.SetAiming(Aiming); // 조준 자세(§45.4 AN0)
        Camera cam = Camera.main;
        if (cam != null)
        {
            if (baseFov < 0f) baseFov = cam.fieldOfView;
            zoom = Mathf.MoveTowards(zoom, Aiming ? AimZoom : 1f, AimZoomSpeed * Time.deltaTime);
            cam.fieldOfView = baseFov * zoom;
        }
        UpdateAimHum(Aiming);
        if (Aiming && crosshair == null) crosshair = CreateCrosshair();
        if (crosshair != null && crosshair.activeSelf != Aiming) crosshair.SetActive(Aiming);
        UpdateBalloonPreview(held, cam);
    }

    // 물풍선을 들고 있으면 맞을 대상·예상 궤적(§45.7 WB0).
    private BalloonAimPreview preview;

    private void UpdateBalloonPreview(ItemSO held, Camera cam)
    {
        bool balloon = held != null && held.IsTool && (held.Tool.Kind == ToolKind.WaterBalloon || held.Tool.Kind == ToolKind.JailBalloon)
                       && !PlayerInput.IsGameplaySuppressed && EscapeManager.ActionsAllowed;
        if (!balloon) { if (preview != null) preview.Hide(); return; }
        if (preview == null) preview = BalloonAimPreview.Create();
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Vector3 aim = cam != null ? cam.transform.forward : transform.forward;
        IGameCharacter target = PickTarget(aim, held.Tool.Range, ToolAim.BalloonHalfAngle);
        Vector3 from = ToolShotFx.HandPosition(transform);
        Vector3 to = target != null ? WaterBubbleView.BodyBounds(target.gameObject.transform).center : GroundAhead(cam, origin, aim, held.Tool.Range);
        preview.Show(from, to, target != null ? target.gameObject.transform : null);
    }

    // 스턴건을 조준하는 동안 윙 소리 반복(본인 2D, S5).
    private void UpdateAimHum(bool aiming)
    {
        if (aiming && !aimHum.IsPlaying) aimHum = GameAudio.StartLoop(SoundId.StunAimHum, null);
        else if (!aiming && aimHum.IsPlaying) { aimHum.Stop(); aimHum = default; }
    }

    // 도구를 쓰는 순간의 소리(던지기·발사·휘두르기). ToolHitReceiver도 다른 사람 화면에서 같은 소리를 쓴다.
    public static SoundId UseSound(ToolKind kind)
    {
        switch (kind)
        {
            case ToolKind.Hammer: return SoundId.HammerSwing;
            case ToolKind.StunGun: return SoundId.StunFire;
            case ToolKind.Skates: return SoundId.CookieDodge;   // 임시음(Request1009Plan.md §25.6 소리)
            case ToolKind.Pumpkin: return SoundId.PortalOpen;
            case ToolKind.Recorder: return SoundId.UiConfirm;
            case ToolKind.NoiseBomb: return SoundId.UiToastAlert;
            default: return SoundId.BalloonThrow;
        }
    }

    private void OnDestroy()
    {
        aimHum.Stop();
        if (crosshair != null) Destroy(crosshair);
        if (preview != null) Destroy(preview.gameObject);
        Camera cam = Camera.main;
        if (cam != null && baseFov > 0f) cam.fieldOfView = baseFov;
        Aiming = false;
    }

    // 화면 가운데 조준점(작은 점 + 네 갈래 선). 조준하는 동안만 보인다.
    private static GameObject CreateCrosshair()
    {
        var root = new GameObject("StunGunCrosshair", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var color = new Color(0.55f, 1f, 0.95f, 0.95f);
        void Bar(Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = color;
            img.raycastTarget = false;
        }
        Bar(Vector2.zero, new Vector2(5f, 5f));
        Bar(new Vector2(0f, 16f), new Vector2(3f, 12f));
        Bar(new Vector2(0f, -16f), new Vector2(3f, 12f));
        Bar(new Vector2(16f, 0f), new Vector2(12f, 3f));
        Bar(new Vector2(-16f, 0f), new Vector2(12f, 3f));
        root.SetActive(false);
        return root;
    }

    public void Use(int slot, ItemSO item)
    {
        if (Time.time < readyAt || item == null || !item.IsTool || !item.Tool.UsedByClick) return;
        ToolSO tool = item.Tool;
        if (tool.Kind == ToolKind.StunGun && !Aiming) return; // 스턴건은 우클릭으로 조준한 상태에서만 쏜다
        readyAt = Time.time + tool.CooldownSeconds;
        GameAudio.Play(UseSound(tool.Kind)); // 쓴 사람은 누르는 순간 바로(2D). 다른 사람은 승인된 ToolHit에서 3D로 듣는다(S5)

        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Camera cam = Camera.main;
        Vector3 aim = cam != null ? cam.transform.forward : transform.forward;
        var targets = new List<int>();
        Vector3 point;

        switch (tool.Kind)
        {
            case ToolKind.Hammer:
                // 휘두르기 동작을 먼저 하고, 망치가 닿는 순간에 대상을 골라 보낸다
                if (pose == null) pose = GetComponent<PlayerActionPose>();
                if (pose != null) pose.Play(PlayerActionPose.OneShot.Swing);
                StartCoroutine(HammerHit(slot, tool));
                return;
            case ToolKind.StunGun:
            {
                Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(origin, aim);
                float camOffset = cam != null ? Vector3.Distance(cam.transform.position, origin) : 0f;
                point = ray.origin + ray.direction * (tool.Range + camOffset);
                foreach (RaycastHit hit in SortedHits(ray, tool.HitRadius, tool.Range + camOffset))
                {
                    IGameCharacter c = hit.collider.GetComponentInParent<IGameCharacter>();
                    if (c != null && c.gameObject == gameObject) continue; // 자기 몸은 지나친다
                    if (c == null && hit.collider.isTrigger) continue;      // 캐릭터가 아닌 트리거는 무시
                    point = hit.point;
                    // 전방 20 m 안에 있는 대상만(몸 겉면이 아니라 몸 중심까지의 수평 거리, §45.6) — 큰 괴물 몸 겉면이 먼저 닿아도 멀면 빗나간다
                    if (c != null && c.View != null && Flat(c.gameObject.transform.position - transform.position) <= tool.Range) targets.Add(c.View.ViewID);
                    break;                                                  // 처음 맞은 것(벽 포함)에서 멈춘다
                }
                break;
            }
            case ToolKind.Recorder:
                // 녹음한 목소리가 없으면 쓸 수 없다(§26 H7 — 방장도 HasVoice로 다시 확인한다)
                if (!VoiceStore.HasLocal) { EscapeHud.Toast(EscapeTextsSO.Current.noRecording); UiSoundCues.Error(); return; }
                point = transform.position;
                break;
            case ToolKind.Skates:
                point = transform.position; // 나에게 쓰는 아이템 — 대상은 방장이 정한다(§25.5 I)
                break;
            case ToolKind.Pumpkin:
            {
                // 맵 안 무작위 지점(바닥 있고 끼지 않는 곳, §25.3 Q7) — 방장은 그 점이 맞는지만 확인한다
                if (!manager.TryRandomMapPoint(new System.Random(), out point)) return;
                break;
            }
            case ToolKind.WaterBalloon:
            case ToolKind.JailBalloon:
            {
                if (pose == null) pose = GetComponent<PlayerActionPose>();
                if (pose != null) pose.Play(PlayerActionPose.OneShot.Throw);
                IGameCharacter target = PickTarget(aim, tool.Range, ToolAim.BalloonHalfAngle);
                if (target != null)
                {
                    point = target.gameObject.transform.position;
                    targets.Add(target.View.ViewID);
                }
                else point = GroundAhead(cam, origin, aim, tool.Range);
                break;
            }
            default:
            {
                Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(origin, aim);
                float camOffset = cam != null ? Vector3.Distance(cam.transform.position, origin) : 0f;
                point = Physics.Raycast(ray, out RaycastHit hit, tool.Range + camOffset, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    ? hit.point : ray.origin + ray.direction * (tool.Range + camOffset);
                // 앞쪽 사거리(10 m) 안에서 땅에 떨어진다: 너무 멀면 당기고, 허공이면 그 아래 땅으로
                Vector3 flat = point - origin;
                flat.y = 0f;
                if (flat.magnitude > tool.Range) point = new Vector3(origin.x, point.y, origin.z) + flat.normalized * tool.Range;
                if (Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && ground.point.y < point.y + 0.1f)
                    point = ground.point;
                CollectInSphere(point, tool.HitRadius, targets, tool.Kind == ToolKind.JailBalloon ? 1 : 8);
                break;
            }
        }

        int color = Random.Range(0, 10); // 물풍선 색(팔레트 번호, 받는 쪽에서 범위를 확인)
        manager.Request(EscapeOp.ToolUse, slot, color, point, targets.ToArray());
    }

    // 뿅망치: 망치가 닿는 순간 전방 부채꼴에서 가장 가까운 한 명을 골라 보낸다(§45.5 HM1).
    private System.Collections.IEnumerator HammerHit(int slot, ToolSO tool)
    {
        yield return new WaitForSeconds(PlayerActionPose.SwingHit);
        Camera cam = Camera.main;
        Vector3 aim = cam != null ? cam.transform.forward : transform.forward;
        Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up);
        if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
        IGameCharacter target = PickTarget(flat, tool.Range, ToolAim.HammerHalfAngle);
        Vector3 point = target != null ? target.gameObject.transform.position + Vector3.up : transform.position + Vector3.up + flat.normalized * tool.Range * 0.5f;
        manager.Request(EscapeOp.ToolUse, slot, 0, point, target != null ? new[] { target.View.ViewID } : new int[0]);
    }

    // 앞 부채꼴 안의 가장 가까운 캐릭터(나·부서진·나간 사람 제외). 몸 방향이 아니라 카메라가 보는 수평 방향 기준.
    public IGameCharacter PickTarget(Vector3 aim, float range, float halfAngle)
    {
        candidates.Clear();
        candidatePoints.Clear();
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.gameObject == gameObject || c.View == null || c.View.Owner == null) continue;
            if (c is HideOrSeekPlayer cookie && cookie.IsBroken) continue;
            if (EscapeManager.HasLeftMap(c.View.Owner.ActorNumber)) continue;
            candidates.Add(c);
            candidatePoints.Add(c.gameObject.transform.position);
        }
        int i = ToolAim.PickInCone(transform.position, aim, candidatePoints.ToArray(), range, halfAngle);
        return i >= 0 ? candidates[i] : null;
    }

    private readonly List<IGameCharacter> candidates = new List<IGameCharacter>();
    private readonly List<Vector3> candidatePoints = new List<Vector3>();

    // 대상이 없을 때 떨어질 곳: 보는 곳(사거리 안)의 땅.
    private static Vector3 GroundAhead(Camera cam, Vector3 origin, Vector3 aim, float range)
    {
        Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(origin, aim);
        float camOffset = cam != null ? Vector3.Distance(cam.transform.position, origin) : 0f;
        Vector3 point = Physics.Raycast(ray, out RaycastHit hit, range + camOffset, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point : ray.origin + ray.direction * (range + camOffset);
        Vector3 flat = point - origin;
        flat.y = 0f;
        if (flat.magnitude > range) point = new Vector3(origin.x, point.y, origin.z) + flat.normalized * range;
        if (Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && ground.point.y < point.y + 0.1f)
            point = ground.point;
        return point;
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    private static RaycastHit[] SortedHits(Ray ray, float radius, float distance)
    {
        RaycastHit[] hits = Physics.SphereCastAll(ray, radius, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits;
    }

    private void CollectInSphere(Vector3 center, float radius, List<int> targets, int max)
    {
        int n = Physics.OverlapSphereNonAlloc(center, radius, overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n && targets.Count < max; i++)
        {
            IGameCharacter c = overlap[i].GetComponentInParent<IGameCharacter>();
            if (c == null || c.gameObject == gameObject || c.View == null || targets.Contains(c.View.ViewID)) continue;
            targets.Add(c.View.ViewID);
        }
    }
}
