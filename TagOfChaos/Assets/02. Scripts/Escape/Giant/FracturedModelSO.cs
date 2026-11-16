using UnityEngine;

// 조각 모델의 재질 묶음(Request1009Plan.md §82) — 부서지는 소리를 고른다.
public enum FractureStyle : byte
{
    Chunk = 0,     // 덩어리(사탕·과자·돌·나무)
    Wood = 1,      // 나무·판자
    Shell = 2,     // 얇은 껍데기·기계
    Pole = 3,      // 가는 물체(꺾여 쓰러짐)
    Panel = 4,     // 천·판(텐트·깃발)
    Room = 5,      // 술래방
    Building = 6,  // 건물(§66 조각 모델)
}

// 조각 모델 하나를 가리키는 작은 에셋(Request1009Plan.md §73.11). Resources/Escape/Fractured/<원본 이름>.asset.
// 예전에는 규칙 에셋(DestructionRules)이 조각 모델 38개(모든 맵, 876 MB)를 직접 가리켜, 맵을 열 때 처음 읽는 순간 모두 한꺼번에
// 화면을 멈춘 채 불러왔다(한 PC에 창 3개 → 50초 멈춤 → 서버 끊김). 이제 규칙 에셋은 이름만 갖고, 물체가 처음 맞을 때 그 물체 것만 비동기로 읽는다.
// §82: Blender에서 미리 구운 무너지는 애니메이션(clips)도 함께 든다. 소품은 바깥으로 터지는 1개, 건물은 맞은 방향 4개
// (pushes = 그 애니메이션에서 조각들이 평균으로 움직인 방향, 모델 뿌리 기준 — 맞은 쪽 반대로 가장 가까운 것을 고른다).
public class FracturedModelSO : ScriptableObject
{
    public const string ResourceFolder = "Escape/Fractured/";

    [SerializeField] private GameObject model;
    [SerializeField] private AnimationClip[] clips = new AnimationClip[0];
    [SerializeField] private Vector3[] pushes = new Vector3[0];
    [SerializeField] private FractureStyle style;

    public GameObject Model => model;
    public FractureStyle Style => style;
    public int ClipCount => clips != null ? clips.Length : 0;
    public AnimationClip Clip(int i) => clips != null && i >= 0 && i < clips.Length ? clips[i] : null;
    public Vector3 Push(int i) => pushes != null && i >= 0 && i < pushes.Length ? pushes[i] : Vector3.zero;

    // 모델 뿌리 기준 미는 방향에 가장 가까운 애니메이션(방향이 없거나 하나뿐이면 첫 번째).
    public AnimationClip ClipFor(Vector3 localPush)
    {
        int n = ClipCount;
        if (n == 0) return null;
        if (n == 1 || localPush.sqrMagnitude < 1e-6f) return clips[0];
        int best = 0;
        float bestDot = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            float d = Vector3.Dot(Push(i), localPush.normalized);
            if (d > bestDot) { bestDot = d; best = i; }
        }
        return clips[best];
    }

#if UNITY_EDITOR
    public void EditorSet(GameObject m) => model = m;
    public void EditorSetClips(AnimationClip[] c, Vector3[] p, FractureStyle s) { clips = c; pushes = p; style = s; }
#endif
}

// 조각 모델 비동기 읽기(이름 → 모델). 같은 이름은 한 번만 읽는다.
public static class FracturedModels
{
    private static readonly System.Collections.Generic.Dictionary<string, ResourceRequest> requests =
        new System.Collections.Generic.Dictionary<string, ResourceRequest>();

    // 읽기 시작(이미 시작했으면 그대로).
    public static void Request(string name)
    {
        if (string.IsNullOrEmpty(name) || requests.ContainsKey(name)) return;
        requests[name] = Resources.LoadAsync<FracturedModelSO>(FracturedModelSO.ResourceFolder + name);
    }

    // 다 읽었으면 조각 모델 묶음(없으면 null). 아직 읽는 중이면 loading = true.
    public static FracturedModelSO GetSet(string name, out bool loading)
    {
        loading = false;
        if (string.IsNullOrEmpty(name) || !requests.TryGetValue(name, out ResourceRequest r)) return null;
        if (!r.isDone) { loading = true; return null; }
        return r.asset as FracturedModelSO;
    }

    public static GameObject Get(string name, out bool loading)
    {
        FracturedModelSO set = GetSet(name, out loading);
        return set != null ? set.Model : null;
    }
}
