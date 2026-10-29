using UnityEngine;

// 마녀 팔 내리기(2026-10-29 사용자 지시): 마녀 모델(WITCH_Scarecrow)은 리깅을 쉽게 하려고 팔을 옆으로 편 T-Pose로 만들어졌고,
// 대기·걷기·일어서기 클립도 팔이 그 자세 그대로다. 게임에서는 늘 팔을 내린 대기 자세여야 하므로, Animator가 자세를 정한 뒤(LateUpdate)
// 위팔이 아래(살짝 바깥·앞)를 향하게 돌린다. 클립의 작은 흔들림은 아래팔·손에 그대로 남는다.
// 내리치기(Strike)는 팔을 머리 위로 들었다 내리치는 동작이라 그 동안에는, 팔이 옆으로 수평에 가깝게 벌어진(T자) 순간에만 내린다.
[DefaultExecutionOrder(500)]
public class WitchArmsDown : MonoBehaviour
{
    private const float BlendSpeed = 4f;
    private static readonly int StrikeState = Animator.StringToHash("Strike");

    private Animator animator;
    private Transform chest;
    private Arm left, right;
    private float weight = 1f;

    private struct Arm
    {
        public Transform Upper, Fore;
        public bool Valid => Upper != null && Fore != null;
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        chest = Find(transform, "Chest");
        left = new Arm { Upper = Find(transform, "UpperArm.L"), Fore = Find(transform, "ForeArm.L") };
        right = new Arm { Upper = Find(transform, "UpperArm.R"), Fore = Find(transform, "ForeArm.R") };
    }

    private void LateUpdate()
    {
        if (chest == null) return;
        bool striking = animator != null && animator.isActiveAndEnabled && IsStriking();
        weight = Mathf.MoveTowards(weight, striking ? 0f : 1f, BlendSpeed * Time.deltaTime);
        Lower(left);
        Lower(right);
    }

    private bool IsStriking()
    {
        AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(0);
        if (now.shortNameHash == StrikeState) return true;
        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == StrikeState;
    }

    // 위팔 방향(위팔 → 아래팔)을 아래 + 바깥 0.3 + 앞 0.12 쪽으로.
    private void Lower(Arm arm)
    {
        if (!arm.Valid) return;
        Vector3 current = arm.Fore.position - arm.Upper.position;
        if (current.sqrMagnitude < 1e-8f) return;
        Vector3 outward = Vector3.ProjectOnPlane(arm.Upper.position - chest.position, Vector3.up);
        outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : Vector3.zero;
        // 내리치는 중(weight < 1): 옆으로 벌어진 만큼만(바깥 방향 0.6 → 0, 0.9 이상 → 1)
        float amount = Mathf.Lerp(Mathf.InverseLerp(0.6f, 0.9f, Vector3.Dot(current.normalized, outward)), 1f, weight);
        Vector3 desired = (Vector3.down + outward * 0.3f + transform.forward * 0.12f).normalized;
        Quaternion turn = Quaternion.FromToRotation(current.normalized, desired);
        arm.Upper.rotation = Quaternion.Slerp(Quaternion.identity, turn, amount) * arm.Upper.rotation;
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform t = Find(root.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }
}
