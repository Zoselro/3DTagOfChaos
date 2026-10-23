// 탈출 모드 요청 종류(EscapePlan.md §5.2). 누른 클라이언트가 방장에게 보내고, 방장(EscapeAuthority)만 상태를 바꾼다.
// 요청 값 보조(설치할 칸 번호: 0은 "아무 빈 칸", 칸 i는 i + 1 — 예전 요청과 섞여도 뜻이 같게).
public static class EscapeNet
{
    public static int EncodeSlot(int slot) => slot + 1;
}

// 5·6·11(예전 장치 설치·훔치기)은 Request1009Plan.md §25에서 없앴다 — 옛 빌드와 섞이지 않게 번호를 다시 쓰지 않는다.
public enum EscapeOp : byte
{
    OpenChest = 1,      // A = 상자 번호
    PickUp = 2,         // A = 아이템 번호(바닥)
    Drop = 3,           // A = 인벤토리 칸, Pos = 떨어뜨릴 위치
    DropAll = 4,        // Pos = 떨어뜨릴 위치(잡힘·탈출)
    RocketInsert = 7,   // A = 인벤토리 칸 → 스파이 로켓
    RocketBoard = 8,
    Exit = 9,           // A = 탈출구 번호(0~3) — 쿠키가 열린 탈출구로 걸어 나감
    ToolUse = 10,       // A = 인벤토리 칸, B = 물풍선 색, Pos = 맞힌 지점·이동할 곳, Targets = 맞은 캐릭터 ViewID
    RepairBegin = 12,   // A = 건물 번호, B = 인벤토리 칸 — 재료를 들고 E를 누르기 시작(§25.5 E)
    RepairStop = 13,    // A = 건물 번호 — E를 뗐다(게이지는 방장이 들고 있다)
    RepairFinish = 14,  // A = 건물 번호 — 누르는 사람 화면에서 게이지가 다 찼다(방장이 자기 계산으로 확인)
    DismantleBegin = 15,// A = 건물 번호 — 스파이가 해체 도구로 누르기 시작(§25.5 J)
    Dismantle = 16,     // A = 건물 번호 — 20초를 다 눌렀다
    RainbowOpen = 17,   // 무지개 상자 열기(쿠키·스파이)
    RainbowBreak = 18,  // 무지개 상자 부수기(괴물)
    HatFly = 19,        // A = 인벤토리 칸 — 헬리콥터 모자를 쓰고 떠오르기 시작
    HatEscape = 20,     // 모자 비행으로 맵 밖에 나갔다
    PlaceBomb = 21,     // Pos = 놓을 자리(괴물)
    RemoveBomb = 22,    // A = 폭탄 번호(괴물이 자기 폭탄을 치움)
    DecoyCatch = 23,    // A = AI 번호(괴물이 AI를 잡음)
}

public enum EscapeNoticeKind : byte
{
    SpyCaught = 1,
    // 2(StolenToRocket)는 2026-10-03에 StolenFromDevice로 바뀌어 지웠다 — 번호를 비워 둔다.
    DeviceComplete = 3,
    StolenFromDevice = 4, // 내용: 아이템 ID, 아이템 번호, 보여 줄 시각(PhotonNetwork.Time) — 괴물 제외 대상에게만
    Respawned = 5,        // 로켓에 넣은 재료가 다른 상자(또는 장치 옆 바닥)에 다시 생김. 내용: 위 셋 + 바닥이면 true(Request1009Plan.md §22.6 M4)
    BuildingRepaired = 6, // 건물 하나가 가동됐다(§25.5 E). 내용: 재료 ID
    ExitsOpened = 7,      // 모든 건물이 가동돼 탈출구 2개가 열렸다
    RainbowAppeared = 8,  // 마녀 시간에 무지개 상자가 나타났다(§25.5 H)
    RainbowBroken = 9,    // 괴물이 무지개 상자를 부쉈다
    NoiseBomb = 10,       // 소음폭탄이 터졌다(비상벨). 내용: 건물 번호(문자열)
    MaterialRestored = 11, // 닿는 곳에 하나도 없던 재료를 방장이 다시 만들었다(§26 D). 내용: 아이템 ID, 아이템 번호, 보여 줄 시각 — 괴물 제외
    AllDecoysGone = 12,    // 마녀 시간이 시작돼 AI가 모두 사라졌다(§26 Q16)
    CookieCaught = 13,     // 괴물이 진짜 쿠키(스파이 포함)를 잡았다. 내용: 잡힌 사람 액터 번호(문자열)
}

// 게임이 끝난 이유(NetKeys.EscapeEndReason).
public enum EscapeEndReason
{
    AllResolved = 1, // 모두 탈출했거나 잡혔다
    TimeUp = 2,      // 제한시간이 끝났다(D8)
    WitchStrike = 3, // 마녀가 내리쳤다
}

// 죽은 이유(NetKeys.DeathCause, 결과 화면).
public enum DeathCause
{
    None = 0,
    Monster = 1,
    Witch = 2,
}

public struct EscapeRequest
{
    public EscapeOp Op;
    public int Sender;
    public int A;
    public int B;
    public UnityEngine.Vector3 Pos;
    public int[] Targets;

    public object[] ToPayload() => new object[] { (byte)Op, A, B, Pos, Targets ?? new int[0] };

    public static bool TryParse(object data, int sender, out EscapeRequest request)
    {
        request = default;
        if (!(data is object[] d) || d.Length < 5) return false;
        if (!(d[0] is byte op) || !(d[1] is int a) || !(d[2] is int b) || !(d[3] is UnityEngine.Vector3 pos)) return false;
        request = new EscapeRequest { Op = (EscapeOp)op, Sender = sender, A = a, B = b, Pos = pos, Targets = d[4] as int[] ?? new int[0] };
        return true;
    }
}
