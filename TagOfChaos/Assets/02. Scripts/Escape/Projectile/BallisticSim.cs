using UnityEngine;

// 던진 물체 하나의 상태(Request1009Plan.md §70.3.4).
public struct ProjectileState
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Age;            // 출발 뒤 지난 시간(초)
    public float FirstImpactAt;  // 처음 부딪힌 나이(-1 = 아직)
    public int Bounces;
    public bool Resting;

    public static ProjectileState Launch(Vector3 origin, Vector3 velocity) =>
        new ProjectileState { Position = origin, Velocity = velocity, FirstImpactAt = -1f };
}

// 중력·튕김·구름을 직접 계산하는 작은 시뮬레이션(§70.3.2 — Rigidbody 대신). 모든 화면·방장·미리보기가 같은 입력(시작점·속도·출발 시각)으로
// 같은 계산을 해 추가 동기화 없이 같은 궤적을 그린다. 벽·바닥은 Sweep(실행 때는 Physics.SphereCast)으로 찾는다. 고정 간격이라 프레임과 무관하다.
public static class BallisticSim
{
    public const float Step = 1f / 60f;
    public const float RollingNormalSpeed = 1.2f; // 면 방향 속도가 이보다 작으면 튕기지 않고 면을 따라 구른다
    public const float GroundNormalY = 0.6f;      // 이 기울기보다 완만한 면만 "바닥"(멈출 수 있다)
    public const float Skin = 0.003f;

    // 구를 from에서 direction으로 distance만큼 보냈을 때 처음 닿는 곳(그때 구 중심)·면 법선·닿은 충돌체. 없으면 false.
    public delegate bool Sweep(Vector3 from, Vector3 direction, float distance, float radius, out Vector3 centerAtHit, out Vector3 normal, out Collider collider);

    // 한 스텝 진행. 이번 스텝에 무언가에 닿았으면 그 충돌체(바닥·벽이면 그것, 시험에서는 null일 수 있음)와 true.
    public static bool Advance(ref ProjectileState s, ProjectileConfigSO c, Sweep world, out Collider touched)
    {
        touched = null;
        bool hit = false;
        if (!s.Resting)
        {
            s.Velocity += Vector3.down * (c.gravity * Step);
            Vector3 move = s.Velocity * Step;
            float distance = move.magnitude;
            if (distance > 1e-6f && world(s.Position, move / distance, distance, c.radius, out Vector3 at, out Vector3 normal, out Collider col))
            {
                hit = true;
                touched = col;
                s.Position = at + normal * Skin;
                if (s.FirstImpactAt < 0f) s.FirstImpactAt = s.Age;
                Vector3 vn = Vector3.Project(s.Velocity, normal);
                Vector3 vt = s.Velocity - vn;
                bool intoSurface = Vector3.Dot(s.Velocity, normal) < 0f;
                if (intoSurface && vn.magnitude > RollingNormalSpeed)
                {
                    // 튕김: 면 방향은 되돌리고, 면을 따라가는 속도는 조금 잃는다
                    s.Bounces++;
                    s.Velocity = vt * (1f - c.surfaceFriction) - vn * c.bounciness;
                }
                else
                {
                    // 구름: 면 방향 속도를 없애고 면을 따라 감속한다
                    float speed = vt.magnitude;
                    float slowed = Mathf.Max(0f, speed - c.rollDeceleration * Step);
                    s.Velocity = speed > 1e-5f ? vt * (slowed / speed) : Vector3.zero;
                }
                if (normal.y >= GroundNormalY && s.Velocity.magnitude < c.restSpeed)
                {
                    s.Velocity = Vector3.zero;
                    s.Resting = true;
                }
            }
            else s.Position += move;
        }
        s.Age += Step;
        return hit;
    }

    // 폭탄이 터질 때인지: 첫 충돌 뒤 fuseAfterImpact, 또는 아무것도 안 닿아도 maxLifeSeconds.
    public static bool FuseDone(in ProjectileState s, ProjectileConfigSO c) =>
        s.Age >= c.maxLifeSeconds || (s.FirstImpactAt >= 0f && s.Age - s.FirstImpactAt >= c.fuseAfterImpact);

    // 미리보기(§70.3.6): 지금 조준으로 던지면 지나갈 점들. firstImpact = 처음 닿은 점 번호(-1 = 없음), end = 멈추거나 터질 자리.
    // stopAtFirstImpact: 물풍선처럼 처음 닿으면 끝나는 것.
    public static int Predict(Vector3 origin, Vector3 velocity, ProjectileConfigSO c, Sweep world, Vector3[] points, bool stopAtFirstImpact,
        out int firstImpact, out Vector3 end, int stepsPerPoint = 3)
    {
        var s = ProjectileState.Launch(origin, velocity);
        int n = 0;
        firstImpact = -1;
        if (points.Length > 0) points[n++] = origin;
        int maxSteps = Mathf.CeilToInt(c.maxLifeSeconds / Step);
        for (int i = 1; i <= maxSteps; i++)
        {
            bool hit = Advance(ref s, c, world, out _);
            if (hit && firstImpact < 0)
            {
                if (n < points.Length) points[n++] = s.Position;
                firstImpact = n - 1;
                if (stopAtFirstImpact) break;
            }
            else if (i % stepsPerPoint == 0 && n < points.Length) points[n++] = s.Position;
            if (s.Resting || FuseDone(s, c) || n >= points.Length) break;
        }
        end = s.Position;
        if (n < points.Length && (n == 0 || points[n - 1] != end)) points[n++] = end;
        return n;
    }
}
