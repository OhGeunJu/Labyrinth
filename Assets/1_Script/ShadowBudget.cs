using System.Collections.Generic;
using UnityEngine;

// **그림자를 던지는 불은 화면 가운데 몇 개뿐이다**(2026-10-11, 사용자: *"그림자를 끄면 어색하잖아"*).
//
// 점 조명 하나는 그림자를 **여섯 방향**으로 굽는다. 폐광 벽의 횃불이 화면 둘레에 일흔 개쯤 서면
// 그림자 지도가 수백 장이 되고, URP는 판(2048²)에 들어가는 만큼만 남기고 나머지를 버린다 — 어느
// 것을 버릴지는 URP가 고르므로 **가까운 횃불의 그림자가 빠지고 먼 것이 남기도 한다.**
//
// 그래서 고르는 것을 여기서 한다: **시선 축에 가까운 `MaxCasters`개만** 그림자를 던지고 나머지는
// 빛만 낸다. 멀리 있는 횃불의 그림자는 어차피 몇 픽셀이다.
//
// **바뀌는 순간이 안 보여야 한다** — 켜고 끄기만 하면 카메라를 밀 때 그림자가 툭툭 나타난다. 그래서
// 밀려날 차례가 가까운 것부터 그림자 세기를 0으로 줄여 두고, 0이 된 다음에 끈다.
//
// 세는 것은 `RockRenderer`가 세운 벽 조명뿐이다(그림자를 켠 프리팹이 그것뿐이다). 그림자를 켠 다른
// 불이 많아지면 세우는 자리에서 `Track`을 부를 것.
public static class ShadowBudget
{
    // 8 × 6 = 48장 — 2048² 판에 256²짜리가 64장 들어간다.
    public const int MaxCasters = 8;

    // 밀려나는 경계 안쪽 이만큼(비율)에서 세기가 준다.
    const float FadeBand = 0.35f;

    struct Entry
    {
        public Light        Light;
        public LightShadows Kind;       // 프리팹이 든 그림자 종류
        public float        Strength;   // 프리팹이 든 그림자 세기
    }

    static readonly List<Entry> _all    = new();
    static readonly List<float> _dist   = new();
    static readonly List<float> _sorted = new();

    public static void Track(GameObject go)
    {
        if (go == null) return;
        foreach (var l in go.GetComponentsInChildren<Light>(includeInactive: true))
        {
            if (l.shadows == LightShadows.None) continue;
            _all.Add(new Entry { Light = l, Kind = l.shadows, Strength = l.shadowStrength });
        }
    }

    public static void Tick(Camera cam)
    {
        if (cam == null || _all.Count == 0) return;

        Vector3 eye = cam.transform.position, axis = cam.transform.forward;

        _dist.Clear();
        _sorted.Clear();
        for (int i = _all.Count - 1; i >= 0; i--)
        {
            if (_all[i].Light != null) continue;
            _all[i] = _all[_all.Count - 1];      // 청크가 내려가며 같이 부서진 것
            _all.RemoveAt(_all.Count - 1);
        }

        // **카메라가 아니라 시선 축에서 잰다** — 카메라는 높이 떠 있어서 거기서 재면 줌에 따라 순서가
        // 뒤집힌다. 축에서 재면 화면 가운데에 가까운 것이 먼저다.
        for (int i = 0; i < _all.Count; i++)
        {
            Vector3 to = _all[i].Light.transform.position - eye;
            float d = (to - axis * Vector3.Dot(to, axis)).sqrMagnitude;
            _dist.Add(d);
            _sorted.Add(d);
        }

        // 경계 = 처음으로 밀려나는 것의 거리. 다 들어가면 경계가 없다.
        float cut = float.PositiveInfinity;
        if (_sorted.Count > MaxCasters)
        {
            _sorted.Sort();
            cut = Mathf.Sqrt(_sorted[MaxCasters]);
        }

        for (int i = 0; i < _all.Count; i++)
        {
            var e = _all[i];
            float k = float.IsPositiveInfinity(cut) ? 1f
                    : cut <= 0f ? 0f
                    : Mathf.Clamp01((cut - Mathf.Sqrt(_dist[i])) / (cut * FadeBand));

            var kind = k > 0f ? e.Kind : LightShadows.None;
            if (e.Light.shadows != kind) e.Light.shadows = kind;
            if (k > 0f) e.Light.shadowStrength = e.Strength * k;
        }
    }
}
