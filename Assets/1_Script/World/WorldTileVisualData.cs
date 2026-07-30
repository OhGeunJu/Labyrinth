using System;
using System.Collections.Generic;
using UnityEngine;

// 월드맵 **격자 지점에 세우는 오브젝트**의 정의 — 지형 자체는 여기서 다루지 않는다.
//   지형(형태·색) = `WorldMapTerrain`이 맵 전체를 메시 한 장으로 짓거나, 직접 만들어 붙인 것.
//   여기 있는 건 그 지형 **위에 얹는 것들**이다: 바이옴 데코(나무·바위 등)와 내용물 마커.
//
// 높이는 코드가 정하지 않는다 — `WorldMapGrid`가 **실제 표면에 레이캐스트**해 앉힌다.
// 연결 안 해도 지도는 뜬다(내용물은 기본 도형 마커). 프리팹을 꽂으면 그것이 이긴다 —
// `BiomeContentData`와 같은 규칙: 데이터가 없으면 기본 동작.
[CreateAssetMenu(fileName = "WorldTileVisuals", menuName = "DungeonColony/WorldTileVisuals")]
public class WorldTileVisualData : ScriptableObject
{
    [Serializable]
    public class BiomeVisual
    {
        public Biome Biome;

        [Tooltip("그 바이옴 지역의 지형 위에 세울 데코(나무·바위 등). 비우면 아무것도 안 세운다.")]
        public GameObject Prefab;

        [Range(0f, 1f), Tooltip("그 바이옴 칸 중 몇 할에 세울지. 1이면 전부.")]
        public float Density = 1f;

        [Tooltip("칸 중심에서 흩뜨리는 반경(월드 유닛). 0이면 정확히 중앙 — 격자가 드러난다.")]
        public float Scatter = 1f;

        [Tooltip("칸마다 무작위 Y회전을 준다(같은 프리팹이 반복돼 보이지 않게).")]
        public bool RandomYaw = true;

        [Tooltip("도착 메뉴(`WorldMenuPanel`) 왼쪽에 깔 지역 그림. 내용물 쪽 그림이 없을 때의 폴백. " +
                 "둘 다 비면 바이옴 색 단색.")]
        public Sprite MenuArt;
    }

    [Serializable]
    public class ContentVisual
    {
        public WorldNodeContent Content;

        [Tooltip("비우면 코드 생성 마커(내용물 색 큐브).")]
        public GameObject Prefab;

        [Tooltip("지형 표면에서 얼마나 띄울지.")]
        public float HeightOffset;

        [Tooltip("도착 메뉴(`WorldMenuPanel`) 왼쪽에 깔 그림. 바이옴 그림보다 이쪽이 우선.")]
        public Sprite MenuArt;
    }

    [SerializeField] List<BiomeVisual>   biomes   = new();
    [SerializeField] List<ContentVisual> contents = new();

    public BiomeVisual GetVisual(Biome b)
    {
        for (int i = 0; i < biomes.Count; i++)
            if (biomes[i] != null && biomes[i].Biome == b) return biomes[i];
        return null;
    }

    // 에디터 생성기가 바이옴별 항목을 채울 때 쓰는 진입점(없으면 만들어 준다).
    public BiomeVisual GetOrCreateVisual(Biome b)
    {
        var v = GetVisual(b);
        if (v != null) return v;
        v = new BiomeVisual { Biome = b };
        biomes.Add(v);
        return v;
    }

    public ContentVisual GetContent(WorldNodeContent c)
    {
        for (int i = 0; i < contents.Count; i++)
            if (contents[i] != null && contents[i].Content == c) return contents[i];
        return null;
    }
}
