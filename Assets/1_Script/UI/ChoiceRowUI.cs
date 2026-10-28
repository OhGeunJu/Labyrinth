using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// **고르는 줄 하나** — `이름표  <  값  >`. 데이터 연결만 한다(여백·글꼴·색은 씬이 갖는다).
// 무엇을 고르는지는 줄이 모른다 — 화면이 `Key`로 가려 목록을 넘겨 준다(`CharacterCreationUI`).
public class ChoiceRowUI : MonoBehaviour
{
    [Tooltip("이 줄이 고르는 것 — 화면이 이 값으로 가른다(gender · body · head · hair · eyes · mouth · ear · nose).")]
    public string Key;

    [SerializeField] TextMeshProUGUI label;
    [SerializeField] TextMeshProUGUI value;
    [SerializeField] Button prev;
    [SerializeField] Button next;

    public event Action<ChoiceRowUI, int> OnStep;   // −1 / +1

    void Awake()
    {
        if (prev != null) prev.onClick.AddListener(() => OnStep?.Invoke(this, -1));
        if (next != null) next.onClick.AddListener(() => OnStep?.Invoke(this, +1));
    }

    public void Show(string text, bool canStep)
    {
        if (value != null) value.text = text;
        if (prev  != null) prev.interactable = canStep;
        if (next  != null) next.interactable = canStep;
    }
}
