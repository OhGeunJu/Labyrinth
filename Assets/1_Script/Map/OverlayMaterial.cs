using UnityEngine;
using UnityEngine.Rendering;

// **세상 위에 얹는 선의 머티리얼 — 한 벌**(2026-08-23).
//
// 건설 격자(`GridOverlay`)와 영역 원(`RangeCircle`)이 같은 것을 필요로 한다: 정점 색 ·
// 알파 블렌딩 · **깊이 판정 없음**. 각자 만들면 한쪽만 고쳐져서, 실제로 핵의 원을 고친 다음
// **격자만 바닥에 묻힌 채로 남았다.**
//
// ── 왜 깊이 판정을 끄나 ────────────────────────────────────────────────────
//
// 이것들은 *어디까지가 내 땅인가* · *칸이 어디서 나뉘는가*를 알려 주는 **표시**지 세상에 놓인
// 물건이 아니다. 바닥을 깔면 그 아래로 들어가 **정작 건설할 때 안 보인다** — 격자가 안 보이는
// 건설 격자는 아무것도 아니다.
//
// ── 그래도 UI 위로는 안 올라온다 ───────────────────────────────────────────
//
// 스크린 스페이스 오버레이 캔버스는 **카메라 렌더가 끝난 뒤에 따로** 그려지므로, 세상의 깊이
// 판정과 무관하게 언제나 위다. 이것이 `GL` 즉시 모드와 갈리는 자리다 — 그쪽은 카메라 렌더
// *끝*에 끼어들어 캔버스와 순서를 다투고, 누가 이기는지가 렌더 파이프라인 버전에 달렸다.
public static class OverlayMaterial
{
    public static Material Create()
    {
        // **`Hidden/Internal-Colored`가 먼저다** — 그쪽만 `_ZTest`를 노출하고, 그게 이 파일의
        // 전부다. `Sprites/Default`는 그 속성이 없어 깊이 판정을 못 끈다(폴백으로만 둔다).
        var shader = RuntimeShaders.Find(RuntimeShaders.InternalColored, RuntimeShaders.SpritesDefault);
        var mat = new Material(shader)
        {
            hideFlags   = HideFlags.HideAndDontSave,
            renderQueue = 3000,                       // 투명 — 불투명 지오메트리 다음
        };

        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_Cull",     (int)CullMode.Off);
        mat.SetInt("_ZWrite",   0);
        mat.SetInt("_ZTest",    (int)CompareFunction.Always);
        return mat;
    }
}
