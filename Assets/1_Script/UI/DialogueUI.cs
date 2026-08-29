using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// **사람과 말을 주고받는 창.** 화면 **아래에 가로로 길게** 눕는다.
//
// ── 왜 아래인가 ────────────────────────────────────────────────────────────
//
// 대화는 **상대를 보면서** 하는 것이다. 가운데에 큰 판을 띄우면 말 거는 상대가 그 판에
// 가려져, "누구와 말하는 중인지"를 창 안의 글자로만 알게 된다. 아래에 눕히면 위쪽 화면이
// 그대로 남아 폰도 지형도 계속 보인다.
//
// **월드맵 도착 메뉴(`WorldMenuUI`)와 다른 창인 이유**도 거기 있다. 그쪽은 *장소*에 들어설 때
// 뜨는 화면이라 왼쪽을 통째로 차지해도 되지만(볼 것이 지도뿐이다), 이건 *사람* 앞에 서서
// 뜨는 것이라 그 사람을 가리면 안 된다.
//
// ── 모양은 씬이, 코드는 데이터만 ───────────────────────────────────────────
//
// 선택지 줄은 **씬의 `OptionTemplate`을 복제**한다(`PawnContextMenuUI`·`WorldMenuUI`와 같은 규약).
// 여백·폰트·색은 전부 씬 오브젝트가 갖고, 여기서는 글자와 콜백만 건다.
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("씬 참조")]
    [SerializeField] GameObject      panel;
    [Tooltip("말하는 사람 이름 — 제목 바")]
    [SerializeField] TextMeshProUGUI speakerText;
    [Tooltip("왼쪽 초상화. 없으면 칸을 접는다")]
    [SerializeField] Image           portrait;
    [Tooltip("지금 하는 말")]
    [SerializeField] TextMeshProUGUI lineText;
    [Tooltip("선택지 줄이 쌓일 부모 (세로 레이아웃)")]
    [SerializeField] Transform       optionContainer;
    [Tooltip("복제해서 쓸 선택지 줄 하나. 씬에 꺼둔 채로 둔다.")]
    [SerializeField] Button          optionTemplate;

    readonly List<GameObject> _rows = new();

    // 못 누르는 줄의 색 — `PawnContextMenuUI`와 **같은 값**이어야 한다.
    // 창마다 다른 회색을 쓰면 "이건 왜 색이 다르지"가 뜻이 있는 것처럼 보인다.
    static readonly Color DisabledColor = new(0.45f, 0.45f, 0.48f, 0.7f);

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
        if (optionTemplate != null) optionTemplate.gameObject.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // 한 마디를 띄운다. **고르면 다음 `Open`을 부르는 것이 곧 대화의 흐름이다** —
    // 트리를 미리 만들어 두는 대신, 선택지의 콜백이 다음 마디를 연다
    // (`WorldMapView`가 마을 메뉴에서 "메뉴 안 메뉴"를 여는 것과 같은 방식).
    public void Open(string speaker, Sprite face, string line, IList<PawnMenuOption> options)
    {
        if (panel == null || optionContainer == null || optionTemplate == null) return;

        if (speakerText != null) speakerText.text = Josa.Fix(speaker);
        if (lineText    != null) lineText.text    = Josa.Fix(line);

        // 초상화가 없으면 **칸을 비우는 대신 숨긴다** — 빈 사각형이 남으면 "그림이 깨졌나"로 읽힌다.
        if (portrait != null)
        {
            portrait.sprite = face;
            portrait.gameObject.SetActive(face != null);
        }

        foreach (var r in _rows) if (r != null) Destroy(r);
        _rows.Clear();

        if (options != null)
            foreach (var opt in options)
            {
                var row = Instantiate(optionTemplate, optionContainer);
                row.gameObject.SetActive(true);

                var txt = row.GetComponentInChildren<TextMeshProUGUI>(true);
                if (txt != null) txt.text = Josa.Fix(opt.Label);

                // 사유가 있으면 마우스를 올렸을 때 뜬다 — 우클릭 창과 **같은 부품**을 쓴다.
                if (!string.IsNullOrEmpty(opt.Note))
                    row.gameObject.AddComponent<MenuRowTooltip>().Set(opt.Label, opt.Note);

                if (opt.OnClick == null)
                {
                    row.interactable = false;
                    if (txt != null) txt.color = DisabledColor;
                    _rows.Add(row.gameObject);
                    continue;
                }

                var captured = opt.OnClick;
                row.onClick.RemoveAllListeners();

                // **닫고 나서 부른다.** 콜백이 다음 마디를 열 수 있으므로 순서가 뒤집히면
                // 새로 연 창을 그 자리에서 닫는다.
                row.onClick.AddListener(() => { Close(); captured?.Invoke(); });
                _rows.Add(row.gameObject);
            }

        WindowRaiser.Open(panel);
    }

    public void Close()
    {
        WindowRaiser.Close(panel);

        // 줄이 사라지면 `OnPointerExit`가 안 온다 — 커서를 올린 채 고르면 설명만 남는다
        // (`ItemInfoUI`가 겪은 그것).
        HoverTooltipUI.Hide();
    }
}
