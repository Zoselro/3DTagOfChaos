using System;
using System.Collections.Generic;
using UnityEngine;

// 맵별 탈출 장치 연출의 바탕(EscapeVisualPlan.md §4). 시동·문 열림·케이크 부서짐·출발 같은 모든 연출은
// EscapeState의 CompletedAt(완성 시각)과 DepartedAt(출발 시각), 그리고 공통 시계(PhotonNetwork.Time)의 차이로만 계산한다.
// 그래서 모든 화면이 같은 모습이고, 늦게 들어온 사람도 바로 맞는 장면을 본다. 네트워크 메시지를 따로 보내지 않는다.
// 소리(SoundPlan.md S6): 하위 클래스가 Awake에서 시간표(AddCue — 완성·출발 시각 기준 몇 초)와 반복음(AddLoop)을 등록하면
// Step이 매 프레임 경계를 지나는 순간 한 번 낸다. 경계를 지난 지 CueLateTolerance가 넘었으면(늦게 들어옴 — 이미 몇 초·몇십 초 지난 상태를 받음) 내지 않는다.
public abstract class EscapeSequence : MonoBehaviour
{
    public enum CueClock { Completed, Departed }
    public const float CueLateTolerance = 1.5f; // 상태가 네트워크로 늦게 오거나 완성 순간 프레임이 끊겨도 0초 소리가 빠지지 않을 만큼

    private struct Cue
    {
        public SoundId Id;
        public CueClock Clock;
        public float At;
        public Transform Where;
    }

    private sealed class Loop
    {
        public SoundId Id;
        public Func<float, float, bool> Active; // (완성 뒤 초, 출발 뒤 초) → 울려야 하는지
        public Transform Where;
        public AudioHandle Handle;
        public float RetryAt;                   // 칸이 모자라 시작하지 못했으면 잠시 뒤 다시
    }

    private readonly List<Cue> cues = new List<Cue>();
    private readonly List<Loop> loops = new List<Loop>();
    private float lastCompleted = -1f, lastDeparted = -1f;
    private Transform boardPoint;

    // 완성된 뒤 이 시간이 지나야 탈 수 있다(연출이 탈 수 있는 단계에 도착할 때까지). 방장도 이 값으로 탑승을 승인한다.
    public abstract float BoardReadySeconds { get; }

    // 출발 연출 길이. 이 시간 동안은 게임 끝 판정을 미뤄 연출을 끝까지 보여준다.
    public abstract float DepartureSeconds { get; }

    // 쿠키가 타는 곳(모델의 "Board" 빈 오브젝트). 없으면 장치 중심.
    public Transform BoardPoint
    {
        get
        {
            if (boardPoint == null) boardPoint = FindDeep(transform, "Board") ?? transform;
            return boardPoint;
        }
    }

    // 탄 쿠키의 카메라가 기다리는 동안·출발할 때 따라 볼 곳(움직이는 탈것 본체, 오븐 입구, 포탈). 기본은 타는 곳.
    public virtual Transform DepartureFocus => BoardPoint;

    // 매 프레임(모든 클라이언트). state는 null일 수 있다(판 시작 전).
    public abstract void Tick(EscapeState state, double now);

    // 탈 때 나는 소리(EscapeAudioPresenter): 폴짝 뛰어 타는 탈것은 BoardHop, 빛 속으로 빨려 드는 오븐·포탈은 BoardSuck.
    public virtual SoundId BoardSound => SoundId.BoardHop;

    // EscapeDevice가 매 프레임 부른다: 모습(Tick) → 소리.
    public void Step(EscapeState state, double now)
    {
        Tick(state, now);
        float c = state != null ? Since(state.CompletedAt, now) : -1f;
        float d = state != null ? Since(state.DepartedAt, now) : -1f;
        foreach (Cue cue in cues)
        {
            bool departed = cue.Clock == CueClock.Departed;
            if (ShouldCue(cue.At, departed ? lastDeparted : lastCompleted, departed ? d : c))
                GameAudio.PlayOn(cue.Id, cue.Where != null ? cue.Where : transform);
        }
        foreach (Loop loop in loops)
        {
            bool want = loop.Active(c, d);
            if (want && !loop.Handle.IsPlaying && Time.unscaledTime >= loop.RetryAt)
            {
                loop.RetryAt = Time.unscaledTime + 1f;
                loop.Handle = GameAudio.StartLoop(loop.Id, loop.Where != null ? loop.Where : transform);
            }
            else if (!want && loop.Handle.IsPlaying) StopLoop(loop);
        }
        lastCompleted = c;
        lastDeparted = d;
    }

    // 시간표 소리를 지금 내야 하는지(순수 계산): 직전 프레임엔 at 전, 이번 프레임엔 at 이후, 그리고 지난 지 얼마 안 됨.
    public static bool ShouldCue(float at, float before, float now) => before < at && now >= at && now - at <= CueLateTolerance;

    protected void AddCue(SoundId id, CueClock clock, float at, Transform where = null) =>
        cues.Add(new Cue { Id = id, Clock = clock, At = at, Where = where });

    protected void AddLoop(SoundId id, Func<float, float, bool> active, Transform where = null) =>
        loops.Add(new Loop { Id = id, Active = active, Where = where });

    protected virtual void OnDisable()
    {
        foreach (Loop loop in loops) StopLoop(loop);
    }

    private static void StopLoop(Loop loop)
    {
        loop.Handle.Stop();
        loop.Handle = default;
    }

    // 쿠키 한 명이 탔을 때(모든 클라이언트). from = 그 쿠키가 서 있던 곳.
    public virtual void OnCookieBoarded(Vector3 from) => BoardingFx.Play(from, BoardPoint.position, BoardingFx.Style.Hop);

    // 기준 시각에서 지난 시간(초). 아직 그 일이 일어나지 않았으면 음수.
    protected static float Since(double at, double now) => at > 0 ? (float)(now - at) : -1f;

    // 이름으로 자식을 깊이 찾는다(움직이는 부품 아래에 있는 칸·좌석 자리도 찾게).
    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    protected static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
}
