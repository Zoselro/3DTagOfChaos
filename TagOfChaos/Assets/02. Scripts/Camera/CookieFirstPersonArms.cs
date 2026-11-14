using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 쿠키 1인칭 조준 화면의 팔(Request1009Plan.md §77 FP1 — 사용자: FPS처럼 손과 든 아이템이 보이게).
// 몸 메시에서 팔(휴머노이드 LeftUpperArm·RightUpperArm과 그 아래 뼈: 아래팔·손·손가락)에 주로 붙은 삼각형만 골라
// 같은 뼈·같은 재질의 메시를 한 번 만든다(MonsterFirstPersonLimbs와 같은 방식). 뼈를 함께 쓰므로 조준 자세·던지기·반동이 그대로 따라간다.
// 본인 화면에서만 켠다 — 다른 사람은 원래 몸 전체를 본다.
public static class CookieFirstPersonArms
{
    private static readonly Dictionary<Mesh, Mesh> cache = new Dictionary<Mesh, Mesh>(); // 원본 메시 → 팔만 남긴 메시

    // body와 같은 자리에 팔만 그리는 렌더러. 휴머노이드가 아니거나 메시를 읽을 수 없거나 팔이 없으면 null.
    public static SkinnedMeshRenderer Create(SkinnedMeshRenderer body, Animator animator)
    {
        if (body == null || animator == null || !animator.isHuman) return null;
        Mesh arms = ArmMesh(body, animator);
        if (arms == null) return null;
        var go = new GameObject(body.name + "_FPArms");
        // 캐릭터 뿌리 바로 아래에 둔다 — 모델 아래에 두면 CharacterModelSwap.BodyRenderers에 섞인다. 스키닝은 뼈 위치만 쓰므로 자리는 상관없다.
        go.transform.SetParent(animator.transform, false);
        go.layer = body.gameObject.layer;
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = arms;
        smr.bones = body.bones;
        smr.rootBone = body.rootBone;
        smr.sharedMaterials = body.sharedMaterials;
        smr.updateWhenOffscreen = true; // 카메라가 몸 안에 있어 경계 상자 판정이 틀리기 쉽다
        smr.shadowCastingMode = ShadowCastingMode.Off; // 그림자는 원래 몸(그림자만)이 낸다
        smr.receiveShadows = body.receiveShadows;
        go.SetActive(false);
        return smr;
    }

    // 팔 뼈: 위팔 두 개와 그 아래 모든 뼈.
    public static bool[] ArmBones(Transform[] bones, Transform leftUpper, Transform rightUpper)
    {
        var keep = new bool[bones != null ? bones.Length : 0];
        for (int i = 0; i < keep.Length; i++)
        {
            Transform b = bones[i];
            keep[i] = b != null && ((leftUpper != null && b.IsChildOf(leftUpper)) || (rightUpper != null && b.IsChildOf(rightUpper)));
        }
        return keep;
    }

    private static Mesh ArmMesh(SkinnedMeshRenderer body, Animator animator)
    {
        Mesh src = body.sharedMesh;
        if (src == null) return null;
        if (cache.TryGetValue(src, out Mesh cached)) return cached;
        if (!src.isReadable)
        {
            Debug.LogWarning($"[CookieFirstPersonArms] Mesh {src.name} is not readable — enable Read/Write on the model to show arms in first person.");
            cache[src] = null;
            return null;
        }
        bool[] keepBone = ArmBones(body.bones, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), animator.GetBoneTransform(HumanBodyBones.RightUpperArm));
        BoneWeight[] weights = src.boneWeights;
        var keepVertex = new bool[weights.Length];
        for (int v = 0; v < weights.Length; v++) keepVertex[v] = weights[v].boneIndex0 < keepBone.Length && keepBone[weights[v].boneIndex0];

        Mesh m = Object.Instantiate(src);
        m.name = src.name + "_FPArms";
        int kept = 0;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] tris = src.GetTriangles(s);
            var outTris = new List<int>(tris.Length / 8);
            for (int t = 0; t + 2 < tris.Length; t += 3)
                if (keepVertex[tris[t]] && keepVertex[tris[t + 1]] && keepVertex[tris[t + 2]])
                {
                    outTris.Add(tris[t]); outTris.Add(tris[t + 1]); outTris.Add(tris[t + 2]);
                }
            m.SetTriangles(outTris, s);
            kept += outTris.Count / 3;
        }
        if (kept == 0) { Object.Destroy(m); m = null; }
        cache[src] = m;
        return m;
    }
}
