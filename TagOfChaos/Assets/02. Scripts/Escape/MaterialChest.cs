using UnityEngine;

// 상자 하나(EscapePlan.md §1.4). 씬의 CHEST_Slot 자리에 붙어 있고, 판마다 EscapeState가 일반/스파이 상자와 열림 여부를 정한다.
// 겉모습은 두 상자가 같다. 이름 표시: 일반 = 재료 상자, 스파이 상자 = 모두에게 스파이 상자(쿠키에게는 잠겨 있음도).
// 한 번 열면 닫히지 않고, 재료가 다시 생긴 상자만 닫힌 모습으로 돌아간다. 괴물은 쓸 수 없다(D33).
// 여는 데 E를 holdSeconds(2초) 누르고 있어야 한다(Request1009Plan.md §1). 쿠키에게 잠긴 스파이 상자는 누르기가 시작되지 않는다.
// 변장 시간에는 숨어 있다가 끝나면 살짝 튀어나오며 나타난다(Request1009Plan.md §16.1 — 소리 없음).
public class MaterialChest : MonoBehaviour, IHoldInteractable, IInteractionLabel
{
    private const float OpenLidAngle = -110f;
    private const float AppearSeconds = 0.35f;
    private const float AppearOvershoot = 1.08f;
    private const int AppearDust = 14;
    private const float LidCloseSeconds = 0.4f;
    private const int RecloseDust = 6;

    [SerializeField] private Transform lid;
    [SerializeField, Min(0f)] private float holdSeconds = 2f;
    [SerializeField, Min(0.5f)] private float interactionRange = 2.2f;

    private EscapeManager manager;
    private int anchorIndex;
    private int chestIndex = -1;
    private EscapeState.Chest data;
    private bool registered;
    private Vector3 baseScale = Vector3.one;
    private float appearStart = -1f;
    private float lidCloseStart = -1f;
    private ParticleSystem dust;

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => interactionRange;

    public void Bind(EscapeManager owner, int anchor)
    {
        manager = owner;
        anchorIndex = anchor;
        if (lid == null) lid = transform.Find("Lid");
        baseScale = transform.localScale;
        Hide();
    }

    // appear: 숨어 있던 상자가 변장 시간이 끝나 처음 나타나는 순간이면 튀어나오는 연출을 한다.
    public void Show(int index, EscapeState.Chest chest, bool appear = false)
    {
        // 열려 있던 상자에 재료가 다시 생겨 닫힌 모습으로 돌아갈 때는 뚜껑을 천천히 닫는다(Request1009Plan.md §22.6 M4) — 근처 쿠키가 알아보게.
        bool reclosing = gameObject.activeSelf && chestIndex == index && data.Opened && !chest.Opened;
        chestIndex = index;
        data = chest;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            if (appear) BeginAppear();
        }
        if (reclosing) BeginLidClose();
        else if (chest.Opened) { lidCloseStart = -1f; SetLid(1f); }
        else if (lidCloseStart < 0f) SetLid(0f); // 닫는 중이면 그대로 둔다
        SetRegistered(!chest.Opened);
        ApplyColor(chest.Spy);
    }

    // 상자 색(§69 D1 — 사용자): 일반 = 파랑, 스파이 상자 = 다크레드. 뚜껑까지 같은 색, 재질은 공유한 채 MaterialPropertyBlock으로.
    public static readonly Color NormalColor = new Color(0.25f, 0.5f, 0.95f);
    public static readonly Color SpyColor = new Color(0.45f, 0.05f, 0.07f);
    private MaterialPropertyBlock block;
    private int coloredSpy = -1;

    private void ApplyColor(bool spy)
    {
        int key = spy ? 1 : 0;
        if (coloredSpy == key) return;
        coloredSpy = key;
        if (block == null) block = new MaterialPropertyBlock();
        Color c = spy ? SpyColor : NormalColor;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_Color", c);
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }

    public void Hide()
    {
        chestIndex = -1;
        SetRegistered(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        SetRegistered(false);
        EndAppear();
        EndLidClose();
    }

    private void BeginLidClose()
    {
        lidCloseStart = Time.time;
        if (dust == null) dust = CreateDust();
        dust.Emit(RecloseDust);
    }

    private void EndLidClose()
    {
        if (lidCloseStart < 0f) return;
        lidCloseStart = -1f;
        SetLid(data.Opened ? 1f : 0f);
    }

    // open01: 0 = 닫힘, 1 = 열림
    private void SetLid(float open01)
    {
        if (lid != null) lid.localRotation = Quaternion.Euler(OpenLidAngle * open01, 0f, 0f);
    }

    private void BeginAppear()
    {
        appearStart = Time.time;
        transform.localScale = baseScale * AppearScale(0f);
        if (dust == null) dust = CreateDust();
        dust.Emit(AppearDust);
    }

    private void EndAppear()
    {
        if (appearStart < 0f) return;
        appearStart = -1f;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (lidCloseStart >= 0f)
        {
            float lt = (Time.time - lidCloseStart) / LidCloseSeconds;
            if (lt >= 1f) EndLidClose();
            else SetLid(1f - Mathf.SmoothStep(0f, 1f, lt));
        }
        if (appearStart < 0f) return;
        float t = (Time.time - appearStart) / AppearSeconds;
        if (t >= 1f) EndAppear();
        else transform.localScale = baseScale * AppearScale(t);
    }

    // 0→1.08(70%)→1로 커지는 크기(순수 계산, 시험용).
    public static float AppearScale(float t)
    {
        t = Mathf.Clamp01(t);
        const float peakAt = 0.7f;
        if (t < peakAt) return Mathf.SmoothStep(0f, AppearOvershoot, t / peakAt);
        return Mathf.Lerp(AppearOvershoot, 1f, Mathf.SmoothStep(0f, 1f, (t - peakAt) / (1f - peakAt)));
    }

    // 바닥에서 퍼지는 작은 먼지(상자 자식이 아니라 같은 자리에 둬서 크기 연출의 영향을 받지 않는다).
    private ParticleSystem CreateDust()
    {
        ParticleSystem ps = EscapeVisuals.CreateParticles("ChestAppearDust", transform.parent, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(0.5f, 0.9f), Speed = new Vector2(0.6f, 1.6f), Size = new Vector2(0.35f, 0.7f),
            Color = new Color(0.55f, 0.5f, 0.48f, 0.45f), Gravity = -0.05f, EmissionOff = true, Manual = true, Once = true,
            Shape = ParticleSystemShapeType.Circle, Radius = 0.6f, ShapeRotation = new Vector3(-90f, 0f, 0f),
            FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 1f,
        });
        ps.transform.position = transform.position + Vector3.up * 0.1f;
        return ps;
    }

    private void SetRegistered(bool value)
    {
        if (registered == value) return;
        registered = value;
        if (value) InteractableRegistry.Register(this);
        else InteractableRegistry.Unregister(this);
    }

    public bool CanInteract(IGameCharacter character) =>
        EscapeManager.ActionsAllowed && EscapeManager.ChestsVisibleIn(GamePhaseState.Current) && chestIndex >= 0 && !data.Opened && character.Role == CharacterRole.Cookie;

    public float HoldSecondsFor(IGameCharacter character) => data.Spy && !RoomState.IsLocalSpy() ? 0f : holdSeconds;

    public void Interact(IGameCharacter character)
    {
        if (chestIndex < 0 || manager == null) return;
        if (data.Spy && !RoomState.IsLocalSpy()) return; // 쿠키에게는 잠겨 있음(아이콘 아래 표시)
        manager.Request(EscapeOp.OpenChest, chestIndex);
    }

    public string GetLabel(IGameCharacter viewer)
    {
        EscapeTextsSO t = EscapeTextsSO.Current;
        if (!data.Spy) return t.materialChest;
        return RoomState.IsLocalSpy() ? t.spyChest : t.spyChest + "\n" + t.locked;
    }
}
