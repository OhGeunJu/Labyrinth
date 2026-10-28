using UnityEngine;

// **짐승에 단 수레**(2026-10-01, `CartData`). 답하는 것은 셋 — 달았나(`Has` — 명령을 받는가) · 얼마나 더 지나
// (`BonusOf` — `CarryCapacity.For`) · 얼마나 느려지나(`PawnMover.CartSpeedMult`).
//
// **다는 문은 `Attach` 하나 · 떼는 문은 `Detach` 하나**다 — 창고에서 꺼내고 돌려주는 것도 여기서 한다(`MaterialPool`).
// 가축이 죽거나 시체가 되면 수레는 그 자리에 떨어진다(`OnDestroy`·`Corpse` 길 — 바닥 물건).
[DisallowMultipleComponent]
public class PawnCart : MonoBehaviour
{
    public CartData Data { get; private set; }
    GameObject _model;

    public static bool Has(UnitBrain b) => b != null && b.TryGetComponent<PawnCart>(out var c) && c.Data != null;

    public static float BonusOf(UnitBrain b) =>
        b != null && b.TryGetComponent<PawnCart>(out var c) && c.Data != null ? c.Data.CarryBonus : 0f;

    // 달 수 있나 — 가축 · 아직 안 달았음 · 핵 영역 안(창고에서 꺼내 오는 일을 핵이 대신한다).
    public static string WhyNot(UnitBrain beast)
    {
        if (beast == null || !beast.IsTamedBeast) return "가축만 수레를 끈다";
        if (Has(beast)) return "이미 수레를 달았다";
        if (beast.Blackboard != null && (beast.Blackboard.IsKnockedOut || beast.Blackboard.IsPrisoner)) return "지금은 못 단다";
        if (!InTerritory(beast.transform.position)) return "핵 영역 안에서만 단다";
        if (FindCartNear(beast) == null) return "창고에 수레가 없다";
        return null;
    }

    static bool InTerritory(Vector3 p) => DungeonCore.AnyCovers(p);

    // 그 짐승이 선 거점의 창고에 있는 수레 하나(종류가 여럿이면 처음 찾은 것).
    static CartData FindCartNear(UnitBrain beast)
    {
        foreach (var item in DataManager.AllItems.Values)
            if (item is CartData cart && MaterialPool.Available(cart, beast) > 0) return cart;
        return null;
    }

    public static bool Attach(UnitBrain beast)
    {
        if (WhyNot(beast) != null) return false;
        var cart = FindCartNear(beast);
        if (cart == null || !MaterialPool.Consume(cart, 1, beast)) return false;

        var pc = beast.gameObject.AddComponent<PawnCart>();
        pc.Data = cart;
        pc.BuildModel();
        pc.ApplyMover();
        EventNotificationUI.Notify("수레", $"{beast.UnitName}에게 {cart.ItemName}을(를) 달았습니다 - 이제 직접 끌 수 있습니다.");
        return true;
    }

    // 떼서 창고로 돌려보낸다(창고 먼저 · 바닥은 최후 — `MaterialPool.Refund`).
    public void Detach()
    {
        if (Data != null) MaterialPool.Refund(Data, 1, transform.position);
        Data = null;
        if (_model != null) Destroy(_model);
        var mover = GetComponent<PawnMover>();
        if (mover != null) mover.CartSpeedMult = 1f;

        // 명령을 받던 것이 끊긴다 — 들고 있던 명령은 놓는다(맨몸 짐승은 저 알아서 다닌다).
        var brain = GetComponent<UnitBrain>();
        if (brain != null) brain.AbandonWork();
        Destroy(this);
    }

    // **죽으면 수레는 그 자리에 남는다** — 몸과 함께 사라지면 잃은 줄도 모른다.
    void OnDestroy()
    {
        if (Data == null || !gameObject.scene.isLoaded) return;
        DroppedItem.Spawn(Data, 1, transform.position + Vector3.up * 0.3f);
        Data = null;
    }

    void ApplyMover()
    {
        var mover = GetComponent<PawnMover>();
        if (mover != null) mover.CartSpeedMult = Data != null ? Data.SpeedMult : 1f;
    }

    // 짐승 뒤에 붙인다. **콜라이더는 뗀다** — 수레가 몸이 되면 길찾기가 모르는 몸이 하나 더 생긴다.
    void BuildModel()
    {
        if (Data == null || Data.Model == null) return;

        _model = Instantiate(Data.Model, transform);
        _model.name = "[Cart]";
        foreach (var c in _model.GetComponentsInChildren<Collider>(true)) Destroy(c);
        if (Data.Material != null)
            foreach (var r in _model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Data.Material;
                r.sharedMaterials = mats;
            }

        // 크기 — 가장 긴 수평 변을 `ModelLength`로. 팩마다 단위가 달라 재서 맞춘다(`CampfireDresser`와 같은 방식).
        _model.transform.localRotation = Quaternion.identity;
        _model.transform.localScale = Vector3.one;
        var rs = _model.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float len = Mathf.Max(b.size.x, b.size.z);
        float k = len > 0.01f ? Data.ModelLength / len : 1f;
        _model.transform.localScale = Vector3.one * k / Mathf.Max(0.01f, transform.lossyScale.x);

        // 몸 뒤(−Z)로 몸통 반 + 수레 반만큼 · 바닥에 닿게.
        float back = 0.45f + Data.ModelLength * 0.5f;
        _model.transform.localPosition = new Vector3(0f, 0f, -back / Mathf.Max(0.01f, transform.lossyScale.z));

        // 바닥 맞춤 — 크기를 바꾼 뒤 다시 잰다.
        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        _model.transform.position += Vector3.up * (transform.position.y - b.min.y);
    }
}
