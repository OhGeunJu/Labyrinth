using UnityEditor;
using UnityEngine;

// 이동 막힘 계측 on/off. 플레이 중에도 바로 반영된다(정적 플래그라 재컴파일 불필요).
public static class MovementDiagnosticsMenu
{
    [MenuItem("DungeonColony/Toggle Movement Diagnostics")]
    static void ToggleAll()
    {
        MovementDiagnostics.Enabled = !MovementDiagnostics.Enabled;
        Debug.Log($"[이동진단] {(MovementDiagnostics.Enabled ? "켜짐" : "꺼짐")}");
    }

    // 요약만 보고 싶을 때 — 개별 이벤트 줄만 끈다.
    [MenuItem("DungeonColony/Toggle Movement Diagnostics (Events Only)")]
    static void ToggleEvents()
    {
        MovementDiagnostics.LogEvents = !MovementDiagnostics.LogEvents;
        Debug.Log($"[이동진단] 개별 이벤트 로그 {(MovementDiagnostics.LogEvents ? "켜짐" : "꺼짐")} " +
                  "(요약은 그대로)");
    }
}
