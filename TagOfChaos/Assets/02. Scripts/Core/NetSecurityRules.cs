using System.Collections;
using UnityEngine;

// 조작된 클라이언트를 막는 판정(AuditFixPlan.md §7~§10). Photon Cloud에는 서버 판정이 없어 어떤 클라이언트든 이벤트·RPC를 마음대로 보내고,
// 방·남의 플레이어 속성을 쓰고, 스스로 방장이 될 수 있다(PUN 소스: Room.SetCustomProperties, Player.SetCustomProperties, Room.SetMasterClient).
// 그래서 받는 쪽과 정직한 방장이 이 규칙으로 판단한다. 네트워크와 분리한 순수 계산이라 EditMode로 시험한다.
public static class NetSecurityRules
{
    // ---------------- 방장 ----------------

    // 방장 정책상 자격자(괴물이 아닌 최선 입장자)가 나인데 다른 사람이 방장이면 내가 되찾는다(S-01).
    // 정책대로 넘긴 경우(새 방장 = 자격자)에는 아무도 되찾지 않는다.
    public static bool ShouldReclaimMaster(int currentMaster, int desiredMaster, int localActor) =>
        desiredMaster > 0 && localActor == desiredMaster && currentMaster != desiredMaster;

    // 방의 씬에 들어와 있지 않은 사람은 되찾지 않는다(Request1009Plan.md §43) — 씬에 없는 방장은 그 씬의 진행 로직을 돌릴 수 없고,
    // PUN은 방장을 씬 동기화에서 빼므로 시작 직후 되찾으면 혼자 대기실에 남았다.
    public static bool ShouldReclaimMaster(int currentMaster, int desiredMaster, int localActor, bool inRoomScene) =>
        inRoomScene && ShouldReclaimMaster(currentMaster, desiredMaster, localActor);

    // ---------------- 속성 쓰기 ----------------

    // 플레이어 속성을 쓴 사람이 허용되는지. writer 0 = 알 수 없음(서버가 보낸 사람을 담지 않음) → 막지 않는다.
    // 방장 키는 방장(또는 방장 교체 직후의 이전 방장)만, 본인 키는 본인(또는 방장 — 판 초기화·복구)만.
    public static bool IsAllowedPlayerWriter(bool masterKey, int writer, int target, int master, int previousMaster)
    {
        if (writer <= 0) return true;
        bool byMaster = writer == master || (previousMaster > 0 && writer == previousMaster);
        return masterKey ? byMaster : writer == target || byMaster;
    }

    // 방 속성을 쓴 사람이 방장인지(이전 방장 유예 포함). writer 0 = 알 수 없음 → 막지 않는다.
    public static bool IsAllowedRoomWriter(int writer, int master, int previousMaster) =>
        writer <= 0 || writer == master || (previousMaster > 0 && writer == previousMaster);

    // Photon 속성 값 비교: 배열(int[]·byte[]·string[] 등)과 Hashtable(문 상태)은 내용으로 비교한다.
    public static bool ValuesEqual(object a, object b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a is IDictionary da && b is IDictionary db)
        {
            if (da.Count != db.Count) return false;
            foreach (DictionaryEntry e in da)
                if (!db.Contains(e.Key) || !ValuesEqual(e.Value, db[e.Key])) return false;
            return true;
        }
        if (a is System.Array xa && b is System.Array xb)
        {
            if (xa.Length != xb.Length || xa.GetType() != xb.GetType()) return false;
            for (int i = 0; i < xa.Length; i++)
                if (!Equals(xa.GetValue(i), xb.GetValue(i))) return false;
            return true;
        }
        return a.Equals(b);
    }

    // ---------------- 처형(괴물이 쿠키를 잡음) ----------------

    public const float GrabReachSlack = 3f; // 동기화 지연·몸 크기 여유(m)

    // 방장이 처형을 확정해도 되는지(S-04·S-06). 위치는 방장 화면 기준 — 기록(PositionHistory)이 없을 때(입장 직후)만 쓴다.
    public static bool CanConfirmGrabKill(bool senderIsMonster, bool huntPhase, bool cookieAlive, bool cookieOnMap,
        Vector3 monsterFeet, Vector3 cookieFeet, float reachFromCenter, float maxHeight, float sinceLastConfirm, float minInterval)
    {
        if (!senderIsMonster || !huntPhase || !cookieAlive || !cookieOnMap) return false;
        if (sinceLastConfirm < minInterval) return false;
        Vector3 d = cookieFeet - monsterFeet;
        if (d.y > maxHeight + GrabReachSlack) return false;
        d.y = 0f;
        float reach = reachFromCenter + GrabReachSlack;
        return d.sqrMagnitude <= reach * reach;
    }

    // 되돌림 판정(Request1009Plan.md §46.2 GK1): 괴물 화면이 본 순간의 두 위치로 잡기 거리를 재고, 그 위치들이 방장 기록의 실제 길과 맞는지 본다.
    public const float GrabClaimReachSlack = 0.5f;   // 괴물 화면 판정과 보낸 값의 차이(반올림·한 프레임)
    public const float GrabClaimPathTolerance = 2f;  // 주장한 위치가 방장 기록의 길에서 벗어나도 되는 거리(m)
    public const float GrabClaimMaxAge = 0.6f;       // 이보다 오래된 주장은 받지 않는다(초)
    public const float GrabRewindWindow = 0.5f;      // 본 시각보다 이만큼 앞선 기록부터 비교한다(초)
    public const float RemoteViewLag = 0.15f;        // 방장 화면의 원격 캐릭터가 받은 위치보다 늦게 그려지는 시간(보간, 초)
    public const float MaxRemoteLead = 0.6f;

    // 원격 캐릭터의 기록을 앞으로 늘릴 시간: 보낸 뒤 걸린 시간 + 보간 지연.
    public static float RemoteLead(float claimAge) => Mathf.Clamp(claimAge, 0f, GrabClaimMaxAge) + RemoteViewLag;

    // 패킷 기준 기록(§70.2.5 D4)을 앞으로 늘릴 시간: 마지막 패킷을 보낸 시각부터 본 시각까지 + 한 패킷 간격의 여유.
    public const float PacketLeadSlack = 0.05f;
    public static float PacketLead(double seenAt, double newestRecordAt) =>
        Mathf.Clamp((float)(seenAt - newestRecordAt), 0f, MaxRemoteLead) + PacketLeadSlack;

    public enum GrabVerdict : byte
    {
        Confirmed,
        NotMonster,
        WrongPhase,
        NotAlive,
        OffMap,
        TooSoon,
        ClaimTooOld,
        OutOfReach,
        CookieMismatch,
        MonsterMismatch,
    }

    // 공통 조건(보낸 사람·단계·생존·맵·간격).
    public static GrabVerdict GrabPreconditions(bool senderIsMonster, bool huntPhase, bool cookieAlive, bool cookieOnMap, float sinceLastConfirm, float minInterval)
    {
        if (!senderIsMonster) return GrabVerdict.NotMonster;
        if (!huntPhase) return GrabVerdict.WrongPhase;
        if (!cookieAlive) return GrabVerdict.NotAlive;
        if (!cookieOnMap) return GrabVerdict.OffMap;
        return sinceLastConfirm < minInterval ? GrabVerdict.TooSoon : GrabVerdict.Confirmed;
    }

    // 괴물 화면이 본 순간(claimAge초 전)의 괴물·쿠키 위치로 확정해도 되는지. cookiePathError·monsterPathError는 그 주장 위치가
    // 방장 기록의 길(본 시각 − 0.5초 ~ 지금)에서 가장 가까웠던 거리(PositionTrack.TryMinDistance).
    public static GrabVerdict JudgeGrabClaim(Vector3 seenMonster, Vector3 seenCookie, float reachFromCenter, float maxHeight,
        float claimAge, float cookiePathError, float monsterPathError)
    {
        if (claimAge > GrabClaimMaxAge || claimAge < -0.1f) return GrabVerdict.ClaimTooOld;
        Vector3 d = seenCookie - seenMonster;
        if (d.y > maxHeight + GrabClaimReachSlack) return GrabVerdict.OutOfReach;
        d.y = 0f;
        float reach = reachFromCenter + GrabClaimReachSlack;
        if (d.sqrMagnitude > reach * reach) return GrabVerdict.OutOfReach;
        if (cookiePathError > GrabClaimPathTolerance) return GrabVerdict.CookieMismatch;
        if (monsterPathError > GrabClaimPathTolerance) return GrabVerdict.MonsterMismatch;
        return GrabVerdict.Confirmed;
    }

    // ---------------- 쿠키 들기 ----------------

    public const float CarryReachSlack = 1.5f;

    // 들리는 쪽이 들기를 받아들이는지(F-01). 보낸 사람이 드는 쪽 캐릭터의 주인이어야 하고, 이미 들려 있거나 누군가를 들고 있으면 거절.
    public static bool CanAcceptCarry(bool selfBroken, bool selfCarried, bool selfCarrying, bool senderOwnsCarrier, float distance, float grabRange)
    {
        if (selfBroken || selfCarried || selfCarrying || !senderOwnsCarrier) return false;
        return distance <= grabRange + CarryReachSlack;
    }

    // ---------------- 이동 ----------------

    // 보고된 이동이 가능한 속도인지(S-15). dt가 너무 작으면(같은 프레임) 판단하지 않는다.
    public static bool IsPlausibleMove(Vector3 from, Vector3 to, float dt, float maxSpeed)
    {
        if (dt <= 0.05f) return true;
        Vector3 d = to - from;
        d.y = Mathf.Max(0f, d.y); // 떨어지는 것은 빠를 수 있다
        return d.magnitude <= maxSpeed * dt + 2f;
    }
}
