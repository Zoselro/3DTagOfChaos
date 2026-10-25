using UnityEngine;

// 분수 물 표면이 흐르게(Request1009Plan.md §39 F2). 물줄기·넘쳐 흐르는 물·물보라·물결은 같은 프리팹의 파티클(FountainWaterBuilder가
// 만든다)이 스스로 돈다 — 여기서는 위·아래 물 표면의 텍스처 위치만 천천히 흘리며 흔든다.
// 물 재질은 공유 재질이라 값을 직접 바꾸지 않고(에디터에서 에셋 파일이 바뀐다, §23.9) 렌더러의 물 칸에만 MaterialPropertyBlock을 쓴다.
// 화면에 보일 때만 갱신한다. 네트워크와 무관한 겉모습이라 화면마다 따로 흐른다.
public sealed class FountainWater : MonoBehaviour
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

    [SerializeField] private Renderer body;                             // 분수 모델(돌·금·물 세 칸)
    [SerializeField] private int waterMaterialIndex = 2;
    [SerializeField] private Vector2 drift = new Vector2(0.02f, 0.035f); // 1초에 흐르는 거리(UV)
    [SerializeField] private float wobble = 0.012f;                      // 흔들림 폭(UV)
    [SerializeField] private float wobbleSpeed = 1.4f;

    private MaterialPropertyBlock block;
    private Vector2 scale = Vector2.one, offset;
    private float phase;

    public Renderer Body => body;
    public int WaterMaterialIndex => waterMaterialIndex;

    private void Awake()
    {
        Material[] mats = body != null ? body.sharedMaterials : null;
        if (mats == null || waterMaterialIndex < 0 || waterMaterialIndex >= mats.Length || mats[waterMaterialIndex] == null)
        {
            enabled = false;
            return;
        }
        Material water = mats[waterMaterialIndex];
        if (water.HasProperty(MainTexId))
        {
            scale = water.mainTextureScale;
            offset = water.mainTextureOffset;
        }
        block = new MaterialPropertyBlock();
        Vector3 p = transform.position;
        phase = Mathf.Repeat(p.x * 0.37f + p.z * 0.61f, 10f); // 분수마다 흔들림이 어긋나게
    }

    private void Update()
    {
        if (!body.isVisible) return;
        float t = Time.time + phase;
        Vector2 o = offset + new Vector2(Mathf.Repeat(drift.x * t, 1f), Mathf.Repeat(drift.y * t, 1f))
                    + new Vector2(Mathf.Sin(t * wobbleSpeed), Mathf.Cos(t * wobbleSpeed * 0.8f)) * wobble;
        block.SetVector(MainTexSt, new Vector4(scale.x, scale.y, o.x, o.y));
        body.SetPropertyBlock(block, waterMaterialIndex);
    }

#if UNITY_EDITOR
    public void EditorSetup(Renderer value, int index) { body = value; waterMaterialIndex = index; }
#endif
}
