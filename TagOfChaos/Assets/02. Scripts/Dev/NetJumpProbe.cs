using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 순간이동 기록 도구(Request1009Plan.md §71.4.5 N0). 모든 화면에서 남의 캐릭터·AI가 한 프레임에 JumpMeters 넘게 움직이면
// (긴 프레임이면 그만큼 더 여유를 둔다) 그때 상황(들림·원격 돌진·마지막 패킷 뒤 시간·방장·핑)과 함께 로그를 남긴다 — 빌드에서는 Player.log에 남는다.
// 개발 빌드·에디터에서는 F9로 "핫스팟 흉내"(PUN 네트워크 시뮬레이션: 지연 150 ms·흔들림 50 ms·손실 2 %)를 켜고 끌 수 있다.
public class NetJumpProbe : MonoBehaviour
{
    public const float JumpMeters = 1.5f;
    public const float MaxLegitSpeed = 25f; // 돌진 포함 정상 이동의 상한(m/s) — 긴 프레임(창이 뒤에 있어 느려진 에디터 등)의 정상 이동을 순간이동으로 세지 않는다
    private const float DecoyRefreshSeconds = 1f;
    private const KeyCode SimulationKey = KeyCode.F9;

    private static NetJumpProbe instance;
    private readonly Dictionary<Transform, Vector3> last = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, float> firstSeen = new Dictionary<Transform, float>();
    private const float SpawnGrace = 2f; // 막 생긴 캐릭터는 첫 위치 패킷이 와서 생성 자리 → 실제 자리로 옮겨지므로 세지 않는다(대기실 입장 때 80 m대 오탐)
    private readonly List<Transform> drop = new List<Transform>();
    private DecoyView[] decoys = new DecoyView[0];
    private float nextDecoyRefresh;

    public static int JumpCount { get; private set; } // 시험용

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(NetJumpProbe));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<NetJumpProbe>();
    }

    private void Update()
    {
        if ((Application.isEditor || Debug.isDebugBuild) && Input.GetKeyDown(SimulationKey)) ToggleHotspotSimulation();
    }

    // 핫스팟 흉내 켜기/끄기(개발용). 켜져 있으면 끄고, 꺼져 있으면 지연 150 ms·흔들림 50 ms·손실 2 %.
    public static void ToggleHotspotSimulation() => SetSimulation(!PhotonNetwork.NetworkingClient.LoadBalancingPeer.IsSimulationEnabled, 150, 50, 2);

    public static void SetSimulation(bool on, int latencyMs, int jitterMs, int lossPercent)
    {
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        var s = peer.NetworkSimulationSettings;
        s.IncomingLag = s.OutgoingLag = latencyMs / 2;
        s.IncomingJitter = s.OutgoingJitter = jitterMs / 2;
        s.IncomingLossPercentage = s.OutgoingLossPercentage = lossPercent;
        peer.IsSimulationEnabled = on;
        Debug.LogWarning(on ? $"[NetSim] Hotspot simulation ON: {latencyMs} ms lag, {jitterMs} ms jitter, {lossPercent}% loss." : "[NetSim] Network simulation OFF.");
    }

    private void LateUpdate()
    {
        if (!PhotonNetwork.InRoom)
        {
            if (last.Count > 0) { last.Clear(); firstSeen.Clear(); }
            return;
        }
        IReadOnlyList<IGameCharacter> all = CharacterRegistry.All;
        for (int i = 0; i < all.Count; i++)
        {
            IGameCharacter c = all[i];
            if (c == null || c.View == null || c.View.IsMine || !CharacterRegistry.IsAlive(c)) continue;
            Check(c.gameObject.transform, $"{c.Role} actor {c.View.OwnerActorNr}", Describe(c));
        }
        if (Time.unscaledTime >= nextDecoyRefresh)
        {
            nextDecoyRefresh = Time.unscaledTime + DecoyRefreshSeconds;
            decoys = FindObjectsByType<DecoyView>(FindObjectsSortMode.None);
        }
        EscapeManager m = EscapeManager.Instance;
        foreach (DecoyView v in decoys)
            if (v != null && v.isActiveAndEnabled) Check(v.transform, $"AI {v.Id}", m != null ? $"route rev {m.Routes.Revision}" : "-");

        // 사라진 대상 정리
        drop.Clear();
        foreach (Transform t in last.Keys) if (t == null) drop.Add(t);
        for (int i = 0; i < drop.Count; i++) { last.Remove(drop[i]); firstSeen.Remove(drop[i]); }
    }

    private static string Describe(IGameCharacter c)
    {
        if (c is HideOrSeekPlayer cookie)
            return $"carrier {cookie.CurrentCarrierViewId}, carrying {cookie.IsCarryingSomeone}";
        if (c is MonsterController monster)
            return $"state {monster.CurrentMoveState}";
        return "-";
    }

    private void Check(Transform t, string who, string why)
    {
        if (!firstSeen.TryGetValue(t, out float born)) firstSeen[t] = born = Time.unscaledTime;
        if (last.TryGetValue(t, out Vector3 p) && Time.unscaledTime - born >= SpawnGrace)
        {
            float moved = Vector3.Distance(p, t.position);
            if (moved > JumpMeters + MaxLegitSpeed * Time.unscaledDeltaTime)
            {
                JumpCount++;
                Debug.LogWarning($"[NetJump] {who} jumped {moved:F1} m in one frame ({why}, frame {Time.unscaledDeltaTime * 1000f:F0} ms, " +
                                 $"master {PhotonNetwork.MasterClient?.ActorNumber}, ping {PhotonNetwork.GetPing()} ms)");
            }
        }
        last[t] = t.position;
    }
}
