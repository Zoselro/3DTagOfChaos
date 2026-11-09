using System.Collections;
using UnityEngine;

// 괴물 전용 엘리베이터(Request1009Plan.md §58): 지상 술래방 가운데와 지하 동굴 가운데의 발판 한 쌍.
// 괴물이 발판 위에서 상호작용 키를 누르면 빛기둥이 차오르는 짧은 연출 뒤 반대쪽 발판 위로 옮긴다. 쿠키·스파이는 쓸 수 없다.
// 각 화면이 자기 괴물만 옮긴다 — 괴물 위치는 PhotonView로 이미 동기화되므로 방장 판정·Room Prop이 필요 없다.
public class MonsterElevator : MonoBehaviour, IInteractable
{
    private const float Cooldown = 1.5f;     // 도착한 뒤 바로 되돌아가지 않게
    private const float ArrivalLift = 0.35f;

    [SerializeField] private MonsterElevator destination;
    [Tooltip("타는 동안 아래에서 위로 차오르는 빛기둥(선택).")]
    [SerializeField] private Transform beam;
    [SerializeField, Min(0.5f)] private float interactionRange = 2.6f;
    [SerializeField, Min(0f)] private float rideSeconds = 1.2f;

    private bool riding;
    private float readyAt;
    private Vector3 beamScale = Vector3.one;

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => interactionRange;
    public Vector3 ArrivalPoint => transform.position + Vector3.up * ArrivalLift;
    public MonsterElevator Destination => destination;

    private void Awake()
    {
        if (GetComponent<MonsterElevatorMarker>() == null) gameObject.AddComponent<MonsterElevatorMarker>(); // 괴물에게만 보이는 표지판·고리·방향 표시(§69 D4)
        if (beam != null) { beamScale = beam.localScale; SetBeam(0f); }
    }

    private void OnEnable() => InteractableRegistry.Register(this);
    private void OnDisable() => InteractableRegistry.Unregister(this);

    // 준비 시간(쿠키가 숨는 동안)에는 타지 못한다(Request1009Plan.md §62) — 술래 사냥·타임어택 중에만.
    public static bool PhaseAllows(GamePhase phase) => phase == GamePhase.Hunt || phase == GamePhase.TimeAttack;

    public bool CanInteract(IGameCharacter character) =>
        destination != null && !riding && Time.time >= readyAt && PhaseAllows(GamePhaseState.Current)
        && character.Role == CharacterRole.Monster && character.View != null && character.View.IsMine;

    public void Interact(IGameCharacter character)
    {
        if (CanInteract(character) && character is MonsterController monster) StartCoroutine(Ride(monster));
    }

    private IEnumerator Ride(MonsterController monster)
    {
        riding = true;
        GameAudio.PlayAt(SoundId.DoorOpen, transform.position);
        for (float t = 0f; t < rideSeconds; t += Time.deltaTime)
        {
            SetBeam(t / rideSeconds);
            yield return null;
        }
        SetBeam(0f);
        // 타는 동안 기절·처형 등으로 조작을 잃었으면 옮기지 않는다
        if (monster != null && monster.CanInteract && PhaseAllows(GamePhaseState.Current))
        {
            monster.TeleportTo(destination.ArrivalPoint);
            destination.OnArrived();
        }
        riding = false;
        readyAt = Time.time + Cooldown;
    }

    private void OnArrived()
    {
        readyAt = Time.time + Cooldown;
        GameAudio.PlayAt(SoundId.Respawn, transform.position);
    }

    private void SetBeam(float k)
    {
        if (beam == null) return;
        float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        beam.localScale = new Vector3(beamScale.x, beamScale.y * s, beamScale.z);
        beam.gameObject.SetActive(s > 0.001f);
    }

#if UNITY_EDITOR
    public void EditorBind(MonsterElevator to, Transform glow)
    {
        destination = to;
        beam = glow;
    }
#endif
}
