using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 거대 괴물이 술래방을 부수며 나온다(Request1009Plan.md §81 R, 사용자 결정). EscapeManager.Start가 붙인다.
// - 거대 괴물은 술래방 가운데에서 솟아오른다(GiantBrain.TryPickSpawn). 솟기 시작하는 순간(GiantRules.RevealStart) 모든 화면이 같은 시각에
//   술래방 전체(벽·지붕·문·장식)를 건물처럼 무너뜨린다(DestructionPresenter — §82 조각 모델 MonsterRoom_<씬> + 구운 애니메이션·잔해·먼지·흔들림·소리).
//   바닥(방 바닥 높이 + FloorTop 아래)과 진저브레드 괴물 엘리베이터는 남긴다.
// - 무너진 뒤에는 술래방 가드(쿠키 내보내기)와 술래방 문을 끈다 — 그 판 동안 술래방 자리는 그냥 땅이다(다음 판은 맵을 다시 읽어 원래대로).
// - 그 순간 술래방 안에 있던 사람(술래 포함)은 밟혀 죽는다 — 방장만 판정(거대 괴물 밟기와 같은 방 속성).
// - 늦게 들어온 화면은 연출·판정 없이 무너진 상태로 시작한다. 판정에 새 네트워크 값은 없다(거대 괴물 상태 + 마녀 등장 시각).
public class MonsterRoomBreak : MonoBehaviour
{
    public const float FloorTop = 0.6f;          // 방 바닥 높이 + 이것 아래 = 바닥(남김)
    private const double AnimateWindow = 3.0;    // 솟기 시작 뒤 이 안에 본 화면만 무너지는 연출·판정

    public static bool Broken { get; private set; }

    private EscapeManager manager;

    public void Init(EscapeManager owner)
    {
        manager = owner;
        Broken = false;
        if (owner != null && owner.MonsterRoom != null) FracturedModels.Request(FractureNameFor(owner.MonsterRoom)); // 솟기 전에 미리 읽어 둔다(§82)
    }

    private void OnDestroy() => Broken = false;

    // 술래방 안 부분 중 남길 것: 바닥(위 끝이 방 바닥 + FloorTop 아래)·괴물 엘리베이터. 굽기(GiantNavMeshBaker)도 같은 규칙을 쓴다.
    public static bool Keep(Bounds b, float floorY, bool elevator) => elevator || b.max.y < floorY + FloorTop;

    public static bool Keep(Component c, Bounds b, float floorY) =>
        Keep(b, floorY, c.GetComponentInParent<MonsterElevator>() != null || c.GetComponentInParent<MonsterElevatorMarker>() != null || NameHas(c.transform, "Elevator"));

    private static bool NameHas(Transform t, string part)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name.Contains(part)) return true;
            if (t.name == "MonsterRoom") break;
        }
        return false;
    }

    private void Update()
    {
        if (Broken || manager == null || manager.MonsterRoom == null) return;
        if (!GiantState.TryGetCurrent(out _) || !WitchTimeline.TryGetAppearAt(out double appearAt)) return;
        double since = PhotonNetwork.Time - GiantRules.RevealStart(appearAt);
        if (since < 0) return;
        Break(since < AnimateWindow);
    }

    private void Break(bool animate)
    {
        Broken = true;
        Transform room = manager.MonsterRoom;
        DestructibleUnit u = BuildUnit(room);
        MonsterRoomGuard guard = room.GetComponent<MonsterRoomGuard>();
        if (guard != null) guard.enabled = false;
        var presenter = GetComponent<DestructionPresenter>();
        if (presenter != null) presenter.PresentExtra(u, animate);
        else DestructionNet.SetBroken(u, true);
        Debug.Log($"[MonsterRoomBreak] Monster room broken by the giant (animate {animate}, {u.Renderers.Length} renderers, {u.Colliders.Length} colliders).");
        if (animate && PhotonNetwork.IsMasterClient) KillInside();
    }

    // 술래방 전체를 건물 하나로(목록 밖 — Id −1)
    public static DestructibleUnit BuildUnit(Transform room)
    {
        float floorY = room.position.y;
        var renderers = new List<Renderer>();
        var colliders = new List<Collider>();
        var lights = new List<Light>();
        bool any = false;
        Bounds b = new Bounds(room.position, Vector3.zero);
        foreach (Renderer r in room.GetComponentsInChildren<Renderer>())
        {
            if (Keep(r, r.bounds, floorY)) continue;
            renderers.Add(r);
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        foreach (Collider c in room.GetComponentsInChildren<Collider>())
            if (!c.isTrigger && !Keep(c, c.bounds, floorY)) colliders.Add(c);
        foreach (Light l in room.GetComponentsInChildren<Light>())
            if (!Keep(l, new Bounds(l.transform.position, Vector3.zero), floorY)) lights.Add(l);
        return new DestructibleUnit
        {
            Id = -1, Grade = DestructibleGrade.Building, Root = room, ModelRoot = room,
            FractureName = FractureNameFor(room), // §82: Blender로 미리 조각낸 술래방(바닥·엘리베이터 뺀 부분) + 바깥으로 무너지는 애니메이션
            Bounds = b, Zone = b, Level = (int)DestructibleGrade.Building,
            Renderers = renderers.ToArray(), Colliders = colliders.ToArray(), Lights = lights.ToArray(),
        };
    }

    public static string FractureNameFor(Transform room) => "MonsterRoom_" + room.gameObject.scene.name;

    // 무너질 때 술래방 안에 있던 사람은 밟혀 죽는다(사용자 결정) — 거대 괴물 밟기(GiantBrain.Judge)와 같은 값.
    private void KillInside()
    {
        var people = new List<IGameCharacter>();
        GiantBrain.CollectTargets(people);
        foreach (IGameCharacter c in people)
        {
            Vector3 p = c.gameObject.transform.position;
            if (!manager.InMonsterRoom(p)) continue;
            Player owner = c.View.Owner;
            if (c.Role == CharacterRole.Monster)
                AuthorityProps.SetPlayer(owner, new Hashtable { { NetKeys.DeathCause, (int)DeathCause.Giant } });
            else
                AuthorityProps.SetPlayer(owner, new Hashtable { { NetKeys.HitCount, 2 }, { NetKeys.DeathCause, (int)DeathCause.Giant } });
            Debug.Log($"[MonsterRoomBreak] Actor {owner.ActorNumber} ({c.Role}) was crushed inside the monster room.");
        }
    }
}
