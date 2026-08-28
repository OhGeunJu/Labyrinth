
#ifndef UNIVERSAL_TERRAIN_LIT_PASSES_INCLUDED
#define UNIVERSAL_TERRAIN_LIT_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

// ─────────────────────────────────────────────────────────────────────────────
// **매크로 변주 — 타일 반복을 깨는 유일한 실질적 수단**(DungeonColony, 2026-08-27)
//
// 타일 텍스처는 주기가 눈에 보인다. 타일을 키우고 스플랫으로 변형을 섞어도 **넓고 평평한
// 면에서는 여전히 "한 장으로 덮인 벽"으로 읽힌다** — 포켓몬 바이올렛의 절벽이 그 예다.
//
// 눈이 반복이라고 읽는 것은 무늬의 *모양*보다 **밝기와 색이 똑같다는 것**이다. 그래서
// 저주파 노이즈로 **알베도를 곱한다.** 무늬는 그대로지만 넓은 얼룩이 생겨 격자가 흩어진다.
//
// · 픽셀당 노이즈 몇 번뿐이라 **텍스처 페치가 안 는다**(스토캐스틱 타일링은 3배가 된다).
// · **월드 좌표로 뽑는다** — 청크마다 새로 계산해도 이음매에서 안 튄다.
// · 애드 패스에도 같이 들어간다(이 파일을 둘 다 include한다) — 한쪽만 곱하면 레이어가
//   다섯 장을 넘는 순간 **패스 경계에서 색이 갈린다.**
//
// 값은 여기서 고친다. 머티리얼 프로퍼티로 빼지 않은 이유: URP의 `UnityPerMaterial` CBUFFER를
// 건드리면 SRP 배처가 지형에서 깨진다.
// **값은 밖에서 넣는다** — `TerrainPalette`가 `Shader.SetGlobal*`로 밀어 준다.
//
// 머티리얼 프로퍼티가 아니라 **전역 유니폼**인 것이 요점이다: URP의 `UnityPerMaterial`
// CBUFFER를 건드리면 지형에서 SRP 배처가 깨지는데, 전역은 다른 버퍼라 안 깨진다.
// 덕분에 **인스펙터에서 만지면 화면이 바로 따라온다** — 이건 옳고 그름이 아니라 눈으로
// 맞추는 값이라, 고칠 때마다 셰이더를 다시 컴파일하게 두면 안 맞춰진다.
//
//   _DC_Macro : x=세기 · y=큰 주기(m) · z=중간 주기(m) · w=색조
//   _DC_Far   : x=섞기 시작(m) · y=완전히 큰 타일(m) · z=큰 타일 배율
float4 _DC_Macro;
float4 _DC_Far;

//   _DC_Tri   : x=세기(0이면 끔) · y=섞기 시작(법선 y) · z=완전히 옆면(법선 y) · w=청크 한 변(m)
float4 _DC_Tri;

// ─────────────────────────────────────────────────────────────────────────────
// **세계 색조 — 지역의 정체성**(2026-08-27)
//
// Terrain Layer는 *"여기 무엇이 깔려 있나"*에 답한다. 그런데 눈이 *"저긴 다른 곳이다"*라고
// 읽는 것은 재질이 아니라 **색**이다 — 같은 흙이라도 잿빛 골짜기와 붉은 협곡은 다르다.
// 그걸 레이어로 하려면 **색깔 수만큼 레이어를 늘려야** 하는데 URP 지형은 한 패스에 넉 장이라
// 금방 애드 패스가 늘어난다. **색조는 텍스처 한 장으로 세계 전체를 덮는 것이 맞다.**
//
// 켄시가 `fullmap.tif` 위에 색 오버레이를 얹는 그 자리다.
//
// · **매크로 변주와 다른 축이다** — 저쪽은 주기 62~340m로 *타일 반복을 깨는* 것이고,
//   이쪽은 km 단위로 *지역을 가르는* 것이다. 주파수 대역이 달라 겹쳐도 안 싸운다.
// · **알베도에 곱한다** — 0.5가 중립(×1)이고 저장은 그 절반이다. 그래서 0~1이 ×0~×2가 된다.
// · **바이옴 격자보다 잘다** — 격자는 지역 하나가 512m인데 이 그림은 텍셀이 16m라,
//   *"이 골짜기만 붉다"*처럼 격자로는 못 적는 것이 적힌다.
//
//   _DC_Tint : x=세기(0이면 끔) · y=세계 한 변(m)
float4 _DC_Tint;
TEXTURE2D(_DC_ColorMap);
SAMPLER(sampler_DC_ColorMap);

half3 DC_WorldTint(float3 positionWS)
{
    if (_DC_Tint.x <= 0.003) return half3(1, 1, 1);

    float2 uv = positionWS.xz / max(_DC_Tint.y, 1.0);
    half3 c = SAMPLE_TEXTURE2D(_DC_ColorMap, sampler_DC_ColorMap, uv).rgb * 2.0h;
    return lerp(half3(1, 1, 1), c, (half)_DC_Tint.x);
}

float DC_Hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// 값 노이즈 — 격자 네 귀퉁이를 부드럽게 섞는다. 펄린을 쓸 것도 없이 이 배율에서는 충분하다.
float DC_Noise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = DC_Hash(i);
    float b = DC_Hash(i + float2(1, 0));
    float c = DC_Hash(i + float2(0, 1));
    float d = DC_Hash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// **비탈은 위에서 내려다본 UV로는 늘어난다**(triplanar).
//
// 지형의 스플랫 UV는 언제나 `worldXZ × 타일`이다 — **위에서 눌러 찍는 것**이라 면이
// 설수록 세로로 잡아 늘여진다. 절벽이 흘러내린 듯 보이는 것이 그것이고, 켄시 글이
// *"수직 데이터는 평면 이미지로 표현할 수 없다"*고 한 자리다.
//
// 제대로 된 triplanar는 세 면을 다 뽑아 섞지만(샘플 3배), 지형은 **바닥이 이미 맞으므로**
// 옆면 하나만 더 있으면 된다. 어느 옆면인지는 법선의 x·z 중 누운 쪽이 정한다.
//
// ── 타일 크기를 되짚는 법 ──────────────────────────────────────────────────
//
// 스플랫 UV는 **청크 안에서 0~1인 좌표**(`uvMainAndLM.xy`)에 `_SplatN_ST`를 곱한 것이다.
// 그러니 **m당 타일 수 = `_SplatN_ST / 청크 한 변`**이고, 청크 한 변은 C#이 밀어 준다.
// **`positionWS`로 나누면 안 된다** — 그건 세계 좌표라 청크마다 배율이 달라진다.
//
// 가로는 **세계 좌표 그대로** 쓴다 — 청크 경계에서 그림이 끊기지 않게.
float2 DC_SideUV(float3 positionWS, float3 normalWS, float2 st)
{
    float2 perMeter = st / max(_DC_Tri.w, 1.0);

    bool useX = abs(normalWS.x) > abs(normalWS.z);
    float2 side = useX ? float2(positionWS.z, positionWS.y)
                       : float2(positionWS.x, positionWS.y);
    return side * perMeter;
}

// 알베도에 곱할 값. 1을 중심으로 오르내린다.
half3 DC_MacroVariation(float3 positionWS)
{
    if (_DC_Macro.x <= 0.001) return half3(1, 1, 1);

    float2 p = positionWS.xz;

    // 두 배율을 섞는다 — 큰 것만 쓰면 멀리서만 듣고, 작은 것만 쓰면 지저분해 보인다.
    float big = DC_Noise(p / _DC_Macro.y);
    float mid = DC_Noise(p / _DC_Macro.z + 17.3);
    float n   = big * 0.65 + mid * 0.35;

    half v = (half)((n - 0.5) * 2.0 * _DC_Macro.x);

    // 색조도 조금 흔든다 — 밝기만 흔들면 흑백 얼룩처럼 보인다. 채널마다 살짝 어긋나게.
    half3 tint = half3(1.0, 1.0 - _DC_Macro.w * 0.35, 1.0 - _DC_Macro.w * 0.7);
    return saturate(half3(1, 1, 1) + v * tint);
}
// ─────────────────────────────────────────────────────────────────────────────


struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 uvMainAndLM              : TEXCOORD0; // xy: control, zw: lightmap
    #ifndef TERRAIN_SPLAT_BASEPASS
        float4 uvSplat01                : TEXCOORD1; // xy: splat0, zw: splat1
        float4 uvSplat23                : TEXCOORD2; // xy: splat2, zw: splat3
    #endif

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half4 normal                    : TEXCOORD3;    // xyz: normal, w: viewDir.x
        half4 tangent                   : TEXCOORD4;    // xyz: tangent, w: viewDir.y
        half4 bitangent                 : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
    #else
        half3 normal                    : TEXCOORD3;
        half3 vertexSH                  : TEXCOORD4; // SH
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light
    #else
        half  fogFactor                 : TEXCOORD6;
    #endif

    float3 positionWS               : TEXCOORD7;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord              : TEXCOORD8;
    #endif

#if defined(DYNAMICLIGHTMAP_ON)
    float2 dynamicLightmapUV        : TEXCOORD9;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion           : TEXCOORD10;
#endif

    float4 clipPos                  : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings IN, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    inputData.positionWS = IN.positionWS;
    inputData.positionCS = IN.clipPos;

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = half3(IN.normal.w, IN.tangent.w, IN.bitangent.w);
        inputData.tangentToWorld = half3x3(-IN.tangent.xyz, IN.bitangent.xyz, IN.normal.xyz);
        inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
        half3 SH = 0;
    #elif defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
        float2 sampleCoords = (IN.uvMainAndLM.xy / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
        half3 normalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
        half3 tangentWS = cross(GetObjectToWorldMatrix()._13_23_33, normalWS);
        inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(-tangentWS, cross(normalWS, tangentWS), normalWS));
        half3 SH = IN.vertexSH;
    #else
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
        inputData.normalWS = IN.normal;
        half3 SH = IN.vertexSH;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = IN.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        inputData.fogCoord = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactorAndVertexLight.x);
        inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;
    #else
    inputData.fogCoord = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactor);
    #endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.clipPos);

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = IN.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = IN.uvMainAndLM.zw;
    #else
    inputData.vertexSH = SH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = IN.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings IN, inout InputData inputData)
{
    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
    half3 SH = 0;
    #else
    half3 SH = IN.vertexSH;
    #endif

#if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, inputData.positionCS.xy);
#elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(IN.uvMainAndLM.zw, IN.dynamicLightmapUV, SH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(IN.uvMainAndLM.zw);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(SH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        inputData.positionCS.xy,
        IN.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(IN.uvMainAndLM.zw, SH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(IN.uvMainAndLM.zw);
#endif
}

#ifndef TERRAIN_SPLAT_BASEPASS

void NormalMapMix(float4 uvSplat01, float4 uvSplat23, inout half4 splatControl, inout half3 mixedNormal)
{
    #if defined(_NORMALMAP)
        half3 nrm = half(0.0);
        nrm += splatControl.r * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, uvSplat01.xy), _NormalScale0);
        nrm += splatControl.g * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, uvSplat01.zw), _NormalScale1);
        nrm += splatControl.b * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, uvSplat23.xy), _NormalScale2);
        nrm += splatControl.a * UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, uvSplat23.zw), _NormalScale3);

        // avoid risk of NaN when normalizing.
        #if !HALF_IS_FLOAT
            nrm.z += half(0.01);
        #else
            nrm.z += 1e-5f;
        #endif

        mixedNormal = normalize(nrm.xyz);
    #endif
}


void SplatmapMix(float4 uvMainAndLM, float4 uvSplat01, float4 uvSplat23, inout half4 splatControl, out half weight, out half4 mixedDiffuse, out half4 defaultSmoothness, inout half3 mixedNormal, float3 positionWS, float3 normalWS)
{
    half4 diffAlbedo[4];

    diffAlbedo[0] = SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uvSplat01.xy);
    diffAlbedo[1] = SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uvSplat01.zw);
    diffAlbedo[2] = SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uvSplat23.xy);
    diffAlbedo[3] = SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uvSplat23.zw);

    // **먼 데는 큰 타일로 갈아 끼우고**(거리 블렌드), **비탈은 옆면으로 바꾸어 뽑는다**(triplanar).
    //
    // 둘은 따로 도는 것이 아니라 **순서가 있다** — 거리를 먼저 섞고 그 위에 옆면을 얹는다.
    // 반대로 두면 먼 절벽에서 **거리 블렌드가 옆면을 통째로 덮어써서** 늘어난 그림이 돌아온다.
    // 그리고 옆면도 **같은 배율로** 뽑는다 — 한쪽만 큰 타일이면 배율이 갈려 경계에 띠가 생긴다.
    float camDist = distance(positionWS, _WorldSpaceCameraPos);
    half  far = (half)saturate((camDist - _DC_Far.x) / max(1.0, _DC_Far.y - _DC_Far.x));

    // 가까운 데서는 분기로 통째로 건너뛴다(샘플이 두 배라 늘 뽑으면 비싸다).
    if (far > 0.003h)
    {
        diffAlbedo[0] = lerp(diffAlbedo[0], SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uvSplat01.xy * _DC_Far.z), far);
        diffAlbedo[1] = lerp(diffAlbedo[1], SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uvSplat01.zw * _DC_Far.z), far);
        diffAlbedo[2] = lerp(diffAlbedo[2], SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uvSplat23.xy * _DC_Far.z), far);
        diffAlbedo[3] = lerp(diffAlbedo[3], SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uvSplat23.zw * _DC_Far.z), far);
    }

    // 평지에서는 역시 분기로 통째로 건너뛴다 — **대부분의 픽셀은 값을 안 치른다.**
    if (_DC_Tri.x > 0.003)
    {
        half tri = (half)(saturate((_DC_Tri.y - abs(normalWS.y)) /
                                   max(0.001, _DC_Tri.y - _DC_Tri.z)) * _DC_Tri.x);
        if (tri > 0.003h)
        {
            float m = lerp(1.0, _DC_Far.z, far);   // 멀어지면 옆면도 같이 커진다

            float2 s0 = DC_SideUV(positionWS, normalWS, _Splat0_ST.xy) * m;
            float2 s1 = DC_SideUV(positionWS, normalWS, _Splat1_ST.xy) * m;
            float2 s2 = DC_SideUV(positionWS, normalWS, _Splat2_ST.xy) * m;
            float2 s3 = DC_SideUV(positionWS, normalWS, _Splat3_ST.xy) * m;

            diffAlbedo[0] = lerp(diffAlbedo[0], SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, s0), tri);
            diffAlbedo[1] = lerp(diffAlbedo[1], SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, s1), tri);
            diffAlbedo[2] = lerp(diffAlbedo[2], SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, s2), tri);
            diffAlbedo[3] = lerp(diffAlbedo[3], SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, s3), tri);
        }
    }

    // This might be a bit of a gamble -- the assumption here is that if the diffuseMap has no
    // alpha channel, then diffAlbedo[n].a = 1.0 (and _DiffuseHasAlphaN = 0.0)
    // Prior to coming in, _SmoothnessN is actually set to max(_DiffuseHasAlphaN, _SmoothnessN)
    // This means that if we have an alpha channel, _SmoothnessN is locked to 1.0 and
    // otherwise, the true slider value is passed down and diffAlbedo[n].a == 1.0.
    defaultSmoothness = half4(diffAlbedo[0].a, diffAlbedo[1].a, diffAlbedo[2].a, diffAlbedo[3].a);
    defaultSmoothness *= half4(_Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3);

#ifndef _TERRAIN_BLEND_HEIGHT // density blending
    if(_NumLayersCount <= 4)
    {
        // 20.0 is the number of steps in inputAlphaMask (Density mask. We decided 20 empirically)
        half4 opacityAsDensity = saturate((half4(diffAlbedo[0].a, diffAlbedo[1].a, diffAlbedo[2].a, diffAlbedo[3].a) - (1 - splatControl)) * 20.0);
        opacityAsDensity += 0.001h * splatControl;      // if all weights are zero, default to what the blend mask says
        half4 useOpacityAsDensityParam = { _DiffuseRemapScale0.w, _DiffuseRemapScale1.w, _DiffuseRemapScale2.w, _DiffuseRemapScale3.w }; // 1 is off
        splatControl = lerp(opacityAsDensity, splatControl, useOpacityAsDensityParam);
    }
#endif

    // Now that splatControl has changed, we can compute the final weight and normalize
    weight = dot(splatControl, 1.0h);

#ifdef TERRAIN_SPLAT_ADDPASS
    clip(weight <= 0.005h ? -1.0h : 1.0h);
#endif

#ifndef _TERRAIN_BASEMAP_GEN
    // Normalize weights before lighting and restore weights in final modifier functions so that the overal
    // lighting result can be correctly weighted.
    splatControl /= (weight + HALF_MIN);
#endif

    mixedDiffuse = 0.0h;
    mixedDiffuse += diffAlbedo[0] * half4(_DiffuseRemapScale0.rgb * splatControl.rrr, 1.0h);
    mixedDiffuse += diffAlbedo[1] * half4(_DiffuseRemapScale1.rgb * splatControl.ggg, 1.0h);
    mixedDiffuse += diffAlbedo[2] * half4(_DiffuseRemapScale2.rgb * splatControl.bbb, 1.0h);
    mixedDiffuse += diffAlbedo[3] * half4(_DiffuseRemapScale3.rgb * splatControl.aaa, 1.0h);

    NormalMapMix(uvSplat01, uvSplat23, splatControl, mixedNormal);
}

#endif

#ifdef _TERRAIN_BLEND_HEIGHT
void HeightBasedSplatModify(inout half4 splatControl, in half4 masks[4])
{
    // heights are in mask blue channel, we multiply by the splat Control weights to get combined height
    half4 splatHeight = half4(masks[0].b, masks[1].b, masks[2].b, masks[3].b) * splatControl.rgba;
    half maxHeight = max(splatHeight.r, max(splatHeight.g, max(splatHeight.b, splatHeight.a)));

    // Ensure that the transition height is not zero.
    half transition = max(_HeightTransition, 1e-5);

    // This sets the highest splat to "transition", and everything else to a lower value relative to that, clamping to zero
    // Then we clamp this to zero and normalize everything
    half4 weightedHeights = splatHeight + transition - maxHeight.xxxx;
    weightedHeights = max(0, weightedHeights);

    // We need to add an epsilon here for active layers (hence the blendMask again)
    // so that at least a layer shows up if everything's too low.
    weightedHeights = (weightedHeights + 1e-6) * splatControl;

    // Normalize (and clamp to epsilon to keep from dividing by zero)
    half sumHeight = max(dot(weightedHeights, half4(1, 1, 1, 1)), 1e-6);
    splatControl = weightedHeights / sumHeight.xxxx;
}
#endif

void SplatmapFinalColor(inout half4 color, half fogCoord)
{
    color.rgb *= color.a;

    #ifndef TERRAIN_GBUFFER // Technically we don't need fogCoord, but it is still passed from the vertex shader.

    #ifdef TERRAIN_SPLAT_ADDPASS
        color.rgb = MixFogColor(color.rgb, half3(0,0,0), fogCoord);
    #else
        color.rgb = MixFog(color.rgb, fogCoord);
    #endif

    #endif
}

void SetupTerrainDebugTextureData(inout InputData inputData, float2 uv)
{
    #if defined(DEBUG_DISPLAY)
        #if defined(TERRAIN_SPLAT_ADDPASS)
            if (_DebugMipInfoMode != DEBUGMIPINFOMODE_NONE)
            {
                discard; // Layer 4 & beyond are done additively, doesn't make sense for the mipmap streaming debug views -> stop.
            }
        #endif

        switch (_DebugMipMapTerrainTextureMode)
        {
            case DEBUGMIPMAPMODETERRAINTEXTURE_CONTROL:
                SETUP_DEBUG_TEXTURE_DATA_FOR_TEX(inputData, TRANSFORM_TEX(uv, _Control), _Control);
                break;
            case DEBUGMIPMAPMODETERRAINTEXTURE_LAYER0:
                SETUP_DEBUG_TEXTURE_DATA_FOR_TEX(inputData, TRANSFORM_TEX(uv, _Splat0), _Splat0);
                break;
            case DEBUGMIPMAPMODETERRAINTEXTURE_LAYER1:
                SETUP_DEBUG_TEXTURE_DATA_FOR_TEX(inputData, TRANSFORM_TEX(uv, _Splat1), _Splat1);
                break;
            case DEBUGMIPMAPMODETERRAINTEXTURE_LAYER2:
                SETUP_DEBUG_TEXTURE_DATA_FOR_TEX(inputData, TRANSFORM_TEX(uv, _Splat2), _Splat2);
                break;
            case DEBUGMIPMAPMODETERRAINTEXTURE_LAYER3:
                SETUP_DEBUG_TEXTURE_DATA_FOR_TEX(inputData, TRANSFORM_TEX(uv, _Splat3), _Splat3);
                break;
            default:
                break;
        }

        // TERRAIN_STREAM_INFO: no streamInfo will have been set (no MeshRenderer); set status to "6" to reflect in the debug status that this is a terrain
        // also, set the per-material status to "4" to indicate warnings
        inputData.streamInfo = TERRAIN_STREAM_INFO;
    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard Terrain shader
Varyings SplatmapVert(Attributes v)
{
    Varyings o = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    TerrainInstancing(v.positionOS, v.normalOS, v.texcoord);

    VertexPositionInputs Attributes = GetVertexPositionInputs(v.positionOS.xyz);

    o.uvMainAndLM.xy = v.texcoord;
    o.uvMainAndLM.zw = v.texcoord * unity_LightmapST.xy + unity_LightmapST.zw;

    #ifndef TERRAIN_SPLAT_BASEPASS
        o.uvSplat01.xy = TRANSFORM_TEX(v.texcoord, _Splat0);
        o.uvSplat01.zw = TRANSFORM_TEX(v.texcoord, _Splat1);
        o.uvSplat23.xy = TRANSFORM_TEX(v.texcoord, _Splat2);
        o.uvSplat23.zw = TRANSFORM_TEX(v.texcoord, _Splat3);
    #endif

#if defined(DYNAMICLIGHTMAP_ON)
    o.dynamicLightmapUV = v.texcoord * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(Attributes.positionWS);
        float4 vertexTangent = float4(cross(float3(0, 0, 1), v.normalOS), 1.0);
        VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, vertexTangent);

        o.normal = half4(normalInput.normalWS, viewDirWS.x);
        o.tangent = half4(normalInput.tangentWS, viewDirWS.y);
        o.bitangent = half4(normalInput.bitangentWS, viewDirWS.z);
    #else
        o.normal = TransformObjectToWorldNormal(v.normalOS);
        OUTPUT_SH4(Attributes.positionWS, o.normal.xyz, GetWorldSpaceNormalizeViewDir(Attributes.positionWS), o.vertexSH, o.probeOcclusion);
    #endif

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(Attributes.positionCS.z);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        o.fogFactorAndVertexLight.x = fogFactor;
        o.fogFactorAndVertexLight.yzw = VertexLighting(Attributes.positionWS, o.normal.xyz);
    #else
        o.fogFactor = fogFactor;
    #endif

    o.positionWS = Attributes.positionWS;
    o.clipPos = Attributes.positionCS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        o.shadowCoord = GetShadowCoord(Attributes);
    #endif

    return o;
}

void ComputeMasks(out half4 masks[4], half4 hasMask, Varyings IN)
{
    masks[0] = 0.5h;
    masks[1] = 0.5h;
    masks[2] = 0.5h;
    masks[3] = 0.5h;

#ifdef _MASKMAP
    masks[0] = lerp(masks[0], SAMPLE_TEXTURE2D(_Mask0, sampler_Mask0, IN.uvSplat01.xy), hasMask.x);
    masks[1] = lerp(masks[1], SAMPLE_TEXTURE2D(_Mask1, sampler_Mask0, IN.uvSplat01.zw), hasMask.y);
    masks[2] = lerp(masks[2], SAMPLE_TEXTURE2D(_Mask2, sampler_Mask0, IN.uvSplat23.xy), hasMask.z);
    masks[3] = lerp(masks[3], SAMPLE_TEXTURE2D(_Mask3, sampler_Mask0, IN.uvSplat23.zw), hasMask.w);
#endif

    masks[0] *= _MaskMapRemapScale0.rgba;
    masks[0] += _MaskMapRemapOffset0.rgba;
    masks[1] *= _MaskMapRemapScale1.rgba;
    masks[1] += _MaskMapRemapOffset1.rgba;
    masks[2] *= _MaskMapRemapScale2.rgba;
    masks[2] += _MaskMapRemapOffset2.rgba;
    masks[3] *= _MaskMapRemapScale3.rgba;
    masks[3] += _MaskMapRemapOffset3.rgba;
}

// Used in Standard Terrain shader
#ifdef TERRAIN_GBUFFER
GBufferFragOutput SplatmapFragment(Varyings IN)
#else
void SplatmapFragment(
    Varyings IN
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
    )
#endif
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
#ifdef _ALPHATEST_ON
    ClipHoles(IN.uvMainAndLM.xy);
#endif

    half3 normalTS = half3(0.0h, 0.0h, 1.0h);
#ifdef TERRAIN_SPLAT_BASEPASS
    half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMainAndLM.xy).rgb;
    albedo *= DC_MacroVariation(IN.positionWS);   // 매크로 변주 — 원경도 같이 흔들어야 경계가 안 생긴다
    albedo *= DC_WorldTint(IN.positionWS);        // 세계 색조 — 지역의 정체성
    half smoothness = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMainAndLM.xy).a;
    half metallic = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MetallicTex, IN.uvMainAndLM.xy).r;
    half alpha = 1;
    half occlusion = 1;
#else

    half4 hasMask = half4(_LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3);
    half4 masks[4];
    ComputeMasks(masks, hasMask, IN);

    float2 splatUV = (IN.uvMainAndLM.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);

    half alpha = dot(splatControl, 1.0h);
#ifdef _TERRAIN_BLEND_HEIGHT
    // disable Height Based blend when there are more than 4 layers (multi-pass breaks the normalization)
    if (_NumLayersCount <= 4)
        HeightBasedSplatModify(splatControl, masks);
#endif

    half weight;
    half4 mixedDiffuse;
    half4 defaultSmoothness;
    SplatmapMix(IN.uvMainAndLM, IN.uvSplat01, IN.uvSplat23, splatControl, weight, mixedDiffuse, defaultSmoothness, normalTS, IN.positionWS, IN.normal.xyz);
    half3 albedo = mixedDiffuse.rgb;
    albedo *= DC_MacroVariation(IN.positionWS);   // 매크로 변주
    albedo *= DC_WorldTint(IN.positionWS);        // 세계 색조

    half4 defaultMetallic = half4(_Metallic0, _Metallic1, _Metallic2, _Metallic3);
    half4 defaultOcclusion = half4(_MaskMapRemapScale0.g, _MaskMapRemapScale1.g, _MaskMapRemapScale2.g, _MaskMapRemapScale3.g) +
                            half4(_MaskMapRemapOffset0.g, _MaskMapRemapOffset1.g, _MaskMapRemapOffset2.g, _MaskMapRemapOffset3.g);

    half4 maskSmoothness = half4(masks[0].a, masks[1].a, masks[2].a, masks[3].a);
    defaultSmoothness = lerp(defaultSmoothness, maskSmoothness, hasMask);
    half smoothness = dot(splatControl, defaultSmoothness);

    half4 maskMetallic = half4(masks[0].r, masks[1].r, masks[2].r, masks[3].r);
    defaultMetallic = lerp(defaultMetallic, maskMetallic, hasMask);
    half metallic = dot(splatControl, defaultMetallic);

    half4 maskOcclusion = half4(masks[0].g, masks[1].g, masks[2].g, masks[3].g);
    defaultOcclusion = lerp(defaultOcclusion, maskOcclusion, hasMask);
    half occlusion = dot(splatControl, defaultOcclusion);
#endif

    InputData inputData;
    InitializeInputData(IN, normalTS, inputData);
    SetupTerrainDebugTextureData(inputData, IN.uvMainAndLM.xy);

#if defined(_DBUFFER)
    half3 specular = half3(0.0h, 0.0h, 0.0h);
    ApplyDecal(IN.clipPos,
        albedo,
        specular,
        inputData.normalWS,
        metallic,
        occlusion,
        smoothness);
#endif

    InitializeBakedGIData(IN, inputData);

#ifdef TERRAIN_GBUFFER

    BRDFData brdfData;
    InitializeBRDFData(albedo, metallic, /* specular */ half3(0.0h, 0.0h, 0.0h), smoothness, alpha, brdfData);

    // Baked lighting.
    half4 color;
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
    color.rgb = GlobalIllumination(brdfData, (BRDFData)0, 0, inputData.bakedGI, occlusion, inputData.positionWS,
                                   inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
    color.a = alpha;
    SplatmapFinalColor(color, inputData.fogCoord);

    // Dynamic lighting: emulate SplatmapFinalColor() by scaling gbuffer material properties. This will not give the same results
    // as forward renderer because we apply blending pre-lighting instead of post-lighting.
    // Blending of smoothness and normals is also not correct but close enough?
    brdfData.albedo.rgb *= alpha;
    brdfData.diffuse.rgb *= alpha;
    brdfData.specular.rgb *= alpha;
    brdfData.reflectivity *= alpha;
    inputData.normalWS = inputData.normalWS * alpha;
    smoothness *= alpha;

    return PackGBuffersBRDFData(brdfData, inputData, smoothness, color.rgb, occlusion);
#else

    half4 color = UniversalFragmentPBR(inputData, albedo, metallic, /* specular */ half3(0.0h, 0.0h, 0.0h), smoothness, occlusion, /* emission */ half3(0, 0, 0), alpha);

    SplatmapFinalColor(color, inputData.fogCoord);

    outColor = half4(color.rgb, 1.0h);

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
#endif
}

// Shadow pass

// Shadow Casting Light geometric parameters. These variables are used when applying the shadow Normal Bias and are set by UnityEngine.Rendering.Universal.ShadowUtils.SetupShadowCasterConstantBuffer in com.unity.render-pipelines.universal/Runtime/ShadowUtils.cs
// For Directional lights, _LightDirection is used when applying shadow Normal Bias.
// For Spot lights and Point lights, _LightPosition is used to compute the actual light direction because it is different at each shadow caster geometry vertex.
float3 _LightDirection;
float3 _LightPosition;

struct AttributesLean
{
    float4 position     : POSITION;
    float3 normalOS       : NORMAL;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsLean
{
    float4 clipPos      : SV_POSITION;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

VaryingsLean ShadowPassVertex(AttributesLean v)
{
    VaryingsLean o = (VaryingsLean)0;
    UNITY_SETUP_INSTANCE_ID(v);
    TerrainInstancing(v.position, v.normalOS, v.texcoord);

    float3 positionWS = TransformObjectToWorld(v.position.xyz);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 clipPos = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    clipPos.z = min(clipPos.z, UNITY_NEAR_CLIP_VALUE);
#else
    clipPos.z = max(clipPos.z, UNITY_NEAR_CLIP_VALUE);
#endif

    o.clipPos = clipPos;

    o.texcoord = v.texcoord;

    return o;
}

half4 ShadowPassFragment(VaryingsLean IN) : SV_TARGET
{
#ifdef _ALPHATEST_ON
    ClipHoles(IN.texcoord);
#endif
    return 0;
}

// Depth pass

VaryingsLean DepthOnlyVertex(AttributesLean v)
{
    VaryingsLean o = (VaryingsLean)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    TerrainInstancing(v.position, v.normalOS);
    o.clipPos = TransformObjectToHClip(v.position.xyz);
    o.texcoord = v.texcoord;
    return o;
}

half4 DepthOnlyFragment(VaryingsLean IN) : SV_TARGET
{
#ifdef _ALPHATEST_ON
    ClipHoles(IN.texcoord);
#endif
#ifdef SCENESELECTIONPASS
    // We use depth prepass for scene selection in the editor, this code allow to output the outline correctly
    return half4(_ObjectId, _PassValue, 1.0, 1.0);
#endif
    return IN.clipPos.z;
}

#endif
