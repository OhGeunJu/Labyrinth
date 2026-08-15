using System.Collections.Generic;
using UnityEngine;

// 무리에 낀 사람 하나 — **명세로만 산다.**
//
// 실제 `UnitBrain`을 찍어 두지 않는 이유: 지도에 무리가 스물이면 폰 백 마리가 꺼진 채로
// 씬에 앉아 있게 된다. 지도 위에서 필요한 것은 "얼마나 센가"뿐이고, 그건 `PawnSpec`이 답한다.
// 실제 폰이 필요한 순간은 하나뿐이다 — 약탈대가 거점에 닿아 **필드맵에 서야 할 때**.
// 그래서 프리팹을 같이 들고 다닌다.
public class BandMember
{
    public GameObject Prefab;
    public UnitData   Unit;
    public PawnSpec   Spec;

    public string Name => Spec != null ? Spec.Name : (Unit != null ? Unit.RaceName : "이름 없음");
}

// **지도 위에 실제로 서 있는 무리 하나.**
//
// `WorldBandData`가 종류(무엇이 될 수 있는가)라면 이쪽은 개체다 — `MerchantData`와 `Shop`의
// 관계와 같다. 위치·목적지·남은 인원이 여기 있고, 죽으면 이것만 사라진다.
public class WorldBand
{
    public WorldBandData Data;
    public readonly List<BandMember> Members = new();

    // 어디서 나왔나. 순찰대가 도는 중심이자, 약탈대가 소굴을 잃으면 갈 곳을 잃는 근거.
    public Vector2Int Home;

    // 지금 향하는 칸. 도착하면 행동별로 다음 일이 벌어진다.
    public Vector2Int Goal;

    // 지도 위 위치(월드 좌표). 격자에 매이지 않는다 — 플레이어 원정대와 같은 규칙이다.
    public Vector3    Position;
    public Vector2Int Node;

    // 쫓는 중이면 그 지점. 목적지(`Goal`)와 별개다 — 추격을 접으면 원래 가던 곳으로 돌아간다.
    public Vector3 ChaseAt;
    public bool    Chasing;

    // **다가오는 원정대를 기다리는 중.** 적대가 아닌 무리는 도망칠 이유가 없으므로 멈춰 선다.
    // 없으면 상단·순찰대는 원정대와 속도가 비슷해 **영영 따라잡을 수 없고**, 그러면 지도 위의
    // 중립 무리는 말을 걸 수 없는 그림이 된다.
    public bool Waiting;

    // **떠돌이 무리는 언제까지 밖에 있나**(게임 초). 지나면 근거지로 돌아가고, 닿으면 병력이
    // 소굴로 복귀한다. 무한정 떠돌게 두면 지도가 무리로 차서 새 무리가 못 나오고
    // (`maxBands`), 소굴을 쳐도 이미 나온 것들은 그대로 남는다.
    public float ExpiresAt;
    public bool  Returning;

    // **끌고 다니는 포로.** 싸움에 진 원정대가 여기 실린다 — 진 것이 끝이 아니라 붙잡혀
    // 어딘가로 끌려가는 것이고, 되찾을 길이 남는다(탈출 · 그 무리를 이겨 구출 · 소굴을 침).
    //
    // 폰은 꺼진 채로 산다(원정 나간 멤버와 같은 상태) — 필드맵에 서 있으면 안 되고,
    // 그렇다고 지워 버리면 이름·스탯·특성이 통째로 사라진다.
    public readonly List<UnitBrain> Captives = new();

    // 상단이 지고 다니는 것. **무리마다 따로 돈다** — 같은 `MerchantData`를 쓰는 상단이
    // 둘이어도 한쪽에서 산 물건이 다른 쪽에서 사라지면 안 된다(`Shop`의 규약 그대로).
    public Shop Shop;

    // 조우 메뉴를 이미 한 번 띄운 상대. 도망친 뒤 같은 프레임에 또 뜨는 것을 막는다.
    public float NoContactUntil;

    public bool IsAlive => Members.Count > 0;
    public int  Count   => Members.Count;

    public string Name => Data != null ? Data.BandName : "무리";

    public FactionData Faction => Data != null ? Data.Faction : null;

    public WorldBandBehavior Behavior => Data != null ? Data.Behavior : WorldBandBehavior.Patrol;

    public string Summary => $"{Name} · {Count}명";

    // ── 전력 ──────────────────────────────────────────────────────────────────

    // 무리 전체의 전투력. 매 프레임 묻지 않으므로 캐시하지 않는다 —
    // 캐시하면 전투로 인원이 준 뒤 갱신을 빠뜨리는 사고가 생긴다.
    public float Power
    {
        get
        {
            float sum = 0f;
            foreach (var m in Members) sum += WorldBattle.PowerOf(m);
            return sum * (Data != null ? Data.PowerMultiplier : 1f);
        }
    }

    // ── 굴리기 ────────────────────────────────────────────────────────────────

    // 명세에서 무리 하나를 뽑는다. **시드 하나에서 전부 나온다** — 같은 시드면 같은 무리다
    // (마을 후보·매장 자원과 같은 규칙). 무리는 지도에 오래 남지 않으므로 결정성이 화면에
    // 드러날 일은 적지만, 이 저장소의 굴림은 전부 시드에서 나온다는 규약을 깨지 않는다.
    public static WorldBand Roll(WorldBandData data, Vector2Int home, int seed)
    {
        if (data == null || !data.IsUsable) return null;

        var band = new WorldBand { Data = data, Home = home, Goal = home };
        var rng  = new System.Random(seed);

        int i = 0;
        foreach (var entry in data.Members)
        {
            if (entry == null || entry.Prefab == null) continue;

            int lo = Mathf.Max(0, Mathf.Min(entry.MinCount, entry.MaxCount));
            int hi = Mathf.Max(0, Mathf.Max(entry.MinCount, entry.MaxCount));
            int n  = lo + rng.Next(hi - lo + 1);

            var unit = UnitOf(entry.Prefab);
            for (int k = 0; k < n; k++, i++)
            {
                // 개체 시드는 무리 시드에서 갈라 나온다 — 멤버마다 다른 사람이 되게.
                var spec = PawnSpec.Roll(unit, unchecked(seed * 31 + i * 7919),
                                         entry.MinGrade, entry.MaxGrade);
                band.Members.Add(new BandMember { Prefab = entry.Prefab, Unit = unit, Spec = spec });
            }
        }

        if (band.Members.Count == 0) return null;

        // 상단의 세력이 곧 가게의 세력이다 — 우호도가 값에 얹힌다(`Shop.Faction`).
        if (data.Behavior == WorldBandBehavior.Caravan && data.Merchant != null)
            band.Shop = new Shop(data.Merchant, data.Faction);

        return band;
    }

    // 프리팹에 배선된 종족 데이터. 프리팹 에셋에서도 읽힌다(직렬화된 값이라).
    static UnitData UnitOf(GameObject prefab)
    {
        var brain = prefab != null ? prefab.GetComponent<UnitBrain>() : null;
        return brain != null ? brain.Data : null;
    }

    // ── 손실 ──────────────────────────────────────────────────────────────────

    // 전투에서 잃은 만큼 덜어낸다. **약한 사람부터 죽는다** — 두목이 잡졸보다 먼저 쓰러지면
    // 무리를 여러 번 두들겨 약화시키는 일이 성립하지 않는다(마지막 한 명이 늘 최약체가 된다).
    // 실제로 받은 손실을 돌려준다 — 요청한 수가 인원보다 많을 수 있다.
    public int TakeLosses(int count)
    {
        if (count <= 0 || Members.Count == 0) return 0;

        Members.Sort((a, b) => WorldBattle.PowerOf(a).CompareTo(WorldBattle.PowerOf(b)));

        int take = Mathf.Min(count, Members.Count);
        Members.RemoveRange(0, take);
        return take;
    }
}
