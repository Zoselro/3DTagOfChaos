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
        actionLayerIndex = animator.GetLayerIndex(PlayerActionPose.ActionLayer);
        carryLayerIndex = animator.GetLayerIndex(CarryLayerName);
        rightUpperArm = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
        leftUpperArm = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
        headMeasured = false; // 새 몸의 머리 크기는 다음 망치 자세에서 잰다(모델이 붙은 뒤)
        bodyFrontMeasured = false; // 새 몸의 앞면·손바닥 축은 다음 두 손 들기에서 잰다(§88)
        palmR.Known = palmL.Known = false;
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
            // 두 손이 자루 끝을 쥐게 내린다(ApplyHammerGrip, 손 간격을 매 프레임 따라감). 머리 가운데는 훑기 판정에 쓴다(§75 A4).
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
            Bounds mb = MeshBounds(model);
            float scale = model.transform.localScale.y;
            hammerBottomModel = mb.min.y * scale;
            hammerHeadModel = (mb.min.y + mb.size.y * HammerHeadFromBottom) * scale;
            ApplyHammerGrip(DefaultHammerHandGap);
            hammerHeadRadius = Mathf.Max(mb.extents.x, mb.extents.z) * scale;
        }
        bool oneHanded = holdType == HoldType.OneHanded;
        bool gripPoint = oneHanded && def.UseGripPoint; // 총처럼 손잡이가 있는 아이템: 손잡이를 쥐고 앞을 겨눈다(§75.14)
        holdRotation = (oneHanded && !gripPoint ? OneHandedTilt : Quaternion.identity) * Quaternion.Euler(def.HoldEuler);
        holdOffset = def.HoldOffset;
        if (gripPoint) model.transform.localPosition -= model.transform.localRotation * Vector3.Scale(def.GripPoint, model.transform.localScale);
        else if (oneHanded) model.transform.localPosition += OneHandedGripOffset(model); // 쥔 곳이 소켓(손)에 오게 — 기울기는 쥔 곳을 축으로
        if (socket.localScale != Vector3.one) socket.localScale = Vector3.one; // 앞 아이템의 Fit 배율(§88.9)을 지운다
        if (holdType == HoldType.TwoHanded && !isHammer)
        {
            // 두 손 아이템: 가로 길이에 맞춰 잡는다(§88 G1·G2). 모델 회전·크기가 정해진 뒤 잰다.
            gripSize = ItemGripMeasure.WithOverride(ItemGripMeasure.Measure(model, socket, holdRotation, def.GripHeight), def.GripHalfWidth);
            gripInset = def.GripInset;
            gripSolved = false;
            bodyFrontMeasured = false; // 몸 앞면은 아이템 높이·폭 범위에서 잰다 — 아이템마다 다시
        }
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
            if (shaft.sqrMagnitude > MinHandGap * MinHandGap) { hammerShaft = shaft.normalized; ApplyHammerGrip(shaft.magnitude); }
            Vector3 face = Vector3.Cross(body * Vector3.right, hammerShaft);
            if (face.sqrMagnitude < 1e-4f) face = Vector3.Cross(body * Vector3.forward, hammerShaft);
            Quaternion hammer = Quaternion.LookRotation(face.normalized, hammerShaft) * holdRotation;
            socket.SetPositionAndRotation(rightHand.position, ClearOfHead(rightHand.position, hammer) * hammer);
            return;
        }
        Vector3 anchor = holdType == HoldType.TwoHanded || rightHand == null
            ? CarryPose.HandsCenter(transform, animator)
            : rightHand.position;
        if (holdType == HoldType.TwoHanded && gripSolved && twoHandIK > 0f)
        {
            // 두 손 아이템(§88 G6): 잡는 높이의 아이템 가운데를 계산한 자리(몸 가운데 앞)에 — 손이 덜 닿아도 아이템은 몸에 안 박힌다.
            // 팔 짧은 캐릭터는 Fit이 아이템을 줄인다(§88.9) — 소켓 크기로 줄이고, 소켓→아이템 가운데 거리도 같이 줄인다.
            float scale = Mathf.Lerp(1f, grip.Scale, twoHandIK);
            Vector3 solved = transform.TransformPoint(grip.ItemCenter) - body * gripSize.Center * scale;
            socket.SetPositionAndRotation(Vector3.Lerp(anchor, solved, twoHandIK) + body * holdOffset, body * holdRotation);
            socket.localScale = Vector3.one * scale;
            return;
        }
        socket.SetPositionAndRotation(anchor + body * holdOffset, body * holdRotation);
        FirstPersonThrowItem();
    }

    // 1인칭에서도 던지는 도구는 던지기 준비 손에 그대로 둔다(§77.1 — 예전 FP3의 "오른손 앞에 작게"는 없앴다). 줄여 둔 크기만 되돌린다.
    private void FirstPersonThrowItem()
    {
        if (socket.localScale != Vector3.one) socket.localScale = Vector3.one;
    }

    private const float HeldBalloonSize = 0.38f;
    // ---------------- 뿅망치 들기 자세(§87) ----------------
    // 들고만 있을 때 두 손을 몸 앞 대각선 자루 위에 놓는다(Action 층 IK Pass): 아래 손(왼손)은 배 앞, 위 손(오른손)은 자루를 따라 그 위,
    // 망치 머리는 오른쪽 위 앞. 클립(Cookie_HammerHold)은 두 손을 가슴 앞에 수평으로 두어 망치가 옆으로 길게 뻗었다.
    // 내려찍기(Swing)에서는 IK를 끄고 클립 손을 따라간다. 둘 사이는 HammerIKBlendSeconds 동안 이어 붙인다.
    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.isHuman) return;
        if (layerIndex == carryLayerIndex) { TwoHandedIK(); return; }
        if (layerIndex != actionLayerIndex) return;
        bool hold = isHammer && model != null && pose != null && pose.IsHoldingHammer
            && !animator.GetCurrentAnimatorStateInfo(actionLayerIndex).IsName(PlayerActionPose.SwingState);
        float dt = Application.isPlaying ? Time.deltaTime : 1f;
        hammerIK = Mathf.MoveTowards(hammerIK, hold ? 1f : 0f, dt / HammerIKBlendSeconds);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, hammerIK);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, hammerIK);
        if (hammerIK <= 0f) return;
        Quaternion body = transform.rotation;
        Vector3 lower = animator.GetIKPosition(AvatarIKGoal.LeftHand) + body * HoldLowerHandShift;
        Vector3 upper = lower + body * HoldShaft * DefaultHammerHandGap;
        animator.SetIKPosition(AvatarIKGoal.LeftHand, lower);
        animator.SetIKPosition(AvatarIKGoal.RightHand, upper);
    }

    // ---------------- 두 손 아이템 들기(§88) ----------------
    // Carry 층 클립은 쿠키 몸에 맞춘 자세라, 팔이 짧은 머리 큰 캐릭터는 손이 배·허리로 처지고 아이템이 몸에 붙었다.
    // 두 손 아이템을 드는 동안: 높이·앞은 어깨에서 팔 길이 비율(TwoHandGrip), 좌우는 아이템 가로 길이의 양 끝(ItemGripMeasure),
    // 팔이 모자라면 옆면 뒤쪽을 잡는다. 손바닥은 아이템 옆면을 본다(PalmInward).
    private void TwoHandedIK()
    {
        bool on = model != null && !isHammer && !isGlove && holdType == HoldType.TwoHanded
            && rightUpperArm != null && leftUpperArm != null && rightForearm != null && leftForearm != null && rightHand != null && leftHand != null;
        float dt = Application.isPlaying ? Time.deltaTime : 1f;
        twoHandIK = Mathf.MoveTowards(twoHandIK, on ? 1f : 0f, dt / HammerIKBlendSeconds);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, twoHandIK);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, twoHandIK);
        if (twoHandIK <= 0f || !on)
        {
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            LearnPalmOffset(ref palmR, AvatarIKGoal.RightHand, rightHand);
            LearnPalmOffset(ref palmL, AvatarIKGoal.LeftHand, leftHand);
            return;
        }
        float armR = ArmLength(rightUpperArm, rightForearm, rightHand), armL = ArmLength(leftUpperArm, leftForearm, leftHand);
        Vector3 shoulderR = transform.InverseTransformPoint(rightUpperArm.position), shoulderL = transform.InverseTransformPoint(leftUpperArm.position);
        var input = new TwoHandGrip.Input
        {
            ShoulderR = shoulderR, ShoulderL = shoulderL, ArmR = armR, ArmL = armL,
            HalfWidth = gripSize.HalfWidth, HalfDepth = gripSize.HalfDepth, Inset = gripInset,
        };
        if (!bodyFrontMeasured)
        {
            // 몸 앞면을 높이별로 한 번 재고(§88.9), 그 높이에서 닿는 잡는 높이·배율을 정한다(아이템·캐릭터가 바뀔 때만).
            bodyFrontMeasured = true;
            MeasureFrontProfile();
            frontFallbackZ = Mathf.Max(shoulderR.z, shoulderL.z) + 0.1f;
            fit = TwoHandGrip.Fit(input, gripSize.Below, gripSize.Above, FrontInBand);
        }
        float y = Mathf.Min(shoulderR.y - armR * TwoHandGrip.Drop, shoulderL.y - armL * TwoHandGrip.Drop) - fit.ExtraDrop;
        bodyFrontZ = FrontInBand(y - gripSize.Below * fit.Scale, y + gripSize.Above * fit.Scale);
        grip = TwoHandGrip.SolveWith(input, fit, bodyFrontZ);
        gripSolved = true;
        animator.SetIKPosition(AvatarIKGoal.RightHand, transform.TransformPoint(grip.HandR));
        animator.SetIKPosition(AvatarIKGoal.LeftHand, transform.TransformPoint(grip.HandL));
        PalmInward(ref palmR, AvatarIKGoal.RightHand, rightHand, true);
        PalmInward(ref palmL, AvatarIKGoal.LeftHand, leftHand, false);
    }

    private static float ArmLength(Transform upper, Transform lower, Transform hand) =>
        Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);

    // 몸 앞면 높이별 표(§88.9): 아이템 폭(최소 BodyHalfWidth) 안의 몸 꼭짓점을 ProfileStep 높이 칸마다 가장 앞 z로 모은다.
    // 팔(어깨·위팔·아래팔·손)이 주로 움직이는 꼭짓점은 뺀다 — 손은 아이템 옆에 있어야 하므로. 큰 머리(마카롱·붕어)는 높은 칸만 앞으로 튀어나온다.
    private void MeasureFrontProfile()
    {
        float halfX = Mathf.Max(gripSize.HalfWidth, BodyHalfWidth);
        for (int i = 0; i < frontProfile.Length; i++) frontProfile[i] = float.MinValue;
        var arms = new System.Collections.Generic.HashSet<Transform>();
        foreach (HumanBodyBones b in ArmBones) { Transform t = animator.GetBoneTransform(b); if (t != null) arms.Add(t); }
        var baked = new Mesh();
        var verts = new System.Collections.Generic.List<Vector3>();
        foreach (Renderer r in CharacterModelSwap.BodyRenderers(transform))
        {
            if (!(r is SkinnedMeshRenderer smr) || smr.sharedMesh == null) continue;
            smr.BakeMesh(baked, false); // 뼈 공간 크기 그대로 — 회전·위치만 더한다(HammerClearance.MeasureHead와 같음)
            Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            baked.GetVertices(verts);
            BoneWeight[] weights = smr.sharedMesh.boneWeights;
            Transform[] bones = smr.bones;
            for (int i = 0; i < verts.Count; i++)
            {
                if (i < weights.Length && weights[i].boneIndex0 < bones.Length && arms.Contains(bones[weights[i].boneIndex0])) continue;
                Vector3 p = transform.InverseTransformPoint(toWorld.MultiplyPoint3x4(verts[i]));
                int k = Mathf.FloorToInt(p.y / ProfileStep);
                if (Mathf.Abs(p.x) < halfX && k >= 0 && k < frontProfile.Length) frontProfile[k] = Mathf.Max(frontProfile[k], p.z);
            }
        }
        if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked);
    }

    // 높이 범위(lowY~highY) 몸 앞면 z. 그 범위에 몸이 없으면 어깨 앞 0.1 m.
    private float FrontInBand(float lowY, float highY)
    {
        float front = float.MinValue;
        int a = Mathf.Max(0, Mathf.FloorToInt(lowY / ProfileStep)), b = Mathf.Min(frontProfile.Length - 1, Mathf.FloorToInt(highY / ProfileStep));
        for (int k = a; k <= b; k++) front = Mathf.Max(front, frontProfile[k]);
        return front > float.MinValue ? front : frontFallbackZ;
    }

    // §88 첫 판: 기본 잡는 높이 한 곳에서만 몸 앞면을 쟀다 — 높이를 낮춰 보는 Fit(§88.9)을 위해 MeasureFrontProfile·FrontInBand로 바뀜.
    // // 아이템이 차지할 높이(잡는 높이 − Below ~ + Above)·폭(±아이템 가로 절반, 최소 BodyHalfWidth) 안의 몸 꼭짓점 중 가장 앞 z(몸 기준).
    // // 팔(어깨·위팔·아래팔·손)이 주로 움직이는 꼭짓점은 뺀다 — 손은 아이템 옆에 있어야 하므로(처음 판은 손까지 재서 쿠키 앞면이 0.48 m로 나왔다).
    // // 아이템 뒷면이 이보다 앞이어야 한다(머리 큰 캐릭터는 아이템 위쪽이 턱·수염 높이에 닿으므로 머리 앞면도 들어간다). 못 재면 어깨 앞 0.1 m.
    // private float MeasureBodyFront(float handY, float shoulderZ)
    // {
    //     float lowY = handY - gripSize.Below, highY = handY + gripSize.Above, halfX = Mathf.Max(gripSize.HalfWidth, BodyHalfWidth);
    //     var arms = new System.Collections.Generic.HashSet<Transform>();
    //     foreach (HumanBodyBones b in ArmBones) { Transform t = animator.GetBoneTransform(b); if (t != null) arms.Add(t); }
    //     float front = float.MinValue;
    //     var baked = new Mesh();
    //     var verts = new System.Collections.Generic.List<Vector3>();
    //     foreach (Renderer r in CharacterModelSwap.BodyRenderers(transform))
    //     {
    //         if (!(r is SkinnedMeshRenderer smr) || smr.sharedMesh == null) continue;
    //         smr.BakeMesh(baked, false); // 뼈 공간 크기 그대로 — 회전·위치만 더한다(HammerClearance.MeasureHead와 같음)
    //         Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
    //         baked.GetVertices(verts);
    //         BoneWeight[] weights = smr.sharedMesh.boneWeights;
    //         Transform[] bones = smr.bones;
    //         for (int i = 0; i < verts.Count; i++)
    //         {
    //             if (i < weights.Length && weights[i].boneIndex0 < bones.Length && arms.Contains(bones[weights[i].boneIndex0])) continue;
    //             Vector3 p = transform.InverseTransformPoint(toWorld.MultiplyPoint3x4(verts[i]));
    //             if (Mathf.Abs(p.x) < halfX && p.y > lowY && p.y < highY) front = Mathf.Max(front, p.z);
    //         }
    //     }
    //     if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked);
    //     return front > float.MinValue ? front : shoulderZ + 0.1f;
    // }

    private static readonly HumanBodyBones[] ArmBones =
    {
        HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
    };

    // 손바닥이 아이템 옆면을 보게(§88 G4). 뼈대에 손가락 뼈가 없어(25뼈) 손바닥 축은 바인드 자세(T자, 손바닥 아래)에서 잰다.
    // IK 목표 회전과 손뼈 회전 사이의 아바타 고유 차이(offset)는 IK를 쓰지 않는 동안 배운다.
    private struct PalmFrame { public bool Known, HasOffset; public Vector3 Palm, Finger; public Quaternion Offset; }

    private void LearnPalmOffset(ref PalmFrame f, AvatarIKGoal goal, Transform hand)
    {
        if (hand == null) return;
        f.Offset = Quaternion.Inverse(animator.GetIKRotation(goal)) * hand.rotation;
        f.HasOffset = true;
    }

    private void PalmInward(ref PalmFrame f, AvatarIKGoal goal, Transform hand, bool right)
    {
        if (!f.Known) f = MeasurePalm(hand, right, f);
        if (!f.Known || !f.HasOffset) { animator.SetIKRotationWeight(goal, 0f); return; }
        Quaternion body = transform.rotation;
        Vector3 inward = body * (right ? Vector3.left : Vector3.right);
        Vector3 forward = body * Vector3.forward;
        Quaternion boneLocalBasis = Quaternion.LookRotation(f.Finger, f.Palm);
        Quaternion boneWanted = Quaternion.LookRotation(forward, inward) * Quaternion.Inverse(boneLocalBasis);
        animator.SetIKRotation(goal, boneWanted * Quaternion.Inverse(f.Offset));
        animator.SetIKRotationWeight(goal, twoHandIK * PalmWeight);
    }

    // 손뼈 기준 손바닥 축·손가락 축: 바인드 자세에서 손바닥 = 아래, 손가락 = 바깥(오른손 +x, 왼손 −x).
    private PalmFrame MeasurePalm(Transform hand, bool right, PalmFrame f)
    {
        foreach (Renderer r in CharacterModelSwap.BodyRenderers(transform))
        {
            if (!(r is SkinnedMeshRenderer smr) || smr.sharedMesh == null) continue;
            int i = System.Array.IndexOf(smr.bones, hand);
            if (i < 0 || i >= smr.sharedMesh.bindposes.Length) continue;
            Quaternion bind = smr.sharedMesh.bindposes[i].inverse.rotation; // 메시 공간에서 바인드 때 손뼈 회전
            Vector3 down = smr.transform.InverseTransformDirection(-transform.up).normalized;
            Vector3 outward = smr.transform.InverseTransformDirection(transform.right * (right ? 1f : -1f)).normalized;
            Vector3 palm = Quaternion.Inverse(bind) * down, finger = Quaternion.Inverse(bind) * outward;
            palm = Vector3.ProjectOnPlane(palm, finger).normalized;
            if (palm.sqrMagnitude < 0.5f) continue;
            f.Palm = palm; f.Finger = finger; f.Known = true;
            return f;
        }
        return f;
    }

    private const string CarryLayerName = "Carry";
    private const float PalmWeight = 0.7f;
    private const float BodyHalfWidth = 0.12f;
    // private const float BodyBand = 0.12f; // §88 첫 판(손 높이 ±0.12) — 아이템 높이 범위(gripSize.Below·Above)로 바뀜
    private ItemGripSize gripSize = ItemGripSize.Default;
    private float gripInset = -0.02f;
    private TwoHandGrip.Result grip;
    private bool gripSolved;
    private bool bodyFrontMeasured;
    private float bodyFrontZ;
    private const float ProfileStep = 0.025f;
    private readonly float[] frontProfile = new float[120]; // 0 ~ 3 m
    private float frontFallbackZ;
    private TwoHandGrip.FitParams fit = TwoHandGrip.FitParams.Normal;
    private PalmFrame palmR, palmL;

    // §87 뒤 임시 코드(어깨 폭 기준 손 올리기) — §88 TwoHandGrip으로 바뀌어 쓰지 않는다.
    // // ---------------- 두 손 아이템 들기(§88) ----------------
    // // Carry 층 클립(Cookie_Carrying)은 쿠키 몸에 맞춘 자세라, 팔이 짧은 머리 큰 캐릭터는 손이 가슴보다 아래(배·허리)로 처져 아이템이 몸에 붙었다.
    // // 두 손 아이템을 드는 동안 손을 어깨에서 팔 길이 비율로 앞·조금 아래에 둔다 — 캐릭터마다 팔 길이·어깨 폭에 맞는다.
    // private void TwoHandedIK()
    // {
    //     bool on = model != null && !isHammer && !isGlove && holdType == HoldType.TwoHanded && rightUpperArm != null && leftUpperArm != null;
    //     float dt = Application.isPlaying ? Time.deltaTime : 1f;
    //     twoHandIK = Mathf.MoveTowards(twoHandIK, on ? 1f : 0f, dt / HammerIKBlendSeconds);
    //     animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, twoHandIK);
    //     animator.SetIKPositionWeight(AvatarIKGoal.RightHand, twoHandIK);
    //     if (twoHandIK <= 0f || !on) return;
    //     RaiseHand(AvatarIKGoal.RightHand, rightUpperArm, rightForearm, rightHand);
    //     RaiseHand(AvatarIKGoal.LeftHand, leftUpperArm, leftForearm, leftHand);
    // }
    //
    // private void RaiseHand(AvatarIKGoal goal, Transform upper, Transform lower, Transform hand)
    // {
    //     if (upper == null || lower == null || hand == null) return;
    //     float arm = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
    //     Vector3 shoulder = transform.InverseTransformPoint(upper.position);
    //     animator.SetIKPosition(goal, transform.TransformPoint(TwoHandTarget(shoulder, arm)));
    // }
    //
    // // 어깨(몸 기준) → 손 목표: 좌우는 어깨 폭 × TwoHandWidth, 높이는 어깨에서 팔 길이 × TwoHandDrop 아래, 앞은 팔 길이 × TwoHandReach.
    // public static Vector3 TwoHandTarget(Vector3 shoulderLocal, float arm) =>
    //     new Vector3(shoulderLocal.x * TwoHandWidth, shoulderLocal.y - arm * TwoHandDrop, shoulderLocal.z + arm * TwoHandReach);
    //
    // (CarryLayerName은 위 §88 코드로 옮김)
    // public const float TwoHandDrop = 0.12f;  // 어깨에서 팔 길이 × 0.12 아래 — 팔을 거의 어깨 높이로 든다
    // public const float TwoHandReach = 0.9f;  // 어깨에서 팔 길이 × 0.9 앞 — 아이템이 몸에서 떨어져 앞에 뜬다
    // public const float TwoHandWidth = 0.9f;  // 어깨 폭 × 0.9(아이템 양옆을 잡는다)

    private float twoHandIK;
    private int carryLayerIndex = -1;
    private Transform rightUpperArm, leftUpperArm;

    private const float HammerIKBlendSeconds = 0.12f;
    private static readonly Vector3 HoldShaft = new Vector3(0.8f, 0.6f, 0.3f).normalized; // 몸 기준: 오른쪽·위·조금 앞 대각선(머리가 얼굴 옆에 온다)
    private static readonly Vector3 HoldLowerHandShift = new Vector3(-0.04f, -0.12f, 0.06f); // 클립 왼손 자리에서 배 쪽으로 내림
    private float hammerIK;
    private int actionLayerIndex = -1;

    // 두 손이 자루 끝을 쥔다: 왼손(아래 손)이 자루 끝에서 HammerEndMargin 위, 오른손은 그 위로 지금 손 간격만큼(§87).
    // 예전에는 오른손이 자루 아래 40 %(머리 바로 아래)를 쥐어 손 밖으로 자루 끝이 0.4 m 튀어나왔다.
    private void ApplyHammerGrip(float handGap)
    {
        if (model == null) return;
        float y = -(handGap + HammerEndMargin) - hammerBottomModel; // 소켓(오른손) 기준 모델 위치: 자루 끝 = 오른손 - (손 간격 + 여유)
        model.transform.localPosition = Vector3.up * y;
        hammerHeadLocal = Vector3.up * (hammerHeadModel + y);
    }

    private const float HammerEndMargin = 0.06f;      // 왼손 손목 뼈에서 자루 끝까지(주먹 안에 끝이 들어갈 만큼)
    private const float DefaultHammerHandGap = 0.28f; // 손뼈를 못 읽을 때 쓰는 두 손 간격(쿠키 HammerHold 잰 값)
    private float hammerBottomModel, hammerHeadModel;
    private const float HammerHeadFromBottom = 0.78f; // 머리 가운데: 아래에서 78 %(ITEM_Hammer 머리 y 0.07~0.33)
    private const float MinHandGap = 0.05f;
    private bool isHammer, isGlove;
    private PlayerActionPose pose;
    private Vector3 hammerShaft = Vector3.up;
}
