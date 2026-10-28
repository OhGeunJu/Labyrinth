using UnityEditor;
using UnityEngine;

// 이동 막힘 계측 on/off. 플레이 중에도 바로 반영된다(정적 플래그라 재컴파일 불필요).
public static class MovementDiagnosticsMenu
{
    [MenuItem("DungeonColony/Debug/Movement Diagnostics")]
    static void ToggleAll()
    {
        MovementDiagnostics.Enabled = !MovementDiagnostics.Enabled;
        Debug.Log($"[이동진단] {(MovementDiagnostics.Enabled ? "켜짐" : "꺼짐")}");
    }

    // 요약만 보고 싶을 때 — 개별 이벤트 줄만 끈다.
    [MenuItem("DungeonColony/Debug/Movement Diagnostics (Events Only)")]
    static void ToggleEvents()
    {
        MovementDiagnostics.LogEvents = !MovementDiagnostics.LogEvents;
        Debug.Log($"[이동진단] 개별 이벤트 로그 {(MovementDiagnostics.LogEvents ? "켜짐" : "꺼짐")} " +
                  "(요약은 그대로)");
    }

    // 지각 기억(놓침 · 수색 · 다시 봄 · 잊음) — 상태가 바뀔 때만 한 줄(2026-10-18).
    [MenuItem("DungeonColony/Debug/Perception Log")]
    static void TogglePerception()
    {
        Perception.Enabled = !Perception.Enabled;
        Debug.Log($"[지각] 로그 {(Perception.Enabled ? "켜짐" : "꺼짐")}");
    }

    // 원거리 거리 유지(붙었다 · 벌어졌다 · 쐈다 · 몰렸다) — 상태가 바뀔 때만 한 줄(2026-10-20).
    [MenuItem("DungeonColony/Debug/Kiting Log")]
    static void ToggleKiting()
    {
        Kiting.Enabled = !Kiting.Enabled;
        Debug.Log($"[거리] 로그 {(Kiting.Enabled ? "켜짐" : "꺼짐")}");
    }
}
