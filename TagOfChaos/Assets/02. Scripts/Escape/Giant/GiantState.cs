using System.IO;
using UnityEngine;

// 거대 괴물의 지금 일(Room Prop NetKeys.Giant, byte[] 약 100 B — Request1009Plan.md §64). 방장(GiantBrain)만 쓴다.
// 위치를 계속 보내지 않는다: 거대 괴물 NavMesh 길(점 MaxPoints개까지, 출발 시각)과 공통 시계로 모든 화면이 같은 위치를 계산한다(Sample).
// 건물은 돌아가고 작은 소품은 통과한다(§64.4). 속도는 GiantRules.Distance(걷기 시작부터 공통 시계)로 정해진다.
public struct GiantState
{
    private const byte Format = 2;
    public const int MaxPoints = 10;

    public Vector3 Spawn;       // 나오는 자리(마법진 가운데)
    public float SpawnYaw;
    public ushort Seq;          // 쓸 때마다 1씩(같은 밟기·층 이동을 두 번 처리하지 않게)

    // 이동 길
    public double MoveAt;       // 출발 시각(PhotonNetwork.Time)
    public Vector3[] Points;    // 첫 점 = 출발 위치. 하나뿐이면 서 있다
    public float Yaw;           // 다 가서(또는 서서) 바라보는 방향(도)

    public Vector3 Start => Points != null && Points.Length > 0 ? Points[0] : Spawn;
    public Vector3 End => Points != null && Points.Length > 0 ? Points[Points.Length - 1] : Spawn;
    public bool Moves => Points != null && Points.Length > 1;

    public float Length
    {
        get
        {
            float l = 0f;
            if (Points != null) for (int i = 1; i < Points.Length; i++) l += Vector3.Distance(Points[i - 1], Points[i]);
            return l;
        }
    }

    // 밟기(빠른 내리찍기)
    public double StompAt;      // 0 = 없음
    public Vector3 StompCenter;

    // 층 이동
    public double WarpAt;       // 0 = 없음
    public Vector3 WarpFrom, WarpTo;
    public float WarpYaw;

    public bool Stomping(double now) => StompAt > 0 && now >= StompAt && now < StompAt + GiantRules.StompSeconds;
    public bool Warping(double now) => WarpAt > 0 && now >= WarpAt && now < WarpAt + GiantRules.WarpSeconds;

    public struct Pose
    {
        public Vector3 Position;
        public float Yaw;
        public float Speed;     // 지금 걷는 속도(멈췄으면 0)
        public float Sink;      // 땅속으로 들어간 정도(0 = 땅 위, 1 = 다 들어감) — 층 이동
    }

    // 지금 자세(순수 계산). walkStart = 걷기 시작(마녀 걷기와 같다), timeAttackSeconds = 방장이 정한 마녀 시간.
    public Pose Sample(double now, double walkStart, int timeAttackSeconds)
    {
        var p = new Pose { Position = Start, Yaw = Yaw };
        if (WarpAt > 0 && now >= WarpAt)
        {
            double w = now - WarpAt;
            if (w < GiantRules.WarpSeconds)
            {
                bool sinking = w < GiantRules.WarpSinkSeconds;
                p.Position = sinking ? WarpFrom : WarpTo;
                p.Yaw = sinking ? Yaw : WarpYaw;
                p.Sink = sinking ? Sink01(w - 1.0) : 1f - Sink01(w - GiantRules.WarpSinkSeconds - 1.0);
                return p;
            }
        }

        p.Position = Start;
        if (!Moves || now <= MoveAt) return p;
        double t0 = System.Math.Max(MoveAt, walkStart);
        double travelled = now > t0 ? GiantRules.Distance(now - walkStart, timeAttackSeconds) - GiantRules.Distance(t0 - walkStart, timeAttackSeconds) : 0;
        for (int i = 1; i < Points.Length; i++)
        {
            Vector3 a = Points[i - 1], b = Points[i];
            float seg = Vector3.Distance(a, b);
            Vector3 flat = b - a;
            flat.y = 0f;
            if (flat.sqrMagnitude > 1e-4f) p.Yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            if (travelled <= seg)
            {
                p.Position = seg > 1e-4f ? Vector3.Lerp(a, b, (float)(travelled / seg)) : b;
                if (now >= walkStart) p.Speed = GiantRules.Speed(now - walkStart, timeAttackSeconds);
                return p;
            }
            travelled -= seg;
        }
        p.Position = End;
        p.Yaw = Yaw;
        return p;
    }

    private static float Sink01(double t) => t <= 0 ? 0f : t >= 1.5 ? 1f : Mathf.SmoothStep(0f, 1f, (float)(t / 1.5));

    public byte[] Encode()
    {
        using (var ms = new MemoryStream(200))
        using (var w = new BinaryWriter(ms))
        {
            w.Write(Format);
            Write(w, Spawn); w.Write(SpawnYaw); w.Write(Seq);
            w.Write(MoveAt); w.Write(Yaw);
            int n = Points != null ? Mathf.Min(Points.Length, MaxPoints) : 0;
            w.Write((byte)n);
            for (int i = 0; i < n; i++) Write(w, Points[i]);
            w.Write(StompAt); Write(w, StompCenter);
            w.Write(WarpAt); Write(w, WarpFrom); Write(w, WarpTo); w.Write(WarpYaw);
            return ms.ToArray();
        }
    }

    public static bool TryDecode(byte[] data, out GiantState s)
    {
        s = default;
        if (data == null || data.Length < 2 || data[0] != Format) return false;
        try
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                r.ReadByte();
                s.Spawn = Read(r); s.SpawnYaw = r.ReadSingle(); s.Seq = r.ReadUInt16();
                s.MoveAt = r.ReadDouble(); s.Yaw = r.ReadSingle();
                int n = r.ReadByte();
                s.Points = new Vector3[n];
                for (int i = 0; i < n; i++) s.Points[i] = Read(r);
                s.StompAt = r.ReadDouble(); s.StompCenter = Read(r);
                s.WarpAt = r.ReadDouble(); s.WarpFrom = Read(r); s.WarpTo = Read(r); s.WarpYaw = r.ReadSingle();
                return true;
            }
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    // 방 속성에서 읽는다(없으면 이번 판에는 거대 괴물이 없다).
    public static bool TryGetCurrent(out GiantState s)
    {
        s = default;
        return RoomState.TryGetBytes(NetKeys.Giant, out byte[] data) && TryDecode(data, out s);
    }

    private static void Write(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
    private static Vector3 Read(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
}
