using System.Collections.Generic;
using UnityEngine;

// ③ 3×3 · 20m / ④ 5×5 · 10m — 플레이어 칸 ± (창 크기 − 1) / 2 칸 안의 스포너를 켠다.
public class SpotlightActivation : ActivationPolicy
{
    private readonly int _half;          // 3×3 → 1, 5×5 → 2
    private readonly float _cellSize;
    private readonly Vector2 _origin;    // 청크 원점 (x, z) — 0-1 결정: (−90, −45)

    // 칸 → 그 칸에 중심이 있는 스포너 id들. 스포너는 안 움직이므로 Init에서 1회.
    private readonly Dictionary<Vector2Int, List<int>> _cells = new Dictionary<Vector2Int, List<int>>();

    // 지금 창 안 스포너. 플레이어 칸이 바뀔 때만 다시 채운다.
    private readonly List<int> _window = new List<int>();
    private Vector2Int _lastCell;
    private bool _hasLast;

    public SpotlightActivation(int windowSize, float cellSize, Vector2 origin)
    {
        _half = (windowSize - 1) / 2;
        _cellSize = cellSize;
        _origin = origin;
    }

    public override void Init(ActivationLayout layout)
    {
        _cells.Clear();
        _window.Clear();
        _hasLast = false;

        foreach (SpawnerRecord record in layout._records)
        {
            Vector2Int cell = ToCell(record._center);
            if (!_cells.TryGetValue(cell, out List<int> ids)) _cells[cell] = ids = new List<int>();
            ids.Add(record._spawnerId);
        }
    }

    public override void SelectSpawners(Vector3 playerPos, HashSet<int> current, HashSet<int> result)
    {
        Vector2Int cell = ToCell(playerPos);

        if (!_hasLast || cell != _lastCell)
        {
            _window.Clear();
            for (int dx = -_half; dx <= _half; dx++)
                for (int dz = -_half; dz <= _half; dz++)
                    if (_cells.TryGetValue(new Vector2Int(cell.x + dx, cell.y + dz), out List<int> ids))
                        _window.AddRange(ids);

            _lastCell = cell;
            _hasLast = true;
        }

        // ActivationManager가 매 프레임 result를 비우므로, 칸이 그대로여도 매번 채워야 한다.
        foreach (int id in _window) result.Add(id);
    }

    public override string ToString() =>
        $"Spotlight {_half * 2 + 1}x{_half * 2 + 1} · 칸 {_cellSize}m · 원점 ({_origin.x}, {_origin.y})";

#if UNITY_EDITOR
    // 창 안 칸들 + 창 외곽. 플레이어 칸은 노랑으로 채운다.
    public override void DrawGizmos(Vector3 playerPos)
    {
        Vector2Int cell = ToCell(playerPos);
        float y = playerPos.y;

        for (int dx = -_half; dx <= _half; dx++)
            for (int dz = -_half; dz <= _half; dz++)
                ActivationGizmos.DrawCell(_origin.x + (cell.x + dx) * _cellSize, _origin.y + (cell.y + dz) * _cellSize,
                                          _cellSize, y, dx == 0 && dz == 0);

        ActivationGizmos.DrawWindow(_origin.x + (cell.x - _half) * _cellSize, _origin.y + (cell.y - _half) * _cellSize,
                                    (_half * 2 + 1) * _cellSize, y);
    }
#endif

    // 바닥 평면은 x·z. Vector2Int의 y 자리에 z 칸 번호가 들어간다.
    private Vector2Int ToCell(Vector3 p) =>
        new Vector2Int(Mathf.FloorToInt((p.x - _origin.x) / _cellSize),
                       Mathf.FloorToInt((p.z - _origin.y) / _cellSize));
}
