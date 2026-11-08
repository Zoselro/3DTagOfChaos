using Photon.Pun;
using UnityEngine;

// 호박 마차 한 대(Request1009Plan.md §68.7) — 모든 화면. PumpkinCarriagePresenter가 만든다.
// 문 각도·사라짐·도착 연기는 상태 시각(PhotonNetwork.Time)으로 모든 화면이 같게 그리고, 주인 화면만 자기 캐릭터를 문 → 안 → 도착 지점으로 옮긴다.
// 주인이 문 앞(PumpkinCarriageRules.DoorPoint)에서 E를 누르면 방장에게 CarriageBoard를 보낸다(다른 사람에게는 안내가 뜨지 않는다).
public class PumpkinCarriageView : MonoBehaviour, IHoldInteractable, IInteractionLabel
{
    public const string DoorName = "PROP_PumpkinCarriage_Door";
    private const float PopSeconds = 0.35f;

    private EscapeManager manager;
    private EscapeState.Carriage data;
    private Transform door;
    private Quaternion doorClosed;
    private bool registered, departed, ownerSetup, vanishing;
    private float spawnedAt, requestedAt = -10f;
    private Vector3 enterFrom;
    private Renderer[] renderers;
    private Collider[] colliders;

    public int Id => data.Id;
    public Vector3 InteractionPoint => PumpkinCarriageRules.DoorPoint(data.Pos, data.Yaw);
    public float InteractionRange => PumpkinCarriageRules.BoardReach;

    public void Bind(EscapeManager owner, EscapeState.Carriage carriage)
    {
        manager = owner;
        data = carriage;
        door = EscapeCue.FindDeep(transform, DoorName);
        if (door != null) doorClosed = door.localRotation;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        spawnedAt = Time.time;
        // 이미 다 지난 마차(늦게 들어온 사람)는 연기 없이
        if (PhotonNetwork.Time - carriage.SummonedAt < 1.0)
        {
            PumpkinCarriagePresenter.Smoke(carriage.Pos, 1.4f);
            GameAudio.PlayAt(SoundId.PortalOpen, carriage.Pos);
            transform.localScale = Vector3.one * 0.2f;
        }
        else spawnedAt -= PopSeconds;
        InteractableRegistry.Register(this);
        registered = true;
    }

    public void Apply(EscapeState.Carriage carriage)
    {
        bool justBoarded = !data.Boarded && carriage.Boarded;
        data = carriage;
        if (justBoarded) GameAudio.PlayAt(SoundId.DoorOpen, InteractionPoint);
    }

    // 상태에서 빠졌다: 아직 떠나지 않았으면 연기와 함께 사라진다(타지 않고 10초 지남 등).
    public void Vanish()
    {
        if (vanishing) return;
        vanishing = true;
        if (!departed)
        {
            PumpkinCarriagePresenter.Smoke(data.Pos, 1.4f);
            GameAudio.PlayAt(SoundId.PortalFlash, data.Pos);
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (registered) InteractableRegistry.Unregister(this);
        registered = false;
    }

    private void Update()
    {
        float pop = Mathf.Clamp01((Time.time - spawnedAt) / PopSeconds);
        if (pop < 1f) transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, BackOut(pop));
        else if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;

        double now = PhotonNetwork.Time;
        if (door != null) VehicleSpot.RotateAround(door, doorClosed, -PumpkinCarriageRules.DoorAngle(data.BoardAt, now), Vector3.up);
        if (!data.Boarded) return;

        float t = (float)(now - data.BoardAt);
        HideOrSeekPlayer mine = data.Owner == LocalActor ? LocalCookie() : null;
        if (mine != null && !departed) RideLocal(mine, now);
        if (!closeSoundPlayed && t >= PumpkinCarriageRules.EnterEnd) { closeSoundPlayed = true; GameAudio.PlayAt(SoundId.DoorClose, InteractionPoint); }
        if (!departed && t >= PumpkinCarriageRules.TeleportAt) Depart(mine);
    }

    private bool closeSoundPlayed;

    // 주인 화면: 문 앞 → 안(들어감)으로 옮기고, 도착 시각까지 안에 둔다. 마차 충돌체와는 부딪히지 않게 한다.
    private void RideLocal(HideOrSeekPlayer me, double now)
    {
        if (!ownerSetup)
        {
            ownerSetup = true;
            enterFrom = me.transform.position;
            foreach (Collider mineCol in me.GetComponentsInChildren<Collider>())
                foreach (Collider c in colliders)
                    if (c != null && mineCol != null) Physics.IgnoreCollision(mineCol, c, true);
        }
        Vector3 inside = PumpkinCarriageRules.InsidePoint(data.Pos, data.Yaw);
        Vector3 doorPoint = PumpkinCarriageRules.DoorPoint(data.Pos, data.Yaw) + Vector3.up * (inside.y - data.Pos.y) * 0.5f;
        float k = PumpkinCarriageRules.EnterProgress(data.BoardAt, now);
        // 문 앞까지 먼저, 그다음 안으로(두 구간)
        Vector3 target = k < 0.35f ? Vector3.Lerp(enterFrom, doorPoint, k / 0.35f) : Vector3.Lerp(doorPoint, inside, (k - 0.35f) / 0.65f);
        me.TeleportTo(target);
        Vector3 look = inside - doorPoint;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) me.transform.rotation = Quaternion.LookRotation(look);
    }

    // 마차가 연기와 함께 사라지고, 주인은 도착 지점에 나타난다(모든 화면에 두 곳 연기).
    private void Depart(HideOrSeekPlayer mine)
    {
        departed = true;
        PumpkinCarriagePresenter.Smoke(data.Pos, 1.4f);
        PumpkinCarriagePresenter.Smoke(data.Dest, 0.8f);
        GameAudio.PlayAt(SoundId.PortalFlash, data.Pos);
        GameAudio.PlayAt(SoundId.PortalFlash, data.Dest);
        foreach (Renderer r in renderers) if (r != null) r.enabled = false;
        foreach (Collider c in colliders) if (c != null) c.enabled = false;
        if (registered) { InteractableRegistry.Unregister(this); registered = false; }
        if (mine != null) mine.TeleportTo(data.Dest);
    }

    private static float BackOut(float x) { const float c = 1.7f; x -= 1f; return 1f + (c + 1f) * x * x * x + c * x * x; }

    private static int LocalActor => PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;

    private static HideOrSeekPlayer LocalCookie()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is HideOrSeekPlayer cookie && cookie.IsMine && CharacterRegistry.IsAlive(c)) return cookie;
        return null;
    }

    // ---------------- E 상호작용(주인만) ----------------

    public bool CanInteract(IGameCharacter character) =>
        EscapeManager.ActionsAllowed && !vanishing && !departed && !data.Boarded && Time.time - requestedAt > 1f
        && character != null && character.View != null && character.View.IsMine && data.Owner == LocalActor
        && PumpkinCarriageRules.StageAt(data.SummonedAt, 0, PhotonNetwork.Time) == PumpkinCarriageRules.Stage.Waiting;

    public float HoldSecondsFor(IGameCharacter character) => 0f;

    public void Interact(IGameCharacter character)
    {
        requestedAt = Time.time;
        manager.Request(EscapeOp.CarriageBoard, data.Id);
    }

    public string GetLabel(IGameCharacter viewer) => EscapeTextsSO.Current.carriageBoard;
}
