using UnityEngine;

// 뿅망치가 캐릭터 머리를 뚫지 않게 하는 계산(Request1009Plan.md §75.12). 몸집이 쿠키의 절반인데 머리가 아주 큰 캐릭터(마카롱·몽블랑·레모네이드·붕어빵)는
// 같은 Humanoid 동작이어도 손이 낮고 머리가 넓어, 망치 머리·자루가 머리 속을 지난다. 머리를 타원체로 보고, 겹치면 오른손(쥔 곳)을 축으로
// 망치를 머리에서 먼 쪽(몸 바깥·앞)으로 돌린다 — 원래 방향에 가장 가까운 트인 방향. 머리와 안 겹치는 쿠키는 그대로(0°)다.
public static class HammerClearance
{
    public const float ShaftRadius = 0.06f;

    // 머리 타원체(가운데 center, 반지름 radii — 모두 같은 공간)와 반지름 r 구가 겹치면 true.
    public static bool Overlaps(Vector3 point, float r, Vector3 center, Vector3 radii)
    {
        Vector3 d = point - center;
        float x = d.x / (radii.x + r), y = d.y / (radii.y + r), z = d.z / (radii.z + r);
        return x * x + y * y + z * z < 1f;
    }

    // 망치(쥔 곳 pivot, 쥔 곳 → 머리 가운데 headOffset, 머리 반지름 headRadius)가 머리와 겹치는지 — 머리 가운데와 자루 가운데·머리 쪽 끝을 본다.
    // 모든 값은 몸 기준 공간(몸 회전을 뺀 공간)이다.
    public static bool Hits(Vector3 pivot, Vector3 headOffset, float headRadius, Vector3 center, Vector3 radii)
    {
        return Overlaps(pivot + headOffset, headRadius, center, radii)
            || Overlaps(pivot + headOffset * 0.5f, ShaftRadius, center, radii)
            || Overlaps(pivot + headOffset * 0.25f, ShaftRadius, center, radii);
    }

    // 몸 기준 공간에서 망치를 비켜 세우는 회전: 원래 방향에서 "머리 가운데 → 쥔 곳" 방향(머리에서 가장 먼 쪽) 사이를 Steps 칸으로 나눠
    // 원래 방향에 가장 가까운 트인 방향을 고른다. 끝까지 막히면(쥔 곳이 머리에 아주 가까우면) 머리 반대 방향 — 가장 덜 겹친다.
    public const int Steps = 10;

    public static Quaternion FindClear(Vector3 pivot, Vector3 headOffset, float headRadius, Vector3 center, Vector3 radii)
    {
        if (!Hits(pivot, headOffset, headRadius, center, radii)) return Quaternion.identity;
        float length = headOffset.magnitude;
        if (length < 1e-4f) return Quaternion.identity;
        Vector3 from = headOffset / length;
        Vector3 away = pivot - center;
        // 머리에서 바로 아래로 멀어지면 몸통을 지나므로, 아래 성분을 줄여 몸 바깥(오른쪽)·앞으로 뻗게 한다
        away = new Vector3(away.x, away.y * 0.5f, away.z) + new Vector3(0.6f, 0f, 0.4f);
        away.Normalize();
        for (int i = 1; i <= Steps; i++)
        {
            Vector3 dir = Vector3.Slerp(from, away, i / (float)Steps);
            if (!Hits(pivot, dir * length, headRadius, center, radii)) return Quaternion.FromToRotation(from, dir);
        }
        return Quaternion.FromToRotation(from, away);
    }

    // 몸 메시에서 목보다 위 꼭짓점의 경계 → 머리 타원체(몸 기준 공간, 가운데는 머리 뼈 기준 offset). 메시가 없으면 false.
    public static bool MeasureHead(Transform body, Transform headBone, float neckY, Renderer[] renderers, out Vector3 offsetFromHeadBone, out Vector3 radii)
    {
        offsetFromHeadBone = Vector3.zero; radii = Vector3.zero;
        if (body == null || headBone == null || renderers == null) return false;
        bool any = false;
        Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
        var baked = new Mesh();
        var verts = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Matrix4x4 toWorld;
            if (renderers[i] is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) continue;
                smr.BakeMesh(baked, false); // 뼈 공간 크기 그대로(모델 Transform 크기 100이어도 월드 크기) — 회전·위치만 더한다
                toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                baked.GetVertices(verts);
            }
            else if (renderers[i] is MeshRenderer mr && mr.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
            {
                toWorld = mr.transform.localToWorldMatrix;
                mf.sharedMesh.GetVertices(verts);
            }
            else continue;
            for (int v = 0; v < verts.Count; v++)
            {
                Vector3 p = body.InverseTransformPoint(toWorld.MultiplyPoint3x4(verts[v]));
                if (p.y < neckY) continue;
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                any = true;
            }
        }
        if (Application.isPlaying) Object.Destroy(baked); else Object.DestroyImmediate(baked);
        if (!any) return false;
        Vector3 center = (min + max) * 0.5f;
        radii = (max - min) * 0.5f;
        offsetFromHeadBone = center - body.InverseTransformPoint(headBone.position);
        return true;
    }
}
