using System.Reflection;
using Photon.Pun;
using UnityEngine;

// PUN 씬 로드 상태(Request1009Plan.md §46 후속 — 방장만 대기실에 남는 문제).
// PUN LoadLevel은 방장이 다른 씬을 부르면 진행 중이던 로드를 allowSceneActivation = false로 "취소"하고 참조를 버린다
// (PhotonNetworkPart.SetLevelInPropsIfSynced). Unity에서 그렇게 멈춘 비동기 로드는 90 %에서 영원히 기다리며 그 뒤의 모든 씬 로드를
// 막는다 — 방장만 대기실에 갇혔다. 그래서: 로드 중에는 다른 씬을 부르지 않게(IsBusy) 하고, 그래도 취소된 로드가 생기면 되살린다(Rescue).
public static class PunSceneLoad
{
    private static readonly FieldInfo OperationField =
        typeof(PhotonNetwork).GetField("_AsyncLevelLoadingOperation", BindingFlags.NonPublic | BindingFlags.Static);

    private static AsyncOperation watched;

    public static int RescueCount { get; private set; } // 시험·로그용

    // PUN이 지금 들고 있는 로드(없으면 null).
    public static AsyncOperation Current => OperationField != null ? OperationField.GetValue(null) as AsyncOperation : null;

    // PUN LoadLevel이 아직 끝나지 않았는지(대기열에서 기다리는 중 = 진행도 0도 포함).
    public static bool IsBusy
    {
        get
        {
            if (OperationField == null)
            {
                float p = PhotonNetwork.LevelLoadingProgress;
                return p > 0f && p < 1f;
            }
            AsyncOperation op = Current;
            return op != null && !op.isDone;
        }
    }

    // PhotonNetwork.LoadLevel 대신 쓴다: 부른 순간 PUN이 끝나지 않은 로드를 취소했으면 바로 다시 허용한다.
    public static void Load(string sceneName)
    {
        AsyncOperation before = Current;
        PhotonNetwork.LoadLevel(sceneName);
        if (before != null && !before.isDone && !before.allowSceneActivation)
        {
            before.allowSceneActivation = true;
            RescueCount++;
            Debug.LogWarning($"[RoomSync] PUN cancelled an unfinished scene load while loading '{sceneName}' — let it finish so later loads are not blocked.");
        }
    }

    // 주기적으로(RoomSceneFollower): PUN이 끝나지 않은 로드를 취소하고 새 로드로 바꿨으면 옛 로드를 다시 허용해 대기열을 푼다.
    // 옛 씬이 잠깐 열린 뒤 새 로드가 이어서 열린다. 방을 나갈 때 PUN이 일부러 멈춘 로드는 건드리지 않는다.
    public static bool RescueCancelled(bool inRoom)
    {
        AsyncOperation op = Current;
        bool rescued = false;
        if (inRoom && watched != null && !ReferenceEquals(watched, op) && !watched.isDone && !watched.allowSceneActivation)
        {
            watched.allowSceneActivation = true;
            RescueCount++;
            rescued = true;
        }
        watched = inRoom ? op : null;
        return rescued;
    }
}
