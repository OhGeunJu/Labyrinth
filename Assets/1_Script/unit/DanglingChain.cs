using System;
using System.Collections.Generic;
using UnityEngine;

// **늘어진 사슬이 흔들린다**(2026-11-02, 사용자: *"고리를 길게 달면 모션을 할 때 물리력이 없어서 너무 떠 보이려나?"*).
//
// 사슬을 메시에 그대로 구우면 팔을 휘두를 때 팔과 한 몸으로 돌아 뻣뻣하게 뜬다. 그래서 고리마다 따로 굽고(`ChibiPawnCreator.Troll` —
// 이름 `{관절}.Pick.{축}.{벌}.Link{n}`) 여기서 고리마다 **걸린 자리를 축으로** 아래로 처지며 늦게 따라오게 한다. 물리 엔진은 안 쓴다 —
// 폰이 많아도 고리 몇 개를 돌릴 뿐이다.
//
// - **축은 판에서 세운다** — 처음 켜질 때(바인드 자세) 고리의 위 끝에 빈 축을 두고 고리를 그 밑으로 옮긴다. 다음 고리의 축은 앞
//   고리 축의 자식이라 사슬이 한 줄로 이어진다. 고르는 축(`PawnLook`)은 렌더러를 켜고 끌 뿐이라 옮겨도 그대로 돈다.
// - **흔들림** — 늦은 업데이트(애니메이션 뒤)마다 축이 "곧게 매달린 자세"로 다가간다(`stiffness`). 부모(팔 · 머리)에 대해
//   `maxBend`보다 더 꺾이지 않는다(고리가 살을 뚫고 들어가지 않게).
[DisallowMultipleComponent]
public class DanglingChain : MonoBehaviour
{
    [Tooltip("곧게 매달린 자세로 얼마나 빨리 돌아오나(클수록 덜 흔들린다).")]
    [SerializeField] float stiffness = 9f;
    [Tooltip("부모에 대해 가장 많이 꺾이는 각(도).")]
    [SerializeField] float maxBend = 65f;

    // 고리가 걸린 곳 — 고리 상자의 위 끝에서 이만큼(높이 반에 대한 비) 내려온 안쪽 끝(쇠 굵기 둘 ÷ 바깥 반길이 ≈ 0.38 — `ChibiPawnCreator.Link`).
    const float HookFromTop = 0.38f;

    sealed class Seg
    {
        public Transform Pivot;
        public Quaternion RestLocal;   // 부모에 대한 처음 자세(곧게 매달렸다)
        public Quaternion World;       // 지난 프레임의 자세 — 늦게 따라오게
    }

    readonly List<List<Seg>> _chains = new();

    void Start() => Build();

    void Build()
    {
        var model = transform.Find("Model");
        if (model == null) return;

        // 이름의 앞머리(`…Link` 앞)가 같은 고리끼리 한 사슬이다.
        var groups = new Dictionary<string, SortedDictionary<int, Renderer>>();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name;
            int at = n.LastIndexOf(".Link", StringComparison.Ordinal);
            if (at < 0 || !int.TryParse(n.Substring(at + 5), out int index)) continue;
            string key = n.Substring(0, at);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new SortedDictionary<int, Renderer>();
            list[index] = r;
        }

        foreach (var group in groups.Values)
        {
            var chain = new List<Seg>();
            Transform parent = null;
            foreach (var r in group.Values)
            {
                parent ??= r.transform.parent;   // 첫 고리가 붙은 관절(팔뚝 · 머리)
                var b = r.bounds;
                var pivot = new GameObject(r.name + ".Pivot").transform;
                pivot.SetParent(parent, false);
                pivot.position = b.center + Vector3.up * (b.extents.y * (1f - HookFromTop));
                pivot.rotation = parent.rotation;
                r.transform.SetParent(pivot, true);
                chain.Add(new Seg { Pivot = pivot, RestLocal = pivot.localRotation, World = pivot.rotation });
                parent = pivot;
            }
            if (chain.Count > 0) _chains.Add(chain);
        }
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // 멈춘 동안은 그대로
        float k = 1f - Mathf.Exp(-stiffness * dt);
        foreach (var chain in _chains)
        foreach (var s in chain)
        {
            if (s.Pivot == null) continue;
            var rest = s.Pivot.parent.rotation * s.RestLocal;
            var hang = Quaternion.FromToRotation(rest * Vector3.down, Vector3.down) * rest;   // 곧게 매달린 자세
            var next = Quaternion.Slerp(s.World, hang, k);
            if (Quaternion.Angle(rest, next) > maxBend) next = Quaternion.RotateTowards(rest, next, maxBend);
            s.Pivot.rotation = next;
            s.World = next;
        }
    }
}
