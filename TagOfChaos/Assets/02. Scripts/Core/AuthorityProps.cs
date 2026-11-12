using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 방장이 쓰는 권위 속성의 단일 쓰기 경로(AuditFixPlan.md §7.2). 방장이 쓴 값을 "기댓값"으로 기억해 두고, 방장이 아닌 사람이
// 그 키를 바꾸면 RoomPropertyGuard가 이 기댓값으로 되돌린다. 서버가 "누가 바꿨는지"를 알려 주지 않아도 동작한다.
// 방장이 아닐 때 부르면 아무것도 쓰지 않는다(조작이 아닌 정상 코드도 방장 전용 경로에서만 부른다).
public static class AuthorityProps
{
    private static readonly Dictionary<string, object> room = new Dictionary<string, object>();
    private static readonly Dictionary<string, object> players = new Dictionary<string, object>();
    private static float graceUntil = -1f;
    private const float GraceSeconds = 1.5f; // 방장이 된 직후: 이전 방장이 보낸 변경이 늦게 도착하므로 받아들인다

    public static int PreviousMaster { get; private set; } = -1;

    public static bool InGrace => Time.realtimeSinceStartup < graceUntil;

    // §72 E7: 방장이 같은 키를 짧은 간격으로 두 번 쓰면, 첫 값의 서버 응답(에코)이 둘째 값보다 늦게 도착해 감시(RoomPropertyGuard)가
    // 방장 자신의 옛 값을 "남이 바꾼 값"으로 보고 되돌리며 경고를 남겼다. 최근 RecentSeconds초 안에 방장이 쓴 값은 정상으로 본다.
    public const float RecentSeconds = 5f;
    private const int RecentMax = 8;
    private static readonly Dictionary<string, List<(object value, float at)>> recent = new Dictionary<string, List<(object, float)>>();

    // ---------------- 쓰기 ----------------

    public static void SetRoom(Hashtable props)
    {
        if (props == null || props.Count == 0 || !RoomState.IsInRoom()) return;
        if (!PhotonNetwork.IsMasterClient) { Debug.LogWarning("[AuthorityProps] SetRoom ignored: not the master client."); return; }
        foreach (System.Collections.DictionaryEntry e in props)
            if (e.Key is string key && NetKeys.IsMasterKey(key, NetKeys.Target.Room)) { room[key] = e.Value; Remember(key, e.Value); }
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    public static void SetPlayer(Player target, Hashtable props)
    {
        if (target == null || props == null || props.Count == 0 || !RoomState.IsInRoom()) return;
        if (!PhotonNetwork.IsMasterClient) { Debug.LogWarning("[AuthorityProps] SetPlayer ignored: not the master client."); return; }
        foreach (System.Collections.DictionaryEntry e in props)
            if (e.Key is string key && NetKeys.IsMasterKey(key, NetKeys.Target.Player))
            {
                players[PlayerKey(target.ActorNumber, key)] = e.Value;
                Remember(PlayerKey(target.ActorNumber, key), e.Value);
            }
        target.SetCustomProperties(props);
    }

    // ---------------- 기댓값 ----------------

    // 방장이 되었을 때(방 생성·방장 교체): 지금 캐시를 기댓값으로 받아들이고 잠시 유예한다.
    public static void Adopt(int previousMaster)
    {
        room.Clear();
        players.Clear();
        recent.Clear();
        PreviousMaster = previousMaster;
        graceUntil = Time.realtimeSinceStartup + GraceSeconds;
        if (!RoomState.IsInRoom()) return;
        foreach (System.Collections.DictionaryEntry e in PhotonNetwork.CurrentRoom.CustomProperties)
            if (e.Key is string key && NetKeys.IsMasterKey(key, NetKeys.Target.Room)) room[key] = e.Value;
        foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
            foreach (System.Collections.DictionaryEntry e in p.CustomProperties)
                if (e.Key is string key && NetKeys.IsMasterKey(key, NetKeys.Target.Player)) players[PlayerKey(p.ActorNumber, key)] = e.Value;
    }

    public static void Clear()
    {
        room.Clear();
        players.Clear();
        recent.Clear();
        PreviousMaster = -1;
        graceUntil = -1f;
    }

    // 유예 중에 도착한 변경은 정상으로 보고 기댓값으로 삼는다.
    public static void AcceptRoom(string key, object value) => room[key] = value;
    public static void AcceptPlayer(int actor, string key, object value) => players[PlayerKey(actor, key)] = value;

    public static object ExpectedRoom(string key) => room.TryGetValue(key, out object v) ? v : null;

    // 방장이 마지막으로 보낸 값(서버 응답 전이라 캐시가 옛 값일 때 이어서 계산하는 데 쓴다). 기록이 없으면 false.
    public static bool TryGetExpectedRoom(string key, out object value) => room.TryGetValue(key, out value);
    public static object ExpectedPlayer(int actor, string key) => players.TryGetValue(PlayerKey(actor, key), out object v) ? v : null;

    // 방장이 최근에 쓴 값인지(서버 응답이 순서가 바뀌어 도착한 옛 값 — 되돌리지 않는다).
    public static bool WasRecentlyWrittenRoom(string key, object value) => WasRecent(key, value);
    public static bool WasRecentlyWrittenPlayer(int actor, string key, object value) => WasRecent(PlayerKey(actor, key), value);

    private static bool WasRecent(string id, object value)
    {
        if (!recent.TryGetValue(id, out List<(object value, float at)> list)) return false;
        float since = Time.realtimeSinceStartup - RecentSeconds;
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i].at >= since && NetSecurityRules.ValuesEqual(list[i].value, value)) return true;
        return false;
    }

    private static void Remember(string id, object value)
    {
        if (!recent.TryGetValue(id, out List<(object value, float at)> list)) recent[id] = list = new List<(object, float)>(RecentMax);
        float now = Time.realtimeSinceStartup;
        list.RemoveAll(x => now - x.at > RecentSeconds);
        if (list.Count >= RecentMax) list.RemoveAt(0);
        list.Add((value, now));
    }

    private static string PlayerKey(int actor, string key) => actor + "|" + key;
}
