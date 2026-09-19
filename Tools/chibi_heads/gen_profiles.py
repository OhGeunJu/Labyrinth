import json, os, numpy as np

S = os.path.dirname(os.path.abspath(__file__))
d = json.load(open(os.path.join(S, 'heads.json')))

def smooth(r, sigma=1.0):
    n = len(r)
    k = np.arange(-3, 4)
    w = np.exp(-0.5 * (k / sigma) ** 2); w /= w.sum()
    p = np.pad(r, 3, mode='edge')
    return np.convolve(p, w, mode='valid')

# **우리 이름으로 적는다**(`ChibiNames` — 림월드 이름을 그대로 쓰지 않는다). 그림 파일 이름은 옛 이름 그대로다.
WIDTH = {"Average": "Broad", "Narrow": "Slim"}
JAW   = {"Normal": "Soft", "Pointy": "Sharp", "Wide": "Square"}
def our_name(n):
    g, rest = n.split('_')
    for a, b in WIDTH.items():
        if rest.startswith(a): return f"{g}_{b}{JAW[rest[len(a):]]}"
    return n

lines = []
for name, v in d.items():
    name = our_name(name)
    H, W = v['H'], v['W']
    t = np.array(v['t']) + 0.5 / H          # 행 가운데 (저장 때 반 픽셀 밀림 보정)
    r = np.array(v['r'])
    o = np.argsort(t); t, r = t[o], r[o]
    r = smooth(r)
    r = r / (2 * r.max()) * 1.0              # 최대 0.5로
    r = np.minimum(r, 0.5)
    t = np.concatenate([[0.0], t, [1.0]])
    r = np.concatenate([[0.0], r, [0.0]])
    # 49점으로 다시 뽑는다 — 양 끝을 촘촘히(마지막 반 픽셀에서 닫힌다)
    u = (1 - np.cos(np.linspace(0, np.pi, 49))) / 2
    ts = u
    rs = np.interp(ts, t, r)
    rs[0] = rs[-1] = 0.0
    eyes = v['eyes']
    e = eyes[0] if eyes else None
    cores = v['cores']
    eyeT = np.mean([x['t'] for x in cores])
    eyeDx = np.mean([abs(x['dx']) for x in cores])
    eyeD = np.mean([np.sqrt(x['wpx'] * x['hpx']) for x in cores])   # 진한 알맹이의 넓이가 같은 원의 지름(픽셀)
    fl = lambda a: ', '.join(f'{x:.4f}f' for x in a)
    lines.append(f'        new Ref("{name}", {W:.2f}f, {H:.2f}f, {eyeT:.4f}f, {eyeDx:.4f}f, {eyeD:.2f}f,\n'
                 f'                new[] {{ {fl(ts)} }},\n'
                 f'                new[] {{ {fl(rs)} }}),')

body = '\n'.join(lines)
cs = f'''// **자동 생성 — 손으로 고치지 말 것.** 림월드 머리형 열두 장(사용자가 보낸 스크린샷)에서
// 살색 윤곽을 한 행씩 재어 뽑았다(`Tools/chibi_heads/trace_heads.py` → `gen_profiles.py`).
//
// 값의 뜻:
//   W·H      머리 살(외곽선 안쪽)의 폭·높이 — 픽셀. 몸통 높이 {np.mean([v["bodyH"] for v in d.values()]):.1f}px가 같은 그림 안의 눈금이다.
//   EyeT     눈 가운데 높이(0 = 턱 끝 · 1 = 정수리)
//   EyeDx    눈 가운데가 한복판에서 떨어진 거리 ÷ 머리 폭
//   EyeD     눈 지름 — 픽셀. 그림의 눈은 가로로 긴 타원(약 12 × 7)이라 **넓이가 같은 원**으로 옮겼다
//            (가로 폭을 지름으로 쓰면 세로가 1.5배라 눈이 커 보인다)
//   T·R      옆선: 높이 t에서의 반폭 ÷ 머리 폭(최대 0.5). **정면 실루엣이 그대로 이것이다.**
public static class ChibiHeadProfiles
{{
    public const float BodyPx = {np.mean([v["bodyH"] for v in d.values()]):.2f}f;   // 그림 속 몸통 높이 — 미터로 옮길 때의 눈금

    public readonly struct Ref
    {{
        public readonly string  Name;
        public readonly float   W, H, EyeT, EyeDx, EyeD;
        public readonly float[] T, R;
        public Ref(string name, float w, float h, float eyeT, float eyeDx, float eyeD, float[] t, float[] r)
        {{ Name = name; W = w; H = h; EyeT = eyeT; EyeDx = eyeDx; EyeD = eyeD; T = t; R = r; }}

        public float RadiusAt(float t)
        {{
            if (t <= T[0]) return R[0];
            for (int i = 1; i < T.Length; i++)
                if (t <= T[i])
                {{
                    float k = (t - T[i - 1]) / (T[i] - T[i - 1]);
                    return R[i - 1] + (R[i] - R[i - 1]) * k;
                }}
            return R[^1];
        }}
    }}

    public static readonly Ref[] All =
    {{
{body}
    }};
}}
'''
open(os.path.join(S, '..', '..', 'Assets', 'Editor', 'ChibiHeadProfiles.cs'), 'w', encoding='utf-8', newline='\n').write(cs)
for name, v in d.items():
    c = v['cores']
    print(name, 'eyeT', round(np.mean([x['t'] for x in c]), 3), 'dx', round(np.mean([abs(x['dx']) for x in c]), 3),
          'D px', round(np.mean([np.sqrt(x['wpx'] * x['hpx']) for x in c]), 1))
