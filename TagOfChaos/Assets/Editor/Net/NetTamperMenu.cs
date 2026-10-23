using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

// 멀티 보안 시험용 "조작된 클라이언트" 메뉴(AuditFixPlan.md 13.3·16장). 에디터에서 Play Mode로 방에 들어간 한 사람이
// 정상 코드가 하지 않는 쓰기·이벤트·RPC를 직접 보낸다 — 다른 PC(빌드)의 화면이 영향을 받지 않고 [NetGuard]·[GrabKill] 로그가
// 남는지 확인한다. 에디터 폴더라 빌드에는 들어가지 않는다. 대상은 "나 아닌 첫 번째 사람"(입장 순).
public static class NetTamperMenu
{
    private const string Root = "Tools/TagOfChaos/Net Tamper Test/";

    private static Player FirstOther()
    {
        Player[] others = PhotonNetwork.PlayerListOthers;
        return others.Length > 0 ? others[0] : null;
    }

    private static HideOrSeekPlayer CookieOf(Player p)
    {
        if (p == null) return null;
        foreach (HideOrSeekPlayer c in Object.FindObjectsByType<HideOrSeekPlayer>(FindObjectsSortMode.None))
            if (c.View != null && c.View.Owner == p) return c;
        return null;
    }

    private static bool Ready() => EditorApplication.isPlaying && PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode;

    [MenuItem(Root + "1 Break other player (write HitCount)", true)]
    [MenuItem(Root + "2 Write GameResult", true)]
    [MenuItem(Root + "3 Write RegionMove", true)]
    [MenuItem(Root + "4 Take master client", true)]
    [MenuItem(Root + "5 Paint other cookie", true)]
    [MenuItem(Root + "7 Grab other cookie from afar", true)]
    [MenuItem(Root + "8 Monster - grab-kill other cookie from afar", true)]
    [MenuItem(Root + "9 Revive myself (clear my HitCount)", true)]
    private static bool Validate() => Ready();

    [MenuItem(Root + "1 Break other player (write HitCount)")]
    private static void BreakOther()
    {
        Player target = FirstOther();
        if (target == null) { Debug.Log("[NetTamper] No other player."); return; }
        target.SetCustomProperties(new Hashtable { { NetKeys.HitCount, 2 }, { NetKeys.DeathCause, (int)DeathCause.Monster } });
        Debug.Log($"[NetTamper] Wrote HitCount=2 on actor {target.ActorNumber}. Expected: the master restores it, nobody breaks.");
    }

    [MenuItem(Root + "2 Write GameResult")]
    private static void WriteResult()
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.GameResult, (int)GameResult.MonsterWins } });
        Debug.Log("[NetTamper] Wrote GameResult. Expected: the master restores it, no result screen.");
    }

    [MenuItem(Root + "3 Write RegionMove")]
    private static void WriteRegionMove()
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.RegionMove, RegionMove.Encode("eu", RegionMove.NewToken()) } });
        Debug.Log("[NetTamper] Wrote RegionMove. Expected (waiting room): nobody leaves.");
    }

    [MenuItem(Root + "4 Take master client")]
    private static void TakeMaster()
    {
        PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);
        Debug.Log("[NetTamper] Requested master client for myself. Expected: the policy master takes it back within a second.");
    }

    [MenuItem(Root + "5 Paint other cookie")]
    private static void PaintOther()
    {
        HideOrSeekPlayer cookie = CookieOf(FirstOther());
        if (cookie == null) { Debug.Log("[NetTamper] No other cookie."); return; }
        object[] content = { cookie.View.ViewID, 0 }; // { viewId, 팔레트 번호 } — 물풍선 전신 칠하기와 같은 모양
        PhotonNetwork.RaiseEvent(NetEventCodes.PaintStroke, content, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        Debug.Log($"[NetTamper] Sent a paint fill for view {cookie.View.ViewID}. Expected: ignored on every screen.");
    }

    [MenuItem(Root + "7 Grab other cookie from afar")]
    private static void GrabFromAfar()
    {
        HideOrSeekPlayer target = CookieOf(FirstOther());
        HideOrSeekPlayer mine = CookieOf(PhotonNetwork.LocalPlayer);
        if (target == null || mine == null) { Debug.Log("[NetTamper] Need my cookie and another cookie."); return; }
        target.View.RPC(HideOrSeekPlayer.RpcOnGrabbedByOwner, target.View.Owner, mine.View.ViewID);
        Debug.Log($"[NetTamper] Asked actor {target.View.Owner.ActorNumber} to be carried by me ({Vector3.Distance(target.transform.position, mine.transform.position):F1} m). Expected: rejected when far.");
    }

    [MenuItem(Root + "8 Monster - grab-kill other cookie from afar")]
    private static void GrabKillFromAfar()
    {
        HideOrSeekPlayer target = CookieOf(FirstOther());
        MonsterController monster = null;
        foreach (MonsterController m in Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None))
            if (m.photonViewOwner() == PhotonNetwork.LocalPlayer) monster = m;
        if (target == null || monster == null) { Debug.Log("[NetTamper] Need to be a monster and another cookie."); return; }
        target.View.RPC(HideOrSeekPlayer.RpcRequestGrabKill, RpcTarget.All, monster.GetComponent<PhotonView>().ViewID);
        Debug.Log("[NetTamper] Sent a grab-kill request from afar. Expected: the master logs [GrabKill] Rejected, the cookie stays alive.");
    }

    [MenuItem(Root + "9 Revive myself (clear my HitCount)")]
    private static void ReviveSelf()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.HitCount, null }, { NetKeys.DeathCause, null } });
        Debug.Log("[NetTamper] Cleared my own HitCount. Expected: the master restores it, I stay broken.");
    }

    private static Player photonViewOwner(this MonsterController m)
    {
        var view = m.GetComponent<PhotonView>();
        return view != null ? view.Owner : null;
    }
}
