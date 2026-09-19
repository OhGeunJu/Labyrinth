using System.Collections.Generic;
using UnityEngine;

// **폰마다 스텐실 번호 하나 — 몸통의 바깥 선이 "제 몸"만 피하게**(2026-09-19).
//
// 몸통 선(`ToonOutlineOuter`)은 제 몸통이 칠한 자리를 건너뛰어 배 한가운데의 가로선을 없앤다. 그 자리를
// 한 값으로 적으면 **모든 폰의 몸이 같은 값**이라 앞 폰의 선이 뒤 폰의 몸 위에서 사라진다(사용자:
// *"이건 해결해야해"*). 그래서 폰마다 번호를 달리 준다.
//
// - **스텐실 번호는 렌더 상태라 `MaterialPropertyBlock`으로 못 준다** — 재질을 복제해야 한다. 복제하는 것은
//   `_StencilRef`를 가진 재질 중 **적거나 읽는 것**뿐이고(몸통 · 얼굴 살 · 코 · 바깥 선) 한 폰 안에서는 원본 하나에 사본 하나다.
// - **번호는 1~127을 돈다** — 128번째 폰부터 겹치는데, 틀리는 것은 **같은 번호의 두 폰이 화면에서 겹칠
//   때 그 경계의 선 하나**뿐이다(0은 안 쓴다 — 아무것도 안 적힌 자리의 값이다).
// - **맨 위 비트(128)는 "얼굴"이다**(2026-09-19) — 코 선은 **제 얼굴 위에서만** 비키고 제 손·몸통·다른 폰 위에서는
//   서야 한다(사용자: *"머리처럼 다른 부위랑 겹쳐졌을때도 외곽선이 보여야지"* · *"근데 머리랑은 외곽선이 보이면 안돼"*).
//   번호 하나로는 *"폰의 몸인가"*까지만 묻는다 — 몸통 선을 폰마다 가를 때 배운 *"내 몸인가"*를 한 칸 더 좁혀
//   **"내 얼굴인가"**를 물으려면 얼굴이 따로 적혀야 한다. 얼굴인지는 재질이 든다(`_StencilFace`).
// - **재질을 바꾸는 코드가 생기면 이것을 다시 부를 것**(`Apply`) — 원본으로 갈아 끼우면 번호가 기본값(1)으로 돌아간다.
[DisallowMultipleComponent]
public class PawnOutlineId : MonoBehaviour
{
    static readonly int StencilRef = Shader.PropertyToID("_StencilRef");
    static readonly int WriteMask  = Shader.PropertyToID("_StencilWriteMask");
    static readonly int FaceFlag   = Shader.PropertyToID("_StencilFace");

    // 얼굴 표시 — 번호(1~127)에 얹는 비트. 재질을 굽는 도구도 이 값을 쓴다(씬 뷰 기본값 1 + 128).
    public const int FaceBit = 128;
    static int RefFor(Material m, int id) => m.HasProperty(FaceFlag) && m.GetFloat(FaceFlag) > 0.5f ? id | FaceBit : id;

    // 번호가 뜻을 갖는 재질인가 — 적는 쪽(쓰기 마스크 > 0)이거나 읽는 쪽(쓰기 마스크가 없는 선 재질).
    // 손·팔·귀의 살도 `_StencilRef`를 갖지만 **안 적으므로** 복제하지 않는다(머리 살은 얼굴이라 적는다).
    static bool Uses(Material m)
        => m.HasProperty(StencilRef) && (!m.HasProperty(WriteMask) || m.GetFloat(WriteMask) > 0f);
    static int _next;

    readonly List<Material> _clones = new();
    public int Id { get; private set; }

    void Awake()
    {
        Id = _next % (FaceBit - 1) + 1;
        _next++;
        Apply();
    }

    public void Apply()
    {
        var copies = new Dictionary<Material, Material>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || !Uses(m)) continue;
                if (_clones.Contains(m)) { m.SetInt(StencilRef, RefFor(m, Id)); continue; }   // 이미 제 사본이다
                if (!copies.TryGetValue(m, out var c))
                {
                    c = new Material(m) { name = m.name + $" (폰 {Id})" };
                    c.SetInt(StencilRef, RefFor(c, Id));
                    copies[m] = c;
                    _clones.Add(c);
                }
                mats[i] = c;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    void OnDestroy()
    {
        foreach (var c in _clones) if (c != null) Destroy(c);
        _clones.Clear();
    }
}
