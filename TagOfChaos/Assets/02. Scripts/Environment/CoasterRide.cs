using Photon.Pun;
using UnityEngine;

// 판 내내 도는 롤러코스터(Request1009Plan.md §31). 트랙 루트에 붙이고, 편성(CoasterTrain)마다 공통 시계로 레일 위 거리를 정해 준다.
// 시계 = 방 안이면 PhotonNetwork.Time, 아니면 Time.timeAsDouble — 모든 화면이 같은 자리를 계산하므로 네트워크 메시지가 없다.
// 네트워크 시각은 조금씩 튀므로 화면 시계는 프레임 시간으로 흐르다 차이를 천천히 따라잡는다(0.5초 넘게 벌어지면 바로 맞춤).
// 두 번째 편성은 경로의 SecondTrainOffset(한 바퀴 비율)만큼 앞서 간다(공용 구간에서 겹치지 않는 값, CoasterPathBaker가 구함).
public sealed class CoasterRide : MonoBehaviour
{
    private const double SnapSeconds = 0.5;
    private const float CatchUpRate = 2f; // 1초에 차이의 이만큼 비율을 따라잡는다

    [SerializeField] private CoasterPathSO path;
    [SerializeField] private CoasterTrain[] trains = new CoasterTrain[0];

    private double clock;
    private bool clockStarted;

    public CoasterPathSO Path => path;
    public int TrainCount => trains.Length;
    public CoasterTrain Train(int i) => trains[i];

    // 편성 i의 머리 칸이 시각 t에 있는 레일 거리(m).
    public float HeadDistance(int i, double t) => path.DistanceAtTime(t + i * path.SecondTrainOffset * path.LapTime);

    public static double NetworkTime => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

    private void Start()
    {
        if (path == null || !path.IsValid) { enabled = false; return; }
        foreach (CoasterTrain train in trains) if (train != null) train.Bind(path, transform);
        Step(0f, true);
    }

    private void FixedUpdate() => Step(Time.fixedDeltaTime, false);

    private void Step(float dt, bool teleport)
    {
        double target = NetworkTime;
        if (!clockStarted || System.Math.Abs(target - clock) > SnapSeconds) { clock = target; clockStarted = true; }
        else clock += dt + (target - clock - dt) * Mathf.Clamp01(CatchUpRate * dt);
        for (int i = 0; i < trains.Length; i++)
            if (trains[i] != null) trains[i].Place(HeadDistance(i, clock), teleport);
    }

#if UNITY_EDITOR
    public void EditorSetup(CoasterPathSO value, CoasterTrain[] value2) { path = value; trains = value2; }

    // 에디터에서 시각 t의 자리로 바로 놓는다(시험·미리보기).
    public void EditorPlaceAt(double t)
    {
        for (int i = 0; i < trains.Length; i++)
        {
            trains[i].Bind(path, transform);
            trains[i].Place(HeadDistance(i, t), true);
        }
    }
#endif
}
