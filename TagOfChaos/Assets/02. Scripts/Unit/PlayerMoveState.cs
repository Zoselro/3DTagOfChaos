public enum PlayerMoveState
{
    Idle,
    Walk,
    Run,
    Jump,
    Dodge,
    Held,   // 그랩당한 상태(GameRule.md §4.1)
    Broken, // GrabKill로 파괴된 상태(GameRule.md §4.4)
    Carried, // 다른 쿠키에게 두 팔로 안겨 들린 상태(Request1009Plan.md §16.3) — 괴물에게 잡힌 처형 연출은 Held 그대로
    Dance,   // 제자리 춤(B키·AI, Request1009Plan.md §44.9) — 어떤 춤인지는 Animator int DanceId(DanceCatalogSO)
    StrafeLeft,  // 조준 방향을 본 채 왼쪽 옆걸음(§75 Q2) — 동기화는 int라 끝에만 덧붙인다
    StrafeRight, // 오른쪽 옆걸음
    WalkBack,    // 뒷걸음
}
