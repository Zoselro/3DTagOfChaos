using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 괴물의 탈출 모드 상태: 마녀가 내리치면 괴물도 죽는다(EscapePlan.md §1.2, 결과 화면 "마녀에 의해 사망").
// 거대 괴물에게 밟혀도 죽는다(DeathCause.Giant, Request1009Plan.md §64.6) — 판은 계속되므로 본인 화면은 관전으로 넘어간다.
// 사망은 방장이 주먹이 닿는 순간에 확정해 DeathCause=Witch로 쓴다(GameRuleController, AuditFixPlan.md §8.1) — 이 값을 받으면 모든 화면에서 숨긴다.
// 예전에는 각 화면이 WitchStrike를 보고 스스로 숨고 괴물 본인이 사망 원인을 써, 조작된 WitchStrike 하나로 괴물이 멈췄다.
public class MonsterEscapeState : MonoBehaviourPunCallbacks
{
    public static readonly Color MonsterDebrisColor = new Color(0.25f, 0.12f, 0.3f); // 마녀에게 부서질 때 조각 색(대역도 같은 색)
    private MonsterController monster;
    private bool dead;

    public void Init(MonsterController owner)
    {
        monster = owner;
        ApplyIfDead(shatter: false); // 늦게 들어와 이미 죽어 있으면 조각 없이 숨긴다
    }

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changed)
    {
        if (monster != null && monster.View != null && target == monster.View.Owner && changed.ContainsKey(NetKeys.DeathCause)) ApplyIfDead(shatter: true);
    }

    // 본인 술래가 판 도중 죽었다: 1인칭 시점을 내려놓고 남은 쿠키를 관전한다(쿠키의 SpectatorController를 그대로 쓴다).
    private void EnterSpectator()
    {
        var view = GetComponent<MonsterViewSwitcher>();
        if (view != null) Destroy(view); // OnDestroy가 카메라를 3인칭 기본으로 되돌린다
        var spectator = GetComponent<SpectatorController>();
        if (spectator == null) spectator = gameObject.AddComponent<SpectatorController>();
        spectator.Init(monster.View);
        StartCoroutine(EnterNextFrame(spectator));
    }

    private static System.Collections.IEnumerator EnterNextFrame(SpectatorController spectator)
    {
        yield return null; // 시점 전환(MonsterViewSwitcher.OnDestroy)이 끝난 뒤
        if (spectator != null) spectator.EnterSpectatorMode(SpectatorKind.Broken);
    }

    private void ApplyIfDead(bool shatter)
    {
        if (dead || monster == null || monster.View == null || monster.View.Owner == null) return;
        if (!RoomState.TryGetPlayerInt(monster.View.Owner, NetKeys.DeathCause, out int cause) || (cause != (int)DeathCause.Witch && cause != (int)DeathCause.Giant)) return;
        dead = true;
        Renderer[] body = GetComponentsInChildren<Renderer>(true);
        if (shatter) WitchShatter.ShatterBody(body, MonsterDebrisColor); // 내리치는 순간 몸이 조각난다(Request1009Plan.md §23.5 X7)
        foreach (Renderer r in body) r.enabled = false; // 모든 화면에서 사라진다
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false; // 보이지 않는 몸에 막히지 않게
        monster.enabled = false;
        if (cause == (int)DeathCause.Giant && monster.View.IsMine) EnterSpectator();
    }
}
