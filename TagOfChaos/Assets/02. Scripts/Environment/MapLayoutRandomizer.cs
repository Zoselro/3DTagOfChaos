using UnityEngine;

// 판 배치 적용(Request1009Plan.md §89 L2). EscapeManager·DecoyNavMesh·MonsterRoomGuard보다 먼저(Awake) 서브 맵 묶음과
// 과일 밭 건물을 정해진 자리로 옮긴다. 참조는 그대로이고 위치만 바뀌므로 수리 자리·탈출구·상자 위치가 그대로 따라온다.
[DefaultExecutionOrder(-500)]
public class MapLayoutRandomizer : MonoBehaviour
{
    [SerializeField] private Transform[] subSlots = new Transform[3];      // SubSlot_NW, SubSlot_N, SubSlot_NE(고원 윗면 가운데, 앞 = 맵 가운데)
    [SerializeField] private Transform[] subContents = new Transform[3];   // SubMap_Almond, SubMap_Chocolate, SubMap_Flour
    [SerializeField] private Transform[] fieldLots = new Transform[MapLayout.LotCount];
    [SerializeField] private Transform fieldMaterialBuilding;              // Building_Q4
    [SerializeField] private Transform[] generalBuildings = new Transform[MapLayout.LotCount - 1];

    public static MapLayout.Layout Current { get; private set; }
    public static bool Applied { get; private set; }

    private void Awake()
    {
        Applied = false;
        RememberTilts();
        Current = MapLayout.FromRoom();
        Apply(Current);
    }

    // §89.20 과일 밭 건물이 씬에 놓인 자세에서 자리(lot) 회전을 뺀 나머지(테스트 맵 정자의 X 90° 등) — 옮길 때 다시 곱해 눕지 않게 한다.
    private readonly System.Collections.Generic.Dictionary<Transform, Quaternion> tilts = new System.Collections.Generic.Dictionary<Transform, Quaternion>();

    private void RememberTilts()
    {
        tilts.Clear();
        var movers = new System.Collections.Generic.List<Transform> { fieldMaterialBuilding };
        if (generalBuildings != null) movers.AddRange(generalBuildings);
        foreach (Transform t in movers)
        {
            if (t == null || fieldLots == null) continue;
            Transform near = null; float best = float.MaxValue;
            foreach (Transform lot in fieldLots)
                if (lot != null && (lot.position - t.position).sqrMagnitude < best) { best = (lot.position - t.position).sqrMagnitude; near = lot; }
            tilts[t] = near != null ? Quaternion.Inverse(near.rotation) * t.rotation : Quaternion.identity;
        }
    }

    private void OnDestroy() => Applied = false;

    public void Apply(MapLayout.Layout l)
    {
        int[] order = MapLayout.Permutations[l.SubMapOrder];
        for (int slot = 0; slot < subSlots.Length && slot < order.Length; slot++)
        {
            Transform c = order[slot] < subContents.Length ? subContents[order[slot]] : null;
            if (c != null && subSlots[slot] != null) c.SetPositionAndRotation(subSlots[slot].position, subSlots[slot].rotation);
        }
        int g = 0;
        for (int lot = 0; lot < fieldLots.Length; lot++)
        {
            Transform t = lot == l.MaterialLot ? fieldMaterialBuilding : g < generalBuildings.Length ? generalBuildings[g++] : null;
            if (t != null && fieldLots[lot] != null) t.SetPositionAndRotation(fieldLots[lot].position, fieldLots[lot].rotation * (tilts.TryGetValue(t, out Quaternion tilt) ? tilt : Quaternion.identity));
        }
        Physics.SyncTransforms();   // 같은 프레임의 레이(SnapToGround·DropPointNear)가 새 자리를 보게
        Applied = true;
    }

#if UNITY_EDITOR
    public void EditorBind(Transform[] slots, Transform[] contents, Transform[] lots, Transform materialBuilding, Transform[] generals)
    {
        subSlots = slots; subContents = contents; fieldLots = lots; fieldMaterialBuilding = materialBuilding; generalBuildings = generals;
    }
#endif
}
