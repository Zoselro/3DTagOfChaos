using UnityEngine;

// §90 U2 서브 맵(SubMap_Almond/Chocolate/Flour)의 하늘 섬 몸체 재질. 판마다 서브 맵이 세 자리(고원)를 바꿔 앉으므로,
// MapLayoutRandomizer.Apply가 그 자리의 고원 몸체(Plateau_<slot>/Model)에 이 재질 배열을 넣는다(모든 화면 같은 배치).
// 재질 순서 = 고원 메시(HOB_Plateau_Sky68)의 부분 메시 순서(RockDark·Grass·Rock·Moss·Foliage·Frosting·Seed·Glaze·Sprinkle1).
public class SubMapTheme : MonoBehaviour
{
    [SerializeField] private Material[] plateauMaterials = new Material[0];

    public Material[] PlateauMaterials => plateauMaterials;

    public void ApplyTo(Transform plateauModel)
    {
        if (plateauModel == null || plateauMaterials == null || plateauMaterials.Length == 0) return;
        foreach (Renderer r in plateauModel.GetComponentsInChildren<Renderer>(true))
        {
            Material[] cur = r.sharedMaterials;
            var next = new Material[cur.Length];
            for (int i = 0; i < cur.Length; i++) next[i] = i < plateauMaterials.Length && plateauMaterials[i] != null ? plateauMaterials[i] : cur[i];
            r.sharedMaterials = next;
        }
    }

#if UNITY_EDITOR
    public void EditorSetMaterials(Material[] mats) => plateauMaterials = mats ?? new Material[0];
#endif
}
