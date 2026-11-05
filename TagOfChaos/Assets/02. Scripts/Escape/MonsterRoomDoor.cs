using UnityEngine;

// 술래방 문(Request1009Plan.md §25.5 C, 테스트 맵 MonsterDoor_0~3). 쿠키·스파이 몸은 막고 괴물만 지나간다 — 각자 화면이 자기 캐릭터만
// 움직이므로, 괴물 본인 화면에서 문 충돌체를 끈다. 준비 시간(PrepareEndTime 전)에는 괴물도 막혀 술래방에 갇혀 있다.
public class MonsterRoomDoor : MonoBehaviour
{
    private const float CheckSeconds = 0.25f;

    private Collider door;   // 끄고 켜므로 Awake에서 한 번 잡는다(GetComponent는 꺼진 것도 돌려준다)
    private Renderer look;
    private float nextCheck;

    private void Awake()
    {
        door = GetComponent<Collider>();
        look = GetComponent<Renderer>();
    }

    private void Update()
    {
        if (door == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckSeconds;
        bool open = GamePhaseState.Current != GamePhase.Prepare && GamePhaseState.Current != GamePhase.Lobby;
        bool passable = open && LocalMonster() != null;
        // 괴물 본인 화면에서는 열린 문의 충돌체를 끈다. 예전에는 Physics.IgnoreCollision으로 괴물 몸과의 충돌만 껐지만, 괴물 몸은
        // 연속 충돌 검사(ContinuousDynamic)라 그 설정이 듣지 않아 문에 막혔다(§52 Play Mode에서 확인). 각 화면은 자기 캐릭터만 물리로
        // 움직이므로(남의 캐릭터는 네트워크 위치를 따른다) 문을 꺼도 다른 사람의 쿠키는 자기 화면에서 그대로 막힌다.
        if (door.enabled == passable) door.enabled = !passable;
        // 괴물에게 열린 문은 흐리게(지나갈 수 있음), 닫힌 문·쿠키에게는 그대로
        if (look != null) look.enabled = !passable;
    }

    private static MonsterController LocalMonster()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.Role == CharacterRole.Monster && c.View != null && c.View.IsMine && c is MonsterController m) return m;
        return null;
    }
}
