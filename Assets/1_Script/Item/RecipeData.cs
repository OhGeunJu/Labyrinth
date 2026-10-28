using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/RecipeData")]
public class RecipeData : ScriptableObject
{
    [Serializable]
    public struct Ingredient
    {
        public ItemData Item;
        public int      Amount;
    }

    [Tooltip("식별자. 비우면 에셋 파일명을 쓴다(권장). 표시명과 달리 번역되지 않는다.")]
    public string           Id;

    [Tooltip("화면에 보이는 이름. 번역 대상이므로 **식별자로 쓰지 말 것**.")]
    public string           RecipeName;
    public List<Ingredient> Ingredients  = new();
    public ItemData         Result;
    public int              ResultAmount = 1;
    public float            CraftTime    = 5f;
    public JobCategory      Category     = JobCategory.Crafting;

    [Header("스킬")]
    public CraftSkill RequiredSkillType  = CraftSkill.Labor;
    [Tooltip("이 수치 미만이면 결과물 50% 패널티")]
    public float      RequiredSkillLevel = 0f;
    [Tooltip("제작 완료 시 스킬 성장량")]
    public float      SkillGrowthAmount  = 0.5f;


    [Header("연구 해금")]
    [Tooltip("null이면 처음부터 개방. 지정 시 해당 연구 완료 후 제작 가능.")]
    public ResearchData RequiredResearch;

    // **어느 등급의 작업대라야 만드나**(2026-09-17, `BenchTier`).
    //
    // 연구와 **다른 축이다**: 연구는 *배웠는가*이고 이건 *설비가 되는가*다. 둘 다 잠그면
    // 이중 잠금이므로 **상위 금속 장비에만** 건다 — 티타늄·미스릴은 배워서 되는 것이
    // 아니라 **제대로 된 모루가 있어야** 두들겨진다.
    //
    // **0이 기본이고 그건 "아무 데서나"다** — 새 필드는 0에서 시작하므로 이미 저작된
    // 레시피 백예순여덟이 전부 그대로 열려 있다.
    [Tooltip("이 등급 이상의 작업대라야 만든다. 0·1 = 아무 작업대나, 2 = 개량 작업대. " +
             "연구(배웠는가)와 다른 축이다 — 설비가 되는가를 묻는다.")]
    public int RequiredBenchTier = 0;
}
