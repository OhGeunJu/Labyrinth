using System.Collections.Generic;
using UnityEngine;

// **"나 대신 이 몸들 중 하나로 태어나라"** — 이벤트·바이옴·모집이 가리키는 프리팹(`Wanderer_Human`)에 붙는다.
// 가리키는 쪽은 그대로 이 프리팹 하나만 알고, 무엇이 서는지는 `PawnSpawn`이 여기서 고른다(가리키는 자리마다
// 고르게 하면 하나만 빠뜨려도 그쪽에서 한 체형만 나온다).
[DisallowMultipleComponent]
public class BodyVariants : MonoBehaviour
{
    [Tooltip("태어날 수 있는 몸들 — `Pawn/3. Create Chibi Pawns`가 채운다.")]
    public List<PawnBody> Variants = new();

    // 성별이 맞는 것 중 하나(씨앗으로 — 굴리지 않는다). 맞는 것이 없으면 아무거나.
    public PawnBody Pick(int seed, Gender gender)
    {
        var fit = new List<PawnBody>();
        foreach (var v in Variants) if (v != null && v.Gender == gender) fit.Add(v);
        if (fit.Count == 0) foreach (var v in Variants) if (v != null) fit.Add(v);
        if (fit.Count == 0) return null;
        // 성별을 뽑은 열과 **다른 열**을 쓴다 — 같은 열이면 "여자면 늘 첫째 체형"이 생긴다.
        var rng = new System.Random(unchecked(seed * 131 + 71));
        return fit[rng.Next(fit.Count)];
    }
}
