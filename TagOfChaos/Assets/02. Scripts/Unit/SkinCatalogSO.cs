using UnityEngine;

// 쿠키 스킨 목록(research.md §12 E4). 스킨 적용(PlayerSkinApplier)과 대기실 선택 버튼(PlayerSkinSelector)이
// 같은 목록을 쓴다 — 예전에는 머티리얼 배열은 프리팹에, 버튼 3개(A/B/C)는 씬 코드에 따로 있어 개수가 암묵적으로
// 맞아야 했다. 스킨을 추가하려면 이 에셋에 항목만 더하면 된다. 인덱스는 Player Props의 SkinIndex로 동기화된다.
[CreateAssetMenu(menuName = "TagOfChaos/SkinCatalog", fileName = "SkinCatalog")]
public class SkinCatalogSO : ScriptableObject
{
    [System.Serializable]
    public struct SkinEntry
    {
        public string label;      // 선택 버튼에 표시할 이름(캐릭터 이름 — 에셋에 입력)
        public Material material; // 모델이 오기 전 임시 겉모습(지금 쿠키 몸에 입히는 색)
        [Tooltip("캐릭터 모델(Request1009Plan.md §25.6 — 사용자가 만들면 넣는다). 비어 있으면 지금 쿠키 몸 + material.")]
        public GameObject modelPrefab;
        // 잡혀 부서질 때 가루 색 3~4개(Request1009Plan.md §45.10). 모델 텍스처에서 뽑는다(Tools/Characters/Build Characters).
        [Tooltip("Crumb colors when this character is crushed (filled from the model texture).")]
        public Color[] crumbColors;
    }

    [SerializeField] private SkinEntry[] skins = new SkinEntry[0];

    public int Count => skins != null ? skins.Length : 0;

    // 네트워크로 받은 인덱스가 범위를 벗어나도 예외 대신 가장 가까운 유효 값을 쓴다.
    public int ClampIndex(int index) => Count == 0 ? 0 : Mathf.Clamp(index, 0, Count - 1);
    public Material GetMaterial(int index) => Count == 0 ? null : skins[ClampIndex(index)].material;
    public string GetLabel(int index) => Count == 0 ? string.Empty : skins[ClampIndex(index)].label;
    public GameObject GetModel(int index) => Count == 0 ? null : skins[ClampIndex(index)].modelPrefab;
    public Color[] GetCrumbColors(int index) => Count == 0 ? null : skins[ClampIndex(index)].crumbColors;

#if UNITY_EDITOR
    public void EditorSet(SkinEntry[] entries) => skins = entries;
    public SkinEntry EditorGet(int index) => skins[index];
#endif
}
