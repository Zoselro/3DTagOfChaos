using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// 거대 괴물 연출(모든 화면, Request1009Plan.md §64.5·64.7). EscapeManager.Start가 붙인다. 위치·동작·마법진은 모두
// 방 속성(NetKeys.Giant = GiantState, 마녀 시각)과 공통 시계로 계산해 모든 화면이 같다.
// - 등장: 마법진(원 → 별, GiantRules.CircleSeconds) → 괴물이 마법진에서 솟아오르며 서서히 나타남(RevealSeconds) → 마법진 사라짐.
// - 걷기: GiantState 길. Walk 동작은 이동 속도에 맞춰 빨라진다(GiantRules.WalkAnimRate — 발이 미끄러지지 않게). 걸음마다 쿵 소리·흔들림,
//   걸어간 자리에 시럽 웅덩이. 모델의 제자리걸음용 Puddle 뼈는 숨긴다(실제로 움직이면 발밑에서 미끄러져 보였다).
// - 밟기: StompAt부터 Monster_Stomp(0.9초)을 그 시각에 맞춰 재생, 발이 닿는 순간 쿵·흔들림·먼지.
// - 층 이동: 마법진에서 가라앉고 다른 곳 마법진에서 솟아오른다. 마녀가 도착하면(걷기 끝) 0.5초 동안 사라진다.
// 동작은 Animator 컨트롤러 없이 PlayableGraph(Idle·Walk·Stomp 섞기)로 돌려 시각을 직접 정한다.
public class GiantPresenter : MonoBehaviourPunCallbacks
{
    private const float VanishSeconds = 0.5f;
    private const float WeightSpeed = 6f;        // 동작 섞기(초당)
    private const float TurnSpeed = 220f;        // 몸 돌리기(도/초)
    private const float StepInterval = 0.5f;     // Walk 동작 시간 기준 쿵 간격(초)
    private const float StepShakeNear = 0.12f, StepShakeRange = 30f, StepShakeSeconds = 0.25f;
    private const float StompShakeNear = 0.45f, StompShakeRange = 45f, StompShakeSeconds = 0.5f;
    private const float DripInterval = 0.7f;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static GiantPresenter Current { get; private set; }

    private EscapeManager manager;
    private GiantSettingsSO settings;
    private Transform root, model;
    private Renderer[] renderers = new Renderer[0];
    private Material[][] opaqueMaterials, fadeMaterials;
    private Color[][] baseColors;
    private bool fading;
    private readonly List<Transform> puddleBones = new List<Transform>();
    private Transform head;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable idlePlayable, walkPlayable, stompPlayable;
    private float idleTime, walkTime, wIdle = 1f, wWalk, wStomp;

    private GiantSummonCircle introCircle, warpFromCircle, warpToCircle;
    private GiantState state;
    private bool hasState;
    private float yaw;
    private bool placed;
    private double impactPlayedFor = -1;
    private float nextStepAt = StepInterval;
    private float nextDrip;

    public bool IsShown => root != null && root.gameObject.activeSelf;
    public Vector3 HeadPosition => head != null ? head.position : root != null ? root.position + Vector3.up * GiantRules.Height * 0.8f : Vector3.zero;

    public void Init(EscapeManager owner)
    {
        manager = owner;
        settings = GiantSettingsSO.Current;
        Current = this;
        ReadState();
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
        if (Current == this) Current = null;
        DestroyMaterials(fadeMaterials);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed.ContainsKey(NetKeys.Giant)) ReadState();
    }

    public override void OnJoinedRoom() => ReadState();

    private void ReadState() => hasState = GiantState.TryGetCurrent(out state);

    private void Update()
    {
        if (!hasState || !WitchTimeline.TryGetAppearAt(out double appearAt) || RoomState.TryGetInt(NetKeys.GameResult, out _))
        {
            HideAll();
            return;
        }
        if (root == null && !Build()) { hasState = false; return; }

        double now = PhotonNetwork.Time;
        double walkStart = WitchTimeline.WalkStart(appearAt);
        double walkEnd = WitchTimeline.WalkEnd(appearAt);
        UpdateIntroCircle(now, appearAt);
        UpdateWarpCircles(now);

        // 보이는 정도: 솟아오르며 나타남 → (마녀 도착) 사라짐
        float reveal = GiantRules.RevealProgress(now, appearAt);
        float vanish = now >= walkEnd ? Mathf.Clamp01((float)(now - walkEnd) / VanishSeconds) : 0f;
        if (reveal <= 0f || vanish >= 1f) { SetBodyVisible(false); return; }
        SetBodyVisible(true);

        GiantState.Pose pose = state.Sample(now, walkStart, RoomState.TimeAttackSeconds());
        float rise = (1f - Mathf.SmoothStep(0f, 1f, reveal)) * GiantRules.Height * 0.9f;
        float sink = pose.Sink * GiantRules.Height * 1.05f;
        root.position = pose.Position - Vector3.up * (rise + sink);
        yaw = placed ? Mathf.MoveTowardsAngle(yaw, pose.Yaw, TurnSpeed * Time.deltaTime) : pose.Yaw;
        placed = true;
        root.rotation = Quaternion.Euler(0f, yaw, 0f);
        SetAlpha(Mathf.Min(reveal, 1f - vanish, 1f - pose.Sink * 0.85f));

        Animate(now, pose);
        if (pose.Speed > 0f) Footsteps(pose);
        StompImpact(now);
    }

    // ---------------- 동작 ----------------

    private void Animate(double now, GiantState.Pose pose)
    {
        float dt = Time.deltaTime;
        double stompT = state.StompAt > 0 ? now - state.StompAt : -1;
        bool stomping = stompT >= 0 && stompT < GiantRules.StompSeconds;
        bool walking = !stomping && pose.Speed > 0f;
        if (stomping)
        {
            wStomp = 1f; wWalk = 0f; wIdle = 0f; // 빠른 내리찍기 — 섞지 않고 바로
            stompPlayable.SetTime(stompT);
        }
        else
        {
            wStomp = Mathf.MoveTowards(wStomp, 0f, WeightSpeed * 2f * dt);
            wWalk = Mathf.MoveTowards(wWalk, walking ? 1f : 0f, WeightSpeed * dt);
            wIdle = Mathf.Max(0f, 1f - wWalk - wStomp);
        }
        if (walking) walkTime += dt * GiantRules.WalkAnimRate(pose.Speed);
        idleTime += dt;
        walkPlayable.SetTime(Wrap(walkTime, settings.WalkClip));
        idlePlayable.SetTime(Wrap(idleTime, settings.IdleClip));
        mixer.SetInputWeight(0, wIdle);
        mixer.SetInputWeight(1, wWalk);
        mixer.SetInputWeight(2, wStomp);
    }

    private static double Wrap(float t, AnimationClip clip) => clip != null && clip.length > 0f ? t % clip.length : 0;

    private void LateUpdate()
    {
        foreach (Transform b in puddleBones) if (b != null) b.localScale = Vector3.zero; // 동작이 쓴 뒤에 숨긴다
    }

    private void Footsteps(GiantState.Pose pose)
    {
        if (walkTime >= nextStepAt)
        {
            nextStepAt = walkTime + StepInterval;
            GameAudio.PlayAt(SoundId.MonsterStep, root.position);
            Shake(root.position, StepShakeNear, StepShakeRange, StepShakeSeconds);
        }
        if (Time.time >= nextDrip && settings.SyrupColors != null && settings.SyrupColors.Length > 0)
        {
            nextDrip = Time.time + DripInterval;
            Vector2 r = Random.insideUnitCircle * 2.5f;
            GiantDeathFx.SyrupDrip(root.position + root.rotation * new Vector3(r.x, 0.2f, r.y), settings.SyrupColors[Random.Range(0, settings.SyrupColors.Length)]);
        }
    }

    private void StompImpact(double now)
    {
        if (state.StompAt <= 0 || state.StompAt == impactPlayedFor) return;
        double t = now - state.StompAt;
        if (t < GiantRules.StompImpactSeconds) return;
        impactPlayedFor = state.StompAt;
        if (t > GiantRules.StompImpactSeconds + 0.5) return; // 늦게 들어온 화면은 지난 밟기를 다시 보이지 않는다
        GameAudio.PlayAt(SoundId.WitchStep, state.StompCenter);
        Shake(state.StompCenter, StompShakeNear, StompShakeRange, StompShakeSeconds);
        ParticleSystem dust = EscapeVisuals.CreateParticles("GiantStompDust", null, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(0.7f, 1.1f), Speed = new Vector2(4f, 8f), Size = new Vector2(1.2f, 2.4f),
            Color = new Color(0.75f, 0.62f, 0.5f, 0.8f), MaxParticles = 30, EmissionOff = true,
            Shape = ParticleSystemShapeType.Circle, Radius = 1.5f, ShapeRotation = new Vector3(90f, 0f, 0f),
            GrowTo = 1.8f, FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 0.7f, Once = true,
        });
        dust.transform.position = state.StompCenter + Vector3.up * 0.3f;
        dust.Emit(24);
        Destroy(dust.gameObject, 2f);
    }

    private static void Shake(Vector3 at, float near, float range, float seconds)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float k = 1f - Mathf.Clamp01(Vector3.Distance(cam.transform.position, at) / range);
        if (k > 0f) CameraShake.Add(near * k, seconds);
    }

    // ---------------- 마법진 ----------------

    private void UpdateIntroCircle(double now, double appearAt)
    {
        if (introCircle == null) return;
        introCircle.transform.SetPositionAndRotation(state.Spawn, Quaternion.Euler(0f, state.SpawnYaw, 0f));
        float fade = 1f - Mathf.Clamp01((float)(now - GiantRules.RevealEnd(appearAt)) / GiantRules.CircleFadeSeconds);
        introCircle.Set(GiantRules.CircleRingProgress(now, appearAt), GiantRules.CircleStarProgress(now, appearAt), fade);
    }

    // 가라앉는 곳: 0~1초 그려짐, 가라앉은 뒤(2.5초) 1초 사라짐. 솟아오르는 곳: 2.5~3.5초 그려짐, 다 솟은 뒤(5초) 1초 사라짐.
    private void UpdateWarpCircles(double now)
    {
        if (warpFromCircle == null) return;
        double w = state.WarpAt > 0 ? now - state.WarpAt : -1;
        DrawWarp(warpFromCircle, state.WarpFrom, w, 0.0);
        DrawWarp(warpToCircle, state.WarpTo, w, GiantRules.WarpSinkSeconds);
    }

    private static void DrawWarp(GiantSummonCircle circle, Vector3 at, double w, double start)
    {
        double t = w - start;
        if (w < 0 || t < 0 || t > GiantRules.WarpSinkSeconds + 1.0) { circle.Set(0f, 0f, 0f); return; }
        circle.transform.position = at;
        float ring = Mathf.Clamp01((float)(t / 0.5)), star = Mathf.Clamp01((float)((t - 0.5) / 0.5));
        float alpha = 1f - Mathf.Clamp01((float)(t - GiantRules.WarpSinkSeconds));
        circle.Set(ring, star, alpha);
    }

    // ---------------- 모델 ----------------

    private bool Build()
    {
        if (settings.Model == null || settings.IdleClip == null || settings.WalkClip == null || settings.StompClip == null)
        {
            Debug.LogError("[Giant] GiantSettings is missing the model or clips.");
            return false;
        }
        float circleRadius = GiantRules.Height * 0.65f;
        introCircle = GiantSummonCircle.Create(transform, settings.CircleColor, settings.CircleGlow, circleRadius);
        warpFromCircle = GiantSummonCircle.Create(transform, settings.CircleColor, settings.CircleGlow, circleRadius);
        warpToCircle = GiantSummonCircle.Create(transform, settings.CircleColor, settings.CircleGlow, circleRadius);

        root = new GameObject("Giant").transform;
        root.SetParent(transform, false);
        model = Instantiate(settings.Model, root, false).transform;
        model.localScale = Vector3.one * GiantRules.Scale;
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Puddle_")) puddleBones.Add(t);
            else if (t.name == "Head") head = t;
        }

        renderers = model.GetComponentsInChildren<Renderer>(true);
        opaqueMaterials = new Material[renderers.Length][];
        fadeMaterials = new Material[renderers.Length][];
        baseColors = new Color[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true; // 5배로 커져 원래 경계 상자로는 잘려 보였다
            opaqueMaterials[i] = renderers[i].sharedMaterials;
            fadeMaterials[i] = new Material[opaqueMaterials[i].Length];
            baseColors[i] = new Color[opaqueMaterials[i].Length];
            for (int k = 0; k < opaqueMaterials[i].Length; k++)
            {
                Material src = opaqueMaterials[i][k];
                var m = src != null ? new Material(src) : EscapeVisuals.NewMaterial(Color.white, 0f, fade: true);
                EscapeVisuals.ApplyFade(m);
                m.SetInt("_ZWrite", 1); // 반투명한 동안에도 몸 뒤쪽이 앞을 뚫고 보이지 않게
                fadeMaterials[i][k] = m;
                baseColors[i][k] = m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
            }
        }

        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create("Giant");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, 3);
        idlePlayable = Clip(settings.IdleClip, 0);
        walkPlayable = Clip(settings.WalkClip, 1);
        stompPlayable = Clip(settings.StompClip, 2);
        mixer.SetInputWeight(0, 1f);
        AnimationPlayableOutput.Create(graph, "Giant", animator).SetSourcePlayable(mixer);
        graph.Play();
        root.gameObject.SetActive(false);
        return true;
    }

    private AnimationClipPlayable Clip(AnimationClip clip, int input)
    {
        AnimationClipPlayable p = AnimationClipPlayable.Create(graph, clip);
        p.SetSpeed(0); // 시각은 직접 정한다(공통 시계)
        p.SetApplyFootIK(false);
        graph.Connect(p, 0, mixer, input);
        return p;
    }

    private void SetBodyVisible(bool visible)
    {
        if (root != null && root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        if (!visible) placed = false;
    }

    private void HideAll()
    {
        SetBodyVisible(false);
        if (introCircle != null) introCircle.Set(0f, 0f, 0f);
        if (warpFromCircle != null) warpFromCircle.Set(0f, 0f, 0f);
        if (warpToCircle != null) warpToCircle.Set(0f, 0f, 0f);
    }

    // 1이면 원래(불투명) 재질, 그보다 작으면 Fade 재질로 알파를 맞춘다.
    private void SetAlpha(float alpha)
    {
        bool fade = alpha < 0.999f;
        if (fade != fading)
        {
            fading = fade;
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterials = fade ? fadeMaterials[i] : opaqueMaterials[i];
                renderers[i].shadowCastingMode = fade ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
        if (!fade) return;
        for (int i = 0; i < fadeMaterials.Length; i++)
            for (int k = 0; k < fadeMaterials[i].Length; k++)
            {
                Color c = baseColors[i][k];
                c.a *= Mathf.Clamp01(alpha);
                fadeMaterials[i][k].SetColor(ColorId, c);
            }
    }

    private static void DestroyMaterials(Material[][] sets)
    {
        if (sets == null) return;
        foreach (Material[] set in sets)
            foreach (Material m in set)
                if (m != null) Destroy(m);
    }
}
