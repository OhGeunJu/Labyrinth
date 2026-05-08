using UnityEngine;

// 상인 프리팹에 부착. 일정 시간 후 자동으로 사라짐.
public class MerchantBehaviour : MonoBehaviour
{
    [Tooltip("머무는 시간(초). 이후 자동 소멸.")]
    [SerializeField] float stayDuration = 120f;

    float _timer;

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= stayDuration)
            Destroy(gameObject);
    }
}
