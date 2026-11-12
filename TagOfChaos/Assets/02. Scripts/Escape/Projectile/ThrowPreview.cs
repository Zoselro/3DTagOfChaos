using UnityEngine;

// 던지는 물체를 들고 있는 동안(본인 화면만) 화면 가운데 조준점과 점선 궤적(Request1009Plan.md §70.3.6).
// 궤적은 실제 던질 때와 같은 계산(ThrowAim → BallisticSim.Predict)이라 어긋나지 않고, 벽에 막히면 거기서 끊긴다.
public class ThrowPreview
{
    private const int MaxPoints = 120;
    private readonly Vector3[] points = new Vector3[MaxPoints];
    private BalloonAimPreview line;
    private GameObject crosshair;

    public void Show(Camera cam, Vector3 hand, Transform thrower, ProjectileConfigSO c, float ringRadius, Color color)
    {
        if (c == null || thrower == null) { Hide(); return; }
        if (line == null) line = BalloonAimPreview.Create(dotted: true);
        if (crosshair == null) crosshair = CreateCrosshair(color);
        if (!crosshair.activeSelf) crosshair.SetActive(true);
        ThrowAim aim = ThrowAim.FromCamera(cam, hand, thrower.forward, c);
        ProjectileWorld.Ignore = thrower;
        int n = BallisticSim.Predict(aim.Origin, aim.Velocity, c, ProjectileWorld.Sweep, points, c.burstOnImpact, out _, out Vector3 end);
        ProjectileWorld.Ignore = null;
        line.ShowPath(points, n, end, ringRadius, color);
    }

    public void Hide()
    {
        if (line != null) line.Hide();
        if (crosshair != null && crosshair.activeSelf) crosshair.SetActive(false);
    }

    public void Destroy()
    {
        if (line != null) Object.Destroy(line.gameObject);
        if (crosshair != null) Object.Destroy(crosshair);
    }

    // 화면 가운데 작은 점.
    private static GameObject CreateCrosshair(Color color)
    {
        var root = new GameObject("ThrowCrosshair", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var dot = new GameObject("Dot", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        dot.transform.SetParent(root.transform, false);
        var rt = (RectTransform)dot.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(10f, 10f);
        var img = dot.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(color.r, color.g, color.b, 1f);
        img.raycastTarget = false;
        var outline = dot.AddComponent<UnityEngine.UI.Outline>(); // 밝은 배경에서도 보이게
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return root;
    }
}
