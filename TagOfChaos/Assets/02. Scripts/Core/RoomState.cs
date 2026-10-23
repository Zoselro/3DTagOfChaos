using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Room/Player CustomProperties를 안전하게 읽는 조회 헬퍼. 전 도메인이 쓰므로 Core/에 둔다(research.md §7).
// 값의 타입이 예상과 다르거나(키 삭제 통지 null 포함) 없으면 예외 대신 false를 돌려준다 — 예전에는
// (int)raw 같은 직접 캐스트라 타입이 어긋나면 InvalidCastException이 났다(research.md §8.22).
public static class RoomState
{
    public static bool IsInRoom() => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;

    public static bool TryGetInt(string key, out int value)
    {
        value = default;
        if (!IsInRoom()) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object raw) || !(raw is int v)) return false;
        value = v;
        return true;
    }

    public static bool TryGetDouble(string key, out double value)
    {
        value = default;
        if (!IsInRoom()) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object raw) || !(raw is double v)) return false;
        value = v;
        return true;
    }

    public static bool TryGetIntArray(string key, out int[] value)
    {
        value = null;
        if (!IsInRoom()) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object raw) || !(raw is int[] v)) return false;
        value = v;
        return true;
    }

    // 대기실에서 목소리를 녹음했는지(§26 H11).
    public static bool HasVoice(Player player) => TryGetPlayerBool(player, NetKeys.HasVoice, out bool v) && v;

    public static bool TryGetPlayerBool(Player player, string key, out bool value)
    {
        value = default;
        if (player == null || !player.CustomProperties.TryGetValue(key, out object raw) || !(raw is bool v)) return false;
        value = v;
        return true;
    }

    public static bool TryGetPlayerInt(Player player, string key, out int value)
    {
        value = default;
        if (player == null || !player.CustomProperties.TryGetValue(key, out object raw) || !(raw is int v)) return false;
        value = v;
        return true;
    }

    // 괴물이 한 명 이상 확정됐는지. 이탈로 빈 배열만 남은 경우도 "미확정"으로 본다(research.md §8.7).
    public static bool HasMonster() => TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) && monsters.Length > 0;

    public static bool IsMonster(int actorNumber) =>
        TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) && System.Array.IndexOf(monsters, actorNumber) >= 0;

    public static bool IsLocalMonster() => PhotonNetwork.LocalPlayer != null && IsMonster(PhotonNetwork.LocalPlayer.ActorNumber);

    public static bool IsBroken(Player player) => TryGetPlayerInt(player, NetKeys.HitCount, out int hitCount) && hitCount >= 2;

    public static bool IsSpy(int actorNumber) =>
        TryGetIntArray(NetKeys.SpyActorNumbers, out int[] spies) && System.Array.IndexOf(spies, actorNumber) >= 0;

    public static bool IsLocalSpy() => PhotonNetwork.LocalPlayer != null && IsSpy(PhotonNetwork.LocalPlayer.ActorNumber);


    // 방장이 정한 스파이 수(0~1)·마녀 타이머(초). 방 설정이 없으면(오프라인 개발 방 등) 기본값.
    public static int SpyCount() =>
        TryGetInt(NetKeys.RoomSpyCount, out int spies) ? Mathf.Max(0, spies) : GameSettings.Current.DefaultSpies;

    public static int TimeAttackSeconds() =>
        TryGetInt(NetKeys.TimeAttackDuration, out int seconds) && seconds > 0 ? seconds : GameSettings.Current.DefaultTimeAttackMinutes * 60;

    public static int MonsterCount() => TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) ? monsters.Length : 0;

    // 이번 판에 필요한 괴물 수(GameSettings.MonsterCount, 인원에 맞게 제한)를 모두 뽑았는지.
    // 괴물 1명 규칙에서는 HasMonster()와 같고, 인원이 늘어 괴물 수를 늘리면 그 수만큼 채워져야 true가 된다.
    public static bool IsMonsterSelectionComplete()
    {
        if (!IsInRoom()) return false;
        int required = GameSettings.Current.MonsterCountFor(PhotonNetwork.CurrentRoom.PlayerCount);
        return MonsterCount() >= required;
    }

    // 정원이 모두 찼는지(시작 조건·괴물 선정 조건 공용, Bug-fix-plan.md §30.2). 오프라인 개발 방(MaxPlayers=0)은
    // 혼자 테스트할 수 있도록 찬 것으로 본다. 규칙이 바뀌면(예: 정원의 일부만 차도 시작) 이 함수만 고친다.
    public static bool IsRoomFull()
    {
        if (!IsInRoom()) return false;
        Room room = PhotonNetwork.CurrentRoom;
        if (PhotonNetwork.OfflineMode && room.MaxPlayers == 0) return true;
        return room.MaxPlayers > 0 && room.PlayerCount >= room.MaxPlayers;
    }

    // 호스트 = 시작 버튼을 가진 사람(Bug-fix-plan.md §33 A안): 방에 남아 있는 사람 중 가장 먼저 들어온 사람.
    // 괴물 여부와 무관하다 — 방을 만든 사람이 괴물이 돼도 시작 버튼은 그대로 두고, 나가면 다음 입장자에게 간다.
    // 게임 진행 권한(Photon 방장)은 괴물이 아니어야 하므로 아래 DesiredMasterActor로 따로 정한다.
    public static int HostActor()
    {
        if (!IsInRoom()) return -1;
        int best = -1;
        foreach (int actor in PhotonNetwork.CurrentRoom.Players.Keys) // PlayerList는 부를 때마다 정렬·배열을 새로 만든다(F-11)
            if (best < 0 || actor < best) best = actor;
        return best;
    }

    // 방 안 사람들(순서 없음). 매 프레임 도는 곳에서 PhotonNetwork.PlayerList 대신 쓴다 — 할당 없이 순회된다(F-11).
    public static Dictionary<int, Player>.ValueCollection Players =>
        IsInRoom() ? PhotonNetwork.CurrentRoom.Players.Values : EmptyPlayers.Values;
    private static readonly Dictionary<int, Player> EmptyPlayers = new Dictionary<int, Player>();

    // 네트워크 상태와 분리한 순수 계산(테스트용): 최소 ActorNumber, 없으면 -1.
    public static int HostActor(int[] actorNumbers) => DesiredMasterActor(actorNumbers, null);

    public static bool IsLocalHost() => PhotonNetwork.LocalPlayer != null && HostActor() == PhotonNetwork.LocalPlayer.ActorNumber;

    // 방장 정책(Bug-fix-plan.md §26.8): "괴물이 아닌 사람 중 입장 순서가 가장 빠른 사람". Photon ActorNumber는
    // 방 안에서 입장 순으로 1,2,3…이 부여되고 재사용되지 않으므로 최소 ActorNumber = 가장 먼저 들어온 사람이다
    // (방을 만든 사람 → 두 번째 입장자 → 세 번째 입장자 …). 후보가 없으면(괴물만 남음) -1.
    public static int DesiredMasterActor()
    {
        if (!IsInRoom()) return -1;
        TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters);
        int best = -1;
        foreach (int actor in PhotonNetwork.CurrentRoom.Players.Keys)
        {
            if (monsters != null && System.Array.IndexOf(monsters, actor) >= 0) continue;
            if (best < 0 || actor < best) best = actor;
        }
        return best;
    }

    // 네트워크 상태와 분리한 순수 계산(테스트·정책 확장용): 괴물이 아닌 액터 중 최소 번호, 없으면 -1.
    public static int DesiredMasterActor(int[] actorNumbers, int[] monsterActorNumbers)
    {
        int best = -1;
        foreach (int actor in actorNumbers)
        {
            if (monsterActorNumbers != null && System.Array.IndexOf(monsterActorNumbers, actor) >= 0) continue;
            if (best < 0 || actor < best) best = actor;
        }
        return best;
    }
}
