using UnityEngine;
using UnityEngine.Rendering;

public class SubCore : MonoBehaviour
{
    const float EffectInterval = 0.3f;

    SubCoreData _data;
    Material    _mat;
    float       _effectTimer;

    public SubCoreData Data => _data;

    public void Init(SubCoreData data)
    {
        _data = data;
    }

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

    void Start()
    {
        DungeonCore.Instance?.RegisterSubCore(this);
    }

    void OnDestroy()
    {
        DungeonCore.Instance?.UnregisterSubCore(this);
        Destroy(_mat);
    }

    void Update()
    {
        _effectTimer += Time.deltaTime;
        if (_effectTimer < EffectInterval) return;
        _effectTimer = 0f;
        ApplyAreaEffects();
    }

    void ApplyAreaEffects()
    {
        if (_data == null) return;
        bool hasMove = _data.MoveSpeedModifier != 0f;
        bool hasDmg  = _data.DamagePerSecond   >  0f;
        if (!hasMove && !hasDmg) return;

        float effectiveDuration = EffectInterval + 0.1f; // 다음 틱까지 유지될 여유

        var cols = Physics.OverlapSphere(transform.position, _data.Radius);
        foreach (var col in cols)
        {
            var brain = col.GetComponent<UnitBrain>();
            if (brain == null) continue;

            var faction = col.GetComponent<Faction>();
            bool isEnemy = faction != null && faction.Type == FactionType.Enemy;

            if (!isEnemy && hasMove)
            {
                var sfx = col.GetComponent<StatusEffectSystem>();
                if (sfx == null) continue;
                float strength = Mathf.Abs(_data.MoveSpeedModifier);
                var type = _data.MoveSpeedModifier > 0f
                    ? StatusEffectType.Hasted
                    : StatusEffectType.Slowed;
                sfx.ApplyEffect(type, effectiveDuration, strength);
            }

            if (isEnemy && hasDmg)
                brain.TakeDamage(_data.DamagePerSecond * EffectInterval);
        }
    }

    void OnEnable()  => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

    public bool IsWithinRange(Vector2Int tile)
    {
        if (_data == null) return false;
        var wp = MapManager.Instance.TileToWorld(tile);
        return IsWithinRangeWorld(wp);
    }

    public bool IsWithinRangeWorld(Vector3 worldPos)
    {
        if (_data == null) return false;
        float dx = worldPos.x - transform.position.x;
        float dz = worldPos.z - transform.position.z;
        return dx * dx + dz * dz <= _data.Radius * _data.Radius;
    }

    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != Camera.main || _data == null) return;
        DrawRange();
    }

    void OnRenderObject()
    {
        if (GraphicsSettings.currentRenderPipeline != null || _data == null) return;
        DrawRange();
    }

    void DrawRange()
    {
        const int segments = 48;
        float radius = _data.Radius;
        Vector3 center = new Vector3(transform.position.x, 0.02f, transform.position.z);

        _mat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINE_STRIP);
        GL.Color(new Color(0.6f, 0.4f, 1f, 0.6f));

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            GL.Vertex3(center.x + Mathf.Cos(angle) * radius,
                       center.y,
                       center.z + Mathf.Sin(angle) * radius);
        }

        GL.End();
        GL.PopMatrix();
    }
}
