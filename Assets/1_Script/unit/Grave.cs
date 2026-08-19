using System.Collections.Generic;
using UnityEngine;

// **무덤 — 시체를 땅에 묻어 치운다.** 하나가 들어간다.
//
// 왜 필요한가: 시체는 저절로 사라지지 않고 **뼈는 영영 남는다**(`CorpseStage`). 도축이
// 유일한 치우는 길이었는데 그건 **나올 것이 있는 몸에만** 통한다 — 사람 시체는
// `Butchering.HasYield`가 거짓이라 도축 자체가 안 떴고, 그래서 **동료의 시신을 치울 길이
// 아예 없었다.**
//
// **화장터와 나란한 물건이다**(`Pyre`). 고르는 일이 되도록 값을 갈랐다:
//
//   무덤   — 값이 싸고 즉시 끝난다. 대신 **그 칸을 영영 먹고**, 몸이 세상에 남는다.
//   화장터 — 목재를 태우고 시간이 걸린다. 대신 **끝난다** — 되돌릴 수 없다.
//
// 몸이 남는 것이 나중에 값을 갖는다: 붙일 예정인 *"교단·망자 습격이 남은 시체를 일으킨다"*는
// 무덤을 **미루는 선택**으로 만든다(파묻은 것도 파낼 수 있다 — `Corpse.SetBuried`가 그 문이다).
public class Grave : MonoBehaviour, IWorldClickable, ICarrySite
{
    public static readonly List<Grave> All = new();
    public static event System.Action<Grave> OnSelected;

    [Tooltip("시체가 놓일 자리. 비우면 무덤 한가운데.")]
    [SerializeField] Transform graveAnchor;

    public Corpse Buried { get; private set; }
    public bool   IsOccupied => Buried != null;

    public Vector3 GravePosition => graveAnchor != null ? graveAnchor.position : transform.position;

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // **무덤이 철거되면 시체가 도로 나온다.** 안 꺼내면 묻어 둔 몸이 건물과 함께 조용히
    // 사라진다 — 이 저장소가 되풀이해 막아 온 "조용한 손실"이 그대로다. 파내는 길이
    // 하나(`Exhume`)라 여기서도 그걸 부른다.
    //
    // **`OnDisable`이 아니라 `OnDestroy`다.** 정착지를 옮기면 그쪽 루트가 통째로 꺼지는데
    // (`SettlementManager` — 한 번에 하나만 시뮬된다), 거기서 파내면 **마을을 한 번 다녀오는
    // 것만으로 무덤이 전부 열린다.** 되돌려야 하는 것은 *꺼짐*이 아니라 *없어짐*이다.
    void OnDestroy() => Exhume();

    // ── `ICarrySite` ─────────────────────────────────────────────────────────
    //
    // **시체만 받는다.** 우리·침대가 시체를 거절하는 것과 정확히 짝이다 — 산 사람을 묻는
    // 것은 이 게임에 없는 동작이고, 있다면 그건 매장이 아니라 처형이다.
    public bool Accepts(UnitBrain brain)
        => brain != null && !IsOccupied && PawnCarry.IsCorpse(brain);

    Vector3     ICarrySite.PutPosition => GravePosition;
    Quaternion? ICarrySite.PutRotation => null;
    bool        ICarrySite.IsFull      => IsOccupied;
    string      ICarrySite.SiteName    => "무덤";

    void ICarrySite.Put(UnitBrain brain)
    {
        if (!Accepts(brain)) return;

        var corpse = brain.GetComponent<Corpse>();
        if (corpse == null) return;

        Buried = corpse;

        // 무덤의 자식으로 붙인다 — 무덤을 옮기면(`BuildingRelocator`) 같이 간다.
        // **부모 크기를 상쇄하지 않는다**: 무덤은 스케일이 1이고, 업기(`PawnCarry`)와 달리
        // 여기 붙는 것은 화면에서 안 보이는 물건이라 크기가 뜻을 갖지 않는다.
        corpse.transform.SetParent(transform, worldPositionStays: true);
        corpse.transform.position = GravePosition;
        corpse.SetBuried(true);

        EventNotificationUI.Notify("매장", $"{corpse.UnitName}을(를) 묻었습니다.");
    }

    // **파낸다** — 되돌리는 길. 장비를 안 뒤지고 묻었을 때, 그리고 나중에 붙일
    // *"교단이 무덤을 파낸다"*가 같은 문을 쓴다.
    public void Exhume()
    {
        var corpse = Buried;
        Buried = null;
        if (corpse == null) return;

        corpse.transform.SetParent(null, worldPositionStays: true);
        corpse.SetBuried(false);

        // **흙 위로 올려놓는다** — 묻힌 자리 그대로 두면 무덤 모델과 겹쳐 클릭이 갈린다.
        // 시체도 폰이라 세우는 규약은 하나(`PawnPlacement`)를 쓴다.
        PawnPlacement.PlaceOnGround(corpse.gameObject, GravePosition);
    }

    public void OnWorldClick() => OnSelected?.Invoke(this);

    // **이 시체를 물고 있는 무덤.** `PrisonCage.HolderOf`와 같은 자리 — 파내야 하는 쪽
    // (교단이 무덤을 판다, `Necromancy`)이 시체만 들고 있어도 자리를 되짚을 수 있어야 한다.
    public static Grave HolderOf(Corpse corpse)
    {
        if (corpse == null) return null;
        foreach (var g in All)
            if (g != null && g.Buried == corpse) return g;
        return null;
    }

    public static Grave FindNearestFree(Vector3 from)
    {
        Grave best = null;
        float bestSq = float.MaxValue;
        foreach (var g in All)
        {
            if (g == null || g.IsOccupied) continue;
            float sq = (g.transform.position - from).sqrMagnitude;
            if (sq >= bestSq) continue;
            best   = g;
            bestSq = sq;
        }
        return best;
    }
}
