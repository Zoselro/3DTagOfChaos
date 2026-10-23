using System.Collections.Generic;
using UnityEngine;

// 모든 아이템과 맵별 레시피 목록(Resources/Escape/EscapeCatalog). 네트워크로 받은 아이템 ID를 ItemSO로 바꿀 때 쓴다.
[CreateAssetMenu(menuName = "TagOfChaos/Escape/Catalog", fileName = "EscapeCatalog")]
public class EscapeCatalogSO : ScriptableObject
{
    public const string ResourcePath = "Escape/EscapeCatalog";

    [SerializeField] private ItemSO[] items = new ItemSO[0];
    [SerializeField] private EscapeRecipeSO[] recipes = new EscapeRecipeSO[0];
    [Tooltip("일반 상자에 넣는 뿅망치.")]
    [SerializeField] private ItemSO hammer;

    [Header("Request1009Plan.md §25")]
    [Tooltip("수리키트(스파이 만능 재료, 공구함과 같은 아이템).")]
    [SerializeField] private ItemSO repairKit;
    [Tooltip("스파이 상자 고정: 해체 도구.")]
    [SerializeField] private ItemSO dismantleTool;
    [Tooltip("스파이 상자 나머지 3개를 겹치지 않게 고르는 도구(스턴건·망치·색깔 물풍선·감옥 풍선).")]
    [SerializeField] private ItemSO[] spyRandomTools = new ItemSO[0];
    [Tooltip("재료가 없는 일반 상자에 들어가는 쿠키 아이템과 비율(스케이트 5 : 호박마차 3 : 녹음기 5, §25.3.2 S1).")]
    [SerializeField] private ItemSO[] cookieItems = new ItemSO[0];
    [SerializeField] private int[] cookieItemWeights = new int[0];
    [Tooltip("무지개 상자에서 나오는 헬리콥터 모자.")]
    [SerializeField] private ItemSO heliHat;
    [Tooltip("괴물 소음폭탄이 터질 때의 기절 수치(ToolSO).")]
    [SerializeField] private ItemSO noiseBomb;

    [Header("Shared models (EscapeVisualPlan.md §3)")]
    [Tooltip("탈것에 타는 순간·좌석에 앉은 쿠키 인형")]
    [SerializeField] private GameObject passengerModel;
    [Tooltip("괴물 승리 결과의 쿠키 유리병(3D)")]
    [SerializeField] private GameObject jarModel;
    [Tooltip("유리병 안에 쌓이는 작은 쿠키")]
    [SerializeField] private GameObject jarCookieModel;

    private Dictionary<string, ItemSO> byId;
    private static EscapeCatalogSO cached;

    public IReadOnlyList<ItemSO> Items => items;
    public ItemSO RepairKit => repairKit;
    public ItemSO DismantleTool => dismantleTool;
    public IReadOnlyList<ItemSO> SpyRandomTools => spyRandomTools;
    public IReadOnlyList<ItemSO> CookieItems => cookieItems;
    public IReadOnlyList<int> CookieItemWeights => cookieItemWeights;
    public ItemSO HeliHat => heliHat;
    public ItemSO NoiseBomb => noiseBomb;
    public ItemSO Hammer => hammer;
    public GameObject PassengerModel => passengerModel;
    public GameObject JarModel => jarModel;
    public GameObject JarCookieModel => jarCookieModel;

    public static EscapeCatalogSO Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<EscapeCatalogSO>(ResourcePath);
            return cached;
        }
    }

    public ItemSO Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (byId == null)
        {
            byId = new Dictionary<string, ItemSO>();
            foreach (ItemSO item in items)
                if (item != null && !string.IsNullOrEmpty(item.ItemId)) byId[item.ItemId] = item;
        }
        byId.TryGetValue(id, out ItemSO found);
        return found;
    }

    public EscapeRecipeSO RecipeFor(string mapName)
    {
        foreach (EscapeRecipeSO r in recipes)
            if (r != null && r.MapName == mapName) return r;
        return null;
    }

#if UNITY_EDITOR
    public void EditorSetupReform(ItemSO kit, ItemSO dismantle, ItemSO[] spyRandom, ItemSO[] cookie, int[] cookieWeights, ItemSO hat, ItemSO bomb)
    {
        repairKit = kit;
        dismantleTool = dismantle;
        spyRandomTools = spyRandom;
        cookieItems = cookie;
        cookieItemWeights = cookieWeights;
        heliHat = hat;
        noiseBomb = bomb;
    }

    public void EditorSetup(ItemSO[] allItems, EscapeRecipeSO[] allRecipes, ItemSO hammerItem)
    {
        items = allItems;
        recipes = allRecipes;
        hammer = hammerItem;
        byId = null;
    }
#endif
}
