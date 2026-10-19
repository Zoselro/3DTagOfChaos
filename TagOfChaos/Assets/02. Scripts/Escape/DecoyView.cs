using UnityEngine;

// AI 하나의 모습: 늘 제자리에 서 있다(걷기는 없앴다, §26 H12). 녹음기가 붙으면 주인의 녹음 목소리를 5~20초 간격으로 3D로 낸다(DecoyVoice).
// 괴물은 진짜 쿠키와 똑같이 조준(빨간 표시) + E로 잡는다(MonsterGrabKillTrigger) — 흰 E 안내는 뜨지 않는다(진짜와 구별되지 않게).
// 잡히면 그 괴물의 GrabKill 동작(2.63초)에 맞춰 진짜 쿠키와 같은 순서로 Grab_Socket에 붙어 눌리고(소리) → 가루가 터지고(쿠키 가루 VFX·소리)
// → 사라진다(CookieLifeStatePresenter와 같은 진행도·같은 VFX 프리팹). 방장이 잡는 시간 뒤 상태에서 지운다(§26 H6).
public class DecoyView : MonoBehaviour
{
    private const float VoiceCheckSeconds = 0.25f;
    private const float BodyCenterHeight = 0.95f;
    private const float RequestRetrySeconds = 1f;

    private static CookieLifeStatePresenter cookieLook; // 진짜 쿠키 프리팹의 가루 설정(VFX·개수·색)

    private EscapeManager manager;
    private int id;
    private AudioSource voice;
    private double playingStart = -1;
    private float nextVoiceCheck;
    private bool caught;
    private bool crushed;
    private bool squashPlayed;
    private bool crumbsSpawned;
    private bool grabStarted;
    private float requestedAt = -10f;
    private Vector3 originalScale;
    private Color crumbColor = new Color(0.55f, 0.30f, 0.12f);

    public int Id => id;
    // 괴물 조준에 잡히는지(아직 잡히지 않았고, 방금 잡기 요청을 보내지 않았다).
    public bool IsCatchable => !caught && EscapeManager.ActionsAllowed && Time.unscaledTime - requestedAt > RequestRetrySeconds;

    public void Init(EscapeManager owner, int decoyId)
    {
        manager = owner;
        id = decoyId;
        originalScale = transform.localScale;
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color")) { crumbColor = r.sharedMaterial.color; break; }
    }

    public void RequestCatch()
    {
        requestedAt = Time.unscaledTime;
        manager.Request(EscapeOp.DecoyCatch, id);
    }

    public void Apply(EscapeState.Decoy d)
    {
        if (caught) return;
        transform.position = manager.SnapToGround(d.Pos + Vector3.up);
        transform.rotation = Quaternion.Euler(0f, d.Yaw, 0f);
    }

    public void Tick(EscapeState.Decoy d, double now)
    {
        if (d.Caught) { TickCaught(d); return; }
        if (d.VoiceAt <= 0 || Time.unscaledTime < nextVoiceCheck) return;
        nextVoiceCheck = Time.unscaledTime + VoiceCheckSeconds;
        AudioClip clip = VoiceStore.ClipFor(d.Owner);
        if (clip == null) return;
        if (!DecoyVoice.TryCurrentPlay(d.VoiceAt, d.Seed, clip.length, now, out double start) || start == playingStart) return;
        float offset = (float)(now - start);
        if (offset >= clip.length) return; // 지난 재생은 건너뛴다(늦게 들어온 화면)
        playingStart = start;
        EnsureVoice();
        voice.clip = clip;
        voice.time = Mathf.Clamp(offset, 0f, clip.length - 0.01f);
        voice.Play();
    }

    private void EnsureVoice()
    {
        if (voice != null) return;
        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.maxDistance = 30f;
        voice.loop = false;
        voice.playOnAwake = false;
    }

    // 잡힘: 잡은 괴물의 손(Grab_Socket)을 따라가며 진짜 쿠키와 같은 진행도에 눌림 소리 → 가루 → 사라짐. 잡은 괴물 본인 화면은 동작을 시작한다.
    private void TickCaught(EscapeState.Decoy d)
    {
        if (!caught)
        {
            caught = true;
            if (voice != null) voice.Stop();
            foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false; // 괴물 손에 들린 동안 부딪히지 않는다
        }
        MonsterController monster = FindMonster(d.CaughtBy);
        if (monster == null || monster.GrabSocket == null) return;
        if (!grabStarted)
        {
            grabStarted = true;
            if (monster.IsLocallyControlled && !monster.IsGrabKilling) monster.PlayGrabKill(); // 동작 상태는 괴물 동기화로 모두에게 간다
        }
        if (crushed) return;
        float progress = monster.GrabKillProgress;
        if (progress >= monster.GrabKillCrushNormalizedTime) { Crush(monster); return; }
        if (!squashPlayed && progress >= monster.GrabKillSquashStart)
        {
            squashPlayed = true;
            GameAudio.PlayAt(SoundId.MonsterSquash, transform.position);
        }
        if (!crumbsSpawned && progress >= monster.GrabKillCrumbTime) SpawnCrumbs(monster);
        Vector3 scale = CookieLifeStatePresenter.CrushScale(monster, progress);
        Transform socket = monster.GrabSocket;
        Vector3 toMonster = Vector3.ProjectOnPlane(monster.transform.position - socket.position, Vector3.up);
        Quaternion facing = toMonster.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toMonster) : transform.rotation;
        transform.localScale = Vector3.Scale(originalScale, scale);
        transform.SetPositionAndRotation(socket.position - Vector3.up * (BodyCenterHeight * scale.y), facing);
    }

    private static MonsterController FindMonster(int actor)
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is MonsterController m && CharacterRegistry.IsAlive(c) && c.View != null && c.View.Owner != null && c.View.Owner.ActorNumber == actor) return m;
        return null;
    }

    // 진짜 쿠키(CookieLifeStatePresenter.SpawnCrumbs)와 같은 가루: 같은 VFX 프리팹을 괴물 손 위치에서 괴물 앞쪽으로 터뜨리고, 조각마다 몸 색.
    private void SpawnCrumbs(MonsterController monster)
    {
        crumbsSpawned = true;
        GameAudio.PlayAt(SoundId.CookieCrumble, transform.position);
        CookieLifeStatePresenter look = CookieLook();
        if (look == null || look.BreakVfxPrefab == null) return;
        Vector3 position = monster != null ? monster.GrabSocket.position : transform.position + Vector3.up * BodyCenterHeight;
        Vector3 forward = monster != null ? Vector3.ProjectOnPlane(monster.transform.forward, Vector3.up) : transform.forward;
        Quaternion rotation = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        GameObject burst = Instantiate(look.BreakVfxPrefab, position, rotation);
        var particles = burst.GetComponent<ParticleSystem>();
        if (particles == null) return;
        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < look.CrumbCount; i++)
        {
            emit.startColor = i % 3 == 0 ? look.CrumbColor : crumbColor;
            particles.Emit(emit, 1);
        }
    }

    private static CookieLifeStatePresenter CookieLook()
    {
        if (cookieLook == null)
        {
            var prefab = Resources.Load<GameObject>("HideOrSeekPlayer");
            if (prefab != null) cookieLook = prefab.GetComponentInChildren<CookieLifeStatePresenter>(true);
        }
        return cookieLook;
    }

    private void Crush(MonsterController monster)
    {
        crushed = true;
        if (!crumbsSpawned) SpawnCrumbs(monster);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
    }

    // 상태에서 지워졌을 때: 잡혀서 이미 부서졌으면 조용히, 아니면(마녀 시간·주인이 잡힘) 가루와 함께 사라진다.
    public void Vanish()
    {
        if (!crushed)
        {
            crushed = true;
            SpawnCrumbs(null);
        }
        Destroy(gameObject);
    }
}
