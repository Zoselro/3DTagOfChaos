using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// HideOrSeekPlayer.prefab에 부착. bodyRenderer는 쿠키 몸 SkinnedMeshRenderer(GameRule.md §1.5).
// Awake()에서 적용한다 — 다른 컴포넌트의 Start가 몸 재질·모델을 읽기 전에 스킨이 반영돼 있도록(research.md §5.7).
// (몸 색칠 캔버스 PlayerPaintCanvas는 없앴다 — Request1009Plan.md §47 CL4.)
public class PlayerSkinApplier : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private SkinCatalogSO catalog; // 대기실 선택 버튼(PlayerSkinSelector)과 같은 스킨 목록(research.md §12 E4)

    private void Awake()
    {
        ApplySkin();
    }

    // 다른 클라이언트 관점에서 이 캐릭터가 스폰된 시점에 소유자의 SkinIndex가 아직 서버에
    // 반영되기 전이었을 가능성에 대한 방어 — 값이 늦게 도착하면 이 콜백이 재적용한다.
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.SkinIndex)) return;
        ApplySkin();
    }

    // 판 시작 때 방장이 캐릭터를 겹치지 않게 다시 정한다(Request1009Plan.md §25.5 B).
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed.ContainsKey(NetKeys.CharacterAssignments)) ApplySkin();
    }

    // 그 사람의 캐릭터 칸(판의 배정이 있으면 그것 — 겹치지 않음).
    public static int CharacterIndexOf(Player owner)
    {
        if (owner == null) return 0;
        int selected = RoomState.TryGetPlayerInt(owner, NetKeys.SkinIndex, out int v) ? v : CharacterAssignment.Unselected;
        return CharacterAssignment.Current(owner.ActorNumber, selected);
    }

    private void ApplySkin()
    {
        if (bodyRenderer == null || catalog == null || catalog.Count == 0 || pv.Owner == null) return;

        int index = CharacterIndexOf(pv.Owner);
        // 캐릭터 모델(§45 CH6): 모델이 있는 칸이면 몸을 통째로 바꾸고, 없는 칸(쿠키)이면 쿠키 몸 + 머티리얼.
        CharacterModelSwap.Apply(transform, catalog.GetModel(index));
        if (catalog.GetModel(index) != null) return;
        Material skin = catalog.GetMaterial(index);
        if (skin == null) return;
        bodyRenderer.sharedMaterial = skin;
    }
}
