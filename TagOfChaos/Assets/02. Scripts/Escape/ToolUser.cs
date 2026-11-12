using System.Collections.Generic;
using UnityEngine;

// 손에 든 도구 쓰기(EscapePlan.md §1.8). 맞힌 대상은 쓰는 사람 화면에서 고르고, 방장이 횟수·쿨다운을 확인해
// 모두에게 명중(ToolHit)을 알린다 — 기절·떨어뜨리기·색칠은 맞은 사람 본인이 처리한다(ToolHitReceiver).
// - 뿅망치(§45.5): 두 손으로 휘두르고, 망치가 닿는 순간(0.32초) 전방 5 m·60° 안에서 가장 가까운 대상 한 명.
// - 스턴건: 카메라가 보는 방향 사거리(20 m, §45.6) 안 첫 대상. 우클릭을 누르고 있는 동안만 조준(조준점 + 약간 확대 + 조준 자세)되고, 그때 좌클릭해야 쏜다.
// - 물풍선·섬광탄(§70.3): 화면 가운데가 보는 곳으로 던진다(자동 조준 없음). 손을 떠나면 실제로 날아가 물풍선은 사람 몸에 닿으면 가두고
//   벽·바닥에 먼저 닿으면 그냥 터지며, 섬광탄은 튕기고 구르다 첫 충돌 2초 뒤 터진다. 들고 있는 동안 조준점과 점선 궤적이 보인다.
// - 권투 글러브(§68 GP3): 뿅망치처럼 휘둘러 주먹이 닿는 순간 앞 짧은 부채꼴 안 가장 가까운 쿠키 한 명(술래 제외).
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
        UpdateThrowPreview(held, cam);
        UpdateRecorderTarget(held, cam);
    }

    // ---------------- 던지는 도구 조준(§70.3.6) ----------------
    private readonly ThrowPreview throwPreview = new ThrowPreview();
    private static readonly Color BalloonPreviewColor = new Color(0.45f, 0.95f, 1f, 0.9f);
    private static readonly Color FlashPreviewColor = new Color(1f, 0.95f, 0.6f, 0.9f);
    private const float BalloonSplashRing = 0.5f;

    private void UpdateThrowPreview(ItemSO held, Camera cam)
    {
        bool thrown = held != null && held.IsTool && held.Tool.Projectile != null && !PlayerInput.IsGameplaySuppressed && EscapeManager.ActionsAllowed;
        if (!thrown || cam == null) { throwPreview.Hide(); return; }
        bool balloon = held.Tool.Kind == ToolKind.WaterBalloon;
        throwPreview.Show(cam, ToolShotFx.HandPosition(transform), transform, held.Tool.Projectile,
            balloon ? BalloonSplashRing : held.Tool.HitRadius, balloon ? BalloonPreviewColor : FlashPreviewColor);
    }

    // 손을 떠나는 순간(던지기 동작 0.35초) 그때 보는 곳으로 던진다. 그사이 다른 칸으로 바꿨으면 던지지 않는다.
    private System.Collections.IEnumerator ReleaseThrow(int slot, ItemSO item)
    {
        yield return new WaitForSeconds(PlayerActionPose.ThrowRelease);
        if (inventory == null || inventory.HeldItem != item || ProjectileSystem.Instance == null) yield break;
        ThrowAim aim = ThrowAim.FromCamera(Camera.main, ToolShotFx.HandPosition(transform), transform.forward, item.Tool.Projectile);
        ProjectileSystem.Instance.Throw(item, slot, Random.Range(0, 10), aim, transform);
    }

    // ---------------- 녹음기: 조준한 내 AI(§69 B3) ----------------
    public const float RecorderAimRange = 20f;
    private const float RecorderAimAngle = 12f;
    private BalloonAimPreview recorderMarker;
    private int recorderTarget = -1;
    private static readonly Color RecorderColor = new Color(0.45f, 1f, 0.55f, 0.9f);

    private void UpdateRecorderTarget(ItemSO held, Camera cam)
    {
        bool recorder = held != null && held.IsTool && held.Tool.Kind == ToolKind.Recorder && !PlayerInput.IsGameplaySuppressed && EscapeManager.ActionsAllowed;
        recorderTarget = -1;
        DecoyView picked = recorder ? PickOwnDecoy(cam) : null;
        if (picked == null) { if (recorderMarker != null) recorderMarker.Hide(); return; }
        recorderTarget = picked.Id;
        if (recorderMarker == null) recorderMarker = BalloonAimPreview.Create();
        recorderMarker.ShowRingOnly(picked.transform, RecorderColor);
    }

    // 화면 가운데에 가장 가까운(각도) 내 AI — RecorderAimRange 안, 녹음기가 아직 없고, 잡히지 않은 것.
    private DecoyView PickOwnDecoy(Camera cam)
    {
        EscapeState s = manager != null ? manager.State : null;
        if (s == null || cam == null || Photon.Pun.PhotonNetwork.LocalPlayer == null) return null;
        int me = Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber;
        Vector3 eye = cam.transform.position, fwd = cam.transform.forward;
        DecoyView best = null;
        float bestAngle = RecorderAimAngle;
        foreach (DecoyView v in FindObjectsByType<DecoyView>(FindObjectsSortMode.None))
        {
            int i = s.DecoyIndex(v.Id);
            if (i < 0 || s.Decoys[i].Owner != me || s.Decoys[i].Caught || s.Decoys[i].VoiceAt > 0) continue;
            Vector3 body = v.transform.position + Vector3.up * 1f;
            if (Flat(body - transform.position) > RecorderAimRange) continue;
            float angle = Vector3.Angle(fwd, body - eye);
            if (angle < bestAngle) { bestAngle = angle; best = v; }
        }
        return best;
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
            case ToolKind.BoxingGlove: return SoundId.CookieDodge; // 주먹 휘두르는 소리(임시)
            case ToolKind.StunGun: return SoundId.StunFire;
            case ToolKind.Skates: return SoundId.CookieDodge;   // 임시음(Request1009Plan.md §25.6 소리)
            case ToolKind.Pumpkin: return SoundId.PortalOpen;
            case ToolKind.ColaCannon: return SoundId.PortalOpen;
            case ToolKind.Recorder: return SoundId.UiConfirm;
            case ToolKind.NoiseBomb: return SoundId.UiToastAlert;
            default: return SoundId.BalloonThrow;
        }
    }

    private void OnDestroy()
    {
        aimHum.Stop();
        if (crosshair != null) Destroy(crosshair);
        throwPreview.Destroy();
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
                StartCoroutine(HammerHit(slot, tool, PlayerActionPose.SwingHit, ToolAim.HammerHalfAngle));
                return;
            case ToolKind.BoxingGlove:
                if (pose == null) pose = GetComponent<PlayerActionPose>();
                if (pose != null) pose.Play(PlayerActionPose.OneShot.Swing);
                StartCoroutine(HammerHit(slot, tool, GloveHitDelay, GloveHalfAngle));
                return;
            case ToolKind.Flashbang:
            case ToolKind.WaterBalloon:
                if (tool.Projectile == null) { readyAt = 0f; return; }
                if (pose == null) pose = GetComponent<PlayerActionPose>();
                if (pose != null) pose.Play(PlayerActionPose.OneShot.Throw);
                StartCoroutine(ReleaseThrow(slot, item)); // 대상은 날아가 닿을 때 방장이 정한다(§70.3)
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
                if (!VoiceStore.HasLocal) { EscapeHud.Toast(EscapeTextsSO.Current.noRecording); UiSoundCues.Error(); readyAt = 0f; return; }
                // 조준한 내 AI에 붙인다(§69 B3). 조준한 AI가 없으면 안내만.
                if (recorderTarget < 0) { EscapeHud.Toast(EscapeTextsSO.Current.recorderAim); UiSoundCues.Error(); readyAt = 0f; return; }
                point = transform.position;
                targets.Add(recorderTarget);
                break;
            case ToolKind.Skates:
                point = transform.position; // 나에게 쓰는 아이템 — 대상은 방장이 정한다(§25.5 I)
                break;
            case ToolKind.Pumpkin:
            {
                // 호박 마차 소환(§68.7): 보는 쪽 앞 빈 바닥(막히면 옆·뒤)에 문이 나를 보게 — 방장은 자리만 다시 확인한다.
                // 순간이동할 곳은 탄 뒤 방장이 정한다(맵 안 무작위, §25.3 Q7 그대로).
                if (!PumpkinCarriagePresenter.TryFindSummonSpot(manager, transform.position, aim, out point, out float yaw))
                {
                    EscapeHud.Toast(EscapeTextsSO.Current.carriageNoRoom);
                    UiSoundCues.Error();
                    readyAt = 0f;
                    return;
                }
                manager.Request(EscapeOp.ToolUse, slot, Mathf.RoundToInt(yaw), point, new int[0]);
                return;
            }
            case ToolKind.ColaCannon:
            {
                // 콜라 대포 설치(§68.9): 보는 쪽 앞 빈 바닥에 뒤 계단이 나를 보게. 날아갈 곳은 E로 탈 때 그 방향으로 방장이 정한다.
                if (!ColaCannonPresenter.TryFindSummonSpot(manager, transform.position, aim, out point, out float yaw))
                {
                    EscapeHud.Toast(EscapeTextsSO.Current.cannonNoRoom);
                    UiSoundCues.Error();
                    readyAt = 0f;
                    return;
                }
                manager.Request(EscapeOp.ToolUse, slot, Mathf.RoundToInt(yaw), point, new int[0]);
                return;
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
                CollectInSphere(point, tool.HitRadius, targets, 8, tool.CanHitMonster);
                break;
            }
        }

        int color = Random.Range(0, 10); // 물풍선 색(팔레트 번호, 받는 쪽에서 범위를 확인)
        manager.Request(EscapeOp.ToolUse, slot, color, point, targets.ToArray());
    }

    private const float GloveHitDelay = 0.15f;  // 주먹이 닿는 순간(뿅망치보다 빠름)
    private const float GloveHalfAngle = 45f;

    // 뿅망치·권투 글러브: 휘두른 것이 닿는 순간 전방 부채꼴에서 가장 가까운 한 명을 골라 보낸다(§45.5 HM1, §68 GP3).
    private System.Collections.IEnumerator HammerHit(int slot, ToolSO tool, float delay, float halfAngle)
    {
        yield return new WaitForSeconds(delay);
        Camera cam = Camera.main;
        Vector3 aim = cam != null ? cam.transform.forward : transform.forward;
        Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up);
        if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
        IGameCharacter target = PickTarget(flat, tool.Range, halfAngle, tool.CanHitMonster);
        Vector3 point = target != null ? target.gameObject.transform.position + Vector3.up : transform.position + Vector3.up + flat.normalized * tool.Range * 0.5f;
        manager.Request(EscapeOp.ToolUse, slot, 0, point, target != null ? new[] { target.View.ViewID } : new int[0]);
    }

    // 앞 부채꼴 안의 가장 가까운 캐릭터(나·부서진·나간 사람 제외). 몸 방향이 아니라 카메라가 보는 수평 방향 기준.
    // includeMonsters=false(스턴건이 아닌 도구, §59 AT2)면 괴물을 건너뛰어 그 뒤의 쿠키를 고른다.
    public IGameCharacter PickTarget(Vector3 aim, float range, float halfAngle, bool includeMonsters = true)
    {
        candidates.Clear();
        candidatePoints.Clear();
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.gameObject == gameObject || c.View == null || c.View.Owner == null) continue;
            if (c is HideOrSeekPlayer cookie && cookie.IsBroken) continue;
            if (!includeMonsters && c.Role == CharacterRole.Monster) continue;
            if (EscapeManager.HasLeftMap(c.View.Owner.ActorNumber)) continue;
            candidates.Add(c);
            candidatePoints.Add(c.gameObject.transform.position);
        }
        int i = ToolAim.PickInCone(transform.position, aim, candidatePoints.ToArray(), range, halfAngle);
        return i >= 0 ? candidates[i] : null;
    }

    private readonly List<IGameCharacter> candidates = new List<IGameCharacter>();
    private readonly List<Vector3> candidatePoints = new List<Vector3>();

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    private static RaycastHit[] SortedHits(Ray ray, float radius, float distance)
    {
        RaycastHit[] hits = Physics.SphereCastAll(ray, radius, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits;
    }

    private void CollectInSphere(Vector3 center, float radius, List<int> targets, int max, bool includeMonsters)
    {
        int n = Physics.OverlapSphereNonAlloc(center, radius, overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n && targets.Count < max; i++)
        {
            IGameCharacter c = overlap[i].GetComponentInParent<IGameCharacter>();
            if (c == null || c.gameObject == gameObject || c.View == null || targets.Contains(c.View.ViewID)) continue;
            if (!includeMonsters && c.Role == CharacterRole.Monster) continue;
            targets.Add(c.View.ViewID);
        }
    }
}
