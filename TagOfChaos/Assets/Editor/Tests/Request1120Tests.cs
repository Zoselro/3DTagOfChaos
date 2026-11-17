using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Request1009Plan.md §84 — 얼굴 없는 캐릭터 4종으로 교체(같은 뼈대 25개·휴머노이드·이름 그대로).
public class Request1120Tests
{
    private static GameObject Prefab(string n) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterBuilder.PrefabFolder}/CHR_{n}.prefab");

    // 프리팹마다 정상 휴머노이드 아바타, 몸 메시 이름 = <이름>_Body, 뼈대 뿌리 = Armature(코드·동작이 이름으로 찾는다).
    [Test]
    public void Characters_HumanoidWithSameNames()
    {
        foreach (string n in CharacterBuilder.Names)
        {
            GameObject p = Prefab(n);
            Assert.IsNotNull(p, n);
            Animator a = p.GetComponent<Animator>();
            Assert.IsTrue(a != null && a.avatar != null && a.avatar.isHuman && a.avatar.isValid, n + ": humanoid avatar");
            Assert.IsFalse(a.applyRootMotion, n + ": no root motion");
            SkinnedMeshRenderer smr = p.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.AreEqual(n + "_Body", smr.sharedMesh.name, n + ": body mesh name");
            Assert.IsNotNull(p.transform.Find("Armature"), n + ": Armature root");
            Assert.AreEqual(25, smr.bones.Length, n + ": 25 bones");
            Assert.IsTrue(smr.sharedMesh.isReadable, n + ": readable (first-person arms)");
            Assert.Less(smr.sharedMesh.triangles.Length / 3, 23000, n + ": triangle budget");
        }
    }

    // 1인칭 팔 메시에 머리·몸 조각이 섞이지 않는다: 팔 뼈가 주인 꼭짓점은 어깨보다 18 cm 위로 가지 않는다.
    [Test]
    public void ArmWeights_StayOnArms()
    {
        foreach (string n in CharacterBuilder.Names)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(n));
            try
            {
                Animator a = go.GetComponent<Animator>();
                SkinnedMeshRenderer smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                bool[] keep = CookieFirstPersonArms.ArmBones(smr.bones, a.GetBoneTransform(HumanBodyBones.LeftUpperArm), a.GetBoneTransform(HumanBodyBones.RightUpperArm));
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                Vector3[] v = baked.vertices;
                BoneWeight[] w = smr.sharedMesh.boneWeights;
                float shoulder = a.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y;
                int arm = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    if (w[i].boneIndex0 >= keep.Length || !keep[w[i].boneIndex0]) continue;
                    arm++;
                    Assert.Less(smr.transform.TransformPoint(v[i]).y, shoulder + 0.18f, n + ": arm vertex above the shoulder");
                }
                Assert.Greater(arm, 500, n + ": arm vertices");
                Object.DestroyImmediate(baked);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }

    // §85 캐릭터 시험: 다음·이전 캐릭터 번호가 끝에서 처음으로 돈다.
    [Test]
    public void OfflineCharacterTest_CyclesThroughCatalog()
    {
        int count = GameSettings.Current.CharacterCatalog.Count;
        Assert.AreEqual(5, count, "cookie + 4 characters");
        Assert.AreEqual(1, OfflineModeBootstrap.NextCharacter(0, 1, count));
        Assert.AreEqual(0, OfflineModeBootstrap.NextCharacter(count - 1, 1, count), "wraps to the cookie");
        Assert.AreEqual(count - 1, OfflineModeBootstrap.NextCharacter(0, -1, count), "Shift wraps back to the last");
        Assert.AreEqual(0, OfflineModeBootstrap.NextCharacter(3, 1, 0), "no catalog");
    }
}
