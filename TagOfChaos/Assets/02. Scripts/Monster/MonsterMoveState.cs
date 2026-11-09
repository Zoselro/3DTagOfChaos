// PlayerMoveState와 동일한 계약 — enum 이름이 MonsterAnimator.controller의 트리거 파라미터 이름과 정확히
// 일치해야 한다. 상태·파라미터 이름의 오타(GrapKill)는 GrabKill로 정리했다(research.md §8.21).
public enum MonsterMoveState
{
    Idle,
    Walk,
    TentacleDash,
    GrabKill,
    TentacleDashWindup, // 돌진 준비 자세(§69 F2) — MonsterWindupClipBuilder가 상태·트리거를 넣는다
}
