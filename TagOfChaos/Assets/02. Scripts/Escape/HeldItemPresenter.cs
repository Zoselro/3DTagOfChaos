using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 쿠키가 손에 든 아이템을 모든 클라이언트 화면에 보여준다(EscapePlan.md §1.9). 소유자의 고른 칸(SelectedSlot)과
// EscapeState로 무엇을 드는지 정한다. 손에 든 아이템이 겉으로 보이므로 누가 재료를 들고 있는지 모두가 볼 수 있다(스파이 추리 단서).
// 드는 모습(Request1009Plan.md §16.4):
// - 두 손 아이템: 다른 쿠키를 들 때와 같은 상체 두 손 자세(Carry 레이어)를 켜고, 두 손 뼈 가운데에 놓는다.
// - 한 손 아이템: 오른손 뼈를 따라가고, 오른팔만 가슴 옆 앞으로 드는 자세(Hold 층, §75 D1)를 켠다. 손에 든 동안만 HeldScale로 키운다.
// - 뿅망치(§75 A1): 소켓 = 오른손, 자루 = 왼손 → 오른손 선(어깨 걸침·내려찍기 클립이 두 손을 늘 자루 위에 둔다).
// - 권투 글러브(§75 C1): 모델 두 개를 양손 뼈에 끼운다(왼손은 좌우 뒤집기). 가드·주먹은 PlayerActionPose.
// 위치는 애니메이션이 끝난 뒤(LateUpdate) 맞추고, 방향은 몸 방향을 따른다(손목 회전에 아이템이 뒤집히지 않게).
public class HeldItemPresenter : MonoBehaviourPunCallbacks
{
    private const float OneHandedSitAbove = 0.09f; // 한 손 아이템: 모델 아래 끝이 손목 뼈보다 이만큼 위(주먹 위에 올려 든다, §75 D1)
    private static readonly Quaternion OneHandedTilt = Quaternion.Euler(35f, 0f, 0f); // 쥔 곳을 축으로 위쪽을 앞으로 기울인다(손잡이가 몸에 가려지지 않게)

    private EscapeManager manager;
    private PhotonView view;
    private HideOrSeekPlayer cookie;
    private Animator animator;
    private Transform rightHand, leftHand;
    private Transform socket;
    private GameObject model;
    private string shownId;
    private HoldType holdType;
    private Vector3 holdOffset;   // 몸 방향 기준 추가 위치(ItemSO.HoldOffset)
    private Quaternion holdRotation = Quaternion.identity;

    // 캐릭터 모델이 바뀌면(아바타 교체, §45 CH6) 손 뼈를 다시 찾는다.
    private Avatar boneAvatar;

    private void RefreshBones()
    {
        if (animator == null || animator.avatar == boneAvatar) return;
        boneAvatar = animator.avatar;
        rightHand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        leftHand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
        rightForearm = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
        leftForearm = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
        headBone = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        headMeasured = false; // 새 몸의 머리 크기는 다음 망치 자세에서 잰다(모델이 붙은 뒤)
    }

    public void Init(EscapeManager escape, PhotonView ownerView)
    {
        manager = escape;
        view = ownerView;
        cookie = GetComponent<HideOrSeekPlayer>();
        animator = GetComponent<Animator>();
        boneAvatar = null;
        RefreshBones();
        socket = new GameObject("CarriedItemSocket").transform;
        socket.SetParent(transform, false);
        manager.StateChanged += Refresh;
        Refresh();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (manager != null) manager.StateChanged -= Refresh;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        if (manager != null)
        {
            manager.StateChanged -= Refresh;
            manager.StateChanged += Refresh;
        }
    }

    public override void OnPlayerPropertiesUpdate(Player target, ExitGames.Client.Photon.Hashtable changed)
    {
        if (view != null && target == view.Owner && changed.ContainsKey(NetKeys.SelectedSlot)) Refresh();
    }

    private void Refresh()
    {
        if (manager == null || manager.State == null || view == null || view.Owner == null) return;
        int actor = view.Owner.ActorNumber;
        int slot = RoomState.TryGetPlayerInt(view.Owner, NetKeys.SelectedSlot, out int s) ? s : 0;
        int item = manager.State.HeldItemAt(actor, slot);
        ItemSO def = manager.ItemDef(item);
        string id = def != null ? def.ItemId : null;
        if (id == shownId) return;

        shownId = id;
        if (model != null) Destroy(model);
        model = null;
        ClearGloves();
        isHammer = def != null && def.IsTool && def.Tool.Kind == ToolKind.Hammer;
        isGlove = def != null && def.IsTool && def.Tool.Kind == ToolKind.BoxingGlove;
        if (pose == null) pose = GetComponent<PlayerActionPose>();
        if (pose != null)
        {
            // 들기 자세(§74 P13·§75): 뿅망치 = 어깨 걸침, 글러브 = 가드, 다른 한 손 아이템 = 오른팔만 가슴 옆 앞
            pose.SetHold(isHammer ? PlayerActionPose.Hold.Hammer : isGlove ? PlayerActionPose.Hold.Glove : PlayerActionPose.Hold.None);
            pose.SetOneHandHold(def != null && !isHammer && !isGlove && def.HoldType == HoldType.OneHanded);
            pose.SetThrowAim(def != null && def.IsTool && def.Tool.IsThrown); // 조준 = 던지기 준비 자세(§77 TP1)
        }
        // 두 손 들기(Carry 층, 머리 위 두 손)는 재료 같은 두 손 아이템만 — 뿅망치는 Action 층의 어깨 걸침이 상체를 맡는다(§75 A1)
        if (cookie != null) cookie.SetCarry(CarryReason.TwoHandedItem, def != null && def.LocksSlotSwitch && !isHammer);
        if (def == null) return;

        holdType = def.HoldType;
        if (isGlove) { CreateGloves(def); return; }
        bool balloon = def.IsTool && def.Tool.Kind == ToolKind.WaterBalloon;
        model = balloon ? WaterBubbleView.CreateSmall(socket, HeldBalloonSize) : EscapeVisuals.CreateItemModel(def, socket); // 물풍선은 갇힘 물방울과 같은 모양(§45.7 WB5)
        if (def.HeldScale != 1f) model.transform.localScale *= def.HeldScale; // 손에 든 동안만 키운다(뿅망치 3배 §45.5, 작은 한 손 아이템 §75 D1)
        if (isHammer)
        {
            // 소켓 = 오른손(위쪽 손), 위 = 자루(왼손 → 오른손 → 망치 머리), 앞 = 머리가 내려치는 면. 망치 머리(모델 X축 원통)를 소켓 앞(Z)으로 돌리고
            // 오른손이 자루 아래에서 HammerGripFromBottom 지점을 쥐게 내린다. 머리 가운데는 훑기 판정에 쓴다(§75 A4).
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
            Bounds mb = MeshBounds(model);
            float scale = model.transform.localScale.y;
            float grip = (mb.min.y + mb.size.y * HammerGripFromBottom) * scale;
            model.transform.localPosition = -Vector3.up * grip;
            hammerHeadLocal = Vector3.up * ((mb.min.y + mb.size.y * HammerHeadFromBottom) * scale - grip);
            hammerHeadRadius = Mathf.Max(mb.extents.x, mb.extents.z) * scale;
        }
        bool oneHanded = holdType == HoldType.OneHanded;
        bool gripPoint = oneHanded && def.UseGripPoint; // 총처럼 손잡이가 있는 아이템: 손잡이를 쥐고 앞을 겨눈다(§75.14)
        holdRotation = (oneHanded && !gripPoint ? OneHandedTilt : Quaternion.identity) * Quaternion.Euler(def.HoldEuler);
        holdOffset = def.HoldOffset;
        if (gripPoint) model.transform.localPosition -= model.transform.localRotation * Vector3.Scale(def.GripPoint, model.transform.localScale);
        else if (oneHanded) model.transform.localPosition += OneHandedGripOffset(model); // 쥔 곳이 소켓(손)에 오게 — 기울기는 쥔 곳을 축으로
        LateUpdate();
    }

    // 모델 메시의 모델 기준 경계(첫 메시) — 회전·크기 전 값.
    private static Bounds MeshBounds(GameObject go)
    {
        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
        return mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f);
    }

    // ---------------- 뿅망치 머리(§75 A4) ----------------

    private Vector3 hammerHeadLocal;
    private float hammerHeadRadius = 0.4f;

    // 지금 손에 든 뿅망치 머리 가운데(월드)와 반지름. 뿅망치를 안 들었으면 false.
    public bool TryGetHammerHead(out Vector3 center, out float radius)
    {
        center = default; radius = hammerHeadRadius;
        if (!isHammer || model == null) return false;
        center = socket.TransformPoint(hammerHeadLocal);
        return true;
    }

    // ---------------- 머리 비키기(§75.12) ----------------

    private Transform headBone;
    private bool headMeasured, headKnown;
    private Vector3 headOffset, headRadii;          // 머리 타원체: 머리 뼈 기준 가운데(몸 기준 공간)·반지름
    private Quaternion hammerAvoid = Quaternion.identity; // 몸 기준 공간의 비키기 회전(부드럽게 따라간다)
    private const float AvoidDegreesPerSecond = 900f;

    // 망치(쥔 곳 pivot, 회전 hammer)가 머리와 겹치면 쥔 곳을 축으로 비켜 세우는 월드 회전. 머리를 못 재면 그대로.
    private Quaternion ClearOfHead(Vector3 pivot, Quaternion hammer)
    {
        if (!headMeasured && headBone != null)
        {
            headMeasured = true;
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            float neckY = transform.InverseTransformPoint((neck != null ? neck : headBone).position).y;
            headKnown = HammerClearance.MeasureHead(transform, headBone, neckY, CharacterModelSwap.BodyRenderers(transform), out headOffset, out headRadii);
        }
        if (!headKnown || headBone == null) return Quaternion.identity;
        Quaternion body = transform.rotation, toBody = Quaternion.Inverse(body);
        Vector3 pivotLocal = transform.InverseTransformPoint(pivot);
        Vector3 offsetLocal = toBody * (hammer * hammerHeadLocal);
        Vector3 centerLocal = transform.InverseTransformPoint(headBone.position) + headOffset;
        Quaternion target = HammerClearance.FindClear(pivotLocal, offsetLocal, hammerHeadRadius, centerLocal, headRadii);
        Quaternion eased = Quaternion.RotateTowards(hammerAvoid, target, AvoidDegreesPerSecond * Time.deltaTime);
        // 부드럽게 따라가되, 아직 머리 속이면 바로 트인 각도로(빠른 내려찍기에서 한두 프레임 뚫리지 않게)
        hammerAvoid = HammerClearance.Hits(pivotLocal, eased * offsetLocal, hammerHeadRadius, centerLocal, headRadii) ? target : eased;
        return body * hammerAvoid * toBody;
    }

    // ---------------- 권투 글러브 양손(§75 C1) ----------------

    private GameObject gloveR, gloveL;
    private Transform rightForearm, leftForearm;
    private static readonly Quaternion GloveModelRotation = Quaternion.Euler(0f, 0f, 0f); // 글러브 모델 앞(주먹 면)을 팔 방향으로
    private const float GloveForward = 0.05f; // 손목에서 주먹 쪽으로

    private void CreateGloves(ItemSO def)
    {
        gloveR = EscapeVisuals.CreateItemModel(def, transform);
        gloveL = EscapeVisuals.CreateItemModel(def, transform);
        if (def.HeldScale != 1f) { gloveR.transform.localScale *= def.HeldScale; gloveL.transform.localScale *= def.HeldScale; }
        Vector3 ls = gloveL.transform.localScale; gloveL.transform.localScale = new Vector3(-ls.x, ls.y, ls.z); // 왼손은 좌우 뒤집기
        foreach (Renderer r in gloveL.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        LateUpdate();
    }

    private void ClearGloves()
    {
        if (gloveR != null) Destroy(gloveR);
        if (gloveL != null) Destroy(gloveL);
        gloveR = gloveL = null;
    }

    // 주먹 끝(월드) — 훑기 판정용. 글러브를 안 꼈으면 false.
    public bool TryGetFist(bool left, out Vector3 center)
    {
        GameObject g = left ? gloveL : gloveR;
        center = g != null ? g.transform.position : default;
        return g != null;
    }

    private void PlaceGlove(GameObject glove, Transform hand, Transform forearm, Quaternion body)
    {
        if (glove == null || hand == null) return;
        Vector3 dir = forearm != null ? hand.position - forearm.position : body * Vector3.forward;
        if (dir.sqrMagnitude < 1e-6f) dir = body * Vector3.forward;
        dir.Normalize();
        Vector3 up = Vector3.ProjectOnPlane(body * Vector3.up, dir);
        if (up.sqrMagnitude < 1e-4f) up = body * Vector3.back;
        glove.transform.SetPositionAndRotation(hand.position + dir * GloveForward, Quaternion.LookRotation(dir, up) * GloveModelRotation);
    }

    // 한 손 아이템은 주먹 위에 올려 든다 — 예전에는 위에서 0.12 m 아래를 쥐어 작은 아이템이 큰 손(벙어리장갑 모양) 속에 묻혀 안 보였다(§75 D1).
    private static Vector3 OneHandedGripOffset(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return Vector3.zero;
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        Vector3 localCenter = go.transform.InverseTransformPoint(b.center);
        float localExtentY = b.extents.y / Mathf.Max(0.0001f, go.transform.lossyScale.y);
        return Vector3.up * GripLift(localCenter.y, localExtentY);
    }

    // 아래 끝(center - extent)이 손목보다 OneHandedSitAbove 위에 오도록 올리는 높이.
    public static float GripLift(float centerY, float extentY) => extentY - centerY + OneHandedSitAbove;

    private void LateUpdate()
    {
        Quaternion body = transform.rotation;
        RefreshBones();
        if (isGlove)
        {
            PlaceGlove(gloveR, rightHand, rightForearm, body);
            PlaceGlove(gloveL, leftHand, leftForearm, body);
            return;
        }
        if (model == null) return;
        if (isHammer && rightHand != null && leftHand != null)
        {
            // 자루 = 왼손 → 오른손 선(클립에서 두 손이 늘 자루 위에 있다) — 손뼈를 바로 따라가 덜렁거리지 않는다(§75 A1).
            // 앞(머리가 내려치는 면) = 몸 오른쪽 × 자루. 두 손이 거의 겹치는 순간에는 직전 방향을 쓴다.
            Vector3 shaft = rightHand.position - leftHand.position;
            if (shaft.sqrMagnitude > MinHandGap * MinHandGap) hammerShaft = shaft.normalized;
            Vector3 face = Vector3.Cross(body * Vector3.right, hammerShaft);
            if (face.sqrMagnitude < 1e-4f) face = Vector3.Cross(body * Vector3.forward, hammerShaft);
            Quaternion hammer = Quaternion.LookRotation(face.normalized, hammerShaft) * holdRotation;
            socket.SetPositionAndRotation(rightHand.position, ClearOfHead(rightHand.position, hammer) * hammer);
            return;
        }
        Vector3 anchor = holdType == HoldType.TwoHanded || rightHand == null
            ? CarryPose.HandsCenter(transform, animator)
            : rightHand.position;
        socket.SetPositionAndRotation(anchor + body * holdOffset, body * holdRotation);
        FirstPersonThrowItem();
    }

    // 1인칭에서도 던지는 도구는 던지기 준비 손에 그대로 둔다(§77.1 — 예전 FP3의 "오른손 앞에 작게"는 없앴다). 줄여 둔 크기만 되돌린다.
    private void FirstPersonThrowItem()
    {
        if (socket.localScale != Vector3.one) socket.localScale = Vector3.one;
    }

    private const float HeldBalloonSize = 0.38f;
    private const float HammerGripFromBottom = 0.40f; // 오른손이 쥐는 곳: 자루 아래에서 40 %(머리 바로 아래)
    private const float HammerHeadFromBottom = 0.78f; // 머리 가운데: 아래에서 78 %(ITEM_Hammer 머리 y 0.07~0.33)
    private const float MinHandGap = 0.05f;
    private bool isHammer, isGlove;
    private PlayerActionPose pose;
    private Vector3 hammerShaft = Vector3.up;
}
