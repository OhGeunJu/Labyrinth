using DG.Tweening;
using UnityEngine;

// **창이 어떻게 뜨고 사라지나 — 답은 여기 하나다**(2026-08-29, DOTween 도입).
//
// 부르는 곳마다 트윈을 적으면 반드시 어긋나는데, 이 저장소에서 그 어긋남은 취향 문제가
// 아니라 **고장**이다:
//
//  · **`SetUpdate(true)`를 빠뜨리면 그 창만 멈춘다.** `GameTimeManager`가 `Time.timeScale`을
//    만지므로(`CameraController.Dt`가 `unscaledDeltaTime`을 쓰는 바로 그 이유), 스케일된
//    시간으로 돌면 **일시정지 중에 창이 영영 안 열리고 3배속에서는 세 배로 빨라진다.**
//    멈춰 세우는 이유의 절반이 창을 들여다보려는 것이라, 그건 그냥 안 열리는 것과 같다.
//  · **`SetLink`를 빠뜨리면 파괴된 대상을 계속 만진다** → `MissingReferenceException`.
//    운반 목적지와 static 이벤트 람다에서 이미 겪은 그 사고다 — UI는 창을 닫는 것만으로도
//    대상이 사라진다.
//
// 그래서 **트윈을 만드는 자리를 여기로 모은다.** 부르는 쪽은 꼬리표를 몰라도 되고, 새 창을
// 만들 때 붙이는 것을 잊을 자리가 없다(`WindowRaiser`가 여는 길을 하나로 둔 것과 같은 규칙).
public static class UiTween
{
    // **눈금은 짧다.** 창은 정보를 보려고 여는 것이라, 뜨는 것을 구경시키면 조작이 느려진다.
    // 닫는 쪽이 더 짧은 이유는 **이미 볼 일이 끝났기 때문**이다.
    public const float OpenTime  = 0.14f;
    public const float CloseTime = 0.10f;

    // 열릴 때 이만큼에서 커진다. 1에 가까울수록 얌전하다 — 0.9 아래로 내리면 **창이 튀어나와**
    // 어디서 나타났는지를 눈으로 좇게 되고, 그건 정보를 읽는 것을 늦춘다.
    const float PopFrom = 0.94f;

    // ── 창 ─────────────────────────────────────────────────────────────────

    // 켜면서 부풀린다. **`WindowRaiser.Open`이 유일한 호출자**여야 한다.
    public static void PopIn(GameObject panel)
    {
        if (panel == null) return;

        // **이미 떠 있으면 다시 안 띄운다.** `WindowRaiser.Open`은 열린 창을 앞으로 올릴 때도
        // 불리고(상자를 다시 클릭), 툴팁은 커서가 얹혀 있는 동안 계속 불린다 — 그때마다 다시
        // 부풀면 **창이 맥박친다.**
        bool wasOpen = panel.activeSelf;
        panel.SetActive(true);

        var cg = Group(panel);

        // **이미 떠 있어도 사라지는 중일 수 있다.** `FadeOut`은 0.1초 동안 창을 켜 둔 채
        // 흐리므로, 그 사이에 다시 열면 `activeSelf`는 참인데 알파는 0으로 가는 중이다 —
        // 안 되돌리면 **다시 열었는데 그대로 사라진다**(닫자마자 다시 여는 조작에서, 그리고
        // 툴팁이 줄 사이를 건널 때 실제로 그렇다).
        DOTween.Kill(cg);
        cg.blocksRaycasts = true;
        cg.interactable   = true;   // 다른 코드가 꺼 두었을 수 있다
        if (wasOpen) { cg.alpha = 1f; return; }

        cg.alpha = 0f;
        Fade(cg, 1f, OpenTime, Ease.OutQuad, panel);

        var t = panel.transform;

        // **완료시키며 죽인다**(`complete: true`). 크기를 건드리는 것은 이 메서드뿐이고 언제나
        // 원래 크기로 끝나므로, 다음번에 읽는 `localScale`이 곧 저작된 크기다 — 그래서 원래
        // 크기를 어디에도 저장하지 않아도 된다. 그냥 죽이면 **중간 크기에서 멈춘 값이 다음
        // 기준이 되어 창이 열릴 때마다 조금씩 쪼그라든다.**
        DOTween.Kill(t, true);
        Vector3 full = t.localScale;
        t.localScale = full * PopFrom;
        t.DOScale(full, OpenTime).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(panel);
    }

    // 흐려진 뒤 꺼진다.
    //
    // ── 중간에 죽어도 되돌린다 (2026-08-29) ────────────────────────────────
    //
    // 되돌리는 일을 `OnComplete`에만 두었더니, 페이드가 **끝나기 전에 죽는 경우**에 그 코드가
    // 통째로 안 돌았다 — 창은 **반쯤 투명한 채로 살아 있고 `blocksRaycasts`가 꺼져 있어**
    // 보이는데 아무것도 안 눌린다. 화면에서는 *"UI가 먹통"*으로만 보인다.
    //
    // **`OnKill`은 끝나든 죽든 반드시 돈다.** 그래서 되돌리기는 거기 두고, `OnComplete`는
    // *"정말 끝났나"*만 기록한다 — 중간에 죽은 것을 끄면 **닫히던 창이 사라져** 다시 열 수 없다.
    public static void FadeOut(GameObject panel)
    {
        if (panel == null || !panel.activeSelf) return;

        var cg = Group(panel);
        DOTween.Kill(cg);

        // **사라지는 동안 클릭을 안 먹는다** — 안 그러면 닫기를 누른 뒤 0.1초 동안 그 자리의
        // 버튼이 계속 눌린다(`BuildingRoof`가 지붕 콜라이더를 떼는 것과 같은 뜻).
        cg.blocksRaycasts = false;

        bool finished = false;

        Fade(cg, 0f, CloseTime, Ease.InQuad, panel)
          .OnComplete(() => finished = true)
          .OnKill(() =>
          {
              if (finished) panel.SetActive(false);

              // **되돌려 놓는다.** `SetActive(true)`를 직접 부르는 곳이 아직 여럿이라
              // (모드 전환·에디터 도구), 알파를 0으로 남겨 두면 그 창이 **켜졌는데 안 보인다.**
              // 그리고 `blocksRaycasts`를 안 되돌리면 **보이는데 안 눌린다.**
              if (cg == null) return;
              cg.alpha          = 1f;
              cg.blocksRaycasts = true;
          });
    }

    // ── 강조 ───────────────────────────────────────────────────────────────

    // 톡 튀어나온다. 손에 집은 아이콘처럼 **방금 생긴 것**에 쓴다.
    public static void Pop(Transform t)
    {
        if (t == null) return;

        DOTween.Kill(t, true);
        Vector3 full = t.localScale;
        t.localScale = full * 0.8f;
        t.DOScale(full, 0.18f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(t.gameObject);
    }

    // ── 그릇 ───────────────────────────────────────────────────────────────

    // **알파를 다루려면 `CanvasGroup`이 있어야 한다.** 없으면 붙인다 — 창 열다섯 개에
    // 손으로 붙이게 하면 **붙이는 것을 잊은 창만 안 흐려지고**, 새 창을 만들 때 또 잊는다.
    // 기본값이 중립이라(`interactable`·`blocksRaycasts` 참) 붙이는 것만으로는 아무것도 안 바뀐다.
    static CanvasGroup Group(GameObject panel)
    {
        var cg = panel.GetComponent<CanvasGroup>();
        return cg != null ? cg : panel.AddComponent<CanvasGroup>();
    }

    // **`cg.DOFade`를 안 쓴다 — 그건 DOTween의 UI 모듈에 있다.**
    //
    // 모듈은 유틸리티 패널의 **체크박스**로 켜고 끄는 것이라, 그걸 쓰면 **게임 UI 전체가 그
    // 체크박스에 걸린다** — 누군가 `UI`를 끄는 순간 창이 하나도 안 열리고, 원인은 이 파일이
    // 아니라 패널에 있어 찾기도 어렵다. `DOTween.To`는 어느 모듈도 안 탄다.
    //
    // **`SetTarget`을 반드시 붙인다** — `DOTween.To`는 대상이 없어서, 안 붙이면
    // `DOTween.Kill(cg)`가 **이 트윈을 못 찾는다**(창을 다시 열어도 옛 페이드가 계속 돌아
    // 알파가 둘 사이에서 싸운다).
    static Tweener Fade(CanvasGroup cg, float to, float time, Ease ease, GameObject link) =>
        DOTween.To(() => cg.alpha, a => cg.alpha = a, to, time)
               .SetTarget(cg)
               .SetEase(ease)
               .SetUpdate(true)
               .SetLink(link);
}
