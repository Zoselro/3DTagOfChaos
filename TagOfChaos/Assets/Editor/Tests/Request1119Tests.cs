using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Request1009Plan.md §82 — 큐브 조각 없애기: 모든 물체가 Blender 조각 모델 + 구운 애니메이션으로 무너진다.
public class Request1119Tests
{
    private static FracturedModelSO Holder(string name) =>
        AssetDatabase.LoadAssetAtPath<FracturedModelSO>($"{DestructionRulesSetup.HolderFolder}/{name}.asset");

    // U1: 소품·텐트·계단·술래방 종류마다 조각 모델 + 바깥으로 터지는 애니메이션 하나, 건물마다 4방향 애니메이션(서로 다른 방향).
    [Test]
    public void EveryKind_HasModelAndBakedClip()
    {
        DestructionRulesSO rules = DestructionRulesSO.Current;
        Assert.GreaterOrEqual(rules.PropFracturedNames.Length, 96, "props/tents/stairs/rooms");
        foreach (string name in rules.PropFracturedNames)
        {
            FracturedModelSO so = Holder(name);
            Assert.IsNotNull(so, name + ": holder");
            Assert.IsNotNull(so.Model, name + ": model");
            Assert.AreEqual(1, so.ClipCount, name + ": one burst clip");
            Assert.Greater(so.Clip(0).length, 1.5f, name + ": clip length");
        }
        // Assert.AreEqual(38, rules.FracturedNames.Length, "buildings");
        Assert.AreEqual(45, rules.FracturedNames.Length, "buildings (+ §89.22 bakery 7)");
        foreach (string name in rules.FracturedNames)
        {
            FracturedModelSO so = Holder(name);
            Assert.IsNotNull(so, name + ": holder");
            Assert.AreEqual(FractureStyle.Building, so.Style, name + ": style");
            Assert.AreEqual(4, so.ClipCount, name + ": four direction clips");
            for (int i = 0; i < 4; i++)
            {
                Assert.Greater(so.Push(i).magnitude, 0.5f, name + ": clip " + i + " has a direction");
                for (int k = i + 1; k < 4; k++)
                    Assert.Less(Vector3.Dot(so.Push(i), so.Push(k)), 0.5f, name + $": clips {i}/{k} differ");
            }
        }
    }

    // U1: 맞은 방향에 가장 가까운 애니메이션을 고른다.
    [Test]
    public void ClipFor_PicksNearestPush()
    {
        FracturedModelSO so = Holder(DestructionRulesSO.Current.FracturedNames[0]);
        for (int i = 0; i < so.ClipCount; i++)
            Assert.AreSame(so.Clip(i), so.ClipFor(so.Push(i) + Vector3.Cross(so.Push(i), Vector3.forward) * 0.3f), "clip " + i);
        Assert.AreSame(so.Clip(0), so.ClipFor(Vector3.zero), "no direction → first");
    }

    [Test]
    public void KindName_StripsCopyAndNumberSuffixes()
    {
        Assert.AreEqual("CF_Bench", DestructibleCatalog.KindName("CF_Bench (3)"));
        Assert.AreEqual("GIN_Lollipop", DestructibleCatalog.KindName("GIN_Lollipop_02"));
        Assert.AreEqual("GIN_Lollipop", DestructibleCatalog.KindName("GIN_Lollipop_02 (1) (2)"));
        Assert.AreEqual("MO_Tent", DestructibleCatalog.KindName("MO_Tent"));
    }

    // U2·U3: 맵 5개 — 부서지는 목록의 모든 물체에 조각 모델이 있고(소품·텐트·계단은 종류 이름), 술래방 조각 모델도 있다.
    [Test]
    public void AllMaps_EveryDestructibleHasFractureModel()
    {
        DestructionRulesSO rules = DestructionRulesSO.Current;
        var kinds = new HashSet<string>(rules.PropFracturedNames);
        var buildings = new HashSet<string>(rules.FracturedNames);
        foreach (string path in DecoyNavMeshBaker.MapScenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Transform map = DestructibleCatalog.FindMap(scene);
                DestructibleCatalog cat = DestructibleCatalog.Build(map, rules);
                int withModel = 0, buildingNoModel = 0;
                foreach (DestructibleUnit u in cat.Units)
                {
                    if (u.Grade == DestructibleGrade.Building)
                    {
                        if (u.FractureName == null) { buildingNoModel++; continue; }
                        Assert.IsTrue(buildings.Contains(u.FractureName), scene.name + ": " + u.Root.name);
                    }
                    else
                    {
                        Assert.IsNotNull(u.FractureName, scene.name + ": " + u.Root.name + " has a fractured kind");
                        Assert.IsTrue(kinds.Contains(u.FractureName), scene.name + ": " + u.FractureName);
                        Assert.AreSame(u.Root, u.ModelRoot, scene.name + ": " + u.Root.name + " model root = root");
                    }
                    withModel++;
                }
                Assert.Greater(withModel, 10, scene.name + ": destructibles with models");
                Transform room = null;
                foreach (Transform t in map.GetComponentsInChildren<Transform>(true)) if (t.name == "MonsterRoom") { room = t; break; }
                Assert.IsNotNull(room, scene.name + ": monster room");
                Assert.IsTrue(kinds.Contains(MonsterRoomBreak.FractureNameFor(room)), scene.name + ": monster room fractured model");
                Assert.AreEqual(MonsterRoomBreak.FractureNameFor(room), MonsterRoomBreak.BuildUnit(room).FractureName, scene.name + ": room unit uses it");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }

    // U2: 조각 모델이 원본 자리에 그대로 겹친다(애니메이션 첫 프레임) — 소품 하나를 원본 노드 자리에 틀어 경계 상자를 비교.
    [Test]
    public void FracturePlayer_FirstFrameMatchesOriginal()
    {
        string path = DecoyNavMeshBaker.MapScenes[0];
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        var made = new List<GameObject>();
        try
        {
            DestructibleCatalog cat = DestructibleCatalog.Build(DestructibleCatalog.FindMap(scene), DestructionRulesSO.Current);
            int checkedCount = 0;
            foreach (DestructibleUnit u in cat.Units)
            {
                if (u.Grade == DestructibleGrade.Building || u.FractureName == null || checkedCount >= 6) continue;
                FracturePlayer p = FracturePlayer.Play(Holder(u.FractureName), u.ModelRoot, Vector3.zero, 1f, false);
                Assert.IsNotNull(p, u.FractureName);
                made.Add(p.gameObject);
                Renderer[] rs = p.Renderers();
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                Assert.Less((b.center - u.Bounds.center).magnitude, u.Bounds.size.magnitude * 0.08f + 0.05f, u.Root.name + ": center");
                Assert.AreEqual(u.Bounds.size.magnitude, b.size.magnitude, u.Bounds.size.magnitude * 0.2f, u.Root.name + ": size");
                checkedCount++;
            }
            Assert.AreEqual(6, checkedCount);
        }
        finally
        {
            foreach (GameObject g in made) Object.DestroyImmediate(g);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    // U2·U3: 큐브 조각 코드가 남아 있지 않다(거대 괴물·마녀 부수기).
    [Test]
    public void NoCubeDebrisLeft()
    {
        foreach (string file in new[] { "Assets/02. Scripts/Escape/Giant/DestructionPresenter.cs", "Assets/02. Scripts/Escape/WitchShatter.cs" })
        {
            string src = File.ReadAllText(file);
            Assert.IsFalse(src.Contains("CreatePrimitive"), file + ": no primitive cubes");
            Assert.IsFalse(src.Contains("AddComponent<Rigidbody>"), file + ": no physics debris");
        }
    }

    // U4: 재질 묶음별 소리, 소리 파일 6개가 목록에 들어 있다.
    [Test]
    public void BreakSounds_ByStyle()
    {
        var wood = ScriptableObject.CreateInstance<FracturedModelSO>();
        try
        {
            wood.EditorSetClips(new AnimationClip[0], new Vector3[0], FractureStyle.Wood);
            Assert.AreEqual(SoundId.BreakWood, DestructionPresenter.SoundFor(wood, DestructibleGrade.Prop));
            wood.EditorSetClips(new AnimationClip[0], new Vector3[0], FractureStyle.Shell);
            Assert.AreEqual(SoundId.BreakMetal, DestructionPresenter.SoundFor(wood, DestructibleGrade.Prop));
            wood.EditorSetClips(new AnimationClip[0], new Vector3[0], FractureStyle.Panel);
            Assert.AreEqual(SoundId.BreakTent, DestructionPresenter.SoundFor(wood, DestructibleGrade.Tent));
            wood.EditorSetClips(new AnimationClip[0], new Vector3[0], FractureStyle.Building);
            Assert.AreEqual(SoundId.CollapseBuilding, DestructionPresenter.SoundFor(wood, DestructibleGrade.Building));
            Assert.AreEqual(SoundId.CollapseBuilding, DestructionPresenter.SoundFor(null, DestructibleGrade.Stairs));
        }
        finally { Object.DestroyImmediate(wood); }
        var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalogSO>("Assets/Resources/Audio/SoundCatalog.asset");
        Assert.IsNotNull(catalog);
        var ids = new HashSet<SoundId> { SoundId.BreakWood, SoundId.BreakMetal, SoundId.BreakCandy, SoundId.BreakTent, SoundId.CollapseBuilding, SoundId.CrumbleBody };
        int found = 0;
        foreach (SoundCatalogSO.Entry e in catalog.Entries)
            if (e != null && ids.Contains(e.id)) { Assert.Greater(e.clips.Length, 0, e.id + ": clip"); found++; }
        Assert.AreEqual(6, found);
    }
}
