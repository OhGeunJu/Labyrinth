using System.Collections.Generic;
using UnityEngine;

// **상단들을 들고 시간을 굴린다**(2026-10-01, `Caravan`). 씬 배선이 없다 — 저작할 것이 없는 순수 관리자라
// `DraftController`처럼 스스로 선다(`SelfStanding`).
//
// 짐칸은 상단마다 **숨은 `Inventory`** 하나다 — 일행의 배낭을 쓰면 격자 크기가 폰마다 달라 *"무게는 남는데 칸이 없다"*가
// 생기고, 대표의 배낭에 산 물건이 섞인다. 한도는 무게 하나(`Caravan.Capacity` — 일행의 적재 합, 수레 포함)다.
public class CaravanManager : MonoBehaviour
{
    public static CaravanManager Instance { get; private set; }

    readonly List<Caravan> _caravans = new();
    public IReadOnlyList<Caravan> Caravans => _caravans;

    public static event System.Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => SelfStanding.Create<CaravanManager>("CaravanManager");

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public Caravan Create()
    {
        var go = new GameObject($"[CaravanCargo {_caravans.Count + 1}]") { hideFlags = SelfStanding.RuntimeObject };
        go.transform.SetParent(transform, false);
        go.SetActive(false);   // 보이지도 · 눌리지도 않는다 — 짐 담는 그릇일 뿐
        var inv = go.AddComponent<Inventory>();
        inv.Resize(24, 48);

        var c = new Caravan(inv) { Name = $"{_caravans.Count + 1}번 상단", Home = DungeonCore.Instance };
        _caravans.Add(c);
        OnChanged?.Invoke();
        return c;
    }

    // 없앤다 — 길에 있으면 먼저 데려온다(사람과 짐을 포탈 너머에 버리지 않는다).
    public void Remove(Caravan c)
    {
        if (c == null || !_caravans.Contains(c)) return;
        c.ForceReturn();
        if (c.Cargo != null) Destroy(c.Cargo.gameObject);
        _caravans.Remove(c);
        OnChanged?.Invoke();
    }

    // 이 폰이 지금 어느 상단에 묶여 있나(모이는 중 · 길 위). 설정에 이름만 올라 있는 것은 안 센다.
    public static Caravan OwnerOf(UnitBrain b)
    {
        var m = Instance;
        if (m == null || b == null) return null;
        foreach (var c in m._caravans)
            if (c.State != CaravanState.Idle && ContainsAway(c, b)) return c;
        return null;
    }

    static bool ContainsAway(Caravan c, UnitBrain b)
    {
        foreach (var a in c.Away) if (a == b) return true;
        return false;
    }

    void Update()
    {
        for (int i = 0; i < _caravans.Count; i++) _caravans[i].Tick();
    }
}
