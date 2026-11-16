using System.Collections.Generic;
using UnityEngine;

// 미리 구운 무너지는 애니메이션 틀기(Request1009Plan.md §82). 실행 중 물리 없음 — 매번·모든 화면에서 같은 모습.
// - 원본 노드(ModelRoot)의 세계 위치·회전·크기를 가진 감싸개 아래에 조각 모델을 원래 크기 그대로 놓고, 프레임마다
//   AnimationClip.SampleAnimation으로 그 시각의 자세를 넣는다(Animator·컨트롤러 없음. 애니메이션 경로 = 조각 모델 뿌리 기준).
//   애니메이션이 모델 뿌리 값을 써도 감싸개 덕분에 자리는 그대로다.
// - Hold ≥ 0(소품·텐트·마녀 대상): 끝난 뒤 Hold초 두었다가 조각이 작아지며 사라지고, 같은 종류의 다음 물체가 다시 쓴다(풀).
// - Hold < 0(건물·술래방): 끝난 자세로 멈춘다(Finished) — DestructionPresenter가 큰 잔해만 남기고 나머지를 지운다.
public sealed class FracturePlayer : MonoBehaviour
{
    public const float ShrinkSeconds = 0.6f;

    private enum Phase { Play, Hold, Shrink, Done }

    private static readonly Dictionary<FracturedModelSO, Stack<FracturePlayer>> pool = new Dictionary<FracturedModelSO, Stack<FracturePlayer>>();

    public FracturedModelSO Set { get; private set; }
    public GameObject Model { get; private set; }
    public AnimationClip Clip { get; private set; }
    public bool Finished => phase != Phase.Play;
    public float Length => Clip != null ? Clip.length : 0f;

    private Transform[] parts;
    private Vector3[] partScales;
    private Phase phase = Phase.Done;
    private float start, hold;
    private bool pooled;

    // 조각 모델을 at 자리에서 튼다. worldPush = 미는 방향(세계, 방향 애니메이션을 고른다). 모델이 없으면 null.
    public static FracturePlayer Play(FracturedModelSO set, Transform at, Vector3 worldPush, float hold, bool usePool)
    {
        if (set == null || set.Model == null || at == null) return null;
        FracturePlayer p = null;
        if (usePool && pool.TryGetValue(set, out Stack<FracturePlayer> stack))
            while (p == null && stack.Count > 0) p = stack.Pop(); // 씬이 바뀌어 지워진 것은 건너뛴다
        if (p == null) p = Create(set);
        p.pooled = usePool;
        p.hold = hold;
        p.transform.SetPositionAndRotation(at.position, at.rotation);
        p.transform.localScale = at.lossyScale;
        for (int i = 0; i < p.parts.Length; i++) if (p.parts[i] != null) p.parts[i].localScale = p.partScales[i];
        p.Clip = set.ClipFor(p.Model.transform.InverseTransformDirection(worldPush));
        p.gameObject.SetActive(true);
        p.start = Time.time;
        p.phase = Phase.Play;
        p.Sample(0f);
        return p;
    }

    private static FracturePlayer Create(FracturedModelSO set)
    {
        var wrap = new GameObject("Fracture_" + set.name);
        var p = wrap.AddComponent<FracturePlayer>();
        p.Set = set;
        p.Model = Instantiate(set.Model, wrap.transform, false);
        var list = new List<Transform>();
        foreach (Renderer r in p.Model.GetComponentsInChildren<Renderer>()) list.Add(r.transform);
        p.parts = list.ToArray();
        p.partScales = new Vector3[p.parts.Length];
        for (int i = 0; i < p.parts.Length; i++) p.partScales[i] = p.parts[i].localScale;
        return p;
    }

    public Renderer[] Renderers() => Model != null ? Model.GetComponentsInChildren<Renderer>() : new Renderer[0];

    private void Sample(float t)
    {
        if (Clip != null && Model != null) Clip.SampleAnimation(Model, Mathf.Clamp(t, 0f, Clip.length));
    }

    private void Update()
    {
        float e = Time.time - start;
        switch (phase)
        {
            case Phase.Play:
                Sample(e);
                if (e < Length) return;
                phase = hold < 0f ? Phase.Done : Phase.Hold;
                return;
            case Phase.Hold:
                if (e >= Length + hold) phase = Phase.Shrink;
                return;
            case Phase.Shrink:
                float k = 1f - Mathf.Clamp01((e - Length - hold) / ShrinkSeconds);
                for (int i = 0; i < parts.Length; i++) if (parts[i] != null) parts[i].localScale = partScales[i] * k;
                if (k <= 0f) Release();
                return;
        }
    }

    private void Release()
    {
        phase = Phase.Done;
        if (!pooled) { Destroy(gameObject); return; }
        gameObject.SetActive(false);
        if (!pool.TryGetValue(Set, out Stack<FracturePlayer> stack)) pool[Set] = stack = new Stack<FracturePlayer>();
        stack.Push(this);
    }

    // 시험용: 풀에 든 수
    public static int Pooled(FracturedModelSO set) => set != null && pool.TryGetValue(set, out Stack<FracturePlayer> s) ? s.Count : 0;
}
