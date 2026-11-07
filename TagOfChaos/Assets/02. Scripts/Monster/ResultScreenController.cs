using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameScene에 1개, 전원 대상 — 결과 화면 표시/자동 복귀(GameRule.md §8.2).
//
// root가 이 컴포넌트 자신의 GameObject이므로 SetActive(false)로 숨기면 OnDisable에서 Photon 콜백
// 등록이 풀려 GameResult를 영영 받지 못했다(Bug-fix-plan.md §23.7) — CanvasGroup으로만 숨긴다.
public class ResultScreenController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text remainingCountText;
    // 쿠키 생존 아이콘: 쿠키 수만큼 템플릿을 복제해 컨테이너에 채운다(생존=흰색/파괴=회색). 예전에는 4칸 고정
    // 배열이라 인원이 늘면 아이콘이 모자랐다(Bug-fix-plan.md §27 확장성). 템플릿은 비활성 상태로 둔다.
    [SerializeField] private Transform cookieIconContainer;
    [SerializeField] private Image cookieIconTemplate;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerResultRow playerRowPrefab;
    [SerializeField] private TMP_Text lobbyButtonCountdownText;
    [SerializeField] private float autoReturnDelay = 12f;
    [SerializeField] private string lobbyCountdownFormat = "Back to lobby ({0})"; // 인스펙터에서 입력(코드에 한글 금지)

    private CanvasGroup rootGroup;
    private bool isShown;

    private void Awake()
    {
        rootGroup = CanvasGroupVisibility.Ensure(root);
        CanvasGroupVisibility.Set(rootGroup, false);
    }

    // 결과는 방장만 쓴다. 조작된 클라이언트가 쓴 결과로 판이 끝나지 않도록, 쓴 사람이 방장인지 확인한 뒤(한 프레임 뒤 — 쓴 사람 기록은
    // 이 콜백 직후에 도착한다, RoomPropertyGuard) 보여 준다(AuditFixPlan.md §7.2).
    private bool resultPending;

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey(NetKeys.GameResult)) resultPending = true;
    }

    private void Update()
    {
        if (!resultPending) return;
        resultPending = false;
        if (!RoomState.TryGetInt(NetKeys.GameResult, out int result)) return;
        if (!RoomPropertyGuard.WrittenByMaster(NetKeys.GameResult))
        {
            Debug.LogWarning("[ResultScreen] Ignored a game result that was not written by the master client.");
            return;
        }
        ShowResult((GameResult)result);
    }

    // 마지막 쿠키가 잡히면 파괴 판정(HitCount)이 처형 즉시 기록돼 승패가 바로 난다. 괴물이 쿠키를 들어 올려 부수는 연출이
    // 끝난 뒤에 결과를 보여 준다(Bug-fix-plan.md §36 D2). 연출이 멈춰도 결과가 막히지 않도록 최대 대기 시간을 둔다.
    private const float MaxGrabKillWait = 5f;

    private void ShowResult(GameResult result)
    {
        if (isShown) return; // 결과 행/코루틴이 중복 생성되지 않도록 한 번만 표시
        isShown = true;

        if (result == GameResult.MonsterWins || result == GameResult.EscapeEnded) StartCoroutine(ShowAfterGrabKills(result));
        else Present(result);
    }

    private IEnumerator ShowAfterGrabKills(GameResult result)
    {
        float giveUpAt = Time.time + MaxGrabKillWait;
        while (IsAnyCookieBeingGrabKilled() && Time.time < giveUpAt) yield return null;
        Present(result);
    }

    private static bool IsAnyCookieBeingGrabKilled()
    {
        foreach (IGameCharacter character in CharacterRegistry.All)
        {
            if (character.Role != CharacterRole.Cookie || !CharacterRegistry.IsAlive(character)) continue;
            var presenter = character.gameObject.GetComponent<CookieLifeStatePresenter>();
            if (presenter != null && presenter.IsBeingGrabKilled) return true;
        }
        return false;
    }

    private void Present(GameResult result)
    {
        if (result == GameResult.EscapeEnded)
        {
            PresentEscape();
            return;
        }

        CanvasGroupVisibility.Set(rootGroup, true);

        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters);
        int aliveCount = 0;
        int cookieCount = 0;

        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(pl => pl.ActorNumber))
        {
            bool isMonster = monsters != null && monsters.Contains(p.ActorNumber);
            bool isBroken = RoomState.IsBroken(p);

            if (playerRowPrefab != null && playerListContent != null)
            {
                var row = Instantiate(playerRowPrefab, playerListContent);
                if (isMonster) row.SetMonster(p.NickName);
                else row.SetCookie(p.NickName, alive: !isBroken);
            }

            if (isMonster) continue;

            AddCookieIcon(isBroken);
            cookieCount++;
            if (!isBroken) aliveCount++;
        }

        // 분모는 실제 쿠키 수(최대 3명) — 예전에는 4로 하드코딩돼 있었다(research.md §8.23).
        if (remainingCountText != null) remainingCountText.text = $"{aliveCount} / {cookieCount}";
        StartCoroutine(AutoReturnCountdown());
    }

    // 탈출 모드 결과(EscapePlan.md §1.3): 사람마다 "닉네임(역할) 결과" 한 줄. 쿠키가 한 명도 탈출하지 못하고 괴물이 잡았으면
    // 괴물 승리 화면(쿠키 유리병 트로피)을 함께 보여준다. 스파이는 게임이 끝난 뒤 공개된 번호(RevealedSpies)로 표시한다.
    private void PresentEscape()
    {
        CanvasGroupVisibility.Set(rootGroup, true);
        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters);
        RoomState.TryGetIntArray(NetKeys.RevealedSpies, out int[] spies);
        EscapeState state = EscapeManager.Instance != null ? EscapeManager.Instance.State : null;

        int cookieCount = 0, escapedCookies = 0, totalCatches = 0;
        var jarEntries = new System.Collections.Generic.List<MonsterJarTrophy.Entry>();
        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(pl => pl.ActorNumber))
        {
            int actor = p.ActorNumber;
            bool isMonster = monsters != null && monsters.Contains(actor);
            bool isSpy = spies != null && spies.Contains(actor);
            bool escaped = state != null && state.HasEscaped(actor); // 방장이 쓴 탈출 상태만(S-05)
            PlayerResultRow row = playerRowPrefab != null && playerListContent != null ? Instantiate(playerRowPrefab, playerListContent) : null;

            if (isMonster)
            {
                int catches = RoomState.TryGetPlayerInt(p, NetKeys.CatchCount, out int c) ? c : 0;
                RoomState.TryGetPlayerInt(p, NetKeys.DeathCause, out int cause);
                if (row != null) row.SetMonster(p.NickName, catches, cause == (int)DeathCause.Witch, cause == (int)DeathCause.Giant);
                totalCatches += catches;
                jarEntries.Add(new MonsterJarTrophy.Entry { Name = p.NickName, Catches = catches });
                continue;
            }

            bool stomped = RoomState.TryGetPlayerInt(p, NetKeys.DeathCause, out int escaperCause) && escaperCause == (int)DeathCause.Giant;
            if (row != null) row.SetEscaper(p.NickName, isSpy, escaped, stomped);
            if (isSpy) continue;
            cookieCount++;
            if (escaped) escapedCookies++;
            AddCookieIcon(!escaped);
        }

        bool monsterWins = escapedCookies == 0 && totalCatches > 0;
        if (monsterWins) MonsterJarTrophy.Build(root.transform, jarEntries); // 괴물이 쿠키 유리병을 들고 선다(추천안)
        if (remainingCountText != null) remainingCountText.text = $"{escapedCookies} / {cookieCount}";
        StartCoroutine(AutoReturnCountdown());
    }

    private void AddCookieIcon(bool isBroken)
    {
        if (cookieIconContainer == null || cookieIconTemplate == null) return;
        Image icon = Instantiate(cookieIconTemplate, cookieIconContainer);
        icon.gameObject.SetActive(true);
        icon.color = isBroken ? Color.gray : Color.white;
    }

    private IEnumerator AutoReturnCountdown()
    {
        float remaining = autoReturnDelay;
        while (remaining > 0f)
        {
            if (lobbyButtonCountdownText != null)
                lobbyButtonCountdownText.text = string.Format(lobbyCountdownFormat, Mathf.CeilToInt(remaining));
            remaining -= Time.deltaTime;
            yield return null;
        }
        OnLobbyButtonClicked();
    }

    public void OnLobbyButtonClicked()
    {
        StopAllCoroutines();
        if (PhotonNetwork.IsMasterClient)
            RoomSceneTransition.LoadLevelForRoom(SceneNames.GameLobby); // 방 재개방은 대기실의 RoundStateResetter가 한다(§26.3)
    }
}
