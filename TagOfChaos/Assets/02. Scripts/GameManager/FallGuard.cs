using UnityEngine;

// 캐릭터(쿠키·괴물) 공용 낙하 최후 방어선 — VoidKillZone 배치를 놓친 맵에서도 일정 높이 아래로 떨어지면
// 스폰 지점으로 되돌린다(Bug-fix-plan.md §30.4). 예전에는 HideOrSeekPlayer.FixedUpdate에만 있어 괴물은 복귀하지 못했다.
// 같은 GameObject의 IRespawnable을 사용하므로 새 캐릭터 프리팹에도 이 컴포넌트만 붙이면 된다.
public class FallGuard : MonoBehaviour
{
    // 한 번 떨어질 때 VoidKillZone(캐릭터의 여러 콜라이더)과 이 방어선이 겹쳐 연달아 호출되지 않도록 한다.
    private const float RespawnCooldown = 0.2f;

    private IRespawnable respawnable;
    private float nextAllowedTime;
    private float fellAt = -1f;
    public const float FellConfirmSeconds = 3f;

    private bool ShouldBreakInsteadOfRespawn(IRespawnable target)
    {
        if (!(target is HideOrSeekPlayer cookie) || cookie.IsBroken) return false;
        EscapeManager m = EscapeManager.Instance;
        if (!EscapeManager.IsActive || m == null || m.State == null || !SkyFallRules.MapHasSkyFall(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)) return false;
        int actor = Photon.Pun.PhotonNetwork.LocalPlayer != null ? Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber : -1;
        return !RoomState.IsMonster(actor) && !m.State.HasLeftMap(actor) && m.State.GlideIndex(actor) < 0;
    }

    private void Awake()
    {
        respawnable = GetComponent<IRespawnable>();
        if (respawnable == null)
            Debug.LogWarning($"[FallGuard] No IRespawnable on {name}. Fall recovery is disabled.");
    }

    private void FixedUpdate()
    {
        if (respawnable == null || !respawnable.IsLocallyControlled) return;
        if (transform.position.y < GameSettings.Current.FallRespawnHeight) TryRespawn(respawnable);
    }

    // VoidKillZone도 이 경로를 쓴다 — 쿨다운을 캐릭터 단위로 공유한다.
    public static void TryRespawn(IRespawnable target)
    {
        if (target == null || !target.IsLocallyControlled) return;

        var component = target as Component;
        var guard = component != null ? component.GetComponent<FallGuard>() : null;
        if (guard != null)
        {
            if (Time.time < guard.nextAllowedTime) return;
            guard.nextAllowedTime = Time.time + RespawnCooldown;
        }
        // §91 Heaven Of Bakery: 쿠키·스파이가 허공으로 떨어지면 되돌리지 않고 부서진다(방장에게 알리고, 방장이 확정).
        // 방장이 끝내 확정하지 않으면(예: 판 밖·이미 나감) 잠시 뒤 예전처럼 되돌린다.
        if (guard != null && guard.ShouldBreakInsteadOfRespawn(target))
        {
            if (guard.fellAt < 0f) { guard.fellAt = Time.time; EscapeManager.Instance.Request(EscapeOp.FellFromSky); }
            if (Time.time - guard.fellAt < FellConfirmSeconds) return;
        }
        if (guard != null) guard.fellAt = -1f;
        target.RespawnToSpawnPoint();
        if (component != null && component.TryGetComponent(out IGameCharacter character)) GameAudio.PlayCharacter(SoundId.Respawn, character); // 본인 2D(S4)
    }
}
