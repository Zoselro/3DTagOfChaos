using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 가마솥 신청 결과(Request1009Plan.md §71 L1). 방장이 신청한 사람에게만 보낸다(NetEventCodes.ClaimResult).
public enum ClaimOutcome : byte { Accepted = 0, RoomNotFull = 1, NotAtCauldron = 2, NoSeat = 3, AlreadyMonster = 4 }

// 가마솥 트리거 — 쿠키가 안에 있으면 ClaimMonster 이벤트를 마스터에게 보낸다(GameRule.md §2.1).
// §71 L1~L4: 결과를 받아 알린다 · 답이 없으면 RetrySeconds마다 다시 보낸다(최대 MaxTries번) ·
// 안에 서 있는 채로 정원이 차면 자동으로 신청한다 · 자리보다 사람이 많으면 먼저 들어간 사람(enteredAt)이 된다(방장이 짧게 모아 고른다).
[RequireComponent(typeof(Collider))]
public class Cauldron : MonoBehaviour, IOnEventCallback
{
    public const float RetrySeconds = 1f;
    public const int MaxTries = 3;

    [SerializeField] private MonsterRevealController revealController; // 이미 확정된 경우 누가 괴물인지 다시 보여줌

    private bool localInside;
    private double enteredAt;   // 내가 들어간 시각(PhotonNetwork.Time) — 먼저 들어간 사람 우선
    private int tries;          // 이번 "안에 있음"에서 보낸 횟수
    private float retryAt;
    private bool settled;       // 결과를 받았다(다시 들어오거나 정원이 다시 찰 때까지 안 보낸다)

    public static ClaimOutcome? LastOutcome { get; private set; } // 시험·로그용

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<HideOrSeekPlayer>();
        if (player == null || !player.IsMine) return;
        localInside = true;
        enteredAt = PhotonNetwork.Time;
        tries = 0;
        settled = false;

        // 정원이 차기 전에는 아무 반응도 하지 않는다(Bug-fix-plan.md §30.2) — 안에 서 있다가 정원이 차면 Update가 자동 신청한다(§71 L3).
        if (!RoomState.IsRoomFull()) return;

        // 이미 괴물이 정해졌으면 신청을 보내지 않고, 들어간 사람에게 누가 괴물인지 알려준다(Bug-fix-plan.md §24.3 B3).
        if (RoomState.IsMonsterSelectionComplete() || RoomState.IsLocalMonster())
        {
            settled = true;
            if (revealController != null) revealController.ShowAgain();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<HideOrSeekPlayer>();
        if (player == null || !player.IsMine) return;
        localInside = false;
    }

    private void Update()
    {
        if (!localInside || !RoomState.IsInRoom()) return;
        if (!RoomState.IsRoomFull())
        {
            tries = 0;       // 정원이 다시 차면 처음부터 자동 신청
            settled = false;
            return;
        }
        if (settled || RoomState.IsMonsterSelectionComplete() || RoomState.IsLocalMonster()) return;
        if (tries > 0 && Time.unscaledTime < retryAt) return;
        if (tries >= MaxTries)
        {
            settled = true;
            ScreenToast.Show(EscapeTextsSO.Current.claimNoAnswer);
            Debug.LogWarning("[Cauldron] No answer from the host for the monster claim.");
            return;
        }
        SendClaim();
    }

    private void SendClaim()
    {
        tries++;
        retryAt = Time.unscaledTime + RetrySeconds;
        object content = new object[] { PhotonNetwork.LocalPlayer.ActorNumber, enteredAt };
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(NetEventCodes.ClaimMonster, content, options, SendOptions.SendReliable);
        if (tries > 1) Debug.Log($"[Cauldron] Monster claim resent (try {tries}).");
    }

    public void OnEvent(EventData e)
    {
        if (e.Code != NetEventCodes.ClaimResult || !(e.CustomData is byte raw)) return;
        if (!PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null && e.Sender != PhotonNetwork.CurrentRoom.MasterClientId) return;
        var outcome = (ClaimOutcome)raw;
        LastOutcome = outcome;
        settled = true;
        EscapeTextsSO t = EscapeTextsSO.Current;
        switch (outcome)
        {
            case ClaimOutcome.RoomNotFull: ScreenToast.Show(t.claimRoomNotFull); break; // 내 화면에서 정원이 풀렸다 다시 차면 Update가 새로 신청한다
            case ClaimOutcome.NotAtCauldron: ScreenToast.Show(t.claimNotAtCauldron); break;
            case ClaimOutcome.NoSeat:
                ScreenToast.Show(t.claimNoSeat);
                if (revealController != null) revealController.ShowAgain();
                break;
        }
        Debug.Log($"[Cauldron] Monster claim result: {outcome}.");
    }
}
