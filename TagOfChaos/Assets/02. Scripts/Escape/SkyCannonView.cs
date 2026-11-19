using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Heaven Of Bakery 하늘 대포 한 대(Request1009Plan.md §89.24·§89.25) — 맵 씬에 박혀 있다(HeavenOfBakeryMapBuilder.BuildSkyCannons가 붙인다).
// 생기거나 사라지지 않고, 탑승 기록(EscapeState.Cannons 중 Sky = Index + 1)이 있으면 그 시각표로 해치·포신·발사·착지를 모든 화면이 같게 그린다.
// 쿠키(술래 아님)가 뒤 계단 앞에서 E → SkyCannonBoard. 탄 사람 화면만 자기 캐릭터를 계단 → 병 안 → 포물선 → 착지로 옮긴다.
// AI는 방장의 DecoyBrain이 같은 기록을 만들고, 몸은 DecoyRoute(Fly)가 옮긴다.
public class SkyCannonView : MonoBehaviour, IHoldInteractable, IInteractionLabel
{
    public const string TurretName = "SkyCannon_Turret";
    public const string BarrelName = "SkyCannon_Barrel";
    public const string HatchName = "SkyCannon_Hatch";

    [SerializeField] private int index;
    [SerializeField] private Transform landing;

    private static readonly List<SkyCannonView> all = new List<SkyCannonView>();
    public static IReadOnlyList<SkyCannonView> All => all;

    public int Index => index;
    public Vector3 Pos => transform.position;
    public float Yaw => transform.eulerAngles.y;
    public Vector3 Landing => landing != null ? landing.position : transform.position;
    public Vector3 StepsPoint => SkyCannonRules.StepsPoint(Pos, Yaw);

    private Transform barrel, hatch;
    private Quaternion barrelRest, hatchRest;
    private bool registered, fired, landed, closeSound, bodyHidden, ownerSetup, boardSound;
    private int rideId;
    private float requestedAt = -10f;
    private Vector3 enterFrom;
    private Collider[] colliders;
    private Renderer[] riderRenderers;
    private string targetKey;

    public static SkyCannonView Find(int i)
    {
        foreach (SkyCannonView v in all) if (v != null && v.index == i) return v;
        return null;
    }

#if UNITY_EDITOR
    public void EditorBind(int cannonIndex, Transform landingPoint) { index = cannonIndex; landing = landingPoint; }
#endif

    private void Awake()
    {
        barrel = EscapeCue.FindDeep(transform, BarrelName);
        hatch = EscapeCue.FindDeep(transform, HatchName);
        if (barrel != null) barrelRest = barrel.localRotation;
        if (hatch != null) hatchRest = hatch.localRotation;
        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
        InteractableRegistry.Register(this);
        registered = true;
    }

    private void OnDisable()
    {
        all.Remove(this);
        ShowRider(true);
        if (registered) InteractableRegistry.Unregister(this);
        registered = false;
    }

    private void Update()
    {
        EscapeManager manager = EscapeManager.Instance;
        double now = PhotonNetwork.Time;
        EscapeState.Cannon ride = default;
        if (manager == null || !SkyCannonRules.TryActive(manager.State, index, now, out ride)) { Rest(); return; }
        if (ride.Id != rideId) Begin(ride);

        float t = (float)(now - ride.BoardAt);
        float pitch = SkyCannonRules.PitchAt(ride.BoardAt, now);
        float kick = fired && t < ColaCannonRules.FireAt + 0.3f ? Mathf.Sin(Mathf.Clamp01((t - ColaCannonRules.FireAt) / 0.3f) * Mathf.PI) * 6f : 0f;
        Pose(pitch, kick, ColaCannonRules.HatchAngle(ride.BoardAt, now));

        HideOrSeekPlayer mine = ride.Owner == LocalActor ? LocalCookie() : null;
        if (!boardSound) { boardSound = true; GameAudio.PlayAt(SoundId.DoorOpen, SkyCannonRules.Breech(Pos, Yaw, SkyCannonRules.RestPitch)); }
        if (!closeSound && t >= ColaCannonRules.EnterEnd) { closeSound = true; GameAudio.PlayAt(SoundId.DoorClose, SkyCannonRules.Breech(Pos, Yaw, pitch)); }
        ShowRider(ride.Owner, !(t >= ColaCannonRules.EnterEnd && t < ColaCannonRules.FireAt)); // 병 안에 있는 동안 진짜 쿠키 몸을 숨긴다(AI는 DecoyView가 경로로 숨긴다)
        if (!fired && t >= ColaCannonRules.FireAt) Fire();
        if (mine != null && !landed) RideLocal(mine, ride, t, pitch);
        if (!landed && t >= ColaCannonRules.LandAt(ride.Flight)) Land(mine, ride);
    }

    private void Begin(EscapeState.Cannon ride)
    {
        ShowRider(true);
        rideId = ride.Id;
        fired = landed = closeSound = ownerSetup = boardSound = false;
        // 이미 진행 중인 탑승을 늦게 본 화면(늦게 들어옴): 지난 소리·거품은 건너뛴다
        float t = (float)(PhotonNetwork.Time - ride.BoardAt);
        if (t > 0.5f) boardSound = true;
        if (t > ColaCannonRules.EnterEnd + 0.5f) closeSound = true;
        if (t > ColaCannonRules.FireAt + 0.5f) fired = true;
        if (t > ColaCannonRules.LandAt(ride.Flight) + 0.5f) landed = true;
    }

    private void Rest()
    {
        if (rideId != 0) { ShowRider(true); rideId = 0; RestoreCollisions(); }
        Pose(SkyCannonRules.RestPitch, 0f, 0f);
    }

    private void Pose(float pitch, float kick, float hatchAngle)
    {
        Vector3 right = transform.right;
        if (barrel != null) VehicleSpot.RotateAround(barrel, barrelRest, -pitch + kick, right);
        if (hatch != null) VehicleSpot.RotateAround(hatch, hatchRest, hatchAngle, right);
    }

    // 탄 사람 화면: 계단 앞 → 해치 → 병 안(겨누는 동안 포신을 따라) → 발사 뒤 포물선.
    private void RideLocal(HideOrSeekPlayer me, EscapeState.Cannon ride, float t, float pitch)
    {
        if (!ownerSetup)
        {
            ownerSetup = true;
            enterFrom = me.transform.position;
            SetCollisions(me, true);
        }
        Vector3 target;
        if (t < ColaCannonRules.FireAt)
        {
            Vector3 breech = SkyCannonRules.Breech(Pos, Yaw, SkyCannonRules.RestPitch) + Vector3.down * 0.9f;
            Vector3 inside = SkyCannonRules.Inside(Pos, Yaw, pitch);
            float k = ColaCannonRules.EnterProgress(ride.BoardAt, PhotonNetwork.Time);
            target = k < 1f ? (k < 0.5f ? Vector3.Lerp(enterFrom, breech, k / 0.5f) : Vector3.Lerp(breech, inside, (k - 0.5f) / 0.5f)) : inside;
            me.transform.rotation = SkyCannonRules.Rotation(Yaw);
        }
        else target = ColaCannonRules.FlightPoint(SkyCannonRules.LaunchPoint(Pos, Yaw), ride.Dest, t - ColaCannonRules.FireAt, ride.Flight);
        me.TeleportTo(target);
    }

    private HideOrSeekPlayer ignoring;
    private void SetCollisions(HideOrSeekPlayer me, bool ignore)
    {
        ignoring = ignore ? me : null;
        if (me == null) return;
        foreach (Collider mineCol in me.GetComponentsInChildren<Collider>())
            foreach (Collider c in colliders)
                if (c != null && mineCol != null) Physics.IgnoreCollision(mineCol, c, ignore);
    }

    private void RestoreCollisions() { if (ignoring != null) SetCollisions(ignoring, false); }

    private void Fire()
    {
        fired = true;
        Vector3 dir = ColaCannonRules.BarrelDirection(Yaw, SkyCannonRules.FirePitch);
        Vector3 mouth = SkyCannonRules.Mouth(Pos, Yaw, SkyCannonRules.FirePitch);
        ColaCannonPresenter.Fizz(mouth, dir);
        GameAudio.PlayAt(SoundId.FlashBang, mouth);
        GameAudio.PlayAt(SoundId.BalloonSplash, mouth);
    }

    private void Land(HideOrSeekPlayer mine, EscapeState.Cannon ride)
    {
        landed = true;
        VehicleSpot.Smoke(ride.Dest, 0.6f, new Color(0.6f, 0.35f, 0.2f));
        GameAudio.PlayAt(SoundId.CookieLand, ride.Dest);
        if (mine != null) mine.TeleportTo(ride.Dest);
        RestoreCollisions();
    }

    // 진짜 쿠키 몸 그리기 끄기·켜기(forceRenderingOff — ColaCannonView와 같은 이유, §69 B5). AI(음수 번호)는 여기서 다루지 않는다.
    private void ShowRider(int owner, bool show)
    {
        if (owner <= 0) { ShowRider(true); return; }
        if (bodyHidden == !show) return;
        if (!show)
        {
            IGameCharacter rider = FindCharacter(owner);
            if (rider == null) return;
            riderRenderers = rider.gameObject.GetComponentsInChildren<Renderer>();
        }
        bodyHidden = !show;
        if (riderRenderers == null) return;
        foreach (Renderer r in riderRenderers) if (r != null) r.forceRenderingOff = !show;
        if (show) riderRenderers = null;
    }

    private void ShowRider(bool show)
    {
        if (!show || !bodyHidden) return;
        bodyHidden = false;
        if (riderRenderers != null) foreach (Renderer r in riderRenderers) if (r != null) r.forceRenderingOff = false;
        riderRenderers = null;
    }

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

    // ---------------- E 상호작용(쿠키만) ----------------

    public Vector3 InteractionPoint => StepsPoint;
    public float InteractionRange => SkyCannonRules.BoardReach;

    public bool CanInteract(IGameCharacter character)
    {
        if (!EscapeManager.ActionsAllowed || Time.time - requestedAt < 1f) return false;
        if (!(character is HideOrSeekPlayer) || character.View == null || !character.View.IsMine) return false;
        if (RoomState.IsMonster(LocalActor) || EscapeManager.IsRidingVehicle(LocalActor)) return false; // 술래는 타지 않는다(§89.25 Q1)
        EscapeManager manager = EscapeManager.Instance;
        return manager != null && !SkyCannonRules.Busy(manager.State, index, PhotonNetwork.Time);
    }

    public float HoldSecondsFor(IGameCharacter character) => 0f;

    public void Interact(IGameCharacter character)
    {
        requestedAt = Time.time;
        EscapeManager.Instance.Request(EscapeOp.SkyCannonBoard, index, 0);
    }

    public string GetLabel(IGameCharacter viewer)
    {
        EscapeTextsSO texts = EscapeTextsSO.Current;
        string island = TargetName(texts);
        return string.IsNullOrEmpty(island) ? texts.cannonBoard : string.Format(texts.skyCannonBoard, island);
    }

    // 착지 자리 섬에 지금 놓인 서브 맵(판마다 섬 내용이 바뀐다 — MapLayoutRandomizer)의 이름.
    private string TargetName(EscapeTextsSO texts)
    {
        if (targetKey == null)
        {
            targetKey = string.Empty;
            float best = float.MaxValue;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("SubMap_")) continue;
                float d = (t.position - Landing).sqrMagnitude;
                if (d < best) { best = d; targetKey = t.name; }
            }
        }
        switch (targetKey)
        {
            case "SubMap_Almond": return texts.skyIslandAlmond;
            case "SubMap_Chocolate": return texts.skyIslandChocolate;
            case "SubMap_Flour": return texts.skyIslandFlour;
            default: return null;
        }
    }
}
