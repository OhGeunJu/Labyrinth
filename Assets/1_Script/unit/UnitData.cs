using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StatGradeEntry
{
    public StatName Stat;
    public StatGrade Min = StatGrade.D;
    public StatGrade Max = StatGrade.D;
}

[System.Serializable]
public class StatGrowthEntry
{
    public StatName Stat;
    [Tooltip("이 스탯의 성장 속도 개별 배율. 종족 GrowthRate에 곱해짐")]
    public float Multiplier = 1f;
}

[CreateAssetMenu(fileName = "UnitData", menuName = "DungeonColony/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("식별자. 비우면 에셋 파일명을 쓴다(권장). 표시명과 달리 번역되지 않고 바뀌지 않아야 한다.")]
    public string Id;

    [Tooltip("화면에 보이는 이름. 번역 대상이므로 **식별자로 쓰지 말 것** — Id를 쓴다.")]
    public string RaceName;
    public Sprite Icon;

    [Tooltip("개체 고유 이름 후보. 소환 시 랜덤 1개 선택. 비어 있으면 RaceName 사용.")]
    public List<string> NamePool = new();
    public float MaxHP      = 100f;
    public float MoveSpeed  = 4f;

    [Header("전투")]
    public float AttackDamage  = 10f;
    public float AttackRange   = 1.5f;
    public float DetectRange   = 10f;
    // 전투 성향(교전/도주)·유휴 배회는 종족이 아니라 개체의 역할이므로 여기가 아니라
    // AI 프로필(AiProfileData SO, UnitBrain.aiProfile에 연결)에 있다 — 같은 "사람"이라도 마을사람/경비병이 다르다.

    [Header("등급 (기본)")]
    [Tooltip("오버라이드되지 않은 모든 스탯의 기본 등급 범위")]
    public StatGrade DefaultMinGrade = StatGrade.D;
    public StatGrade DefaultMaxGrade = StatGrade.D;

    [Header("등급 오버라이드 (스탯별)")]
    [Tooltip("특정 스탯에 다른 등급 범위 지정. Min==Max면 고정값")]
    public List<StatGradeEntry> StatGrades = new();

    [Header("성장 속도 (스탯별)")]
    [Tooltip("이 종족이 특정 스탯만 빠르게/느리게 성장. 목록에 없는 스탯은 기본(1.0)")]
    public List<StatGrowthEntry> StatGrowthRates = new();

    public float GetGrowthRate(StatName stat)
    {
        for (int i = 0; i < StatGrowthRates.Count; i++)
            if (StatGrowthRates[i].Stat == stat) return StatGrowthRates[i].Multiplier;
        return 1f;
    }

    public (StatGrade min, StatGrade max) GetGradeRange(StatName stat)
    {
        for (int i = 0; i < StatGrades.Count; i++)
            if (StatGrades[i].Stat == stat) return (StatGrades[i].Min, StatGrades[i].Max);
        return (DefaultMinGrade, DefaultMaxGrade);
    }

    public StatGrade RollGrade(StatName stat)
    {
        var (min, max) = GetGradeRange(stat);
        return StatGradeExt.RandomBetween(min, max);
    }
}
