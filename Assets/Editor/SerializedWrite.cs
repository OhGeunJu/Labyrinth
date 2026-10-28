using UnityEditor;
using UnityEngine;

// private `[SerializeField]`는 밖에서 못 만지므로 도구가 직렬화 창구로 넣는다.
// (한때 `FireArt`에 얹혀 있었다 — 불꽃·등을 짓던 도구들이 2026-09-09에 다 쓰고 지워지면서
//  남은 것이 이 한 줄이라 제 이름을 준다.)
public static class SerializedWrite
{
    public static void Set(Component c, System.Action<SerializedObject> write)
    {
        var so = new SerializedObject(c);
        write(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
