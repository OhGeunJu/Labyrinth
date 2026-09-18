using UnityEngine;

// **한 물건이 여러 모습으로 난다**(2026-09-18, 사용자: *"버섯이 한 종류만 보이지말고 …
// 하나의 버섯에서 랜덤으로 여러종류의 모델링이 나오게"*).
//
// ## 자리가 모습을 정한다 — 굴리지 않는다
//
// `Random`으로 고르면 **청크가 다시 올라올 때마다 모습이 바뀐다**(뿌리는 것은 좌표 해시라
// 자리는 그대로인데 얼굴만 달라진다 — 걸어갔다 돌아올 때마다 버섯이 변신한다). 그래서
// **자리에서 해시를 뽑는다**: 같은 자리는 언제나 같은 모습이고, 저장할 것도 없다.
//
// 뿌리기·나무·바위가 전부 같은 규칙으로 서 있고(`ScatterRule`), 이건 그 규칙을 **한 물건
// 안쪽으로** 한 겹 더 들여온 것이다.
//
// ## 켜고 끌 뿐 만들지 않는다
//
// 변형은 **에디터가 미리 굽는다**(`PropDresser`). 런타임이 `Instantiate`하면 뿌리기가
// 세우는 것마다 오브젝트가 배로 늘고, 무엇보다 **프리팹을 봐도 무엇이 나올지 알 수 없다.**
[DisallowMultipleComponent]
public class PropVariant : MonoBehaviour
{
    public const string Prefix = "[Var ";   // `[Var 0]` … `[Var n]`

    void Awake() => Apply();

    // **`Awake`에서 고른다** — `Start`면 한 프레임 동안 전부 켜진 채로 보인다(프리팹은
    // 0번만 켜 두므로 실제로는 안 그렇지만, 순서에 기대지 않는 편이 낫다).
    public void Apply()
    {
        int count = 0;
        for (int i = 0; ; i++)
        {
            if (transform.Find(Prefix + i + "]") == null) break;
            count++;
        }
        if (count <= 1) return;   // 변형이 없거나 하나뿐 — 손댈 것이 없다

        var p = transform.position;
        int h = Hash(Mathf.RoundToInt(p.x * 4f), Mathf.RoundToInt(p.z * 4f));
        int pick = h % count;

        for (int i = 0; i < count; i++)
        {
            var v = transform.Find(Prefix + i + "]");
            if (v != null) v.gameObject.SetActive(i == pick);
        }

        Vary(transform.Find(Prefix + pick + "]"), h);
    }

    // **기울이고 크기를 흔든다**(사용자: *"자연스폰될 때 방향도 좀 돌려줘야해 너무 똑같아
    // 보이지 않게"*).
    //
    // 뿌리기는 **Y축을 이미 360도 돌린다**(`WorldContentStreamer`) — 그런데도 똑같아 보인
    // 이유는 **모델이 한 종류**여서였고, 그건 변형이 풀었다. 남은 것은 **자란 모양**이다:
    // 자연물은 똑바로 서 있지 않다.
    //
    // **같은 해시를 쓴다** — 변형을 고른 그 수에서 파생시키면 *모습·기울기·크기*가 한 자리에
    // 묶여, 같은 자리는 언제나 같은 포기다.
    //
    // **크게 흔들지 않는다** — 기울기 ±9°·크기 ±14%. 더 주면 **땅에 박히거나 떠 보인다**
    // (피벗이 발밑이라 기울이면 한쪽 끝이 지면을 파고든다).
    void Vary(Transform v, int h)
    {
        if (v == null) return;

        float tiltX = Frac(h, 7)  * 18f - 9f;
        float tiltZ = Frac(h, 13) * 18f - 9f;
        float scale = 0.86f + Frac(h, 19) * 0.28f;

        v.localRotation = Quaternion.Euler(tiltX, v.localEulerAngles.y, tiltZ);

        // **곱한다 — 덮어쓰지 않는다.** 에디터가 이미 발자국에 맞춰 줄여 놓았으므로
        // (`PropDresser.Fit`), `Vector3.one`을 넣으면 **그 크기가 통째로 날아가** 버섯이
        // 원래 소품 크기로 되돌아온다.
        if (_varied) return;
        _varied = true;
        v.localScale *= scale;
    }

    // **한 번만 흔든다** — `Apply`가 다시 불리면 크기가 계속 곱해진다.
    bool _varied;

    // 해시에서 0~1을 뽑는다 — **자리마다 다른 축을 쓰려고 `salt`로 가른다**(같은 수를 그대로
    // 쓰면 기울기 둘이 늘 같은 방향으로 붙는다).
    static float Frac(int h, int salt)
    {
        unchecked
        {
            int x = h * salt;
            x ^= x >> 15;
            return (x & 0xffff) / 65535f;
        }
    }

    // **좌표 해시** — 뿌리기가 쓰는 것과 같은 결(결정적·자리에 매인다). 0.25m 눈금으로
    // 반올림하는 이유는 부양 높이나 미세한 오차로 **같은 물건이 다른 답을 받지 않게** 하려는 것이다.
    static int Hash(int x, int z)
    {
        unchecked
        {
            int h = x * 73856093 ^ z * 19349663;
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return h & 0x7fffffff;
        }
    }
}
