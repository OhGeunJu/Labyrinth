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
// - **씬 뷰에서도 번호를 준다**(2026-11-03, 사용자: 겹쳐 세운 오우거 사이의 선이 씬 뷰에서 사라졌다 · *"씬뷰도 고쳐줘"*) — 판에서만 돌던
//   동안은 씬 뷰의 폰이 모두 재질 기본값(1)이라 겹친 자리의 선이 비켰다. 에디터에서는 사본을 **저장하지 않는 재질**(`HideAndDontSave`)로
//   끼우고 원본을 적어 둔다 — 씬을 저장하는 동안(`sceneSaving` → `sceneSaved`) · 꺼질 때(도메인 리로드 · Play 진입) 원본으로 되돌린다.
//   안 되돌리면 씬이 사라질 사본을 가리킨 채 저장돼 재질이 빈다. **프리팹 편집 화면 · 도구가 연 프리팹(미리보기 씬)에서는 안 한다** —
//   거기서 저장하면 사본이 프리팹에 박힌다(`ChibiPawnCreator`의 `LoadPrefabContents`도 미리보기 씬이다).
// - **프리팹을 다시 구우면 씬의 폰이 분홍이 됐다**(2026-11-03, `Pawn/3` 직후 고블린 · 사람 · 오크 — 사용자 화면). 씬의 프리팹 인스턴스에 재질을
//   끼우면 유니티는 그것을 **인스턴스의 덮어쓰기**로 기억한다. 프리팹이 바뀌면 인스턴스를 다시 세우며 그 덮어쓰기를 다시 얹는데, 사본은 그 사이
//   지워져 칸이 비었다(Missing). 그래서 끼울 때 **빈 칸은 프리팹 원본 재질로 메우고**(`SourceMats`) · 원본도 프리팹에서 읽고 · 인스턴스가
//   갱신되면(`prefabInstanceUpdated`) 다시 끼운다.
[ExecuteAlways]
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
    readonly Dictionary<Renderer, Material[]> _originals = new();   // 에디터에서만 — 저장 · 끌 때 되돌릴 원본
    public int Id { get; private set; }

    void Awake()
    {
        if (!Application.isPlaying) return;
        Id = _next % (FaceBit - 1) + 1;
        _next++;
        Apply();
    }

    public void Apply()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !EditLive()) return;
#endif
        var copies = new Dictionary<Material, Material>();
        int healed = 0;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            var before = (Material[])mats.Clone();
            bool changed = false;
#if UNITY_EDITOR
            // 에디터 — 원본은 프리팹이 답한다 · 지워진 사본을 가리키는 빈 칸은 그것으로 메운다(위 머리말).
            if (!Application.isPlaying && SourceMats(r) is Material[] src && src.Length == mats.Length)
            {
                before = (Material[])src.Clone();
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == null && src[i] != null) { mats[i] = src[i]; changed = true; healed++; }
            }
#endif
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || !Uses(m)) continue;
                if (_clones.Contains(m)) { m.SetInt(StencilRef, RefFor(m, Id)); continue; }   // 이미 제 사본이다
                if (!copies.TryGetValue(m, out var c))
                {
                    c = new Material(m) { name = m.name + $" (폰 {Id})" };
                    if (!Application.isPlaying) c.hideFlags = HideFlags.HideAndDontSave;
                    c.SetInt(StencilRef, RefFor(c, Id));
                    copies[m] = c;
                    _clones.Add(c);
                }
                mats[i] = c;
                changed = true;
            }
            if (!changed) continue;
            if (!Application.isPlaying && !_originals.ContainsKey(r)) _originals[r] = before;
            r.sharedMaterials = mats;
        }
        if (healed > 0) Debug.Log($"[외곽선] {name} — 빈 재질 {healed}칸을 프리팹 원본으로 메웠다(지워진 씬 뷰 사본을 가리키던 칸).", this);
    }

    void OnDestroy()
    {
        if (!Application.isPlaying) { Restore(); return; }
        foreach (var c in _clones) if (c != null) Destroy(c);
        _clones.Clear();
    }

    // 원본 재질로 되돌리고 사본을 버린다(에디터에서만 부른다).
    void Restore()
    {
        foreach (var kv in _originals)
            if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
        _originals.Clear();
        foreach (var c in _clones) if (c != null) DestroyImmediate(c);
        _clones.Clear();
    }

#if UNITY_EDITOR
    static int _nextEdit;
    static readonly HashSet<PawnOutlineId> _live = new();
    static bool _hooked;

    // 이 렌더러의 프리팹 원본 재질 — 프리팹 인스턴스가 아니면 null.
    static Material[] SourceMats(Renderer r)
    {
        var src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(r);
        return src != null ? src.sharedMaterials : null;
    }

    // 열린 씬의 폰인가 — 프리팹 편집 화면 · 도구가 연 프리팹(미리보기 씬)은 아니다.
    bool EditLive() => gameObject.scene.IsValid() && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene);

    void OnEnable()
    {
        if (Application.isPlaying || !EditLive()) return;
        if (Id == 0) { Id = _nextEdit % (FaceBit - 1) + 1; _nextEdit++; }
        _live.Add(this);
        Hook();
        Apply();
    }

    void OnDisable()
    {
        if (Application.isPlaying) return;
        Restore();
        _live.Remove(this);
    }

    static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (scene, _) =>
        {
            foreach (var p in _live) if (p != null && p.gameObject.scene == scene) p.Restore();
        };
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += scene =>
        {
            foreach (var p in _live) if (p != null && p.isActiveAndEnabled && p.gameObject.scene == scene) p.Apply();
        };
        // 프리팹이 바뀌어 씬의 인스턴스가 다시 섰다 — 렌더러가 새것이라 옛 기록은 버리고 다시 끼운다(빈 칸은 `Apply`가 메운다).
        UnityEditor.PrefabUtility.prefabInstanceUpdated += root =>
        {
            if (root == null) return;
            foreach (var p in root.GetComponentsInChildren<PawnOutlineId>(true))
                if (p.isActiveAndEnabled && p.EditLive()) { p._originals.Clear(); p.Apply(); }
        };
        // Play에 들어가기 직전 — 유니티가 씬을 백업하기 전에 원본으로(백업이 곧 지워질 사본을 가리키면 판의 재질이 빈다).
        // 돌아오면 씬이 다시 서며 `OnEnable`이 끼운다.
        UnityEditor.EditorApplication.playModeStateChanged += state =>
        {
            if (state != UnityEditor.PlayModeStateChange.ExitingEditMode) return;
            foreach (var p in _live) if (p != null) p.Restore();
        };
    }
#endif
}
