using System;
using System.Collections.Generic;
using UnityEngine;

// 춤 목록(Request1009Plan.md §44.9). 춤이 늘어나도 코드를 고치지 않는다: 클립을 만들고 여기에 한 줄 더한 뒤
// 메뉴 Tools/Characters/Rebuild Dance States를 누르면 PlayerAnimator의 Dance 상태들이 다시 만들어진다.
// 네트워크로는 id(byte)만 보낸다 — id는 한 번 정하면 바꾸지 않는다.
[CreateAssetMenu(menuName = "TagOfChaos/Dance Catalog", fileName = "DanceCatalog")]
public class DanceCatalogSO : ScriptableObject
{
    public const string ResourcePath = "Dance/DanceCatalog";
    public const string StatePrefix = "Dance_";   // Animator 상태 이름 = Dance_<id>
    public const string IdParameter = "DanceId";  // int
    public const string Trigger = "Dance";        // PlayerMoveState.Dance 이름과 같다

    [Serializable]
    public class DanceEntry
    {
        public byte id;
        public string label;
        public AnimationClip clip;     // Humanoid(쿠키 뼈대) — 캐릭터 5종이 같이 쓴다
        public bool loop = true;
        [Min(0)] public int aiWeight = 1;   // 0이면 AI는 이 춤을 고르지 않는다
        public bool playerUsable = true;    // B키로 출 수 있는지
        public AudioClip music;             // 선택
        public Sprite icon;                 // 선택(나중 춤 고르기 화면)
    }

    [SerializeField] private List<DanceEntry> dances = new List<DanceEntry>();

    private static DanceCatalogSO cached;

    public static DanceCatalogSO Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<DanceCatalogSO>(ResourcePath);
            return cached;
        }
    }

    public IReadOnlyList<DanceEntry> Dances => dances;

    public DanceEntry Find(byte id)
    {
        foreach (DanceEntry d in dances) if (d != null && d.id == id) return d;
        return null;
    }

    public static string StateName(byte id) => StatePrefix + id;

    // AI: aiWeight로 고른다(roll = 0~1 균등값). 출 춤이 없으면 false.
    public bool TryPickForAi(double roll, out byte id) => TryPick(roll, false, out id);

    // 플레이어(B키): 출 수 있는 춤 중 하나를 고른 값(roll)으로. 나중에 고르기 화면이 생기면 이 함수 대신 그 선택을 쓴다.
    public bool TryPickForPlayer(double roll, out byte id) => TryPick(roll, true, out id);

    private bool TryPick(double roll, bool player, out byte id)
    {
        id = 0;
        int total = 0;
        foreach (DanceEntry d in dances) total += Weight(d, player);
        if (total <= 0) return false;
        int pick = Mathf.Clamp((int)(roll * total), 0, total - 1);
        foreach (DanceEntry d in dances)
        {
            pick -= Weight(d, player);
            if (pick < 0) { id = d.id; return true; }
        }
        return false;
    }

    private static int Weight(DanceEntry d, bool player)
    {
        if (d == null || d.clip == null) return 0;
        if (player) return d.playerUsable ? 1 : 0;
        return Mathf.Max(0, d.aiWeight);
    }

#if UNITY_EDITOR
    public void EditorSet(List<DanceEntry> entries) => dances = entries;
#endif
}
