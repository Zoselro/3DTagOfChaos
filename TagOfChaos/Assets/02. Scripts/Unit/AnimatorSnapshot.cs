using UnityEngine;

// Animator.Rebind()(캐릭터 모델 교체로 아바타를 바꿀 때) 앞뒤로 모든 층의 상태·재생 위치·가중치와 Int/Float/Bool 파라미터를
// 그대로 되돌린다(Request1009Plan.md §81 S1). Rebind는 Animator를 처음 켠 것처럼 되돌려(바닥 층 Idle, 위 층 가중치 0),
// 대기실에서 들고 달리다 스킨을 바꾸면 Idle 자세로 달리고 드는 자세가 사라졌다 — 동작 코드는 "값이 바뀔 때만" Animator에 보내기 때문.
// Trigger는 이미 쓴 신호라 되돌리지 않는다 — 상태를 직접 다시 틀므로 필요 없다.
public sealed class AnimatorSnapshot
{
    private struct Layer { public int Hash; public float Time; public float Weight; }

    private Layer[] layers;
    private AnimatorControllerParameter[] parameters;
    private float[] floats;
    private int[] ints;
    private bool[] bools;
    private float speed;

    // 아직 한 번도 돌지 않은 Animator(막 생긴 캐릭터)는 기억할 것이 없다 — null.
    public static AnimatorSnapshot Take(Animator a)
    {
        if (a == null || a.runtimeAnimatorController == null || !a.isInitialized || !a.isActiveAndEnabled) return null;
        var s = new AnimatorSnapshot { speed = a.speed, layers = new Layer[a.layerCount] };
        for (int i = 0; i < a.layerCount; i++)
        {
            // 넘어가는 중이면 넘어갈 상태를 기억한다
            AnimatorStateInfo st = a.IsInTransition(i) ? a.GetNextAnimatorStateInfo(i) : a.GetCurrentAnimatorStateInfo(i);
            s.layers[i] = new Layer
            {
                Hash = st.fullPathHash,
                Time = st.loop ? Mathf.Repeat(st.normalizedTime, 1f) : st.normalizedTime,
                Weight = i == 0 ? 1f : a.GetLayerWeight(i),
            };
        }
        s.parameters = a.parameters;
        int n = s.parameters.Length;
        s.floats = new float[n];
        s.ints = new int[n];
        s.bools = new bool[n];
        for (int i = 0; i < n; i++)
        {
            int h = s.parameters[i].nameHash;
            switch (s.parameters[i].type)
            {
                case AnimatorControllerParameterType.Float: s.floats[i] = a.GetFloat(h); break;
                case AnimatorControllerParameterType.Int: s.ints[i] = a.GetInteger(h); break;
                case AnimatorControllerParameterType.Bool: s.bools[i] = a.GetBool(h); break;
            }
        }
        return s;
    }

    public void Restore(Animator a)
    {
        if (a == null || a.runtimeAnimatorController == null) return;
        for (int i = 0; i < parameters.Length; i++)
        {
            int h = parameters[i].nameHash;
            switch (parameters[i].type)
            {
                case AnimatorControllerParameterType.Float: a.SetFloat(h, floats[i]); break;
                case AnimatorControllerParameterType.Int: a.SetInteger(h, ints[i]); break;
                case AnimatorControllerParameterType.Bool: a.SetBool(h, bools[i]); break;
            }
        }
        for (int i = 0; i < layers.Length && i < a.layerCount; i++)
        {
            if (i > 0) a.SetLayerWeight(i, layers[i].Weight);
            if (layers[i].Hash != 0 && a.HasState(i, layers[i].Hash)) a.Play(layers[i].Hash, i, layers[i].Time);
        }
        a.speed = speed; // 점프 정점 멈춤(speed 0)도 그대로
    }
}
