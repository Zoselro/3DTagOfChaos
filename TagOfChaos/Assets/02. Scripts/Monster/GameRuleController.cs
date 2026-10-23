using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 승리 판정(마스터 전용, GameRule.md §8.1). 탈출 모드 맵은 EscapeRules(Request1009Plan.md §25.5 D — 게임 제한시간 없음),
// 탈출 모드가 없는 씬은 쿠키 전원 파괴 → 괴물 승. 괴물 수와 무관하게 쿠키 쪽만 검사하면 되므로 다중 괴물 확장에도 그대로 동작한다.
// EscapeEnded: 탈출 모드가 끝났다 — 이유는 NetKeys.EscapeEndReason, 사람마다 결과는 Player Props로 계산한다(EscapePlan.md §5.8).
public enum GameResult { CookiesWin, MonsterWins, EscapeEnded }

public class GameRuleController : MonoBehaviourPunCallbacks
{
    // 온라인에서는 SetCustomProperties가 서버 응답 전까지 로컬 캐시에 반영되지 않는다. 응답 전 프레임마다 결과를 다시 보내거나,
    // 그 사이 다른 결과(파괴 완료 → 시간 종료)를 보내 나중 값이 이기지 않도록 한 판에 한 번만 보낸다(Bug-fix-plan.md §41 ㊹).
    private bool resultRequested;
    private bool witchRequested;
    private float witchStruckAt = -1f;
    private bool witchDeathsWritten;
    private readonly System.Collections.Generic.List<EscapeRules.PlayerInfo> escapePlayers = new System.Collections.Generic.List<EscapeRules.PlayerInfo>();

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || resultRequested) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        GamePhase phase = GamePhaseState.Current;
        if (EscapeManager.IsActive)
        {
            if (phase == GamePhase.Prepare || phase == GamePhase.Hunt || phase == GamePhase.TimeAttack) UpdateEscape(monsters, phase == GamePhase.TimeAttack);
            return;
        }
        if (phase != GamePhase.Hunt) return; // 준비 중이거나 이미 판정됨

        if (AllCookiesBroken(monsters)) Finish(GameResult.MonsterWins);
    }

    // 탈출 모드 판정(EscapePlan.md §1.2). 규칙은 EscapeRules(순수 계산)에 있다.
    private void UpdateEscape(int[] monsters, bool timeAttack)
    {
        EscapeState state = EscapeManager.Instance.State;
        escapePlayers.Clear();
        foreach (Player p in RoomState.Players)
        {
            int actor = p.ActorNumber;
            bool escaped = state != null && state.HasEscaped(actor); // 방장이 쓴 탈출 상태만(본인이 쓰는 속성은 믿지 않는다, S-05)
            escapePlayers.Add(new EscapeRules.PlayerInfo
            {
                IsMonster = monsters.Contains(actor),
                IsSpy = RoomState.IsSpy(actor),
                Escaped = escaped,
                Broken = RoomState.IsBroken(p),
            });
        }

        bool struck = RoomState.TryGetInt(NetKeys.WitchStrike, out _);
        // 내리친 뒤 지난 시간: 방장이 함께 쓴 시각(WitchStrikeAt) 기준이라 방장이 바뀌어도 이어진다(F-20). 없으면(옛 방) 이 방장의 시계로.
        float sinceStrike = -1f;
        if (!struck) { witchStruckAt = -1f; witchDeathsWritten = false; }
        else if (RoomState.TryGetDouble(NetKeys.WitchStrikeAt, out double strikeAt)) sinceStrike = (float)(PhotonNetwork.Time - strikeAt);
        else
        {
            if (witchStruckAt < 0f) witchStruckAt = Time.time;
            sinceStrike = Time.time - witchStruckAt;
        }
        // 마녀가 밟는 순간(도착 연출 테이크 2 끝) 방장이 사망을 확정한다(예전에는 각자 본인이 썼다 — AuditFixPlan.md §8.1, §24.10)
        if (struck && !witchDeathsWritten && sinceStrike >= WitchTimeline.FinaleImpactSeconds)
        {
            witchDeathsWritten = true;
            WriteWitchDeaths(state, monsters);
        }
        RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out double timeAttackEnd);

        // 안전 상한(§25.3 Q4): 아무도 탈출하지 못한 채 오래 지나면 마녀 타이머를 강제로 시작한다(맵 가운데를 앞 샷으로)
        if (!timeAttack && RoomState.TryGetDouble(NetKeys.PrepareEndTime, out double prepareEnd)
            && EscapeRules.ShouldForceWitchTimer(PhotonNetwork.Time, prepareEnd, GameSettings.Current.SafetyCapSeconds, false))
            EscapeManager.Instance.StartWitchTimer(Vector3.zero);

        switch (EscapeRules.Evaluate(escapePlayers, timeAttack, PhotonNetwork.Time, timeAttackEnd, sinceStrike, EscapeManager.Instance.IsDeparting(PhotonNetwork.Time)))
        {
            case EscapeRules.Decision.StrikeWitch:
                if (!witchRequested)
                {
                    witchRequested = true;
                    AuthorityProps.SetRoom(new Hashtable { { NetKeys.WitchStrike, (int)WitchMode.TimeAttack }, { NetKeys.WitchStrikeAt, PhotonNetwork.Time } });
                }
                break;
            case EscapeRules.Decision.EndAllResolved: FinishEscape(EscapeEndReason.AllResolved); break;
            case EscapeRules.Decision.EndTimeUp: FinishEscape(EscapeEndReason.TimeUp); break;
            case EscapeRules.Decision.EndWitchStrike: FinishEscape(EscapeEndReason.WitchStrike); break;
        }
    }

    // 마녀에게 죽는 사람: 괴물은 모두, 쿠키·스파이는 탈출하지 않았고 살아 있으며 이번 내리침에 죽는 쪽(처단이면 스파이만, WitchTimeline).
    private static void WriteWitchDeaths(EscapeState state, int[] monsters)
    {
        foreach (Player p in RoomState.Players)
        {
            int actor = p.ActorNumber;
            if (System.Array.IndexOf(monsters, actor) >= 0)
            {
                AuthorityProps.SetPlayer(p, new Hashtable { { NetKeys.DeathCause, (int)DeathCause.Witch } });
                continue;
            }
            if (RoomState.IsBroken(p) || (state != null && state.HasEscaped(actor))) continue;
            AuthorityProps.SetPlayer(p, new Hashtable { { NetKeys.HitCount, 2 }, { NetKeys.DeathCause, (int)DeathCause.Witch } });
        }
    }

    // 끝난 이유와 함께, 결과 화면이 스파이를 표시할 수 있도록 스파이 번호를 공개한다.
    private void FinishEscape(EscapeEndReason reason)
    {
        resultRequested = true;
        RoomState.TryGetIntArray(NetKeys.SpyActorNumbers, out int[] spies);
        AuthorityProps.SetRoom(new Hashtable
        {
            { NetKeys.GameResult, (int)GameResult.EscapeEnded },
            { NetKeys.EscapeEndReason, (int)reason },
            { NetKeys.RevealedSpies, spies ?? new int[0] },
        });
        Debug.Log($"[GameRule] Escape mode ended: {reason}");
    }

    private bool AllCookiesBroken(int[] monsters)
    {
        // 방에 남은 쿠키가 한 명도 없어도 true(괴물 승)가 된다 — 쿠키 전원이 나가면 더 이상 술래잡기가
        // 성립하지 않으므로 괴물 승리로 끝내는 것을 의도된 규칙으로 명시한다(research.md §8.15).
        foreach (Player p in RoomState.Players)
        {
            if (monsters.Contains(p.ActorNumber)) continue;
            if (!RoomState.IsBroken(p)) return false;
        }
        return true;
    }

    // 결과가 도착하면(내가 보낸 것이든 이전 방장이 보낸 것이든) 단계 판정이 Result가 되므로 플래그는 그대로 둔다.
    // 방장이 바뀌면 새 방장은 캐시(GamePhaseState)로 다시 판단한다 — 응답 전에 이전 방장이 나갔으면 다시 보낸다.
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        resultRequested = false;
        witchRequested = false;
        witchDeathsWritten = false; // 이전 방장이 이미 썼다면 같은 값을 다시 쓸 뿐이다
    }

    private void Finish(GameResult result)
    {
        resultRequested = true;
        AuthorityProps.SetRoom(new Hashtable { { NetKeys.GameResult, (int)result } });
    }
}
