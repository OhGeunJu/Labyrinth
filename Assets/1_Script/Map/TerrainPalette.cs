using System;
using System.Collections.Generic;
using UnityEngine;

// **지형에 무엇을 깔 것인가**(2026-08-27).
//
// 여기까지 지형은 **머티리얼 한 장**이었다 — 사막이든 설원이든 늪이든 걸어 들어가면 똑같은
// 색이었다. 지도는 바이옴 색으로 칠하는데 **실제 세계에서는 구별이 안 됐다**: *"저쪽은 어떤
// 곳인가"*가 지도에만 있고 발밑에는 없었다.
//
// ── 순서가 곧 스플랫 채널이다 ───────────────────────────────────────────────
//
// 청크는 런타임에 만들어지므로 **모든 청크가 같은 `TerrainLayer` 목록을 공유해야 한다.**
// 목록이 청크마다 다르면 3번 채널이 여기서는 모래, 저기서는 눈이 되어 **이음매에서 텍스처가
// 튄다.** 그래서 `Layers`를 여기서 한 번 만들고 세계가 그것만 쓴다.
//
// ── 몇 장까지 쓰나 ─────────────────────────────────────────────────────────
//
// **URP 지형은 한 패스에 네 장을 섞는다**(2026-08-27 확인 — 여덟이라고 적었던 것은 틀렸다.
// 여덟은 HDRP·빌트인 이야기다). 다섯 장부터는 **애드 패스**로 지형을 한 번 더 그린다:
//
//     레이어 1~4 → 1패스 · 5~8 → 2패스 · 9~12 → 3패스 · 13~16 → 4패스
//
// 알파맵도 **네 장당 텍스처 하나**다. 열여섯이면 청크당 64×64×4 = **64KB**.
//
// 그래서 상한은 "여덟"이 아니라 **패스를 몇 번 그릴 것인가**다. 바이옴이 일곱이니
// **바닥 일곱 + 변형 여섯 + 절벽 셋 = 열여섯**이 네 패스로 떨어진다 — 무거우면 변형이나
// 바이옴별 절벽을 비우면 그만큼 줄어든다.
//
// **변형이 여섯인 것은 동굴 하나를 비웠기 때문이다**(`TerrainPaletteCreator`) — 일곱 바이옴에
// 전부 채우면 열일곱이 되어 다섯 패스로 넘어간다. 지표에 거의 안 깔리는 바이옴이 접을 자리다.
[CreateAssetMenu(fileName = "TerrainPalette", menuName = "DungeonColony/TerrainPalette")]
public class TerrainPalette : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public Biome        Biome;
        public TerrainLayer Ground;

        // **같은 바닥의 다른 얼굴.** 비워도 된다 — 채우면 채널을 하나 더 먹는 대신
        // **넓은 얼룩으로 두 개가 섞여** 멀리서 보는 반복이 확 준다.
        public TerrainLayer Variant;

        // 이 바이옴의 절벽. 비우면 아래의 공용 절벽을 쓴다.
        //
        // **사막 절벽과 숲 절벽이 같으면 바이옴이 절반만 바뀐 것으로 보인다** — 바닥은
        // 모래인데 비탈은 이끼 낀 바위인 꼴이다. 같은 계열끼리는 나눠 쓰면 채널이 안 는다.
        public TerrainLayer Cliff;
    }

    [Tooltip("바이옴마다 바닥에 깔 것. 안 적은 바이옴은 첫 줄을 쓴다.")]
    public Entry[] Biomes = Array.Empty<Entry>();

    [Tooltip("급경사면에 깔 것 — **바이옴을 안 가린다.** 절벽은 어디서나 바위다.")]
    public TerrainLayer Cliff;

    [Tooltip("이 각도(도)를 넘으면 절벽으로 친다.")]
    [Range(10f, 70f)] public float CliffAngle = 30f;

    [Tooltip("절벽으로 넘어가는 폭(도). 0이면 칼같이 갈려 **등고선처럼 줄이 보인다.**")]
    [Range(1f, 30f)] public float CliffBlend = 12f;

    // ── 반복을 깨는 것 ─────────────────────────────────────────────────────
    //
    // 타일 텍스처는 **주기가 눈에 보인다** — `TileSize`가 2m였을 때는 256m 청크에 같은 그림이
    // 축당 128번 반복돼 격자처럼 읽혔다. 셋으로 깬다:
    //   ① 타일을 키운다(레이어 에셋의 `TileSize` — `9. Create Terrain Palette`가 정한다)
    //   ② **넓은 얼룩으로 변형과 섞는다**(아래)
    //   ③ 절벽 경계를 흔든다(아래) — 안 흔들면 산비탈에 등고선 같은 줄이 남는다

    [Header("반복 깨기")]
    [Tooltip("변형과 섞이는 얼룩의 크기(m). 시야보다 커야 **'저쪽은 색이 좀 다르네'**로 읽힌다 — " +
             "작으면 그냥 지저분해 보인다.")]
    [Range(50f, 1500f)] public float VariantPeriod = 420f;

    [Tooltip("변형이 얼마나 강하게 나오나. 0.5면 반반, 낮으면 가끔 얼룩진다.")]
    [Range(0f, 1f)] public float VariantAmount = 0.5f;

    [Tooltip("얼룩 경계를 밀어 구부리는 세기(주기 대비 비율). 0이면 **동글동글한 방울**만 나온다 — " +
             "크기가 다 비슷해서 그것대로 반복으로 읽힌다.")]
    [Range(0f, 1f)] public float VariantWarp = 0.35f;

    [Tooltip("절벽 경계를 흔드는 세기. 0이면 등고선처럼 매끈한 줄이 보인다.")]
    [Range(0f, 0.3f)] public float EdgeNoise = 0.12f;

    [Tooltip("그 흔들림의 잘기(m). 작을수록 자잘하게 삐죽하다.")]
    [Range(4f, 120f)] public float EdgePeriod = 26f;

    // ── 셰이더가 쓰는 값 ───────────────────────────────────────────────────
    //
    // 위의 것들은 **알파맵을 구울 때** 쓰이고(4m 격자), 아래는 **픽셀마다** 쓰인다.
    // 알파맵으로는 못 하는 것 — 알베도를 곱하고, 거리로 타일을 갈아 끼우는 것 — 이 여기 있다.
    //
    // **전역 유니폼으로 민다**(`PushToShader`) — 머티리얼 프로퍼티로 두면 URP의
    // `UnityPerMaterial` CBUFFER를 건드려 지형에서 SRP 배처가 깨진다.

    // ── 침식이 아는 것 ─────────────────────────────────────────────────────
    //
    // 경사만 보면 **물이 지난 데와 아닌 데가 똑같다.** 침식 마스크(`TerrainHeightmap`)가
    // 있으면 그 둘을 가를 수 있다 — 이것이 켄시가 World Machine에서 뽑아 쓰는 그 정보다.

    [Header("침식 마스크")]
    [Tooltip("깎여서 암반이 드러난 자리에 **절벽 텍스처**를 얼마나 얹나. " +
             "능선과 물길 옆면이 맨바위가 된다. 0이면 안 쓴다.")]
    [Range(0f, 1f)] public float WearToRock = 0.55f;

    [Tooltip("퇴적된 자리에 **변형 텍스처**를 얼마나 얹나. 골짜기 바닥이 다른 바닥이 된다 — " +
             "새 채널을 안 쓰고 이미 있는 변형을 재활용한다.")]
    [Range(0f, 1f)] public float DepositToVariant = 0.7f;

    [Header("셰이더 — 매크로 변주")]
    [Tooltip("저주파 노이즈로 알베도를 곱하는 세기. **넓고 평평한 면이 '한 장으로 덮인 벽'으로 " +
             "읽히는 것**을 깨는 유일한 수단이다. 0.5를 넘으면 얼룩덜룩해진다.")]
    [Range(0f, 0.8f)] public float MacroStrength = 0.30f;

    [Tooltip("큰 얼룩의 주기(m) — *'저쪽은 색이 좀 다르네'*.")]
    [Range(80f, 1200f)] public float MacroPeriodBig = 340f;

    [Tooltip("중간 얼룩의 주기(m) — **격자를 실제로 부수는 층**이다.")]
    [Range(15f, 300f)] public float MacroPeriodMid = 62f;

    [Tooltip("밝기만이 아니라 색조도 얼마나 흔드나. 0이면 흑백 얼룩처럼 보인다.")]
    [Range(0f, 1f)] public float MacroTint = 0.35f;

    [Header("셰이더 — 거리 블렌드")]
    [Tooltip("이 거리(m)부터 큰 타일과 섞기 시작. **카메라가 늘 보는 거리보다 멀게 둘 것** — " +
             "RTS처럼 높이서 내려다보면 화면 전체가 전환 구간에 들어가, 가까운 쪽의 선명한 " +
             "디테일을 영영 못 보고 알베도 샘플만 늘 두 배가 된다.")]
    [Range(10f, 400f)] public float FarStart = 70f;

    [Tooltip("이 거리(m)부터는 큰 타일만. 사이가 좁으면 화면에 전환 띠가 보인다.")]
    [Range(40f, 1200f)] public float FarEnd = 350f;

    [Tooltip("먼 쪽 타일 배율. 작을수록 크다 — 0.17이면 약 6배.")]
    [Range(0.05f, 1f)] public float FarScale = 0.17f;

    // ── 옆면 투영(triplanar) ────────────────────────────────────────────
    //
    // 스플랫 UV는 **위에서 눌러 찍는 것**이라 면이 설수록 세로로 늘어난다.
    // 침식을 넣은 뒤에 급경사가 언제나 늘었으므로 이것 없이는 골짜기 벽이 전부 흘러내린 듯 보인다.
    [Header("셰이더 — 옆면 투영")]
    [Tooltip("비탈에서 옆면 UV를 얼마나 쓰나. 0이면 끕다. **1이 기본**이다 — " +
             "반쪽만 쓰면 늘어난 그림이 그대로 절반 남는다.")]
    [Range(0f, 1f)] public float TriplanarStrength = 1f;

    [Tooltip("이 법선 y부터 옆면을 섞기 시작. 0.8이면 약 37°부터다.")]
    [Range(0.3f, 1f)] public float TriplanarStart = 0.80f;

    [Tooltip("이 법선 y부터는 옆면만. 시작과 너무 붙으면 경계에 띄가 보인다.")]
    [Range(0f, 0.9f)] public float TriplanarFull = 0.45f;

    // ── 세계 색조 ──────────────────────────────────────────────────────────
    //
    // **레이어와 다른 물음에 답한다.** 레이어는 *"여기 무엇이 깔려 있나"*이고 이건
    // *"이 지역이 무슨 색인가"*다 — 같은 흙이라도 잿빛 골짜기와 붉은 협곡은 달라야 하는데,
    // 그걸 레이어로 하면 **색깔 수만큼 레이어가 늘어** 한 패스 넉 장을 금방 넘긴다.
    //
    // **여기 두는 이유는 레이어 목록과 같다** — 세계가 한 벌이어야 한다. 청크마다 다르면
    // 이음매에서 색이 튄다.
    [Header("셰이더 — 세계 색조")]
    [Tooltip("세계 전체를 덮는 색조 한 장(`C. Bake World Color Map`이 만든다). " +
             "**0.5가 중립**이고 저장은 그 절반이라 0~1이 ×0~×2가 된다. 비우면 꺼진다.")]
    public Texture2D WorldColor;

    [Tooltip("얼마나 얹나. 1이면 그림 그대로, 0이면 안 쓴다. **바이옴 레이어가 이미 " +
             "성격을 들고 있으므로** 여기서 더 미는 것은 지역 안의 변화다.")]
    [Range(0f, 1f)] public float WorldColorStrength = 0.7f;

    // ── 채널 목록 — 한 번 만들고 캐싱한다 ──────────────────────────────────
    //
    // **중복을 접는다.** 두 바이옴이 같은 텍스처를 쓰면 채널도 하나여야 한다 — 안 접으면
    // 여덟 칸이 금방 넘치고, 같은 그림이 두 채널에서 반쯤씩 섞여 흐릿해진다.
    TerrainLayer[] _layers;
    Dictionary<Biome, int> _channel;
    Dictionary<Biome, int> _variant;
    Dictionary<Biome, int> _cliff;
    int _cliffChannel = -1;

    public TerrainLayer[] Layers { get { Build(); return _layers; } }

    public int ChannelOf(Biome b)
    {
        Build();
        return _channel.TryGetValue(b, out int i) ? i : 0;
    }

    // 변형이 없으면 **본래 채널을 돌려준다** — 부르는 쪽이 있나 없나를 매번 묻지 않게.
    public int VariantOf(Biome b)
    {
        Build();
        return _variant.TryGetValue(b, out int i) ? i : ChannelOf(b);
    }

    // 바이옴별 절벽. 없으면 공용 절벽, 그것도 없으면 그 바이옴의 바닥.
    public int CliffOf(Biome b)
    {
        Build();
        if (_cliff.TryGetValue(b, out int i)) return i;
        return _cliffChannel >= 0 ? _cliffChannel : ChannelOf(b);
    }

    // 저작 창에서 값을 만지면 다시 만든다.
    void OnValidate() { Forget(); PushToShader(); }

    public void Forget() { _layers = null; _channel = null; _variant = null; _cliff = null; _cliffChannel = -1; }

    void Build()
    {
        if (_layers != null) return;

        var list = new List<TerrainLayer>();
        _channel = new Dictionary<Biome, int>();
        _variant = new Dictionary<Biome, int>();
        _cliff   = new Dictionary<Biome, int>();

        foreach (var e in Biomes)
        {
            if (e.Ground == null) continue;

            int i = list.IndexOf(e.Ground);
            if (i < 0) { i = list.Count; list.Add(e.Ground); }
            _channel[e.Biome] = i;

            if (e.Cliff != null)
            {
                int c = list.IndexOf(e.Cliff);
                if (c < 0) { c = list.Count; list.Add(e.Cliff); }
                _cliff[e.Biome] = c;
            }

            if (e.Variant == null || e.Variant == e.Ground) continue;
            int v = list.IndexOf(e.Variant);
            if (v < 0) { v = list.Count; list.Add(e.Variant); }
            _variant[e.Biome] = v;
        }

        if (Cliff != null)
        {
            _cliffChannel = list.IndexOf(Cliff);
            if (_cliffChannel < 0) { _cliffChannel = list.Count; list.Add(Cliff); }
        }

        // **비어 있으면 안 된다** — 레이어가 하나도 없는 `TerrainData`는 회색으로 뜨고
        // 원인이 화면에 안 나온다.
        if (list.Count == 0) _cliffChannel = -1;

        _layers = list.ToArray();

        // **네 장마다 패스가 하나씩 는다** — 열일곱 장이면 청크 하나를 다섯 번 그리고,
        // 저작 씬처럼 청크가 여든 장 서 있으면 그만큼 배치가 는다. 못 쓸 정도는 아니지만
        // **모르고 넘기면 "왜 갑자기 무겁지"**가 되므로 몇 패스인지 알린다.
        int passes = Mathf.CeilToInt(_layers.Length / 4f);
        if (passes > 4)
            Debug.LogWarning($"[팔레트] 레이어 {_layers.Length}장 = 지형을 **{passes}번** 그린다. " +
                             "무거우면 변형(`Variant`)이나 바이옴별 절벽을 비울 것.");
    }

    public bool IsUsable => Layers != null && Layers.Length > 0;

    // ── 셰이더에 밀기 ───────────────────────────────────────────────────────
    //
    // **부르는 자리가 둘이다** — 런타임(`TerrainStreamer.Apply`)과 저작 도구
    // (`TerrainAuthoring.SyncValues`). 한쪽만 밀면 에디터에서 본 땅과 게임의 땅이 다르게 보인다.
    static readonly int MacroId = Shader.PropertyToID("_DC_Macro");
    static readonly int FarId   = Shader.PropertyToID("_DC_Far");
    static readonly int TriId   = Shader.PropertyToID("_DC_Tri");
    static readonly int TintId  = Shader.PropertyToID("_DC_Tint");
    static readonly int MapId    = Shader.PropertyToID("_DC_ColorMap");

    public void PushToShader()
    {
        Shader.SetGlobalVector(MacroId, new Vector4(MacroStrength, MacroPeriodBig, MacroPeriodMid, MacroTint));

        // **끝이 시작보다 앞이면 안 된다** — 나눗셈이 뒤집혀 가까운 데가 먼 것으로 잡힌다.
        Shader.SetGlobalVector(FarId, new Vector4(FarStart, Mathf.Max(FarEnd, FarStart + 1f), FarScale, 0f));

        // **청크 한 변을 같이 밀어야 한다** — 셰이더는 스플랫 UV가 청크 안에서 0~1인
        // 좌표에 곱해진 것임만 알 뿐, 그 청크가 몇 m인지를 모른다. 안 밀면
        // **m당 타일 수를 모르고 옆면만 말도 안 되는 배율로 깔린다.**
        Shader.SetGlobalVector(TriId, new Vector4(
            TriplanarStrength, TriplanarStart,
            Mathf.Min(TriplanarFull, TriplanarStart - 0.01f),   // 뒤집히면 나눗셈이 반대로 도는다
            TerrainGrid.ChunkSize));

        // **그림이 없으면 세기를 0으로 눌러서 끈다** — 안 누르면 셰이더가 안 물린 텍스처를
        // 뽑는데, 그때 무엇이 오는지는 플랫폼이 정한다(검정이면 세계가 통째로 까매진다).
        float tint = WorldColor != null ? WorldColorStrength : 0f;
        Shader.SetGlobalTexture(MapId, WorldColor != null ? (Texture)WorldColor : Texture2D.grayTexture);
        Shader.SetGlobalVector(TintId, new Vector4(tint, TerrainGrid.WorldSize, 0f, 0f));
    }
}
