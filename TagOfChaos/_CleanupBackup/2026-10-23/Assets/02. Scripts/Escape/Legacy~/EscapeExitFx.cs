using UnityEngine;

// 탈출 장치가 완성됐을 때의 맵별 연출(EscapePlan.md §1.6). Blender 모델에 이름 규칙대로 자식이 있으면 그것을 쓰고
// (ESC_Glow, ESC_Cake_Intact, ESC_Cake_Rocket), 없으면 장치 위에 빛나는 기둥 하나로 대신한다.
public static class EscapeExitFx
{
    private const string GlowName = "ESC_Glow";
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    // 장치별 빛 렌더러(한 번만 찾는다 — F-15). 씬이 내려가면 비운다.
    private static readonly System.Collections.Generic.Dictionary<Transform, Renderer[]> glowRenderers =
        new System.Collections.Generic.Dictionary<Transform, Renderer[]>();
    private static bool hooked;

    public static void SetComplete(Transform device, EscapeExitKind kind, bool complete)
    {
        // 캔디숲: 케이크가 부서지고 로켓이 나온다 — 로켓 자체가 완성 표시이므로 빛 기둥으로 가리지 않는다.
        Transform cake = device.Find("ESC_Cake_Intact");
        Transform rocket = device.Find("ESC_Cake_Rocket");
        if (cake != null) cake.gameObject.SetActive(!complete);
        if (rocket != null) rocket.gameObject.SetActive(complete);

        Transform glow = device.Find(GlowName);
        if (glow == null && complete && rocket == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = GlowName;
            go.transform.SetParent(device, false);
            go.transform.localPosition = Vector3.up * 3f;
            go.transform.localScale = new Vector3(1.2f, 3f, 1.2f);
            EscapeVisuals.Tint(go, new Color(1f, 0.9f, 0.5f, 1f), 2.5f);
            glow = go.transform;
        }
        if (glow != null) glow.gameObject.SetActive(complete);
        glowRenderers.Remove(device); // 빛이 새로 생겼을 수 있다 — 다음 Tick에서 다시 찾는다
    }

    // 진저브레드 룬은 무지개빛으로 색이 돈다. 다른 맵은 은은하게 맥동한다.
    public static void Tick(Transform device, EscapeExitKind kind)
    {
        if (!hooked)
        {
            hooked = true;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += _ => glowRenderers.Clear();
        }
        if (!glowRenderers.TryGetValue(device, out Renderer[] renderers))
        {
            Transform glow = device.Find(GlowName);
            renderers = glow != null ? glow.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            glowRenderers[device] = renderers;
        }
        if (renderers.Length == 0) return;
        Color c = kind == EscapeExitKind.RuneAltar
            ? Color.HSVToRGB(Mathf.Repeat(Time.time * 0.25f, 1f), 0.7f, 1f)
            : new Color(1f, 0.9f, 0.5f) * (0.85f + 0.15f * Mathf.Sin(Time.time * 3f));
        // 재질을 복제하지 않고(r.material) 렌더러별 속성 블록으로 색만 바꾼다(F-15).
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.gameObject.activeInHierarchy) continue;
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, c);
            block.SetColor(EmissionColorId, c * 2.5f);
            r.SetPropertyBlock(block);
        }
    }
}
