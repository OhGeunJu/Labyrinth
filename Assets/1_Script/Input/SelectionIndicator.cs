using UnityEngine;

public class SelectionIndicator : MonoBehaviour
{
    [SerializeField] Color selectedColor = Color.green;
    [SerializeField] float indicatorSize = 0.8f;

    GameObject _indicator;

    void Awake()
    {
        // 납작한 원통으로 발밑 원 표현.
        //
        // **발밑이 어디인지는 콜라이더가 답한다**(`PawnPlacement.Lift` = 루트에서 바닥까지).
        // 예전엔 `-0.9`로 못 박혀 있었는데, 그건 사람 꼴(높이 2·중심 0)만 맞는 값이다 —
        // 늑대·곰처럼 몸이 낮아 콜라이더를 줄인 폰은 **표식이 땅속에 그려진다.**
        // 살짝 띄우는 것은 지면과 z-파이팅을 피하려는 것.
        float foot = -PawnPlacement.Lift(gameObject) + 0.1f;

        _indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _indicator.transform.SetParent(transform);
        _indicator.transform.localPosition = new Vector3(0f, foot, 0f);
        _indicator.transform.localScale = new Vector3(indicatorSize, 0.01f, indicatorSize);

        // 콜라이더 제거 (선택 방해 안 되게)
        Destroy(_indicator.GetComponent<Collider>());

        _indicator.GetComponent<Renderer>().material.color = selectedColor;
        _indicator.SetActive(false);
    }

    public void SetSelected(bool selected) => _indicator.SetActive(selected);
}