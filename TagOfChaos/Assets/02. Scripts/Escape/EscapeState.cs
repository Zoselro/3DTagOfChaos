using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 아이템이 지금 어디 있는지(EscapePlan.md §5.2).
public enum ItemLocation : byte
{
    Chest,  // A = 상자 번호
    Held,   // A = 액터 번호, B = 인벤토리 칸(0~3)
    Ground, // Pos = 바닥 위치
    Device, // A = 건물 번호(0~3) — 건물에 넣은 재료(Request1009Plan.md §25.5 E)
    Gone,   // 없어짐(도구 횟수 소진·모자를 씀 등)
}

// 무지개 상자 상태(§25.5 H).
public enum RainbowState : byte { None = 0, Closed = 1, Opened = 2, Broken = 3 }

// 탈출 모드 한 판의 상태. 방장이 가진 원본을 Room Prop(NetKeys.EscapeState, byte[])로 통째로 쓰고, 모두가 읽는다.
// 문(DoorStates)과 같은 "방장만 쓴다" 방식이다. 재료·상자·AI·폭탄에는 PhotonView가 없다(최적화, §5.2).
// v5(Request1009Plan.md §25): 탈출 장치 1개 → 사분면 건물 4곳(수리 게이지), 탈것 → 동서남북 탈출구 4개(그중 2개 열림),
// 스파이 로켓 자리, 무지개 상자·헬리콥터 모자 비행, 괴물 소음폭탄, AI(미끼) 추가.
// v6(§26): 로켓 칸의 수리키트 50%(KitHalf), 괴물에게 잡히는 중인 AI(CaughtBy·CaughtAt).
// v7(§68 GP5): 소음폭탄 → 사이렌 함정 — 갇힌 건물(Building)·갇힌 시각(CagedAt)·연타 수(Mash).
// v8(§68.7): 호박 마차 탈것(Carriages) — 소환 자리·방향·시각, 탄 시각, 도착 지점.
// v9(§68.9): 콜라 대포 탈것(Cannons) — 설치 자리·방향·시각, 탄 시각·겨눈 방향, 착지 지점·비행 시간.
// v10(§69 E3): 대포 비행 천장(Ceiling — 지하처럼 위가 막힌 곳에서 포물선이 천장을 넘지 않게).
public sealed class EscapeState
{
    public const byte FormatVersion = 10;
    public const int BuildingCount = 4;
    public const int ExitCount = 4;
    public const float RepairSeconds = 10f;      // 재료 넣기(수리) 게이지가 다 차는 시간(§25.5 E)
    public const float RepairDecayPerSecond = 0.01f; // 멈추면 초당 1%씩 줄어든다(10초 게이지가 0이 되려면 100초 — "매우 천천히")
    public const float DismantleSeconds = 20f;   // 스파이 해체 도구(§25.3 Q8)

    public struct Item
    {
        public string Id;
        public ItemLocation Loc;
        public int A;
        public int B;
        public Vector3 Pos;
        public int Charges;
    }

    public struct Chest
    {
        public int Anchor;  // 씬의 CHEST_Slot 번호
        public bool Spy;
        public bool Opened;
    }

    // 사분면 건물 하나. 재료는 판마다 섞어 정한다(§25 결정).
    public struct Building
    {
        public string MaterialId;
        public bool Active;      // 이번 판에 수리해야 하는 건물(쓰지 않는 건물은 처음부터 가동)
        public int ItemIndex;    // 넣은 재료(-1 = 아직)
        public float Progress;   // ProgressAt 시각의 게이지(0~1)
        public double ProgressAt;
        public int Repairer;     // 지금 수리 중인 사람(0 = 없음) — 수리 중이면 차오르고, 아니면 줄어든다

        public bool Repaired => !Active || ItemIndex >= 0;

        // 지금 시각의 게이지(순수 계산): 수리 중이면 RepairSeconds에 걸쳐 차오르고, 아니면 천천히 줄어든다.
        public float ProgressAtTime(double now)
        {
            if (Repaired) return 1f;
            float dt = (float)System.Math.Max(0, now - ProgressAt);
            float value = Repairer != 0 ? Progress + dt / RepairSeconds : Progress - dt * RepairDecayPerSecond;
            return Mathf.Clamp01(value);
        }
    }

    // 스파이 로켓 칸(§25.5 J, §26.3.3): 맵별로 정한 재료(Accepts 중 하나). 수리키트는 칸을 50%씩 채운다(KitHalf).
    public struct Slot
    {
        public string Label;
        public string[] Accepts;
        public bool Prefilled;
        public int ItemIndex;
        public bool RocketFilled;
        public int Anchor;
        public bool KitHalf; // 수리키트로 50% 찼다

        public bool Filled => Prefilled || ItemIndex >= 0 || RocketFilled;
        public bool Allows(string itemId) => Accepts != null && System.Array.IndexOf(Accepts, itemId) >= 0;
        public float Progress => Filled ? 1f : KitHalf ? 0.5f : 0f;
    }

    // 수리키트 한 번(순수 계산, §26.3.3 Q7 사용자 제안): 0%인 칸이 있으면 0%인 칸 모두 50%, 없으면(남은 칸이 모두 50%) 한 칸을 100%로.
    // 그 한 칸 = 스파이가 그 재료를 들고 있지 않은 칸, 그런 칸이 없으면 첫 칸. 바뀐 칸이 없으면 false(로켓이 이미 완성).
    public static bool ApplyRepairKit(List<Slot> slots, System.Func<Slot, bool> heldBySpy)
    {
        bool anyEmpty = false;
        for (int i = 0; i < slots.Count; i++) if (!slots[i].Filled && !slots[i].KitHalf) { anyEmpty = true; break; }
        if (anyEmpty)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Filled || slots[i].KitHalf) continue;
                Slot s = slots[i];
                s.KitHalf = true;
                slots[i] = s;
            }
            return true;
        }
        int pick = -1;
        for (int i = 0; i < slots.Count && pick < 0; i++) if (!slots[i].Filled && (heldBySpy == null || !heldBySpy(slots[i]))) pick = i;
        for (int i = 0; i < slots.Count && pick < 0; i++) if (!slots[i].Filled) pick = i;
        if (pick < 0) return false;
        Slot chosen = slots[pick];
        chosen.RocketFilled = true;
        slots[pick] = chosen;
        return true;
    }

    // 헬리콥터 모자 비행(§25.5 H): 쓴 사람과 쓴 시각. 모든 화면이 시각으로 같은 높이를 그린다.
    public struct Flight
    {
        public int Actor;
        public double StartAt;
    }

    // 괴물 사이렌 함정(옛 소음폭탄, §25.5 K·§68 GP5): 놓인 자리. 누가 근처 건물 수리를 시작하면 그 건물이 철창에 갇히고(CagedAt·Building),
    // 안의 사람들이 연타(Mash)로 부수거나 시간이 지나면 Spent, 그 뒤 쿨타임이 지나면 목록에서 빠진다. 치워도 Spent.
    public struct Bomb
    {
        public int Id;
        public int Owner;
        public Vector3 Pos;
        public bool Spent;
        public double SpentAt;
        public int Building;     // 갇힌 건물(-1 = 아직)
        public double CagedAt;   // 철창이 선 시각(0 = 아직)
        public int Mash;         // 철창 연타 수

        public bool Caged => CagedAt > 0 && !Spent;
    }

    // 맵에 깔린 AI(미끼, §25.5 L): 그 사람 모습으로 서 있다. 녹음기가 붙으면 VoiceAt부터 5~20초마다 주인의 녹음 목소리를 낸다(간격 = Seed, §26).
    // 괴물이 잡으면 CaughtBy·CaughtAt이 기록되고, 잡는 동작(GrabKill)이 끝난 뒤 방장이 지운다.
    public struct Decoy
    {
        public int Id;
        public int Owner;
        public Vector3 Pos;
        public float Yaw;
        public double VoiceAt;
        public int Seed;
        public int CaughtBy;   // 잡은 괴물 액터(0 = 아직)
        public double CaughtAt;

        public bool Caught => CaughtBy != 0;
    }

    // 호박 마차 탈것(§68.7): 주인이 쓰면 Pos에 Yaw 방향으로 소환된다. 주인이 E를 누르면 BoardAt, 그때 방장이 도착 지점(Dest)을 정한다.
    // 시간표는 PumpkinCarriageRules(문 열림 → 들어감 → 닫힘 → 사라짐·도착). 타지 않으면 IdleSeconds 뒤, 탔으면 RemoveAt 뒤 방장이 지운다.
    public struct Carriage
    {
        public int Id;
        public int Owner;
        public Vector3 Pos;
        public float Yaw;
        public double SummonedAt;
        public double BoardAt;   // 0 = 아직 안 탐
        public Vector3 Dest;

        public bool Boarded => BoardAt > 0;
    }

    // 콜라 대포 탈것(§68.9): 주인이 쓰면 Pos에 Yaw 방향으로 설치된다. 주인이 E를 누르면 BoardAt·AimYaw(그때 보던 방향),
    // 방장이 착지 지점(Dest)과 비행 시간(Flight)을 정한다. 시간표는 ColaCannonRules.
    public struct Cannon
    {
        public int Id;
        public int Owner;
        public Vector3 Pos;
        public float Yaw;
        public double SummonedAt;
        public double BoardAt;   // 0 = 아직 안 탐
        public float AimYaw;
        public Vector3 Dest;
        public float Flight;
        public float Ceiling;    // 비행 꼭대기 한계(월드 y, 0 = 위가 열림)

        public bool Boarded => BoardAt > 0;
    }

    public int Revision; // 방장이 커밋할 때마다 1씩 오른다 — 방장 화면은 늦게 도착한 옛 상태를 무시한다(F-18)
    public readonly List<Item> Items = new List<Item>();
    public readonly List<Chest> Chests = new List<Chest>();
    public readonly List<Building> Buildings = new List<Building>();
    public readonly List<Slot> RocketSlots = new List<Slot>();
    public int RocketAnchor = -1;   // 씬의 RocketSpawn 번호(-1 = 정하지 않음)
    public byte OpenExits;           // 열린 탈출구(비트 0~3 = 북·동·남·서)
    public double ExitsOpenedAt;
    public readonly List<int> Escaped = new List<int>(); // 탈출구·모자로 나간 쿠키·스파이
    public readonly List<int> Boarded = new List<int>(); // 로켓에 탄 스파이
    public int RainbowAnchor = -1;
    public RainbowState Rainbow;
    public readonly List<Flight> Flights = new List<Flight>();
    public readonly List<Bomb> Bombs = new List<Bomb>();
    public readonly List<Decoy> Decoys = new List<Decoy>();
    public readonly List<Carriage> Carriages = new List<Carriage>();
    public readonly List<Cannon> Cannons = new List<Cannon>();
    public int NextId = 1; // 폭탄·AI·마차·대포 번호(지워져도 요청이 엉뚱한 것을 가리키지 않게)

    public int ActiveBuildingCount
    {
        get
        {
            int n = 0;
            foreach (Building b in Buildings) if (b.Active) n++;
            return n;
        }
    }

    public bool AllBuildingsRepaired
    {
        get
        {
            if (Buildings.Count == 0) return false;
            foreach (Building b in Buildings) if (!b.Repaired) return false;
            return true;
        }
    }

    public bool RocketComplete
    {
        get
        {
            if (RocketSlots.Count == 0) return false;
            foreach (Slot s in RocketSlots) if (!s.Filled) return false;
            return true;
        }
    }

    public bool ExitOpen(int exit) => exit >= 0 && exit < ExitCount && (OpenExits & (1 << exit)) != 0;
    public bool AnyExitOpen => OpenExits != 0;

    // ---------------- queries ----------------

    public int HeldItemAt(int actor, int slot)
    {
        for (int i = 0; i < Items.Count; i++)
            if (Items[i].Loc == ItemLocation.Held && Items[i].A == actor && Items[i].B == slot) return i;
        return -1;
    }

    public int HeldCount(int actor)
    {
        int n = 0;
        foreach (Item it in Items) if (it.Loc == ItemLocation.Held && it.A == actor) n++;
        return n;
    }

    public int ItemInChest(int chest)
    {
        for (int i = 0; i < Items.Count; i++)
            if (Items[i].Loc == ItemLocation.Chest && Items[i].A == chest) return i;
        return -1;
    }

    public int FlightIndex(int actor)
    {
        for (int i = 0; i < Flights.Count; i++) if (Flights[i].Actor == actor) return i;
        return -1;
    }

    public int DecoyIndex(int id)
    {
        for (int i = 0; i < Decoys.Count; i++) if (Decoys[i].Id == id) return i;
        return -1;
    }

    public int CarriageIndex(int id)
    {
        for (int i = 0; i < Carriages.Count; i++) if (Carriages[i].Id == id) return i;
        return -1;
    }

    // 이 사람이 탄 마차(BoardAt이 있는 것) 번호, 없으면 -1.
    public int RiddenCarriageOf(int actor)
    {
        for (int i = 0; i < Carriages.Count; i++) if (Carriages[i].Owner == actor && Carriages[i].Boarded) return i;
        return -1;
    }

    public int CannonIndex(int id)
    {
        for (int i = 0; i < Cannons.Count; i++) if (Cannons[i].Id == id) return i;
        return -1;
    }

    // 이 사람이 탄 대포(BoardAt이 있는 것) 번호, 없으면 -1.
    public int RiddenCannonOf(int actor)
    {
        for (int i = 0; i < Cannons.Count; i++) if (Cannons[i].Owner == actor && Cannons[i].Boarded) return i;
        return -1;
    }

    public int BombIndex(int id)
    {
        for (int i = 0; i < Bombs.Count; i++) if (Bombs[i].Id == id) return i;
        return -1;
    }

    // 괴물의 남은 소음폭탄 칸(순수 계산, §25.3 R7): 놓인 것과 터지거나 치운 지 cooldown이 안 지난 것이 칸을 차지한다.
    public int BombChargesLeft(int owner, int maxCharges, double now, float cooldown)
    {
        int used = 0;
        foreach (Bomb b in Bombs)
            if (b.Owner == owner && (!b.Spent || now < b.SpentAt + cooldown)) used++;
        return Mathf.Max(0, maxCharges - used);
    }

    // 탈출 판정의 단일 기준(방장이 쓴 상태만 — 본인이 쓰는 속성은 믿지 않는다, AuditFixPlan.md §8.2).
    // 탈출 = 탈출구·모자로 나갔거나 스파이 로켓에 탔다. 맵을 떠남 = 탈출과 같다(탈것 대기는 없어졌다).
    public bool HasEscaped(int actor) => Escaped.Contains(actor) || Boarded.Contains(actor);
    public bool HasLeftMap(int actor) => HasEscaped(actor);

    // ---------------- serialization ----------------

    public byte[] Encode()
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(FormatVersion);
            w.Write(Revision);
            w.Write(NextId);
            w.Write(Items.Count);
            foreach (Item it in Items)
            {
                w.Write(it.Id ?? string.Empty);
                w.Write((byte)it.Loc);
                w.Write(it.A);
                w.Write(it.B);
                WriteVector(w, it.Pos);
                w.Write((byte)Mathf.Clamp(it.Charges, 0, 255));
            }
            w.Write(Chests.Count);
            foreach (Chest c in Chests)
            {
                w.Write((byte)c.Anchor);
                w.Write(c.Spy);
                w.Write(c.Opened);
            }
            w.Write(Buildings.Count);
            foreach (Building b in Buildings)
            {
                w.Write(b.MaterialId ?? string.Empty);
                w.Write(b.Active);
                w.Write(b.ItemIndex);
                w.Write(b.Progress);
                w.Write(b.ProgressAt);
                w.Write(b.Repairer);
            }
            WriteSlots(w, RocketSlots);
            w.Write(RocketAnchor);
            w.Write(OpenExits);
            w.Write(ExitsOpenedAt);
            WriteInts(w, Escaped);
            WriteInts(w, Boarded);
            w.Write(RainbowAnchor);
            w.Write((byte)Rainbow);
            w.Write(Flights.Count);
            foreach (Flight f in Flights) { w.Write(f.Actor); w.Write(f.StartAt); }
            w.Write(Bombs.Count);
            foreach (Bomb b in Bombs) { w.Write(b.Id); w.Write(b.Owner); WriteVector(w, b.Pos); w.Write(b.Spent); w.Write(b.SpentAt); w.Write(b.Building); w.Write(b.CagedAt); w.Write(b.Mash); }
            w.Write(Decoys.Count);
            foreach (Decoy d in Decoys) { w.Write(d.Id); w.Write(d.Owner); WriteVector(w, d.Pos); w.Write(d.Yaw); w.Write(d.VoiceAt); w.Write(d.Seed); w.Write(d.CaughtBy); w.Write(d.CaughtAt); }
            w.Write(Carriages.Count);
            foreach (Carriage c in Carriages) { w.Write(c.Id); w.Write(c.Owner); WriteVector(w, c.Pos); w.Write(c.Yaw); w.Write(c.SummonedAt); w.Write(c.BoardAt); WriteVector(w, c.Dest); }
            w.Write(Cannons.Count);
            foreach (Cannon c in Cannons) { w.Write(c.Id); w.Write(c.Owner); WriteVector(w, c.Pos); w.Write(c.Yaw); w.Write(c.SummonedAt); w.Write(c.BoardAt); w.Write(c.AimYaw); WriteVector(w, c.Dest); w.Write(c.Flight); w.Write(c.Ceiling); }
            w.Flush();
            return ms.ToArray();
        }
    }

    public static EscapeState Decode(byte[] data)
    {
        if (data == null || data.Length == 0) return null;
        try
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                if (r.ReadByte() != FormatVersion) return null;
                var s = new EscapeState { Revision = r.ReadInt32(), NextId = r.ReadInt32() };
                int items = r.ReadInt32();
                for (int i = 0; i < items; i++)
                {
                    s.Items.Add(new Item
                    {
                        Id = r.ReadString(),
                        Loc = (ItemLocation)r.ReadByte(),
                        A = r.ReadInt32(),
                        B = r.ReadInt32(),
                        Pos = ReadVector(r),
                        Charges = r.ReadByte(),
                    });
                }
                int chests = r.ReadInt32();
                for (int i = 0; i < chests; i++)
                    s.Chests.Add(new Chest { Anchor = r.ReadByte(), Spy = r.ReadBoolean(), Opened = r.ReadBoolean() });
                int buildings = r.ReadInt32();
                for (int i = 0; i < buildings; i++)
                {
                    s.Buildings.Add(new Building
                    {
                        MaterialId = r.ReadString(),
                        Active = r.ReadBoolean(),
                        ItemIndex = r.ReadInt32(),
                        Progress = r.ReadSingle(),
                        ProgressAt = r.ReadDouble(),
                        Repairer = r.ReadInt32(),
                    });
                }
                ReadSlots(r, s.RocketSlots);
                s.RocketAnchor = r.ReadInt32();
                s.OpenExits = r.ReadByte();
                s.ExitsOpenedAt = r.ReadDouble();
                ReadInts(r, s.Escaped);
                ReadInts(r, s.Boarded);
                s.RainbowAnchor = r.ReadInt32();
                s.Rainbow = (RainbowState)r.ReadByte();
                int flights = r.ReadInt32();
                for (int i = 0; i < flights; i++) s.Flights.Add(new Flight { Actor = r.ReadInt32(), StartAt = r.ReadDouble() });
                int bombs = r.ReadInt32();
                for (int i = 0; i < bombs; i++)
                    s.Bombs.Add(new Bomb { Id = r.ReadInt32(), Owner = r.ReadInt32(), Pos = ReadVector(r), Spent = r.ReadBoolean(), SpentAt = r.ReadDouble(), Building = r.ReadInt32(), CagedAt = r.ReadDouble(), Mash = r.ReadInt32() });
                int decoys = r.ReadInt32();
                for (int i = 0; i < decoys; i++)
                    s.Decoys.Add(new Decoy { Id = r.ReadInt32(), Owner = r.ReadInt32(), Pos = ReadVector(r), Yaw = r.ReadSingle(), VoiceAt = r.ReadDouble(), Seed = r.ReadInt32(), CaughtBy = r.ReadInt32(), CaughtAt = r.ReadDouble() });
                int carriages = r.ReadInt32();
                for (int i = 0; i < carriages; i++)
                    s.Carriages.Add(new Carriage { Id = r.ReadInt32(), Owner = r.ReadInt32(), Pos = ReadVector(r), Yaw = r.ReadSingle(), SummonedAt = r.ReadDouble(), BoardAt = r.ReadDouble(), Dest = ReadVector(r) });
                int cannons = r.ReadInt32();
                for (int i = 0; i < cannons; i++)
                    s.Cannons.Add(new Cannon { Id = r.ReadInt32(), Owner = r.ReadInt32(), Pos = ReadVector(r), Yaw = r.ReadSingle(), SummonedAt = r.ReadDouble(), BoardAt = r.ReadDouble(), AimYaw = r.ReadSingle(), Dest = ReadVector(r), Flight = r.ReadSingle(), Ceiling = r.ReadSingle() });
                return s;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[EscapeState] Decode failed: {e.Message}");
            return null;
        }
    }

    public EscapeState Clone() => Decode(Encode());

    private static void WriteVector(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
    private static Vector3 ReadVector(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

    private static void WriteSlots(BinaryWriter w, List<Slot> slots)
    {
        w.Write(slots.Count);
        foreach (Slot s in slots)
        {
            w.Write(s.Label ?? string.Empty);
            string[] accepts = s.Accepts ?? new string[0];
            w.Write(accepts.Length);
            foreach (string a in accepts) w.Write(a ?? string.Empty);
            w.Write(s.Prefilled);
            w.Write(s.ItemIndex);
            w.Write(s.RocketFilled);
            w.Write((byte)Mathf.Clamp(s.Anchor, 0, 255));
            w.Write(s.KitHalf);
        }
    }

    private static void ReadSlots(BinaryReader r, List<Slot> slots)
    {
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            var s = new Slot { Label = r.ReadString() };
            int a = r.ReadInt32();
            s.Accepts = new string[a];
            for (int k = 0; k < a; k++) s.Accepts[k] = r.ReadString();
            s.Prefilled = r.ReadBoolean();
            s.ItemIndex = r.ReadInt32();
            s.RocketFilled = r.ReadBoolean();
            s.Anchor = r.ReadByte();
            s.KitHalf = r.ReadBoolean();
            slots.Add(s);
        }
    }

    private static void WriteInts(BinaryWriter w, List<int> values)
    {
        w.Write(values.Count);
        foreach (int v in values) w.Write(v);
    }

    private static void ReadInts(BinaryReader r, List<int> values)
    {
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++) values.Add(r.ReadInt32());
    }
}
