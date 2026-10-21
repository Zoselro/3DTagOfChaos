using UnityEngine;

// 술래방 안에 들어간 쿠키·스파이를 문 바깥으로 내보낸다(Request1009Plan.md §28.15 E1). 문은 쿠키에게 늘 벽이라 어떻게든 들어가면 갇힌다 —
// 각 화면이 자기 캐릭터만 본다(내 캐릭터가 괴물이 아니고, 들려 있지·쓰러져 있지 않고, 안쪽에 DwellSeconds 넘게 있으면).
// 방 안쪽 = 벽 안쪽 면(innerHalf) 안 + 모든 문 면 안. 옮길 자리는 가장 가까운 문 바깥의 빈 곳(EscapeManager.IsValidTeleport와 같은 규칙).
public class MonsterRoomGuard : MonoBehaviour
{
    private const float CheckSeconds = 0.25f;
    private const float DwellSeconds = 0.5f;
    private const float DoorMargin = 0.3f;
    private static readonly float[] EjectRadii = { 3f, 4.5f, 6f, 7.5f };
    private const int EjectAngles = 9;

    [SerializeField, Min(0f)] private float innerHalf = 11.7f;
    [SerializeField] private Transform[] doors = new Transform[0];

    private Vector3[] doorPositions; // 문은 움직이지 않는다 — 한 번만 모은다
    private float nextCheck;
    private float insideSince = -1f;

    public float InnerHalf => innerHalf;

    public bool IsInside(Vector3 p) => IsInside(transform.position, innerHalf, DoorPositions(), p);

    // 방장 이동 검사 예외(EscapeAuthority.TrackPositions): 방 안에서 문 바깥 옮길 자리 범위 안으로 한 번에 옮겨진 것.
    public bool IsEjectMove(Vector3 from, Vector3 to)
    {
        if (!IsInside(from) || IsInside(to)) return false;
        float reach = EjectRadii[EjectRadii.Length - 1] + 1f;
        foreach (Vector3 door in DoorPositions())
            if (Flat(to - door).sqrMagnitude <= reach * reach) return true;
        return false;
    }

    // 순수 판정(시험용): 정사각 방 안쪽이고, 모든 문 면(문 → 방 가운데 방향) 안쪽인지.
    public static bool IsInside(Vector3 center, float half, Vector3[] doorPositions, Vector3 p)
    {
        Vector3 d = p - center;
        if (Mathf.Abs(d.x) >= half || Mathf.Abs(d.z) >= half) return false;
        foreach (Vector3 door in doorPositions)
        {
            Vector3 outward = Flat(door - center);
            if (outward.sqrMagnitude < 0.01f) continue;
            outward.Normalize();
            if (Vector3.Dot(Flat(p - door), outward) > -DoorMargin) return false;
        }
        return true;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckSeconds;
        HideOrSeekPlayer local = LocalEscaper();
        if (local == null || local.IsMovementLocked || !IsInside(local.transform.position))
        {
            insideSince = -1f;
            return;
        }
        if (insideSince < 0f) { insideSince = Time.unscaledTime; return; }
        if (Time.unscaledTime - insideSince < DwellSeconds) return;
        insideSince = -1f;
        if (TryEjectPoint(local.transform.position, out Vector3 to))
        {
            Debug.Log($"[MonsterRoomGuard] {local.name} was inside the monster room at {local.transform.position} — moved out to {to}.");
            local.TeleportTo(to);
            GameAudio.PlayCharacter(SoundId.Respawn, local);
        }
        else Debug.LogWarning($"[MonsterRoomGuard] {local.name} is inside the monster room but no free spot outside a door was found.");
    }

    // 가장 가까운 문부터, 문 바깥 반원 위 자리를 가까운 순으로 본다.
    public bool TryEjectPoint(Vector3 from, out Vector3 point)
    {
        point = default;
        EscapeManager manager = EscapeManager.Instance;
        if (manager == null || doors.Length == 0) return false;
        var order = (Transform[])doors.Clone();
        System.Array.Sort(order, (a, b) => DistanceOrMax(a, from).CompareTo(DistanceOrMax(b, from)));
        foreach (Transform door in order)
        {
            if (door == null) continue;
            Vector3 outward = Flat(door.position - transform.position).normalized;
            if (outward == Vector3.zero) continue;
            foreach (float radius in EjectRadii)
                for (int i = 0; i < EjectAngles; i++)
                {
                    // 0, +22.5, -22.5, +45 … ±90°(문 정면부터 옆으로)
                    int step = (i + 1) / 2;
                    float angle = (i % 2 == 1 ? 1f : -1f) * step * 90f / ((EjectAngles - 1) / 2);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * outward;
                    Vector3 feet = manager.SnapToGround(Flat(door.position) + dir * radius + Vector3.up * (transform.position.y + 2f));
                    if (manager.IsValidTeleport(feet) && !IsInside(feet)) { point = feet + Vector3.up * 0.05f; return true; }
                }
        }
        return false;
    }

    private static float DistanceOrMax(Transform t, Vector3 p) => t != null ? Flat(t.position - p).sqrMagnitude : float.MaxValue;

    private Vector3[] DoorPositions()
    {
        if (doorPositions != null && Application.isPlaying) return doorPositions;
        doorPositions = new Vector3[doors.Length];
        for (int i = 0; i < doors.Length; i++) doorPositions[i] = doors[i] != null ? doors[i].position : transform.position;
        return doorPositions;
    }

    private static HideOrSeekPlayer LocalEscaper()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (CharacterRegistry.IsAlive(c) && c.Role != CharacterRole.Monster && c.View != null && c.View.IsMine && c is HideOrSeekPlayer p) return p;
        return null;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

#if UNITY_EDITOR
    public void EditorBind(float wallInnerHalf, Transform[] roomDoors)
    {
        innerHalf = wallInnerHalf;
        doors = roomDoors;
        doorPositions = null;
    }
#endif
}
