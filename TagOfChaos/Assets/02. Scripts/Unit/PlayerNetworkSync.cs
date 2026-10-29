using Photon.Pun;
using UnityEngine;

// 쿠키 동기화 — 공용 NetworkTransformSync에 캐리 여부·들고 있는 쪽 두 필드를 덧붙인다(research.md §12 E1, Request1009Plan.md §16.3).
public class PlayerNetworkSync : NetworkTransformSync<PlayerMoveState>
{
    // 드는 쪽의 캐리(상체 Carry 레이어) 여부 — 원격 화면에서도 들고 있는 자세가 보이도록 동기화한다.
    // (예전 4번째 필드 isJump는 수신만 하고 아무도 쓰지 않던 죽은 값이라 대체했다, research.md §8.21)
    public bool RemoteIsCarrying { get; private set; }

    // 이 쿠키를 들고 있는 쿠키의 ViewID(안 들렸으면 -1) — 들린 사람이 보낸다. 모든 화면이 각자 그 쿠키의 손 위에 놓는다.
    public int RemoteCarrierViewId { get; private set; } = -1;

    // 춤 번호(DanceCatalogSO) — 상태가 Dance일 때만 의미가 있다(Request1009Plan.md §44.9).
    public byte RemoteDanceId { get; private set; }

    // 스턴건 조준 중(우클릭 유지) — 다른 화면에서도 조준 자세가 보이게(Request1009Plan.md §45.6).
    public bool RemoteAiming { get; private set; }

    public void Write(PhotonStream stream, Transform transform, PlayerMoveState state, bool isCarrying, int carrierViewId, byte danceId, bool aiming)
    {
        Write(stream, transform, state);
        stream.SendNext(isCarrying);
        stream.SendNext(carrierViewId);
        stream.SendNext(danceId);
        stream.SendNext(aiming);
    }

    public override void Read(PhotonStream stream, Transform transform)
    {
        base.Read(stream, transform);
        RemoteIsCarrying = (bool)stream.ReceiveNext();
        RemoteCarrierViewId = (int)stream.ReceiveNext();
        RemoteDanceId = (byte)stream.ReceiveNext();
        RemoteAiming = (bool)stream.ReceiveNext();
    }
}
