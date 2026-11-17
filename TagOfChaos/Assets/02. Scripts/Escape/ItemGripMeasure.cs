using UnityEngine;

// 두 손 아이템의 몸 기준 크기(Request1009Plan.md §88 G1). 소켓 회전 = 몸 회전 × holdRotation 이므로
// 소켓 기준 좌표에 holdRotation을 곱하면 몸 기준(오른쪽 x·위 y·앞 z) 크기가 된다.
// 메시 경계만 보면 모델 루트 크기가 100인 마카롱 바퀴를 0.01 m로 잰다 → 렌더러 Transform(크기·회전 포함)을 거쳐 잰다.
public readonly struct ItemGripSize
{
    public readonly float HalfWidth;  // 몸 좌우 길이의 절반
    public readonly float HalfDepth;  // 몸 앞뒤 길이의 절반
    public readonly Vector3 Center;   // 몸 방향 기준, 소켓에서 잡는 높이의 아이템 가운데까지(holdRotation 적용 뒤)
    public readonly float Below, Above; // 잡는 높이에서 아이템 아래 끝·위 끝까지(몸 앞면을 잴 높이 범위)

    public ItemGripSize(float halfWidth, float halfDepth, Vector3 center, float below, float above)
    {
        HalfWidth = halfWidth;
        HalfDepth = halfDepth;
        Center = center;
        Below = below;
        Above = above;
    }

    public static readonly ItemGripSize Default = new ItemGripSize(0.2f, 0.15f, Vector3.zero, 0.2f, 0.2f);
}

public static class ItemGripMeasure
{
    // gripHeight: 손이 잡는 높이(아이템 높이 비율, 0 = 아래 끝, 0.5 = 가운데, 1 = 위 끝).
    public static ItemGripSize Measure(GameObject model, Transform socket, Quaternion holdRotation, float gripHeight)
    {
        if (model == null || socket == null) return ItemGripSize.Default;
        Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
        Matrix4x4 toSocket = socket.worldToLocalMatrix;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.TryGetComponent(out MeshFilter f) ? f.sharedMesh : null;
            if (mesh == null) continue;
            Bounds b = mesh.bounds;
            Matrix4x4 toItem = toSocket * r.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = holdRotation * toItem.MultiplyPoint3x4(c);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        return FromBounds(min, max, gripHeight);
    }

    // 몸 기준 경계(min·max) → 잡는 크기. 경계가 없으면 기본값.
    public static ItemGripSize FromBounds(Vector3 min, Vector3 max, float gripHeight)
    {
        if (min.x > max.x) return ItemGripSize.Default;
        Vector3 center = (min + max) * 0.5f;
        center.y = Mathf.Lerp(min.y, max.y, Mathf.Clamp01(gripHeight));
        return new ItemGripSize((max.x - min.x) * 0.5f, (max.z - min.z) * 0.5f, center, center.y - min.y, max.y - center.y);
    }

    // 아이템 에셋의 덮어쓰기(0보다 크면 그 폭)를 적용한다.
    public static ItemGripSize WithOverride(ItemGripSize size, float halfWidthOverride) =>
        halfWidthOverride > 0f ? new ItemGripSize(halfWidthOverride, size.HalfDepth, size.Center, size.Below, size.Above) : size;
}
