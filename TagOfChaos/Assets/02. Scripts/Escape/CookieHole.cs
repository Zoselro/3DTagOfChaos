using UnityEngine;

// 괴물집·천막 뒤쪽 쿠키 구멍(Request1009Plan.md §28.11.7 H2). 구멍을 막는 보이지 않는 충돌체 — 내 캐릭터가 괴물이 아니면
// 충돌을 끈다(쿠키·스파이는 지나가고 괴물은 막힌다). 술래방 문(MonsterRoomDoor)과 반대. 각 화면은 자기 캐릭터만 물리로 움직인다.
public class CookieHole : MonoBehaviour
{
    private const float CheckSeconds = 0.25f;

    private Collider hole;
    private IGameCharacter ignoredFor;
    private float nextCheck;

    private void Awake() => hole = GetComponent<Collider>();

    private void Update()
    {
        if (hole == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckSeconds;
        IGameCharacter local = LocalCharacter();
        if (local == ignoredFor) return;
        if (ignoredFor != null) SetIgnored(ignoredFor, false);
        ignoredFor = local != null && local.Role != CharacterRole.Monster ? local : null;
        if (ignoredFor != null) SetIgnored(ignoredFor, true);
    }

    private void SetIgnored(IGameCharacter character, bool ignore)
    {
        if (character == null || character.gameObject == null) return;
        foreach (Collider c in character.gameObject.GetComponentsInChildren<Collider>(true))
            if (c != null && !c.isTrigger) Physics.IgnoreCollision(c, hole, ignore);
    }

    private static IGameCharacter LocalCharacter()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.View != null && c.View.IsMine && c.gameObject != null) return c;
        return null;
    }
}
