using UnityEngine;

// **천장에 뚫린 구멍 — 위에서 빛이 내려온다**(2026-10-11, 사용자: *"전체적으로 더 어두우면 좋겠고
// 천장에서 빛이 들어오는 구멍이 종종 있으면 좋겠어 그러면 미궁을 더 어둡게 구현해도 되는거잖아"*).
//
// 굴 전체를 어둡게 내리면 **길을 읽을 데가 없어진다.** 그 자리를 이것이 메운다 — 멀리서도 보이는
// 밝은 웅덩이가 드문드문 있어서, 어둠은 깊어지되 *"저기까지 가면 된다"*가 남는다.
//
// ## 불이 아니라 바깥 빛이다
//
// 횃불·야광석은 **어두울수록 세진다**(`LightSource.NightBoost`). 이것은 거꾸로다 — 바깥이 낮이면 볕이
// 쏟아지고 밤이면 달빛만 샌다. **시계는 `UnderworldLighting.Darkness` 하나를 읽는다**(화면이 옮겨 가는
// 그 값이라, 굴이 어두워지는 박자와 볕이 꺼지는 박자가 같다).
//
// ## 볕 아래서는 폰도 본다
//
// `LightSource`를 같이 든다(`Vision`이 읽는다) — 밝아 보이는 만큼 실제로 본다. **달빛은 안 쳐 준다**:
// 밤의 웅덩이는 길잡이일 뿐 시야를 걷지 않는다(그건 불이 하는 일이다).
//
// `LightSource`는 **등이 없는 자식에 단다** — 제 등을 찾으면 밤에 세기를 올려 버린다.
public class Skylight : MonoBehaviour
{
    // 달빛이 볕의 몇 배인가.
    const float Moon = 0.12f;

    // 이보다 어두워지면 시야를 안 걷는다(저녁까지는 걷고 새벽·밤은 안 걷는다).
    const float SightUntil = 0.6f;

    static readonly Color SunColor  = new(1.00f, 0.95f, 0.82f);
    static readonly Color MoonColor = new(0.55f, 0.68f, 1.00f);
    static readonly int   BaseColor = Shader.PropertyToID("_BaseColor");

    [Tooltip("위에서 내리쬐는 등.")]
    [SerializeField] Light beam;

    [Tooltip("빛기둥 모형. 등과 같이 옅어진다.")]
    [SerializeField] Renderer shaft;

    [Tooltip("볕 속에 뜬 먼지. 등과 같이 옅어진다.")]
    [SerializeField] Renderer motes;

    [Tooltip("볕 아래의 시야. 등이 없는 자식에 단 `LightSource`.")]
    [SerializeField] LightSource sight;

    float _intensity;
    Color _shaftColor, _motesColor;
    MaterialPropertyBlock _block;

    public void Configure(Light beam, Renderer shaft, Renderer motes, LightSource sight)
    {
        this.beam  = beam;
        this.shaft = shaft;
        this.motes = motes;
        this.sight = sight;
    }

    void Awake()
    {
        _block = new MaterialPropertyBlock();
        if (beam != null) _intensity = beam.intensity;
        _shaftColor = ColorOf(shaft);
        _motesColor = ColorOf(motes);

        // 그림자는 화면 가운데 몇 개만 던진다 — 횃불과 한 몫을 나눠 쓴다.
        ShadowBudget.Track(gameObject);
    }

    // **구멍마다 크기가 다르다** — 뿌리기가 크기를 굴려 주고(`ScatterRule.ScaleRange`) 여기서 그것을 *너비*로만
    // 받는다. 높이까지 같이 줄이면 작은 구멍은 낮은 천장에 뚫린 것이 되고, 등이 바닥에 가까워져 더 밝아진다
    // (작은 구멍이 더 눈부신 것은 거꾸로다). 높이는 그대로 두고 원뿔의 각으로 넓힌다 — 같은 높이에서 각만
    // 달라지면 바닥의 밝기는 같다.
    //
    // **`Start`다** — 뿌리기가 크기를 곱하는 것은 `Instantiate` 다음 줄이라 `Awake`에서는 아직 1이다.
    //
    // 크기는 **세계 기준으로 잰다**(`lossyScale`) — 무리 프리팹에서는 부모(뿌리기가 곱한 것)와 제 것(프리팹에
    // 적힌 것)이 겹친다.
    void Start()
    {
        // 무리의 곁가지가 바위 속에 떨어졌다 — 볕이 들 자리가 아니다.
        if (RockMap.Enabled && RockMap.IsRock(transform.position))
        {
            gameObject.SetActive(false);
            return;
        }

        float s = transform.lossyScale.x, y = transform.lossyScale.y;
        if (Mathf.Approximately(s, 1f) && Mathf.Approximately(y, 1f)) return;

        var local = transform.localScale;
        transform.localScale = new Vector3(local.x, y > 0f ? local.y / y : local.y, local.z);

        if (beam != null)
        {
            float outer = Mathf.Tan(beam.spotAngle      * 0.5f * Mathf.Deg2Rad) * s;
            float inner = Mathf.Tan(beam.innerSpotAngle * 0.5f * Mathf.Deg2Rad) * s;
            beam.spotAngle      = 2f * Mathf.Atan(outer) * Mathf.Rad2Deg;
            beam.innerSpotAngle = 2f * Mathf.Atan(inner) * Mathf.Rad2Deg;
        }

        if (sight != null) sight.Radius *= s;
    }

    void Update()
    {
        float dark = UnderworldLighting.Darkness;
        float k    = Mathf.Lerp(1f, Moon, dark);
        Color tint = Color.Lerp(SunColor, MoonColor, dark);

        if (beam != null)
        {
            beam.intensity = _intensity * k;
            beam.color     = tint;
        }

        float glow = Mathf.Lerp(1f, 0.3f, dark);
        Tint(shaft, _shaftColor, tint, glow);
        Tint(motes, _motesColor, tint, glow);

        if (sight != null) sight.SetLit(dark < SightUntil);
    }

    static Color ColorOf(Renderer r) =>
        r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColor)
            ? r.sharedMaterial.GetColor(BaseColor) : Color.clear;

    void Tint(Renderer r, Color baseColor, Color tint, float glow)
    {
        if (r == null) return;
        var c = baseColor * tint;
        c.a = baseColor.a * glow;
        _block.SetColor(BaseColor, c);
        r.SetPropertyBlock(_block);
    }
}
