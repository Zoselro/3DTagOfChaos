using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 부서지는 물체 상태 공유(Request1009Plan.md §66.3). EscapeManager.Start가 붙인다.
// - 방장: Hit()로 맞은 횟수를 올리고, 바뀐 것을 모아 FlushSeconds마다 Room Prop NetKeys.Destruction에 쓴다.
// - 모든 화면: 방 속성이 바뀌면 물체마다 올라간 단계를 LevelChanged로 알린다(연출은 DestructionPresenter).
//   처음 읽을 때(늦게 들어온 사람)는 연출 없이(animate = false) 최종 상태로 맞춘다.
public class DestructionNet : MonoBehaviour
{
    private const float FlushSeconds = 0.1f;
    private const float BoundsSeconds = 2f; // 서 있는 물체 경계 상자를 다시 재는 주기(건물 겉모습 — 룬 색 변형 — 은 판의 재료가 정해진 뒤 바뀐다)

    public static DestructionNet Instance { get; private set; }

    // (물체, 이전 단계, 새 단계, 연출할지)
    public event System.Action<DestructibleUnit, int, int, bool> LevelChanged;

    public DestructibleCatalog Catalog { get; private set; }

    private byte[] master;          // 방장이 쓰는 원본
    private bool dirty, wasMaster;
    private float nextFlush, nextBounds;
    private object lastSeen;        // 마지막으로 적용한 방 속성 값(참조가 바뀌면 새 값)
    private bool firstApply = true;

    private void Awake()
    {
        Instance = this;
        Catalog = DestructibleCatalog.Build(DestructibleCatalog.FindMap(gameObject.scene), DestructionRulesSO.Current);
        Debug.Log($"[Destruction] {gameObject.scene.name}: {Catalog.Count} destructibles.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int LevelOf(int id) => Catalog.Get(id)?.Level ?? 0;

    // 방장만: 물체 id를 hits번 맞힌다. 이미 부서졌으면 false.
    public bool Hit(int id, int hits = 1)
    {
        DestructibleUnit u = Catalog.Get(id);
        if (!PhotonNetwork.IsMasterClient || u == null || hits <= 0) return false;
        EnsureMaster();
        int level = DestructionState.Get(master, id);
        if (level >= u.HitsToBreak) return false;
        int next = Mathf.Min(u.HitsToBreak, level + hits);
        DestructionState.Set(master, id, next);
        dirty = true;
        Apply(u, next, true); // 방장 화면은 바로(보내는 것을 기다리지 않음)
        return true;
    }

    // 방장만: 맞은 횟수를 바로 끝까지(무너짐).
    public bool Break(int id)
    {
        DestructibleUnit u = Catalog.Get(id);
        return u != null && Hit(id, u.HitsToBreak);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextBounds)
        {
            nextBounds = Time.unscaledTime + BoundsSeconds;
            Catalog.RefreshBounds();
        }
        bool isMaster = PhotonNetwork.IsMasterClient;
        if (isMaster != wasMaster)
        {
            wasMaster = isMaster;
            master = null; // 방장이 바뀌면 방 속성에서 다시 읽는다
        }
        if (isMaster && dirty && Time.unscaledTime >= nextFlush)
        {
            nextFlush = Time.unscaledTime + FlushSeconds;
            dirty = false;
            AuthorityProps.SetRoom(new Hashtable { { NetKeys.Destruction, (byte[])master.Clone() } });
        }
        ReadRoom();
    }

    private void ReadRoom()
    {
        if (!RoomState.TryGetBytes(NetKeys.Destruction, out byte[] data))
        {
            if (lastSeen != null) ResetAll(); // 판이 끝나 지워졌다
            lastSeen = null;
            firstApply = false;
            return;
        }
        if (ReferenceEquals(data, lastSeen)) return;
        lastSeen = data;
        bool animate = !firstApply;
        firstApply = false;
        for (int i = 0; i < Catalog.Count; i++)
        {
            DestructibleUnit u = Catalog.Get(i);
            int level = Mathf.Min(DestructionState.Get(data, i), u.HitsToBreak);
            if (level > u.Level) Apply(u, level, animate);
        }
    }

    private void Apply(DestructibleUnit u, int level, bool animate)
    {
        int from = u.Level;
        if (level <= from) return;
        u.Level = level;
        if (LevelChanged != null) LevelChanged(u, from, level, animate);
        else if (u.Broken) SetBroken(u, true);
    }

    private void EnsureMaster()
    {
        if (master != null && master.Length == DestructionState.ByteLength(Catalog.Count)) return;
        RoomState.TryGetBytes(NetKeys.Destruction, out byte[] room);
        master = DestructionState.Resize(room, Catalog.Count);
    }

    private void ResetAll()
    {
        foreach (DestructibleUnit u in Catalog.Units)
        {
            if (u.Level == 0) continue;
            u.Level = 0;
            SetBroken(u, false);
        }
    }

    // 무너진 모습(연출 없이): 남기는 부분(재료 꽂는 곳 등)만 두고 숨기고 충돌을 끈다.
    public static void SetBroken(DestructibleUnit u, bool broken)
    {
        foreach (Renderer r in u.Renderers) if (r != null) r.enabled = !broken;
        foreach (Collider c in u.Colliders) if (c != null) c.enabled = !broken;
        foreach (Light l in u.Lights) if (l != null) l.enabled = !broken;
    }
}
