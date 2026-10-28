using System;
using System.Collections.Generic;
using UnityEngine;

// 핵 영역(살아있는 모든 핵 반경의 합집합) 밖으로 벗어난 건물을 휴면(비활성)시키고,
// 영역이 복구되면 재가동한다. 설계: docs/design_core_creep.md
// — 고아 건물은 철거하지 않고 "작동 정지 + 시각 표시"만 한다(결정 1).
//
// 작동 정지 방식:
//  · FarmPlot / CraftingTable / WaterSource 등 Update에서 일감을 등록하는 동작 컴포넌트는 enabled=false (가역적).
//  · Storage는 정적 All 집계라 enabled로 막을 수 없음 → Storage.IsActive가 이 컴포넌트의 IsDormant를 직접 참조.
//  · 모든 Renderer를 MaterialPropertyBlock으로 회색 틴트(머티리얼 인스턴스 누수 없음).
[DisallowMultipleComponent]
public class BuildingDormancy : MonoBehaviour
{
    [Tooltip("휴면 시 추가로 비활성화할 동작 컴포넌트. FarmPlot/CraftingTable/WaterSource/Bed는 자동 포함.")]
    [SerializeField] List<Behaviour> suspendOnDormant = new();
    [SerializeField] Color dormantTint = new Color(0.45f, 0.45f, 0.55f, 1f);

    public bool IsDormant { get; private set; }
    public event Action<bool> OnDormancyChanged;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    static readonly int ColorId     = Shader.PropertyToID("_Color");     // Built-in

    Renderer[] _renderers;
    bool _initialized;

    // 런타임 배치 건물에 컴포넌트가 없으면 부착.
    public static void Ensure(GameObject go)
    {
        if (go != null && go.GetComponent<BuildingDormancy>() == null)
            go.AddComponent<BuildingDormancy>();
    }

    void OnEnable()
    {
        DungeonCore.OnTerritoryChanged += Evaluate;
        Evaluate();
    }

    void OnDisable()
    {
        DungeonCore.OnTerritoryChanged -= Evaluate;
    }

    // **건물이 움직였을 때 스스로 다시 본다.** 영역이 바뀌면 `OnTerritoryChanged`가 전부를
    // 재평가하지만, 이전(`BuildingRelocator`)은 **영역은 그대로고 건물만** 옮긴다 — 그때는
    // 아무 이벤트도 안 뜨므로 옮겨 간 건물이 옛 판정을 그대로 들고 있게 된다.
    public void Reevaluate() => Evaluate();

    void Evaluate()
    {
        var core = DungeonCore.Instance;
        // 핵이 아직 없으면 영역 판정 불가 → 가동 유지(보수적). 핵 Start가 OnTerritoryChanged로 재평가시킨다.
        bool inRange = core == null || core.IsWithinAnyRangeWorld(transform.position);
        SetDormant(!inRange);
    }

    void SetDormant(bool dormant)
    {
        if (_initialized && dormant == IsDormant) return;
        _initialized = true;
        IsDormant = dormant;

        foreach (var b in GatherTargets())
            if (b != null) b.enabled = !dormant;

        ApplyTint(dormant);
        OnDormancyChanged?.Invoke(dormant);
    }

    IEnumerable<Behaviour> GatherTargets()
    {
        foreach (var b in suspendOnDormant) yield return b;
        // Update에서 일감을 등록하는 빌트인 건물 동작들. (Storage는 IsActive 플래그로 별도 처리)
        if (TryGetComponent<FarmPlot>(out var fp))      yield return fp;
        if (TryGetComponent<CraftingTable>(out var ct)) yield return ct;
        if (TryGetComponent<WaterSource>(out var ws))   yield return ws;
        // 화톳불: 재우면 `OnDisable`이 불을 끈다. 안 넣으면 **영역 밖에서 혼자 타면서
        // 창고의 목재를 계속 먹는다** — 휴면은 "전면 사용 불가"인데 그 하나만 예외가 된다.
        if (TryGetComponent<Campfire>(out var fire))    yield return fire;
        // 침대: enabled=false면 OnDisable로 정적 목록에서 빠져 배정/사용 대상에서 제외된다.
        // (배정 정보 AssignedUnit은 남아 영역 복구 시 그대로 복원)
        if (TryGetComponent<Bed>(out var bed))          yield return bed;
        // 관개 수로: `OnDisable`이 레지스트리에서 빠지므로 **영역 밖 수로는 아무것도 안 적신다.**
        // 안 넣으면 핵에서 떨어진 곳에 수로만 놓아도 밭이 영영 안 마른다(화톳불과 같은 자리).
        if (TryGetComponent<Irrigation>(out var irr))   yield return irr;
        // 충전대·제단: `OnDisable`이 레지스트리에서 빠진다. 안 넣으면 **핵 영역 밖 충전대가
        // 후보로 남아** 골렘이 16km 밖으로 걸어간다(화톳불·수로와 같은 자리 — 휴면은
        // "전면 사용 불가"인데 하나가 예외가 되면 그 하나로 새어 나간다).
        if (TryGetComponent<Recharger>(out var rc))     yield return rc;
    }

    // 이 오브젝트(또는 부모)가 휴면 중인가 — 클릭/주문 등 외부 진입점 게이트용.
    public static bool IsDormantAt(Component c)
    {
        if (c == null) return false;
        var d = c.GetComponentInParent<BuildingDormancy>();
        return d != null && d.IsDormant;
    }

    void ApplyTint(bool dormant)
    {
        _renderers ??= GetComponentsInChildren<Renderer>();
        var mpb = new MaterialPropertyBlock();
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            if (dormant)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, dormantTint);
                mpb.SetColor(ColorId,     dormantTint);
                r.SetPropertyBlock(mpb);
            }
            else
            {
                r.SetPropertyBlock(null); // 원래 머티리얼 색 복원
            }
        }
    }
}
