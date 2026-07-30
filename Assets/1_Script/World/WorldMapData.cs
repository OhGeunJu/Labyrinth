using System;
using UnityEngine;

// 저작된 월드맵 한 장. `WorldMap.mapData`에 연결하면 런타임이 이걸 그대로 싣는다.
// 편집은 `DungeonColony → World Map Editor`(격자에 칠하는 방식).
//
// 왜 SO인가: 월드 레이아웃은 **세이브 데이터가 아니라 저작 데이터**다. 플레이 중 바뀌는 것
// (발견 상태·내 정착지 승격 등)은 런타임 `WorldMap`이 들고, 원본은 손대지 않는다.
[CreateAssetMenu(fileName = "WorldMapData", menuName = "DungeonColony/WorldMapData")]
public class WorldMapData : ScriptableObject
{
    // 격자 한 칸이 담는 것 — **논리 정보만**이다.
    // 높이/생김새는 여기 없다. 지형은 씬에 직접 만들어 붙인 Terrain이고, 격자는 그 위에 얹힌다.
    [Serializable]
    public struct Node
    {
        public Biome Biome;              // 그 지역의 성격 — 콘텐츠 게이팅(BiomeContent)에 쓰인다
        public WorldNodeContent Content; // 그 칸에 있는 것(마을·소굴·자원·내 정착지)
    }

    [Min(1)] public int Width  = 16;
    [Min(1)] public int Height = 16;

    [Tooltip("시작 정착지 노드. 런타임에 반드시 '내 정착지'가 된다.")]
    public Vector2Int StartNode = new Vector2Int(8, 8);

    // 행 우선 1차원 배열 — Unity는 2차원 배열을 직렬화하지 못한다. 길이 = Width * Height.
    [SerializeField] Node[] nodes = Array.Empty<Node>();

    // `nodes`가 **어느 크기로 지어졌는지**. Width/Height는 인스펙터에서 직접 바뀔 수 있으므로
    // 그것만 보고 옛 크기를 추론하면 안 된다(추론하면 크기를 고치는 순간 내용이 날아간다).
    [SerializeField, HideInInspector] int builtWidth;
    [SerializeField, HideInInspector] int builtHeight;

    // 배열이 현재 크기와 맞는가. 어긋난 에셋을 런타임이 싣지 않도록 하는 게이트.
    public bool IsSized =>
        nodes != null && builtWidth == Width && builtHeight == Height &&
        nodes.Length == Width * Height;

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public Node Get(int x, int y) =>
        IsSized && InBounds(x, y) ? nodes[y * Width + x] : default;

    public void Set(int x, int y, Node n)
    {
        if (!IsSized || !InBounds(x, y)) return;
        nodes[y * Width + x] = n;
    }

    // 크기 변경 — 기존 내용을 좌하단(0,0) 기준으로 보존하고 남는 칸은 기본값.
    // 배열이 크기와 어긋나 있을 때(새 에셋/수동 편집 후)의 복구 경로이기도 하다.
    public void Resize(int w, int h)
    {
        w = Mathf.Max(1, w);
        h = Mathf.Max(1, h);

        var old      = nodes;
        int oldW     = builtWidth, oldH = builtHeight;
        bool hadData = old != null && oldW > 0 && oldH > 0 && old.Length == oldW * oldH;

        var next = new Node[w * h];
        if (hadData)
        {
            int cw = Mathf.Min(w, oldW), ch = Mathf.Min(h, oldH);
            for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                next[y * w + x] = old[y * oldW + x];
        }

        Width  = w;
        Height = h;
        nodes  = next;
        builtWidth  = w;
        builtHeight = h;
        StartNode = new Vector2Int(Mathf.Clamp(StartNode.x, 0, w - 1),
                                   Mathf.Clamp(StartNode.y, 0, h - 1));
    }

    public void Fill(Biome biome)
    {
        if (!IsSized) Resize(Width, Height);
        for (int i = 0; i < nodes.Length; i++) nodes[i].Biome = biome;
    }

    public void ClearContent()
    {
        if (!IsSized) Resize(Width, Height);
        for (int i = 0; i < nodes.Length; i++) nodes[i].Content = WorldNodeContent.Empty;
    }


    void OnValidate()
    {
        // 크기 기록 필드가 없던 시절의 에셋 흡수 — 배열 길이가 맞으면 그대로 인정한다.
        // (이게 없으면 옛 에셋이 "크기 불일치"로 판정돼 아래 Resize에서 통째로 지워진다.)
        if (builtWidth == 0 && builtHeight == 0 && nodes != null && nodes.Length == Width * Height)
        {
            builtWidth  = Width;
            builtHeight = Height;
        }

        // 인스펙터에서 Width/Height를 직접 만졌을 때 배열을 따라가게 한다.
        if (!IsSized) Resize(Width, Height);
    }
}
