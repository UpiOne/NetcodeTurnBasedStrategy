using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{

    [SerializeField] private MapConfig previewMapConfig; 
    
    private int gridWidth;
    private int gridHeight;
    [SerializeField] private float cellSize = 1f;
    
    private Dictionary<Vector2Int, GridCell> _grid;
    public Dictionary<Vector2Int, GridCell> Grid => _grid; 
    
    private Dictionary<Unit, Vector2Int> _unitPositions = new Dictionary<Unit, Vector2Int>();

    public void CreateGrid(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;
        _grid = new Dictionary<Vector2Int, GridCell>();
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector2Int coords = new Vector2Int(x, y);
                _grid[coords] = new GridCell(x, y);
            }
        }
    }
    
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / cellSize);
        int z = Mathf.FloorToInt(worldPosition.z / cellSize);
        return new Vector2Int(x, z);
    }
    
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        float x = gridPosition.x * cellSize + cellSize / 2f;
        float z = gridPosition.y * cellSize + cellSize / 2f; // Используем Y из Vector2Int для оси Z
        return new Vector3(x, 0, z);
    }
    
    public bool IsValidCell(Vector2Int coords)
    {
        return _grid.ContainsKey(coords);
    }
    
    public GridCell GetCell(Vector2Int coords)
    {
        if (IsValidCell(coords))
        {
            return _grid[coords];
        }
        return null;
    }

    #region Debug

    private void OnDrawGizmos()
    {
        if (_grid != null)
        {
            foreach (var cell in _grid.Values)
            {
                Gizmos.color = cell.isWalkable ? Color.green : Color.red;
                var center = GridToWorld(cell.coordinates);
                Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.1f, cellSize));
            }
        }

        else if (previewMapConfig != null)
        {
            Gizmos.color = Color.gray;

            for (int x = 0; x < previewMapConfig.gridWidth; x++)
            {
                for (int y = 0; y < previewMapConfig.gridHeight; y++)
                {
                    var center = new Vector3(x * cellSize + cellSize / 2f, 0, y * cellSize + cellSize / 2f);
                    Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.1f, cellSize));
                }
            }
        }

    }
    #endregion
    
    public void RegisterUnit(Unit unit, Vector2Int initialPosition)
    {
        if (IsValidCell(initialPosition))
        {
            GetCell(initialPosition).occupyingUnit = unit;
            _unitPositions[unit] = initialPosition;
        }
    }

    public void ClearCellOccupation(Vector2Int coords)
    {
        if (IsValidCell(coords))
        {
            GetCell(coords).occupyingUnit = null;
        }
    }

    public void SetCellOccupation(Unit unit, Vector2Int coords)
    {
        if (IsValidCell(coords))
        {
            GetCell(coords).occupyingUnit = unit;
            _unitPositions[unit] = coords;
        }
    }

    public Unit GetUnitAt(Vector2Int coords)
    {
        return GetCell(coords)?.occupyingUnit;
    }

    public void UnregisterUnit(Unit unit)
    {
        if (_unitPositions.TryGetValue(unit, out Vector2Int coords))
        {
            ClearCellOccupation(coords); 
            _unitPositions.Remove(unit);
        }
    }
    
    public List<GridCell> GetCellsOnLine(Vector2Int from, Vector2Int to)
    {
        List<GridCell> cellsOnLine = new List<GridCell>();
        int x0 = from.x, y0 = from.y;
        int x1 = to.x, y1 = to.y;

        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            Vector2Int currentCoords = new Vector2Int(x0, y0);
            if (IsValidCell(currentCoords))
            {
                cellsOnLine.Add(GetCell(currentCoords));
            }

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        return cellsOnLine;
    }
}