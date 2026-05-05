using UnityEngine;
using UnityEngine.Rendering;

public class SubCore : MonoBehaviour
{
    const float EffectInterval = 0.3f;

    [Header("범위")]
    [SerializeField] float radius = 8f;
    [Header("하수인 캡 증가")]
    [SerializeField] int minionCapBonus = 3;
    [Header("범위 내 유닛 효과")]
    [SerializeField] float moveSpeedModifier = 0f;
    [SerializeField] float damagePerSecond   = 0f;

    Material _mat;
    float    _effectTimer;

    public float Radius             => radius;
    public int   MinionCapBonus     => minionCapBonus;
    public float MoveSpeedModifier  => moveSpeedModifier;
    public float DamagePerSecond    => damagePerSecond;

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
        bool hasMove = moveSpeedModifier != 0f;
        bool hasDmg  = damagePerSecond   >  0f;
        if (!hasMove && !hasDmg) return;

        float effectiveDuration = EffectInterval + 0.1f;

        var cols = Physics.OverlapSphere(transform.position, radius);
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
                float strength = Mathf.Abs(moveSpeedModifier);
                var type = moveSpeedModifier > 0f ? StatusEffectType.Hasted : StatusEffectType.Slowed;
                sfx.ApplyEffect(type, effectiveDuration, strength);
            }

            if (isEnemy && hasDmg)
                brain.TakeDamage(damagePerSecond * EffectInterval);
        }
    }

    void OnEnable()  => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

    public bool IsWithinRange(Vector2Int tile)
    {
        var wp = MapManager.Instance.TileToWorld(tile);
        return IsWithinRangeWorld(wp);
    }

    public bool IsWithinRangeWorld(Vector3 worldPos)
    {
        float dx = worldPos.x - transform.position.x;
        float dz = worldPos.z - transform.position.z;
        return dx * dx + dz * dz <= radius * radius;
    }

    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam == Camera.main) DrawRange();
    }

    void OnRenderObject()
    {
        if (GraphicsSettings.currentRenderPipeline == null) DrawRange();
    }

    void DrawRange()
    {
        const int segments = 48;
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
