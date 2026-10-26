using System.Collections.Generic;
using UnityEngine;

// 적외선 투시처럼 벽 너머로도 보이게(Request1009Plan.md §25.5 F — 열린 탈출구, 스파이 로켓). 대상 렌더러마다 같은 메시를 투시 재질로 그리는
// 사본 렌더러(자식 XRay)를 둔다 — 재질 목록에 한 장 덧붙이면 Unity는 마지막 부분 메시에만 그려, 재질이 여럿인 모델(초콜릿 공장 로켓 13개)은
// 조각만 빛나 모양을 알 수 없었다(§42). 정적 배칭된 렌더러는 메시가 합쳐져 있어 예전처럼 덧붙인다.
// 투시 재질은 가려진 부분만 빛나는 색으로 그린다(셰이더 TagOfChaos/XRaySilhouette, 재질 Resources/Escape/XRay). 누구에게 보일지는 부르는 쪽이 정한다.
public class XRayMarker : MonoBehaviour
{
    private const string MaterialPath = "Escape/XRay";
    private const string CopyName = "XRay";
    private static Material sharedBase;

    private readonly List<GameObject> copies = new List<GameObject>();
    private readonly List<Renderer> appended = new List<Renderer>();
    private Material material;
    private bool visible, requested;
    private float hideWithinSqr; // 카메라가 이보다 가까우면 끈다(0 = 늘 켬) — 가까이서는 자기 부품끼리 가린 곳까지 비쳐 얼룩져 보인다

    // fill: 가려진 모양 안쪽을 채우는 세기(0~1, 재질 기본 0.25). 멀리서 모양을 알아봐야 하는 것은 진하게.
    public void Init(Color color, float fill = -1f)
    {
        if (sharedBase == null) sharedBase = Resources.Load<Material>(MaterialPath);
        if (sharedBase == null) return;
        material = new Material(sharedBase);
        material.SetColor("_Color", color);
        if (fill >= 0f) material.SetFloat("_Fill", fill);
        copies.Clear();
        appended.Clear();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == CopyName) continue;
            GameObject copy = r.isPartOfStaticBatch ? null : MakeCopy(r);
            if (copy != null) copies.Add(copy);
            else if (r is MeshRenderer || r is SkinnedMeshRenderer) appended.Add(r);
        }
    }

    private GameObject MakeCopy(Renderer source)
    {
        Mesh mesh = null;
        if (source is MeshRenderer)
        {
            var filter = source.GetComponent<MeshFilter>();
            mesh = filter != null ? filter.sharedMesh : null;
        }
        else if (source is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
        if (mesh == null) return null;

        var go = new GameObject(CopyName);
        go.transform.SetParent(source.transform, false);
        go.layer = source.gameObject.layer;
        go.SetActive(false);
        Renderer r;
        if (source is SkinnedMeshRenderer s)
        {
            var copy = go.AddComponent<SkinnedMeshRenderer>();
            copy.sharedMesh = mesh;
            copy.bones = s.bones;
            copy.rootBone = s.rootBone;
            copy.updateWhenOffscreen = s.updateWhenOffscreen;
            r = copy;
        }
        else
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r = go.AddComponent<MeshRenderer>();
        }
        var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
        for (int i = 0; i < mats.Length; i++) mats[i] = material;
        r.sharedMaterials = mats;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    // 가까이 오면 투시를 끈다(직접 보이므로). 스파이 로켓처럼 부품이 겹친 모델에 쓴다.
    public void HideWithin(float metres) => hideWithinSqr = metres * metres;

    public void SetVisible(bool value)
    {
        requested = value;
        Apply(value && !Near());
    }

    private void Update()
    {
        if (requested && hideWithinSqr > 0f) Apply(!Near());
    }

    private bool Near()
    {
        if (hideWithinSqr <= 0f) return false;
        Camera cam = Camera.main;
        return cam != null && (cam.transform.position - transform.position).sqrMagnitude < hideWithinSqr;
    }

    private void Apply(bool value)
    {
        if (visible == value || material == null) return;
        visible = value;
        foreach (GameObject c in copies)
            if (c != null) c.SetActive(value);
        foreach (Renderer r in appended)
        {
            if (r == null) continue;
            var mats = new List<Material>(r.sharedMaterials);
            if (value) { if (!mats.Contains(material)) mats.Add(material); }
            else mats.Remove(material);
            r.sharedMaterials = mats.ToArray();
        }
    }

    // 컴포넌트만 지워져도 사본이 남지 않게(물체째 지우면 자식인 사본도 함께 사라진다). 에디터 시험에서도 부를 수 있게 즉시 지우기를 가린다.
    private void OnDestroy()
    {
        foreach (GameObject c in copies) Release(c);
        Release(material);
    }

    private static void Release(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
