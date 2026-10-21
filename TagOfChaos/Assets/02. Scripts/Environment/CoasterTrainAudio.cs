using UnityEngine;

// 롤러코스터 편성 소리(Request1009Plan.md §31). 머리 칸을 따라가는 3D 반복음 두 개 —
// 달리는 소리(CoasterRoll, 속도에 따라 크기)와 체인/부스터 딸각(CoasterChain, 그 구간에서만) — 와 빠른 내리막 바람(CoasterWhoosh, 한 번).
// AmbientEmitter처럼 듣는 위치가 가까울 때만 켜고 멀어지면 멈춰 재생 칸을 돌려준다.
[RequireComponent(typeof(CoasterTrain))]
public sealed class CoasterTrainAudio : MonoBehaviour
{
    private const float CheckSeconds = 0.25f;
    private const float FullSpeed = 16f;      // 이 속도에서 달리는 소리가 가장 크다
    private const float QuietShare = 0.25f;   // 느릴 때(체인 오르막)도 이만큼은 난다
    private const float WhooshSpeed = 15f;    // 이 속도를 넘어서는 순간 바람 소리
    private const float FadePerSecond = 3f;

    [SerializeField] private Transform emitter; // 머리 칸

    private CoasterTrain train;
    private AudioHandle roll, chain;
    private float rollFade, chainFade, nextCheck;
    private bool near, wasFast;

    private void Awake() => train = GetComponent<CoasterTrain>();

    private void OnDisable()
    {
        roll.Stop(); chain.Stop();
        roll = default; chain = default;
        rollFade = chainFade = 0f;
        near = false;
    }

    private void Update()
    {
        if (emitter == null) return;
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + CheckSeconds;
            near = AmbientEmitter.ShouldPlay(near, Vector3.Distance(emitter.position, AudioListenerAnchor.Position), MaxDistance(SoundId.CoasterRoll));
        }
        float speed = train.Speed;
        float dt = Time.unscaledDeltaTime;
        rollFade = Mathf.MoveTowards(rollFade, near ? Mathf.Lerp(QuietShare, 1f, speed / FullSpeed) : 0f, FadePerSecond * dt);
        chainFade = Mathf.MoveTowards(chainFade, near && train.OnChain ? 1f : 0f, FadePerSecond * dt);
        Drive(ref roll, SoundId.CoasterRoll, rollFade);
        Drive(ref chain, SoundId.CoasterChain, chainFade);

        bool fast = speed > WhooshSpeed;
        if (fast && !wasFast && near) GameAudio.PlayOn(SoundId.CoasterWhoosh, emitter);
        wasFast = fast;
    }

    private void Drive(ref AudioHandle handle, SoundId id, float fade)
    {
        if (fade > 0.001f && !handle.IsPlaying) handle = GameAudio.StartLoop(id, emitter);
        if (!handle.IsPlaying) return;
        if (fade <= 0.001f) { handle.Stop(); handle = default; return; }
        GameAudio.SetLoopFade(handle, fade);
    }

    private static float MaxDistance(SoundId id) =>
        SoundCatalogSO.Current != null && SoundCatalogSO.Current.TryGet(id, out SoundCatalogSO.Entry e) ? e.maxDistance : 45f;

#if UNITY_EDITOR
    public void EditorSetup(Transform head) => emitter = head;
#endif
}
