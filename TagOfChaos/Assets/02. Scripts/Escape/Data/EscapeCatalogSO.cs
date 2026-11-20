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
    [Tooltip("술래 수색 폭탄의 범위·연출 수치(ToolSO, §68 GP5). 아이템으로 줍지 않는다.")]
    [SerializeField] private ItemSO searchBomb;

    [Header("Request1009Plan.md §90")]
    [Tooltip("§90 B3 술래 바주카포(ToolSO 수치 + 로켓 투사체). 아이템으로 줍지 않는다.")]
    [SerializeField] private ItemSO bazooka;
    [Tooltip("§90 B3 술래 어깨에 메는 바주카포 모델(원점 = 손잡이, 포구 +Z)")]
    [SerializeField] private GameObject bazookaModel;
    [Tooltip("§90 G6 바닥 껌 웅덩이(지름 약 2.9 m)")]
    [SerializeField] private GameObject gumPuddleModel;
    [Tooltip("§90 G6 술래 발에 붙는 껌 덩어리(고리, 지름 약 0.95 m)")]
    [SerializeField] private GameObject gumStuckModel;
    [Tooltip("§90 P5 펼친 패러글라이더(원점 = 쿠키 엉덩이)")]
    [SerializeField] private GameObject gliderModel;
    [Tooltip("§90 P5 패러글라이더를 펴는 동안 쿠키 동작(Glide_Sit 반복)")]
    [SerializeField] private AnimationClip glideClip;
    [Tooltip("§90 B3 술래 바주카 조준(반복)·발사(0.4초) 동작")]
    [SerializeField] private AnimationClip bazookaAimClip;
    [SerializeField] private AnimationClip bazookaFireClip;
    [Tooltip("§90 B3 바주카포를 술래 가슴(Chest) 뼈 기준으로 놓는 자리(조준 동작 첫 프레임에서 잰 값, P90AssetSetup)")]
    [SerializeField] private Vector3 bazookaChestPos;
    [SerializeField] private Quaternion bazookaChestRot = Quaternion.identity;
    [SerializeField] private Vector3 bazookaChestScale = Vector3.one;

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
    public ItemSO SearchBomb => searchBomb;
    public ItemSO Hammer => hammer;
    public ItemSO Bazooka => bazooka;
    public GameObject BazookaModel => bazookaModel;
    public GameObject GumPuddleModel => gumPuddleModel;
    public GameObject GumStuckModel => gumStuckModel;
    public GameObject GliderModel => gliderModel;
    public AnimationClip GlideClip => glideClip;
    public AnimationClip BazookaAimClip => bazookaAimClip;
    public AnimationClip BazookaFireClip => bazookaFireClip;
    public Vector3 BazookaChestPos => bazookaChestPos;
    public Quaternion BazookaChestRot => bazookaChestRot;
    public Vector3 BazookaChestScale => bazookaChestScale;
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

    // §90 P5 다른 맵 전용 아이템인지(이 레시피가 가진 전용 아이템이 아니고, 다른 레시피 하나가 전용으로 가짐).
    public bool IsExclusiveElsewhere(ItemSO item, EscapeRecipeSO here)
    {
        if (item == null) return false;
        if (here != null && here.OwnsExclusive(item)) return false;
        foreach (EscapeRecipeSO r in recipes) if (r != null && r != here && r.OwnsExclusive(item)) return true;
        return false;
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

    // §68: 새 아이템(섬광탄·권투 글러브는 쿠키 상자, 수색 폭탄은 술래 스킬 수치)을 목록에 더한다.
    public void EditorSetRequest1108(ItemSO[] allItems, ItemSO[] cookie, int[] cookieWeights, ItemSO search)
    {
        items = allItems;
        cookieItems = cookie;
        cookieItemWeights = cookieWeights;
        searchBomb = search;
        byId = null;
    }

    // §90: 새 아이템·모델·동작.
    public void EditorSetRequest1119(ItemSO[] allItems, ItemSO[] cookie, int[] cookieWeights, ItemSO bazookaItem, GameObject bazookaMesh, GameObject puddle, GameObject stuck,
        GameObject glider, AnimationClip glide, AnimationClip aim, AnimationClip fire)
    {
        items = allItems;
        cookieItems = cookie;
        cookieItemWeights = cookieWeights;
        bazooka = bazookaItem;
        bazookaModel = bazookaMesh;
        gumPuddleModel = puddle;
        gumStuckModel = stuck;
        gliderModel = glider;
        glideClip = glide;
        bazookaAimClip = aim;
        bazookaFireClip = fire;
        byId = null;
    }

    public void EditorSetBazookaSocket(Vector3 pos, Quaternion rot, Vector3 scale) { bazookaChestPos = pos; bazookaChestRot = rot; bazookaChestScale = scale; }

    public void EditorSetup(ItemSO[] allItems, EscapeRecipeSO[] allRecipes, ItemSO hammerItem)
    {
        items = allItems;
        recipes = allRecipes;
        hammer = hammerItem;
        byId = null;
    }
#endif
}
