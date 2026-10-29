using Photon.Pun;
using UnityEngine;

// 다른 쿠키에게 들린 몸을 모든 화면에서 드는 쪽 두 손 위에 놓는다(Request1009Plan.md §16.3).
// 예전에는 들린 사람 화면만 FixedUpdate에서 위치를 맞추고 다른 화면은 그 위치를 다시 보간해 달릴수록 뒤로 처졌다.
// 지금은 각 화면이 자기 화면의 드는 쪽 손 위치로 애니메이션이 끝난 뒤(LateUpdate) 맞춘다 — 카메라(Camera_Ctrl.LateUpdate)보다 먼저.
[DefaultExecutionOrder(-50)]
public class CarriedBodyPresenter : MonoBehaviour
{
    // 남의 화면에서는 내려놓은 위치가 동기화되기 전 겹쳐 있어, 접촉을 바로 되살리면 그 자리의 캐릭터가 밀려난다.
    private const float RemoteRestoreDelay = 0.5f;
    private static readonly string[] CharacterLayers = { "Cookie", "Monster" };

    private HideOrSeekPlayer self;
    private Transform hips;
    private Animator animatorRef;
    private Avatar boneAvatar;
    private CapsuleCollider rootCapsule;
    private LayerMask normalExclude;
    private LayerMask carriedExclude;
    private bool carriedCollisionOff;
    private float restoreAt = -1f;

    public void Init(HideOrSeekPlayer owner, Animator animator)
    {
        self = owner;
        animatorRef = animator;
        boneAvatar = animator != null ? animator.avatar : null;
        hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        rootCapsule = GetComponent<CapsuleCollider>();
        normalExclude = rootCapsule != null ? rootCapsule.excludeLayers : (LayerMask)0;
        carriedExclude = normalExclude | LayerMask.GetMask(CharacterLayers);
    }

    private void LateUpdate()
    {
        if (self == null) return;
        int carrier = self.CurrentCarrierViewId;
        UpdateCarriedCollision(carrier >= 0);
        if (carrier < 0) return;
        if (TryPose(carrier, out Vector3 position, out Quaternion rotation))
            transform.SetPositionAndRotation(position, rotation);
    }

    // 드는 쪽(carrierViewId)의 지금 손 위치로 이 몸의 루트 위치·회전을 계산한다. 드는 쪽이 사라졌으면 false.
    public bool TryPose(int carrierViewId, out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;
        PhotonView carrierView = PhotonView.Find(carrierViewId);
        if (carrierView == null) return false;
        Transform carrier = carrierView.transform;
        Vector3 hands = CarryPose.HandsCenter(carrier, carrierView.GetComponent<Animator>());
        rotation = CarryPose.CarriedRotation(carrier.rotation);
        if (animatorRef != null && animatorRef.avatar != boneAvatar) // 캐릭터 모델이 바뀌면 엉덩이 뼈를 다시 찾는다(§45 CH6)
        {
            boneAvatar = animatorRef.avatar;
            hips = animatorRef.isHuman ? animatorRef.GetBoneTransform(HumanBodyBones.Hips) : null;
        }
        Vector3 hipsLocal = hips != null
            ? Quaternion.Inverse(transform.rotation) * (hips.position - transform.position)
            : new Vector3(0f, 0.8f, 0f);
        position = CarryPose.RootFor(CarryPose.CarriedHips(hands, carrier.rotation), rotation, hipsLocal);
        return true;
    }
    // 들린 몸은 어떤 캐릭터(쿠키·괴물)와도 부딪히지 않는다 — 모든 화면에서(Request1009Plan.md §22.6 M5). 눕혀 들린 캡슐이 드는 쪽 앞뒤로
    // 튀어나와 지나가는 캐릭터를 막았다. 바닥(Default)은 빼지 않는다 — 본인 화면은 내려놓는 순간 물리가 다시 켜진다.
    private void UpdateCarriedCollision(bool carried)
    {
        if (rootCapsule == null) return;
        if (carried)
        {
            restoreAt = -1f;
            if (carriedCollisionOff) return;
            carriedCollisionOff = true;
            rootCapsule.excludeLayers = carriedExclude;
            return;
        }
        if (!carriedCollisionOff) return;
        if (restoreAt < 0f) restoreAt = Time.time + (self.View != null && self.View.IsMine ? 0f : RemoteRestoreDelay);
        if (Time.time < restoreAt) return;
        carriedCollisionOff = false;
        restoreAt = -1f;
        rootCapsule.excludeLayers = normalExclude;
    }
}
