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

    RangeCircle _circle;
    float    _effectTimer;

    public float Radius             => radius;
    public int   MinionCapBonus     => minionCapBonus;
    public float MoveSpeedModifier  => moveSpeedModifier;
    public float DamagePerSecond    => damagePerSecond;

    void Start()
    {
        DungeonCore.Instance?.RegisterSubCore(this);
    }

    void OnDestroy()
    {
        DungeonCore.Instance?.UnregisterSubCore(this);
        _circle?.Dispose();
    }

    void Update()
    {
        // 원은 메시라 **매 프레임 다시 짓지 않는다** — 자리·반경·색이 그대로면 그냥 돌아온다.
        //
        // **원은 메시로 그린다**(`RangeCircle`) — `GL`로 카메라 렌더 끝에 그리면 오버레이 UI
        // 위에 얹힌다.
        _circle ??= new RangeCircle(transform, "RangeCircle");
        _circle.Draw(new Vector3(transform.position.x, RangeCircle.Lift, transform.position.z),
                     radius, new Color(0.6f, 0.4f, 1f, 0.6f));

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

            // 서브코어는 플레이어 세력 소유물 — 플레이어 편이 아니면 피해, 같은 편이면 버프.
            var faction = col.GetComponent<Faction>();
            bool isEnemy = faction != null && !faction.IsPlayer;

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

    // **원은 메시로 그린다**(`RangeCircle`) — `GL`로 카메라 렌더 끝에 그리면 오버레이 UI
    // 위에 얹힌다.
}
