using Photon.Pun;
using UnityEngine;

// 방장이 승인한 도구 명중(NetEventCodes.ToolHit) 처리. 모든 클라이언트가 효과를 보여주고(물풍선 물방울·터짐, 기절 별),
// 맞은 사람 본인만 자기 상태를 바꾼다: 기절, 손에 든 아이템 하나 떨어뜨리기(D20).
// 쓴 사람이 아닌 화면은 그 사람의 던지기·휘두르기 동작을 튼다(PlayerActionPose, §45.4). 물풍선은 방장이 정한 맞는 시각(hitAt)에
// 대상에게 닿고 그때 갇힌다(§45.7) — 물풍선 색칠은 없앴다(§45.8 Q2).
// 섬광탄(§68 GP2)도 같은 시각표로 날아가 터지고, 맞은 사람 본인 화면은 하얗게 덮인다. 권투 글러브(§68 GP3)는 방장이 센 연타로
// 멈칫(0.2초) 또는 쓰러짐(3초)을 정해 보낸다(d[6]). 술래 수색 폭탄(§68 GP5)은 기절 없이 대상(진짜 사람)만 기침한다.
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
        bool balloon = item.Tool.Kind == ToolKind.WaterBalloon;
        bool knock = d.Length > 6 && d[6] is bool k && k;
        if (!mine && user != null && user.gameObject.TryGetComponent(out PlayerActionPose pose))
        {
            if (item.Tool.IsThrown) pose.Play(PlayerActionPose.OneShot.Throw);
            else if (item.Tool.Kind == ToolKind.Hammer || item.Tool.Kind == ToolKind.BoxingGlove) pose.Play(PlayerActionPose.OneShot.Swing);
        }
        if (balloon && user != null)
        {
            Transform target = targets.Length > 0 && PhotonView.Find(targets[0]) != null ? PhotonView.Find(targets[0]).transform : null;
            ToolShotFx.ThrowBalloon(user.gameObject.transform, target, point, hitAt, () => Apply(item, targets, point, color, hitAt, knock));
            return;
        }
        if (item.Tool.Kind == ToolKind.Flashbang && user != null)
        {
            ToolShotFx.ThrowCanister(user.gameObject.transform, point, hitAt, () => Apply(item, targets, point, color, hitAt, knock));
            return;
        }
        if (item.Tool.Kind == ToolKind.StunGun && from.HasValue) ToolShotFx.Beam(from.Value, point, new Color(0.55f, 1f, 0.95f, 1f));
        Apply(item, targets, point, color, hitAt, knock);
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

    private static void Apply(ItemSO item, int[] targets, Vector3 point, int color, double hitAt, bool knock)
    {
        switch (item.Tool.Kind)
        {
            case ToolKind.SearchBomb:
                SearchBombFx.Burst(point, item.Tool.HitRadius);
                for (int i = 0; i < targets.Length; i++)
                {
                    PhotonView v = PhotonView.Find(targets[i]);
                    if (v != null) SearchBombFx.Cough(v.transform, 0.3f + i * 0.15f); // 기침(진짜 사람만 — 방장이 골랐다)
                }
                return;
            case ToolKind.Flashbang:
                FlashbangFx.Burst(point, item.Tool.HitRadius);
                GameAudio.PlayAt(SoundId.PortalFlash, point); // 섬광음(임시)
                break;
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
        bool balloon = item.Tool.Kind == ToolKind.WaterBalloon;
        bool glove = item.Tool.Kind == ToolKind.BoxingGlove;
        if ((!balloon || targets.Length == 0) && item.Tool.Kind != ToolKind.Flashbang && !(glove && targets.Length == 0)) ToolFx.Play(item.Tool.Kind, point, item.Tint);
        // 맞은 곳 소리(모두, 3D): 물풍선은 늘 터지고, 스턴건·뿅망치는 누군가 맞았을 때만(S5)
        switch (item.Tool.Kind)
        {
            case ToolKind.WaterBalloon: GameAudio.PlayAt(SoundId.BalloonSplash, point); break;
            case ToolKind.StunGun: if (targets.Length > 0) GameAudio.PlayAt(SoundId.StunHit, point); break;
            case ToolKind.Hammer: if (targets.Length > 0) GameAudio.PlayAt(SoundId.HammerBonk, point); break;
            case ToolKind.BoxingGlove: if (targets.Length > 0) GameAudio.PlayAt(knock ? SoundId.HammerBonk : SoundId.CookieLand, point); break;
        }

        foreach (int viewId in targets)
        {
            PhotonView view = PhotonView.Find(viewId);
            if (view == null) continue;
            IGameCharacter character = view.GetComponent<IGameCharacter>();
            if (character == null) continue;

            bool isMonster = character.Role == CharacterRole.Monster;
            if (isMonster && !item.Tool.CanHitMonster) continue; // 스턴건만 괴물을 맞힌다(§59 AT4 — 옛 클라이언트·경합 대비)
            float seconds = isMonster ? item.Tool.StunSecondsMonster : item.Tool.StunSecondsCookie;
            if (glove) seconds = knock ? GloveCombo.KnockSeconds : GloveCombo.FlinchSeconds; // 한 대 = 멈칫, 3초 안 3대 = 쓰러짐
            StunReceiver stun = view.GetComponent<StunReceiver>();
            if (stun != null)
            {
                if (balloon) stun.Trap(hitAt, seconds); // 5 m 공중의 물방울에 갇힘(§45.7)
                else stun.Stun(seconds, stars: !glove || knock); // 모든 화면에 별 이펙트(글러브 멈칫은 별 없이), 본인은 움직임도 막힌다
            }
            if (item.Tool.Kind == ToolKind.Flashbang && view.IsMine) FlashbangFx.Blind(seconds); // 맞은 사람 본인 화면이 하얗게

            if (!view.IsMine || isMonster) continue;
            if (glove && !knock) continue; // 멈칫만으로는 손에 든 것을 떨어뜨리지 않는다
            PlayerInventory inv = view.GetComponent<PlayerInventory>();
            if (inv != null) inv.RequestDropHeld(); // 손에 든 아이템 하나만(D20)
        }
    }
}
