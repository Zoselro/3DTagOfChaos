using System.IO;
using UnityEngine;

// AI 하나가 지금 하는 일(Request1009Plan.md §44.3). 방장의 DecoyBrain이 정해 Room Prop "DR_<id>"(byte[] 약 30~90 B)로 쓰고,
// 모든 화면은 같은 경로·시작 시각(PhotonNetwork.Time)으로 같은 위치를 계산한다(Sample) — 위치를 계속 보내지 않는다.
public enum DecoyGait : byte
{
    Idle = 0,   // 서 있기(마지막 점)
    Walk = 1,
    Run = 2,
    Hide = 3,   // 숨을 곳 안에서 가만히
    Repair = 4, // 건물 방 안에서 수리하는 척(서서 건물 쪽 바라보기)
    Dance = 5,  // 제자리 춤(DanceId = DanceCatalogSO 번호)
}

public struct DecoyRoute
{
    public const int MaxPoints = 12;
    private const byte Format = 2;       // 2: 달리기 끝 지점(RunUntil, half 2 B) 추가(§46.4). 1도 읽는다(끝까지 달림).
    private const byte FormatV1 = 1;
    private const float RunUntilLimit = 60000f; // half로 담을 수 있는 값
    private const float CoordScale = 100f; // 1 cm 단위
    private const float CoordLimit = 327f;  // short 범위(±327.67 m)

    public double StartAt;
    public DecoyGait Gait;
    public byte DanceId;
    public float Speed;      // m/s
    public float HoldUntil;  // StartAt 기준 초 — 이 뒤에는 방장이 다음 일을 정한다
    public float FaceYaw;    // 멈춘 뒤 바라볼 방향(도, NaN이면 마지막 구간 방향)
    public Vector3[] Points; // 첫 점 = 출발 위치
    public float RunUntil;   // Run 경로: 이 거리(m)까지 달리고 그 뒤는 걷는다(지침, §46.4). 0 이하면 끝까지 달린다. Run이 아니면 쓰지 않는다.

    public bool IsValid => Points != null && Points.Length > 0;
    public bool Moves => Gait == DecoyGait.Walk || Gait == DecoyGait.Run;

    public struct Pose
    {
        public Vector3 Position;
        public float Yaw;
        public bool Moving;
        public bool Running; // 달리는 구간(Run 경로의 RunUntil 전)
    }

    public bool RunsPartly => Gait == DecoyGait.Run && RunUntil > 0f && RunUntil < Length - 1e-3f;
    // 달리기가 끝난 뒤 걷는 속도.
    public float WalkSpeedAfterRun => Gait == DecoyGait.Run ? Speed / DecoyRules.RunMultiplier : Speed;
    // 달리는 시간(초). Run이 아니면 0.
    public float RunSeconds => Gait != DecoyGait.Run || Speed <= 0.01f ? 0f : (RunsPartly ? RunUntil : Length) / Speed;

    // 출발 뒤 elapsed초 동안 간 거리(m) — 달리기 끝 지점 뒤는 걷기 속도.
    public float DistanceAt(double elapsed)
    {
        float e = (float)System.Math.Max(0.0, elapsed);
        if (!RunsPartly) return e * Speed;
        float runSeconds = RunSeconds;
        return e <= runSeconds ? e * Speed : RunUntil + (e - runSeconds) * WalkSpeedAfterRun;
    }

    // 경로 전체 길이(m).
    public float Length
    {
        get
        {
            if (!IsValid) return 0f;
            float total = 0f;
            for (int i = 1; i < Points.Length; i++) total += Vector3.Distance(Points[i - 1], Points[i]);
            return total;
        }
    }

    // 다 걸어가는 데 걸리는 시간(초).
    public float TravelSeconds
    {
        get
        {
            if (Speed <= 0.01f) return 0f;
            float length = Length;
            if (!RunsPartly) return length / Speed;
            return RunUntil / Speed + (length - RunUntil) / Mathf.Max(0.01f, WalkSpeedAfterRun);
        }
    }

    public double EndsAt => StartAt + Mathf.Max(HoldUntil, TravelSeconds);

    // now 시각의 위치·방향(순수 계산). 시작 전이면 첫 점, 끝나면 마지막 점에 멈춘다.
    public static Pose Sample(DecoyRoute r, double now, float fallbackYaw = 0f)
    {
        var pose = new Pose { Yaw = fallbackYaw };
        if (!r.IsValid) return pose;
        pose.Position = r.Points[0];
        float lastYaw = fallbackYaw;
        if (r.Points.Length == 1 || r.Speed <= 0.01f)
        {
            pose.Yaw = float.IsNaN(r.FaceYaw) ? fallbackYaw : r.FaceYaw;
            return pose;
        }
        float distance = r.DistanceAt(now - r.StartAt);
        float travelled = distance;
        for (int i = 1; i < r.Points.Length; i++)
        {
            Vector3 a = r.Points[i - 1], b = r.Points[i];
            float seg = Vector3.Distance(a, b);
            Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
            if (flat.sqrMagnitude > 1e-4f) lastYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            if (distance <= seg && seg > 1e-4f)
            {
                pose.Position = Vector3.Lerp(a, b, distance / seg);
                pose.Yaw = lastYaw;
                pose.Moving = now >= r.StartAt;
                pose.Running = pose.Moving && r.Gait == DecoyGait.Run && (r.RunUntil <= 0f || travelled < r.RunUntil);
                return pose;
            }
            distance -= seg;
        }
        pose.Position = r.Points[r.Points.Length - 1];
        pose.Yaw = float.IsNaN(r.FaceYaw) ? lastYaw : r.FaceYaw;
        return pose;
    }

    // [from, to] 동안 경로 위의 AI가 point에 가장 가까이 온 거리(0.05초 간격으로 표본, §46.2 GK4).
    public static float MinDistance(DecoyRoute r, Vector3 point, double from, double to)
    {
        if (!r.IsValid) return float.MaxValue;
        float best = float.MaxValue;
        const double Step = 0.05;
        for (double t = from; ; t += Step)
        {
            if (t > to) t = to;
            best = Mathf.Min(best, Vector3.Distance(Sample(r, t).Position, point));
            if (t >= to) break;
        }
        return best;
    }

    public static Vector3 Quantize(Vector3 p) =>
        new Vector3(Mathf.Round(Mathf.Clamp(p.x, -CoordLimit, CoordLimit) * CoordScale) / CoordScale,
                    Mathf.Round(Mathf.Clamp(p.y, -CoordLimit, CoordLimit) * CoordScale) / CoordScale,
                    Mathf.Round(Mathf.Clamp(p.z, -CoordLimit, CoordLimit) * CoordScale) / CoordScale);

    public static byte[] Encode(DecoyRoute r)
    {
        int n = r.Points != null ? Mathf.Min(r.Points.Length, MaxPoints) : 0;
        using (var ms = new MemoryStream(24 + n * 6))
        using (var w = new BinaryWriter(ms))
        {
            w.Write(Format);
            w.Write(r.StartAt);
            w.Write((byte)r.Gait);
            w.Write(r.DanceId);
            w.Write(Mathf.FloatToHalf(r.Speed));
            w.Write(Mathf.FloatToHalf(r.HoldUntil));
            w.Write(float.IsNaN(r.FaceYaw) ? short.MinValue : (short)Mathf.RoundToInt(Mathf.Repeat(r.FaceYaw, 360f) * 10f));
            w.Write(Mathf.FloatToHalf(r.Gait == DecoyGait.Run ? Mathf.Clamp(r.RunUntil, 0f, RunUntilLimit) : 0f));
            w.Write((byte)n);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = r.Points[i];
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(p.x, -CoordLimit, CoordLimit) * CoordScale));
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(p.y, -CoordLimit, CoordLimit) * CoordScale));
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(p.z, -CoordLimit, CoordLimit) * CoordScale));
            }
            return ms.ToArray();
        }
    }

    public static bool TryDecode(byte[] data, out DecoyRoute r)
    {
        r = default;
        if (data == null || data.Length < 18 || (data[0] != Format && data[0] != FormatV1)) return false;
        try
        {
            using (var ms = new MemoryStream(data))
            using (var rd = new BinaryReader(ms))
            {
                byte format = rd.ReadByte();
                r.StartAt = rd.ReadDouble();
                r.Gait = (DecoyGait)rd.ReadByte();
                r.DanceId = rd.ReadByte();
                r.Speed = Mathf.Clamp(Mathf.HalfToFloat(rd.ReadUInt16()), 0f, 60f);
                r.HoldUntil = Mathf.Clamp(Mathf.HalfToFloat(rd.ReadUInt16()), 0f, 600f);
                short yaw = rd.ReadInt16();
                r.FaceYaw = yaw == short.MinValue ? float.NaN : yaw / 10f;
                r.RunUntil = format >= Format ? Mathf.Clamp(Mathf.HalfToFloat(rd.ReadUInt16()), 0f, RunUntilLimit) : 0f;
                int n = Mathf.Min(rd.ReadByte(), MaxPoints);
                if (n == 0 || ms.Length - ms.Position < n * 6) return false;
                r.Points = new Vector3[n];
                for (int i = 0; i < n; i++)
                    r.Points[i] = new Vector3(rd.ReadInt16() / CoordScale, rd.ReadInt16() / CoordScale, rd.ReadInt16() / CoordScale);
                if (float.IsNaN(r.Speed) || double.IsNaN(r.StartAt)) return false;
                return true;
            }
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }
}
