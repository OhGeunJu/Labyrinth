using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class GridOverlay : MonoBehaviour
{
    [SerializeField] Color lineColor = new Color(1f, 1f, 1f, 0.2f);

    Material _mat;
    bool _visible = false;

    public void SetVisible(bool v) => _visible = v;

    void Awake()
    {
        _mat = new Material(Shader.Find("Hidden/Internal-Colored"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull",    (int)CullMode.Off);
        _mat.SetInt("_ZWrite", 0);
    }

    void OnEnable()  => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    void OnDestroy() => Destroy(_mat);

    // URP
    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != Camera.main) return;
        DrawGrid();
    }

    // Built-in RP fallback
    void OnRenderObject()
    {
        if (GraphicsSettings.currentRenderPipeline != null) return;
        DrawGrid();
    }

    void DrawGrid()
    {
        if (!_visible || MapManager.Instance == null) return;

        _mat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINES);
        GL.Color(lineColor);

        var map = MapManager.Instance;
        float y  = 0.02f;
        float ox = map.WorldOrigin.x;
        float oz = map.WorldOrigin.z;

        for (int row = 0; row <= map.Height; row++)
        {
            GL.Vertex3(ox,              y, oz + row);
            GL.Vertex3(ox + map.Width,  y, oz + row);
        }
        for (int col = 0; col <= map.Width; col++)
        {
            GL.Vertex3(ox + col, y, oz);
            GL.Vertex3(ox + col, y, oz + map.Height);
        }

        GL.End();
        GL.PopMatrix();
    }
}
