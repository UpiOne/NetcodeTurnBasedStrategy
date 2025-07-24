using System;
using System.Collections.Generic;
using UnityEngine;

public interface IInputManager
{
    event Action<Unit> OnUnitSelected;
    event Action<Unit> OnUnitDeselected;
}

[RequireComponent(typeof(LineRenderer))]
public class InputManager : MonoBehaviour, IInputManager
{
    #region Inspector Fields
    [Header("Layers")]
    [Tooltip("Слой для выбора юнитов")] 
    [SerializeField] private LayerMask unitLayerMask;

    [Header("Visuals")]
    [Tooltip("Префаб индикатора радиуса атаки")] 
    [SerializeField] private GameObject rangeIndicatorPrefab;
    [Tooltip("Префаб индикатора радиуса перемещения")] 
    [SerializeField] private GameObject moveIndicatorPrefab;

    [Header("Path Colors")]
    [Tooltip("Цвет валидного пути")] 
    [SerializeField] private Color validPathColor = Color.white;
    [Tooltip("Цвет невалидного пути")] 
    [SerializeField] private Color invalidPathColor = Color.red;
    #endregion

    #region Private Fields
    private readonly List<GameObject> _attackRangeIndicators = new List<GameObject>();
    private readonly List<GameObject> _moveRangeIndicators = new List<GameObject>();
    private readonly List<Unit> _targetedUnits = new List<Unit>();
    private Camera _mainCamera;
    private Unit _selectedUnit;
    private GridManager _gridManager;
    private LineRenderer _lineRenderer;
    private List<GridCell> _currentPath;
    private float _lastRightClickTime = -1f;
    private const float DOUBLE_CLICK_TIME = 0.2f;
    #endregion

    #region Events
    public event Action<Unit> OnUnitSelected;
    public event Action<Unit> OnUnitDeselected;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        _mainCamera = Camera.main;
        _gridManager = FindObjectOfType<GridManager>();
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.startWidth = 0.1f;
        _lineRenderer.endWidth = 0.1f;
        _lineRenderer.positionCount = 0;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleSelection();
        }
        if (_selectedUnit != null)
        {
            if (TurnManager.Instance != null && _selectedUnit.PlayerId.Value != TurnManager.Instance.CurrentPlayerId.Value)
            {
                DeselectUnit();
                return;
            }
            if (Input.GetMouseButtonDown(1))
            {
                if (HandleAttackTargeting()) return;
                if (Time.time - _lastRightClickTime < DOUBLE_CLICK_TIME)
                {
                    HandleMoveCommand();
                }
                else
                {
                    HandlePathPreview();
                }
                _lastRightClickTime = Time.time;
            }
        }
    }
    #endregion

    #region Selection Logic
    private void HandleSelection()
    {
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Unit clickedUnit = hit.collider.GetComponentInParent<Unit>();
            if (clickedUnit != null && clickedUnit.IsOwner)
            {
                SelectUnit(clickedUnit);
            }
            else
            {
                DeselectUnit();
            }
        }
        else
        {
            DeselectUnit();
        }
    }

    private void SelectUnit(Unit unit)
    {
        if (_selectedUnit == unit) return;
        DeselectUnit();
        _selectedUnit = unit;
        _selectedUnit.Select();
        OnUnitSelected?.Invoke(unit);
        ShowMoveRange(_selectedUnit.transform.position);
        ShowAttackRange(_selectedUnit.transform.position);
    }

    private void DeselectUnit()
    {
        if (_selectedUnit != null)
        {
            _selectedUnit.Deselect();
            OnUnitDeselected?.Invoke(_selectedUnit);
        }
        _selectedUnit = null;
        ClearPath();
        ClearMoveRange();
        ClearAttackRange();
    }
    #endregion

    #region Move/Attack Logic
    private bool HandleAttackTargeting()
    {
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Unit targetUnit = hit.collider.GetComponentInParent<Unit>();
            if (targetUnit != null && targetUnit.PlayerId.Value != _selectedUnit.PlayerId.Value)
            {
                if (TurnManager.Instance != null && !TurnManager.Instance.CanAttack.Value) return true;
                _selectedUnit.TryAttack(targetUnit);
                DeselectUnit();
                return true;
            }
        }
        return false;
    }

    private void HandleMoveCommand()
    {
        if (TurnManager.Instance != null && !TurnManager.Instance.CanMove.Value) return;
        if (_currentPath != null && _selectedUnit != null)
        {
            if (_currentPath.Count - 1 > _selectedUnit.CurrentMoveSpeed)
            {
                ClearPath();
                return;
            }
            _selectedUnit.TryMoveAlongPath(_currentPath);
            DeselectUnit();
        }
    }
    #endregion

    #region Path/Range Visuals
    private void HandlePathPreview()
    {
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Vector2Int targetCoords = _gridManager.WorldToGrid(hit.point);
            GridCell targetCell = _gridManager.GetCell(targetCoords);
            Vector2Int startCoords = _gridManager.WorldToGrid(_selectedUnit.transform.position);
            GridCell startCell = _gridManager.GetCell(startCoords);
            if (targetCell != null && startCell != null && targetCell.isWalkable)
            {
                _currentPath = Pathfinding.FindPath(startCell, targetCell, _gridManager.Grid, _selectedUnit);
                if (_currentPath != null && _currentPath.Count > 0)
                {
                    if (_currentPath.Count - 1 > _selectedUnit.CurrentMoveSpeed)
                        DrawPath(_currentPath, invalidPathColor);
                    else
                        DrawPath(_currentPath);
                    ClearMoveRange();
                    Vector3 endOfPathPosition = _gridManager.GridToWorld(_currentPath[_currentPath.Count - 1].coordinates);
                    ShowAttackRange(endOfPathPosition);
                }
                else
                {
                    ClearPath();
                }
            }
        }
    }

    private void DrawPath(List<GridCell> path, Color? overrideColor = null)
    {
        if (path == null || path.Count == 0)
        {
            if (_lineRenderer != null) _lineRenderer.positionCount = 0;
            return;
        }
        Color finalColor = overrideColor ?? validPathColor;
        _lineRenderer.startColor = finalColor;
        _lineRenderer.endColor = finalColor;
        _lineRenderer.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 pointPosition = _gridManager.GridToWorld(path[i].coordinates);
            pointPosition.y = 0.5f;
            _lineRenderer.SetPosition(i, pointPosition);
        }
    }

    private void ClearPath()
    {
        if (_lineRenderer != null) _lineRenderer.positionCount = 0;
        _currentPath = null;
        if (_selectedUnit != null)
        {
            ShowMoveRange(_selectedUnit.transform.position);
            ShowAttackRange(_selectedUnit.transform.position);
        }
    }
    #endregion

    #region Move/Attack Range Visuals
    private void ShowMoveRange(Vector3 origin)
    {
        ClearMoveRange();
        if (_selectedUnit == null || moveIndicatorPrefab == null) return;
        Vector2Int originCoords = _gridManager.WorldToGrid(origin);
        int moveRange = _selectedUnit.CurrentMoveSpeed;
        for (int x = -moveRange; x <= moveRange; x++)
        {
            for (int y = -moveRange; y <= moveRange; y++)
            {
                Vector2Int cellCoords = originCoords + new Vector2Int(x, y);
                if (_gridManager.IsValidCell(cellCoords) && Vector2Int.Distance(originCoords, cellCoords) <= moveRange)
                {
                    GridCell cell = _gridManager.GetCell(cellCoords);
                    if (cell != null && cell.isWalkable && cell.occupyingUnit == null)
                    {
                        Vector3 indicatorPos = _gridManager.GridToWorld(cellCoords);
                        indicatorPos.y = 0.04f;
                        GameObject indicator = Instantiate(moveIndicatorPrefab, indicatorPos, Quaternion.identity);
                        _moveRangeIndicators.Add(indicator);
                    }
                }
            }
        }
    }

    private void ClearMoveRange()
    {
        foreach (var indicator in _moveRangeIndicators)
        {
            Destroy(indicator);
        }
        _moveRangeIndicators.Clear();
    }

    private void ShowAttackRange(Vector3 origin)
    {
        ClearAttackRange();
        if (_selectedUnit == null || rangeIndicatorPrefab == null) return;
        Vector2Int originCoords = _gridManager.WorldToGrid(origin);
        int range = _selectedUnit.AttackRange;
        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                Vector2Int cellCoords = originCoords + new Vector2Int(x, y);
                if (_gridManager.IsValidCell(cellCoords) && Vector2Int.Distance(originCoords, cellCoords) <= range)
                {
                    Vector3 indicatorPos = _gridManager.GridToWorld(cellCoords);
                    indicatorPos.y = 0.05f;
                    GameObject indicator = Instantiate(rangeIndicatorPrefab, indicatorPos, Quaternion.identity);
                    _attackRangeIndicators.Add(indicator);
                    CheckForTargetableUnit(cellCoords);
                }
            }
        }
    }

    private void ClearAttackRange()
    {
        foreach (var indicator in _attackRangeIndicators)
        {
            Destroy(indicator);
        }
        _attackRangeIndicators.Clear();
        foreach (var unit in _targetedUnits)
        {
            if (unit != null) unit.SetAsTarget(false);
        }
        _targetedUnits.Clear();
    }

    private void CheckForTargetableUnit(Vector2Int cellCoords)
    {
        Unit targetUnit = _gridManager.GetUnitAt(cellCoords);
        if (targetUnit != null && targetUnit.PlayerId.Value != _selectedUnit.PlayerId.Value)
        {
            targetUnit.SetAsTarget(true);
            _targetedUnits.Add(targetUnit);
        }
    }
    #endregion
}