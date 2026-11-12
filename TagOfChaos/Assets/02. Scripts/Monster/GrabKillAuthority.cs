using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// 처형 확정(방장 전용, AuditFixPlan.md §8.1 — S-04·S-06·S-14). 예전에는 쿠키 본인이 자기 파괴(HitCount)를 쓰고 괴물 본인이 잡은 수를 썼다 —
// 조작된 쿠키는 잡혀도 죽지 않고, 조작된 괴물은 거리와 관계없이 처형할 수 있었다. 이제 방장이 자기 화면의 위치로 검증한 뒤
// 피해자의 HitCount·DeathCause와 괴물의 CatchCount를 쓴다(쓰기는 AuthorityProps — 남이 바꾸면 RoomPropertyGuard가 되돌린다).
public static class GrabKillAuthority
{
    private static readonly Dictionary<int, float> lastConfirmAt = new Dictionary<int, float>();
    private static readonly Dictionary<int, int> catches = new Dictionary<int, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lastConfirmAt.Clear();
        catches.Clear();
    }

    // 괴물 화면이 본 순간(§46.2 GK1): 괴물·쿠키 위치와 그때의 PhotonNetwork.ServerTimestamp. Valid가 false면 옛 규칙(방장 화면 지금 위치).
    public struct GrabClaim
    {
        public bool Valid;
        public Vector3 Monster;
        public Vector3 Cookie;
        public int SeenStamp;
    }

    // 재현 도구(§70.2.6): 오프라인 한 대에서는 술래와 쿠키가 같은 사람이라 "술래 아님"으로 먼저 거절된다. 켜면 그래도 위치 판정을 해 로그로 남긴다(확정은 하지 않는다).
    public static bool DebugJudgeSameActor;
    public static string LastJudgeDetail { get; private set; }

    public static float LastClaimAge { get; private set; }               // 시험·로그용
    public static NetSecurityRules.GrabVerdict LastVerdict { get; private set; }

    // 방장: RequestGrabKill RPC를 받은 즉시(처형 연출이 몸을 소켓으로 옮기기 전) 부른다. 확정했으면 true.
    public static bool TryConfirm(HideOrSeekPlayer cookie, MonsterController monster, Player sender, GrabClaim claim)
    {
        if (!PhotonNetwork.IsMasterClient || cookie == null || monster == null || sender == null) return false;
        Player victim = cookie.View != null ? cookie.View.Owner : null;
        if (victim == null) return false;

        int monsterActor = sender.ActorNumber;
        GamePhase phase = GamePhaseState.Current;
        bool hunt = phase == GamePhase.Hunt || phase == GamePhase.TimeAttack;
        bool alive = !RoomState.IsBroken(victim) && !RoomState.IsMonster(victim.ActorNumber);
        bool onMap = !EscapeManager.HasLeftMap(victim.ActorNumber) && !EscapeManager.IsVehicleProtected(victim.ActorNumber); // 문 닫힌 호박 마차 안·해치 닫힌 콜라 대포 안·비행 중은 잡히지 않는다(§68.7·§68.9)
        if (onMap && cookie.IsDodgeInvulnerable) // 구르는 중에는 잡히지 않는다(§69 C3) — 방장 화면에서 본 회피 상태
        {
            LastVerdict = NetSecurityRules.GrabVerdict.OffMap;
            Debug.LogWarning($"[GrabKill] Rejected (dodging): monster actor {sender.ActorNumber} -> actor {victim.ActorNumber}.");
            return false;
        }
        float since = lastConfirmAt.TryGetValue(monsterActor, out float at) ? Time.time - at : float.MaxValue;
        var trigger = monster.GetComponent<MonsterGrabKillTrigger>();
        GameSettingsSO settings = GameSettings.Current;
        float reach = (trigger != null ? trigger.BodyFrontOffset : 0f) + settings.GrabReachFromFront;

        NetSecurityRules.GrabVerdict verdict = NetSecurityRules.GrabPreconditions(RoomState.IsMonster(monsterActor), hunt, alive, onMap, since, monster.GrabKillDuration * 0.8f);
        string detail;
        if (verdict == NetSecurityRules.GrabVerdict.Confirmed) verdict = Judge(cookie, monster, claim, reach, settings.MaxGrabHeight, out detail);
        else detail = $"phase {phase}, alive {alive}, onMap {onMap}, since {since:F1}s";
        if (verdict != NetSecurityRules.GrabVerdict.Confirmed && DebugJudgeSameActor)
        {
            NetSecurityRules.GrabVerdict would = Judge(cookie, monster, claim, reach, settings.MaxGrabHeight, out string judged);
            LastJudgeDetail = $"{would}: {judged}";
            Debug.Log($"[GrabKill] (test) position verdict {would}: {judged}");
        }
        LastVerdict = verdict;

        if (verdict != NetSecurityRules.GrabVerdict.Confirmed)
        {
            Debug.LogWarning($"[GrabKill] Rejected ({verdict}): monster actor {monsterActor} -> actor {victim.ActorNumber} ({detail}).");
            return false;
        }
        Debug.Log($"[GrabKill] Confirmed: monster actor {monsterActor} -> actor {victim.ActorNumber} ({detail}).");

        lastConfirmAt[monsterActor] = Time.time;
        AuthorityProps.SetPlayer(victim, new Hashtable { { NetKeys.HitCount, 2 }, { NetKeys.DeathCause, (int)DeathCause.Monster } });

        // 잡은 횟수(결과 화면): 방장이 센다. 방장이 바뀌었으면 지금까지 기록된 값에서 이어 센다.
        int known = RoomState.TryGetPlayerInt(sender, NetKeys.CatchCount, out int c) ? c : 0;
        int next = Mathf.Max(known, catches.TryGetValue(monsterActor, out int counted) ? counted : 0) + 1;
        catches[monsterActor] = next;
        AuthorityProps.SetPlayer(sender, new Hashtable { { NetKeys.CatchCount, next } });
        return true;
    }

    // 위치 판정. 기록이 있으면 되돌림 판정, 없으면(입장 직후·옛 클라이언트) 방장 화면의 지금 위치 + 3 m.
    private static NetSecurityRules.GrabVerdict Judge(HideOrSeekPlayer cookie, MonsterController monster, GrabClaim claim,
        float reach, float maxHeight, out string detail)
    {
        Vector3 monsterNow = monster.transform.position;
        Vector3 cookieNow = cookie.transform.position;
        float nowDistance = Flat(cookieNow - monsterNow);

        if (claim.Valid && PositionHistory.TryGet(cookie.View.ViewID, out PositionTrack cookieTrack)
                        && PositionHistory.TryGet(monster.View.ViewID, out PositionTrack monsterTrack))
        {
            float age = NetAuthority.AgeOf(claim.SeenStamp);
            LastClaimAge = age;
            double now = NetAuthority.Time;
            double seenAt = now - Mathf.Max(0f, age);
            double from = seenAt - NetSecurityRules.GrabRewindWindow;
            // 남의 캐릭터의 기록은 받은 패킷(보낸 시각·위치)이다(§70.2.5 D4) — 길의 끝도 화면에 그려진 위치가 아니라 마지막 패킷 위치로 잇는다.
            bool cookieRemote = PositionHistory.IsRemote(cookie.View), monsterRemote = PositionHistory.IsRemote(monster.View);
            bool cookieKnown = cookieTrack.TryMinDistance(claim.Cookie, from, now, cookieRemote ? cookieTrack.NewestPoint : cookieNow, out float cookieError);
            // 괴물이 보낸 것은 자기 "지금" 위치 — 방장이 가진 마지막 패킷은 그보다 앞선 시각이라 기록 끝을 마지막 속도로 앞으로 늘려 비교한다.
            // 괴물 본인이 방장이면 기록이 곧 실제 위치라 늘리지 않는다.
            float lead = monsterRemote ? NetSecurityRules.PacketLead(seenAt, monsterTrack.NewestTime) : 0f;
            bool monsterKnown = monsterTrack.TryMinDistanceLeading(claim.Monster, from, now, monsterRemote ? monsterTrack.NewestPoint : monsterNow, lead, out float monsterError);
            if (cookieKnown && monsterKnown)
            {
                // 돌진 중이었으면 예고된 돌진 길로도 잰다(§70.2.5 D2) — 초속 68 m는 초당 10번 패킷으로 따라갈 수 없다.
                string dashNote = string.Empty;
                if (MonsterDashLedger.TryGet(monster.View.ViewID, seenAt, out DashWindow dash))
                {
                    float dashError = dash.PathError(claim.Monster, seenAt, MonsterDashLedger.ClaimTimeSlack);
                    dashNote = $", dash path {dashError:F1} m";
                    monsterError = Mathf.Min(monsterError, dashError);
                }
                detail = $"seen {Flat(claim.Cookie - claim.Monster):F1} m, now {nowDistance:F1} m, path error cookie {cookieError:F1} / monster {monsterError:F1} m{dashNote}, age {age * 1000f:F0} ms";
                return NetSecurityRules.JudgeGrabClaim(claim.Monster, claim.Cookie, reach, maxHeight, age, cookieError, monsterError);
            }
        }

        LastClaimAge = -1f;
        detail = $"no history, now {nowDistance:F1} m";
        return NetSecurityRules.CanConfirmGrabKill(true, true, true, true, monsterNow, cookieNow, reach, maxHeight, float.MaxValue, 0f)
            ? NetSecurityRules.GrabVerdict.Confirmed : NetSecurityRules.GrabVerdict.OutOfReach;
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
}
