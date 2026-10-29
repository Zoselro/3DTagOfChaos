using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 쿠키가 손에 든 아이템을 모든 클라이언트 화면에 보여준다(EscapePlan.md §1.9). 소유자의 고른 칸(SelectedSlot)과
// EscapeState로 무엇을 드는지 정한다. 손에 든 아이템이 겉으로 보이므로 누가 재료를 들고 있는지 모두가 볼 수 있다(스파이 추리 단서).
// 드는 모습(Request1009Plan.md §16.4):
// - 두 손 아이템: 다른 쿠키를 들 때와 같은 상체 두 손 자세(Carry 레이어)를 켜고, 두 손 뼈 가운데에 놓는다.
// - 한 손 아이템: 오른손 뼈를 따라간다(걷기·달리기의 팔 흔들림 그대로). 쿠키 손은 손가락 뼈가 없어 쥐는 자세는 따로 없다.
// 위치는 애니메이션이 끝난 뒤(LateUpdate) 맞추고, 방향은 몸 방향을 따른다(손목 회전에 아이템이 뒤집히지 않게).
public class HeldItemPresenter : MonoBehaviourPunCallbacks
{
    private const float OneHandedGripBelowTop = 0.12f; // 한 손 아이템: 모델 아래 끝에서 이만큼 위를 쥔다
    private static readonly Quaternion OneHandedTilt = Quaternion.Euler(35f, 0f, 0f); // 쥔 곳을 축으로 위쪽을 앞으로 기울인다(손잡이가 몸에 가려지지 않게)

    private EscapeManager manager;
    private PhotonView view;
    private HideOrSeekPlayer cookie;
    private Animator animator;
    private Transform rightHand;
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
    }

    public void Init(EscapeManager escape, PhotonView ownerView)
    {
        manager = escape;
        view = ownerView;
        cookie = GetComponent<HideOrSeekPlayer>();
        animator = GetComponent<Animator>();
        rightHand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
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
        if (cookie != null) cookie.SetCarry(CarryReason.TwoHandedItem, def != null && def.LocksSlotSwitch);
        if (def == null) return;

        holdType = def.HoldType;
        isHammer = def.IsTool && def.Tool.Kind == ToolKind.Hammer;
        bool balloon = def.IsTool && (def.Tool.Kind == ToolKind.WaterBalloon || def.Tool.Kind == ToolKind.JailBalloon);
        model = balloon ? WaterBubbleView.CreateSmall(socket, HeldBalloonSize) : EscapeVisuals.CreateItemModel(def, socket); // 물풍선은 갇힘 물방울과 같은 모양(§45.7 WB5)
        if (def.HeldScale != 1f) model.transform.localScale *= def.HeldScale; // 뿅망치는 들고 있는 내내 3배(§45.5)
        if (isHammer)
        {
            // 망치 머리(모델 X축으로 긴 원통)의 두드리는 면이 휘두르는 방향(소켓 앞, Z)을 보게 돌리고, 두 손이 자루 아래쪽을 잡게 올린다(§45.5 HM0).
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
            model.transform.localPosition += Vector3.up * HammerGripLift(model);
        }
        bool oneHanded = holdType == HoldType.OneHanded;
        holdRotation = (oneHanded ? OneHandedTilt : Quaternion.identity) * Quaternion.Euler(def.HoldEuler);
        holdOffset = def.HoldOffset;
        if (oneHanded) model.transform.localPosition += OneHandedGripOffset(model); // 쥔 곳이 소켓(손)에 오게 — 기울기는 쥔 곳을 축으로
        LateUpdate();
    }

    // 한 손 아이템은 아래쪽(손잡이)을 쥐도록 모델을 위로 올린다. 모델 기준점이 가운데여도 손잡이가 손에 오게(순수 계산은 GripLift).
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

    // 아래 끝(center - extent)에서 OneHandedGripBelowTop 위가 손에 오도록 올리는 높이.
    public static float GripLift(float centerY, float extentY) => extentY - OneHandedGripBelowTop - centerY;

    private void LateUpdate()
    {
        if (model == null) return;
        Quaternion body = transform.rotation;
        RefreshBones();
        Vector3 anchor = holdType == HoldType.TwoHanded || rightHand == null
            ? CarryPose.HandsCenter(transform, animator)
            : rightHand.position;
        Quaternion rotation = body * holdRotation;
        if (isHammer)
        {
            // 휘두르는 동안 자루가 가슴 → 두 손 방향을 따라 눕는다(머리 위로 들었다 앞 아래로 내리침). 들고만 있으면 세워 든다.
            if (pose == null) pose = GetComponent<PlayerActionPose>();
            Vector3 chest = transform.position + Vector3.up * HammerPivotHeight;
            Vector3 dir = anchor - chest;
            Vector3 up = pose != null && pose.IsSwinging && dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.up;
            hammerUp = Vector3.Slerp(hammerUp, up, Mathf.Clamp01(Time.deltaTime * 30f));
            rotation = Quaternion.LookRotation(Vector3.Cross(body * Vector3.right, hammerUp), hammerUp) * holdRotation;
        }
        socket.SetPositionAndRotation(anchor + body * holdOffset, rotation);
    }

    // 자루 아래에서 1/4쯤을 쥔다: 모델 가운데를 (반 높이 − 높이의 1/4)만큼 위로.
    private static float HammerGripLift(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b.extents.y * 0.5f - (b.center.y - go.transform.position.y);
    }

    private const float HeldBalloonSize = 0.38f;
    private const float HammerPivotHeight = 0.75f;
    private bool isHammer;
    private PlayerActionPose pose;
    private Vector3 hammerUp = Vector3.up;
}
