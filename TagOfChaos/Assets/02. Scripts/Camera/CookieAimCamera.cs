using UnityEngine;
using UnityEngine.Rendering;

// 쿠키 1인칭 조준 시점(Request1009Plan.md §76 F1~F3, F5). Main Camera에 Camera_Ctrl과 함께 붙고, Camera_Ctrl이 3인칭 자리를 정한 뒤(실행 순서 뒤)
// 카메라를 머리(눈) 자리로 0.5초에 걸쳐 옮긴다 — 시선 방향은 Camera_Ctrl 그대로라 전환 중에도 겨눈 곳이 튀지 않는다.
// 1인칭이 되면 본인 화면에서만 몸을 "그림자만"으로 숨기고 팔만 따로 그린다(§77 FP1 — FPS처럼 손과 든 아이템이 보인다), 화면 가운데에 작은 점을 띄운다.
// 켜고 끄기는 ToolUser(스턴건·물풍선·섬광탄을 든 채 우클릭)가 Want로 정한다.
[DefaultExecutionOrder(100)]
public class CookieAimCamera : MonoBehaviour
{
    public const float BlendSeconds = 0.5f;
    // 눈 자리(§77 FP2): 머리 뼈보다 조금 뒤·위·왼쪽 — FPS처럼 두 손과 든 아이템이 화면 오른쪽 아래에 들어온다(사진 비교로 고름).
    public const float EyeForward = -0.3f;  // 머리 뼈에서 앞으로(음수 = 뒤)
    public const float EyeUp = 0.15f;       // 머리 뼈에서 위로
    public const float EyeSide = -0.08f;    // 머리 뼈에서 오른쪽으로(음수 = 왼쪽)
    private const float HideBodyAt = 0.7f;   // 이만큼 1인칭에 가까워지면 몸을 숨긴다
    private const float DotAt = 0.5f;
    private const float FirstPersonNearClip = 0.05f;

    public bool Want;
    public float Blend { get; private set; }  // 0 = 3인칭, 1 = 1인칭

    // 위를 더 올려다볼 수 있게(Camera_Ctrl이 읽는다): 1인칭 조준으로 가는 중·있는 동안.
    public static bool WideLook { get; private set; }

    private Camera cam;
    private Transform body;
    private Animator animator;
    private Avatar headAvatar;
    private Transform head;
    private Renderer[] hidden;
    private ShadowCastingMode[] hiddenModes;
    private float previousNearClip = -1f;
    private GameObject dot;
    private readonly System.Collections.Generic.List<SkinnedMeshRenderer> arms = new System.Collections.Generic.List<SkinnedMeshRenderer>();
    private readonly System.Collections.Generic.List<SkinnedMeshRenderer> armSources = new System.Collections.Generic.List<SkinnedMeshRenderer>();

    public static CookieAimCamera For(Camera camera)
    {
        if (camera == null) return null;
        CookieAimCamera c = camera.GetComponent<CookieAimCamera>();
        if (c == null) c = camera.gameObject.AddComponent<CookieAimCamera>();
        return c;
    }

    // 따라갈 몸(로컬 쿠키). 바뀌면 숨긴 몸을 되돌린다.
    public void Track(Transform bodyRoot)
    {
        if (body == bodyRoot) return;
        ShowBody();
        ClearArms();
        body = bodyRoot;
        animator = body != null ? body.GetComponent<Animator>() : null;
        head = null;
        headAvatar = null;
    }

    private void Awake() => cam = GetComponent<Camera>();

    private void LateUpdate()
    {
        if (body == null) Want = false;
        Blend = Mathf.MoveTowards(Blend, Want ? 1f : 0f, Time.deltaTime / BlendSeconds);
        WideLook = Want || Blend > 0f;
        if (Blend <= 0f || body == null)
        {
            SetFirstPersonPose(false);
            ShowBody();
            RestoreNearClip();
            SetDot(false);
            return;
        }
        Vector3 eye = EyePosition();
        float k = Mathf.SmoothStep(0f, 1f, Blend);
        transform.position = Vector3.Lerp(transform.position, eye, k);
        if (cam != null && previousNearClip < 0f) { previousNearClip = cam.nearClipPlane; cam.nearClipPlane = FirstPersonNearClip; }
        if (Blend >= HideBodyAt) HideBody(); else ShowBody();
        SetFirstPersonPose(Blend >= HideBodyAt);
        SetDot(Want && Blend >= DotAt);
    }

    // 눈 자리: 머리 뼈(캐릭터 모델이 바뀌면 다시 찾는다) + 몸 앞·위. 머리 뼈가 없으면 몸 위 1.5 m.
    public Vector3 EyePosition()
    {
        if (animator != null && animator.isHuman && (head == null || animator.avatar != headAvatar))
        {
            headAvatar = animator.avatar;
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }
        Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 basePos = head != null ? head.position : body.position + Vector3.up * 1.5f;
        return basePos + forward * EyeForward + Vector3.up * EyeUp + right * EyeSide;
    }

    private PlayerActionPose pose;

    private void SetFirstPersonPose(bool on)
    {
        if (body == null) return;
        if (pose == null || pose.transform != body) pose = body.GetComponent<PlayerActionPose>();
        if (pose != null) pose.FirstPersonView = on;
    }

    private void HideBody()
    {
        if (hidden != null || body == null) return;
        hidden = CharacterModelSwap.BodyRenderers(body);
        hiddenModes = new ShadowCastingMode[hidden.Length];
        for (int i = 0; i < hidden.Length; i++)
        {
            if (hidden[i] == null) continue;
            hiddenModes[i] = hidden[i].shadowCastingMode;
            hidden[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
        ShowArms(true);
    }

    // 팔만 그리는 렌더러(§77 FP1). 캐릭터 모델이 바뀌어 원래 몸이 사라졌으면 다시 만든다.
    private void ShowArms(bool on)
    {
        if (on)
        {
            // 지금 숨긴 몸(모델을 바꾸면 달라진다 — 옛 쿠키 몸은 지워지지 않고 꺼지기만 한다)과 팔을 만든 몸이 다르면 다시 만든다
            if (!SameSources()) ClearArms();
            if (armSources.Count == 0 && hidden != null)
                foreach (Renderer r in hidden)
                {
                    if (!(r is SkinnedMeshRenderer smr)) continue;
                    SkinnedMeshRenderer a = CookieFirstPersonArms.Create(smr, animator);
                    armSources.Add(smr);
                    arms.Add(a);
                }
        }
        foreach (SkinnedMeshRenderer a in arms) if (a != null && a.gameObject.activeSelf != on) a.gameObject.SetActive(on);
    }

    private bool SameSources()
    {
        int n = 0;
        if (hidden != null)
            foreach (Renderer r in hidden)
            {
                if (!(r is SkinnedMeshRenderer smr)) continue;
                if (n >= armSources.Count || armSources[n] != smr) return false;
                n++;
            }
        return n == armSources.Count;
    }

    private void ClearArms()
    {
        foreach (SkinnedMeshRenderer a in arms) if (a != null) Destroy(a.gameObject);
        arms.Clear();
        armSources.Clear();
    }

    private void ShowBody()
    {
        ShowArms(false);
        if (hidden == null) return;
        for (int i = 0; i < hidden.Length; i++) if (hidden[i] != null) hidden[i].shadowCastingMode = hiddenModes[i];
        hidden = null;
        hiddenModes = null;
    }

    private void RestoreNearClip()
    {
        if (cam != null && previousNearClip >= 0f) cam.nearClipPlane = previousNearClip;
        previousNearClip = -1f;
    }

    // 화면 가운데 작은 점(§76 F5 — 3인칭 십자선은 없앴다, 1인칭일 때만).
    private void SetDot(bool on)
    {
        if (on && dot == null) dot = CreateDot();
        if (dot != null && dot.activeSelf != on) dot.SetActive(on);
    }

    private static GameObject CreateDot()
    {
        var root = new GameObject("AimDot", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var go = new GameObject("Dot", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(root.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(8f, 8f);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(1f, 1f, 1f, 0.95f);
        img.raycastTarget = false;
        var outline = go.AddComponent<UnityEngine.UI.Outline>(); // 밝은 바탕에서도 보이게 어두운 테두리
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); // 해상도가 달라도 같은 크기로
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        root.SetActive(false);
        return root;
    }

    private void OnDisable()
    {
        SetFirstPersonPose(false);
        ShowBody();
        RestoreNearClip();
        SetDot(false);
        WideLook = false;
    }

    private void OnDestroy()
    {
        if (dot != null) Destroy(dot);
        ClearArms();
    }
}
