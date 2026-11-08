using Photon.Pun;
using UnityEngine;

// 콜라 대포 한 대(Request1009Plan.md §68.9) — 모든 화면. ColaCannonPresenter가 만든다.
// 해치·회전대·포신 각도, 발사 거품, 착지 연기는 상태 시각(PhotonNetwork.Time)으로 모든 화면이 같게 그린다.
// 탄 사람의 몸은 병 안에 있는 동안(들어간 뒤 ~ 발사) 모든 화면에서 숨기고, 주인 화면만 자기 캐릭터를 계단 → 병 안 → 포물선 → 착지로 옮긴다.
// 주인이 뒤 계단 앞에서 E를 누르면 그 순간 카메라가 보는 방향을 함께 보낸다(CannonBoard, 다른 사람에게는 안내가 뜨지 않는다).
public class ColaCannonView : MonoBehaviour, IHoldInteractable, IInteractionLabel
{
    public const string TurretName = "ColaCannon_Turret";
    public const string BarrelName = "ColaCannon_Barrel";
    public const string HatchName = "ColaCannon_Hatch";
    private const float PopSeconds = 0.35f;

    private EscapeManager manager;
    private EscapeState.Cannon data;
    private Transform turret, barrel, hatch;
    private Quaternion turretRest, barrelRest, hatchRest;
    private bool registered, ownerSetup, vanishing, fired, landed, closeSound, bodyHidden;
    private float spawnedAt, requestedAt = -10f;
    private Vector3 enterFrom;
    private Collider[] colliders;
    private Renderer[] riderRenderers;

    public Vector3 InteractionPoint => ColaCannonRules.StepsPoint(data.Pos, data.Yaw);
    public float InteractionRange => ColaCannonRules.BoardReach;

    public void Bind(EscapeManager owner, EscapeState.Cannon cannon)
    {
        manager = owner;
        data = cannon;
        turret = EscapeCue.FindDeep(transform, TurretName);
        barrel = EscapeCue.FindDeep(transform, BarrelName);
        hatch = EscapeCue.FindDeep(transform, HatchName);
        if (turret != null) turretRest = turret.localRotation;
        if (barrel != null) barrelRest = barrel.localRotation;
        if (hatch != null) hatchRest = hatch.localRotation;
        colliders = GetComponentsInChildren<Collider>(true);
        spawnedAt = Time.time;
        if (PhotonNetwork.Time - cannon.SummonedAt < 1.0)
        {
            ColaCannonPresenter.Smoke(cannon.Pos, 1.5f);
            GameAudio.PlayAt(SoundId.PortalOpen, cannon.Pos);
            transform.localScale = Vector3.one * 0.2f;
        }
        else spawnedAt -= PopSeconds;
        InteractableRegistry.Register(this);
        registered = true;
    }

    public void Apply(EscapeState.Cannon cannon)
    {
        bool justBoarded = !data.Boarded && cannon.Boarded;
        data = cannon;
        if (justBoarded) GameAudio.PlayAt(SoundId.DoorOpen, ColaCannonRules.Breech(data.Pos, data.Yaw, ColaCannonRules.RestPitch));
    }

    public void Vanish()
    {
        if (vanishing) return;
        vanishing = true;
        ShowRider(true);
        ColaCannonPresenter.Smoke(data.Pos, 1.5f);
        GameAudio.PlayAt(SoundId.PortalFlash, data.Pos);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        ShowRider(true);
        if (registered) InteractableRegistry.Unregister(this);
        registered = false;
    }

    private void Update()
    {
        float pop = Mathf.Clamp01((Time.time - spawnedAt) / PopSeconds);
        if (pop < 1f) transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, BackOut(pop));
        else if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;

        double now = PhotonNetwork.Time;
        float turn = ColaCannonRules.TurretYawAt(data.Yaw, data.AimYaw, data.BoardAt, now);
        float pitch = ColaCannonRules.PitchAt(data.BoardAt, now);
        float t = data.Boarded ? (float)(now - data.BoardAt) : -1f;
        float kick = fired && t < ColaCannonRules.FireAt + 0.3f ? Mathf.Sin(Mathf.Clamp01((t - ColaCannonRules.FireAt) / 0.3f) * Mathf.PI) * 6f : 0f; // 발사 반동
        if (turret != null) VehicleSpot.RotateAround(turret, turretRest, turn, Vector3.up);
        Vector3 right = ColaCannonRules.Rotation(data.Yaw + turn) * Vector3.right;
        if (barrel != null) VehicleSpot.RotateAround(barrel, barrelRest, -(pitch - ColaCannonRules.RestPitch) + kick, right);
        if (hatch != null) VehicleSpot.RotateAround(hatch, hatchRest, ColaCannonRules.HatchAngle(data.BoardAt, now), right);
        if (!data.Boarded) return;

        float worldYaw = data.Yaw + turn;
        HideOrSeekPlayer mine = data.Owner == LocalActor ? LocalCookie() : null;
        if (!closeSound && t >= ColaCannonRules.EnterEnd) { closeSound = true; GameAudio.PlayAt(SoundId.DoorClose, ColaCannonRules.Breech(data.Pos, worldYaw, pitch)); }
        ShowRider(!(t >= ColaCannonRules.EnterEnd && t < ColaCannonRules.FireAt)); // 병 안에 있는 동안 몸을 숨긴다(모든 화면)

        if (!fired && t >= ColaCannonRules.FireAt) Fire(worldYaw);
        if (mine != null && !landed) RideLocal(mine, t, worldYaw, pitch);
        if (!landed && t >= ColaCannonRules.LandAt(data.Flight)) Land(mine);
    }

    // 주인 화면: 계단 앞 → 해치 → 병 안(겨누는 동안 포신을 따라) → 발사 뒤 포물선.
    private void RideLocal(HideOrSeekPlayer me, float t, float worldYaw, float pitch)
    {
        if (!ownerSetup)
        {
            ownerSetup = true;
            enterFrom = me.transform.position;
            foreach (Collider mineCol in me.GetComponentsInChildren<Collider>())
                foreach (Collider c in colliders)
                    if (c != null && mineCol != null) Physics.IgnoreCollision(mineCol, c, true);
        }
        Vector3 target;
        if (t < ColaCannonRules.FireAt)
        {
            Vector3 breech = ColaCannonRules.Breech(data.Pos, data.Yaw, ColaCannonRules.RestPitch) + Vector3.down * 0.9f;
            Vector3 inside = ColaCannonRules.Inside(data.Pos, worldYaw, pitch);
            float k = ColaCannonRules.EnterProgress(data.BoardAt, PhotonNetwork.Time);
            target = k < 1f ? (k < 0.5f ? Vector3.Lerp(enterFrom, breech, k / 0.5f) : Vector3.Lerp(breech, inside, (k - 0.5f) / 0.5f)) : inside;
            Vector3 look = ColaCannonRules.BarrelDirection(worldYaw, 0f);
            me.transform.rotation = Quaternion.LookRotation(look);
        }
        else
        {
            Vector3 mouth = ColaCannonRules.Mouth(data.Pos, worldYaw, ColaCannonRules.FirePitch) + Vector3.down * 0.9f;
            target = ColaCannonRules.FlightPoint(mouth, data.Dest, t - ColaCannonRules.FireAt, data.Flight);
        }
        me.TeleportTo(target);
    }

    private void Fire(float worldYaw)
    {
        fired = true;
        Vector3 dir = ColaCannonRules.BarrelDirection(worldYaw, ColaCannonRules.FirePitch);
        Vector3 mouth = ColaCannonRules.Mouth(data.Pos, worldYaw, ColaCannonRules.FirePitch);
        ColaCannonPresenter.Fizz(mouth, dir);
        GameAudio.PlayAt(SoundId.FlashBang, mouth);       // 펑
        GameAudio.PlayAt(SoundId.BalloonSplash, mouth);   // 콜라가 뿜어지는 소리
        if (registered) { InteractableRegistry.Unregister(this); registered = false; }
    }

    private void Land(HideOrSeekPlayer mine)
    {
        landed = true;
        VehicleSpot.Smoke(data.Dest, 0.6f, new Color(0.6f, 0.35f, 0.2f));
        GameAudio.PlayAt(SoundId.CookieLand, data.Dest);
        if (mine != null) mine.TeleportTo(data.Dest);
    }

    // 탄 사람 몸 보이기·숨기기(모든 화면). 대포가 사라질 때 늘 되돌린다.
    private void ShowRider(bool show)
    {
        if (bodyHidden == !show) return;
        if (!show)
        {
            IGameCharacter rider = FindCharacter(data.Owner);
            if (rider == null) return;
            riderRenderers = rider.gameObject.GetComponentsInChildren<Renderer>();
        }
        bodyHidden = !show;
        if (riderRenderers == null) return;
        foreach (Renderer r in riderRenderers) if (r != null) r.enabled = show;
        if (show) riderRenderers = null;
    }

    private static float BackOut(float x) { const float c = 1.7f; x -= 1f; return 1f + (c + 1f) * x * x * x + c * x * x; }

    private static int LocalActor => PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;

    private static HideOrSeekPlayer LocalCookie()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is HideOrSeekPlayer cookie && cookie.IsMine && CharacterRegistry.IsAlive(c)) return cookie;
        return null;
    }

    private static IGameCharacter FindCharacter(int actor)
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (CharacterRegistry.IsAlive(c) && c.View != null && c.View.Owner != null && c.View.Owner.ActorNumber == actor) return c;
        return null;
    }

    // ---------------- E 상호작용(주인만) ----------------

    public bool CanInteract(IGameCharacter character) =>
        EscapeManager.ActionsAllowed && !vanishing && !data.Boarded && Time.time - requestedAt > 1f
        && character != null && character.View != null && character.View.IsMine && data.Owner == LocalActor
        && ColaCannonRules.StageAt(data.SummonedAt, 0, 0f, PhotonNetwork.Time) == ColaCannonRules.Stage.Waiting;

    public float HoldSecondsFor(IGameCharacter character) => 0f;

    // 그 순간 카메라가 보는 수평 방향이 날아갈 방향이다.
    public void Interact(IGameCharacter character)
    {
        requestedAt = Time.time;
        Camera cam = Camera.main;
        Vector3 f = cam != null ? cam.transform.forward : character.gameObject.transform.forward;
        float aim = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        manager.Request(EscapeOp.CannonBoard, data.Id, Mathf.RoundToInt(aim));
    }

    public string GetLabel(IGameCharacter viewer) => EscapeTextsSO.Current.cannonBoard;
}
