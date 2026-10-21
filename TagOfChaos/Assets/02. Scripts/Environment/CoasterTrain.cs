using UnityEngine;

// 롤러코스터 한 편성(머리 칸 + 좌석 칸, Request1009Plan.md §31). 칸마다 앞·뒤 바퀴 두 점을 레일 윗면에 놓고
// 칸 방향 = 뒤 바퀴 → 앞 바퀴, 칸 위쪽 = 두 바퀴 위쪽의 평균 — 곡선·고리에서도 칸이 레일에 붙고 칸 사이가 자연스럽게 꺾인다.
// 칸은 키네마틱 Rigidbody(충돌체는 칸에)라 물리 스텝에서 MovePosition/MoveRotation으로 옮기고 화면은 보간한다.
public sealed class CoasterTrain : MonoBehaviour
{
    [System.Serializable]
    public struct Car
    {
        public Rigidbody body;
        public float behindHead; // 머리 칸 기준점에서 이 칸 기준점까지(레일 거리, m)
        public float wheelHalf;  // 기준점에서 앞·뒤 바퀴까지(m)
    }

    [SerializeField] private Car[] cars = new Car[0];

    private CoasterPathSO path;
    private Transform track;

    public float HeadDistance { get; private set; }
    public float Speed => path != null ? path.SpeedAt(HeadDistance) : 0f;
    public bool OnChain => path != null && path.IsChainAt(HeadDistance);
    public int CarCount => cars.Length;
    public Rigidbody CarBody(int i) => cars[i].body;

    public void Bind(CoasterPathSO value, Transform trackRoot)
    {
        path = value;
        track = trackRoot;
    }

    public void Place(float headDistance, bool teleport)
    {
        HeadDistance = headDistance;
        for (int i = 0; i < cars.Length; i++)
        {
            float s = headDistance - cars[i].behindHead;
            Pose(s, cars[i].wheelHalf, out Vector3 pos, out Quaternion rot);
            Rigidbody body = cars[i].body;
            if (body == null) continue;
            if (teleport || !Application.isPlaying)
            {
                body.transform.SetPositionAndRotation(pos, rot);
                body.position = pos;
                body.rotation = rot;
            }
            else
            {
                body.MovePosition(pos);
                body.MoveRotation(rot);
            }
        }
    }

    // 레일 거리 s에 기준점이 있는 칸의 월드 자세(바퀴 사이 반 길이 wheelHalf)
    public void Pose(float s, float wheelHalf, out Vector3 position, out Quaternion rotation)
    {
        float top = path.RailTop;
        path.Sample(s + wheelHalf, out Vector3 pf, out Vector3 ff, out Vector3 uf);
        path.Sample(s - wheelHalf, out Vector3 pr, out Vector3 fr, out Vector3 ur);
        pf += uf * top;
        pr += ur * top;
        Vector3 forward = pf - pr;
        if (forward.sqrMagnitude < 1e-6f) forward = ff + fr;
        Vector3 up = uf + ur;
        position = track.TransformPoint((pf + pr) * 0.5f);
        rotation = track.rotation * Quaternion.LookRotation(forward.normalized, up.normalized);
    }

#if UNITY_EDITOR
    public void EditorSetup(Car[] value) => cars = value;
#endif
}
