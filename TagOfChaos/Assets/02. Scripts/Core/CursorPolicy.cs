using UnityEngine;

// 마우스 자유 시점(쿠키 3인칭 Camera_Ctrl·괴물 1인칭 MonsterFirstPersonCamera)의 커서 규칙(Request1009Plan.md §59 CL).
// 평소에는 커서를 잠그고 마우스 이동만으로 시점을 돌린다. 아래일 때는 커서를 풀고 시점을 멈춘다:
// 결과 화면 · ESC 메뉴(설정·방 설정·나가기 확인 포함) · 채팅 입력 · 확인창(대기실 강퇴 등) · 커서 보이기 키(기본 Alt)를 누르는 동안.
public static class CursorPolicy
{
    public static bool ShouldBeFree =>
        GamePhaseState.Current == GamePhase.Result || EscMenu.IsOpen || PlayerInput.IsChatting || ConfirmDialog.AnyOpen || PlayerInput.ShowCursorHeld;

    public static void Apply(bool free)
    {
        CursorLockMode want = free ? CursorLockMode.None : CursorLockMode.Locked;
        if (Cursor.lockState != want) Cursor.lockState = want;
    }
}
