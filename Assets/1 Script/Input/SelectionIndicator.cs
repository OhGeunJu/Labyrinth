using UnityEngine;

public class SelectionIndicator : MonoBehaviour
{
    [SerializeField] Color selectedColor = Color.green;
    [SerializeField] float indicatorSize = 0.8f;

    GameObject _indicator;

    void Awake()
    {
        // 납작한 원통으로 발밑 원 표현
        _indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _indicator.transform.SetParent(transform);
        _indicator.transform.localPosition = new Vector3(0f, -0.9f, 0f);
        _indicator.transform.localScale = new Vector3(indicatorSize, 0.01f, indicatorSize);

        // 콜라이더 제거 (선택 방해 안 되게)
        Destroy(_indicator.GetComponent<Collider>());

        _indicator.GetComponent<Renderer>().material.color = selectedColor;
        _indicator.SetActive(false);
    }

    public void SetSelected(bool selected) => _indicator.SetActive(selected);
}