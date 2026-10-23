using Photon.Pun;

// 한 판의 진행 단계(research.md §12 E2). 단계는 여전히 Room Props 조합(PaintPhaseEndTime·MonsterJoined·GameResult)으로
// 동기화되지만(네트워크 프로토콜 불변), 그 조합을 "지금 어느 단계인가"로 해석하는 곳을 여기 한 곳으로 모았다 —
// 예전에는 색칠 UI·붓 커서·캔버스·카운트다운·승패 판정이 같은 조건을 각자 다시 썼다. 새 단계를 넣을 때는
// GamePhase 항목과 Evaluate의 조건부터 추가한다.
public enum GamePhase
{
    Lobby,      // 판 시작 전(대기실)
    Prepare,    // 맵에 들어온 직후 — 괴물은 술래방에 갇혀 있다(Request1009Plan.md §25.5 C, 예전 색칠 단계 자리)
    Hunt,       // 술래문이 열린 뒤 — 게임 제한시간은 없다
    TimeAttack, // 첫 탈출 뒤 마녀 타이머(§25.5 G)
    Result,          // 승패 판정 완료
}

public static class GamePhaseState
{
    public static GamePhase Current
    {
        get
        {
            bool hasPrepareEnd = RoomState.TryGetDouble(NetKeys.PrepareEndTime, out double prepareEnd);
            bool hasResult = RoomState.TryGetInt(NetKeys.GameResult, out _);
            // 마녀 타이머는 첫 탈출 뒤 남은 사람이 있을 때만(타이머 끝 시각이 기록된 경우)
            bool witchTimer = RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out _);
            return Evaluate(hasPrepareEnd, prepareEnd, hasResult, PhotonNetwork.Time, witchTimer);
        }
    }

    // 네트워크 상태와 분리한 순수 해석(테스트용).
    public static GamePhase Evaluate(bool hasPrepareEnd, double prepareEndTime, bool hasResult, double now, bool witchTimer = false)
    {
        if (hasResult) return GamePhase.Result;
        if (witchTimer) return GamePhase.TimeAttack;
        if (hasPrepareEnd) return now < prepareEndTime ? GamePhase.Prepare : GamePhase.Hunt;
        return GamePhase.Lobby;
    }

    // 지금 해당 단계이고 그 단계의 종료 시각이 있으면 true(카운트다운 표시용).
    public static bool TryGetActiveEndTime(GamePhase phase, out double endTime)
    {
        endTime = 0;
        if (Current != phase) return false;
        switch (phase)
        {
            case GamePhase.Prepare: return RoomState.TryGetDouble(NetKeys.PrepareEndTime, out endTime);
            case GamePhase.TimeAttack: return RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out endTime);
            default: return false;
        }
    }
}
