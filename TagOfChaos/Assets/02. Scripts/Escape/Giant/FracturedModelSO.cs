using UnityEngine;

// 조각 모델 하나를 가리키는 작은 에셋(Request1009Plan.md §73.11). Resources/Escape/Fractured/<원본 이름>.asset.
// 예전에는 규칙 에셋(DestructionRules)이 조각 모델 38개(모든 맵, 876 MB)를 직접 가리켜, 맵을 열 때 처음 읽는 순간 모두 한꺼번에
// 화면을 멈춘 채 불러왔다(한 PC에 창 3개 → 50초 멈춤 → 서버 끊김). 이제 규칙 에셋은 이름만 갖고, 건물이 처음 맞을 때 그 건물 것만 비동기로 읽는다.
public class FracturedModelSO : ScriptableObject
{
    public const string ResourceFolder = "Escape/Fractured/";

    [SerializeField] private GameObject model;

    public GameObject Model => model;

#if UNITY_EDITOR
    public void EditorSet(GameObject m) => model = m;
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

    // 다 읽었으면 모델(없으면 null). 아직 읽는 중이면 loading = true.
    public static GameObject Get(string name, out bool loading)
    {
        loading = false;
        if (string.IsNullOrEmpty(name) || !requests.TryGetValue(name, out ResourceRequest r)) return null;
        if (!r.isDone) { loading = true; return null; }
        return r.asset is FracturedModelSO so ? so.Model : null;
    }
}
