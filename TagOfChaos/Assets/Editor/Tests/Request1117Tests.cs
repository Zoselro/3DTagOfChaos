using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Request1009Plan.md §78 — 콜라 대포 천장(지하)·진저브레드 지하 바닥 타일.
public class Request1117Tests
{
    // C3: 천장이 정확히 y 0이어도 "있음" — 날아가는 꼭대기가 천장 아래(여유 CeilingClearance)로 낮아진다. 없음은 원래 높이.
    [Test]
    public void CannonFlight_StaysUnderCeilingAtZero()
    {
        Vector3 a = new Vector3(0f, -4f, 0f), b = new Vector3(0f, -5f, 60f); // 천장(y 0) 바로 아래 — 천장이 없으면 꼭대기가 0 위로 간다
        float f = ColaCannonRules.FlightSeconds(60f);
        Assert.IsTrue(ColaCannonRules.HasCeiling(0f), "y 0 is a ceiling");
        Assert.IsFalse(ColaCannonRules.HasCeiling(ColaCannonRules.NoCeiling));
        float top = float.MinValue, free = float.MinValue;
        for (int i = 0; i <= 40; i++)
        {
            top = Mathf.Max(top, ColaCannonRules.FlightPoint(a, b, f * i / 40f, f, 0f).y);
            free = Mathf.Max(free, ColaCannonRules.FlightPoint(a, b, f * i / 40f, f).y);
        }
        Assert.LessOrEqual(top, 0f - ColaCannonRules.CeilingClearance + 0.1f, "apex under the ceiling (curve peak can sit a few cm off the midpoint)");
        Assert.Greater(free, top, "no ceiling flies higher");
        Assert.AreEqual(b, ColaCannonRules.FlightPoint(a, b, f, f, 0f), "still lands on the spot");
    }

    // R1: 몸 회전 720°/초 — 물리 한 번(0.02초)에 14.4°, 뒤돌기는 0.25초에 끝나고 그 사이 각도를 거친다.
    [Test]
    public void MoveTurn_TurnsAt720DegreesPerSecond()
    {
        Quaternion q = Quaternion.LookRotation(Vector3.right);
        Quaternion step = MoveTurn.Step(q, Vector3.left, 0.02f);
        Assert.AreEqual(14.4f, Quaternion.Angle(q, step), 0.05f);
        for (int i = 0; i < 12; i++) q = MoveTurn.Step(q, Vector3.left, 0.02f);
        Assert.Greater(Quaternion.Angle(q, Quaternion.LookRotation(Vector3.left)), 1f, "0.24 s: not there yet");
        q = MoveTurn.Step(q, Vector3.left, 0.02f);
        Assert.Less(Quaternion.Angle(q, Quaternion.LookRotation(Vector3.left)), 0.5f, "0.26 s: done");
        Assert.AreEqual(q, MoveTurn.Step(q, Vector3.zero, 0.02f), "no input: keep facing");
    }

    // F2: 지하 큰길 끝(|x|·|z| 101 m 너머)·출구 굴에도 보이는 바닥 타일이 있다.
    [Test]
    public void GingerbreadUnderground_TilesReachCaveWallAndExits()
    {
        var patches = AssetDatabase.FindAssets("GIN_Tiles_U_ t:Mesh", new[] { GingerbreadVillageMapBuilder.GeneratedFolder });
        float maxX = 0f, maxZ = 0f, minX = 0f, minZ = 0f;
        int n = 0;
        foreach (string g in patches)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!path.EndsWith("_Patch.asset")) continue;
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Bounds bb = m.bounds;
            maxX = Mathf.Max(maxX, bb.max.x); maxZ = Mathf.Max(maxZ, bb.max.z);
            minX = Mathf.Min(minX, bb.min.x); minZ = Mathf.Min(minZ, bb.min.z);
            Assert.AreEqual(GingerbreadVillageMapBuilder.UgY, bb.center.y, 0.2f, "underground level");
            n++;
        }
        Assert.Greater(n, 0, "patch meshes exist");
        foreach (float v in new[] { maxX, maxZ, -minX, -minZ })
            Assert.GreaterOrEqual(v, GingerbreadVillageMapBuilder.UgRadius + 10f, "tiles reach the cave wall and run into the exit tunnels");
    }
}
