using System.Collections.Generic;
using UnityEngine;

// **세계 어디든 천장을 답한다 — 로드 여부와 무관하게**(2026-08-29).
//
// `TerrainHeight`의 짝이고 **모양이 똑같다**: 노이즈 + 저작한 특징 + 손댄 델타. 같은 모양인
// 것이 요점이다 — 저작 도구도 굽기도 스트리밍도 그쪽 것을 그대로 옮겨 쓴다. 지하로
// 내려가면서 새로 생기는 개념은 **천장 하나뿐**이고, 그 천장조차 이미 있는 물건의 사본이다.
//
// ── 담는 값은 "여유"다, 절대 높이가 아니다 ─────────────────────────────────
//
// 근거는 `TerrainCeilingProfile`에 적혀 있다. 요약하면 **값 하나가 곧 "지나갈 수 있나"**이고,
// **바닥을 다시 구워도 안 어긋난다.** 그래서 델타에 저장되는 것도 여유다.
//
//     천장 높이 = TerrainHeight.At(x,z) + 여유(x,z)
//
// ── 바위와 천장은 같은 것이다 ──────────────────────────────────────────────
//
// 여유가 0 아래로 내려간 자리는 천장 면이 지면 아래로 내려간다는 뜻이고, 그림으로는
// **바위가 땅을 뚫고 올라온 것**으로 보인다. 벽을 따로 세우지 않는다 — 벽은 천장이 내려앉은
// 자리일 뿐이다. 이것이 동굴 생성의 전부다.
public static class TerrainCeiling
{
    // **켜져 있나.** 씬의 `TerrainStreamer`가 넣어 준다. 꺼져 있으면 `HasHeadroom`이 언제나
    // 참이라 **지상 세계의 판정이 한 줄도 안 바뀐다** — 지하가 아직 저작 안 된 씬에서
    // 폰이 이유 없이 갇히지 않게 하는 장치다.
    public static bool Enabled;

    public static TerrainCeilingProfile Profile = TerrainCeilingProfile.Default;

    // **저작한 천장 특징** — 큰 방(+)과 기둥(−). `TerrainFeature`를 **그대로 재사용한다**:
    // 좌표와 반경과 세기로 값을 얹는 물건이라 축이 높이든 여유든 하는 일이 같다. 같은
    // 모양을 하나 더 만들면 저작 도구도 두 벌이 된다.
    public static TerrainFeature[] Features = System.Array.Empty<TerrainFeature>();

    // `TerrainHeight.OwnedPerChunk`와 **같은 값을 써야 한다** — 가장자리 정점의 주인을
    // 정하는 규칙이 갈리면 바닥과 천장의 이음매가 서로 다른 자리에서 생긴다.
    public const int OwnedPerChunk = TerrainGrid.VertsPerAxis - 1;

    // ── 여유의 노이즈 ───────────────────────────────────────────────────────

    // **저장 안 되는 것 전부** — 노이즈 + 저작한 특징. 굽는 도구가 이 값과 견줘 "손댄 것"을
    // 가려내므로, 특징이 여기 안 들어가면 **저작한 방 전체가 델타로 구워진다.**
    // (`TerrainHeight.Procedural`이 같은 이유로 특징을 포함한다.)
    public static float ProceduralClearance(float worldX, float worldZ)
    {
        var p = Profile;
        float s = p.Seed * 0.6151f;   // 시드를 좌표 오프셋으로 — 유니티 펄린에 시드 인자가 없다

        // **바닥과 다른 오프셋을 쓴다.** 같은 오프셋이면 천장이 바닥과 같은 무늬로 오르내려
        // 여유가 어디서나 일정해진다 — 방도 바위도 안 생긴다.
        float c = p.BaseClearance;
        c += TerrainHeight.Octave(worldX, worldZ, s,          p.CavernPeriod) * p.CavernAmplitude;
        c += TerrainHeight.Octave(worldX, worldZ, s + 4409f,  p.TunnelPeriod) * p.TunnelAmplitude;
        c += TerrainHeight.Octave(worldX, worldZ, s + 8677f,  p.RoughPeriod)  * p.RoughAmplitude;

        var f = Features;
        for (int i = 0; i < f.Length; i++) c += f[i].Contribution(worldX, worldZ);
        return c;
    }

    // ── 델타 ────────────────────────────────────────────────────────────────
    //
    // `TerrainHeight`와 **같은 규칙**이다: 청크 단위로 게으르게 읽고 **없다는 사실도 기억한다**.
    // 에셋 타입도 `TerrainDelta`를 그대로 쓴다 — 담긴 숫자의 뜻만 다르다(높이 → 여유).
    static readonly Dictionary<Vector2Int, Dictionary<int, float>> _delta = new();

    public const string DeltaPath = "Ceiling";   // Resources/Ceiling/{x}_{y}.asset

    static Dictionary<int, float> DeltaOf(Vector2Int chunk)
    {
        if (_delta.TryGetValue(chunk, out var d)) return d;

        var asset = Resources.Load<TerrainDelta>($"{DeltaPath}/{TerrainGrid.KeyOf(chunk)}");
        d = null;
        if (asset != null && asset.Index != null && asset.Height != null)
        {
            int n = Mathf.Min(asset.Index.Length, asset.Height.Length);
            if (n > 0)
            {
                d = new Dictionary<int, float>(n);
                for (int i = 0; i < n; i++) d[asset.Index[i]] = asset.Height[i];
            }
        }
        _delta[chunk] = d;    // **null도 넣는다** — "없음"을 기억하는 것이 요점이다
        return d;
    }

    public static void ForgetDeltas() => _delta.Clear();

    // ── 정점 ────────────────────────────────────────────────────────────────

    // **전역 정점 좌표로 묻는다** — 가장자리에서 어느 청크의 값인지가 갈리면 이음매가 생긴다.
    public static float VertexClearanceGlobal(int gx, int gz)
    {
        var chunk = new Vector2Int(
            Mathf.Clamp(gx / OwnedPerChunk, 0, TerrainGrid.ChunksPerAxis - 1),
            Mathf.Clamp(gz / OwnedPerChunk, 0, TerrainGrid.ChunksPerAxis - 1));

        var d = DeltaOf(chunk);
        if (d != null)
        {
            int lx = gx - chunk.x * OwnedPerChunk;
            int lz = gz - chunk.y * OwnedPerChunk;
            if (lx >= 0 && lz >= 0 && lx < TerrainGrid.VertsPerAxis && lz < TerrainGrid.VertsPerAxis
                && d.TryGetValue(lz * TerrainGrid.VertsPerAxis + lx, out float c))
                return c;
        }

        return ProceduralClearance(gx * TerrainGrid.VertexSpacing, gz * TerrainGrid.VertexSpacing);
    }

    public static float VertexClearance(Vector2Int chunk, int vx, int vz) =>
        VertexClearanceGlobal(chunk.x * OwnedPerChunk + vx, chunk.y * OwnedPerChunk + vz);

    // ── 임의 좌표 ───────────────────────────────────────────────────────────

    // 그 자리의 여유(m). **이중선형** — `TerrainHeight.At`과 같은 식이어야 한다. 갈리면
    // 그려진 천장과 길찾기가 재는 천장이 어긋나 **보이는 굴을 못 지나간다.**
    public static float ClearanceAt(float worldX, float worldZ)
    {
        float fx = worldX / TerrainGrid.VertexSpacing;
        float fz = worldZ / TerrainGrid.VertexSpacing;

        int gx = Mathf.FloorToInt(fx);
        int gz = Mathf.FloorToInt(fz);
        float tx = fx - gx;
        float tz = fz - gz;

        float c00 = VertexClearanceGlobal(gx,     gz);
        float c10 = VertexClearanceGlobal(gx + 1, gz);
        float c01 = VertexClearanceGlobal(gx,     gz + 1);
        float c11 = VertexClearanceGlobal(gx + 1, gz + 1);

        return Mathf.Lerp(Mathf.Lerp(c00, c10, tx), Mathf.Lerp(c01, c11, tx), tz);
    }

    public static float ClearanceAt(Vector3 worldPos) => ClearanceAt(worldPos.x, worldPos.z);

    // 천장의 **절대 높이**(m) — 그리는 쪽이 쓴다.
    //
    // **여유가 음수면 바닥 아래로 내려간다**(`SinkDepth`까지). 그래야 바위가 지면을 뚫고
    // 올라온 것처럼 보인다 — 0에서 자르면 천장 면과 지면이 정확히 같은 높이에서 만나
    // **z-파이팅으로 지글거린다.** 판정은 이 값을 안 쓴다(여유를 직접 본다).
    public static float At(float worldX, float worldZ)
    {
        float c = Mathf.Max(ClearanceAt(worldX, worldZ), -Mathf.Max(0f, Profile.SinkDepth));
        return TerrainHeight.At(worldX, worldZ) + c;
    }

    // ── 지나갈 수 있나 ──────────────────────────────────────────────────────

    // **폰이 서 있을 수 있는 머리 위 여유가 있나.**
    //
    // 이것이 없으면 **여유 0.3m짜리 틈으로 경로가 나고 폰이 천장에 머리를 박은 채 벽을
    // 민다** — 기단에서 겪은 것과 똑같은 사고다(길찾기가 높이를 모르면 경로는 태연히
    // 못 가는 곳으로 난다). 그때는 발밑이었고 이번엔 머리 위다.
    //
    // **꺼져 있으면 언제나 참이다** — 지상 씬의 판정을 한 줄도 안 바꾼다.
    public static bool HasHeadroom(float worldX, float worldZ) =>
        !Enabled || ClearanceAt(worldX, worldZ) >= PawnMover.BodyHeight;

    public static bool HasHeadroom(Vector3 worldPos) => HasHeadroom(worldPos.x, worldPos.z);
}
