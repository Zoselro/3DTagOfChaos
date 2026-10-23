using System.Reflection;
using Photon.Pun;
using UnityEngine;

// 보관 중인 PhotonView의 동기화 값을 받아 두는 기록기(NetHold). 마지막 한 번만 있으면 된다 — 꺼낼 때 원래 컴포넌트에 넣는다.
public sealed class HeldSyncRecorder : MonoBehaviour, IPunObservable
{
    // PUN은 스트림 앞부분(머리말)을 건너뛴 위치에서 컴포넌트에 넘긴다. 그 위치는 공개되지 않아 한 번만 리플렉션으로 읽는다.
    private static readonly FieldInfo CurrentItem = typeof(PhotonStream).GetField("currentItem", BindingFlags.NonPublic | BindingFlags.Instance);

    public object[] Data { get; private set; }
    public PhotonMessageInfo Info { get; private set; }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting) return;
        object[] all = stream.ToArray();
        int start = CurrentItem != null ? (int)CurrentItem.GetValue(stream) : 0;
        if (all == null || start > all.Length) return;
        var copy = new object[all.Length - start];
        System.Array.Copy(all, start, copy, 0, copy.Length);
        Data = copy;
        Info = info;
    }
}
