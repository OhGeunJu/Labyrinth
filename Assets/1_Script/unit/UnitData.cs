using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StatGradeEntry
{
    public StatName Stat;
    public StatGrade Min = StatGrade.D;
    public StatGrade Max = StatGrade.D;
}

[CreateAssetMenu(fileName = "UnitData", menuName = "DungeonColony/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("기본 정보")]
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

    [Header("등급 (기본)")]
    [Tooltip("오버라이드되지 않은 모든 스탯의 기본 등급 범위")]
    public StatGrade DefaultMinGrade = StatGrade.D;
    public StatGrade DefaultMaxGrade = StatGrade.D;

    [Header("등급 오버라이드 (스탯별)")]
    [Tooltip("특정 스탯에 다른 등급 범위 지정. Min==Max면 고정값")]
    public List<StatGradeEntry> StatGrades = new();

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
