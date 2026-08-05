using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 원정 편성 목록의 한 줄: [체크][이름][근력 → 이 폰이 더해 주는 적재량]
//
// **모양은 코드가 아니라 씬의 템플릿이 갖는다**(→ `docs/editor_creators.md`).
// 이 컴포넌트는 데이터 연결(이름·적재·체크 상태)만 한다.
public class ExpeditionRowUI : MonoBehaviour
{
    [Header("요소")]
    [SerializeField] Toggle          check;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI loadText;

    [Header("색")]
    [SerializeField] Color wandererName = Color.white;
    [SerializeField] Color minionName   = new Color(0.65f, 0.80f, 0.95f);

    UnitBrain _brain;
    Action    _onChanged;

    public UnitBrain Brain => _brain;
    public bool IsChecked => check != null && check.isOn;

    public void Bind(UnitBrain brain, Action onChanged)
    {
        _brain     = brain;
        _onChanged = onChanged;

        if (nameText != null)
        {
            nameText.text  = brain.UnitName;
            nameText.color = brain.Data is MinionData ? minionName : wandererName;
        }

        // 적재는 근력에서 나온다(`CarryCapacity`) — 누구를 데려갈지의 판단 근거라 줄마다 보여준다.
        // **편성 화면과 실제 적재는 같은 곳에서 나와야 한다** — 여기서 따로 계산하면
        // "15kg이라더니 50까지 받네"가 된다(실제로 그랬다).
        if (loadText != null)
            loadText.text = brain.Stats != null ? $"적재 +{CarryCapacity.For(brain):0}kg" : "";

        if (check != null)
        {
            check.onValueChanged.RemoveAllListeners();
            check.isOn = false;
            check.onValueChanged.AddListener(_ => _onChanged?.Invoke());
        }
    }

    // 집결이 시작되면 목록을 잠근다 — 걸어가는 중에 명단이 바뀌면 누가 가는지가 흔들린다.
    public void SetInteractable(bool on)
    {
        if (check != null) check.interactable = on;
    }

    // 목록을 다시 그릴 때 체크 상태를 되살린다(집결 중에 패널을 열면 누가 가는지 보여야 한다).
    // 콜백을 태우지 않고 값만 바꾼다 — 되살리는 것뿐인데 요약이 다시 계산될 이유가 없다.
    public void SetChecked(bool on)
    {
        if (check != null) check.SetIsOnWithoutNotify(on);
    }
}
