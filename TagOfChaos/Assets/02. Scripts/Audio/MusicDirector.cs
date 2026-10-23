using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

// 배경음 지휘(SoundPlan.md §3.3·S3). AudioRuntime이 소유하고 매 프레임 Tick을 부르면 0.25초마다 지금 상황을 모아
// MusicRules에 "지금 들어야 할 곡"을 묻고, 단계가 바뀐 순간에는 스팅어·결과 징글을 낸다.
// 새 RPC·Props 없이 모든 클라이언트가 이미 아는 값(씬 이름·Room Props 단계·괴물 대기)만 본다 — 늦게 들어온 사람은
// 지금 곡만 듣고 지난 스팅어는 듣지 않는다(첫 판단에서는 스팅어를 내지 않는다).
public class MusicDirector
{
    public const float PollSeconds = 0.25f;

    private readonly MusicPlayer player;
    private float nextPoll;
    private string lastScene;
    private GamePhase lastPhase;
    private bool monstersKnown;

    public MusicDirector(MusicPlayer player)
    {
        this.player = player;
    }

    public bool Enabled { get; set; } = true;

    public void Tick(float now)
    {
        if (!Enabled || now < nextPoll) return;
        nextPoll = now + PollSeconds;

        string sceneName = SceneManager.GetActiveScene().name;
        bool sceneChanged = sceneName != lastScene;
        var context = new MusicContext
        {
            Scene = MusicRules.SceneFromName(sceneName),
            Phase = RoomState.IsInRoom() ? GamePhaseState.Current : GamePhase.Lobby,
            LocalIsMonster = RoomState.IsInRoom() && RoomState.IsLocalMonster(),
        };

        if (!sceneChanged) PlayStingers(context);
        lastScene = sceneName;
        lastPhase = context.Phase;
        monstersKnown = AreMonstersKnown();

        float fade = sceneChanged ? MusicRules.SceneFadeSeconds
                   : context.Phase == GamePhase.Result ? MusicRules.ResultFadeSeconds
                   : MusicRules.PhaseFadeSeconds;
        player.Play(MusicRules.Select(context), fade);
    }

    private void PlayStingers(MusicContext context)
    {
        // 대기실: 괴물이 정해진 순간(가마솥 선정 → 공개)
        if (context.Scene == MusicScene.GameLobby && !monstersKnown && AreMonstersKnown())
            player.Stinger(MusicId.StMonsterReveal);

        if (context.Phase == lastPhase || context.Scene == MusicScene.Lobby || context.Scene == MusicScene.GameLobby) return;
        if (context.Phase == GamePhase.Result)
        {
            if (RoomState.TryGetInt(NetKeys.GameResult, out int result)) player.Stinger(Jingle((GameResult)result));
            return;
        }
        MusicId stinger = MusicRules.StingerFor(lastPhase, context.Phase);
        if (stinger != MusicId.None) player.Stinger(stinger);
    }

    private static bool AreMonstersKnown() =>
        RoomState.IsInRoom() && RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) && monsters.Length > 0;

    // 내 결과로 징글을 고른다. 탈출 판정은 결과 화면과 같은 기준(EscapeManager.HasEscaped — 방장이 쓴 탈출 상태, F-07).
    private static MusicId Jingle(GameResult result)
    {
        Player local = PhotonNetwork.LocalPlayer;
        bool localIsMonster = RoomState.IsLocalMonster();
        bool survivedOrEscaped = result == GameResult.EscapeEnded ? local != null && EscapeManager.HasEscaped(local.ActorNumber) : !RoomState.IsBroken(local);
        bool anyCookieEscaped = false; // 스파이의 로켓 탈출은 세지 않는다(결과 화면의 괴물 승리 기준과 같게)
        if (result == GameResult.EscapeEnded)
        {
            RoomState.TryGetIntArray(NetKeys.SpyActorNumbers, out int[] spies);
            foreach (Player p in RoomState.Players)
            {
                if (RoomState.IsMonster(p.ActorNumber) || (spies != null && System.Array.IndexOf(spies, p.ActorNumber) >= 0)) continue;
                if (EscapeManager.HasEscaped(p.ActorNumber)) { anyCookieEscaped = true; break; }
            }
        }
        return MusicRules.JingleFor(result, localIsMonster, survivedOrEscaped, anyCookieEscaped);
    }
}
