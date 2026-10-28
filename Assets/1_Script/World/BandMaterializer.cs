using System.Collections.Generic;
using UnityEngine;

// **멀 땐 토큰, 가까우면 실체**(2026-08-26).
//
// 세계가 29km로 이어지면서 월드맵 레이어의 자리가 바뀌었다 — **별도 화면**은 죽지만
// *"멀리 있는 것의 추상화"*는 여전히 필요하다. `WorldBand`가 지도를 도는 것은 그대로 두되,
// **활성 반경에 들어오면 실제 폰으로 세우고 나가면 도로 거둔다.**
//
// ── 이것이 사용자가 말한 그 느낌이다 ────────────────────────────────────────
//
// > *"켄시를 보면 적 부대가 이동하다가 동맹 영지를 공격하기도 하는데, 필드맵에서는 갑자기
// > 딱 생겨서 쳐들어오는 느낌"*
//
// 습격이 맵 가장자리에서 **생성되는** 것이 아니라, 며칠 전부터 지도를 걸어온 무리가
// **지평선 너머에서 걸어 들어온다.** 스폰이 사라지고 도착만 남는다.
//
// ── 왜 `SimLocality`와 같은 축인가 ──────────────────────────────────────────
//
// 그쪽은 폰을 **재우고 깨우고**, 이쪽은 **없애고 만든다** — 같은 물음(*"여기가 지금 도는
// 곳인가"*)의 두 단계다. 반경을 따로 두면 *"폰은 도는데 무리는 토큰인"* 띠가 생긴다.
public class BandMaterializer : MonoBehaviour
{
    [Tooltip("이 반경(m) 안에 들어온 무리를 실제 폰으로 세운다. 비우면 `SimLocality.Radius`.")]
    [SerializeField] float radius = 0f;

    [Tooltip("나갈 때는 조금 더 멀어져야 거둔다. **같은 값이면 경계에서 섰다 사라졌다 한다.**")]
    [SerializeField] float hysteresis = 200f;

    [Tooltip("한 프레임에 세우는 무리 수. 여럿이 한꺼번에 들어오면 프레임이 튄다.")]
    [SerializeField, Range(1, 4)] int perFrame = 1;

    [Tooltip("한 프레임에 거두는 무리 수. **거두는 것도 공짜가 아니다** — 무리 하나가 폰 여럿이라 " +
             "카메라를 멀리 옮기면 서 있던 것이 **한 프레임에 통째로 파괴된다**. " +
             "걸어 다닐 때는 하나씩이라 이 값이 뭐든 티가 안 나고, 순간이동할 때만 갈린다.")]
    [SerializeField, Range(1, 4)] int reapPerFrame = 2;

    // 실체가 된 무리 — 토큰 ↔ 그 폰들.
    readonly Dictionary<WorldBand, List<UnitBrain>> _live = new();
    readonly List<WorldBand> _stale = new();

    float Radius => radius > 0.01f ? radius : SimLocality.Radius;

    void Update()
    {
        var mgr = WorldBandManager.Instance;
        if (mgr == null || !SimLocality.Active) return;

        Vector3 center = SimLocality.Center;

        // ① 실체를 거둔다 — **먼저 거둔다.** 나중에 하면 잠깐 둘 다 서 있다.
        //
        // **여기도 한 프레임에 몇씩이다**(2026-08-27) — 무리 하나가 폰 여럿이라, 순간이동하면
        // 서 있던 것이 한꺼번에 파괴돼 프레임이 튄다. 남은 것은 다음 프레임에 다시 걸린다.
        //
        // **자리 동기화는 예산과 무관하게 전부 돈다** — 그건 파괴가 아니라 값 하나 쓰는
        // 일이고, 건너뛰면 그 프레임에 거둬진 무리가 **원래 자리로 순간이동**한다.
        _stale.Clear();
        foreach (var kv in _live)
        {
            var band = kv.Key;
            if (band == null || !band.IsAlive)
            {
                if (_stale.Count < reapPerFrame) _stale.Add(band);
                continue;
            }

            // **토큰의 자리를 폰이 정한다** — 실체인 동안 무리가 실제로 움직였으니, 안 옮기면
            // 거둘 때 **원래 자리로 순간이동**한다.
            SyncTokenPosition(band, kv.Value);

            if (Far(band.Position, center, Radius + hysteresis) && _stale.Count < reapPerFrame)
                _stale.Add(band);
        }
        foreach (var b in _stale) Dematerialize(b);

        // ② 들어온 것을 세운다
        int budget = perFrame;
        foreach (var band in mgr.Bands)
        {
            if (budget <= 0) break;
            if (band == null || !band.IsAlive || _live.ContainsKey(band)) continue;
            if (Far(band.Position, center, Radius)) continue;

            Materialize(band);
            budget--;
        }
    }

    static bool Far(Vector3 a, Vector3 b, float r) => (a - b).sqrMagnitude > r * r;

    // ── 세우기 ──────────────────────────────────────────────────────────────

    void Materialize(WorldBand band)
    {
        var pawns = new List<UnitBrain>();
        _live[band] = pawns;

        for (int i = 0; i < band.Members.Count; i++)
        {
            var m = band.Members[i];
            if (m?.Prefab == null) continue;

            // 무리가 한 점에 서 있으면 서로 밀어내다 흩어진다 — 처음부터 벌려 세운다.
            float ang = i / Mathf.Max(1f, band.Members.Count) * Mathf.PI * 2f;
            float rad = 1.5f + i * 0.4f;
            float x = band.Position.x + Mathf.Cos(ang) * rad;
            float z = band.Position.z + Mathf.Sin(ang) * rad;

            // **바위 속에 세우지 않는다**(2026-08-30). 무리는 토큰일 때 지형만 보고 걸어서
            // **바위 한복판에 서 있을 수 있다** — 그대로 실체화하면 폰이 벽에 낀 채
            // `EscapeRock`이 매번 경고만 찍고(그 반경으로는 못 빠져나온다) 습격이 영영
            // 도착하지 않는다. **자리를 묻는 문은 `RockMap.TryNearestOpen` 하나다.**
            if (RockMap.TryNearestOpen(new Vector3(x, 0f, z), 24f, out var open))
            {
                x = open.x;
                z = open.z;
            }

            // **지면 위에 세우는 길은 `PawnPlacement` 하나다** — 피벗이 발밑이라 그냥 찍으면
            // 캡슐 절반이 파묻히고, 유니티가 겹침을 풀며 옆으로 밀어내다 가장자리면 떨어진다.
            float y = MapManager.Instance != null
                ? MapManager.Instance.SampleGroundHeight(x, z)
                : TerrainHeight.At(x, z);

            var go = PawnSpawn.Instantiate(m.Prefab, m.Spec);   // 명세의 성별이 몸을 고른다
            PawnPlacement.PlaceOnGround(go, new Vector3(x, y, z));

            var brain = go.GetComponent<UnitBrain>();
            if (brain != null)
            {
                // **명세를 입힌다** — 지도에서 본 이름·스탯 그대로 서야 한다. 안 그러면
                // `Awake`가 새로 굴려서 **살펴본 무리와 마주친 무리가 다른 사람**이 된다.
                if (m.Spec != null) brain.ApplySpec(m.Spec);
                pawns.Add(brain);
            }
        }
    }

    // ── 거두기 ──────────────────────────────────────────────────────────────

    void Dematerialize(WorldBand band)
    {
        if (!_live.TryGetValue(band, out var pawns)) return;
        _live.Remove(band);
        if (band == null) return;

        // **죽은 만큼 토큰에서도 뺀다.** 안 빼면 눈앞에서 몰살한 무리가 지도에서 멀쩡히
        // 걸어 다니고, 다시 만나면 되살아난다.
        int alive = 0;
        foreach (var b in pawns)
        {
            if (b == null || b.gameObject == null) continue;
            var bb = b.Blackboard;
            bool dead = b.GetComponent<Corpse>() != null || (bb != null && bb.IsKnockedOut);
            if (!dead) alive++;
            Destroy(b.gameObject);
        }

        int losses = band.Members.Count - alive;
        if (losses > 0) band.TakeLosses(losses);
    }

    // 실체인 동안 폰들이 실제로 걸었으므로 토큰이 따라간다 — 살아 있는 것들의 평균 자리.
    static void SyncTokenPosition(WorldBand band, List<UnitBrain> pawns)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var b in pawns)
        {
            if (b == null) continue;
            sum += b.transform.position;
            n++;
        }
        if (n > 0) band.Position = sum / n;
    }

    void OnDisable()
    {
        foreach (var b in new List<WorldBand>(_live.Keys)) Dematerialize(b);
    }
}
