using UnityEngine;

// **이목구비 아틀라스 — 그림 한 장과 "이름 → 칸" 표**
// (2026-09-18, 사용자가 가져온 글의 형식 — 채널을 쪼갠 마스크 · 칸 나눈 아틀라스 · 알파 자르기).
//
// 그림은 `ChibiPawnCreator.BuildFaceAtlas`가 팩(T.J. Loudon 눈·입)에서 굽는다 — 색 그림을
// **마스크 넷**(R 잉크 · G 흰자 · B 강조 · A 자르기)으로 바꿔 `Cells × Cells` 칸에 하나씩 넣는다.
// 규격은 `Resources/Shaders/ChibiFace.shader` 머리말에 있다.
//
// **이름으로 부른다 — 칸 번호로 부르지 않는다.** 팩에 그림이 하나 늘면 칸이 밀리는데, 번호로
// 저작해 두었으면 **모든 폰의 표정이 한 칸씩 조용히 어긋난다.** 이름은 파일 이름 그대로다
// (`eye_mad` · `mouth_happy1`).
//
// **마지막 칸은 늘 비어 있다** — *"그 이목구비가 없다"*를 칸 하나로 적는다(입 없는 얼굴이 기본이다).
[CreateAssetMenu(menuName = "DungeonColony/Chibi Face Atlas")]
public class ChibiFaceAtlas : ScriptableObject
{
    public Texture2D Texture;
    public int       Cells = 6;
    public string[]  Names = new string[0];

    public int EmptyIndex => Cells * Cells - 1;

    // 이름 → 칸 번호. 비었거나 없으면 **빈 칸**이다(없는 그림을 잘못된 그림으로 보여 주지 않는다).
    public int IndexOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return EmptyIndex;
        for (int i = 0; i < Names.Length; i++) if (Names[i] == name) return i;
        return EmptyIndex;
    }

    // 칸 번호 → 셰이더가 받는 옮김(칸 단위). 판의 UV는 **왼쪽 아래 칸**을 가리키므로 거기서의 거리다.
    // 칸은 **왼쪽 위부터** 가로로 채운다(그림을 열었을 때 읽는 순서).
    public Vector2 OffsetOf(int index)
    {
        int col = index % Cells, rowFromTop = index / Cells;
        return new Vector2(col, Cells - 1 - rowFromTop);
    }
}
