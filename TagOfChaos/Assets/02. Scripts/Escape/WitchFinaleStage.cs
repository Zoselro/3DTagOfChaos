using System;
using System.Collections.Generic;
using UnityEngine;

// 도착 연출의 쿠키·괴물 대역(Request1009Plan.md §24.10 Y4 — 결정 Q1 C·Q2·Q3 a·Q4·Q6). 마녀(WitchPresenter)가 붙인다.
// - 도착하는 순간 각 화면이 한 번, 지금 살아 있는 실제 캐릭터를 "보이는 부분만" 복제한다: 꺼진 부모 아래에서 Instantiate해
//   스크립트가 깨어나지 않게 하고(Awake·OnEnable 없음 — PhotonView 등록·입력·카메라를 건드리지 않음) Transform·Animator·렌더러·MeshFilter만 남긴다.
//   칠한 색·스킨은 렌더러 재질을 그대로 쓰므로 함께 따라온다. 이름표는 지운다(Q4).
// - 누구를: 괴물 모두 + 쿠키(탈출·파괴되지 않은 쿠키·스파이). 액터 번호 순(Q2).
// - 어디에: 마녀 발끝이 닿는 Ground 경계에서 맵 안쪽(WitchPresenter.StagePoint, Q6). 괴물은 마녀를 등지고 가로로 서고 괴물 k가 쿠키 k를
//   촉수로 들어 올린 채(GrabKill 들어 올린 순간에서 멈춤, Q3 a), 남는 쿠키는 그 앞 반원에서 마녀를 올려다본다. 모든 화면이 같은 규칙이라 같은 장면.
// - 연출 동안 실제 쿠키·괴물은 매 프레임 숨긴다(카메라 앵글에 걸리지 않게 — 사용자 추가 결정). 마녀가 밟는 순간(Impacted) 대역이 부서진다.
public class WitchFinaleStage : MonoBehaviour
{
    public const float MonsterSpacing = 7f;          // 괴물 사이(m)
    public const float CookieRingRadius = 5f;        // 남는 쿠키가 서는 반원 반지름(m, 대역 자리에서 맵 안쪽)
    public const float CookieRingDegrees = 120f;     //   반원 폭
    public const float LiftedNormalizedTime = 0.45f; // GrabKill에서 쿠키를 들어 올린 순간(눌림 0.582 전)
    private const string SocketName = "Grab_Socket";
    private static readonly int GrabKillState = Animator.StringToHash("GrabKill");
    private static readonly int HeldState = Animator.StringToHash("Held");
    private static readonly int IdleState = Animator.StringToHash("Idle");

    // 실제 캐릭터를 숨기는 중(실제 캐릭터의 마녀 죽음 가루를 내지 않는다 — CookieLifeStatePresenter).
    public static bool Active { get; private set; }
    public const float DefaultStandHeight = 7f;
    // 대역 무리의 키(들린 쿠키까지, 대역 자리 땅에서) — 테이크 1 첫 화면이 이만큼 담는다.
    public float StandHeight { get; private set; } = DefaultStandHeight;

    public struct Member
    {
        public int Actor;
        public bool IsMonster;
        public int Index;   // 고르기 전 목록에서의 자리(어느 캐릭터인지)
    }

    // 대역 하나의 자리·자세(순수 계산 결과). HeldBy >= 0이면 그 번째 괴물이 들고 있다.
    public struct Placement
    {
        public int Actor;
        public int Index;
        public bool IsMonster;
        public Vector3 Position;
        public Quaternion Rotation;
        public int HeldBy;
    }

    private class Stand
    {
        public GameObject Clone;
        public Renderer[] Renderers;
        public Animator Animator;
        public bool IsMonster;
        public Transform Socket;        // 괴물: 쿠키를 드는 소켓
        public Stand Holder;            // 쿠키: 들고 있는 괴물
        public float BodyCenter;        // 쿠키: 루트에서 몸 중심 높이
        public Color DebrisColor;
        public GameObject BreakVfx;
        public int CrumbCount;
        public Color[] CrumbColors;
    }

    private readonly List<Stand> stands = new List<Stand>();
    private readonly Dictionary<IGameCharacter, Renderer[]> hiddenBodies = new Dictionary<IGameCharacter, Renderer[]>();
    private Transform root;
    private bool built, broken, measured;
    private int builtFrame;
    private WitchPresenter witch;

    private void OnEnable() => WitchPresenter.Impacted += OnImpact;

    private void OnDisable()
    {
        WitchPresenter.Impacted -= OnImpact;
        Active = false;
    }

    private void Awake() => witch = GetComponent<WitchPresenter>();

    private void Update()
    {
        bool finale = witch != null && witch.InFinale && witch.IsShown;
        Active = finale;
        if (!finale) return;
        if (!built) Build();
    }

    // 다른 스크립트(부서짐·스킨·관전 등)가 이번 프레임에 실제 캐릭터를 다시 켰어도 끄고, 들린 쿠키를 소켓에 맞춘다(괴물 동작 뒤).
    private void LateUpdate()
    {
        if (!Active) return;
        HideRealCharacters();
        foreach (Stand s in stands)
        {
            if (s.Holder == null || s.Holder.Socket == null || s.Clone == null || !s.Clone.activeSelf) continue;
            Transform socket = s.Holder.Socket;
            Vector3 toMonster = Vector3.ProjectOnPlane(s.Holder.Clone.transform.position - socket.position, Vector3.up);
            Quaternion facing = toMonster.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toMonster) : s.Clone.transform.rotation;
            s.Clone.transform.SetPositionAndRotation(socket.position - Vector3.up * s.BodyCenter, facing);
        }
        if (!measured && built && Time.frameCount > builtFrame) MeasureStandHeight(); // 동작이 한 번 적용된 뒤(들린 쿠키 포함)
    }

    private void MeasureStandHeight()
    {
        measured = true;
        float top = 0f;
        foreach (Stand st in stands)
            foreach (Renderer r in st.Renderers) if (r != null && r.gameObject.activeInHierarchy) top = Mathf.Max(top, r.bounds.max.y - witch.StagePoint.y);
        StandHeight = top > 0.5f ? top : DefaultStandHeight;
    }

    // ---------------- 고르기·배치(순수 계산) ----------------

    // 세울 사람(Q2): 괴물 모두와, 이번 내리침에 죽는 쿠키(탈출·파괴되지 않은). 액터 번호 순(같으면 괴물 먼저).
    public static List<Member> Choose(IList<(int actor, bool monster, bool escaped, bool broken)> people)
    {
        var list = new List<Member>();
        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (!p.monster && (p.escaped || p.broken)) continue;
            list.Add(new Member { Actor = p.actor, IsMonster = p.monster, Index = i });
        }
        list.Sort((a, b) => a.Actor != b.Actor ? a.Actor.CompareTo(b.Actor) : b.IsMonster.CompareTo(a.IsMonster));
        return list;
    }

    // 자리(Q3 a·Q6): 괴물은 stage를 중심으로 가로(MonsterSpacing 간격)에 서서 inward(맵 안쪽 = 카메라 쪽)를 본다. 괴물 k가 쿠키 k를 든다.
    // 남는 쿠키는 stage에서 inward로 CookieRingRadius 반원에 서서 마녀 쪽(−inward)을 본다. 높이는 stage와 같다(실제로는 땅에 맞춘다).
    public static List<Placement> Arrange(IList<Member> members, Vector3 stage, Vector3 inward)
    {
        inward = Vector3.ProjectOnPlane(inward, Vector3.up);
        inward = inward.sqrMagnitude > 1e-6f ? inward.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, inward);
        var monsters = new List<Member>();
        var cookies = new List<Member>();
        foreach (Member m in members) (m.IsMonster ? monsters : cookies).Add(m);

        var result = new List<Placement>();
        for (int i = 0; i < monsters.Count; i++)
        {
            float x = (i - (monsters.Count - 1) * 0.5f) * MonsterSpacing;
            result.Add(new Placement { Actor = monsters[i].Actor, Index = monsters[i].Index, IsMonster = true, Position = stage + side * x, Rotation = Quaternion.LookRotation(inward), HeldBy = -1 });
        }
        int held = Mathf.Min(monsters.Count, cookies.Count);
        for (int i = 0; i < held; i++)
            result.Add(new Placement { Actor = cookies[i].Actor, Index = cookies[i].Index, Position = result[i].Position, Rotation = Quaternion.LookRotation(-inward), HeldBy = i });
        int rest = cookies.Count - held;
        for (int j = 0; j < rest; j++)
        {
            float a = rest == 1 ? 0f : Mathf.Lerp(-CookieRingDegrees * 0.5f, CookieRingDegrees * 0.5f, j / (float)(rest - 1));
            Vector3 dir = Quaternion.AngleAxis(a, Vector3.up) * inward;
            result.Add(new Placement { Actor = cookies[held + j].Actor, Index = cookies[held + j].Index, Position = stage + dir * CookieRingRadius, Rotation = Quaternion.LookRotation(-inward), HeldBy = -1 });
        }
        return result;
    }

    private static readonly RaycastHit[] groundHits = new RaycastHit[16];

    // 그 자리 땅 높이(캐릭터·트리거·조각은 빼고, 위에서 아래로 가장 높은 면). 없으면 fallback.
    public static float GroundHeight(Vector3 at, float fallback)
    {
        int n = Physics.RaycastNonAlloc(new Vector3(at.x, at.y + 80f, at.z), Vector3.down, groundHits, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            if (groundHits[i].collider.GetComponentInParent<IGameCharacter>() != null) continue;
            best = Mathf.Max(best, groundHits[i].point.y);
        }
        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    // ---------------- 만들기 ----------------

    private void Build()
    {
        built = true;
        root = new GameObject("FinaleStage").transform;
        root.SetParent(transform, false);

        EscapeState state = EscapeManager.Instance != null ? EscapeManager.Instance.State : null;
        var characters = new List<IGameCharacter>();
        var people = new List<(int, bool, bool, bool)>();
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.View == null || c.View.Owner == null) continue;
            int actor = c.View.Owner.ActorNumber;
            characters.Add(c);
            bool monster = c.Role == CharacterRole.Monster;
            people.Add((actor, monster, state != null && state.HasEscaped(actor), !monster && RoomState.IsBroken(c.View.Owner)));
        }
        List<Placement> places = Arrange(Choose(people), witch.StagePoint, witch.TowardMap);

        var monsterStands = new List<Stand>();
        foreach (Placement p in places)
        {
            Stand stand = Clone(characters[p.Index], p.IsMonster);
            if (stand == null) continue;
            Vector3 pos = p.Position;
            pos.y = GroundHeight(pos, witch.StagePoint.y);
            stand.Clone.transform.SetPositionAndRotation(pos, p.Rotation);
            if (p.IsMonster)
            {
                monsterStands.Add(stand);
                if (stand.Animator != null)
                {
                    stand.Animator.Play(GrabKillState, 0, LiftedNormalizedTime);
                    stand.Animator.speed = 0f; // 들어 올린 순간에 멈춘다
                }
            }
            else
            {
                if (p.HeldBy >= 0 && p.HeldBy < monsterStands.Count) stand.Holder = monsterStands[p.HeldBy];
                if (stand.Animator != null) stand.Animator.Play(stand.Holder != null ? HeldState : IdleState, 0, 0f);
            }
            stands.Add(stand);
        }
        builtFrame = Time.frameCount;
        HideRealCharacters();
        Debug.Log($"[WitchFinaleStage] {stands.Count} stand-in(s) at {witch.StagePoint} ({monsterStands.Count} monster(s)).");
    }

    // 실제 캐릭터의 보이는 부분만 복제한다. 원본은 건드리지 않는다.
    private Stand Clone(IGameCharacter source, bool monster)
    {
        GameObject src = source.gameObject;
        var holder = new GameObject("CloneHolder");
        holder.SetActive(false);
        holder.transform.SetParent(root, false);
        GameObject copy = Instantiate(src, holder.transform);
        copy.name = src.name + "_Stand";

        // 렌더러별 속성 블록(스킨·표시 색)은 복제되지 않으므로 같은 순서로 옮긴다
        Renderer[] from = src.GetComponentsInChildren<Renderer>(true);
        Renderer[] to = copy.GetComponentsInChildren<Renderer>(true);
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < from.Length && i < to.Length; i++)
        {
            if (!from[i].HasPropertyBlock()) continue;
            from[i].GetPropertyBlock(block);
            to[i].SetPropertyBlock(block);
        }

        var stand = new Stand { IsMonster = monster };
        if (monster)
        {
            var controller = src.GetComponent<MonsterController>();
            if (controller != null && controller.GrabSocket != null) stand.Socket = copy.transform.Find(PathFrom(src.transform, controller.GrabSocket));
            if (stand.Socket == null) stand.Socket = EscapeCue.FindDeep(copy.transform, SocketName);
            stand.DebrisColor = MonsterEscapeState.MonsterDebrisColor;
        }
        else
        {
            var life = src.GetComponent<CookieLifeStatePresenter>();
            var capsule = src.GetComponent<CapsuleCollider>();
            stand.BodyCenter = capsule != null ? capsule.center.y * src.transform.lossyScale.y : 1f;
            stand.DebrisColor = life != null ? life.CrumbColor : new Color(0.55f, 0.30f, 0.12f);
            stand.BreakVfx = life != null ? life.BreakVfxPrefab : null;
            stand.CrumbCount = life != null ? life.CrumbCount : 12;
            var paint = src.GetComponent<PlayerPaintCanvas>();
            if (paint != null) paint.SampleSurfaceColors(colors => stand.CrumbColors = colors);
        }

        StripToVisuals(copy);
        copy.transform.SetParent(root, true);
        Destroy(holder);
        copy.SetActive(true);

        stand.Clone = copy;
        stand.Renderers = copy.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in stand.Renderers)
        {
            r.enabled = true; // 원본이 숨김(파괴·1인칭 그림자만)이어도 대역은 보인다
            r.forceRenderingOff = false;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
        }
        stand.Animator = copy.GetComponent<Animator>();
        if (stand.Animator != null)
        {
            stand.Animator.applyRootMotion = false;
            stand.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            stand.Animator.speed = 1f;
        }
        return stand;
    }

    private static string PathFrom(Transform root, Transform target)
    {
        var names = new List<string>();
        for (Transform t = target; t != null && t != root; t = t.parent) names.Insert(0, t.name);
        return string.Join("/", names);
    }

    private static readonly Dictionary<Type, Type[]> requiredCache = new Dictionary<Type, Type[]>();

    // Transform·Animator·Renderer·MeshFilter만 남긴다. 이름표(TextMeshPro)는 오브젝트째 지운다(Q4).
    // 다른 컴포넌트가 요구하는(RequireComponent) 것은 그쪽을 먼저 지운 뒤에 지운다 — 순서가 틀리면 Unity가 오류를 남긴다.
    public static void StripToVisuals(GameObject copy)
    {
        foreach (TMPro.TMP_Text text in copy.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if (text != null && text.gameObject != copy) DestroyImmediate(text.gameObject);

        var remove = new List<Component>();
        foreach (Component c in copy.GetComponentsInChildren<Component>(true))
            if (c != null && !Keep(c)) remove.Add(c);
        for (int guard = 0; remove.Count > 0 && guard < 16; guard++)
        {
            bool progressed = false;
            for (int i = remove.Count - 1; i >= 0; i--)
            {
                Component c = remove[i];
                if (c == null) { remove.RemoveAt(i); continue; }
                if (IsRequiredByOther(c, remove)) continue;
                DestroyImmediate(c);
                remove.RemoveAt(i);
                progressed = true;
            }
            if (!progressed) break;
        }
    }

    private static bool Keep(Component c) => c is Transform || c is Animator || c is Renderer || c is MeshFilter;

    private static bool IsRequiredByOther(Component target, List<Component> remaining)
    {
        Type t = target.GetType();
        foreach (Component other in remaining)
        {
            if (other == null || other == target || other.gameObject != target.gameObject) continue;
            foreach (Type need in Required(other.GetType()))
                if (need != null && need.IsAssignableFrom(t)) return true;
        }
        return false;
    }

    private static Type[] Required(Type type)
    {
        if (requiredCache.TryGetValue(type, out Type[] cached)) return cached;
        var list = new List<Type>();
        foreach (RequireComponent rc in type.GetCustomAttributes(typeof(RequireComponent), true))
        {
            list.Add(rc.m_Type0);
            list.Add(rc.m_Type1);
            list.Add(rc.m_Type2);
        }
        return requiredCache[type] = list.ToArray();
    }

    // ---------------- 실제 캐릭터 숨김 ----------------

    private void HideRealCharacters()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c)) continue;
            if (!hiddenBodies.TryGetValue(c, out Renderer[] body) || body == null)
                hiddenBodies[c] = body = c.gameObject.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in body) if (r != null && r.enabled) r.enabled = false;
        }
    }

    // ---------------- 부서짐 ----------------

    // 마녀가 밟는 순간: 대역이 부서진다(쿠키 = 몸 조각 + 가루, 괴물 = 몸 조각) 뒤 사라진다.
    private void OnImpact()
    {
        if (broken || !built) return;
        broken = true;
        foreach (Stand s in stands)
        {
            if (s.Clone == null) continue;
            WitchShatter.ShatterBody(s.Renderers, s.DebrisColor);
            if (!s.IsMonster) SpawnCrumbs(s);
            s.Clone.SetActive(false);
        }
    }

    private static void SpawnCrumbs(Stand s)
    {
        if (s.BreakVfx == null) return;
        Vector3 at = s.Clone.transform.position + Vector3.up * s.BodyCenter;
        GameObject burst = Instantiate(s.BreakVfx, at, s.Clone.transform.rotation);
        var particles = burst.GetComponent<ParticleSystem>();
        if (particles == null) return;
        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < s.CrumbCount; i++)
        {
            emit.startColor = s.CrumbColors != null && s.CrumbColors.Length > 0 ? s.CrumbColors[UnityEngine.Random.Range(0, s.CrumbColors.Length)] : s.DebrisColor;
            particles.Emit(emit, 1);
        }
    }
}
