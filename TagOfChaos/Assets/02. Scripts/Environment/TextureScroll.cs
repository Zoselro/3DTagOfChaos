using UnityEngine;

// 흐르는 초콜릿(Request1009Plan.md §41) — 가마솥 배수관 줄기·받는 통·분쇄기 출구 줄기의 텍스처를 한 방향으로 흘린다.
// 공유 재질 값을 바꾸지 않도록 MaterialPropertyBlock으로 이 렌더러에만 _MainTex_ST를 준다(FountainWater와 같은 방식).
// 화면에 보일 때만 갱신한다. 네트워크와 무관한 겉모습.
public sealed class TextureScroll : MonoBehaviour
{
    private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

    [SerializeField] private Renderer target;
    [SerializeField] private Vector2 speed = new Vector2(0f, 1f);  // 1초에 흐르는 거리(UV)

    private MaterialPropertyBlock block;
    private Vector2 scale = Vector2.one, offset;

    public Vector2 Speed => speed;

    public static Vector2 OffsetAt(Vector2 start, Vector2 speed, float time) =>
        new Vector2(Mathf.Repeat(start.x + speed.x * time, 1f), Mathf.Repeat(start.y + speed.y * time, 1f));

    private void Awake()
    {
        if (target == null) target = GetComponent<Renderer>();
        if (target == null || target.sharedMaterial == null) { enabled = false; return; }
        scale = target.sharedMaterial.mainTextureScale;
        offset = target.sharedMaterial.mainTextureOffset;
        block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (!target.isVisible) return;
        Vector2 o = OffsetAt(offset, speed, Time.time);
        block.SetVector(MainTexSt, new Vector4(scale.x, scale.y, o.x, o.y));
        target.SetPropertyBlock(block);
    }

#if UNITY_EDITOR
    public void EditorSetup(Renderer renderer, Vector2 uvPerSecond) { target = renderer; speed = uvPerSecond; }
#endif
}
