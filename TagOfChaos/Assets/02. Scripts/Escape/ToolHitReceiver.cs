using Photon.Pun;
using UnityEngine;

// 방장이 승인한 도구 명중(NetEventCodes.ToolHit) 처리. 모든 클라이언트가 효과를 보여주고(물풍선 물방울·터짐, 기절 별),
// 맞은 사람 본인만 자기 상태를 바꾼다: 기절, 손에 든 아이템 하나 떨어뜨리기(D20).
// 쓴 사람이 아닌 화면은 그 사람의 던지기·휘두르기 동작을 튼다(PlayerActionPose, §45.4). 물풍선은 방장이 정한 맞는 시각(hitAt)에
// 대상에게 닿고 그때 갇힌다(§45.7) — 물풍선 색칠은 없앴다(§45.8 Q2).
public static class ToolHitReceiver
{
    public static void Handle(object data)
    {
        if (!(data is object[] d) || d.Length < 5 || !(d[0] is string itemId) || !(d[2] is int[] targets) || !(d[3] is Vector3 point)) return;
        int color = d[4] is int c ? c : 0;
        EscapeManager manager = EscapeManager.Instance;
        ItemSO item = manager != null ? manager.Catalog.Find(itemId) : null;
        if (item == null || !item.IsTool) return;

        // 던진·쏜 사람 손에서 날아가는 모습. 물풍선은 떨어진 순간에 명중 처리를 한다.
        int senderActor = d[1] is int sender ? sender : -1;
        double hitAt = d.Length > 5 && d[5] is double at ? at : PhotonNetwork.Time;
        IGameCharacter user = FindCharacter(senderActor);
        Vector3? from = HandOf(user);
        // 쓴 사람은 누르는 순간 이미 들었다(ToolUser.Use). 다른 사람은 그 손에서 3D로(S5).
        bool mine = PhotonNetwork.LocalPlayer != null && senderActor == PhotonNetwork.LocalPlayer.ActorNumber;
        if (!mine) GameAudio.PlayAt(ToolUser.UseSound(item.Tool.Kind), from ?? point);
        bool balloon = item.Tool.Kind == ToolKind.WaterBalloon || item.Tool.Kind == ToolKind.JailBalloon;
        if (!mine && user != null && user.gameObject.TryGetComponent(out PlayerActionPose pose))
        {
            if (balloon) pose.Play(PlayerActionPose.OneShot.Throw);
            else if (item.Tool.Kind == ToolKind.Hammer) pose.Play(PlayerActionPose.OneShot.Swing);
        }
        if (balloon && user != null)
        {
            Transform target = targets.Length > 0 && PhotonView.Find(targets[0]) != null ? PhotonView.Find(targets[0]).transform : null;
            ToolShotFx.ThrowBalloon(user.gameObject.transform, target, point, hitAt, () => Apply(item, targets, point, color, hitAt));
            return;
        }
        if (item.Tool.Kind == ToolKind.StunGun && from.HasValue) ToolShotFx.Beam(from.Value, point, new Color(0.55f, 1f, 0.95f, 1f));
        Apply(item, targets, point, color, hitAt);
    }

    private const float PumpkinDelay = 1f; // 호박마차: 연기 뒤 1초 만에 이동(§25.3 Q7)

    // 나에게 쓰는 아이템(스케이트·호박마차): 대상 목록의 쿠키에게(모든 화면 — 빠르기는 방장 이동 검사도 알아야 한다).
    private static void ApplySelf(int[] targets, System.Action<HideOrSeekPlayer> apply)
    {
        foreach (int viewId in targets)
        {
            PhotonView view = PhotonView.Find(viewId);
            if (view != null && view.TryGetComponent(out HideOrSeekPlayer cookie)) apply(cookie);
        }
    }

    private static IGameCharacter FindCharacter(int actor)
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (CharacterRegistry.IsAlive(c) && c.View != null && c.View.Owner != null && c.View.Owner.ActorNumber == actor) return c;
        return null;
    }

    private static Vector3? HandOf(IGameCharacter c)
    {
        if (c == null) return null;
        return ToolShotFx.HandPosition(c.gameObject.transform);
    }

    private static void Apply(ItemSO item, int[] targets, Vector3 point, int color, double hitAt)
    {
        switch (item.Tool.Kind)
        {
            case ToolKind.Skates: ApplySelf(targets, c => c.ApplySpeedBoost(item.Tool.SpeedMultiplier, item.Tool.EffectSeconds)); return;
            case ToolKind.Pumpkin:
                ApplySelf(targets, c =>
                {
                    ToolFx.Play(ToolKind.WaterBalloon, c.transform.position, item.Tint); // 출발 자리 연기(임시 연출)
                    ToolFx.Play(ToolKind.WaterBalloon, point, item.Tint);
                    GameAudio.PlayAt(SoundId.PortalFlash, point);
                    c.TeleportAfter(point, PumpkinDelay);
                });
                return;
            case ToolKind.Recorder: return; // 걷기·목소리는 상태(Decoys)로 모든 화면이 그린다
            case ToolKind.NoiseBomb:
                GameAudio.PlayAt(SoundId.StunHit, point);
                break;
        }
        bool balloon = item.Tool.Kind == ToolKind.WaterBalloon || item.Tool.Kind == ToolKind.JailBalloon;
        if (!balloon || targets.Length == 0) ToolFx.Play(item.Tool.Kind, point, item.Tint);
        // 맞은 곳 소리(모두, 3D): 물풍선은 늘 터지고, 스턴건·뿅망치는 누군가 맞았을 때만(S5)
        switch (item.Tool.Kind)
        {
            case ToolKind.WaterBalloon: GameAudio.PlayAt(SoundId.BalloonSplash, point); break;
            case ToolKind.StunGun: if (targets.Length > 0) GameAudio.PlayAt(SoundId.StunHit, point); break;
            case ToolKind.Hammer: if (targets.Length > 0) GameAudio.PlayAt(SoundId.HammerBonk, point); break;
        }

        foreach (int viewId in targets)
        {
            PhotonView view = PhotonView.Find(viewId);
            if (view == null) continue;
            IGameCharacter character = view.GetComponent<IGameCharacter>();
            if (character == null) continue;

            bool isMonster = character.Role == CharacterRole.Monster;
            float seconds = isMonster ? item.Tool.StunSecondsMonster : item.Tool.StunSecondsCookie;
            StunReceiver stun = view.GetComponent<StunReceiver>();
            if (stun != null)
            {
                if (balloon) stun.Trap(hitAt, seconds); // 5 m 공중의 물방울에 갇힘(§45.7)
                else stun.Stun(seconds); // 모든 화면에 별 이펙트, 본인은 움직임도 막힌다
            }

            if (!view.IsMine || isMonster) continue;
            PlayerInventory inv = view.GetComponent<PlayerInventory>();
            if (inv != null) inv.RequestDropHeld(); // 손에 든 아이템 하나만(D20)
        }
    }
}
