using UnityEngine;

// MovementDiagnostics 요약 주기 호출 전용. 자동 생성되므로 씬에 직접 붙일 필요 없다.
// (MonoBehaviour는 파일명과 클래스명을 맞춰야 안전해서 별도 파일로 둔다.)
public class MovementDiagnosticsTicker : MonoBehaviour
{
    void Update() => MovementDiagnostics.Tick();
}
