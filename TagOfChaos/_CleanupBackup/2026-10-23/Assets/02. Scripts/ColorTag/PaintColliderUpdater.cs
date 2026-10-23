using Unity.Jobs;
using UnityEngine;

// 로컬 쿠키의 색칠용 MeshCollider를 현재 애니메이션 포즈로 갱신하는 협력 클래스(PlayerPaintCanvas가 소유,
// Bug-fix-plan.md §20 A안 → §30.5).
//
// 몸통 메시가 정점 16.7만 개라 "굽기(BakeMesh) + 스케일 보정 루프 + MeshCollider 재쿠킹"이 한 번에 수십 ms였고,
// 이를 색칠 페이즈 내내 3프레임마다 수행해 쿠키 전원이 색칠 시간 동안 끊겼다(§30.5.3). 이제
//   ① 호출부가 필요할 때(칠하는 중·커서가 몸 근처)만, 시간 간격으로 요청하고(F1)
//   ② 물리 쿠킹(Physics.BakeMesh)은 워커 스레드 Job에서 수행한 뒤 완료되면 콜라이더에 대입하며(F2 — 이미 쿠킹된
//      데이터를 재사용하므로 대입이 즉시 끝난다. 쿠킹 중에는 이전 포즈의 콜라이더를 그대로 쓴다)
//   ③ BakeMesh(useScale: true)로 스케일 보정 루프를 없애고(F3 — 결과가 기존 "false로 굽고 localScale로 나누기"와 같음)
//   ④ 레이캐스트 전용이라 가벼운 쿠킹 옵션만 쓴다(F4).
// 두 메시를 번갈아 써서 Job이 쿠킹 중인 메시를 콜라이더나 다음 굽기가 건드리지 않게 한다.
public class PaintColliderUpdater
{
    private const MeshColliderCookingOptions CookingOptions = MeshColliderCookingOptions.UseFastMidphase;

    private struct CookJob : IJob
    {
        public int MeshId;
        public MeshColliderCookingOptions Options;

        public void Execute()
        {
            Physics.BakeMesh(MeshId, false, Options);
        }
    }

    private readonly SkinnedMeshRenderer source;
    private readonly MeshCollider target;
    private readonly Mesh[] buffers = new Mesh[2];
    private int backIndex;
    private JobHandle cookHandle;
    private bool isCooking;
    private float nextRefreshTime;

    public PaintColliderUpdater(SkinnedMeshRenderer source, MeshCollider target, string name)
    {
        this.source = source;
        this.target = target;
        target.cookingOptions = CookingOptions; // 대입 시 Job이 만든 쿠킹 데이터를 재사용하려면 옵션이 같아야 한다
        for (int i = 0; i < buffers.Length; i++)
            buffers[i] = new Mesh { name = $"BakedColliderMesh_{name}_{i}" };
    }

    // 매 프레임 호출. wanted=false여도 진행 중인 쿠킹의 완료 처리는 계속한다.
    public void Tick(bool wanted, float refreshInterval)
    {
        if (isCooking && cookHandle.IsCompleted)
        {
            cookHandle.Complete();
            isCooking = false;
            target.sharedMesh = buffers[backIndex];
            backIndex = 1 - backIndex; // 방금 콜라이더가 된 메시는 다음 굽기에서 쓰지 않는다
        }

        if (!wanted || isCooking || Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + refreshInterval;

        Mesh back = buffers[backIndex];
        source.BakeMesh(back, true);
        cookHandle = new CookJob { MeshId = back.GetInstanceID(), Options = CookingOptions }.Schedule();
        JobHandle.ScheduleBatchedJobs();
        isCooking = true;
    }

    public void Dispose()
    {
        if (isCooking) cookHandle.Complete();
        isCooking = false;
        for (int i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] != null) Object.Destroy(buffers[i]);
            buffers[i] = null;
        }
    }
}
