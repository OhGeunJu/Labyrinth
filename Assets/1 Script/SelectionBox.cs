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