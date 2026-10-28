using System.Collections.Generic;
using UnityEngine;

// **세계에 실제로 있는 장소를 좌표로 찾는다**(2026-08-27).
//
// 이어진 세계로 오면서 *"이 근처에 마을이 있나"*에 답할 것이 없어졌다. 지금까지는 **저작한
// 격자를 전수 훑었다** — `WorldBandManager`가 실제로 64×64 = 4,096칸을 돌며 마을을 모으고,
// `WorldLairs`·`TownMarket`·`TownRecruits`가 전부 `Vector2Int` 노드를 키로 쓴다.
//
// 그 격자에는 두 가지 문제가 있다:
//
//   ① **손으로 놓은 것이 안 보인다.** 저작 씬에서 세운 마을은 격자에 안 찍혀 있으므로
//      게임 지도에도, 무리가 찾는 마을 목록에도 없다. **세계에 있는데 아무도 모른다.**
//   ② **한 지역에 하나뿐이다.** 격자 한 칸(256m)이 곧 주소라, 소굴 둘을 나란히 못 놓는다.
//
// **주소를 격자에서 `Id`로 옮기면 둘 다 풀린다.** 자리는 월드 좌표이고, 신원은 굽기가
// 발급한 `WorldObject.Id`다 — 둘 다 이미 있다.
//
// ── 왜 `Resources.LoadAll`인가 ──────────────────────────────────────────────
//
// `TerrainHeight.DeltaOf`는 일부러 **한 청크씩** 읽는다(손댄 청크만 에셋이 있어서, 통째로
// 읽으면 없는 것까지 훑는다). 여기는 반대다 — **지도가 세계 전체를 한 번에 그리고**, 무리는
// 가 보지 않은 곳의 마을도 알아야 한다(`WorldLairs.BuildAll`이 미리 다 깨우는 그 이유).
// 게다가 장소는 저작한 것이라 수백~수천 개로 묶여 있다. **전부 필요하면 전부 읽는 것이 맞다.**
public static class WorldSites
{
    public readonly struct Site
    {
        public readonly string           Id;
        public readonly string           Name;
        public readonly WorldNodeContent Kind;
        public readonly Vector2          Position;   // 월드 XZ

        public Site(string id, string name, WorldNodeContent kind, Vector2 pos)
        {
            Id = id; Name = name; Kind = kind; Position = pos;
        }

        // 높이는 묻는 자리에서 낸다 — **저장하지 않는다**(지형을 다시 조각하면 어긋난다).
        public Vector3 World => new(Position.x, TerrainHeight.At(Position.x, Position.y), Position.y);

        public bool IsValid => !string.IsNullOrEmpty(Id);
    }

    static List<Site> _all;

    public static IReadOnlyList<Site> All { get { Ensure(); return _all; } }

    // 구운 것이 바뀌면 부른다(`WorldContentAssets.Flush`). 안 비우면 **저작 도구가 옛 세계를
    // 계속 본다.**
    public static void Forget() => _all = null;

    // **판이 열릴 때 비운다** — "Enter Play Mode Options"에서 도메인 리로드를 끄면 지난 판의
    // 목록을 그대로 들고 시작한다(`WorldContentStreamer`가 정적 캐시에 대해 지키는 그 규칙).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _all = null;

    static void Ensure()
    {
        if (_all != null) return;
        _all = new List<Site>();

        foreach (var chunk in Resources.LoadAll<WorldChunkContent>(WorldChunkContent.ResourceDir))
        {
            if (chunk == null || chunk.Objects == null) continue;

            foreach (var o in chunk.Objects)
            {
                // **`Empty`는 장소가 아니다** — 장식·소품·폰이다. 색인에 넣으면 지도가
                // 물건으로 뒤덮이고, *"가장 가까운 마을"*이 바위를 짚는다.
                if (o == null || o.Kind == WorldNodeContent.Empty) continue;

                // **세계 밖은 안 담는다**(2026-08-27). 세계를 줄이면 밖으로 나간 청크의
                // 에셋이 `Resources/`에 그대로 남는데, `LoadAll`은 그것까지 읽는다 —
                // 안 거르면 **지도 밖의 마을이 "가장 가까운 마을"로 뽑혀** 무리가 세계
                // 밖으로 걸어 나간다. (에셋 자체는 `5. Bake World`가 지운다.)
                if (o.Position.x < 0f || o.Position.y < 0f ||
                    o.Position.x >= TerrainGrid.WorldSize || o.Position.y >= TerrainGrid.WorldSize) continue;

                _all.Add(new Site(o.Id, o.Name, o.Kind, o.Position));
            }
        }
    }

    // ── 찾기 ────────────────────────────────────────────────────────────────

    public static IEnumerable<Site> Of(WorldNodeContent kind)
    {
        Ensure();
        foreach (var s in _all) if (s.Kind == kind) yield return s;
    }

    public static Site ById(string id)
    {
        Ensure();
        if (string.IsNullOrEmpty(id)) return default;
        foreach (var s in _all) if (s.Id == id) return s;
        return default;
    }

    // 가장 가까운 것. **못 찾으면 `IsValid`가 거짓인 것을 준다** — `null`을 돌려주는 형태로
    // 두면 부르는 쪽마다 검사를 빠뜨린다.
    public static Site Nearest(WorldNodeContent kind, Vector3 from)
    {
        Ensure();
        Site best = default;
        float bestSqr = float.MaxValue;

        foreach (var s in _all)
        {
            if (s.Kind != kind) continue;
            float dx = s.Position.x - from.x, dz = s.Position.y - from.z;
            float d = dx * dx + dz * dz;
            if (d < bestSqr) { bestSqr = d; best = s; }
        }
        return best;
    }

}
