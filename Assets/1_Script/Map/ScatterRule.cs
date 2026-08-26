using System;
using UnityEngine;

// **자잘한 것은 좌표에서 계산해 뿌린다** — 나무·바위(2026-08-26).
//
// 29,440m 세계에 나무를 손으로 놓을 수는 없다. 그래서 **좌표 해시로 결정적으로** 뿌린다 —
// 떠났다 돌아와도 **같은 자리에 같은 나무**가 선다(저장하는 것이 없다).
//
// **조건은 지형이 답한다** — 절벽에 나무가 서면 안 되고, 설선 위에도 없어야 한다.
// 그 판정은 `TerrainHeight`가 하므로 **청크가 로드 안 돼 있어도 계산된다.**
[Serializable]
public class ScatterRule
{
    public string Name = "나무";

    [Tooltip("`ResourceNode`가 붙은 프리팹(`3_Prefab/Node/`).")]
    public GameObject Prefab;

    [Tooltip("청크(256m) 하나당 몇 개. 소수도 된다 — 0.5면 두 청크에 하나꼴.")]
    [Min(0f)] public float PerChunk = 20f;

    [Header("설 수 있는 자리")]
    [Tooltip("경사(도). 나무는 완만한 데만 — 절벽에 서면 떠 보인다.")]
    public float MaxSlope = 25f;

    [Tooltip("고도(m). 설선 위에는 나무가 없다.")]
    public float MinHeight = -100f;
    public float MaxHeight =  400f;

    [Header("생김새")]
    public Vector2 ScaleRange = new(0.85f, 1.2f);

    [Tooltip("서로 이만큼(m)보다 가까우면 하나를 버린다. 0이면 안 본다.")]
    [Min(0f)] public float MinSpacing = 4f;
}
