using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// §90 B3 술래 바주카포(MonsterController가 붙인다). 본인: 우클릭(시점 키)을 누르고 있으면 어깨에 메고 조준(걷기 50%, 돌진·잡기 불가, 시야 0.85배),
// 그 상태에서 좌클릭 = 발사(쿨타임 10초, 사냥 시간만). 로켓은 ProjectileSystem(방장 판정)으로 날고, 터지면 EscapeAuthority.BazookaExplode가 판정한다.
// 다른 화면: 조준 여부는 플레이어 속성(BzAim)으로, 발사 동작은 ProjectileSystem이 로켓을 받을 때(PlayFireRemote) 보인다.
public class BazookaSkill : MonoBehaviour
{
    public const string AimProp = "BzAim";
    private const float FireShowSeconds = 0.7f;
    private const float AimFovScale = 0.85f;
    private static readonly Vector3 MuzzleLocal = new Vector3(0f, 0.585f, 2.62f); // 모델 기준 포구(원점 = 손잡이, 앞 +Z)

    public static float ReadyAt { get; private set; }
    public static BazookaSkill Local { get; private set; }

    private MonsterController monster;
    private PhotonView pv;
    private Animator animator;
    private Transform chest;
    private GameObject model;
    private bool aiming, sentAim;
    private float aimSince = -10f, fireUntil = -10f;
    private float baseFov;

    public bool IsAiming => aiming;
    public bool ModelVisible => model != null && model.activeSelf;

    public void Init(MonsterController owner)
    {
        monster = owner;
        pv = owner.GetComponent<PhotonView>();
        animator = owner.GetComponentInChildren<Animator>(true);
        foreach (Transform t in owner.GetComponentsInChildren<Transform>(true)) if (t.name == "Chest") { chest = t; break; }
        // if (pv != null && pv.IsMine) Local = this;
        if (pv != null && pv.IsMine && pv.Owner != null) Local = this; // §91 씬에 직접 놓은 술래(주인 없음)는 내 바주카포가 아니다
        ProjectileSystem.ThrowRejected += OnThrowRejected;
    }

    private void OnDestroy()
    {
        ProjectileSystem.ThrowRejected -= OnThrowRejected;
        if (Local == this) { Local = null; PlayerInput.BazookaAiming = false; RestoreFov(); }
        if (model != null) Destroy(model);
    }

    private void OnThrowRejected(ItemSO item, ThrowReject why)
    {
        if (item != null && item.IsTool && item.Tool.Kind == ToolKind.Bazooka) ReadyAt = 0f;
    }

    private static ItemSO Item => EscapeCatalogSO.Current != null ? EscapeCatalogSO.Current.Bazooka : null;

    // 지금 조준·발사할 수 있는지(본인): 탈출 모드 사냥 시간, 움직일 수 있고, 돌진·잡기·기절 중이 아님.
    public bool CanAim() =>
        EscapeManager.IsActive && EscapeManager.ActionsAllowed && MonsterElevator.PhaseAllows(GamePhaseState.Current)
        && Item != null && Item.IsTool && Item.Tool.Projectile != null && monster != null && monster.CanInteract && !monster.IsDashWindingUp;

    private void Update()
    {
        if (pv == null) return;
        // if (pv.IsMine) UpdateLocal();
        if (pv.IsMine && pv.Owner != null) UpdateLocal(); // §91 씬에 직접 놓은 술래 복사본은 입력을 받지 않는다(그 자리에서 쏘면 방장이 거절)
        else aiming = pv.Owner != null && pv.Owner.CustomProperties.TryGetValue(AimProp, out object v) && v is bool b && b;
        UpdateVisual();
    }

    private void UpdateLocal()
    {
        bool want = PlayerInput.BazookaAimHeld && CanAim();
        if (want != aiming) { aiming = want; if (want) aimSince = Time.time; }
        PlayerInput.BazookaAiming = aiming;
        monster.AimSpeedScale = aiming ? BazookaRules.AimWalkScale : 1f;
        if (aiming != sentAim && PhotonNetwork.LocalPlayer != null)
        {
            sentAim = aiming;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { AimProp, aiming } });
        }
        UpdateFov();
        if (aiming && PlayerInput.BazookaFirePressed) TryFire();
    }

    // 발사(시험에서도 부른다). 쐈으면 true.
    public bool TryFire()
    {
        if (!aiming || Time.time - aimSince < BazookaRules.AimHoldSeconds) return false;
        if (Time.time < ReadyAt) { UiSoundCues.Error(); return false; }
        ItemSO item = Item;
        if (item == null || ProjectileSystem.Instance == null) return false;
        EnsureModel();
        PlaceModel();
        Vector3 muzzle = model.transform.TransformPoint(MuzzleLocal);
        Vector3 dir = AimDirection(muzzle);
        ReadyAt = Time.time + BazookaRules.Cooldown;
        ProjectileSystem.Instance.Throw(item, BazookaRules.ThrowSlot, 0, ThrowAim.Straight(muzzle, dir, BazookaRules.Speed, BazookaRules.MaxRange), transform);
        GameAudio.Play(SoundId.BazookaFire);
        PlayFire(muzzle, dir);
        return true;
    }

    // 화면 가운데가 가리키는 곳(최대 90 m)으로 — 포구에서 그 점을 향한다.
    private Vector3 AimDirection(Vector3 muzzle)
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.forward;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 target = ray.origin + ray.direction * BazookaRules.MaxRange;
        foreach (RaycastHit h in Physics.RaycastAll(ray, BazookaRules.MaxRange + 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (Vector3.Dot(h.point - muzzle, ray.direction) < 1f) continue; // 포구 뒤(카메라와 포구 사이)
            if ((h.point - ray.origin).sqrMagnitude < (target - ray.origin).sqrMagnitude) target = h.point;
        }
        Vector3 d = target - muzzle;
        return d.sqrMagnitude > 0.01f ? d.normalized : ray.direction;
    }

    // 다른 화면: 로켓을 받았을 때 그 술래의 발사 동작·포구 불꽃(ProjectileSystem.PlayThrowPresentation).
    public void PlayFireRemote(Vector3 origin, Vector3 velocity) => PlayFire(origin, velocity);

    private void PlayFire(Vector3 origin, Vector3 dir)
    {
        fireUntil = Time.time + FireShowSeconds;
        if (animator != null && HasParam("BazookaFire")) animator.SetTrigger("BazookaFire");
        BazookaFx.MuzzleFlash(origin, dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward);
    }

    private void UpdateVisual()
    {
        bool show = aiming || Time.time < fireUntil;
        if (animator != null && HasParam("BazookaAim")) animator.SetBool("BazookaAim", show);
        if (show) { EnsureModel(); PlaceModel(); }
        if (model != null && model.activeSelf != show) model.SetActive(show);
    }

    private void EnsureModel()
    {
        if (model != null) return;
        EscapeCatalogSO c = EscapeCatalogSO.Current;
        GameObject prefab = c != null ? c.BazookaModel : null;
        // FBX 루트가 메시 자체(크기 100·회전)라 감싸는 부모에 자리를 준다
        model = new GameObject("Bazooka_Held");
        GameObject mesh = prefab != null ? Instantiate(prefab, model.transform, false) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        if (prefab == null) mesh.transform.SetParent(model.transform, false);
        foreach (Collider col in model.GetComponentsInChildren<Collider>()) Destroy(col);
        Transform parent = chest != null ? chest : transform;
        model.transform.SetParent(parent, false);
        if (c != null && chest != null)
        {
            model.transform.localPosition = c.BazookaChestPos;
            model.transform.localRotation = c.BazookaChestRot;
            model.transform.localScale = c.BazookaChestScale;
        }
        model.SetActive(false);
    }

    private void PlaceModel() { } // 가슴 뼈 자식이라 동작을 그대로 따른다(손은 조준 동작이 손잡이·통에 맞춰 둔다)

    private bool HasParam(string name)
    {
        foreach (AnimatorControllerParameter p in animator.parameters) if (p.name == name) return true;
        return false;
    }

    private void UpdateFov()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (baseFov <= 0f) baseFov = cam.fieldOfView;
        float want = aiming ? baseFov * AimFovScale : baseFov;
        cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, want, 60f * Time.deltaTime);
        if (!aiming && Mathf.Approximately(cam.fieldOfView, baseFov)) baseFov = 0f; // 다른 쪽(쿠키 조준 등)이 바꾼 기본값을 다음에 다시 읽는다
    }

    private void RestoreFov()
    {
        Camera cam = Camera.main;
        if (cam != null && baseFov > 0f) cam.fieldOfView = baseFov;
    }
}
