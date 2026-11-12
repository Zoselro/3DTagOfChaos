using UnityEngine;

// 술래가 예고한 돌진 한 번(Request1009Plan.md §70.2.5 D1): 준비 자세가 시작될 때 시작 위치·방향·출발 시각을 모두에게 알린다.
// 돌진은 초속 68 m라 초당 10번 오는 위치 동기화로는 방장이 길을 알 수 없다 — 방장은 "예고된 길 위, 그 시각까지 갈 수 있는 거리 안"인지로 잰다.
// 네트워크·씬을 모르는 순수 계산이라 EditMode로 시험한다.
public struct DashWindow
{
    public Vector3 Start;
    public Vector3 Direction;   // 수평 단위 벡터
    public double MoveStartsAt; // 출발 시각(공용 시계, 초)
    public float Speed;         // m/s
    public float Distance;      // m

    public bool IsValid => Speed > 0f && Distance > 0f && Direction.sqrMagnitude > 0.5f;
    public double MoveEndsAt => MoveStartsAt + Distance / Mathf.Max(0.01f, Speed);

    // t가 돌진 이동 시간(앞뒤 slack초 여유) 안인지.
    public bool Covers(double t, float slack) => IsValid && t >= MoveStartsAt - slack && t <= MoveEndsAt + slack;

    // t 시각까지 돌진으로 갈 수 있는 거리.
    public float ReachableAt(double t) => Mathf.Clamp((float)(t - MoveStartsAt) * Speed, 0f, Distance);

    public Vector3 PointAt(double t) => Start + Direction * ReachableAt(t);

    // 주장한 위치가 "시작 ~ (t + timeSlack)까지 갈 수 있는 곳" 선분에서 벗어난 수평 거리. 높이(경사·계단)는 잡기 높이 검사가 따로 본다.
    public float PathError(Vector3 claimed, double t, float timeSlack)
    {
        Vector3 a = Start, b = Start + Direction * ReachableAt(t + timeSlack);
        a.y = b.y = claimed.y;
        return PositionTrack.SegmentDistance(claimed, a, b);
    }
}
