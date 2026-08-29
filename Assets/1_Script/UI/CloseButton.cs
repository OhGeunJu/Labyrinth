using UnityEngine;
using UnityEngine.UI;

// 버튼에 붙이고 Target에 닫을 패널을 연결하면 클릭 시 닫힌다.
//
// **닫기 버튼이 곧 "사람이 닫는" 단일 출처다**(2026-08-29) — 그래서 흐려지며 닫히는 규칙을
// 여기 한 곳에 붙이면 창 전부가 따라온다(`WindowRaiser.Open`이 여는 쪽에서 하는 일 그대로).
[RequireComponent(typeof(Button))]
public class CloseButton : MonoBehaviour
{
    [SerializeField] GameObject target;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (target != null) WindowRaiser.Close(target);
        });
    }
}
