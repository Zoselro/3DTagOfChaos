using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 술래 본인 1인칭 화면의 팔·촉수(Request1009Plan.md §69 F4 — 사용자: 몸통·머리는 지우고 기존 촉수와 팔만 보이게, 상대 화면은 다 보이게).
// 괴물 몸 메시에서 팔(UpperArm·Forearm·Hand)과 촉수(Tentacle) 뼈에 주로 붙은 삼각형만 골라 같은 뼈·같은 재질의 메시를 한 번 만들고,
// 1인칭일 때만 켠다. 팔과 촉수는 메시를 따로 만들어 촉수는 동작(잡기·돌진·스킬) 중에만 켠다(§74 P15). 원래 몸은 MonsterViewSwitcher가 본인 화면에서 그림자만 남긴다. 뼈를 함께 쓰므로 동작(대기·걷기·잡기·돌진)이 그대로 따라간다.
public static class MonsterFirstPersonLimbs
{
    public enum Part { Arms, Tentacles }

    private static readonly Dictionary<(Mesh, Part), Mesh> cache = new Dictionary<(Mesh, Part), Mesh>(); // 원본 메시·부위 → 그 부위만 남긴 메시

    public static bool IsArmBone(string name) => name.StartsWith("UpperArm_") || name.StartsWith("Forearm_") || name.StartsWith("Hand_");
    public static bool IsTentacleBone(string name) => name.StartsWith("Tentacle_");
    public static bool IsLimbBone(string name) => IsArmBone(name) || IsTentacleBone(name);
    public static bool IsPartBone(string name, Part part) => part == Part.Arms ? IsArmBone(name) : IsTentacleBone(name);

    // body와 같은 자리에 그 부위(팔 또는 촉수)만 그리는 렌더러를 만든다. 메시를 읽을 수 없거나 그 부위가 없으면 null.
    public static SkinnedMeshRenderer Create(SkinnedMeshRenderer body, Part part)
    {
        Mesh limbs = LimbMesh(body, part);
        if (limbs == null) return null;
        var go = new GameObject(body.name + (part == Part.Arms ? "_FPArms" : "_FPTentacles"));
        go.transform.SetParent(body.transform.parent, false);
        go.transform.localPosition = body.transform.localPosition;
        go.transform.localRotation = body.transform.localRotation;
        go.transform.localScale = body.transform.localScale;
        go.layer = body.gameObject.layer;
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = limbs;
        smr.bones = body.bones;
        smr.rootBone = body.rootBone;
        smr.sharedMaterials = body.sharedMaterials;
        smr.updateWhenOffscreen = true; // 카메라가 몸 안에 있어 경계 상자 판정이 틀리기 쉽다
        smr.shadowCastingMode = ShadowCastingMode.Off;
        smr.receiveShadows = body.receiveShadows;
        go.SetActive(false);
        return smr;
    }

    private static Mesh LimbMesh(SkinnedMeshRenderer body, Part part)
    {
        Mesh src = body.sharedMesh;
        if (src == null) return null;
        if (cache.TryGetValue((src, part), out Mesh cached) && cached != null) return cached;
        if (!src.isReadable)
        {
            Debug.LogWarning($"[MonsterFirstPersonLimbs] Mesh {src.name} is not readable — enable Read/Write on the model to show arms in first person.");
            return null;
        }
        Transform[] bones = body.bones;
        var keepBone = new bool[bones.Length];
        for (int i = 0; i < bones.Length; i++) keepBone[i] = bones[i] != null && IsPartBone(bones[i].name, part);
        BoneWeight[] weights = src.boneWeights;
        var keepVertex = new bool[weights.Length];
        for (int v = 0; v < weights.Length; v++) keepVertex[v] = weights[v].boneIndex0 < keepBone.Length && keepBone[weights[v].boneIndex0];

        Mesh m = Object.Instantiate(src);
        m.name = src.name + "_FP" + part;
        int kept = 0;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] tris = src.GetTriangles(s);
            var outTris = new List<int>(tris.Length / 3);
            for (int t = 0; t + 2 < tris.Length; t += 3)
                if (keepVertex[tris[t]] && keepVertex[tris[t + 1]] && keepVertex[tris[t + 2]])
                {
                    outTris.Add(tris[t]); outTris.Add(tris[t + 1]); outTris.Add(tris[t + 2]);
                }
            m.SetTriangles(outTris, s);
            kept += outTris.Count / 3;
        }
        if (kept == 0) { Object.Destroy(m); return null; }
        cache[(src, part)] = m;
        return m;
    }
}
