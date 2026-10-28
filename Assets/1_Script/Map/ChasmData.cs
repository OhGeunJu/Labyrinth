using System;
using System.Collections.Generic;
using UnityEngine;

// **절벽(낭떠러지)의 저작 데이터**(2026-09-26, 사용자 결정). 한 판에 하나이고 `Resources/World/Chasms`에 산다.
//
// **그린 곳에만 선다** — 계단 경계에 저절로 나는 것이 아니다(사용자: *"(나) 채굴 불가가 맞다"*). 그래서
// 원본은 칠한 **2m 덩어리** 목록이다(`GroundLevel.BlockOf`와 같은 격자 — 땅 높이 정점 사이의 한 칸이라,
// 지형에 구멍을 뚫는 단위와 땅 고르기의 단위가 둘 다 이것이다).
//
// **칠하는 문은 `World → I. Paint Chasms` 하나다.** 밖에서 에셋을 고치면 유니티가 저장 한 번에 되돌린다.
[CreateAssetMenu(menuName = "DungeonColony/ChasmData")]
public class ChasmData : ScriptableObject
{
    [Tooltip("칠한 2m 덩어리. 덩어리 (bx, bz)는 월드 [bx·2, bx·2+2]다.")]
    public List<Vector2Int> Blocks = new();

    [Tooltip("절벽 바닥의 월드 높이(m). 벽은 여기까지 내려가고 안개는 여기서부터 쌓인다. " +
             "계단 바닥(±6m)보다 한참 아래여야 한다.")]
    public float BottomY = -24f;

    [Tooltip("땅 고르기가 내릴 수 있는 한계 = 바닥 + 이 값. **파서 절벽 밑과 이어지면 안 된다**(사용자).")]
    public float LevelMargin = 2f;

    [Tooltip("안개가 시작하는 깊이 — 가장자리 윗면에서 이만큼 아래부터 겹겹이 짙어진다.")]
    public float FogStartDepth = 1.5f;

    // **자연 다리** — 처음부터 지어져 있는 다리다. **종류는 지을 수 있는 다리와 같은 `BuildingData`**라
    // 규칙이 한 벌이다(사용자: *"돌다리도 건설할 수 있으면 좋겠는데 · 다른 종류도 그냥 자연 스폰"*).
    [Serializable]
    public class BridgeSpot
    {
        public BuildingData Kind;
        [Tooltip("다리 한가운데의 월드 자리(XZ). Y는 놓일 때 가장자리 높이로 정해진다.")]
        public Vector3 Position;
        [Tooltip("0이면 다리가 Z축을 따라 놓인다. 90이면 X축.")]
        public float Yaw;
    }
    public List<BridgeSpot> Bridges = new();

    // **무너진 다리** — 보이기만 하고 못 건넌다(장식). 건설 목록에는 없다.
    [Serializable]
    public class Ruin
    {
        public GameObject Prefab;
        public Vector3    Position;
        public float      Yaw;
    }
    public List<Ruin> Ruins = new();
}
