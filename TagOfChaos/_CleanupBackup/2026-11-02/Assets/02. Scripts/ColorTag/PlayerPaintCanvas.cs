using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 쿠키 몸의 색 캔버스(RenderTexture). 손 색칠은 없앴다(Request1009Plan.md §26 H12, §38) — 지금 하는 일은 셋:
// - 물풍선에 맞으면 그 색으로 전신을 칠하고(ForceFillColor) 다른 화면에도 같은 색을 알린다(NetEventCodes.PaintStroke).
// - 캐릭터 스킨 위에 캔버스를 합성하는 재질을 입힌다(스킨이 늦게 바뀌면 TrySetBaseSkin).
// - 부서질 때 가루 색으로 쓰도록 지금 보이는 몸 표면 색을 샘플로 돌려준다(SampleSurfaceColors).
public class PlayerPaintCanvas : MonoBehaviour, IOnEventCallback
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Renderer bodyRenderer; // 캐릭터 스킨 렌더러 (SkinnedMeshRenderer)
    [SerializeField] private Shader paintedSkinShader; // ColorTag/PlayerPaintedSkin
    [SerializeField] private ColorPaletteSO palette;
    [SerializeField] private Material finalizeStampMaterial; // 전신 칠하기: 잠금 무시하고 항상 덮어씀
    [SerializeField] private int canvasSize = 512;

    // 몸 표면에서 고른 샘플 UV(삼각형 면적에 비례해 에디터에서 미리 뽑아 저장). 몸 메시(정점 16.7만 개)는 실행 중 읽을 수 없게
    // 임포트돼 있어 미리 계산해 둔다. 처형 때 가루 색을 "칠한 색 + 스킨 색" 비율대로 섞는 데 쓴다(Bug-fix-plan.md §37).
    [SerializeField] private Vector2[] surfaceSampleUVs = new Vector2[0];
    private const int ColorSampleResolution = 64;

    public RenderTexture PaintCanvas { get; private set; }
    public bool IsMine => pv != null && pv.IsMine;

    // 인스턴스별 재질 — 공유 재질 에셋에 값을 쓰면 에디터에서 에셋 파일이 바뀌었다(§23.9). 원본은 인스펙터 연결용.
    private Material fillInstance;
    private Material paintedSkinInstance; // bodyRenderer에 입힌 합성 재질 — OnDestroy에서 해제(research.md §8.19)

    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

    // 캔버스는 Awake에서 만든다 — 이벤트 수신 등록(OnEnable) 직후 도착한 칠하기가 null 캔버스에 그려지지 않게(Bug-fix-plan.md §23.3.4-④).
    private void Awake()
    {
        PaintCanvas = new RenderTexture(canvasSize, canvasSize, 0, RenderTextureFormat.ARGB32)
        {
            name = $"PaintCanvas_{gameObject.name}_{(pv != null ? pv.ViewID : 0)}"
        };
        PaintCanvas.Create();
        ClearCanvas();
        fillInstance = new Material(finalizeStampMaterial);
        // 전신 칠하기는 잠금을 무시하고 덮어쓴다(PaintStamp.shader의 하드웨어 블렌딩: One, Zero).
        fillInstance.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.One);
        fillInstance.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.Zero);
    }

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    // 스킨 합성 재질은 PlayerSkinApplier.Awake()가 스킨을 바꾼 뒤여야 하므로 Start에서 입힌다(같은 오브젝트의 Awake 순서는 보장되지 않음).
    private void Start() => ApplyPaintedSkinMaterial();

    private void ApplyPaintedSkinMaterial()
    {
        if (bodyRenderer == null || paintedSkinShader == null) return;

        Material original = bodyRenderer.sharedMaterial;
        paintedSkinInstance = new Material(paintedSkinShader);
        if (original != null && original.HasProperty("_MainTex"))
            paintedSkinInstance.SetTexture("_MainTex", original.mainTexture);
        paintedSkinInstance.SetTexture("_PaintTex", PaintCanvas);

        bodyRenderer.sharedMaterial = paintedSkinInstance; // 인스턴스를 직접 만들었으므로 .material의 자동 복제는 쓰지 않는다
    }

    // 스킨(SkinIndex)이 합성 재질을 만든 뒤에 늦게 도착한 경우 PlayerSkinApplier가 호출한다. 합성 재질을 스킨 원본으로
    // 통째로 바꾸면 칠한 색이 사라지므로 기본 텍스처만 교체한다.
    public bool TrySetBaseSkin(Material skin)
    {
        if (paintedSkinInstance == null || skin == null) return false;
        if (skin.HasProperty("_MainTex")) paintedSkinInstance.SetTexture("_MainTex", skin.mainTexture);
        return true;
    }

    private void ClearCanvas()
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = PaintCanvas;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }

    // 물풍선에 맞으면 그 색으로 전신이 칠해진다(EscapePlan.md §1.8). 본인 캔버스만, 색은 팔레트 번호(범위 밖이면 접는다).
    public void ForceFillColor(int colorIndex)
    {
        if (!IsMine || palette == null || palette.Count == 0) return;
        colorIndex = Mathf.Abs(colorIndex) % palette.Count;
        Fill(colorIndex);
        if (!PhotonNetwork.InRoom) return;
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(NetEventCodes.PaintStroke, new object[] { pv.ViewID, colorIndex }, options, SendOptions.SendReliable);
    }

    // 다른 화면에서 칠한 색 받기. 페이로드: { viewId, colorIndex }
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.PaintStroke || !(photonEvent.CustomData is object[] data)) return;
        ApplyRemote(photonEvent.Sender, data);
    }

    public void ApplyRemote(int sender, object[] data)
    {
        if (data == null || data.Length < 2 || !(data[0] is int viewId) || !(data[1] is int colorIndex)) return;
        if (pv == null || viewId != pv.ViewID) return;
        if (!EscapeManager.IsActive) return; // 씬 전환 중 늦게 도착한 칠하기가 대기실 캐릭터에 그려지지 않게
        // 자기 몸은 자기만 칠한다 — 보낸 사람이 이 캐릭터의 주인이 아니면 무시한다(F-06, 남의 위장을 바꾸는 조작 방지).
        if (!PhotonNetwork.OfflineMode && (pv.Owner == null || sender != pv.Owner.ActorNumber)) return;
        if (palette == null || colorIndex < 0 || colorIndex >= palette.Count) return;
        Fill(colorIndex);
    }

    // 캔버스 전체를 팔레트 색으로 덮는다(GL 사각형 하나 — 셰이더의 원 판정 좌표를 0으로 두어 전부 칠한다).
    private void Fill(int colorIndex)
    {
        if (PaintCanvas == null || palette == null) return;
        Color color = palette.GetColor(colorIndex);
        // 정점 색은 재질 SetColor와 달리 색 공간 변환이 없으므로 Linear 프로젝트에서는 직접 선형으로 바꿔 넘긴다.
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = PaintCanvas;
        GL.PushMatrix();
        GL.LoadOrtho();
        fillInstance.SetPass(0);
        GL.Begin(GL.QUADS);
        GL.Color(color);
        GL.TexCoord2(0f, 0f); GL.Vertex3(0f, 0f, 0f);
        GL.TexCoord2(0f, 0f); GL.Vertex3(0f, 1f, 0f);
        GL.TexCoord2(0f, 0f); GL.Vertex3(1f, 1f, 0f);
        GL.TexCoord2(0f, 0f); GL.Vertex3(1f, 0f, 0f);
        GL.End();
        GL.PopMatrix();
        RenderTexture.active = prev;
    }

    // 지금 보이는 몸 표면의 색을 샘플 UV 위치마다 돌려준다(칠해진 곳은 칠한 색, 아닌 곳은 스킨 색). 캔버스와 스킨 텍스처를 작게 줄여
    // GPU에서 비동기로 읽으므로 게임이 멈추지 않는다. 칠하기는 모든 클라이언트가 다시 그리므로 원격 쿠키에서도 같은 색이 나온다.
    // 읽을 수 없으면(샘플 UV 없음, 미지원, 실패) null을 넘긴다. 콜백은 몇 프레임 뒤에 온다.
    public void SampleSurfaceColors(System.Action<Color[]> onDone)
    {
        if (onDone == null) return;
        Texture baseTexture = paintedSkinInstance != null ? paintedSkinInstance.mainTexture
            : bodyRenderer != null && bodyRenderer.sharedMaterial != null ? bodyRenderer.sharedMaterial.mainTexture : null;
        if (surfaceSampleUVs == null || surfaceSampleUVs.Length == 0 || PaintCanvas == null || baseTexture == null
            || !SystemInfo.supportsAsyncGPUReadback)
        {
            onDone(null);
            return;
        }

        var paintSmall = RenderTexture.GetTemporary(ColorSampleResolution, ColorSampleResolution, 0, RenderTextureFormat.ARGB32);
        var baseSmall = RenderTexture.GetTemporary(ColorSampleResolution, ColorSampleResolution, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(PaintCanvas, paintSmall);
        Graphics.Blit(baseTexture, baseSmall);

        Color32[] paintPixels = null, basePixels = null;
        bool failed = false;
        int pending = 2;
        void Complete()
        {
            if (--pending > 0) return;
            onDone(failed ? null : ComposeSurfaceColors(paintPixels, basePixels));
        }

        UnityEngine.Rendering.AsyncGPUReadback.Request(paintSmall, 0, request =>
        {
            if (request.hasError) failed = true; else paintPixels = request.GetData<Color32>().ToArray();
            RenderTexture.ReleaseTemporary(paintSmall);
            Complete();
        });
        UnityEngine.Rendering.AsyncGPUReadback.Request(baseSmall, 0, request =>
        {
            if (request.hasError) failed = true; else basePixels = request.GetData<Color32>().ToArray();
            RenderTexture.ReleaseTemporary(baseSmall);
            Complete();
        });
    }

    private Color[] ComposeSurfaceColors(Color32[] paintPixels, Color32[] basePixels)
    {
        var colors = new Color[surfaceSampleUVs.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            Vector2 uv = surfaceSampleUVs[i];
            int x = Mathf.Clamp((int)((uv.x - Mathf.Floor(uv.x)) * ColorSampleResolution), 0, ColorSampleResolution - 1);
            int y = Mathf.Clamp((int)((uv.y - Mathf.Floor(uv.y)) * ColorSampleResolution), 0, ColorSampleResolution - 1);
            int index = y * ColorSampleResolution + x;
            Color32 paint = paintPixels[index];
            Color32 skin = basePixels[index];
            colors[i] = paint.a >= 128 ? new Color32(paint.r, paint.g, paint.b, 255) : new Color32(skin.r, skin.g, skin.b, 255);
        }
        return colors;
    }

    private void OnDestroy()
    {
        if (PaintCanvas != null)
        {
            PaintCanvas.Release();
            Destroy(PaintCanvas);
            PaintCanvas = null;
        }
        if (paintedSkinInstance != null) Destroy(paintedSkinInstance);
        if (fillInstance != null) Destroy(fillInstance);
    }
}
