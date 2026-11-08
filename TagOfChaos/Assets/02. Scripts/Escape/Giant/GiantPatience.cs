using System.Collections.Generic;
using UnityEngine;

// 거대 괴물의 "10초 포기"(Request1009Plan.md §65·§66.4·§68). GiantBrain(방장)이 쓴다.
// 대상에게 갈 수도, 부술 것도 없이(부서지지 않는 곳에 숨음 — 시계탑 몸체·술래방 등) 목표점에서 GiveUpSeconds를 기다리면
// 그 사람을 IgnoreSeconds 동안 빼고 다른 사람을 쫓는다. 그 사람이 MovedDistance 넘게 움직이면 다시 쫓는다.
public sealed class GiantPatience
{
    public const float GiveUpSeconds = 10f;
    public const float IgnoreSeconds = 15f;
    public const float MovedDistance = 6f;

    private readonly Dictionary<int, (float until, Vector3 at)> ignored = new Dictionary<int, (float, Vector3)>();
    private int actor = -1;
    private float since = -1f;

    // 생각할 때마다: 지금 대상(actor)과 기다리는 중인지(waiting). 포기하면 true(그 사람은 빼 둔다).
    public bool Tick(int target, bool waiting, Vector3 targetPos, float now)
    {
        if (target != actor || !waiting)
        {
            actor = target;
            since = waiting ? now : -1f;
            return false;
        }
        if (since < 0f) since = now;
        if (now - since < GiveUpSeconds) return false;
        ignored[target] = (now + IgnoreSeconds, targetPos);
        actor = -1;
        since = -1f;
        return true;
    }

    // 빼 둔 사람인지(시간이 지났거나 움직였으면 다시 쫓는다)
    public bool IsIgnored(int target, Vector3 pos, float now)
    {
        if (!ignored.TryGetValue(target, out var v)) return false;
        Vector3 d = pos - v.at; d.y = 0f;
        if (now < v.until && d.magnitude <= MovedDistance) return true;
        ignored.Remove(target);
        return false;
    }

    public float WaitedSeconds(float now) => since < 0f ? 0f : now - since;
}
