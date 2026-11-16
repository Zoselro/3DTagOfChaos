using UnityEditor;
using UnityEngine;

// 기본 쿠키 앞 단추 = 초콜릿 단추(Request1009Plan.md §83, 사용자 승인). 쿠키 모델(Cookie_Idle.fbx 스킨 메시 하나)은 64 px 크림 단색
// 텍스처를 쓰므로, 가져올 때 가슴 앞 볼록한 단추 두 개의 꼭짓점 UV를 텍스처의 초콜릿 칸(Cookie_BaseSkin_A_Color.png 왼쪽 가운데,
// 아무 면도 쓰지 않는 칸)으로 옮긴다. FBX를 다시 내보내지 않아 뼈·아바타는 그대로이고, 이 메시를 쓰는 모든 곳(판·로비·1인칭 팔)에 같이 반영된다.
// 단추 고르기(메시 좌표, 단위 = 쿠키 키 1.98 m의 1/100): 가슴 앞면 y ≈ −0.142 ×0.01, 단추는 앞으로 약 7 cm 나온 두 덩어리(높이 0.00~0.11·0.20~0.31).
public class CookieChocolateButtons : AssetPostprocessor
{
    public const string ModelPath = "Assets/Animation/Cookie/UV/Cookie_Idle.fbx";
    public static readonly Vector2 ChocolateUv = new Vector2(0.12f, 0.44f);   // 텍스처의 초콜릿 칸 가운데

    // 단추 꼭짓점인지(메시 좌표)
    public static bool IsButton(Vector3 p) =>
        Mathf.Abs(p.x) < 0.0011f && p.z > -0.0003f && p.z < 0.0034f && p.y < -0.00148f;

    private void OnPostprocessModel(GameObject root)
    {
        if (assetPath != ModelPath) return;
        foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh m = r.sharedMesh;
            if (m == null) continue;
            Vector3[] v = m.vertices;
            Vector2[] uv = m.uv;
            if (uv == null || uv.Length != v.Length) continue;
            int n = 0;
            for (int i = 0; i < v.Length; i++)
                if (IsButton(v[i])) { uv[i] = ChocolateUv; n++; }
            m.uv = uv;
            Debug.Log($"[CookieButtons] {m.name}: {n} button vertices → chocolate.");
        }
    }
}
