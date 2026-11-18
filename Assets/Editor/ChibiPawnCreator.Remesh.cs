using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// **표면 그물 뒤 다시 짜기 — AutoRemesher**(2026-11-18, 사용자: 오크 얼굴 *"자세히보면 선이 이상하잖아"* → 오크 머리 · 귀로 확인한 뒤
// *"그냥 모든 모델 다 한번에 되게 해도 괜찮을 거 같아"*).
//
// 표면 그물 → 줄이기를 거친 살은 격자 방향 계단 띠 · 자잘한 돌기를 남긴다(오크 머리 — `Tools/ModelLab/common/mesh_audit.py`로 재면
// 잔물결 0.15 · 매끈한 늑대인간 몸은 0.02). 칠은 거리장 법선이 가려 매끈해 보여도 바깥 선 껍데기는 정점을 밀어 그리므로 톱니가 되고,
// 얇은 판(오크 귀)은 털 뭉치가 됐다. **`SurfaceNets`가 줄인 그물마다 여기를 거친다** — 외부 도구(AutoRemesher — MIT ·
// `C:\tools\autoremesher`)로 고른 사각 그물로 다시 짜고, **점을 거리장 표면으로 다시 당긴다**(다시 짠 면은 원래 표면에서 3mm 남짓
// 벗어난다 — 눈 판은 1.6mm · 문신은 2.5mm 위에 얹으니 그대로 두면 묻힌다). 법선은 거리장에서 다시 잰다(그늘은 `SurfaceNets`가 잰다).
//
// - **도구가 없으면 경고 한 줄을 남기고 줄인 그물을 그대로 쓴다** — 다른 체크아웃에서도 굽기는 돈다.
// - **망가지면 되돌린다** — 당긴 뒤에도 표면에서 칸 ¼보다 뜨거나(얇은 껍데기가 뭉개짐) 크기가 칸 둘보다 달라지면 줄인 그물을 쓴다.
// - **같은 입력은 다시 안 돌린다** — 입력 그물의 해시로 결과 OBJ를 `Library/AutoRemeshCache`에 둔다(첫 굽기만 느리다).
public static partial class ChibiPawnCreator
{
    const string AutoRemesherExe = @"C:\tools\autoremesher\autoremesher.exe";
    const int   RemeshMinVerts = 1500;   // 이보다 작은 조각(이 · 눈동자)은 다시 짤 것이 없다
    const float RemeshQuadsPer = 1.2f;   // 사각면 목표 = 줄인 점 수 × 이것(결과 점 수는 대개 목표의 0.4배)
    static readonly string RemeshCacheDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "AutoRemeshCache"));
    static bool _autoRemesherWarned;
    static int _remeshDone, _remeshCached, _remeshKept;
    static readonly List<string> _remeshKeptWhy = new();
    static string _remeshPendingWhy;

    // 다시 짠 횟수 · 캐시 · 되돌림 — `NetsTimeReport`가 읽고 비운다.
    static string RemeshReport()
    {
        if (_remeshDone + _remeshKept == 0) return "";
        var s = $"다시 짜기 {_remeshDone}개(캐시 {_remeshCached}) · 되돌림 {_remeshKept}개";
        if (_remeshKeptWhy.Count > 0) Debug.LogWarning("[다시 짜기] 되돌린 그물 — " + string.Join(" · ", _remeshKeptWhy));
        _remeshDone = _remeshCached = _remeshKept = 0; _remeshKeptWhy.Clear();
        return s;
    }

    // `pts` · `tris` — 거리장 좌표의 줄인 그물(`place` 전). 다시 짰으면 셋을 바꾸고 true.
    static bool TryAutoRemesh(ref Vector3[] pts, ref Vector3[] normals, List<int> tris, Func<Vector3, float> f, float step)
    {
        if (pts.Length < RemeshMinVerts) return false;
        int inVerts = pts.Length;
        if (!File.Exists(AutoRemesherExe))
        {
            if (!_autoRemesherWarned) Debug.LogWarning($"[다시 짜기] {AutoRemesherExe}가 없다 — 줄인 그물을 그대로 쓴다(울퉁불퉁한 살 · 톱니 선).");
            _autoRemesherWarned = true;
            return false;
        }
        int quads = Mathf.Max(1000, Mathf.RoundToInt(pts.Length * RemeshQuadsPer));
        string key = RunAutoRemesher(pts, tris, quads, out var np, out var nt);
        if (np == null) return Keep("결과 없음");
        if (np.Count < 8 || nt.Count == 0) return Keep("빈 결과");

        // 거리장 표면으로 당긴다(뉴턴 여섯 걸음 · 칸 둘보다 멀리 튀면 제자리) — 법선은 그 자리의 기울기.
        var arr = np.ToArray(); var nrm = new Vector3[arr.Length]; var off = new float[arr.Length];
        Parallel.For(0, arr.Length, v =>
        {
            var p = arr[v];
            for (int it = 0; it < 6; it++)
            {
                var g = Grad(f, p, step * 0.25f); float gl = g.sqrMagnitude;
                if (gl < 1e-10f) break;
                p -= g * (f(p) / gl);
            }
            if ((p - arr[v]).sqrMagnitude < 4f * step * step) arr[v] = p;
            var gn = Grad(f, arr[v], step * 0.5f);
            off[v] = Mathf.Abs(f(arr[v])) / Mathf.Max(gn.magnitude, 0.05f);
            nrm[v] = gn.normalized;
        });

        // 망가졌나 — 표면에서 뜬 점(99%) · 크기.
        Array.Sort(off);
        float p99 = off[(int)(off.Length * 0.99f)];
        if (p99 > step * 0.25f) return Keep($"표면에서 {p99 / step:F2}칸 뜸");
        var b0 = new Bounds(pts[0], Vector3.zero); foreach (var v in pts) b0.Encapsulate(v);
        var b1 = new Bounds(arr[0], Vector3.zero); foreach (var v in arr) b1.Encapsulate(v);
        if ((b0.min - b1.min).magnitude > 2f * step || (b0.max - b1.max).magnitude > 2f * step) return Keep("크기가 달라짐");

        // 앞뒤 — 면의 감김이 거리장 법선과 반대로 돌면 뒤집는다(도구가 감김을 지키는지 문서에 없다).
        int agree = 0, total = 0;
        for (int i = 0; i < nt.Count; i += 3)
        {
            var a = arr[nt[i]]; var fn = Vector3.Cross(arr[nt[i + 1]] - a, arr[nt[i + 2]] - a);
            if (fn.sqrMagnitude < 1e-16f) continue;
            total++; if (Vector3.Dot(fn, nrm[nt[i]] + nrm[nt[i + 1]] + nrm[nt[i + 2]]) > 0f) agree++;
        }
        if (agree * 2 < total) for (int i = 0; i < nt.Count; i += 3) (nt[i + 1], nt[i + 2]) = (nt[i + 2], nt[i + 1]);

        pts = arr; normals = nrm;
        tris.Clear(); tris.AddRange(nt);
        _remeshDone++;
        return true;

        bool Keep(string why)
        {
            _remeshKept++;
            _remeshPendingWhy = $"{inVerts}점 · {why}";   // 이름은 `SaveSkull`이 붙인다(여기서는 모른다)
            return false;
        }
    }
    // AutoRemesher 한 번 — 입력 그물(`pts` · `tris`)을 OBJ로 써서 돌리고 결과를 세모로 읽는다(사각형은 짧은 대각선).
    // 같은 입력 · 목표는 캐시에서. 실패하면 `np`가 null. 반환은 캐시 키(로그용).
    static string RunAutoRemesher(IList<Vector3> pts, IList<int> tris, int quads, out List<Vector3> np, out List<int> nt)
    {
        np = null; nt = null;
        var sb = new StringBuilder(pts.Count * 40);
        foreach (var v in pts) sb.Append("v ").Append(v.x.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                                 .Append(v.y.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                                 .Append(v.z.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        for (int i = 0; i < tris.Count; i += 3) sb.Append("f ").Append(tris[i] + 1).Append(' ').Append(tris[i + 1] + 1).Append(' ').Append(tris[i + 2] + 1).Append('\n');
        string obj = sb.ToString();
        string key;
        using (var sha = System.Security.Cryptography.SHA1.Create())
            key = BitConverter.ToString(sha.ComputeHash(Encoding.ASCII.GetBytes(obj + "|" + quads))).Replace("-", "").Substring(0, 24);

        Directory.CreateDirectory(RemeshCacheDir);
        string outPath = Path.Combine(RemeshCacheDir, key + ".obj");
        if (File.Exists(outPath)) _remeshCached++;
        else
        {
            string inPath = Path.Combine(RemeshCacheDir, key + "_in.obj"), tmpOut = Path.Combine(RemeshCacheDir, key + "_tmp.obj");
            File.WriteAllText(inPath, obj);
            var psi = new System.Diagnostics.ProcessStartInfo(AutoRemesherExe, $"-i \"{inPath}\" -o \"{tmpOut}\" --target-quads {quads}")
                      { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                proc.StandardOutput.ReadToEndAsync(); proc.StandardError.ReadToEndAsync();
                if (!proc.WaitForExit(180_000)) { try { proc.Kill(); } catch { } }
            }
            File.Delete(inPath);
            if (!File.Exists(tmpOut)) return key;
            File.Move(tmpOut, outPath);
        }

        np = new List<Vector3>(); nt = new List<int>(); var poly = new List<int>(8);
        foreach (var line in File.ReadLines(outPath))
        {
            if (line.StartsWith("v "))
            {
                var p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                np.Add(new Vector3(float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture), float.Parse(p[3], CultureInfo.InvariantCulture)));
            }
            else if (line.StartsWith("f "))
            {
                var p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                poly.Clear();
                for (int k = 1; k < p.Length; k++) { int sl = p[k].IndexOf('/'); poly.Add(int.Parse(sl < 0 ? p[k] : p[k].Substring(0, sl), CultureInfo.InvariantCulture) - 1); }
                if (poly.Count == 4)
                {
                    bool ac = (np[poly[0]] - np[poly[2]]).sqrMagnitude <= (np[poly[1]] - np[poly[3]]).sqrMagnitude;
                    if (ac) nt.AddRange(new[] { poly[0], poly[1], poly[2], poly[0], poly[2], poly[3] });
                    else    nt.AddRange(new[] { poly[1], poly[2], poly[3], poly[1], poly[3], poly[0] });
                }
                else for (int k = 1; k + 1 < poly.Count; k++) nt.AddRange(new[] { poly[0], poly[k], poly[k + 1] });
            }
        }
        if (np.Count < 8 || nt.Count == 0) { np = null; nt = null; }
        return key;
    }

    // ── 파이썬이 뽑은 메시(엔트 · 공룡 악어 · 늑대인간) ─────────────────────────────
    //
    // 이 메시는 거리장이 없고, 한 파일에 **겹친 조각 여럿**(몸 · 바지 · 털 술)이 묶여 있다 — 통째로 다시 짜면 조각이 녹아 붙고
    // 안쪽 면 · 가는 술이 지워졌다(늑대인간 몸 — 원래 표면의 1%가 1.5cm 넘게 벗어남). 그래서 **이어진 덩어리마다** 따로 짠다:
    // 점 상한(`cap`)을 덩어리 크기대로 나눠 목표로 삼고, 새 점은 **원래 촘촘한 표면의 가장 가까운 자리**로 붙인다. 법선은 그 자리의
    // 원래 법선을 섞고, 색 · 뼈 가중치는 그 세모에서 가장 가까운 원래 점의 것(팔레트 몫이 번지지 않게). 원래 표면이 새 표면에서
    // 새 변 길이의 ¾보다 멀어지는 점이 0.5%를 넘으면(술 · 가시가 지워짐) 그 덩어리만 예전처럼 줄인다(`Decimate`).
    // 줄인 결과는 상한 안이라 거친 줄이기를 다시 안 거친다(유니티 줄이기가 잔물결을 세 배로 키웠다 — 늑대인간 몸 0.020 → 0.059).

    // 세모 격자 — 점에서 가장 가까운 세모 위 자리(무게중심 좌표까지).
    sealed class TriGrid
    {
        readonly Vector3[] _v; readonly int[] _t; readonly float _cell; readonly Dictionary<Vector3Int, List<int>> _map = new();
        public TriGrid(Vector3[] v, int[] t, float cell)
        {
            _v = v; _t = t; _cell = cell;
            for (int f = 0; f < t.Length / 3; f++)
            {
                Vector3 a = v[t[3 * f]], b = v[t[3 * f + 1]], c = v[t[3 * f + 2]];
                var lo = Cell(Vector3.Min(a, Vector3.Min(b, c))); var hi = Cell(Vector3.Max(a, Vector3.Max(b, c)));
                for (int z = lo.z; z <= hi.z; z++) for (int y = lo.y; y <= hi.y; y++) for (int x = lo.x; x <= hi.x; x++)
                {
                    var k = new Vector3Int(x, y, z);
                    if (!_map.TryGetValue(k, out var l)) _map[k] = l = new List<int>();
                    l.Add(f);
                }
            }
        }
        Vector3Int Cell(Vector3 p) => new(Mathf.FloorToInt(p.x / _cell), Mathf.FloorToInt(p.y / _cell), Mathf.FloorToInt(p.z / _cell));

        // (세모, 무게중심 좌표, 거리²) — 껍질 반지름을 넓혀 가며 찾고, 찾은 거리가 껍질 안이면 멈춘다.
        public (int tri, Vector3 bary, float d2) Nearest(Vector3 p)
        {
            var home = Cell(p); int best = -1; var bb = Vector3.zero; float bd = float.MaxValue;
            for (int r = 0; r < 64; r++)
            {
                for (int z = -r; z <= r; z++) for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z))) != r) continue;
                    if (!_map.TryGetValue(home + new Vector3Int(x, y, z), out var l)) continue;
                    foreach (int f in l)
                    {
                        var (q, bc) = ClosestOnTri(p, _v[_t[3 * f]], _v[_t[3 * f + 1]], _v[_t[3 * f + 2]]);
                        float d2 = (q - p).sqrMagnitude;
                        if (d2 < bd) { bd = d2; best = f; bb = bc; }
                    }
                }
                if (best >= 0 && bd <= (r * _cell) * (r * _cell)) break;
            }
            return (best, bb, bd);
        }
    }

    // 세모 위 가장 가까운 점과 그 무게중심 좌표(a · b · c 몫).
    static (Vector3 q, Vector3 bary) ClosestOnTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return (a, new Vector3(1, 0, 0));
        Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return (b, new Vector3(0, 1, 0));
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f) { float v = d1 / (d1 - d3); return (a + ab * v, new Vector3(1 - v, v, 0)); }
        Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return (c, new Vector3(0, 0, 1));
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f) { float w = d2 / (d2 - d6); return (a + ac * w, new Vector3(1 - w, 0, w)); }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return (b + (c - b) * w, new Vector3(0, 1 - w, w)); }
        float den = 1f / (va + vb + vc); float vv = vb * den, ww = vc * den;
        return (a + ab * vv + ac * ww, new Vector3(1 - vv - ww, vv, ww));
    }

    static float MeanEdge(Vector3[] v, IList<int> t)
    {
        double s = 0; int n = 0;
        for (int i = 0; i < t.Count; i += 3) { s += (v[t[i]] - v[t[i + 1]]).magnitude; n++; }
        return n > 0 ? (float)(s / n) : 0f;
    }

    // `cap`보다 많은 구운 메시를 덩어리마다 다시 짜거나 줄인다. 도구가 없으면 false(부르는 쪽이 옛 길로).
    static bool TryRemeshBaked(string name, Vector3[] pts, Vector3[] nrm, Color[] col, BoneWeight[] bw, List<int> tris, int cap,
                               out Vector3[] oPts, out Vector3[] oN, out Color[] oCol, out BoneWeight[] oBw, out List<int> oTris)
    {
        oPts = null; oN = null; oCol = null; oBw = null; oTris = null;
        if (!File.Exists(AutoRemesherExe))
        {
            if (!_autoRemesherWarned) Debug.LogWarning($"[다시 짜기] {AutoRemesherExe}가 없다 — 줄인 그물을 그대로 쓴다(울퉁불퉁한 살 · 톱니 선).");
            _autoRemesherWarned = true;
            return false;
        }
        int nv = pts.Length;
        // 이어진 덩어리 — 세모로 묶는다.
        var root = new int[nv]; for (int i = 0; i < nv; i++) root[i] = i;
        int Find(int x) { while (root[x] != x) { root[x] = root[root[x]]; x = root[x]; } return x; }
        for (int i = 0; i < tris.Count; i += 3) { int a = Find(tris[i]); root[Find(tris[i + 1])] = a; root[Find(tris[i + 2])] = a; }
        var compOf = new Dictionary<int, int>(); var compVerts = new List<List<int>>();
        for (int i = 0; i < nv; i++) { int r = Find(i); if (!compOf.TryGetValue(r, out int c)) { compOf[r] = c = compVerts.Count; compVerts.Add(new List<int>()); } compVerts[c].Add(i); }
        var compTris = new List<List<int>>(); for (int c = 0; c < compVerts.Count; c++) compTris.Add(new List<int>());
        for (int i = 0; i < tris.Count; i += 3) compTris[compOf[Find(tris[i])]].AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });

        var P = new List<Vector3>(cap + 64); var N = new List<Vector3>(cap + 64); var C = new List<Color>(cap + 64); var W = new List<BoneWeight>(cap + 64); var T = new List<int>();
        int remeshed = 0;
        var local = new Dictionary<int, int>();
        var rest = new List<int>();   // 작은 덩어리 · 다시 짜기에 실패한 덩어리 — 모아서 예전처럼 한꺼번에 줄인다(잎 수천 장)
        for (int c = 0; c < compVerts.Count; c++)
        {
            var vs = compVerts[c]; var ts = compTris[c];
            int target = Mathf.RoundToInt((float)cap * vs.Count / nv);
            if (vs.Count >= RemeshMinVerts && vs.Count > target && target >= 300)
            {
                local.Clear(); for (int k = 0; k < vs.Count; k++) local[vs[k]] = k;
                var lp = new Vector3[vs.Count]; for (int k = 0; k < vs.Count; k++) lp[k] = pts[vs[k]];
                var lt = new int[ts.Count]; for (int k = 0; k < ts.Count; k++) lt[k] = local[ts[k]];
                if (RemeshPiece(lp, lt, vs, nrm, col, bw, target, P, N, C, W, T)) { remeshed++; continue; }
            }
            rest.Add(c);
        }

        // 옛 길 — 남은 덩어리를 한 그물로 모아 상한의 제 몫까지 줄인다.
        int restVerts = 0; foreach (int c in rest) restVerts += compVerts[c].Count;
        if (restVerts > 0)
        {
            local.Clear(); var rv = new List<int>(restVerts); var rt = new List<int>();
            foreach (int c in rest) foreach (int v in compVerts[c]) { local[v] = rv.Count; rv.Add(v); }
            foreach (int c in rest) foreach (int x in compTris[c]) rt.Add(local[x]);
            var rp = new Vector3[rv.Count]; for (int k = 0; k < rv.Count; k++) rp[k] = pts[rv[k]];
            int restTarget = Mathf.Max(64, Mathf.RoundToInt((float)cap * restVerts / nv));
            int baseIdx = P.Count;
            if (rv.Count > restTarget)
            {
                var kept = new List<int>();
                var dp = Decimate(rp, rt, restTarget, kept: kept);
                for (int k = 0; k < dp.Length; k++) { int j = rv[kept[k]]; P.Add(dp[k]); N.Add(nrm[j]); C.Add(col[j]); W.Add(bw[j]); }
            }
            else
                for (int k = 0; k < rv.Count; k++) { int j = rv[k]; P.Add(pts[j]); N.Add(nrm[j]); C.Add(col[j]); W.Add(bw[j]); }
            foreach (int x in rt) T.Add(baseIdx + x);
        }
        oPts = P.ToArray(); oN = N.ToArray(); oCol = C.ToArray(); oBw = W.ToArray(); oTris = T;
        _remeshDone += remeshed;
        Debug.Log($"[다시 짜기] {name}: 점 {nv} → {oPts.Length}(상한 {cap}) · 덩어리 {compVerts.Count}개 중 다시 짬 {remeshed} · 나머지 {rest.Count}개는 줄임");
        return true;
    }

    // 덩어리 하나 — 다시 짜서 원래 표면에 붙이고 값을 옮긴다. 술 · 가시가 지워졌으면 false.
    static bool RemeshPiece(Vector3[] lp, int[] lt, List<int> vs, Vector3[] nrm, Color[] col, BoneWeight[] bw, int target,
                            List<Vector3> P, List<Vector3> N, List<Color> C, List<BoneWeight> W, List<int> T)
    {
        RunAutoRemesher(lp, lt, Mathf.Max(400, Mathf.RoundToInt(target / 0.42f)), out var np, out var nt);
        if (np == null) return false;
        float eOld = MeanEdge(lp, lt);
        var oldGrid = new TriGrid(lp, lt, Mathf.Max(eOld * 3f, 1e-4f));
        var arr = np.ToArray(); var hit = new (int tri, Vector3 bary, float d2)[arr.Length];
        System.Threading.Tasks.Parallel.For(0, arr.Length, i =>
        {
            var h = oldGrid.Nearest(arr[i]); hit[i] = h;
            if (h.tri >= 0) arr[i] = lp[lt[3 * h.tri]] * h.bary.x + lp[lt[3 * h.tri + 1]] * h.bary.y + lp[lt[3 * h.tri + 2]] * h.bary.z;
        });
        if (Array.Exists(hit, h => h.tri < 0)) return false;

        // 술 · 가시가 남았나 — 원래 점(최대 2만 개)에서 새 표면까지.
        var ntArr = nt.ToArray(); float eNew = MeanEdge(arr, ntArr);
        var newGrid = new TriGrid(arr, ntArr, Mathf.Max(eNew * 3f, 1e-4f));
        int stride = Mathf.Max(1, lp.Length / 20000); float lim2 = (eNew * 0.75f) * (eNew * 0.75f);
        var farFlags = new int[(lp.Length + stride - 1) / stride];
        System.Threading.Tasks.Parallel.For(0, farFlags.Length, k => { farFlags[k] = newGrid.Nearest(lp[k * stride]).d2 > lim2 ? 1 : 0; });
        int far = 0; foreach (int x in farFlags) far += x;
        if (far > farFlags.Length * 0.005f) return false;

        // 값 옮기기 — 법선은 섞고, 색 · 가중치는 가장 가까운 원래 점.
        int baseIdx = P.Count; int agree = 0, total = 0;
        var nn = new Vector3[arr.Length];
        for (int i = 0; i < arr.Length; i++)
        {
            var h = hit[i]; int a = vs[lt[3 * h.tri]], b = vs[lt[3 * h.tri + 1]], c = vs[lt[3 * h.tri + 2]];
            nn[i] = (nrm[a] * h.bary.x + nrm[b] * h.bary.y + nrm[c] * h.bary.z).normalized;
            int j = h.bary.x >= h.bary.y && h.bary.x >= h.bary.z ? a : h.bary.y >= h.bary.z ? b : c;
            P.Add(arr[i]); N.Add(nn[i]); C.Add(col[j]); W.Add(bw[j]);
        }
        for (int i = 0; i < ntArr.Length; i += 3)
        {
            var fn = Vector3.Cross(arr[ntArr[i + 1]] - arr[ntArr[i]], arr[ntArr[i + 2]] - arr[ntArr[i]]);
            if (fn.sqrMagnitude < 1e-16f) continue;
            total++; if (Vector3.Dot(fn, nn[ntArr[i]] + nn[ntArr[i + 1]] + nn[ntArr[i + 2]]) > 0f) agree++;
        }
        bool flip = agree * 2 < total;
        for (int i = 0; i < ntArr.Length; i += 3)
        {
            T.Add(baseIdx + ntArr[i]);
            T.Add(baseIdx + (flip ? ntArr[i + 2] : ntArr[i + 1]));
            T.Add(baseIdx + (flip ? ntArr[i + 1] : ntArr[i + 2]));
        }
        return true;
    }
}
