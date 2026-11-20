using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// §90 F4 바닥을 덮는 풀·이끼·덤불(충돌체 없는 장식) — 바닥 아이템이 묻히지 않게 그 위로 띄울 높이를 잰다.
// 씬마다 한 번 이름으로 모은다(사탕 숲 이끼 타일 CF_Tile_Moss 등). 렌더러 경계의 윗면을 덮는 높이로 본다.
public static class GroundCover
{
    public const float MaxLift = 1.3f;
    public const float Margin = 0.2f;
    private static readonly string[] Keys = { "Moss", "Grass", "Bush", "Foliage", "Shrub", "Fern", "Wheat" };
    private static readonly List<Bounds> covers = new List<Bounds>();
    private static int sceneHandle = -1;

    public static bool IsCoverName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (string k in Keys) if (name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    // 덮는 높이(순수 계산 — 시험): 바닥 p.y에서 위로 덮개 윗면 + Margin, 0~MaxLift.
    public static float LiftFor(float groundY, float coverTop) => Mathf.Clamp(coverTop - groundY + Margin, 0f, MaxLift);

    public static float LiftAt(Vector3 p)
    {
        Collect();
        float top = float.NegativeInfinity;
        foreach (Bounds b in covers)
        {
            if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
            if (b.max.y < p.y - 0.2f || b.min.y > p.y + 2f) continue;   // 다른 층
            top = Mathf.Max(top, b.max.y);
        }
        return float.IsNegativeInfinity(top) ? 0f : LiftFor(p.y, top);
    }

    private static void Collect()
    {
        Scene s = SceneManager.GetActiveScene();
        if (s.handle == sceneHandle) return;
        sceneHandle = s.handle;
        covers.Clear();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer || r.GetComponentInParent<IGameCharacter>() != null) continue;
            if (!IsCoverName(r.name) && !(r.transform.parent != null && IsCoverName(r.transform.parent.name))) continue;
            if (r.GetComponent<Collider>() != null && !r.GetComponent<Collider>().isTrigger) continue; // 밟는 바닥(충돌체)은 덮개가 아니다
            Bounds b = r.bounds;
            if (b.size.y > 4f) continue;                                                              // 나무 등 큰 물체는 빼고
            covers.Add(b);
        }
    }
}
