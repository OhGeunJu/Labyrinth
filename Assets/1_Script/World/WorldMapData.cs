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

    // ── 이 지도가 몇 층인가 (2026-09-15) ────────────────────────────────────
    //
    // **깊이가 곧 티어다** — 지하 설계의 축이고, 딸린 규칙이 *"한 층 안에서는 안 흔든다"*이다.
    // 흔들면 플레이어가 재야 하는 것이 *몇 층인가*에서 **이 골짜기가 어느 바이옴인가**로
    // 되돌아간다. **바이옴은 성격만 정한다.**
    //
    // 그런데 그 축을 **아무도 들고 있지 않았다.** `8. Generate Biome Map`이 지하 바이옴
    // 여덟을 한 판에 섞어 깔아서, 16x16 지도에 티어 1·2·3이 같이 있었다 — 시작 둘레
    // 열네 칸 걸러 하나가 **수정동(3층)**이었고 거기 벽을 파면 **티타늄**이 나왔다
    // (사용자: *"근데 1층에 티타늄 막 있는게 맞아?"*). 벽 광맥이 안 보이던 동안에는
    // 이 어긋남도 같이 안 보였다.
    //
    // **층은 저작한다 — 지도가 든다.** 지도 한 장이 곧 한 층이라 여기가 그 자리다
    // (`WorldMap.AuthoredStart`가 여기 있는 것과 같은 이유). 나중에 층 전환이 들어오면
    // *"지금 몇 층인가"*를 물을 곳도 여기다.
    //
    // **어느 바이옴이 몇 층인지는 여기 안 적는다** — `BiomeContentData.Tier`가 이미 답한다
    // (`BiomeTier.Of`). 여기 또 적으면 **표가 두 벌**이 되고, 티어를 고치는 날 한쪽이 뒤처진다.
    [Range(1, 5)]
    [Tooltip("이 지도가 지하 몇 층인가. **여기 깔리는 바이옴은 이 층의 티어와 같아야 한다** — " +
             "`Terrain → 8. Generate Biome Map`이 그것만 깐다.")]
    public int Floor = 1;

    // **층 통로 자리**(2026-10-01, `FloorPassage`) — 지역 칸. **음수면 자동**(내려가는 길 (W−4, H−4) · 올라가는 길 (3, 3)).
    // 내려가는 길은 이 층에서 다음 층으로, 올라가는 길은 앞 층에서 이 층으로 이어진다(1층엔 올라가는 길이 · 4층엔
    // 내려가는 길이 안 선다).
    [Tooltip("이 층에서 다음 층으로 내려가는 통로의 지역 칸. 음수면 자동(W-4, H-4).")]
    public Vector2Int PassageDown = new(-1, -1);
    [Tooltip("앞 층에서 이 층으로 올라오는 통로의 지역 칸. 음수면 자동(3, 3).")]
    public Vector2Int PassageUp = new(-1, -1);

    // ── 손으로 칠했다 (2026-09-15) ──────────────────────────────────────────
    //
    // **생성기는 저작을 대신하지 않는다 — 저작을 시작할 자리를 만든다**(그 파일의 첫 줄이
    // 그렇게 적혀 있다). 그런데 `1. Rebuild Underworld`가 `8. Generate Biome Map`을
    // 단계로 부르므로, **손으로 칠한 지도가 묶음 한 번에 통째로 날아간다.**
    //
    // 이 저장소가 같은 자리에서 쓰는 표시가 이미 있다 — `WorldObjectTag.Generated`
    // (*"손으로 놓은 것은 안 건드린다"*)와 `WorldAuthoringRoot.LoadedWholeWorld`
    // (*"세계 전부를 담았을 때만 굽는다"*). 같은 규칙을 지도에도 둔다.
    //
    // **켜지는 자리는 `World Map Editor`의 붓 하나다** — 한 칸이라도 칠하면 켜진다.
    // **끄는 것은 손으로만** 한다(그 창의 버튼) — *"다시 굴려도 좋다"*는 사람만 아는 사실이다.
    //
    // **생성기는 켜져 있으면 거절한다.** 덮어쓰고 나서 알려 주는 것은 소용이 없다 —
    // 되돌릴 것이 없기 때문이다(Undo는 묶음 실행을 못 넘어간다).
    [Tooltip("손으로 칠한 지도. 켜져 있으면 `8. Generate Biome Map`이 **거절한다** — " +
             "다시 굴리려면 World Map Editor에서 끌 것.")]
    public bool HandPainted;

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
