using Photon.Pun;
using UnityEngine;

// 방 전체를 다른 씬으로 옮기는 유일한 경로(방장 전용). 씬을 옮기기 직전에 서버의 방 이벤트 캐시를 비운다.
//
// PhotonNetwork.Instantiate는 방 이벤트 캐시에 저장되고(AddToRoomCache, PhotonNetwork.cs:2681), 씬 전환으로
// 로컬에서 파괴된 오브젝트의 캐시는 지워지지 않는다. 비우지 않으면 나중에 들어온 사람이 이전 씬들의 쿠키·괴물을
// 전부 자기 대기실에 생성한다(Bug-fix-plan.md §26.3 ㉑-2). DestroyAll() 대신 캐시만 지우는 이유: 씬 로드가 어차피
// 로컬 오브젝트를 파괴하므로 파괴 이벤트까지 보낼 필요가 없고, 대기실에서 메시지 큐를 멈추고 기다리는 괴물이
// GameScene 도착 후 그 파괴 이벤트를 처리하는 일도 없앤다. Room CustomProperties는 이벤트 캐시와 별개라 영향이 없다.
public static class RoomSceneTransition
{
    public static void LoadLevelForRoom(string sceneName)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning($"[RoomSceneTransition] Only the master client can move the room to '{sceneName}'.");
            return;
        }

        // 같은 클라이언트가 보낸 명령은 서버에서 순서대로 처리된다: 캐시 삭제 → curScn 변경(LoadLevel).
        // 다른 사람들의 새 씬 Instantiate는 curScn을 받은 뒤에 일어나므로 새 씬의 캐시는 지워지지 않는다.
        PhotonNetwork.OpRemoveCompleteCache();
        RoomSceneFollower.NoteOwnLoad(sceneName); // 새 씬에 먼저 들어간 뒤 curScn 캐시가 따라오기 전에 "옛 씬으로 따라가기"를 하지 않도록
        PhotonNetwork.LoadLevel(sceneName);
    }
}
