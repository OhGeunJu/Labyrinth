using UnityEngine;

// **이목구비 판 하나 — 제 칸을 기억하고 셰이더에 알린다**
// (2026-09-18, 채널 분할 아틀라스 형식).
//
// 눈·입은 얼굴에서 **따로 뜯어낸 작은 판**이고, 모든 판이 **아틀라스 한 장**을 나눠 쓴다.
// 판마다 어느 칸을 보일지를 여기 적고 `MaterialPropertyBlock`으로 셰이더에 넘긴다 — 재질을
// 판마다 복제하면 폰 수 × 판 수만큼 재질이 불어난다.
//
// **칸은 이름으로 든다**(`ChibiFaceAtlas` 참고 — 번호로 들면 팩이 늘 때 표정이 밀린다).
// **에디터에서도 돈다**(`ExecuteAlways`) — 안 돌면 프리팹을 열었을 때 모든 판이 왼쪽 아래 칸
// 하나로 보여 *무엇이 어디 있는지* 저작 중에 안 보인다.
//
// 표정을 바꾸는 문은 `PawnLook.SetFace`다 — 판을 직접 부르지 말 것(턱이 다섯이라 같은 판이 다섯 벌이다).
[ExecuteAlways]
[DisallowMultipleComponent]
public class ChibiFacePart : MonoBehaviour
{
    public ChibiFaceAtlas Atlas;

    [Tooltip("보일 그림의 이름(팩 파일 이름). 비우면 빈 칸 — 이 이목구비가 없다.")]
    public string Cell = "";

    // **구울 때 정한 기본** — 표정이 끝나면 여기로 돌아온다. `Cell`과 따로 드는 이유: 인스펙터로
    // 고른 것이 `Cell`에 저장되면 *원래 무엇이었나*가 사라져 "되돌리기"가 갈 곳이 없다.
    public string DefaultCell = "";

    static readonly int IdOffset = Shader.PropertyToID("_CellOffset");
    static MaterialPropertyBlock _mpb;   // 처음 쓸 때 만든다 — 정적 초기화자에서 만들면 클래스가 통째로 죽는다

    void OnEnable()   => Apply();
    void OnValidate() => Apply();

    // `null`이면 기본으로 돌아간다. **"-"는 빈 칸**(그 이목구비를 지운다 — 빈 문자열은 *안 골랐다*와 구별이 안 된다).
    public void Set(string cell)
    {
        Cell = cell ?? DefaultCell;
        Apply();
    }

    public void Apply()
    {
        var r = GetComponent<Renderer>();
        if (r == null || Atlas == null) return;

        _mpb ??= new MaterialPropertyBlock();
        r.GetPropertyBlock(_mpb);
        _mpb.SetVector(IdOffset, Atlas.OffsetOf(Atlas.IndexOf(Cell)));
        r.SetPropertyBlock(_mpb);
    }
}
