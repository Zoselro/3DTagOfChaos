using System.Collections.Generic;
using UnityEngine;

// AI가 숨을 수 있는 곳(창고·텐트, Request1009Plan.md §44.4·§44.7). 안쪽 자리(바닥에 맞춘 NavMesh 위 점)는 에디터 굽기 도구가 채운다.
// 같은 곳에 동시에 Capacity마리까지만 들어간다(DecoyBrain이 자리를 나눠 쓴다).
public class DecoyHideSpot : MonoBehaviour
{
    public const int Capacity = 2;

    private static readonly List<DecoyHideSpot> all = new List<DecoyHideSpot>();

    [SerializeField] private Vector3[] spots = new Vector3[0]; // 월드 좌표

    public static IReadOnlyList<DecoyHideSpot> All => all;
    public int SpotCount => spots.Length;
    public Vector3 Spot(int i) => spots[Mathf.Clamp(i, 0, spots.Length - 1)];

    private void OnEnable()
    {
        if (spots.Length > 0) all.Add(this);
    }

    private void OnDisable() => all.Remove(this);

#if UNITY_EDITOR
    public void EditorSetSpots(Vector3[] worldSpots) => spots = worldSpots ?? new Vector3[0];
#endif
}
