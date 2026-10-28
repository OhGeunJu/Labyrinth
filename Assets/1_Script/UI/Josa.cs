using System.Text;

// **조사를 앞말에 맞춰 고른다** — `사파이어이(가)` → `사파이어가`.
//
// 이 저장소는 문장을 `을(를)`·`이(가)`처럼 **둘 다 적어** 두고 있었다(코드 서른두 파일에
// 일흔두 곳, 자원 노드 설명 같은 에셋에도). 읽기에는 문제가 없지만 화면에 그대로 나가면
// *"채굴하면 사파이어이(가) 나오는 광맥"* 이 된다.
//
// ## 문장마다 고치지 않고 나갈 때 고른다
//
// 일흔두 곳을 `Josa.Eul(name)` 식으로 바꾸는 길도 있었다. 안 골랐다 —
//
// - **이름을 두 번 적게 된다**(`$"{who}{Josa.Eul(who)}"`). 둘이 어긋나면 조사만 딴 낱말을
//   보고 고르는데, 그건 화면에서 티가 잘 안 난다.
// - 문장은 코드에만 있는 것이 아니다. **에셋에 저작된 글**(건물 설명 등)은 코드를 못 부른다.
//
// **`X(Y)` 자체가 이미 표식이다.** 그것을 화면에 내보내는 자리에서 한 번 훑어 고르면, 소스는
// 지금 그대로 읽히고 에셋 글도 공짜로 따라온다.
//
// ## 모르면 고르지 않는다
//
// 앞말이 한글도 숫자도 아니면(라틴 문자·기호) **표식을 그대로 둔다.** 짐작해서 고르면 조용히
// 틀린 한국어가 나가지만, 남겨 두면 눈에 띄어 고칠 수 있다.
public static class Josa
{
    // 짝마다 **어느 쪽이 받침용인가**를 적는다. 위치로 못 정한다 — 이 저장소만 해도
    // `을(를)`·`이(가)`는 받침용이 앞이고 `와(과)`는 뒤다.
    //
    // `withJong`이 받침 있을 때, `noJong`이 없을 때.
    struct Pair
    {
        public string WithJong, NoJong;
        public bool   RieulIsNo;   // ㄹ 받침은 받침 없는 쪽을 쓰는가(`으로`/`로`)
    }

    static readonly Pair[] Pairs =
    {
        new Pair { WithJong = "으로", NoJong = "로", RieulIsNo = true },
        new Pair { WithJong = "이라", NoJong = "라" },
        new Pair { WithJong = "을",   NoJong = "를" },
        new Pair { WithJong = "이",   NoJong = "가" },
        new Pair { WithJong = "은",   NoJong = "는" },
        new Pair { WithJong = "과",   NoJong = "와" },
        new Pair { WithJong = "아",   NoJong = "야" },
    };

    // 문장 안의 조사 표식을 전부 고른다. 표식이 없으면 원문을 그대로 돌려준다.
    public static string Fix(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('(') < 0) return text;

        StringBuilder sb = null;
        int i = 0, copied = 0;

        while (i < text.Length)
        {
            int len = MatchAt(text, i, out string picked);
            if (len <= 0) { i++; continue; }

            sb ??= new StringBuilder(text.Length);
            sb.Append(text, copied, i - copied);
            sb.Append(picked);
            i     += len;
            copied = i;
        }

        if (sb == null) return text;
        sb.Append(text, copied, text.Length - copied);
        return sb.ToString();
    }

    // `i`에서 시작하는 표식의 길이(없으면 0)와 고른 조사.
    static int MatchAt(string s, int i, out string picked)
    {
        picked = null;

        foreach (var p in Pairs)
        {
            int len = Marker(s, i, p.WithJong, p.NoJong);
            if (len == 0) len = Marker(s, i, p.NoJong, p.WithJong);
            if (len == 0) continue;

            if (!TryJong(s, i, out int jong)) return 0;   // 모르는 앞말 — 표식을 그대로 둔다

            bool withJong = jong != 0 && !(p.RieulIsNo && jong == 8);
            picked = withJong ? p.WithJong : p.NoJong;
            return len;
        }
        return 0;
    }

    // `s[i..]`가 `head(tail)` 꼴이면 그 길이, 아니면 0.
    static int Marker(string s, int i, string head, string tail)
    {
        int len = head.Length + 1 + tail.Length + 1;
        if (i + len > s.Length) return 0;
        if (string.CompareOrdinal(s, i, head, 0, head.Length) != 0) return 0;
        if (s[i + head.Length] != '(') return 0;
        if (string.CompareOrdinal(s, i + head.Length + 1, tail, 0, tail.Length) != 0) return 0;
        if (s[i + len - 1] != ')') return 0;
        return len;
    }

    // 표식 **바로 앞** 글자의 받침. 리치 텍스트 태그(`</b>`)는 건너뛴다 — 이름을 굵게 쓰면
    // 표식 앞 글자가 `>`가 되어 늘 "모르는 앞말"이 되어 버린다.
    static bool TryJong(string s, int markerStart, out int jong)
    {
        jong = 0;
        int k = markerStart - 1;

        while (k >= 0 && s[k] == '>')
        {
            int open = s.LastIndexOf('<', k);
            if (open < 0) return false;
            k = open - 1;
        }
        if (k < 0) return false;

        char c = s[k];

        if (c >= 0xAC00 && c <= 0xD7A3) { jong = (c - 0xAC00) % 28; return true; }

        // 숫자는 읽는 소리로 정한다 — 영·일·삼·육·칠·팔은 받침, 이·사·오·구는 없다.
        // `일`·`칠`·`팔`은 ㄹ 받침이라 `으로`가 아니라 `로`를 쓴다(그래서 8로 표시한다).
        if (c >= '0' && c <= '9')
        {
            switch (c)
            {
                case '0': case '3': case '6': jong = 1; return true;   // 영·삼·육
                case '1': case '7': case '8': jong = 8; return true;   // 일·칠·팔 (ㄹ)
                default:                      jong = 0; return true;   // 이·사·오·구
            }
        }
        return false;
    }
}
