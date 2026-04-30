using System.Collections.Generic;
using UnityEngine;

public class DungeonCoreManager : MonoBehaviour
{
    public static DungeonCoreManager Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogError("[DungeonCoreManager] 인스턴스가 없습니다.");
            return _instance;
        }
    }
    static DungeonCoreManager _instance;

    readonly List<DungeonCore> _cores = new();
    readonly Dictionary<DungeonCore, HashSet<Vector2Int>> _coreCells = new();
    readonly HashSet<Vector2Int> _occupiedCells = new();

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
    }

    public void RegisterCore(DungeonCore core)
    {
        if (!_cores.Contains(core))
            _cores.Add(core);
    }

    public void UnregisterCore(DungeonCore core)
    {
        _cores.Remove(core);
        UnregisterCells(core);
    }

    // 셀 등록/해제 ────────────────────────────────────────────────────────────
    public void RegisterCells(DungeonCore core, HashSet<Vector2Int> cells)
    {
        if (_coreCells.TryGetValue(core, out var prev))
            foreach (var c in prev) _occupiedCells.Remove(c);

        _coreCells[core] = cells;
        foreach (var c in cells) _occupiedCells.Add(c);
    }

    public void UnregisterCells(DungeonCore core)
    {
        if (!_coreCells.TryGetValue(core, out var cells)) return;
        foreach (var c in cells) _occupiedCells.Remove(c);
        _coreCells.Remove(core);
    }

    public bool IsCellOccupied(Vector2Int cell) => _occupiedCells.Contains(cell);

    public IReadOnlyList<DungeonCore> Cores => _cores;
}
