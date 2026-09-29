using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 배치 창의 한 줄 — 폰 이름 + 1 · 2 · 3순위 드롭다운(`WorkPostsUI`).
//
// 드롭다운의 0번은 늘 **비어 있음**이고 1번부터가 `WorkPosts.Candidates`다. 지금 칸에 든 건물이 목록에 없으면
// (영역 밖으로 밀렸거나 부서졌다) **맨 끝에 그 이름을 붙여** 보여 준다 — 안 그러면 창을 여는 것만으로 칸이 *비어 있음*으로
// 보이고, 그대로 다른 칸을 만지면 그 배치가 조용히 지워진 것처럼 읽힌다.
public class WorkPostRowUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TMP_Dropdown[]  slots = new TMP_Dropdown[WorkPosts.Count];

    UnitBrain _brain;

    public void Bind(UnitBrain brain, List<(string label, WorkPost post)> options)
    {
        _brain = brain;
        if (nameText != null) nameText.text = brain.DisplayTitle;

        var have = WorkPosts.Of(brain);
        for (int i = 0; i < slots.Length && i < WorkPosts.Count; i++)
        {
            var dd = slots[i];
            if (dd == null) continue;

            var posts = new List<WorkPost> { default };
            var labels = new List<string> { "비어 있음" };
            foreach (var (label, post) in options) { labels.Add(label); posts.Add(post); }

            var cur = have != null ? have[i] : default;
            int pick = 0;
            if (cur.IsSet)
            {
                pick = posts.FindIndex(p => p.Kind == cur.Kind && p.Target == cur.Target);
                if (pick < 0) { labels.Add(WorkPosts.Label(cur)); posts.Add(cur); pick = posts.Count - 1; }
            }

            int slot = i;
            dd.onValueChanged.RemoveAllListeners();
            dd.ClearOptions();
            dd.AddOptions(labels);
            dd.SetValueWithoutNotify(pick);
            dd.RefreshShownValue();
            dd.onValueChanged.AddListener(v =>
            {
                if (_brain == null) return;
                var target = v > 0 && v < posts.Count ? posts[v] : default;
                if (target.IsSet) WorkPosts.For(_brain).Set(slot, target);
                else WorkPosts.Of(_brain)?.Clear(slot);
            });
        }
    }
}
