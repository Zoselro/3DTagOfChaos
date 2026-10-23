using UnityEngine;

// 괴물 시점(GameFixPlan.md F6). 테스트를 위해 두 방식 모두 남긴다.
public enum MonsterViewMode
{
    FirstPerson,
    ThirdPerson,
}

// 게임 규칙 수치를 한 곳에 모은 전역 설정(CLAUDE.md 폴더 규칙: 전역 SO → Assets/Resources/GameSettings).
// 인원·괴물 수·시간·색 슬롯 수가 여러 클래스에 상수로 흩어져 있어(4명, 60초, 600초, 30초, 슬롯 4개 …)
// 인원을 늘리려면 코드를 여러 곳 고쳐야 했다. 이제 이 에셋의 값만 바꾸면 된다(Bug-fix-plan.md §27).
[CreateAssetMenu(menuName = "TagOfChaos/GameSettings", fileName = "GameSettings")]
public class GameSettingsSO : ScriptableObject
{
    [Header("Room")]
    [Tooltip("방 최대 인원(방 만들기에서 고를 수 있는 최댓값, Request1009Plan.md §25 — 4~5명). 시작 조건은 '정원이 모두 찼을 때'다.")]
    [SerializeField, Min(2)] private int maxPlayers = 5;
    [Tooltip("방 만들기에서 고를 수 있는 최소 인원.")]
    [SerializeField, Min(2)] private int minPlayers = 4;
    [Tooltip("방 만들기의 기본 인원.")]
    [SerializeField, Min(2)] private int defaultPlayers = 4;

    // 인원이 늘면 괴물·쿠키가 늘던 인원표(roleTable)·스파이 자동 인원(spyMinPlayers)은 없앴다(Request1009Plan.md §25.5 A) — 괴물은 늘 1명,
    // 스파이는 방 설정(NetKeys.RoomSpyCount, 0~maxSpies)으로 정한다.
    [Tooltip("한 판의 괴물 수. 항상 (현재 인원 - 1) 이하로 제한된다(쿠키가 최소 1명은 있어야 함).")]
    [SerializeField, Min(1)] private int monsterCount = 1;

    [Tooltip("방 설정에서 고를 수 있는 스파이 수 최댓값.")]
    [SerializeField, Min(0)] private int maxSpies = 1;
    [Tooltip("방 만들기의 기본 스파이 수.")]
    [SerializeField, Min(0)] private int defaultSpies = 0;
    [Tooltip("스파이를 넣어도 쿠키가 이만큼은 남아야 한다(4명 방 + 스파이 1 = 쿠키 2명).")]
    [SerializeField, Min(1)] private int minCookies = 2;

    [Header("Escape — Spy")]
    [Tooltip("스파이가 탈출 장치에서 재료를 뺀 뒤 이 시간(초)이 지나면 괴물을 뺀 모두에게 '스파이가 OO를 가져갔다' 알림을 띄운다. " +
             "그사이 재료가 다시 장치에 끼워지면 띄우지 않는다(Request1003bPlan.md §2).")]
    [SerializeField, Min(0f)] private float stealNoticeDelaySeconds = 5f;
    public float StealNoticeDelaySeconds => stealNoticeDelaySeconds;

    [Header("Prepare (Request1009Plan.md §25.5 C — 색칠 대신 준비 시간)")]
    [Tooltip("맵에 들어온 뒤 괴물이 술래방에 갇혀 있는 시간(초). 술래문이 이 시간 뒤 열린다.")]
    [SerializeField, Min(0f)] private float prepareSeconds = 15f;
    [Tooltip("시작 버튼 시점부터 준비 끝 시각을 계산할 때 더하는 여유(모두의 맵 로딩 시간 보정).")]
    [SerializeField, Min(0f)] private float sceneLoadGrace = 3f;
    [Tooltip("판 안전 상한(분, §25.3 Q4): 준비가 끝난 뒤 이만큼 지나도 아무도 탈출하지 못했으면 마녀 타이머를 강제로 시작한다.")]
    [SerializeField, Min(1f)] private float safetyCapMinutes = 30f;

    [Header("Characters (Request1009Plan.md §25.5 B)")]
    [Tooltip("캐릭터 4종 목록 — 대기실 선택 버튼·캐릭터 겉모습과 같은 에셋. 판 시작 때 캐릭터 배정이 수를 센다.")]
    [SerializeField] private SkinCatalogSO characterCatalog;
    public SkinCatalogSO CharacterCatalog => characterCatalog;

    [Header("Maps")]
    [Tooltip("판마다 무작위로 고르는 게임 맵 씬 이름(빌드 목록에 있어야 함). 비어 있으면 게임을 시작할 수 없다(시작 시 오류 로그). 직전 판 맵은 다시 고르지 않는다(맵이 2개 이상일 때).")]
    [SerializeField] private string[] gameMapScenes = new string[0];

    [Header("Witch Timer (방 설정, 분 단위 — 예전 타임어택. 게임 제한시간은 없앴다, §25.5 D)")]
    [SerializeField, Min(1)] private int minTimeAttackMinutes = 1;
    [SerializeField, Min(1)] private int maxTimeAttackMinutes = 10;
    [SerializeField, Min(1)] private int defaultTimeAttackMinutes = 1;

    [Header("Monster Departure")]
    [Tooltip("괴물이 전원 나간 뒤 대기실로 돌아가기까지의 경고 시간(초).")]
    [SerializeField, Min(0f)] private float monsterDepartureReturnDelay = 5f;

    [Header("Monster Tentacle Dash (GameRule.md §4.3)")]
    [SerializeField, Min(0f)] private float tentacleDashDistance = 20f;
    [SerializeField, Min(0.01f)] private float tentacleDashDuration = 0.25f;
    [SerializeField, Min(0f)] private float tentacleDashCooldown = 15f;
    [Tooltip("돌진이 따라 올라가거나 내려가는 최대 경사(도). 더 가파른 면은 벽처럼 막혀 돌진 이동이 끝난다(Bug-fix-plan.md §41 ㊳).")]
    [SerializeField, Range(0f, 89f)] private float tentacleDashMaxSlope = 45f;
    [Tooltip("돌진 중 지면 붙이기 여유(m). 한 스텝 동안 최대 경사(TentacleDashMaxSlope)로 떨어질 수 있는 높이에 이 값을 더한 거리 안에 지면이 있으면 붙이고(언덕 꼭대기·내리막), 더 먼 낭떠러지는 그대로 떨어진다.")]
    [SerializeField, Min(0f)] private float tentacleDashGroundSnap = 0.6f;

    [Header("Monster Grab (GameFixPlan.md F1)")]
    [Tooltip("괴물 몸 앞면에서 조준한 쿠키를 잡을 수 있는 거리(m). 몸 앞면은 중심에서 약 1.87m(MonsterPlayer 프리팹 측정).")]
    [SerializeField, Min(0.5f)] private float grabReachFromFront = 2f;
    [Tooltip("조준 판정 구의 반지름(m). 조준이 조금 빗나가도 잡히게 한다.")]
    [SerializeField, Min(0.05f)] private float grabAimRadius = 0.4f;
    [Tooltip("괴물 발에서 잡을 수 있는 쿠키 발까지의 최대 높이(m). 구조물 위에 올라간 쿠키도 이 높이까지는 잡는다(Request1009Plan.md §11).")]
    [SerializeField, Min(0f)] private float maxGrabHeight = 6f;

    [Header("Monster View (GameFixPlan.md F6)")]
    [Tooltip("괴물이 판을 시작할 때의 시점.")]
    [SerializeField] private MonsterViewMode monsterDefaultView = MonsterViewMode.FirstPerson;

    [Header("Network Load (research.md §12.4)")]
    [Tooltip("캐릭터 위치 동기화 횟수(초당, PhotonNetwork.SerializationRate). PUN 기본 10.")]
    [SerializeField, Range(1, 30)] private int characterSyncRate = 10;

    [Header("Spawn")]
    [Tooltip("쿠키 스폰·리스폰 시 스폰 지점 기준 흩뿌림 범위(m).")]
    [SerializeField, Min(0f)] private float cookieSpawnRange = 5f;
    [Tooltip("괴물 스폰·리스폰 시 스폰 지점 기준 흩뿌림 범위(m). 괴물이 여럿일 때 서로 겹치지 않게 한다.")]
    [SerializeField, Min(0f)] private float monsterSpawnRange = 4f;
    [Tooltip("캐릭터가 이 높이 아래로 떨어지면 스폰 지점으로 되돌린다(FallGuard, VoidKillZone을 놓친 맵의 최후 방어선).")]
    [SerializeField] private float fallRespawnHeight = -100f;

    public int MaxPlayers => maxPlayers;
    public int MinPlayers => minPlayers;
    public int DefaultPlayers => defaultPlayers;
    public int MaxSpies => maxSpies;
    public int DefaultSpies => defaultSpies;
    public int MinCookies => minCookies;
    public int MinTimeAttackMinutes => minTimeAttackMinutes;
    public int MaxTimeAttackMinutes => maxTimeAttackMinutes;
    public int DefaultTimeAttackMinutes => defaultTimeAttackMinutes;
    public int MonsterCount => monsterCount;
    public float PrepareSeconds => prepareSeconds;
    public float SceneLoadGrace => sceneLoadGrace;
    public float SafetyCapSeconds => safetyCapMinutes * 60f;
    public float MonsterDepartureReturnDelay => monsterDepartureReturnDelay;
    public float TentacleDashDistance => tentacleDashDistance;
    public float TentacleDashDuration => tentacleDashDuration;
    public float TentacleDashCooldown => tentacleDashCooldown;
    public float TentacleDashMaxSlope => tentacleDashMaxSlope;
    public float TentacleDashGroundSnap => tentacleDashGroundSnap;
    public int CharacterSyncRate => characterSyncRate;
    public float GrabReachFromFront => grabReachFromFront;
    public float GrabAimRadius => grabAimRadius;
    public float MaxGrabHeight => maxGrabHeight;
    public MonsterViewMode MonsterDefaultView => monsterDefaultView;
    public float CookieSpawnRange => cookieSpawnRange;
    public float MonsterSpawnRange => monsterSpawnRange;
    public float FallRespawnHeight => fallRespawnHeight;

    public System.Collections.Generic.IReadOnlyList<string> GameMapScenes => gameMapScenes;

    // 이번 판 맵(GameScenePlan.md D1): 목록에서 무작위, 직전 판 맵은 제외. 목록이 비면 null — 빌드에 없는 기본 씬으로
    // 대신 가지 않도록 호출부가 시작을 막는다(Bug-fix-plan.md §41 ㊷).
    public string PickGameMap(string previousMap)
    {
        var candidates = new System.Collections.Generic.List<string>();
        foreach (string scene in gameMapScenes)
            if (!string.IsNullOrEmpty(scene) && scene != previousMap) candidates.Add(scene);
        if (candidates.Count == 0)
            foreach (string scene in gameMapScenes)
                if (!string.IsNullOrEmpty(scene)) candidates.Add(scene);
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    // 맵 키를 받지 못했을 때 쓰는 대체 맵(목록의 첫 유효 항목, 없으면 null).
    public string FirstGameMap
    {
        get
        {
            foreach (string scene in gameMapScenes)
                if (!string.IsNullOrEmpty(scene)) return scene;
            return null;
        }
    }

    // 현재 방 인원에서 뽑을 괴물 수: 늘 monsterCount(1명), 쿠키가 최소 1명 남도록 제한한다.
    public int MonsterCountFor(int playerCount) => Mathf.Clamp(monsterCount, 1, Mathf.Max(1, playerCount - 1));

    // 방 설정의 스파이 수를 허용 범위로 맞춘다: 0~maxSpies, 그리고 인원 − 괴물 − 스파이 ≥ minCookies(순수 계산, 시험용).
    public int ClampSpies(int requested, int playerCount)
    {
        int room = playerCount - MonsterCountFor(playerCount) - minCookies;
        return Mathf.Clamp(requested, 0, Mathf.Max(0, Mathf.Min(maxSpies, room)));
    }

    private void OnValidate()
    {
        monsterCount = Mathf.Clamp(monsterCount, 1, Mathf.Max(1, maxPlayers - 1));
        minPlayers = Mathf.Clamp(minPlayers, 2, maxPlayers);
        defaultPlayers = Mathf.Clamp(defaultPlayers, minPlayers, maxPlayers);
        defaultSpies = Mathf.Clamp(defaultSpies, 0, maxSpies);
        maxTimeAttackMinutes = Mathf.Max(minTimeAttackMinutes, maxTimeAttackMinutes);
        defaultTimeAttackMinutes = Mathf.Clamp(defaultTimeAttackMinutes, minTimeAttackMinutes, maxTimeAttackMinutes);
#if UNITY_EDITOR
        // 저장된 설정 에셋만 검사한다(테스트가 만드는 빈 임시 인스턴스는 제외).
        if (FirstGameMap == null && UnityEditor.EditorUtility.IsPersistent(this))
            Debug.LogWarning($"[GameSettings] gameMapScenes is empty on {name}. Games cannot start.", this);
#endif
    }
}
