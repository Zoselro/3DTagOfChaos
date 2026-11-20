using UnityEngine;

// §90 G6 껌 수류탄 규칙(순수 계산 — 시험). 던져 터진 자리 1.5 m(+ 몸 반지름) 안 술래 = 이동속도 50% 3.5초(몸에 붙은 껌이 3.5초 동안 줄어듦).
// 아무도 안 맞으면 바닥 웅덩이로 남는다(사라지지 않음, 사람당 최대 4개). 술래가 밟으면 같은 효과, 웅덩이는 3.5초 동안 줄며 사라진다.
// 마녀·거대 괴물은 대상이 아니다(캐릭터 목록에 없는 NPC).
public static class GumTrapRules
{
    public const float SlowSeconds = 3.5f;
    public const float SlowFactor = 0.5f;
    public const float HitRadius = 1.5f;     // 터진 자리에서 술래 몸 겉면까지
    public const float PuddleRadius = 1.2f;  // 바닥 웅덩이(지름 2.4 m)
    public const float StepHeight = 1.5f;    // 웅덩이와 발 높이 차가 이 안이면 밟음
    public const int MaxPuddlesPerOwner = 4;

    // 술래 이동속도 배율: 최근 껌(맞음·밟음)에서 SlowSeconds 안이면 SlowFactor.
    public static float SpeedFactor(EscapeState s, int monsterActor, double now) =>
        IsSlowed(s, monsterActor, now) ? SlowFactor : 1f;

    public static bool IsSlowed(EscapeState s, int monsterActor, double now) => LatestHit(s, monsterActor) is double at && now >= at && now < at + SlowSeconds;

    public static double? LatestHit(EscapeState s, int monsterActor)
    {
        if (s == null) return null;
        double best = -1;
        foreach (EscapeState.Slow x in s.Slows) if (x.Actor == monsterActor && x.At > best) best = x.At;
        return best >= 0 ? best : (double?)null;
    }

    // 몸에 붙은 껌 크기: 맞은 순간 1 → SlowSeconds 뒤 0.
    public static float StickAmount(double hitAt, double now) => Mathf.Clamp01(1f - (float)(now - hitAt) / SlowSeconds);

    // 웅덩이 크기: 밟히기 전 1, 밟힌 뒤 SlowSeconds 동안 0까지.
    public static float PuddleScale(EscapeState.Puddle p, double now) => p.Triggered ? StickAmount(p.TriggeredAt, now) : 1f;

    // 터진 자리에서 술래 몸(세운 캡슐: 발 ~ 키, 반지름 bodyRadius)까지 HitRadius 안인지.
    public static bool HitsBody(Vector3 burst, Vector3 feet, float height, float bodyRadius)
    {
        float y = Mathf.Clamp(burst.y, feet.y, feet.y + Mathf.Max(0f, height));
        Vector3 axis = new Vector3(feet.x, y, feet.z);
        return Vector3.Distance(burst, axis) <= HitRadius + bodyRadius;
    }

    // 술래 발이 웅덩이를 밟았는지(수평 거리 + 높이 차).
    public static bool Steps(Vector3 puddle, Vector3 feet, float bodyRadius)
    {
        if (Mathf.Abs(feet.y - puddle.y) > StepHeight) return false;
        Vector2 d = new Vector2(feet.x - puddle.x, feet.z - puddle.z);
        return d.magnitude <= PuddleRadius + bodyRadius * 0.5f;
    }

    // 새 웅덩이를 깔기 전에 지울 웅덩이(그 주인의 밟히지 않은 웅덩이가 이미 최대 수면 가장 오래된 것), 없으면 -1.
    public static int OldestToRemove(EscapeState s, int owner)
    {
        if (s == null) return -1;
        int count = 0, oldest = -1;
        for (int i = 0; i < s.Puddles.Count; i++)
        {
            EscapeState.Puddle p = s.Puddles[i];
            if (p.Owner != owner || p.Triggered) continue;
            count++;
            if (oldest < 0 || p.PlacedAt < s.Puddles[oldest].PlacedAt) oldest = i;
        }
        return count >= MaxPuddlesPerOwner ? oldest : -1;
    }
}
