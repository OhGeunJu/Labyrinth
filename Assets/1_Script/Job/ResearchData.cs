using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ResearchData")]
public class ResearchData : ScriptableObject
{
    // **JSON 아이템도 재료가 될 수 있어야 한다**(2026-08-24).
    //
    // `Item`은 SO 참조라 **JSON 아이템을 못 가리킨다** — 그것들은 런타임 생성이다
    // (`MerchantPoolEntry`·`LoadoutData.ItemId`가 같은 이유로 같은 문을 냈다).
    // 실제로 공방 연구 다섯이 천·약초·붕대·두꺼운 가죽을 요구하는데 전부 JSON이라,
    // 도구가 `DataManager.GetItem`으로 찾아 넣으려다 **에디터에서는 레지스트리가 안 서 있어
    // 재료가 통째로 비었다.** 값이 0이면 조용히 *"재료 없음"*이 되므로 화면에서 안 드러난다.
    //
    // **읽는 쪽은 반드시 `Resolve()`를 쓸 것** — `Item`을 직접 읽으면 JSON 재료가 null이다.
    [Serializable]
    public struct Ingredient
    {
        [Tooltip("재료(에셋). 비우면 아래 ItemId로 찾는다.")]
        public ItemData Item;
        [Tooltip("JSON·모드 아이템용 id. `Item`이 비었을 때만 쓴다.")]
        public string   ItemId;
        public int      Amount;

        public ItemData Resolve() => Item != null ? Item : DataManager.GetItem(ItemId);

        // 아직 못 찾은 재료도 이름은 보여 준다 — 안 그러면 빈 줄이 뜬다.
        public string DisplayName
        {
            get { var it = Resolve(); return it != null ? it.ItemName : (string.IsNullOrEmpty(ItemId) ? "?" : ItemId); }
        }
    }

    [Header("기본 정보")]
    [Tooltip("식별자. 비우면 에셋 파일명을 쓴다(권장). 표시명과 달리 번역되지 않는다.")]
    public string           Id;
    public string           ResearchName;
    [TextArea]
    public string           Description;

    [Header("연구 비용")]
    public List<Ingredient> Ingredients       = new();
    public float            ResearchTime      = 30f;   // 초
    public float            SkillGrowthAmount = 1f;    // Research 스킬 성장량

    // **선행은 여럿일 수 있다**(2026-08-23). 단일 참조였을 때는 사슬만 표현됐다 —
    // *"제련술과 목공술을 둘 다 끝내야 열리는 것"*을 적을 자리가 없었고, 그러면 연구가
    // 아무리 늘어도 화면은 일렬로만 뻗는다. 리스트로 두면 그래프가 곧 저작물이 된다.
    //
    // **비어 있으면 처음부터 열린 연구다.**
    [Header("선행 연구")]
    public List<ResearchData> Prerequisites = new();

    // **자리는 저작한다 — 규격에 맞춰 밀어 넣지 않는다**(2026-08-24, 사용자 결정).
    //
    // 자동 배치는 *"열 = 가장 긴 선행 사슬"*이라 **선행 없는 연구가 전부 맨 왼쪽에 쌓인다.**
    // 림월드의 연구 트리는 그렇지 않다 — 어떤 것은 한가운데에서 시작하고, 어떤 것은 처음
    // 것에서 쭉 이어져 온다. 그 모양은 **계산이 아니라 저작**이다(`WorldMap`이 지도를 절차
    // 생성하지 않는 것과 같은 자리).
    //
    // **−1이면 자동**이다. 하나도 안 적으면 예전처럼 통째로 자동 배치된다.
    //
    // ── 저작할 때 지킬 것 하나 ─────────────────────────────────────────────
    //
    // 선은 **직각으로 꺾이고 세로 구간이 두 열 사이 한가운데 선다**(`ResearchLinkGraphic`).
    // 그래서 선행과 두 열 이상 떨어뜨리면 **그 사이 열에 놓인 노드를 세로 선이 관통한다.**
    // 떨어뜨려야 한다면 그 통로에 해당하는 칸(사이 열 × 두 노드 사이 행)을 비워 둘 것.
    [Header("그래프 자리 (−1 = 자동)")]
    public int GraphColumn = -1;
    public int GraphRow    = -1;

    public bool HasAuthoredPos => GraphColumn >= 0 && GraphRow >= 0;

    [Header("해금 레시피")]
    public List<RecipeData> UnlockedRecipes = new();
}
