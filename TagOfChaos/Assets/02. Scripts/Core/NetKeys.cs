// Room/Player CustomProperties 키. Lobby/GameManager/Unit/ColorTag/Monster 전 도메인이 참조하므로
// Core/에 둔다(research.md §7 — 원래 ColorTag/에 있었다). 구 4라운드 색상 미니게임 전용 키
// (RoundIndex/RoundEndTime/Color0~3/Tagger*/VoteColorIndex/CookiesDeparted)는 읽고 쓰는 곳이 없어
// 제거했다(research.md §8.21).
//
// 새 키를 추가하면 아래 Scopes 표에 대상(Room/Player)과 수명(Round/Session)을 함께 적는다. 판 초기화 목록
// (RoundRoomKeys/RoundPlayerKeys)은 이 표에서 자동으로 만들어진다 — 예전에는 초기화 목록을 따로 손으로 관리해
// 빠뜨리면 두 번째 판에서만 드러나는 버그가 됐다(research.md §12 E8). 표에서 빠진 키는 EditMode 테스트가 잡는다.
public static class NetKeys
{
    // 괴물 선정(GameRule.md §2)
    public const string MonsterActorNumbers = "MonsterActorNumbers"; // int[]

    // 판 시작 신호 겸 준비 끝 시각(Request1009Plan.md §25.5 C — 예전 색칠 끝 PaintPhaseEndTime·괴물 합류 MonsterJoined를 대신한다)
    public const string PrepareEndTime = "PrepareEndTime";

    // 이탈/방장 위임(GameRule.md §7)
    public const string MonsterDepartedAt = "MonsterDepartedAt";

    // 승패(GameRule.md §8)
    public const string GameResult = "GameResult";

    // 상호작용 문(GameLobbyScene.md §14) — Hashtable { 문 ID(string): 상태(byte, InteractableDoor.DoorState) }
    public const string DoorStates = "DoorStates";

    // 이번 판 맵 씬 이름(string) — 시작 때 방장이 고르고, 대기실에 남은 괴물도 이 씬으로 간다(GameScenePlan.md §3.2)
    public const string GameMapScene = "GameMapScene";

    // 방 설정 — 방을 만들 때 방장이 정한다. 판이 바뀌어도 유지된다. 게임 제한시간(RoomTimeLimit)은 없앴다(Request1009Plan.md §25.5 D).
    public const string RoomSpyCount = "RoomSpyCount";             // int — 스파이 수(0~1)
    public const string TimeAttackDuration = "TimeAttackDuration"; // int 초 — 마녀 타이머
    // 서버 지역 옮기기(Request1003Plan.md §1) — 방장이 쓰는 "지역|표"(string). 받으면 모두 새 지역의 같은 방으로 옮겨 간다.
    public const string RegionMove = "RegionMove";
    // 옮겨 간 방에 붙는 표(string) — 같은 이름의 남의 방에 잘못 들어가지 않았는지 확인한다.
    public const string RegionMoveToken = "RegionMoveToken";

    // 탈출 모드(EscapePlan.md §5.2)
    public const string SpyActorNumbers = "SpyActorNumbers";   // int[] — 시작 버튼 때 방장이 뽑는다
    public const string EscapeState = "EscapeState";           // byte[] — EscapeState.Encode(방장만 쓴다)
    public const string SpyEscapedAt = "SpyEscapedAt";         // double — 로켓이 떠난 시각(타임어택 시작)
    public const string TimeAttackEndTime = "TimeAttackEndTime"; // double
    public const string WitchStrike = "WitchStrike";           // int WitchMode — 마녀가 내리쳤다(1 타임어택 끝, 2 쿠키 탈출 뒤 처단)
    public const string WitchStrikeAt = "WitchStrikeAt";       // double — 내리친 시각(PhotonNetwork.Time). 방장이 바뀌어도 같은 기준으로 경과를 잰다(F-20)
    public const string WitchMode = "WitchMode";               // int WitchMode — 마녀가 오는 이유(Request1009Plan.md §19)
    public const string WitchAppearAt = "WitchAppearAt";       // double — 마녀가 나타나기 시작하는 시각(앞 샷: 로켓·탈출구가 끝난 뒤)
    public const string EscapeEndReason = "EscapeEndReason";   // int — EscapeEndReason
    public const string RevealedSpies = "RevealedSpies";       // int[] — 게임이 끝난 뒤 결과 화면용으로 공개
    public const string FirstEscapeAt = "FirstEscapeAt";       // double — 첫 탈출 시각(마녀 타이머 시작, Request1009Plan.md §25.5 G)
    public const string FirstEscapePoint = "FirstEscapePoint"; // Vector3 — 첫 탈출 지점(마녀 연출 앞 샷)
    public const string CharacterAssignments = "CharacterAssignments"; // int[] — [액터, 캐릭터 번호, …] 시작 때 방장이 정한 캐릭터(중복 없음, §25.5 B)

    // Player CustomProperties (GameRule.md §4.4 — hitCount는 0 또는 2만 실제로 쓰임, v3.6). HitCount·DeathCause는 방장이 처형·마녀를 확정할 때 쓴다.
    public const string HitCount = "HitCount";
    public const string SkinIndex = "SkinIndex"; // int — SkinCatalogSO 인덱스, GameRule.md §1.5 (판이 바뀌어도 유지)
    public const string SelectedSlot = "SelectedSlot"; // int — 인벤토리에서 고른 칸(본인만 쓴다, EscapePlan.md §1.9)
    public const string CatchCount = "CatchCount";     // int — 괴물이 잡은 횟수(방장이 처형을 확정할 때 쓴다)
    public const string DeathCause = "DeathCause";     // int — DeathCause(1 괴물, 2 마녀)
    public const string HasVoice = "HasVoice";         // bool — 대기실에서 목소리를 녹음했다(본인이 쓴다, Request1009Plan.md §26 Q11·Q26)
    public const string LoadedScene = "LoadedScene";   // string — 지금 들어와 있는 씬(본인이 쓴다, RoomSceneFollower — Request1009Plan.md §43)

    public enum Target { Room, Player }

    // 누가 쓰는 키인가(AuditFixPlan.md §7.2). Master = 방장만 쓴다(방장이 아닌 사람이 바꾸면 방장이 되돌린다 — RoomPropertyGuard).
    // Self = 그 플레이어 본인만 쓴다(남이 바꾸면 방장이 되돌린다). Photon Cloud는 어떤 클라이언트든 방·남의 플레이어 속성을 쓸 수 있으므로
    // 서버 대신 이 표로 쓰기 권한을 정한다.
    public enum Writer { Master, Self }

    public enum Lifetime
    {
        Round,   // 한 판이 끝나 GameLobbyScene으로 돌아올 때 지운다(RoundStateResetter)
        Session, // 방에 있는 동안 유지한다
    }

    public readonly struct Scope
    {
        public readonly string Key;
        public readonly Target Target;
        public readonly Lifetime Lifetime;
        public readonly Writer Writer;

        public Scope(string key, Target target, Lifetime lifetime, Writer writer = Writer.Master)
        {
            Key = key;
            Target = target;
            Lifetime = lifetime;
            Writer = writer;
        }
    }

    // 모든 키의 대상·수명 선언(빌드에서 리플렉션 없이 쓰도록 명시적인 표로 둔다).
    public static readonly Scope[] Scopes =
    {
        new Scope(MonsterActorNumbers, Target.Room, Lifetime.Round),
        new Scope(PrepareEndTime, Target.Room, Lifetime.Round),
        new Scope(MonsterDepartedAt, Target.Room, Lifetime.Round),
        new Scope(GameResult, Target.Room, Lifetime.Round),
        new Scope(DoorStates, Target.Room, Lifetime.Round),
        new Scope(GameMapScene, Target.Room, Lifetime.Round),
        new Scope(RoomSpyCount, Target.Room, Lifetime.Session),
        new Scope(TimeAttackDuration, Target.Room, Lifetime.Session),
        new Scope(RegionMove, Target.Room, Lifetime.Round),
        new Scope(RegionMoveToken, Target.Room, Lifetime.Session),
        new Scope(SpyActorNumbers, Target.Room, Lifetime.Round),
        new Scope(EscapeState, Target.Room, Lifetime.Round),
        new Scope(SpyEscapedAt, Target.Room, Lifetime.Round),
        new Scope(TimeAttackEndTime, Target.Room, Lifetime.Round),
        new Scope(WitchStrike, Target.Room, Lifetime.Round),
        new Scope(WitchStrikeAt, Target.Room, Lifetime.Round),
        new Scope(WitchMode, Target.Room, Lifetime.Round),
        new Scope(WitchAppearAt, Target.Room, Lifetime.Round),
        new Scope(EscapeEndReason, Target.Room, Lifetime.Round),
        new Scope(RevealedSpies, Target.Room, Lifetime.Round),
        new Scope(FirstEscapeAt, Target.Room, Lifetime.Round),
        new Scope(FirstEscapePoint, Target.Room, Lifetime.Round),
        new Scope(CharacterAssignments, Target.Room, Lifetime.Round),
        new Scope(HitCount, Target.Player, Lifetime.Round),
        new Scope(SkinIndex, Target.Player, Lifetime.Session, Writer.Self),
        new Scope(SelectedSlot, Target.Player, Lifetime.Round, Writer.Self),
        new Scope(CatchCount, Target.Player, Lifetime.Round),
        new Scope(DeathCause, Target.Player, Lifetime.Round),
        new Scope(HasVoice, Target.Player, Lifetime.Session, Writer.Self),
        new Scope(LoadedScene, Target.Player, Lifetime.Session, Writer.Self),
    };

    // 한 판이 끝나 GameLobbyScene으로 돌아올 때 초기화해야 하는 키 목록(RoundStateResetter, research.md §8.7).
    public static readonly string[] RoundRoomKeys = Collect(Target.Room, Lifetime.Round);
    public static readonly string[] RoundPlayerKeys = Collect(Target.Player, Lifetime.Round);

    // 판 초기화 때 본인이 지우는 키 / 방장이 모두의 것을 지우는 키(쓰기 권한과 같게).
    public static readonly string[] RoundSelfPlayerKeys = Collect(Target.Player, Lifetime.Round, Writer.Self);
    public static readonly string[] RoundMasterPlayerKeys = Collect(Target.Player, Lifetime.Round, Writer.Master);

    // 이 키를 방장만 쓰는지(대상별). 표에 없는 키(PUN 내부 키 등)는 보호하지 않는다.
    public static bool IsMasterKey(string key, Target target) => TryGetScope(key, target, out Scope s) && s.Writer == Writer.Master;
    public static bool IsSelfKey(string key) => TryGetScope(key, Target.Player, out Scope s) && s.Writer == Writer.Self;

    // 접두어 키: 뒤에 번호가 붙어 여러 개가 생기는 키 무리(AI마다 경로 하나 "DR_<id>", Request1009Plan.md §44.6).
    // 상수가 아니라 표(Scopes)에 키 하나로 적을 수 없으므로 따로 둔다 — 쓰기 권한·판 초기화는 접두어로 알아본다.
    public static readonly string DecoyRoutePrefix = "DR_";
    public static readonly Scope[] PrefixScopes = { new Scope(DecoyRoutePrefix, Target.Room, Lifetime.Round) };

    // 이 키가 판이 끝나면 지우는 접두어 키인지(RoundStateResetter).
    public static bool IsRoundPrefixedKey(string key, Target target)
    {
        foreach (Scope s in PrefixScopes)
            if (s.Target == target && s.Lifetime == Lifetime.Round && key != null && key.StartsWith(s.Key)) return true;
        return false;
    }

    private static bool TryGetScope(string key, Target target, out Scope scope)
    {
        foreach (Scope s in Scopes)
        {
            if (s.Key != key || s.Target != target) continue;
            scope = s;
            return true;
        }
        foreach (Scope s in PrefixScopes)
        {
            if (s.Target != target || key == null || !key.StartsWith(s.Key)) continue;
            scope = s;
            return true;
        }
        scope = default;
        return false;
    }

    private static string[] Collect(Target target, Lifetime lifetime, Writer? writer = null)
    {
        var keys = new System.Collections.Generic.List<string>();
        foreach (Scope scope in Scopes)
            if (scope.Target == target && scope.Lifetime == lifetime && (writer == null || scope.Writer == writer.Value)) keys.Add(scope.Key);
        return keys.ToArray();
    }
}
