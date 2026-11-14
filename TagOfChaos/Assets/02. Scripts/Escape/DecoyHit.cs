// AI(미끼)도 도구에 맞는다(Request1009Plan.md §76 A): ToolHit 대상 목록(int[])에 AI는 음수 "-AI 번호"로 넣는다.
// PhotonView 번호는 늘 양수(1001~)라 겹치지 않고, 옛 클라이언트는 음수를 PhotonView.Find 실패로 건너뛴다.
// AI가 맞는 도구는 스턴건·물풍선·섬광탄(§76 요청 1). 맞은 동안 서 있는 시간은 진짜 쿠키와 같다.
public static class DecoyHit
{
    public static int ToTarget(int decoyId) => -decoyId;
    public static bool IsDecoy(int target) => target < 0;
    public static int DecoyId(int target) => -target;

    public static bool CanHit(ToolKind kind) => kind == ToolKind.StunGun || kind == ToolKind.WaterBalloon || kind == ToolKind.Flashbang;

    // 방장 두뇌가 AI를 세워 두는 시간: 물풍선은 갇힘 + 떨어짐(StunReceiver.FallStunSeconds), 나머지는 쿠키 기절 시간.
    public static float HoldSeconds(ToolSO tool)
    {
        if (tool == null) return 0f;
        return tool.Kind == ToolKind.WaterBalloon ? tool.StunSecondsCookie + StunReceiver.FallStunSeconds : tool.StunSecondsCookie;
    }
}
