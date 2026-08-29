using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionBox : MonoBehaviour
{
    [SerializeField] SelectionManager selectionManager;
    [SerializeField] Texture2D boxTexture;   // 흰 1×1 텍스처 or null

    Vector2 _start;
    bool _drawing;

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _start = Mouse.current.position.ReadValue();
            _drawing = false;
        }

        // **UI 위에서 시작한 드래그에는 안 그린다.**
        //
        // 이 상자는 저 혼자 판단하고 있었다 — 그래서 **배낭에서 물건을 끌 때 월드 선택 상자가
        // 같이 떴다**: 선택은 `SelectionManager._pressOverUI`가 이미 막고 있으니 아무도 안
        // 골리는데 초록 사각형만 그려지는, 설명할 수 없는 그림이었다.
        //
        // **판단은 `SelectionManager`가 한다**(`PressOverUI`) — 그리는 쪽이 따로 물으면
        // 언젠가 두 답이 갈린다. 배선이 비었으면 그때만 스스로 찾는다.
        if (selectionManager == null)
            selectionManager = Object.FindFirstObjectByType<SelectionManager>();

        if (selectionManager != null && selectionManager.PressOverUI) { _drawing = false; return; }

        // **지정 모드에서는 안 그린다**(2026-08-29). 채굴·운반은 드래그로 *칸을 찍는* 것이라
        // 미리보기가 이미 있다(찍힌 칸이 물든다). 거기에 초록 선택 상자가 겹치면 **둘이
        // 다른 범위를 가리켜** 무엇이 찍히는지 알 수가 없다 — 실제로 그것 때문에 겨눈 곳과
        // 찍히는 곳이 다르다는 신고가 나왔다.
        if (MineDesignator.IsActive || HaulDesignator.IsActive || BuildModeManager.IsActive)
        {
            _drawing = false;
            return;
        }

        if (Mouse.current.leftButton.isPressed)
        {
            Vector2 cur = Mouse.current.position.ReadValue();
            if (!_drawing && Vector2.Distance(_start, cur) > 5f)
                _drawing = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            _drawing = false;
    }

    void OnGUI()
    {
        if (!_drawing) return;

        Vector2 cur = Mouse.current.position.ReadValue();
        // GUI는 Y축 반전
        Vector2 s = new(_start.x, Screen.height - _start.y);
        Vector2 e = new(cur.x, Screen.height - cur.y);

        Rect rect = new(Mathf.Min(s.x, e.x), Mathf.Min(s.y, e.y),
                        Mathf.Abs(s.x - e.x), Mathf.Abs(s.y - e.y));

        if (boxTexture != null)
            GUI.color = new Color(0.5f, 1f, 0.5f, 0.25f);
        else
            GUI.color = new Color(0.5f, 1f, 0.5f, 0.25f);

        GUI.DrawTexture(rect, boxTexture != null ? boxTexture : Texture2D.whiteTexture);

        GUI.color = new Color(0.5f, 1f, 0.5f, 0.9f);
        GUI.Box(rect, GUIContent.none);

        GUI.color = Color.white;
    }
}