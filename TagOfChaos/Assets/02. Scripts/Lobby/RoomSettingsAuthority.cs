using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 방 설정(인원·스파이 수·마녀 타이머, Request1009Plan.md §25.5 A) 변경의 단일 실행 지점(research.md R5-22).
// 시작 버튼과 같은 호스트(RoomState.HostActor — 괴물이어도 됨)가 바꿀 수 있고, 실제 반영은 Photon 방장이 요청자와
// 값의 범위를 다시 확인해 한다. 호스트가 방장이 아니면 요청 이벤트(NetEventCodes.RoomSettingsRequest)를 보낸다 — GameStartAuthority와 같은 모양.
// 서버 지역(2026-10-03, Request1003Plan.md §1)을 바꾸면 방장이 "옮기기"를 기록하고 모두가 새 지역의 같은 방으로 옮겨 간다(RegionMove).
public static class RoomSettingsAuthority
{
    public enum Result { Applied, PlayersRaised, NotHost, NotMaster, NotInRoom }

    // 지역을 바꾸지 않을 때 넘기는 값(이벤트에서는 -1).
    public const string KeepRegion = null;

    public static bool CanLocalEdit() => RoomState.IsInRoom() && RoomState.IsLocalHost();

    // 호스트 화면에서 호출. 방장이면 바로 반영하고, 아니면 방장에게 요청한다(결과는 Room Props로 모두에게 보인다).
    public static Result Request(int players, int spies, int witchMinutes, string region = KeepRegion)
    {
        if (!RoomState.IsInRoom() || PhotonNetwork.LocalPlayer == null) return Result.NotInRoom;
        if (!CanLocalEdit()) return Result.NotHost;
        if (PhotonNetwork.IsMasterClient) return TryApply(PhotonNetwork.LocalPlayer.ActorNumber, players, spies, witchMinutes, region);

        int regionIndex = PhotonRegions.IndexOf(region);
        MasterRequestResend.Send(NetEventCodes.RoomSettingsRequest, new[] { players, spies, witchMinutes, regionIndex }); // §72 E3
        // 방장도 같은 규칙으로 맞추므로 결과를 미리 알 수 있다
        return ClampPlayers(players) > players ? Result.PlayersRaised : Result.Applied;
    }

    // 방장 쪽 실행. 요청자가 호스트인지 확인하고, 값은 설정 SO의 허용 범위(인원은 지금 들어온 사람 수 이상)로 맞춘다.
    public static Result TryApply(int requesterActor, int players, int spies, int witchMinutes, string region = KeepRegion)
    {
        if (!RoomState.IsInRoom()) return Result.NotInRoom;
        if (!PhotonNetwork.IsMasterClient) return Result.NotMaster;
        if (requesterActor != RoomState.HostActor()) return Result.NotHost;

        GameSettingsSO settings = GameSettings.Current;
        Room room = PhotonNetwork.CurrentRoom;
        int clampedPlayers = ClampPlayers(players);
        room.MaxPlayers = clampedPlayers;
        var props = new Hashtable
        {
            { NetKeys.RoomSpyCount, settings.ClampSpies(spies, clampedPlayers) },
            { NetKeys.TimeAttackDuration, Mathf.Clamp(witchMinutes, settings.MinTimeAttackMinutes, settings.MaxTimeAttackMinutes) * 60 },
        };
        // 지역 옮기기는 판이 시작되기 전(대기실)에만. 방을 닫아 옮기는 동안 새로 들어오는 사람이 없게 한다.
        if (ShouldMoveRegion(region, PhotonRegions.Current, GamePhaseState.Current))
        {
            props[NetKeys.RegionMove] = RegionMove.Encode(region, RegionMove.NewToken());
            room.IsOpen = false;
            room.IsVisible = false;
        }
        AuthorityProps.SetRoom(props);
        return clampedPlayers > players ? Result.PlayersRaised : Result.Applied; // 지금 인원보다 적게 고르면 올려서 알린다
    }

    // 방장 쪽: 요청 이벤트를 풀어 실행한다. 형식이 맞지 않으면 무시.
    public static void HandleRequest(EventData photonEvent)
    {
        if (!(photonEvent.CustomData is int[] values) || values.Length < 3) return;
        string region = values.Length > 3 && values[3] >= 0 && values[3] < PhotonRegions.Codes.Length ? PhotonRegions.Codes[values[3]] : KeepRegion;
        Result result = TryApply(photonEvent.Sender, values[0], values[1], values[2], region);
        Debug.Log($"[RoomSettings] Request from actor {photonEvent.Sender}: {result}");
    }

    // 순수 판정(테스트용): 올바른 다른 지역이고 판이 시작되기 전일 때만 옮긴다.
    public static bool ShouldMoveRegion(string requested, string current, GamePhase phase) =>
        PhotonRegions.IsValid(requested) && requested != current && current != null && phase == GamePhase.Lobby;

    private static int ClampPlayers(int players)
    {
        GameSettingsSO settings = GameSettings.Current;
        int floor = Mathf.Max(settings.MinPlayers, PhotonNetwork.CurrentRoom.PlayerCount);
        return Mathf.Clamp(players, floor, Mathf.Max(floor, settings.MaxPlayers));
    }
}
