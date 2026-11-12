using System.Collections.Generic;
using ExitGames.Client.Photon;
using UnityEngine;

// 모든 화면이 가진 AI 경로 모음(Request1009Plan.md §44.3·§44.6). Room Prop "DR_<id>"를 읽어 두고,
// 화면(DecoyView)·방장 판정(잡기 거리)·방장 판단(DecoyBrain)이 같은 경로로 AI 위치를 계산한다.
// 받은 경로는 점마다 맵 안·술래방 밖인지 확인하고, 벗어난 점이 있으면 그 앞에서 멈추게 자른다(조작된 값 대비).
public sealed class DecoyRouteBook
{
    private readonly Dictionary<int, DecoyRoute> routes = new Dictionary<int, DecoyRoute>();
    private readonly Dictionary<int, DecoyRoute> previous = new Dictionary<int, DecoyRoute>(); // 바뀌기 직전 경로(§70.2.5 D5)
    private readonly System.Func<Vector3, bool> isAllowed;

    // 경로가 바뀐 뒤 이만큼은 다른 화면이 아직 옛 경로로 그리고 있을 수 있다(방 속성이 도착하기 전, 초).
    public const float RouteSwitchGrace = 0.3f;

    public int Count => routes.Count;
    public int Revision { get; private set; } // 바뀔 때마다 오른다(화면이 다시 읽을지 판단)

    public DecoyRouteBook(System.Func<Vector3, bool> isAllowedPoint)
    {
        isAllowed = isAllowedPoint;
    }

    public static string KeyOf(int id) => NetKeys.DecoyRoutePrefix + id;

    public static bool TryParseKey(object key, out int id)
    {
        id = 0;
        return key is string s && s.StartsWith(NetKeys.DecoyRoutePrefix) && int.TryParse(s.Substring(NetKeys.DecoyRoutePrefix.Length), out id);
    }

    public bool TryGet(int id, out DecoyRoute route) => routes.TryGetValue(id, out route);

    // 방 속성 전체(입장·방장 교체) 또는 바뀐 것만(OnRoomPropertiesUpdate). 바뀐 경로가 있으면 true.
    public bool Read(Hashtable props, bool full)
    {
        if (full) { routes.Clear(); previous.Clear(); }
        bool changed = full;
        if (props == null) return changed;
        foreach (System.Collections.DictionaryEntry e in props)
        {
            if (!TryParseKey(e.Key, out int id)) continue;
            changed = true;
            if (e.Value is byte[] data && DecoyRoute.TryDecode(data, out DecoyRoute r)) Replace(id, Sanitize(r));
            else routes.Remove(id);
        }
        if (changed) Revision++;
        return changed;
    }

    // 방장이 쓴 값을 바로 반영한다(서버 응답 전에도 같은 위치로 판정하도록).
    public void SetLocal(int id, DecoyRoute route)
    {
        Replace(id, route);
        Revision++;
    }

    // 새 경로로 바꾸며 직전 경로를 남긴다. 같은 경로를 다시 받은 것(방장 자신의 쓰기가 돌아옴)은 직전 경로를 덮지 않는다.
    private void Replace(int id, DecoyRoute route)
    {
        if (routes.TryGetValue(id, out DecoyRoute old) && old.IsValid && System.Math.Abs(old.StartAt - route.StartAt) > 1e-3) previous[id] = old;
        routes[id] = route;
    }

    // [from, to] 동안 그 AI가 point에 가장 가까이 온 거리. 술래가 다가오면 AI는 도망 경로로 바뀌는데, 술래 화면은 새 경로가 도착하기 전까지
    // 옛 경로로 그린다 — 그래서 바뀐 직후(RouteSwitchGrace)까지는 직전 경로로도 잰다(§70.2.5 D5). 경로가 없으면 float.MaxValue.
    public float MinDistance(int id, Vector3 point, double from, double to)
    {
        if (!routes.TryGetValue(id, out DecoyRoute current)) return float.MaxValue;
        float best = DecoyRoute.MinDistance(current, point, from, to);
        if (previous.TryGetValue(id, out DecoyRoute prev) && from < current.StartAt + RouteSwitchGrace)
            best = Mathf.Min(best, DecoyRoute.MinDistance(prev, point, from, System.Math.Min(to, current.StartAt + RouteSwitchGrace)));
        return best;
    }

    public void RemoveLocal(int id)
    {
        previous.Remove(id);
        if (routes.Remove(id)) Revision++;
    }

    public void Clear()
    {
        routes.Clear();
        previous.Clear();
        Revision++;
    }

    // 경로가 있으면 경로 위치, 없으면 상태의 마지막 자리.
    public Vector3 PositionOf(EscapeState.Decoy d, double now) =>
        routes.TryGetValue(d.Id, out DecoyRoute r) ? DecoyRoute.Sample(r, now, d.Yaw).Position : d.Pos;

    public DecoyRoute.Pose PoseOf(EscapeState.Decoy d, double now)
    {
        if (routes.TryGetValue(d.Id, out DecoyRoute r)) return DecoyRoute.Sample(r, now, d.Yaw);
        return new DecoyRoute.Pose { Position = d.Pos, Yaw = d.Yaw };
    }

    private DecoyRoute Sanitize(DecoyRoute r)
    {
        if (isAllowed == null) return r;
        for (int i = 0; i < r.Points.Length; i++)
        {
            if (isAllowed(r.Points[i])) continue;
            if (i == 0) { r.Speed = 0f; r.Points = new[] { r.Points[0] }; return r; }
            var cut = new Vector3[i];
            System.Array.Copy(r.Points, cut, i);
            r.Points = cut;
            Debug.LogWarning($"[DecoyBrain] Route point {i} is outside the play area — the decoy stops before it.");
            return r;
        }
        return r;
    }
}
