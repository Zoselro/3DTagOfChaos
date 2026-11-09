using System.Collections.Generic;
using UnityEngine;

// 인라인 스케이트를 발에(Request1009Plan.md §69 B1) — 스케이트 효과(HideOrSeekPlayer.IsSpeedBoosted, 명중 알림으로 모든 화면이 안다) 동안 양발 신발 아래에
// 롤러 프레임(Resources/Escape/SkatesFoot, Blender ITEM_Skates_Foot — 단위 크기)을 끼운다. 캐릭터마다 신발 모양이 달라, 발 뼈에 붙은 정점으로 신발 길이·폭·바닥을 재서
// 그 크기로 늘리고, 바퀴 높이만큼 몸(엉덩이 뼈)을 들어 올린다. 발이 가장 낮게 내려온 순간 붙인다(걷는 중에 써도 어긋나지 않게).
// 효과가 끝나거나 몸이 숨으면(탈것·파괴·탈출) 치우고 몸을 내린다.
public class SkateFeetView : MonoBehaviour
{
    public const string PrefabPath = "Escape/SkatesFoot";
    private const float WheelDepth = 0.2f;      // 모델: 바퀴 바닥 = 발판 윗면 아래 (신발 길이 × 이 값)
    private const float Margin = 1.06f;         // 신발보다 조금 크게
    private const float MinShoe = 0.08f;
    private const float MaxLift = 0.25f;
    private const float LowestSlack = 0.012f;   // 지금까지 가장 낮았던 발 높이 + 이만큼 안이면 붙인다
    private const float AttachDeadline = 0.6f;  // 이 시간 안에 낮게 내려오지 않으면 그대로 붙인다

    private static GameObject prefab;
    private static bool prefabLoaded;

    private struct FootPoint { public Transform Bone; public Vector3 Local; }

    private HideOrSeekPlayer cookie;
    private Animator animator;
    private readonly Transform[] skates = new Transform[2];
    private readonly float[] lowest = new float[2];
    private readonly List<FootPoint>[] footPoints = { new List<FootPoint>(), new List<FootPoint>() };
    private Transform pointsBuiltFor; // 정점을 모은 왼발 뼈(모델이 바뀌면 다시 모은다)
    private float boostSince = -1f;
    private float lift;               // 이번 효과 동안 들어 올리는 높이(m)
    private int liftedFrames;

    public void Init(HideOrSeekPlayer owner)
    {
        cookie = owner;
        animator = owner.GetComponent<Animator>();
    }

    public bool IsShowing => skates[0] != null || skates[1] != null;
    public float Lift => lift;

    private void Update()
    {
        if (cookie == null) return;
        if (!ShouldShow() || animator == null || !animator.isHuman)
        {
            Clear();
            return;
        }
        if (boostSince < 0f && !Begin()) return;
        if (liftedFrames < 2) return; // 들어 올린 몸으로 재야 바퀴가 땅에 닿는다
        for (int side = 0; side < 2; side++)
            if (skates[side] == null) TryAttach(side);
    }

    // 애니메이터 IK 단계에서 몸을 바퀴 높이만큼 올린다(PlayerAnimator Base Layer의 IK Pass) — LateUpdate에서 뼈를 옮기면 이 프로젝트는 스키닝에 반영되지 않는다.
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || lift <= 0f || animator == null) return;
        animator.bodyPosition += transform.up * lift;
        liftedFrames++;
    }

    private bool ShouldShow()
    {
        if (!cookie.IsSpeedBoosted || cookie.IsBroken || cookie.View == null || cookie.View.Owner == null) return false;
        int actor = cookie.View.Owner.ActorNumber;
        return !EscapeManager.HasLeftMap(actor) && !EscapeManager.IsRidingVehicle(actor);
    }

    private bool Begin()
    {
        if (!CollectFootPoints()) return false;
        if (!ShoeBox(0, out _, out float lengthL, out _, out float bottomL) || !ShoeBox(1, out _, out float lengthR, out _, out float bottomR)) return false;
        boostSince = Time.time;
        lowest[0] = lowest[1] = float.MaxValue;
        // 바퀴 높이만큼 + 신발이 보이는 바닥보다 묻혀 있으면 그만큼 더(진저브레드는 바닥 타일이 물리 바닥보다 6 cm 높다).
        float sunk = Mathf.Max(0f, VisibleGroundAbove(transform.position) - Mathf.Min(bottomL, bottomR));
        lift = Mathf.Min(Mathf.Max(lengthL, lengthR) * WheelDepth + sunk, MaxLift);
        liftedFrames = 0;
        return true;
    }

    private void TryAttach(int side)
    {
        Transform foot = animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        if (foot == null || !ShoeBox(side, out Vector3 centre, out float length, out float width, out float bottom)) return;
        lowest[side] = Mathf.Min(lowest[side], bottom);
        if (bottom > lowest[side] + LowestSlack && Time.time - boostSince < AttachDeadline) return;
        GameObject model = Prefab();
        if (model == null) return;

        Vector3 at = centre + transform.up * (bottom - Vector3.Dot(centre, transform.up)); // 신발 바닥 가운데
        Transform skate = Instantiate(model, at, Quaternion.LookRotation(transform.forward, transform.up)).transform;
        skate.name = side == 0 ? "Skate_L" : "Skate_R";
        skate.localScale = new Vector3((side == 0 ? -1f : 1f) * width, length, length); // 폭·높이·길이(높이는 길이 비율 — 바퀴가 둥글게)
        foreach (Collider c in skate.GetComponentsInChildren<Collider>()) Destroy(c);
        skate.SetParent(foot, true);
        skates[side] = skate;
    }

    // 신발(발 뼈와 그 아래 뼈에 주로 붙은 정점)을 캐릭터 방향으로 잰 상자: 가운데(월드), 앞뒤 길이, 좌우 폭, 바닥 높이(위 방향 성분).
    private bool ShoeBox(int side, out Vector3 centre, out float length, out float width, out float bottom)
    {
        centre = Vector3.zero; length = width = bottom = 0f;
        List<FootPoint> points = footPoints[side];
        if (points.Count == 0) return false;
        Vector3 f = transform.forward, r = transform.right, u = transform.up;
        float minF = float.MaxValue, maxF = float.MinValue, minR = float.MaxValue, maxR = float.MinValue, minU = float.MaxValue;
        foreach (FootPoint p in points)
        {
            if (p.Bone == null) return false;
            Vector3 w = p.Bone.TransformPoint(p.Local);
            float a = Vector3.Dot(w, f), b = Vector3.Dot(w, r), c = Vector3.Dot(w, u);
            if (a < minF) minF = a; if (a > maxF) maxF = a;
            if (b < minR) minR = b; if (b > maxR) maxR = b;
            if (c < minU) minU = c;
        }
        length = Mathf.Max(MinShoe, maxF - minF) * Margin;
        width = Mathf.Max(MinShoe * 0.6f, maxR - minR) * Margin;
        bottom = minU;
        centre = f * ((minF + maxF) * 0.5f) + r * ((minR + maxR) * 0.5f) + u * minU;
        return true;
    }

    // 몸 메시에서 발 뼈(발가락 포함)에 가장 크게 붙은 정점을 뼈 공간 위치로 모아 둔다(모델이 바뀔 때만 다시).
    private bool CollectFootPoints()
    {
        Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (left == null || right == null) return false;
        if (pointsBuiltFor == left && footPoints[0].Count > 0 && footPoints[1].Count > 0) return true;
        footPoints[0].Clear();
        footPoints[1].Clear();
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh mesh = smr.sharedMesh;
            Transform[] bones = smr.bones;
            if (mesh == null || bones == null || bones.Length == 0 || !mesh.isReadable) continue;
            Matrix4x4[] bind = mesh.bindposes;
            BoneWeight[] weights = mesh.boneWeights;
            Vector3[] verts = mesh.vertices;
            if (weights.Length != verts.Length) continue;
            for (int i = 0; i < verts.Length; i++)
            {
                BoneWeight bw = weights[i];
                if (bw.weight0 < 0.5f || bw.boneIndex0 >= bones.Length || bw.boneIndex0 >= bind.Length) continue;
                Transform bone = bones[bw.boneIndex0];
                if (bone == null) continue;
                int side = bone == left || bone.IsChildOf(left) ? 0 : bone == right || bone.IsChildOf(right) ? 1 : -1;
                if (side < 0) continue;
                footPoints[side].Add(new FootPoint { Bone = bone, Local = bind[bw.boneIndex0].MultiplyPoint3x4(verts[i]) });
            }
        }
        pointsBuiltFor = left;
        if (footPoints[0].Count == 0 || footPoints[1].Count == 0)
        {
            Debug.LogWarning($"[SkateFeetView] No foot vertices on {name} — skates not shown.");
            return false;
        }
        return true;
    }

    // 발밑의 보이는 바닥 높이(월드 y). 콜라이더 없는 바닥 타일(얇고 넓은 렌더러)을 씬마다 한 번 모아 두고, 발밑 ±GroundSearch 안에서 가장 높은 윗면.
    private const float GroundSearch = 0.2f;
    private static readonly List<Bounds> flatGround = new List<Bounds>();
    private static int flatGroundScene = -1;

    private static float VisibleGroundAbove(Vector3 feet)
    {
        int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if (flatGroundScene != scene)
        {
            flatGroundScene = scene;
            flatGround.Clear();
            foreach (MeshRenderer mr in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                Bounds b = mr.bounds;
                if (b.size.y < 0.02f && b.size.x > 1f && b.size.z > 1f) flatGround.Add(b);
            }
        }
        float best = feet.y;
        foreach (Bounds b in flatGround)
        {
            if (feet.x < b.min.x || feet.x > b.max.x || feet.z < b.min.z || feet.z > b.max.z) continue;
            float top = b.max.y;
            if (top > best && top - feet.y <= GroundSearch) best = top;
        }
        return best;
    }

    private void Clear()
    {
        boostSince = -1f;
        lift = 0f;
        liftedFrames = 0;
        for (int side = 0; side < 2; side++)
        {
            if (skates[side] != null) Destroy(skates[side].gameObject);
            skates[side] = null;
        }
    }

    private void OnDestroy() => Clear();

    private static GameObject Prefab()
    {
        if (prefabLoaded) return prefab;
        prefabLoaded = true;
        prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) Debug.LogWarning($"[SkateFeetView] Resources/{PrefabPath} not found — run TagOfChaos/Escape/Build Skates Foot Prefab.");
        return prefab;
    }
}
