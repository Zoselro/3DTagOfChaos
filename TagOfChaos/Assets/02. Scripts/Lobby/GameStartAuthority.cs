using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 판 시작 절차의 단일 실행 지점(Bug-fix-plan.md §33 A안). 시작 버튼은 호스트(RoomState.HostActor — 괴물 여부와
// 무관하게 가장 먼저 들어온 사람)에게 보이지만, 실제 시작은 게임 진행 권한을 가진 Photon 방장(괴물이 아닌 사람,
// MasterClientPolicy)이 실행한다. 호스트가 방장이 아니면 시작 요청 이벤트(NetEventCodes.StartGameRequest)를 방장에게
// 보내고, 방장이 이 클래스로 요청자·조건을 다시 확인해 실행한다.
//
// 예전에는 시작 버튼 주인과 Photon 방장이 같은 개념이라, 방을 만든 사람이 괴물이 되면(괴물은 진행 권한을 가질 수
// 없음) 시작 버튼까지 두 번째 입장자에게 넘어갔다(㉙).
public static class GameStartAuthority
{
    public enum Result { Started, NotMaster, NotHost, NotReady, AlreadyStarted, Settling }

    // 방장이 시작을 연달아 처리하지 않도록 막는 간격 — PrepareEndTime은 서버 응답 후에야 로컬에 보이므로
    // (Room Props 반영 지연) 그 사이 도착한 중복 요청을 이 시간으로 거른다.
    private const float DuplicateGuardSeconds = 3f;
    private static float lastStartTime = float.NegativeInfinity;

    // 방장 교체 직후 이 시간 동안은 시작하지 않는다(Request1009Plan.md §43 F4) — 교체와 씬 변경이 서버에서 엇갈리면
    // 새 방장(대기실에 남은 괴물 호스트)이 씬 동기화에서 빠졌다. 그 사이 온 요청은 기억해 두었다가 방장이 다시 시도한다.
    private const float SettleSeconds = 1f;
    private const float PendingRetrySeconds = 4f;
    private static int pendingRequester = -1;
    private static float pendingUntil = float.NegativeInfinity;

    // 시작 조건: 정원이 모두 찼고(사용자 결정 D1), 이번 판에 필요한 괴물이 모두 정해졌고(괴물 선정은 GameLobbyScene에만
    // 있으므로, 괴물 없이 시작하면 GameScene에서 괴물이 영영 정해지지 않는다), 모두 대기실에 들어와 있다(§43 — 막 재입장해
    // 대기실을 불러오는 중인 사람이 있으면 기다린다).
    public static bool IsReady() =>
        StartReadiness.IsReady(RoomState.IsRoomFull(), RoomState.IsMonsterSelectionComplete(), RoomState.AllPlayersInScene(SceneNames.GameLobby));

    // 호스트 화면에서 호출: 내가 진행 권한을 가진 방장이면 바로 실행하고, 아니면 방장에게 요청을 보낸다.
    public static void RequestStart()
    {
        if (!RoomState.IsInRoom() || PhotonNetwork.LocalPlayer == null) return;

        if (IsIntendedMaster())
        {
            Result result = TryStart(PhotonNetwork.LocalPlayer.ActorNumber);
            RoomSyncLog.Note($"Local start: {result}");
            return;
        }

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(NetEventCodes.StartGameRequest, null, options, SendOptions.SendReliable);
        RoomSyncLog.Note($"Start requested to master (actor {PhotonNetwork.CurrentRoom.MasterClientId})");
    }

    // 방장 쪽 실행. 요청자가 호스트인지, 조건을 만족하는지 다시 확인한다.
    public static Result TryStart(int requesterActor)
    {
        // 방장이 괴물로 정해져 진행 권한이 곧 넘어갈 순간(서버 응답 전)에는 실행하지 않는다 — 괴물 클라이언트가 씬을
        // 넘기면 괴물이 대기실에 남지 못한다(§26.8.3 ②). 호스트 화면의 버튼은 잠시 뒤 다시 눌러 새 방장에게 보낸다.
        if (!IsIntendedMaster()) return Result.NotMaster;
        if (requesterActor != RoomState.HostActor()) return Result.NotHost;
        if (RoomState.TryGetDouble(NetKeys.PrepareEndTime, out _) || Time.unscaledTime - lastStartTime < DuplicateGuardSeconds)
            return Result.AlreadyStarted;
        if (StartReadiness.IsSettling(MasterClientPolicy.SwitchPending, Time.realtimeSinceStartup - MasterClientPolicy.LastSwitchAt, SettleSeconds))
        {
            RememberPending(requesterActor);
            return Result.Settling;
        }
        if (!IsReady()) return Result.NotReady;

        GameSettingsSO settings = GameSettings.Current;
        string map = settings.PickGameMap(lastMap);
        if (string.IsNullOrEmpty(map))
        {
            Debug.LogError("[GameStart] GameSettings.gameMapScenes is empty. The game cannot start.");
            return Result.NotReady;
        }

        lastStartTime = Time.unscaledTime;
        pendingRequester = -1;

        // 준비 끝 시각(시작 신호)을 맵이 아니라 여기서 먼저 기록한다 — 아래 LoadLevel(= curScn 변경)보다 먼저 보내야 한다
        // (같은 클라이언트의 Props 변경은 보낸 순서대로 도착). 괴물도 이제 모두와 함께 맵으로 가서 술래방에서 시작한다(Request1009Plan.md §25.5 C).
        // 판마다 새 값을 쓰므로 이전 판의 값이 남아 판이 시작되지 않던 문제도 함께 막는다.
        lastMap = map;
        int[] spies = PickSpies(settings.ClampSpies(RoomState.SpyCount(), PhotonNetwork.CurrentRoom.PlayerCount));
        AuthorityProps.SetRoom(new Hashtable
        {
            { NetKeys.PrepareEndTime, PhotonNetwork.Time + settings.SceneLoadGrace + settings.PrepareSeconds },
            { NetKeys.GameMapScene, map },
            { NetKeys.SpyActorNumbers, spies },
            { NetKeys.CharacterAssignments, AssignCharacters() },
        });

        PhotonNetwork.CurrentRoom.IsOpen = false; // 씬 전환 도중 새로 입장하는 걸 방지(대기실 복귀 시 RoundStateResetter가 다시 연다)
        RoomSceneTransition.LoadLevelForRoom(map); // 방장만 호출, 나머지는 AutomaticallySyncScene으로 함께 이동
        RoomSyncLog.Note($"Start by actor {requesterActor}. Map: {map}");
        return Result.Started;
    }

    // 방장 교체가 가라앉기를 기다리는 시작 요청(같은 요청자는 처음 마감을 유지한다).
    private static void RememberPending(int requesterActor)
    {
        if (pendingRequester == requesterActor && Time.unscaledTime < pendingUntil) return;
        pendingRequester = requesterActor;
        pendingUntil = Time.unscaledTime + PendingRetrySeconds;
        RoomSyncLog.Note($"Start by actor {requesterActor} waits for the master switch to settle");
    }

    // 대기실(GameLobbyController.Update)에서 매 프레임: 기다리던 요청을 방장이 다시 시도한다. 방장이 바뀌었거나 마감이 지나면 버린다.
    public static void TickPending()
    {
        if (pendingRequester < 0) return;
        if (!PhotonNetwork.IsMasterClient || Time.unscaledTime >= pendingUntil)
        {
            pendingRequester = -1;
            return;
        }
        int requester = pendingRequester;
        Result result = TryStart(requester);
        if (result == Result.Settling) return;
        pendingRequester = -1;
        RoomSyncLog.Note($"Pending start by actor {requester}: {result}");
    }

    // 직전 판 맵(이 클라이언트가 방장으로 시작한 판). 판이 끝나면 Room의 맵 키는 지워지므로 여기서 기억한다.
    private static string lastMap;

    // 괴물이 아닌 사람 중에서 스파이를 무작위로 뽑는다(EscapePlan.md §1.1). 수는 방 설정(RoomSpyCount, §25.5 A), 0명이면 빈 배열.

    // 쿠키·스파이 캐릭터 배정(§25.5 B): 고른 캐릭터를 입장 순서대로 인정하고, 겹치거나 고르지 않은 사람은 남은 캐릭터.
    private static int[] AssignCharacters()
    {
        var actors = new System.Collections.Generic.List<int>();
        var preferences = new System.Collections.Generic.Dictionary<int, int>();
        foreach (Player p in RoomState.Players)
        {
            if (RoomState.IsMonster(p.ActorNumber)) continue;
            actors.Add(p.ActorNumber);
            preferences[p.ActorNumber] = RoomState.TryGetPlayerInt(p, NetKeys.SkinIndex, out int s) ? s : CharacterAssignment.Unselected;
        }
        return CharacterAssignment.Assign(actors, preferences, CharacterCount);
    }

    // 캐릭터 수(대기실 선택 목록과 같은 SkinCatalog — 전역 설정이 가리킨다). 없으면 4.
    private static int CharacterCount
    {
        get
        {
            SkinCatalogSO catalog = GameSettings.Current.CharacterCatalog;
            return catalog != null && catalog.Count > 0 ? catalog.Count : 4;
        }
    }
    private static int[] PickSpies(int count)
    {
        var candidates = new System.Collections.Generic.List<int>();
        foreach (Player p in PhotonNetwork.PlayerList)
            if (!RoomState.IsMonster(p.ActorNumber)) candidates.Add(p.ActorNumber);
        var spies = new System.Collections.Generic.List<int>();
        for (int i = 0; i < count && candidates.Count > 1; i++) // 쿠키가 최소 1명은 남는다
        {
            int pick = Random.Range(0, candidates.Count);
            spies.Add(candidates[pick]);
            candidates.RemoveAt(pick);
        }
        return spies.ToArray();
    }

    // 지금 Photon 방장이고, 방장 정책상 진행 권한을 가져야 하는 사람(괴물이 아닌 최선 입장자)도 나인지.
    // 괴물 목록은 캐시가 아니라 방장이 마지막으로 보낸 목록으로 본다(§43 — 방금 확정한 괴물이 캐시에 아직 없을 때 시작하지 않도록).
    private static bool IsIntendedMaster()
    {
        if (!PhotonNetwork.IsMasterClient || !RoomState.IsInRoom()) return false;
        var actors = PhotonNetwork.CurrentRoom.Players.Keys;
        int[] present = new int[actors.Count];
        actors.CopyTo(present, 0);
        return RoomState.DesiredMasterActor(present, MonsterRoster.Current()) == PhotonNetwork.LocalPlayer.ActorNumber;
    }
}
