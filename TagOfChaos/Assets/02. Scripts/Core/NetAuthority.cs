using Photon.Pun;

// 판정을 맡은 쪽인지와 공용 시계(Request1009Plan.md §70.1.1). 지금은 PUN 방장이다. 나중에 판정을 전용 서버로 옮기면 이 한 곳만 바꾼다 —
// 새 판정 코드(돌진 예고·투사체)는 PhotonNetwork.IsMasterClient·Time을 직접 부르지 않고 여기를 거친다.
public static class NetAuthority
{
    public static bool IsAuthority => PhotonNetwork.IsMasterClient;
    public static double Time => PhotonNetwork.Time;
    public static int Stamp => PhotonNetwork.ServerTimestamp;

    // 보낸 쪽이 찍은 Stamp가 지금부터 몇 초 전인지(음수 = 시계 차이로 앞선 값).
    public static float AgeOf(int stamp) => unchecked(PhotonNetwork.ServerTimestamp - stamp) / 1000f;
}
