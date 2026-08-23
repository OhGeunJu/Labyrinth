using UnityEngine;

// 스탯 열 하나 — 머리글 + 줄들. **줄은 씬이 갖고 여기는 값만 흘려보낸다.**
public class StatColumnUI : MonoBehaviour
{
    [Tooltip("`StatColumns.All`에서 이 열이 몇 번째인가")]
    [SerializeField] int columnIndex;
    [Tooltip("위에서 아래 순서. `StatColumns.All[columnIndex].Stats`와 **개수가 같아야 한다**")]
    [SerializeField] StatRowUI[] rows;

    public void Bind(UnitStats stats, System.Collections.Generic.HashSet<StatName> held)
    {
        if (rows == null || stats == null) return;
        if (columnIndex < 0 || columnIndex >= StatColumns.All.Length) return;

        var list = StatColumns.All[columnIndex].Stats;

        // **짧은 쪽에 맞춘다.** 표에 스탯을 더하고 도구를 안 돌렸으면 줄이 모자란데, 거기서
        // 터지는 것보다 **있는 만큼 채우고 도구를 돌리라고 알리는 편**이 낫다.
        int n = Mathf.Min(rows.Length, list.Length);
        for (int i = 0; i < n; i++)
            if (rows[i] != null) rows[i].Bind(stats, list[i], held != null && held.Contains(list[i]));

        if (rows.Length != list.Length && !_warned)
        {
            _warned = true;
            Debug.LogWarning($"[스탯 창] `{StatColumns.All[columnIndex].Header}` 열의 줄이 " +
                             $"{rows.Length}개인데 스탯은 {list.Length}개입니다 — " +
                             "`DungeonColony → Setup Stat Panel`을 다시 돌리세요.");
        }
    }

    bool _warned;
}
