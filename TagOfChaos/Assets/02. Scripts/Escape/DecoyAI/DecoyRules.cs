using UnityEngine;

// AI 판단의 순수 계산(Request1009Plan.md §44.4) — 시험(DecoyAiTests)으로 확인한다. NavMesh·씬을 모른다.
public static class DecoyRules
{
    public const float RunMultiplier = 1.3f;          // 진짜 쿠키 달리기(Shift)와 같다
    public const float FleeReleaseExtra = 5f;         // 도망 해제: 들리는 거리 + 5 m 밖에
    public const float FleeReleaseSeconds = 3f;       //   3초 머물면
    public const float SameFloorHeight = 4f;          // 이 높이 차 안이면 같은 층
    public const float ZigzagDistance = 6f;           // 이 안이면 1~2초마다 방향을 튼다
    public const float DanceChance = 0.03f;           // 한 행동을 끝낼 때마다 춤출 확률(§44.9 — 아주 가끔)
    public const int BuildingCapacity = 3;

    public enum Activity : byte { None, Wander, Rest, Visit, Hide, Flee, Dance }

    // AI마다 성격 — 씨앗으로 정해 방장이 바뀌어도 같다.
    public struct Personality
    {
        public float Fear;      // 도망 시작 거리 배율 0.8~1.2
        public bool Roams;      // 맵 전체를 다닌다(아니면 지금 자리 둘레 45 m)
        public int Wander, Visit, Hide, Rest; // 행동 가중치
    }

    public static Personality FromSeed(int seed)
    {
        uint h = Hash((uint)seed);
        return new Personality
        {
            Fear = 0.8f + 0.4f * Unit(h),
            Roams = Unit(Hash(h + 1)) < 0.4f,
            Wander = 3 + (int)(Unit(Hash(h + 2)) * 4f),
            Visit = 1 + (int)(Unit(Hash(h + 3)) * 3f),
            Hide = 1 + (int)(Unit(Hash(h + 4)) * 3f),
            Rest = 1 + (int)(Unit(Hash(h + 5)) * 2f),
        };
    }

    // 다음 행동(도망·춤 제외). 준비 시간에는 배회·쉬기만(§44.4).
    public static Activity PickActivity(Personality p, double roll, bool prepare, bool hasBuildings, bool hasHideSpots)
    {
        int visit = prepare || !hasBuildings ? 0 : p.Visit;
        int hide = prepare || !hasHideSpots ? 0 : p.Hide;
        int total = p.Wander + p.Rest + visit + hide;
        int pick = Mathf.Clamp((int)(roll * total), 0, total - 1);
        if ((pick -= p.Wander) < 0) return Activity.Wander;
        if ((pick -= p.Rest) < 0) return Activity.Rest;
        if ((pick -= visit) < 0) return Activity.Visit;
        return Activity.Hide;
    }

    public static bool RollDance(double roll) => roll < DanceChance;

    public static float FleeStartDistance(float audibleRadius, Personality p) => audibleRadius * p.Fear;

    // 술래가 도망 시작 거리 안이고 같은 층인지.
    public static bool ShouldFlee(Vector3 self, Vector3 monster, float startDistance)
    {
        if (Mathf.Abs(self.y - monster.y) > SameFloorHeight) return false;
        return Flat(self - monster).sqrMagnitude < startDistance * startDistance;
    }

    // 도망을 끝내도 되는지: 들리는 거리 + 5 m 밖에 3초 넘게 있었다(safeSince = 처음 밖으로 나간 시각, 안이면 음수).
    public static bool CanStopFleeing(double safeSince, double now) => safeSince >= 0 && now - safeSince >= FleeReleaseSeconds;

    public static bool IsSafe(Vector3 self, Vector3 monster, float audibleRadius) =>
        Mathf.Abs(self.y - monster.y) > SameFloorHeight || Flat(self - monster).magnitude > audibleRadius + FleeReleaseExtra;

    // 도망 지점 점수: 술래에게서 멀수록, 술래 반대쪽일수록, 트인 곳(openness 0~1)일수록, 다른 AI가 적을수록 좋다.
    public static float FleeScore(Vector3 self, Vector3 monster, Vector3 candidate, float openness, int crowd)
    {
        Vector3 away = Flat(self - monster);
        Vector3 go = Flat(candidate - self);
        float alignment = away.sqrMagnitude > 1e-4f && go.sqrMagnitude > 1e-4f ? Vector3.Dot(away.normalized, go.normalized) : 0f;
        return Flat(candidate - monster).magnitude + alignment * 8f + openness * 6f - crowd * 4f;
    }

    // 걷기·달리기 속도.
    public static float SpeedFor(DecoyGait gait, float walkSpeed) => gait == DecoyGait.Run ? walkSpeed * RunMultiplier : walkSpeed;

    public static double Range(double roll, double min, double max) => min + (max - min) * roll;

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static uint Hash(uint h)
    {
        unchecked
        {
            h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; h ^= h >> 16;
            return h;
        }
    }

    private static float Unit(uint h) => (h & 0xFFFFFF) / (float)0x1000000;
}
