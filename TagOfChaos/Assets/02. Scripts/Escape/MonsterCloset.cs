using UnityEngine;

// §90 T2 재료 건물 안 술래 도착 자리 — 벽장(미닫이 문 두 짝) 또는 바닥 뚜껑(두 짝이 위로 열림). 맵 빌더가 놓는다.
// Stand = 술래가 숨어 기다리는 자리(문 안), Exit = 나오는 자리(문 앞). 모습은 MonsterWarpPresenter가 시각으로 움직인다.
public class MonsterCloset : MonoBehaviour
{
    public enum Kind { Closet, Hatch }

    [SerializeField] private Kind kind = Kind.Closet;
    [SerializeField] private Transform stand;
    [SerializeField] private Transform exit;
    [Tooltip("벽장: 왼·오른 문(옆으로 미끄러짐). 뚜껑: 왼·오른 판(바깥 경첩으로 위로 열림).")]
    [SerializeField] private Transform doorL, doorR;
    [Tooltip("문이 다 열렸을 때 옆으로 가는 거리(m, 모델 기준) — 벽장")]
    [SerializeField] private float slide = 2.05f;
    [SerializeField] private float hatchAngle = 100f;

    // 문·판의 처음 자세(벽장 뿌리 기준) — FBX 안 노드는 크기 100·회전이 있어 뿌리 공간에서 움직인다.
    private Vector3 posL, posR;
    private Quaternion rotL, rotR;
    private bool cached;

    public Kind Type => kind;
    public Vector3 StandPoint => stand != null ? stand.position : transform.position;
    public Vector3 ExitPoint => exit != null ? exit.position : transform.position + transform.forward * 3f;
    public Quaternion Facing => Quaternion.LookRotation(transform.forward, Vector3.up);
    public Vector3 SoundPoint => transform.position + Vector3.up * (kind == Kind.Closet ? 3f : 0.5f);
    public const float HatchHinge = 2.17f;   // 뚜껑 판 바깥 경첩(뿌리 기준 ±x)

#if UNITY_EDITOR
    public void EditorSetup(Kind k, Transform standPoint, Transform exitPoint, Transform left, Transform right)
    {
        kind = k; stand = standPoint; exit = exitPoint; doorL = left; doorR = right;
    }
#endif

    private void Cache()
    {
        if (cached) return;
        cached = true;
        if (doorL != null) { posL = transform.InverseTransformPoint(doorL.position); rotL = Quaternion.Inverse(transform.rotation) * doorL.rotation; }
        if (doorR != null) { posR = transform.InverseTransformPoint(doorR.position); rotR = Quaternion.Inverse(transform.rotation) * doorR.rotation; }
    }

    // open: 0 = 닫힘, 1 = 다 열림. rattle: 0~1 덜컹 세기(닫힌 채 흔들림).
    public void Pose(float open, float rattle, float time)
    {
        Cache();
        float shake = rattle > 0f ? rattle * (Mathf.PerlinNoise(time * 23f, 0.3f) - 0.5f) : 0f;
        float shake2 = rattle > 0f ? rattle * (Mathf.PerlinNoise(0.7f, time * 19f) - 0.5f) : 0f;
        if (kind == Kind.Closet)
        {
            // 문 모델은 왼쪽(Door_L)이 +x에 있다 — 각자 바깥쪽으로 미끄러진다(덜컹 = 조금씩 앞뒤·옆)
            Place(doorL, posL + new Vector3(slide * open + shake * 0.08f, 0f, shake2 * 0.06f), rotL);
            Place(doorR, posR + new Vector3(-slide * open - shake2 * 0.08f, 0f, shake * 0.06f), rotR);
        }
        else
        {
            // 바깥 경첩(±HatchHinge)을 축(앞 방향)으로 위로 열린다
            Hinge(doorL, posL, rotL, new Vector3(HatchHinge, 0f, 0f), -(hatchAngle * open + shake * 6f));
            Hinge(doorR, posR, rotR, new Vector3(-HatchHinge, 0f, 0f), hatchAngle * open + shake2 * 6f);
        }
    }

    private void Place(Transform t, Vector3 localPos, Quaternion localRot)
    {
        if (t == null) return;
        t.SetPositionAndRotation(transform.TransformPoint(localPos), transform.rotation * localRot);
    }

    private void Hinge(Transform t, Vector3 basePos, Quaternion baseRot, Vector3 hinge, float angle)
    {
        if (t == null) return;
        Quaternion r = Quaternion.AngleAxis(angle, Vector3.forward);
        Place(t, hinge + r * (basePos - hinge), r * baseRot);
    }

    public static MonsterCloset For(Transform building) => building != null ? building.GetComponentInChildren<MonsterCloset>(true) : null;
}
