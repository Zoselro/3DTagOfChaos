// RaiseEvent 코드. 여러 도메인이 공유하므로 Core/에 둔다(research.md §7).
public static class NetEventCodes
{
    public const byte PaintStroke = 1;  // 물풍선 전신 칠하기(PlayerPaintCanvas.ForceFillColor)를 다른 클라이언트에 전파
    public const byte ClaimMonster = 2; // GameRule.md §2.1 — 가마솥 진입, 괴물 자원 신청
    // 3(ClearColor, 손 색칠 지우기)·4(FillAll)는 없앴다(Request1009Plan.md §38). 옛 빌드와 혼동하지 않도록 비워 둔다.
    public const byte StartGameRequest = 5; // Bug-fix-plan.md §33 — 호스트가 진행 권한을 가진 방장에게 판 시작을 요청
    public const byte DoorStateRequest = 6; // GameLobbyScene.md §14 — 상호작용한 클라이언트가 방장에게 문 상태 변경을 요청
    public const byte EscapeRequest = 7; // EscapePlan.md §5.2 — 상자·줍기·떨어뜨리기·설치·훔치기·로켓·탈출·도구 요청(EscapeOp)
    public const byte EscapeNotice = 8;  // 방장이 모두에게 보내는 알림(EscapeNoticeKind) — 스파이 잡힘, 로켓에 끼움 등
    public const byte ToolHit = 9;       // 방장이 승인한 도구 명중 — 맞은 사람 본인이 기절·떨어뜨리기·색칠을 처리한다
    public const byte RoomSettingsRequest = 10; // research.md R5-22 — 호스트가 방장에게 방 설정(인원·제한시간·타임어택) 변경을 요청
    public const byte KickRequest = 11; // Request1009Plan.md §19.6 — 호스트가 방장에게 대기실 강퇴를 요청(내용: 대상 액터 번호)
    public const byte Kicked = 12;      // 방장이 강퇴 대상에게만 보낸다(내용: 대상 액터 번호) — 받은 사람은 방을 나간다
    public const byte VoiceClip = 14; // Request1009Plan.md §25.3 Q6 — 대기실에서 녹음한 목소리 조각(판 시작 때 한 번, 보낸 사람 본인 것)
    // 13(LobbyDoorState, 괴물 대기실 대기 중 문 공유)은 대기를 없애며 지웠다(§38). 비워 둔다.
}
