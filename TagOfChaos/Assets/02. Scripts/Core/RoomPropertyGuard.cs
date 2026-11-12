using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 권위 속성 감시·복구(AuditFixPlan.md §7.2, S-02·S-03). Photon Cloud는 어떤 클라이언트든 방 속성과 남의 플레이어 속성을 쓸 수 있다.
// - 정직한 방장: 방장 키(NetKeys.Writer.Master)가 자기가 쓴 기댓값(AuthorityProps)과 다르게 바뀌면 자기 캐시를 즉시 되돌리고 다시 쓴다.
//   본인 키(Writer.Self)를 남이 바꾸면(쓴 사람을 알 수 있을 때) 이전 값으로 되돌린다.
// - 모든 클라이언트: 속성 변경 이벤트(EventCode.PropertiesChanged)의 보낸 사람을 키마다 기억해, 받자마자 되돌릴 수 없는 동작
//   (지역 이동·결과 화면·마녀 사망 연출)이 방장이 쓴 값인지 확인할 수 있게 한다(WrittenByMaster).
// 씬과 무관하게 방 안이면 늘 동작해야 하므로 씬 로드 뒤 스스로 만들어 남는다(CharacterInteractor와 같은 방식). 오프라인 방에서는 쉰다.
public class RoomPropertyGuard : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private static RoomPropertyGuard instance;
    private readonly Dictionary<string, int> lastRoomWriter = new Dictionary<string, int>();
    private readonly Dictionary<string, object> lastSelfValue = new Dictionary<string, object>();
    private readonly Hashtable restoreBuffer = new Hashtable();
    private int currentMaster = -1;
    private int previousMaster = -1;
    private float switchedAt = -100f;
    private const float PreviousMasterGrace = 1.5f;

    public static int RestoredCount { get; private set; } // 시험·로그용

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(RoomPropertyGuard));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RoomPropertyGuard>();
    }

    // 받자마자 실행하는 소비자가 쓴다: 이 방 키를 마지막으로 쓴 사람이 방장(또는 방금 바뀐 이전 방장)인지.
    // 보낸 사람을 모르면(서버가 담지 않음, 오프라인) 막지 않는다.
    public static bool WrittenByMaster(string key)
    {
        if (instance == null || PhotonNetwork.OfflineMode || !RoomState.IsInRoom()) return true;
        if (!instance.lastRoomWriter.TryGetValue(key, out int writer)) return true;
        int prev = Time.realtimeSinceStartup - instance.switchedAt < PreviousMasterGrace ? instance.previousMaster : -1;
        return NetSecurityRules.IsAllowedRoomWriter(writer, PhotonNetwork.CurrentRoom.MasterClientId, prev);
    }

    private static bool Guarding => RoomState.IsInRoom() && !PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient;

    // ---------------- 방장 교체·입장 ----------------

    public override void OnJoinedRoom()
    {
        lastRoomWriter.Clear();
        lastSelfValue.Clear();
        currentMaster = PhotonNetwork.CurrentRoom.MasterClientId;
        if (PhotonNetwork.IsMasterClient) AuthorityProps.Adopt(-1);
    }

    public override void OnLeftRoom()
    {
        lastRoomWriter.Clear();
        lastSelfValue.Clear();
        AuthorityProps.Clear();
        currentMaster = previousMaster = -1;
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        previousMaster = currentMaster;
        currentMaster = newMasterClient != null ? newMasterClient.ActorNumber : -1;
        switchedAt = Time.realtimeSinceStartup;
        if (PhotonNetwork.IsMasterClient) AuthorityProps.Adopt(previousMaster);
    }

    // ---------------- 방 속성 ----------------

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!Guarding) return;
        Hashtable cache = PhotonNetwork.CurrentRoom.CustomProperties;
        restoreBuffer.Clear();
        foreach (System.Collections.DictionaryEntry e in changed)
        {
            if (!(e.Key is string key) || !NetKeys.IsMasterKey(key, NetKeys.Target.Room)) continue;
            cache.TryGetValue(key, out object now);
            if (AuthorityProps.InGrace) { AuthorityProps.AcceptRoom(key, now); continue; }
            object expected = AuthorityProps.ExpectedRoom(key);
            if (NetSecurityRules.ValuesEqual(now, expected)) continue;
            if (AuthorityProps.WasRecentlyWrittenRoom(key, now)) continue; // 내 옛 값의 늦은 응답(§72 E7) — 새 값의 응답이 곧 온다
            restoreBuffer[key] = expected;
            if (expected == null) cache.Remove(key); else cache[key] = expected; // 방장 화면은 즉시 정상 값으로
        }
        if (restoreBuffer.Count == 0) return;
        RestoredCount += restoreBuffer.Count;
        Debug.LogWarning($"[NetGuard] Room keys changed by someone other than the master — restoring: {Keys(restoreBuffer)}");
        AuthorityProps.SetRoom(new Hashtable { }.Merged(restoreBuffer));
    }

    // ---------------- 플레이어 속성 ----------------

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changed)
    {
        if (!Guarding || target == null) return;
        restoreBuffer.Clear();
        foreach (System.Collections.DictionaryEntry e in changed)
        {
            if (!(e.Key is string key) || !NetKeys.IsMasterKey(key, NetKeys.Target.Player)) continue;
            target.CustomProperties.TryGetValue(key, out object now);
            if (AuthorityProps.InGrace) { AuthorityProps.AcceptPlayer(target.ActorNumber, key, now); continue; }
            object expected = AuthorityProps.ExpectedPlayer(target.ActorNumber, key);
            if (NetSecurityRules.ValuesEqual(now, expected)) continue;
            if (AuthorityProps.WasRecentlyWrittenPlayer(target.ActorNumber, key, now)) continue; // §72 E7
            restoreBuffer[key] = expected;
            if (expected == null) target.CustomProperties.Remove(key); else target.CustomProperties[key] = expected;
        }
        if (restoreBuffer.Count == 0) return;
        RestoredCount += restoreBuffer.Count;
        Debug.LogWarning($"[NetGuard] Actor {target.ActorNumber}'s master-only keys were changed — restoring: {Keys(restoreBuffer)}");
        AuthorityProps.SetPlayer(target, new Hashtable { }.Merged(restoreBuffer));
    }

    // ---------------- 보낸 사람 기록(속성 변경 이벤트) ----------------

    public void OnEvent(EventData e)
    {
        if (e.Code != EventCode.PropertiesChanged || !RoomState.IsInRoom()) return;
        int writer = e.Sender;
        int target = e.Parameters.TryGetValue(ParameterCode.TargetActorNr, out object t) && t is int ti ? ti : 0;
        if (!(e.Parameters.TryGetValue(ParameterCode.Properties, out object raw) && raw is Hashtable props)) return;
        int master = PhotonNetwork.CurrentRoom.MasterClientId;
        int prev = Time.realtimeSinceStartup - switchedAt < PreviousMasterGrace ? previousMaster : -1;

        if (target == 0)
        {
            foreach (System.Collections.DictionaryEntry p in props)
                if (p.Key is string key) lastRoomWriter[key] = writer;
            if (writer > 0 && !NetSecurityRules.IsAllowedRoomWriter(writer, master, prev))
                Debug.LogWarning($"[NetGuard] Actor {writer} wrote room keys: {Keys(props)}");
            return;
        }

        // 본인 키: 쓴 사람이 본인·방장이면 값을 기억하고, 남이 썼으면 방장이 기억한 값으로 되돌린다.
        Player targetPlayer = PhotonNetwork.CurrentRoom.GetPlayer(target);
        if (targetPlayer == null) return;
        restoreBuffer.Clear();
        foreach (System.Collections.DictionaryEntry p in props)
        {
            if (!(p.Key is string key) || !NetKeys.IsSelfKey(key)) continue;
            string id = target + "|" + key;
            if (NetSecurityRules.IsAllowedPlayerWriter(false, writer, target, master, prev)) { lastSelfValue[id] = p.Value; continue; }
            lastSelfValue.TryGetValue(id, out object before);
            restoreBuffer[key] = before;
            if (before == null) targetPlayer.CustomProperties.Remove(key); else targetPlayer.CustomProperties[key] = before;
        }
        if (restoreBuffer.Count == 0) return;
        Debug.LogWarning($"[NetGuard] Actor {writer} changed actor {target}'s own keys: {Keys(restoreBuffer)}");
        if (Guarding) targetPlayer.SetCustomProperties(new Hashtable { }.Merged(restoreBuffer));
    }

    private static string Keys(Hashtable table)
    {
        var sb = new System.Text.StringBuilder();
        foreach (System.Collections.DictionaryEntry e in table) { if (sb.Length > 0) sb.Append(", "); sb.Append(e.Key); }
        return sb.ToString();
    }
}

internal static class HashtableCopyExtensions
{
    // 버퍼를 다음 콜백 전에 비우므로 보낼 때는 복사본을 쓴다.
    public static Hashtable Merged(this Hashtable into, Hashtable from)
    {
        foreach (System.Collections.DictionaryEntry e in from) into[e.Key] = e.Value;
        return into;
    }
}
