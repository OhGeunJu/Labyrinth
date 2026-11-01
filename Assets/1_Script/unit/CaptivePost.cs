using System.Collections.Generic;
using UnityEngine;

// **먹는 종족의 굴에 박힌 기둥 — 붙잡아 온 사람을 등지게 묶는다**(2026-11-01, 사용자: *"우리가 감옥에 가두는 것처럼 적들도
// 감옥이나 봉 같은 곳에 캐릭을 묶고 뜯어먹으면 좋겠어 켄시처럼"*).
//
// 우리(`PrisonCage`)와 나란한 물건이다 — 묶는 것은 같은 갇힘(`Imprisonment.Bind`)이고, 다른 것은 **누가 세우나**뿐이다.
// 우리는 내가 짓고 이것은 납치한 놈이 굴에 닿을 때 세운다(`Abduction.Deliver` → `Raise`). 같은 굴에 빈 기둥이 있으면 그것을
// 쓴다. 모양은 `Resources/Prefabs/CaptivePost`(`World → L`이 굽는다) — 없으면 모양 없이 선다(경고).
//
// 먹히는 것 · 탈출은 묶인 사람에게 붙는 `Devoured`가 든다. 기둥은 **누가 묶여 있나**와 **어디에 서나**만 안다.
public class CaptivePost : MonoBehaviour
{
    const string PrefabPath = "Prefabs/CaptivePost";   // Assets/Resources/Prefabs/CaptivePost.prefab
    const float  ShareRadius = 8f;                      // 같은 굴로 치는 거리(m) — 이 안의 빈 기둥을 쓴다
    const float  Spacing     = 2.2f;                    // 한 굴에 기둥이 여럿이면 이만큼 띄운다(m)

    static readonly List<CaptivePost> _all = new();
    public static IReadOnlyList<CaptivePost> All => _all;

    public UnitBrain   Captive { get; private set; }
    public FactionData Captor  { get; private set; }

    // 묶인 사람이 서는 자리 · 보는 쪽 — 기둥 앞에서 기둥을 등진다.
    public Vector3    HoldPosition => transform.position + transform.forward * 0.4f;
    public Quaternion HoldRotation => Quaternion.LookRotation(transform.forward, Vector3.up);
    // 먹는 놈이 서는 자리 — 묶인 사람 앞.
    public Vector3    EaterSpot    => transform.position + transform.forward * 1.3f;

    // **눌리는 몸** — 기둥을 눌러 묶인 사람을 다룬다(사용자: *"폰 누르는 것보다는 봉을 누르게"*). 트리거라 길을 안 막는다
    // (`VisiblePick`은 트리거도 짚는다). 모양 프리팹에도, 모양 없이 선 기둥에도 같이 붙도록 여기서 단다.
    void Awake()
    {
        if (TryGetComponent(out Collider _)) return;
        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center    = new Vector3(0f, 1.15f, 0f);
        box.size      = new Vector3(0.6f, 2.3f, 0.6f);
    }

    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

    void Update()
    {
        if (Captive == null) return;
        var bb = Captive.Blackboard;
        if (!Captive.isActiveAndEnabled || PawnCarry.IsCorpse(Captive) || bb == null || !bb.IsPrisoner || bb.CarriedBy != null)
            Captive = null;   // 풀려났거나(업혀 나갔다 · 달아났다) 죽었다 — 기둥은 빈다
    }

    // 굴에 기둥을 하나 내준다 — 그 굴의 빈 기둥이 있으면 그것.
    public static CaptivePost Raise(Vector3 den, FactionData captor)
    {
        int near = 0;
        foreach (var p in _all)
        {
            if (p == null || p.Captor != captor) continue;
            var d = p.transform.position - den; d.y = 0f;
            if (d.sqrMagnitude > ShareRadius * ShareRadius) continue;
            if (p.Captive == null) return p;
            near++;
        }

        // 빈 것이 없으면 새로 — 이미 선 기둥 둘레로 돌려 띄운다(묶인 사람끼리 겹치지 않게).
        var at = den;
        if (near > 0)
        {
            float a = near * 2.4f;
            at += new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Spacing;
            if (!RockMap.TryNearestOpen(at, 4f, out at)) at = den;
        }
        var map = MapManager.Instance;
        if (map != null) at.y = map.SampleGroundHeight(at);

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null) go = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        else
        {
            Debug.LogWarning($"[납치] 기둥 모양이 없다 — Resources/{PrefabPath} (`World → L`이 굽는다). 모양 없이 세운다.");
            go = new GameObject("CaptivePost");
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        }
        go.hideFlags = SelfStanding.RuntimeObject;
        if (!go.TryGetComponent(out CaptivePost post)) post = go.AddComponent<CaptivePost>();
        post.Captor = captor;
        return post;
    }

    // 묶는다 — 갇힘은 한 벌(`Imprisonment.Bind`) · 먹히는 것과 탈출은 `Devoured`.
    public void Tie(UnitBrain brain)
    {
        if (brain == null) return;
        Captive = brain;
        Imprisonment.Bind(brain, HoldPosition, HoldRotation);
        if (!brain.TryGetComponent(out Devoured d)) d = brain.gameObject.AddComponent<Devoured>();
        d.Begin(this);
        Debug.Log($"[납치] {brain.UnitName}을(를) 기둥에 묶었다");
    }

    // 이 세력이 이 굴 둘레에 누군가를 묶어 두고 있나.
    public static bool HoldsCaptive(FactionData faction, Vector3 den, float radius)
    {
        foreach (var p in _all)
        {
            if (p == null || p.Captive == null || p.Captor != faction) continue;
            var d = p.transform.position - den; d.y = 0f;
            if (d.sqrMagnitude <= radius * radius) return true;
        }
        return false;
    }
}
