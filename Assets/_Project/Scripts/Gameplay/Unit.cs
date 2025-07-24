using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public interface IUnit
{
    event Action<IUnit> OnUnitDied;
    event Action<IUnit> OnUnitSelected;
    event Action<IUnit> OnUnitDeselected;
    void Select();
    void Deselect();
    void SetAsTarget(bool isTarget);
    void TryMoveAlongPath(System.Collections.Generic.List<GridCell> path);
    void TryAttack(Unit target);
    void GrantInfiniteMoveSpeed();
    int CurrentMoveSpeed { get; }
    int AttackRange { get; }
    int PlayerIdValue { get; }
}

public class Unit : NetworkBehaviour, IUnit
{
    #region Inspector Fields
    [Header("Data")]
    [Tooltip("ScriptableObject с данными юнита")] [SerializeField] private UnitData unitData;

    [Header("References")]
    [Tooltip("Индикатор выделения")] [SerializeField] private GameObject selectionIndicator;
    [Tooltip("Индикатор цели")] [SerializeField] private GameObject targetIndicator;
    [Tooltip("MeshRenderer основного объекта")] [SerializeField] private MeshRenderer mainMeshRenderer;

    [Header("Team Materials")]
    [Tooltip("Материал игрока 1")] [SerializeField] private Material player1Material;
    [Tooltip("Материал игрока 2")] [SerializeField] private Material player2Material;

    [Header("Movement")]
    [Tooltip("Скорость перемещения юнита")] [SerializeField] private float moveSpeed = 5f;
    #endregion

    #region Private Fields
    private Coroutine _movementCoroutine;
    private int? _overriddenMoveSpeed = null;
    #endregion

    #region Network Variables
    public UnitData Data => unitData;
    public NetworkVariable<int> SyncedMoveSpeed = new NetworkVariable<int>();
    public NetworkVariable<int> SyncedAttackRange = new NetworkVariable<int>();
    public NetworkVariable<int> PlayerId = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    #endregion

    #region Properties
    public int CurrentMoveSpeed => _overriddenMoveSpeed ?? SyncedMoveSpeed.Value;
    public int AttackRange => SyncedAttackRange.Value;
    public int PlayerIdValue => PlayerId.Value;
    #endregion

    #region Events
    public event Action<IUnit> OnUnitDied;
    public event Action<IUnit> OnUnitSelected;
    public event Action<IUnit> OnUnitDeselected;
    #endregion

    #region Initialization and Visuals
    public void Initialize(UnitData data, int playerId)
    {
        unitData = data;
        if (IsServer)
        {
            PlayerId.Value = playerId;
            SyncedMoveSpeed.Value = data.moveSpeed;
            SyncedAttackRange.Value = data.attackRange;
        }
    }

    public override void OnNetworkSpawn()
    {
        PlayerId.OnValueChanged += OnPlayerIdChanged;
        OnPlayerIdChanged(0, PlayerId.Value);
    }

    public override void OnNetworkDespawn()
    {
        PlayerId.OnValueChanged -= OnPlayerIdChanged;
    }

    private void OnPlayerIdChanged(int previousValue, int newValue)
    {
        if (newValue == 0) return;
        gameObject.name = $"Unit (Player {newValue})";
        SetTeamColor(newValue);
    }

    private void SetTeamColor(int ownerId)
    {
        if (mainMeshRenderer == null) return;
        if (ownerId == 1 && player1Material != null)
        {
            mainMeshRenderer.material = player1Material;
        }
        else if (ownerId == 2 && player2Material != null)
        {
            mainMeshRenderer.material = player2Material;
        }
    }

    public void Select()
    {
        if (!IsOwner) return;
        selectionIndicator?.SetActive(true);
        OnUnitSelected?.Invoke(this);
    }

    public void Deselect()
    {
        selectionIndicator?.SetActive(false);
        OnUnitDeselected?.Invoke(this);
    }

    public void SetAsTarget(bool isTarget)
    {
        targetIndicator?.SetActive(isTarget);
    }
    #endregion

    #region Movement
    public void TryMoveAlongPath(System.Collections.Generic.List<GridCell> path)
    {
        if (!IsOwner) return;
        Vector2Int[] pathCoords = new Vector2Int[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            pathCoords[i] = path[i].coordinates;
        }
        MoveServerRpc(pathCoords);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2Int[] pathCoords)
    {
        if (PlayerId.Value != TurnManager.Instance.CurrentPlayerId.Value) return;
        if (!TurnManager.Instance.CanMove.Value) return;
        if (pathCoords.Length - 1 > (_overriddenMoveSpeed ?? unitData.moveSpeed)) return;
        TurnManager.Instance.OnActionTaken(ActionType.Move);
        if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
        _movementCoroutine = StartCoroutine(MoveCoroutine(pathCoords));
    }

    private System.Collections.IEnumerator MoveCoroutine(Vector2Int[] pathCoords)
    {
        GridManager gridManager = FindObjectOfType<GridManager>();
        if (gridManager == null) yield break;
        gridManager.ClearCellOccupation(gridManager.WorldToGrid(transform.position));
        for (int i = 1; i < pathCoords.Length; i++)
        {
            Vector3 targetPosition = gridManager.GridToWorld(pathCoords[i]);
            Vector3 startPosition = transform.position;
            float journeyLength = Vector3.Distance(startPosition, targetPosition);
            if (journeyLength <= 0.01f) continue;
            float journeyTime = 0f;
            while (journeyTime < journeyLength / moveSpeed)
            {
                journeyTime += Time.deltaTime;
                float percent = Mathf.Clamp01(journeyTime / (journeyLength / moveSpeed));
                transform.position = Vector3.Lerp(startPosition, targetPosition, percent);
                yield return null;
            }
            transform.position = targetPosition;
        }
        gridManager.SetCellOccupation(this, gridManager.WorldToGrid(transform.position));
        _movementCoroutine = null;
    }
    #endregion

    #region Attack
    public void TryAttack(Unit target)
    {
        if (!IsOwner || target == null) return;
        AttackServerRpc(target.NetworkObjectId);
    }

    [ServerRpc]
    private void AttackServerRpc(ulong targetNetworkObjectId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObject)) return;
        Unit targetUnit = targetObject.GetComponent<Unit>();
        if (PlayerId.Value != TurnManager.Instance.CurrentPlayerId.Value) return;
        if (!TurnManager.Instance.CanAttack.Value) return;
        float distance = Vector3.Distance(transform.position, targetUnit.transform.position);
        if (distance > unitData.attackRange) return;
        GridManager gridManager = FindObjectOfType<GridManager>();
        Vector2Int myCoords = gridManager.WorldToGrid(transform.position);
        Vector2Int targetCoords = gridManager.WorldToGrid(targetUnit.transform.position);
        var lineOfSight = gridManager.GetCellsOnLine(myCoords, targetCoords);
        foreach (var cell in lineOfSight)
        {
            if (!cell.isWalkable && cell.coordinates != myCoords && cell.coordinates != targetCoords)
            {
                return;
            }
        }
        TurnManager.Instance.OnActionTaken(ActionType.Attack);
        targetUnit.TakeDamage(1);
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer) return;
        FindObjectOfType<GridManager>()?.UnregisterUnit(this);
        GameManager.Instance?.OnUnitDied(this);
        OnUnitDied?.Invoke(this);
        GetComponent<NetworkObject>().Despawn(true);
    }
    #endregion

    #region Special
    [ClientRpc]
    public void GrantInfiniteMoveSpeedClientRpc()
    {
        GrantInfiniteMoveSpeed();
    }

    public void GrantInfiniteMoveSpeed()
    {
        _overriddenMoveSpeed = 999;
    }
    #endregion
}