using UnityEngine;
using UnityEngine.UI;

// 버튼에 붙이고 Target에 닫을 패널을 연결하면 클릭 시 비활성화.
[RequireComponent(typeof(Button))]
public class CloseButton : MonoBehaviour
{
    [SerializeField] GameObject target;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (target != null) target.SetActive(false);
        });
    }
}
