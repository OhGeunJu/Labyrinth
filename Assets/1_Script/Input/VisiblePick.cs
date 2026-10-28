using System.Collections.Generic;
using UnityEngine;

// **커서가 짚는 것은 화면에 보이는 것이다 — 잘린 벽의 사라진 윗부분이 아니다**(2026-09-19).
//
// (사용자: *"카메라를 내리면 벽이 낮아지는데 원래위치에 있던 벽이 먼저 맞아서 판정이 이상해졌던 거구나"* ·
//  *"나머지도 맞춰줘"*.) 벽은 눈높이를 따라 **잘려** 그려지는데(`RockCut.hlsl`) 콜라이더는 24m 그대로다.
// 그냥 쏘면 레이가 **화면에 없는 윗부분**에 먼저 맞아, 겨눈 것보다 카메라 쪽 칸·벽이 잡힌다. 콜라이더를
// 낮추는 길은 안 된다 — 자르는 높이는 카메라를 따라 매 프레임 바뀌고, **막는 판정이 화면을 따라가면 안 된다.**
//
// 그래서 **짚는 쪽이 자르는 면(`RockRenderer.CutY`)을 안다** — 커서로 월드를 짚는 자리(채굴 지정 · 선택 ·
// 우클릭 · 이동 목적지 · 진형 끌기)는 전부 여기를 지난다. 갈라 적으면 **한 조작만 딴 칸을 짚는다.**
//
//   1. 자르는 면 위에서 맞은 **바위**는 버린다(안 보인다). 바위가 아닌 것(폰·건물·광맥)은 안 잘리므로 남긴다.
//   2. 자르는 면에서 그 칸이 바위면 **보이는 뚜껑**을 맞힌 것이다.
//   3. 뚜껑이 아니면 **면 아래로 이어 쏜다** — 보이는 벽 옆면과 바닥이 거기서 잡힌다.
//
// 자르기가 꺼졌거나 카메라가 면 아래면 그냥 쏜 것과 같다.
public struct PickHit
{
    public Collider collider;
    public Vector3  point;
    public Vector3  normal;
    public float    distance;
}

public static class VisiblePick
{
    static readonly List<PickHit>   _hits = new();
    static readonly List<Collider>  _seenRock = new();
    static readonly Collider[]      _overlap = new Collider[8];

    // 보이는 것만, 가까운 순. **돌려준 목록은 다음 호출에서 재사용된다** — 들고 있지 말 것.
    public static List<PickHit> All(Ray ray, float maxDistance = 500f, int mask = ~0,
                                    QueryTriggerInteraction triggers = QueryTriggerInteraction.UseGlobal)
    {
        _hits.Clear();
        _seenRock.Clear();

        bool cut = RockRenderer.CutActive && ray.origin.y > RockRenderer.CutY && ray.direction.y < -1e-4f;
        float cutY = RockRenderer.CutY;

        foreach (var h in Physics.RaycastAll(ray, maxDistance, mask, triggers))
        {
            bool rock = RockRenderer.IsRockCollider(h.collider);
            if (cut && rock && h.point.y > cutY + 0.01f) continue;   // 잘려서 안 보이는 윗부분
            if (rock) _seenRock.Add(h.collider);
            _hits.Add(new PickHit { collider = h.collider, point = h.point, normal = h.normal, distance = h.distance });
        }

        if (cut)
        {
            float t = (cutY - ray.origin.y) / ray.direction.y;
            if (t <= maxDistance)
            {
                var lid = ray.GetPoint(t);
                var map = MapManager.Instance;

                if (map != null && RockMap.IsRock(map.WorldToTile(lid)))
                {
                    // 보이는 뚜껑 — 그 칸을 덮은 바위 콜라이더를 찾아 붙인다(선택·살펴보기가 콜라이더를 본다).
                    var box = RockAt(lid, mask);
                    if (box != null && !_seenRock.Contains(box))
                    {
                        _seenRock.Add(box);
                        _hits.Add(new PickHit { collider = box, point = lid, normal = Vector3.up, distance = t });
                    }
                }
                else
                {
                    // 면 아래로 이어 쏜다 — 보이는 벽 옆면. 바위가 아닌 것은 위에서 이미 담았다.
                    var below = new Ray(lid + ray.direction * 0.01f, ray.direction);
                    foreach (var h in Physics.RaycastAll(below, maxDistance - t, mask, triggers))
                    {
                        if (!RockRenderer.IsRockCollider(h.collider) || _seenRock.Contains(h.collider)) continue;
                        _seenRock.Add(h.collider);
                        _hits.Add(new PickHit { collider = h.collider, point = h.point, normal = h.normal,
                                                distance = h.distance + t + 0.01f });
                    }
                }
            }
        }

        _hits.Sort((a, b) => a.distance.CompareTo(b.distance));
        return _hits;
    }

    // 가장 가까운 보이는 것 하나.
    public static bool First(Ray ray, out PickHit hit, float maxDistance = 500f, int mask = ~0,
                             QueryTriggerInteraction triggers = QueryTriggerInteraction.UseGlobal)
    {
        var all = All(ray, maxDistance, mask, triggers);
        hit = all.Count > 0 ? all[0] : default;
        return all.Count > 0;
    }

    static Collider RockAt(Vector3 p, int mask)
    {
        int n = Physics.OverlapSphereNonAlloc(p + Vector3.down * 0.05f, 0.02f, _overlap, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (RockRenderer.IsRockCollider(_overlap[i])) return _overlap[i];
        return null;
    }
}
