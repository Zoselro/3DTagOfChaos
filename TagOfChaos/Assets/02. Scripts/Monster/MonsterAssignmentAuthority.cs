using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 마스터 전용 — 가마솥 선착순 신청 확정(GameRule.md §2.1). 괴물은 가마솥에 들어간 사람만 된다(인원표상 괴물이 2명이면
// 두 사람이 각각 들어가야 한다).
// (2026-10-03 사용자 요청: 정원이 찬 뒤 30초가 지나면 무작위로 괴물을 뽑던 기능은 삭제.)
//
// 뽑는 괴물 수는 인원표(GameSettings.MonsterCountFor)를 따른다. MonsterActorNumbers 배열을 채우는 방식이라
// 인원이 늘어 괴물이 여럿이어도 같은 코드로 동작한다(Bug-fix-plan.md §27 확장성).
public class MonsterAssignmentAuthority : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // 온라인에서는 SetCustomProperties가 서버 응답 전까지 로컬 캐시에 반영되지 않는다(PUN 기본
    // BroadcastPropsChangeToAll=true). 그래서 목록은 캐시가 아니라 방장이 마지막으로 보낸 목록(MonsterRoster.Current)에서
    // 이어 계산한다 — 예전에는 응답 전에 온 두 번째 지원을 버려(confirmRequested) 동시에 들어간 사람이 괴물이 되지 못했다(F-03).
    private bool resetRequested;

    // §71 L3: 자리보다 지원자가 많을 때 "먼저 들어간 사람"이 되도록, 첫 지원이 오면 ClaimWindow초 동안 모아 들어간 시각 순으로 자리를 준다.
    public const float ClaimWindow = 0.4f;
    private readonly List<(int actor, double enteredAt)> claims = new List<(int, double)>();
    private float decideAt = -1f;

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.ClaimMonster) return;
        if (!PhotonNetwork.IsMasterClient) return;
        // 지원자는 이벤트를 보낸 사람이다 — 내용에 담긴 번호를 믿으면 조작된 클라이언트가 남을 괴물로 지원시킬 수 있었다(F-06).
        int claimantActorNumber = photonEvent.Sender > 0 ? photonEvent.Sender
            : PhotonNetwork.OfflineMode && PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        if (claimantActorNumber <= 0) return;
        // 정원 미달이면 거절한다 — 가마솥(클라이언트)도 거르지만, 정원이 찬 순간 누가 나가는 경합까지 마스터가 최종 거부한다(Bug-fix-plan.md §30.2 C).
        if (!RoomState.IsRoomFull())
        {
            Debug.Log("[MonsterAssignment] Claim rejected: room not full.");
            Answer(claimantActorNumber, ClaimOutcome.RoomNotFull);
            return;
        }
        if (!IsNearCauldron(claimantActorNumber))
        {
            Debug.LogWarning($"[MonsterAssignment] Claim rejected: actor {claimantActorNumber} is not at the cauldron.");
            Answer(claimantActorNumber, ClaimOutcome.NotAtCauldron);
            return;
        }
        double now = PhotonNetwork.Time;
        double entered = photonEvent.CustomData is object[] d && d.Length > 1 && d[1] is double at ? at : now;
        if (double.IsNaN(entered) || entered > now) entered = now; // 미래 시각은 믿지 않는다
        for (int i = 0; i < claims.Count; i++)
            if (claims[i].actor == claimantActorNumber) return; // 다시 보낸 신청(§71 L4) — 이미 모으는 중
        claims.Add((claimantActorNumber, entered));
        if (decideAt < 0f) decideAt = Time.unscaledTime + ClaimWindow;
    }

    // 모은 지원을 들어간 시각 순으로 자리에 넣고, 한 번에 확정한다. 각 지원자에게 결과를 알린다.
    private void DecideClaims()
    {
        decideAt = -1f;
        claims.Sort((a, b) => a.enteredAt.CompareTo(b.enteredAt));
        int capacity = GameSettings.Current.MonsterCountFor(PhotonNetwork.CurrentRoom.PlayerCount);
        int[] roster = MonsterRoster.Current();
        bool changed = false;
        foreach (var claim in claims)
        {
            int actor = claim.actor;
            if (roster.Contains(actor)) { Answer(actor, ClaimOutcome.AlreadyMonster); continue; }
            int[] next = MonsterRoster.TryAppend(roster, actor, capacity);
            if (next == null)
            {
                Debug.Log($"[MonsterAssignment] Claim rejected: no seat left for actor {actor}.");
                Answer(actor, ClaimOutcome.NoSeat);
                continue;
            }
            roster = next;
            changed = true;
            Answer(actor, ClaimOutcome.Accepted);
        }
        claims.Clear();
        if (changed) ConfirmMonsters(roster, "cauldron");
    }

    private static void Answer(int actor, ClaimOutcome outcome)
    {
        // 오프라인 방은 대상 지정(TargetActors)을 무시하므로 모두에게(혼자뿐)
        var options = PhotonNetwork.OfflineMode ? new RaiseEventOptions { Receivers = ReceiverGroup.All } : new RaiseEventOptions { TargetActors = new[] { actor } };
        PhotonNetwork.RaiseEvent(NetEventCodes.ClaimResult, (byte)outcome, options, SendOptions.SendReliable);
    }

    // 지원자의 캐릭터가 가마솥 근처인지. §71 L2: 방장 화면의 지금 위치뿐 아니라 받은 위치 기록(최근 RecentSeconds초)도 본다 —
    // 느린 연결에서 방장 화면의 캐릭터가 아직 가마솥 밖이어도 거절하지 않게. 가마솥·캐릭터를 못 찾으면 막지 않는다.
    private const float CauldronReachSlack = 3f;
    private const double RecentSeconds = 1.5;
    private Cauldron cauldron;
    private Collider cauldronTrigger;

    private bool IsNearCauldron(int actor)
    {
        if (cauldron == null)
        {
            cauldron = FindFirstObjectByType<Cauldron>();
            if (cauldron != null) cauldronTrigger = cauldron.GetComponent<Collider>();
        }
        IGameCharacter character = CharacterRegistry.FindByOwner(actor);
        if (cauldron == null || cauldronTrigger == null || character == null) return true;
        Vector3 at = character.gameObject.transform.position;
        if (Vector3.Distance(cauldronTrigger.ClosestPoint(at), at) <= CauldronReachSlack) return true;
        if (character.View == null || !PositionHistory.TryGet(character.View.ViewID, out PositionTrack track)) return false;
        Bounds b = cauldronTrigger.bounds;
        Vector3 newest = track.NewestPoint;
        Vector3 center = new Vector3(b.center.x, newest.y, b.center.z);
        float radius = Mathf.Max(b.extents.x, b.extents.z);
        double now = PhotonNetwork.Time;
        return track.TryMinDistance(center, now - RecentSeconds, now, newest, out float off) && off <= radius + CauldronReachSlack;
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || !RoomState.IsInRoom())
        {
            claims.Clear();
            decideAt = -1f;
            return;
        }
        if (decideAt >= 0f && Time.unscaledTime >= decideAt) DecideClaims();
        ResetSelectionIfRoomNotFull();
    }

    // 대기실에서 괴물이 나가면 그 사람만 목록에서 빼고, 모자란 자리는 가마솥으로 다시 뽑는다(Bug-fix-plan.md
    // §24.4). 그러지 않으면 떠난 사람이 괴물로 남은 채 시작 버튼이 활성화돼 괴물 없는 판이 시작됐다.
    // (GameScene에서의 이탈은 RoomLifecycleWatcher가 처리한다.)
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        int[] monsters = MonsterRoster.Current();
        if (!monsters.Contains(otherPlayer.ActorNumber)) return;

        // 지금 방에 남은 사람과의 교집합 — 응답 전에 두 번째 괴물이 나가도 둘 다 빠진다(F-04).
        int[] remaining = MonsterRoster.Remaining(monsters, PhotonNetwork.CurrentRoom.Players.Keys);
        Debug.Log($"[MonsterAssignment] Monster (actor {otherPlayer.ActorNumber}) left the lobby. Remaining monsters: {remaining.Length}. Selection restarts for missing seats.");
        AuthorityProps.SetRoom(new Hashtable
        {
            { NetKeys.MonsterActorNumbers, remaining.Length > 0 ? remaining : null },
        });
    }

    // 괴물은 정원이 찼을 때만 존재한다(사용자 결정 §30.7-1). 선정 후 괴물이 아닌 사람이 나가 정원 미달이 되면 선정을
    // 초기화한다 — 괴물 공지는 숨고, 방장 정책(MasterClientPolicy)이 방 생성자에게 권한을 되돌린다(둘 다
    // MonsterActorNumbers 변경에 반응). 이 컴포넌트는 대기실에만 있으므로 게임 중 이탈(RoomLifecycleWatcher)과 무관하다.
    // 서버 응답 전 같은 요청을 반복하지 않도록 resetRequested로 막는다. 처리했으면 true.
    private bool ResetSelectionIfRoomNotFull()
    {
        // 판이 시작된 뒤(시작 버튼이 PrepareEndTime을 기록, 복귀 시 RoundStateResetter가 지움)에는 건드리지 않는다 —
        // 쿠키들이 GameScene에 있는 동안 대기실의 괴물만 남아 방장이 되는 경우에도 진행 중인 판의 괴물 정보를 지우지 않도록.
        if (RoomState.IsRoomFull() || !RoomState.HasMonster() || RoomState.TryGetDouble(NetKeys.PrepareEndTime, out _))
        {
            resetRequested = false;
            return false;
        }
        if (resetRequested) return true;

        resetRequested = true;
        AuthorityProps.SetRoom(new Hashtable
        {
            { NetKeys.MonsterActorNumbers, null },
        });
        Debug.Log("[MonsterAssignment] Room is no longer full. Monster selection reset.");
        return true;
    }

    private void ConfirmMonsters(int[] monsterActorNumbers, string reason)
    {
        if (monsterActorNumbers.Length == 0) return;

        var props = new Hashtable
        {
            { NetKeys.MonsterActorNumbers, monsterActorNumbers },
        };

        AuthorityProps.SetRoom(props);
        Debug.Log($"[MonsterAssignment] Monsters confirmed: [{string.Join(",", monsterActorNumbers)}] (by {reason}).");
    }
}
