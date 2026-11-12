using Photon.Pun;
using UnityEngine;

// 들린 쿠키(로컬 소유자) 쪽의 캐리 추종 — HideOrSeekPlayer에서 분리한 협력 클래스(research.md §12 E6).
// CharacterGroundDetector/PlayerAnimationDriver와 같은 "조정자(MonoBehaviour)가 소유하는 순수 C# 클래스" 스타일이다.
// 소유권 이전 없이 자기 PhotonView를 유지한 채, 드는 쪽 두 손 위치를 매 물리 스텝 로컬로 따라간다(GameRule.md §4.1).
// 보이는 몸은 CarriedBodyPresenter가 매 프레임(LateUpdate) 손 위에 다시 놓는다 — 여기는 물리 몸(Rigidbody) 위치만(Request1009Plan.md §16.3).
public class PlayerCarryFollower
{
    // 들린 쿠키는 드는 쪽 몸 앞(두 손 위)에 겹쳐 있으므로, 그 자리에서 충돌을 되살리면 물리 엔진이 겹침을
    // 풀면서 위로 튕겨냈다(Play Mode 실측: 내려놓는 순간 y=1.4 → 4.75). 두 캡슐 반지름 합(약 0.92m)보다 먼
    // 드는 쪽 정면에 먼저 내려놓은 뒤 충돌을 복원한다.
    private const float ReleaseDropDistance = 1.2f;
    public static float ReleaseDistance => ReleaseDropDistance;

    private readonly GameObject self;
    private readonly Rigidbody rb;
    private readonly CarriedBodyPresenter body;
    private int carrierViewId = -1;
    private GameObject carrierObject; // 충돌 무시를 되돌릴 때 쓰는 캐시

    public bool IsCarried => carrierViewId >= 0;
    public int CarrierViewId => carrierViewId;

    public PlayerCarryFollower(GameObject self, Rigidbody rb, CarriedBodyPresenter body)
    {
        this.self = self;
        this.rb = rb;
        this.body = body;
    }

    // 드는 쪽에 붙는다. 들린 동안에는 위치를 직접 대입하므로 물리 시뮬레이션(중력·충돌 반발)을 끄고,
    // 드는 쪽 몸과 서로 밀어내지 않도록 두 캐릭터 사이 충돌을 무시한다(research.md §8.6).
    public bool TryAttach(int newCarrierViewId)
    {
        PhotonView carrierPv = PhotonView.Find(newCarrierViewId);
        if (carrierPv == null) return false;

        carrierViewId = newCarrierViewId;
        carrierObject = carrierPv.gameObject;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        PlayerGrabController.SetCollisionIgnored(self, carrierObject, true);
        return true;
    }

    // 드는 쪽 정면에 내려놓고 충돌을 복원한다. restorePhysics=false(파괴 상태)면 키네마틱을 유지한다.
    // 실제로 들려 있었으면 true.
    // dropAt: 드는 사람 화면이 정한 내려놓을 자리(§71 B1·§72 E6 — 들린 사람 화면의 드는 사람은 지연만큼 뒤에 있거나 탈것으로 이미 순간이동했을 수 있다).
    // 없거나 지금 자리에서 너무 멀면 예전처럼 이 화면의 드는 사람 앞에 내려놓는다.
    public const float MaxDropOffset = 6f;

    public bool Detach(bool restorePhysics, Vector3? dropAt = null)
    {
        if (carrierViewId < 0) return false;

        if (carrierObject != null)
        {
            Transform carrier = carrierObject.transform;
            Vector3 dropPos = carrier.position + carrier.forward * ReleaseDropDistance;
            if (dropAt.HasValue && (dropAt.Value - rb.position).sqrMagnitude <= MaxDropOffset * MaxDropOffset) dropPos = dropAt.Value;
            Quaternion upright = CarryPose.UprightFacing(dropPos, carrier.position); // 눕혀 들었던 몸을 세워 드는 쪽을 보게
            rb.position = dropPos;
            rb.rotation = upright;
            self.transform.SetPositionAndRotation(dropPos, upright);
            Physics.SyncTransforms();
            PlayerGrabController.SetCollisionIgnored(self, carrierObject, false);
        }

        carrierViewId = -1;
        carrierObject = null;

        if (restorePhysics)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
        }
        return true;
    }

    // 매 FixedUpdate 호출. 드는 쪽 두 손 위로 이동했으면 true, 드는 쪽이 사라졌으면(방 퇴장·파괴) false —
    // 이때 호출부가 Detach로 스스로 내려와야 공중에 고정된 채 남지 않는다.
    public bool TryFollow()
    {
        if (carrierViewId < 0) return false;
        if (body == null || !body.TryPose(carrierViewId, out Vector3 position, out Quaternion rotation)) return false;

        rb.position = position;
        rb.rotation = rotation;
        self.transform.SetPositionAndRotation(position, rotation);
        return true;
    }
}
