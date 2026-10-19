using System;
using System.Collections.Generic;
using UnityEngine;

// 캔디숲 실제 모델 맵의 위치표(Request1009Plan.md §28 C8). CandyForestLayout이 규칙대로 만들고 CandyForestMapBuilder가 씬에 놓는다.
// locked를 켜면 빌더가 위치표를 새로 만들지 않는다 — 손으로 고친 위치를 지킨다.
public class CandyForestLayoutSO : ScriptableObject
{
    [Serializable]
    public struct Placement
    {
        public string asset;      // 프리팹 이름(CF_*)
        public string group;      // 씬에서 묶을 부모 이름(Buildings, Roads, Quadrant_Q1, Edge, Fence, Forest ...)
        public Vector3 position;
        public float yaw;
        public bool noColliders;  // 울타리 밖 숲처럼 겉모습만
    }

    public bool locked;
    public List<Placement> placements = new List<Placement>();
}
