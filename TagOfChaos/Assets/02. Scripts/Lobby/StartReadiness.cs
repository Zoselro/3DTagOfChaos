// 판 시작 판단(Request1009Plan.md §43 F4). 네트워크와 분리한 순수 계산이라 EditMode로 시험한다.
public static class StartReadiness
{
    // 시작 버튼이 눌리는 조건: 정원이 찼고, 방에 있는 괴물이 다 정해졌고, 모두 대기실에 들어와 있다.
    public static bool IsReady(bool roomFull, bool monstersChosen, bool everyoneInLobby) =>
        roomFull && monstersChosen && everyoneInLobby;

    // 방장 교체가 진행 중이거나 막 끝났으면 시작을 미룬다 — 교체와 씬 변경이 서버에서 엇갈리지 않도록.
    public static bool IsSettling(bool switchPending, float secondsSinceLastSwitch, float settleSeconds) =>
        switchPending || secondsSinceLastSwitch < settleSeconds;
}
