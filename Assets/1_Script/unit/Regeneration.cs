using UnityEngine;

// **저절로 아문다**(트롤 — `UnitData.RegenPerSecond`). 불이 붙어 있는 동안은 안 아문다 — 트롤을 잡는 길이 불이다.
//
// **틱마다 누적하는 값은 델타를 곱한다**(배속에서 한 프레임이 커진다). 죽은 몸은 안 아문다.
public class Regeneration : MonoBehaviour
{
    UnitBrain _brain;
    BodyPartSystem _body;
    StatusEffectSystem _sfx;
    float _acc;

    // 한 번에 조금씩 넣으면 피·부위 셈이 매 프레임 돈다 — 1초마다 모아서 넣는다.
    const float Every = 1f;

    void Start()
    {
        _brain = GetComponent<UnitBrain>();
        TryGetComponent(out _body);
        TryGetComponent(out _sfx);
    }

    void Update()
    {
        if (_brain == null || _brain.IsDead || _brain.Data == null) return;

        _acc += Time.deltaTime;
        if (_acc < Every) return;
        float dt = _acc;
        _acc = 0f;

        if (_sfx != null && _sfx.IsBurning) return;

        float amount = _brain.Data.RegenPerSecond * dt;
        if (_body != null) _body.Heal(amount, amount);
    }
}
