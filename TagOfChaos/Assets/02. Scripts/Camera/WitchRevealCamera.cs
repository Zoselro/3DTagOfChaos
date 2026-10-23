using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 마녀 연출 카메라(Request1009Plan.md §18.4, §19.2 → §23.5 X5). 장면은 시각표가 정한다(WitchTimeline.ShotAt) — 모두가 같은 순간 같은 장면.
//   타임어택: 로켓(3초) → 등진 마녀 전신으로(3초) → 전신을 본 채 뒤돌아섬(2초) → 얼굴 클로즈업(2초) → 내 시점으로(3초)
//             … 타임어택 동안 각자 플레이 … 타임어택 끝(마녀 도착) → 도착 3테이크
//   도착 3테이크(§24.10): 검은 화면 → 땅 높이에서 쿠키·괴물 대역 → 로우 앵글로 뒤로 빠지며 마녀 얼굴(6초) → 대역 뒤 땅에서 올려다보는
//             밑창(4초, 끝에 검게) → 검은 화면 → 정면 위 박살(결과까지). 화각도 장면마다 정하고 끝나면 되돌린다.
// Main Camera에 붙고, 다른 카메라 스크립트(3인칭 Camera_Ctrl·괴물 1인칭·관전)가 이번 프레임 위치를 정한 뒤에 실행해(DefaultExecutionOrder)
// 그 위치(내 시점)와 마녀 쪽 자리를 섞는다 — 내 시점은 계속 계산되므로 돌아오는 곳이 정확하다. 장면이 있는 동안 조작은 멈춘다
// (PlayerInput.CutsceneActive, 타임어택은 그만큼 길다 — WitchTimeline.TimeAttackEnd). 공통 시계라 늦게 들어온 사람은 지난 부분을 건너뛴다.
[DefaultExecutionOrder(1000)]
public class WitchRevealCamera : MonoBehaviour
{
    public const float FaceDistanceRatio = 0.28f;  // 얼굴 앞 거리 = 마녀 키 × 비율(키 90 m → 25 m, 모자 챙까지 얼굴이 화면을 채움)
    public const float FaceRiseRatio = 0.01f;      // 눈높이를 살짝 올려 모자 챙 아래 얼굴이 보이게
    public const float LeadDistance = 14f;         // 앞 샷: 대상(로켓·탈것)에서 떨어진 거리(m)
    public const float LeadHeight = 4f;            // 앞 샷: 대상 바닥보다 높이(m)
    public const float LeadFollowUp = 0.35f;       // 앞 샷: 대상이 떠오르면 카메라도 그 비율만큼 따라 오른다(로켓)
    public const float FullBodyDistanceRatio = 1.25f, FullBodyHeightRatio = 0.25f, FullBodyLookRatio = 0.5f; // 전신: 맵 쪽에서 몸 전체가 화면에
    private const float StrikeShakeScale = 4f;     // 멀리서 보는 장면이라 충격 흔들림을 크게(CameraShake, 키 90 m 기준 — 키에 비례)
    // 도착 3테이크(§24.10, 스토리보드 v10)
    public const float LowStartDistance = 12f;     // 테이크 1: 대역 앞 거리(m)에서 시작
    public const float LowEndDistanceRatio = 0.49f;//   뒤로 빠지는 거리 = 키 × 비율
    public const float LowRiseSeconds = 2f;        //   땅 높이에서 올라오는 시간 — 그 뒤 뒤로 빠진다
    public const float LowStartEye = 0.05f, LowRiseEye = 0.3f, LowEndEye = 0.6f; // 눈높이(m) — 늘 대역·마녀보다 아래(로우 앵글)
    public const float LowBelowRatio = 0.18f;      //   화면 아래 끝 = 대역 자리에서 거리 × 비율만큼 아래(땅이 화면 아래쪽에 깔린다)
    public const float FadeInStart = 0.4f, FadeInEnd = 1.2f;   // 테이크 1 처음 검은 화면이 걷히는 구간
    public const float StompCameraBack = 16f;      // 테이크 2: 대역 뒤(맵 안쪽) 거리(m)
    public const float StompCameraEye = 1.6f;      //   눈높이(m)
    public const float StompFadeSeconds = 0.25f;   //   밟기 직전 검게
    public const float EndBackRatio = 0.85f, EndSideRatio = 0.1f, EndUpRatio = 0.35f; // 테이크 3: 밟은 곳에서 맵 쪽·옆·위(키 × 비율)
    public const float EndFadeSeconds = 0.3f;      //   검은 화면 뒤 밝아지는 시간
    public const float MinFinaleFov = 30f, MaxFinaleFov = 90f;

    private bool locked;
    private bool applied;                   // 지난 프레임에 내가 위치를 바꿨는지
    private Vector3 appliedPos, ownPos;     // 아래 카메라가 이번 프레임에 위치를 다시 정하지 않았으면(쉬는 카메라) 지난 내 시점을 쓴다
    private Quaternion appliedRot, ownRot;
    private double leadKey = double.NaN;    // 앞 샷 기준(시작 시각) — 바뀌면 자리를 다시 잡는다
    private Vector3 leadAnchor, leadDir;
    private Camera cam;
    private float ownFov = -1f;              // 도착 연출이 화각을 바꾸기 전 값(연출이 끝나면 되돌린다)
    private List<Renderer> occluderCandidates;                     // 맵 안 부서질 물체(가릴 수 있는 것)
    private readonly List<Renderer> hiddenOccluders = new List<Renderer>();
    private readonly Vector3[] occlusionTargets = new Vector3[4];

    private void OnEnable() => WitchPresenter.Impacted += RestoreOccluders;

    // 클로즈업 카메라 자리: 얼굴 앞(얼굴이 보는 방향) distance만큼, 얼굴을 바라본다.
    public static void CloseUpPose(Vector3 face, Vector3 faceForward, float height, out Vector3 position, out Quaternion rotation)
    {
        Vector3 forward = Vector3.ProjectOnPlane(faceForward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();
        position = face + forward * (height * FaceDistanceRatio) + Vector3.up * (height * FaceRiseRatio);
        rotation = Quaternion.LookRotation(face - position, Vector3.up);
    }

    // 내 시점과 클로즈업 사이 자리. 고개(바라보는 곳)는 위치보다 먼저 얼굴로 돌아가 이동 중에도 마녀가 화면에 있고,
    // 얼굴까지 거리는 클로즈업 거리보다 가까워지지 않는다(몸을 뚫고 지나가지 않게). w = 1이면 CloseUpPose와 같다.
    public static void BlendPose(Vector3 ownPos, Quaternion ownRot, Vector3 face, Vector3 faceForward, float height, float w, out Vector3 position, out Quaternion rotation)
    {
        CloseUpPose(face, faceForward, height, out Vector3 closePos, out Quaternion closeRot);
        position = Vector3.Lerp(ownPos, closePos, w);
        Vector3 toCam = position - face;
        float minDistance = (closePos - face).magnitude;
        if (toCam.sqrMagnitude < minDistance * minDistance)
            position = face + (toCam.sqrMagnitude > 1e-4f ? toCam.normalized : closePos - face) * minDistance;
        Quaternion look = w >= 1f ? closeRot : Quaternion.LookRotation(face - position, Vector3.up);
        rotation = Quaternion.Slerp(ownRot, look, Mathf.Clamp01(w * 2f));
    }

    // 앞 샷으로 옮겨 간 정도(앞 샷 시작부터 초): LeadInSeconds 동안 0 → 1. 순수 계산.
    public static float LeadWeight(float t) => t < 0f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / WitchTimeline.LeadInSeconds));

    // 앞 샷 카메라 자리: 대상 바닥(anchor)에서 dir 쪽으로 LeadDistance, 위로 LeadHeight(+ 떠오른 만큼 일부), 대상을 바라본다.
    public static void LeadPose(Vector3 anchor, Vector3 dir, Vector3 target, out Vector3 position, out Quaternion rotation)
    {
        dir = Vector3.ProjectOnPlane(dir, Vector3.up);
        if (dir.sqrMagnitude < 1e-4f) dir = Vector3.back;
        dir.Normalize();
        float rise = Mathf.Max(0f, target.y - anchor.y);
        position = anchor + dir * LeadDistance + Vector3.up * (LeadHeight + rise * LeadFollowUp);
        rotation = Quaternion.LookRotation(target + Vector3.up - position, Vector3.up);
    }

    // 전신 장면: 맵 쪽(towardMap)에서 마녀 몸 전체(발~모자)가 화면에 들어오는 거리, 약간 아래에서 올려다본다.
    // 타임어택에서는 마녀가 맵을 등지고 있어 등이 보이다가, 돌아서면 얼굴이 이 카메라를 향한다(§23.4 Q1).
    public static void FullBodyPose(Vector3 feet, Vector3 towardMap, float height, out Vector3 position, out Quaternion rotation)
    {
        Vector3 dir = Flat(towardMap);
        position = feet + dir * (height * FullBodyDistanceRatio) + Vector3.up * (height * FullBodyHeightRatio);
        rotation = Quaternion.LookRotation(feet + Vector3.up * (height * FullBodyLookRatio) - position, Vector3.up);
    }

    // 두 점(a·b)이 화면 세로에 들어오는 회전·화각: 두 방향의 가운데를 보고, 사잇각 + 여유(도)를 화각으로.
    public static void FramePose(Vector3 position, Vector3 a, Vector3 b, float margin, out Quaternion rotation, out float fov)
    {
        Vector3 da = (a - position).normalized, db = (b - position).normalized;
        Vector3 aim = da + db;
        if (aim.sqrMagnitude < 1e-6f) aim = da;
        rotation = Quaternion.LookRotation(aim.normalized, Vector3.up);
        fov = Mathf.Clamp(Vector3.Angle(da, db) + margin, MinFinaleFov, MaxFinaleFov);
    }

    // 테이크 1(그림 1~6): t = 도착부터 초. 대역 앞 LowStartDistance의 땅 높이에서 올라와(쿠키·괴물), 그 뒤 로우 앵글을 지킨 채 키 × 0.49까지
    // 뒤로 빠지며 화면 위 끝이 대역 머리 → 아래를 보는 마녀 얼굴(+ 모자)로 옮겨 간다. groundAt: 그 자리 땅 높이.
    public static void LowAnglePose(Vector3 stage, Vector3 inward, float standHeight, Vector3 face, float height, float t,
        System.Func<Vector3, float> groundAt, out Vector3 position, out Quaternion rotation, out float fov)
    {
        Vector3 dir = Flat(inward);
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / LowRiseSeconds));
        float back = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - LowRiseSeconds) / (WitchTimeline.FinaleLowAngleSeconds - LowRiseSeconds)));
        float distance = Mathf.Lerp(LowStartDistance, height * LowEndDistanceRatio, back);
        float eye = Mathf.Lerp(Mathf.Lerp(LowStartEye, LowRiseEye, rise), LowEndEye, back);
        position = stage + dir * distance;
        position.y = (groundAt != null ? groundAt(position) : stage.y) + eye;
        Vector3 bottom = stage - Vector3.up * (distance * LowBelowRatio);
        Vector3 top = Vector3.Lerp(stage + Vector3.up * (standHeight + 0.5f), face + Vector3.up * (height * 0.13f), back);
        FramePose(position, bottom, top, Mathf.Lerp(14f, 4f, back), out rotation, out fov);
    }

    // 테이크 2(그림 12~16): 대역 뒤 StompCameraBack, 눈높이에서 마녀 쪽 위를 넓게 올려다본다(고정) — 밑창이 위에서 내려온다.
    public static void StompPose(Vector3 stage, Vector3 inward, float height, System.Func<Vector3, float> groundAt,
        out Vector3 position, out Quaternion rotation, out float fov)
    {
        Vector3 dir = Flat(inward);
        position = stage + dir * StompCameraBack;
        position.y = (groundAt != null ? groundAt(position) : stage.y) + StompCameraEye;
        FramePose(position, position - dir * 40f + Vector3.up * 2f, position - dir * 25f + Vector3.up * (height * 0.95f), 4f, out rotation, out fov);
    }

    // 테이크 3(그림 17): 밟은 곳(target)에서 맵 쪽·조금 옆·위 — 밟고 선 마녀(발~모자)와 튀는 파편을 정면 위에서.
    public static void EndPose(Vector3 target, Vector3 inward, float height, Vector3 face, out Vector3 position, out Quaternion rotation, out float fov)
    {
        Vector3 dir = Flat(inward);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        position = target + dir * (height * EndBackRatio) + side * (height * EndSideRatio) + Vector3.up * (height * EndUpRatio);
        FramePose(position, target - Vector3.up * (height * 0.02f), face + Vector3.up * (height * 0.14f), 8f, out rotation, out fov);
    }

    // 검은 화면 알파(순수 계산, t = 도착부터 초): 테이크 1 처음, 밟기 직전부터 밟은 뒤 FinaleBlackSeconds까지.
    public static float FinaleFadeAlpha(float t)
    {
        if (t < 0f) return 0f;
        if (t < FadeInEnd) return 1f - Mathf.Clamp01((t - FadeInStart) / (FadeInEnd - FadeInStart));
        float impact = WitchTimeline.FinaleImpactSeconds;
        if (t < impact) return Mathf.Clamp01((t - (impact - StompFadeSeconds)) / StompFadeSeconds);
        float black = impact + WitchTimeline.FinaleBlackSeconds;
        if (t < black) return 1f;
        return 1f - Mathf.Clamp01((t - black) / EndFadeSeconds);
    }

    private static float GroundAt(Vector3 p) => WitchFinaleStage.GroundHeight(p, p.y);

    private static Vector3 Flat(Vector3 v)
    {
        v = Vector3.ProjectOnPlane(v, Vector3.up);
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private static float Smooth(double t, double seconds) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)(t / seconds)));

    // 지금 장면(공통 시계·방 속성).
    public static WitchShot CurrentShot(out double appearAt, out double leadStart, out bool hasLead, out double walkEnd)
    {
        WitchMode mode = WitchTimeline.Mode;
        leadStart = 0; walkEnd = 0; hasLead = false;
        if (mode == WitchMode.None || !WitchTimeline.TryGetAppearAt(out appearAt)) { appearAt = 0; return WitchShot.None; }
        hasLead = WitchTimeline.TryGetLeadInStart(mode, out leadStart);
        walkEnd = WitchTimeline.WalkEnd(appearAt);
        return WitchTimeline.ShotAt(mode, PhotonNetwork.Time, hasLead, leadStart, appearAt, walkEnd, RoomState.TryGetInt(NetKeys.GameResult, out _));
    }

    private void LateUpdate()
    {
        WitchPresenter witch = WitchPresenter.Current;
        Vector3 basePos = applied && transform.position == appliedPos ? ownPos : transform.position;
        Quaternion baseRot = applied && transform.rotation == appliedRot ? ownRot : transform.rotation;

        WitchMode mode = WitchTimeline.Mode;
        WitchShot shot = CurrentShot(out double appearAt, out double leadStart, out _, out double walkEnd);
        if (witch == null || !WitchTimeline.Watches(mode)) shot = WitchShot.None;
        if (shot != WitchShot.None && shot != WitchShot.Lead && !witch.IsShown) shot = WitchShot.None; // 마녀가 아직 이 화면에 없다
        SetLocked(shot != WitchShot.None);
        bool finale = shot == WitchShot.FinaleLowAngle || shot == WitchShot.FinaleStomp || shot == WitchShot.FinaleEnd;
        if (!finale) EndFinaleLook();
        if (shot == WitchShot.None)
        {
            if (applied) transform.SetPositionAndRotation(basePos, baseRot); // 끝난 프레임에 내 시점으로 확실히 되돌린다
            applied = false;
            return;
        }

        ownPos = basePos; ownRot = baseRot;
        double now = PhotonNetwork.Time;

        // 앞 샷 자리: 대상 바닥과, 내 카메라가 있던 쪽 방향을 한 번 정해 둔다
        Transform focus = LeadFocus(mode);
        Vector3 leadPos = basePos; Quaternion leadRot = baseRot;
        if (focus != null)
        {
            if (leadKey != leadStart) { leadKey = leadStart; leadAnchor = focus.position; leadDir = basePos - focus.position; }
            LeadPose(leadAnchor, leadDir, focus.position, out leadPos, out leadRot);
        }

        double t = now - appearAt;
        Vector3 pos; Quaternion rot;
        switch (shot)
        {
            case WitchShot.Lead:
            {
                float l = LeadWeight((float)(now - leadStart));
                pos = Vector3.Lerp(basePos, leadPos, l); rot = Quaternion.Slerp(baseRot, leadRot, l);
                break;
            }
            case WitchShot.Reveal:
            {
                float w = Smooth(t, WitchTimeline.RevealSeconds);
                Vector3 fromPos = focus != null ? leadPos : basePos; Quaternion fromRot = focus != null ? leadRot : baseRot;
                FullBodyPose(witch.FeetPosition, witch.TowardMap, witch.Height, out Vector3 bodyPos, out Quaternion bodyRot);
                pos = Vector3.Lerp(fromPos, bodyPos, w); rot = Quaternion.Slerp(fromRot, bodyRot, w);
                break;
            }
            case WitchShot.Turn:
                FullBodyPose(witch.FeetPosition, witch.TowardMap, witch.Height, out pos, out rot);
                break;
            case WitchShot.Face:
            {
                FullBodyPose(witch.FeetPosition, witch.TowardMap, witch.Height, out Vector3 bodyPos, out Quaternion bodyRot);
                CloseUpPose(witch.FacePoint, witch.FaceForward, witch.Height, out Vector3 facePos, out Quaternion faceRot);
                float k = Smooth(t - WitchTimeline.RevealSeconds - WitchTimeline.TurnSeconds, WitchTimeline.FaceMoveSeconds);
                pos = Vector3.Lerp(bodyPos, facePos, k); rot = Quaternion.Slerp(bodyRot, faceRot, k);
                break;
            }
            case WitchShot.Return:
            {
                double since = t - WitchTimeline.RevealSeconds - WitchTimeline.TurnSeconds - WitchTimeline.FaceSeconds;
                BlendPose(basePos, baseRot, witch.FacePoint, witch.FaceForward, witch.Height, 1f - Smooth(since, WitchTimeline.ReturnSeconds), out pos, out rot);
                break;
            }
            default: // 도착 3테이크
            {
                float since = (float)(now - walkEnd);
                float fov;
                if (shot == WitchShot.FinaleLowAngle)
                {
                    LowAnglePose(witch.StagePoint, witch.TowardMap, StandHeight(witch), witch.FacePoint, witch.Height, since, GroundAt, out pos, out rot, out fov);
                    occlusionTargets[0] = witch.StagePoint + Vector3.up;
                    occlusionTargets[1] = witch.StagePoint + Vector3.up * StandHeight(witch);
                    occlusionTargets[2] = Vector3.Lerp(witch.StagePoint, witch.FacePoint, 0.5f);
                    occlusionTargets[3] = witch.FacePoint;
                    HideOccluders(witch, pos);
                }
                else if (shot == WitchShot.FinaleStomp)
                {
                    StompPose(witch.StagePoint, witch.TowardMap, witch.Height, GroundAt, out pos, out rot, out fov);
                    occlusionTargets[0] = witch.StompTarget + Vector3.up * 12f;
                    occlusionTargets[1] = witch.StompTarget + Vector3.up * 35f;
                    occlusionTargets[2] = witch.StompTarget + Vector3.up * 60f;
                    occlusionTargets[3] = witch.FacePoint;
                    HideOccluders(witch, pos);
                }
                else
                {
                    EndPose(witch.StompTarget, witch.TowardMap, witch.Height, witch.FacePoint, out pos, out rot, out fov);
                    pos += CameraShake.Offset * (StrikeShakeScale * witch.Height / 90f); // 다른 카메라처럼 위치를 정한 뒤 흔들림을 더한다
                }
                SetFinaleLook(fov, FinaleFadeAlpha(since));
                break;
            }
        }
        appliedPos = pos; appliedRot = rot;
        transform.SetPositionAndRotation(appliedPos, appliedRot);
        applied = true;
    }

    private static float StandHeight(WitchPresenter witch)
    {
        var stage = witch.GetComponent<WitchFinaleStage>();
        return stage != null ? stage.StandHeight : WitchFinaleStage.DefaultStandHeight;
    }

    // 도착 연출 동안만 화각·검은 화면을 바꾸고, 끝나면 원래 화각으로 되돌린다.
    private void SetFinaleLook(float fov, float fade)
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam != null)
        {
            if (ownFov < 0f) ownFov = cam.fieldOfView;
            cam.fieldOfView = fov;
        }
        CutsceneFade.Set(fade);
    }

    // 도착 연출 테이크 1·2: 카메라와 대역·밑창·마녀 사이를 가리는 맵 물체를 숨긴다. 밟는 순간(Impacted) 되돌린다 — 그 직후 충격파가
    // 부술 것은 부순다(WitchShatter). 판이 끝나는 장면이라 다른 화면과 맞출 필요가 없다.
    private void HideOccluders(WitchPresenter witch, Vector3 from)
    {
        if (occluderCandidates == null) occluderCandidates = OccluderCandidates(witch);
        foreach (Renderer r in occluderCandidates)
        {
            if (r == null || !r.enabled) continue;
            Bounds b = r.bounds;
            foreach (Vector3 to in occlusionTargets)
            {
                Vector3 d = to - from;
                if (!b.IntersectRay(new Ray(from, d.normalized), out float hit) || hit > d.magnitude - 1f) continue;
                r.enabled = false;
                hiddenOccluders.Add(r);
                break;
            }
        }
    }

    public const float OccluderMaxWidth = 60f;    // 이보다 넓은 것(땅·배경 바닥)은 가림막으로 보지 않는다

    // 가릴 수 있는 것: 중심이 Ground 사각형 안인 메시(맵 안에 놓인 배경 나무·소품 포함). 땅·넓은 바닥·마녀·캐릭터·대역·FarBackdrop은 뺀다.
    public static List<Renderer> OccluderCandidates(WitchPresenter witch)
    {
        var list = new List<Renderer>();
        if (!WitchPath.TryGetGroundRect(witch.gameObject.scene, out Rect ground)) return list;
        foreach (GameObject root in witch.gameObject.scene.GetRootGameObjects())
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                Bounds b = r.bounds;
                if (!ground.Contains(new Vector2(b.center.x, b.center.z)) || Mathf.Max(b.size.x, b.size.z) > OccluderMaxWidth) continue;
                if (r.GetComponentInParent<WitchPresenter>() != null || r.GetComponentInParent<IGameCharacter>() != null) continue;
                bool kept = false;
                for (Transform t = r.transform; t != null && !kept; t = t.parent)
                    kept = t.name == WitchPath.GroundName || t.name == WitchPath.FarBackdropName;
                if (!kept) list.Add(r);
            }
        return list;
    }

    private void RestoreOccluders()
    {
        foreach (Renderer r in hiddenOccluders) if (r != null) r.enabled = true;
        hiddenOccluders.Clear();
    }

    private void EndFinaleLook()
    {
        RestoreOccluders();
        if (ownFov < 0f) return;
        if (cam != null) cam.fieldOfView = ownFov;
        ownFov = -1f;
        CutsceneFade.Set(0f);
    }

    // 앞 샷 대상: 첫 탈출 지점(탈출구·로켓, Request1009Plan.md §25.5 G). 기록이 없으면(옛 방) 스파이 로켓.
    private static Transform LeadFocus(WitchMode mode)
    {
        EscapeManager manager = EscapeManager.Instance;
        if (manager == null) return null;
        Transform first = manager.FirstEscapeFocus;
        if (first != null) return first;
        return manager.Rocket != null ? manager.Rocket.transform : null;
    }

    private void SetLocked(bool value)
    {
        if (locked == value) return;
        locked = value;
        PlayerInput.CutsceneActive = value;
    }

    private void OnDisable()
    {
        WitchPresenter.Impacted -= RestoreOccluders;
        SetLocked(false);
        EndFinaleLook();
    }
}
