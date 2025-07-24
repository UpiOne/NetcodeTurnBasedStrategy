using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public interface IGameManager
{
    event Action<int, string> OnGameEnded;
    event Action<Unit> OnUnitDiedEvent;
    void SpawnUnit(UnitData data, Vector2Int gridPosition, int playerId, ulong ownerClientId);
    bool IsItThatPlayersTurn(ulong clientId, int playerId);
}

public class GameManager : NetworkBehaviour, IGameManager
{
    public static GameManager Instance { get; private set; }
    
    #region Inspector Fields
    [Header("Prefabs")]
    [Tooltip("Префаб юнита для спавна")] 
    [SerializeField] private GameObject unitPrefab;
    
    [Header("Map Configuration")]
    [Tooltip("Текущий конфиг карты")] 
    [SerializeField] private MapConfig currentMapConfig;
    
    [Header("Unit Data")]
    [Tooltip("Данные для юнитов ближнего боя")] 
    [SerializeField] private UnitData meleeUnitData;
    [Tooltip("Данные для юнитов дальнего боя")] 
    [SerializeField] private UnitData rangedUnitData;
    #endregion

    #region Network Variables
    private NetworkVariable<int> _mapSeed = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Player1ClientId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Player2ClientId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    #endregion

    #region Private Fields
    private bool _mapGenerated = false;
    private GridManager _gridManager;
    private readonly List<Unit> _player1Units = new List<Unit>();
    private readonly List<Unit> _player2Units = new List<Unit>();
    #endregion

    #region Events
    public event Action<int, string> OnGameEnded;
    public event Action<Unit> OnUnitDiedEvent;
    #endregion

    #region Public Properties
    public int Player1UnitsCount => _player1Units.Count;
    public int Player2UnitsCount => _player2Units.Count;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    #endregion
    
    #region Public API
    public void SpawnUnit(UnitData data, Vector2Int gridPosition, int playerId, ulong ownerClientId) 
    {
        if (!IsServer) return;
        var spawnConfig = currentMapConfig.playerSpawns.Find(p => p.playerId == playerId);
        if (spawnConfig == null || !spawnConfig.spawnZone.Contains(gridPosition))
        {
            return;
        }
        GridCell cell = _gridManager.GetCell(gridPosition);
        if (cell == null || !cell.isWalkable || cell.occupyingUnit != null)
        {
            return;
        }
        Vector3 spawnPosition = _gridManager.GridToWorld(gridPosition);
        GameObject unitObject = Instantiate(unitPrefab, spawnPosition, Quaternion.identity);
        var networkObject = unitObject.GetComponent<NetworkObject>();
        networkObject.Spawn(true);
        networkObject.ChangeOwnership(ownerClientId);
        Unit unitComponent = unitObject.GetComponent<Unit>();
        if (unitComponent != null)
        {
            unitComponent.Initialize(data, playerId);
            _gridManager.RegisterUnit(unitComponent, gridPosition);
            if (playerId == 1) _player1Units.Add(unitComponent);
            else if (playerId == 2) _player2Units.Add(unitComponent);
        }
    }
    
    public void OnUnitDied(Unit unit)
    {
        if (!IsServer) return;
        if (unit.PlayerId.Value == 1) _player1Units.Remove(unit);
        else if (unit.PlayerId.Value == 2) _player2Units.Remove(unit);
        OnUnitDiedEvent?.Invoke(unit);
        CheckForGameEnd();
    }
    
    public bool IsItThatPlayersTurn(ulong clientId, int playerId)
    {
        if (playerId == 1) return clientId == Player1ClientId.Value;
        if (playerId == 2) return clientId == Player2ClientId.Value;
        return false;
    }

    public void GrantInfiniteMoveSpeedToAllUnits()
    {
        foreach (var unit in FindObjectsOfType<Unit>())
        {
            if (IsServer)
                unit.GrantInfiniteMoveSpeed();
            unit.GrantInfiniteMoveSpeedClientRpc();
        }
    }
    #endregion
    
    #region Game End Logic
    private void CheckForGameEnd()
    {
        if (_player1Units.Count == 0)
        {
            EndGame(2, "");
        }
        else if (_player2Units.Count == 0)
        {
            EndGame(1, "");
        }
    }
    
    public void CheckForGameEndByTurnLimit()
    {
        if (_player1Units.Count > _player2Units.Count)
        {
            EndGame(1, "У игрока 1 больше юнитов");
        }
        else if (_player2Units.Count > _player1Units.Count)
        {
            EndGame(2, "У игрока 2 больше юнитов");
        }
        else
        {
            foreach (var unit in _player1Units.Concat(_player2Units))
            {
                unit.GrantInfiniteMoveSpeed();
            }
        }
    }

    private void EndGame(int winnerId, string reason = "")
    {
        Time.timeScale = 0;
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.enabled = false;
        }
        OnGameEnded?.Invoke(winnerId, reason);
        GameOverClientRpc(winnerId, reason);
    }
    #endregion
    
    #region Network Spawn/Map Generation
     public override void OnNetworkSpawn()
     {
         if (IsServer)
         {
             _mapSeed.Value = (int)System.DateTime.Now.Ticks;
         }
         _mapSeed.OnValueChanged += OnMapSeedChanged;
         if (_mapSeed.Value != 0 && !_mapGenerated)
         {
             GenerateMapFromSeed(_mapSeed.Value);
         }
     }
    
    private void OnMapSeedChanged(int previousValue, int newValue)
    {
        GenerateMapFromSeed(newValue);
    }
    
    private void GenerateMapFromSeed(int seed)
    {
        if (_mapGenerated) return;
        UnityEngine.Random.InitState(seed);
        _gridManager = FindObjectOfType<GridManager>();
        if (_gridManager == null) return;
        MapGenerator.GenerateMap(currentMapConfig, _gridManager, this);
        _mapGenerated = true;
        if (IsServer)
        {
            Player1ClientId.Value = NetworkManager.Singleton.LocalClientId;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }
    #endregion
    
    #region Player Connection/Spawning
    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (clientId == Player1ClientId.Value) return; 
        if (Player2ClientId.Value == 0) 
        {
            Player2ClientId.Value = clientId;
            SpawnAllUnits();
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
    
    private void SpawnAllUnits()
    {
        if (!IsServer) return;
        var p1SpawnConfig = currentMapConfig.playerSpawns.Find(p => p.playerId == 1);
        var p2SpawnConfig = currentMapConfig.playerSpawns.Find(p => p.playerId == 2);
        foreach (var unitData in p1SpawnConfig.startingArmy)
        {
            Vector2Int? spawnPos = FindRandomSpawnPosition(p1SpawnConfig.spawnZone);
            if (spawnPos.HasValue)
            {
                SpawnUnit(unitData, spawnPos.Value, 1, Player1ClientId.Value);
            }
        }
        foreach (var unitData in p2SpawnConfig.startingArmy)
        {
            Vector2Int? spawnPos = FindRandomSpawnPosition(p2SpawnConfig.spawnZone);
            if (spawnPos.HasValue)
            {
                SpawnUnit(unitData, spawnPos.Value, 2, Player2ClientId.Value);
            }
        }
        if (TurnManager.Instance != null)
            TurnManager.Instance.StartGame();
    }
    
    private Vector2Int? FindRandomSpawnPosition(RectInt spawnZone)
    {
        int attempts = 50;
        while (attempts > 0)
        {
            int x = UnityEngine.Random.Range(spawnZone.xMin, spawnZone.xMax);
            int y = UnityEngine.Random.Range(spawnZone.yMin, spawnZone.yMax);
            Vector2Int coords = new Vector2Int(x, y);
            GridCell cell = _gridManager.GetCell(coords);
            if (cell != null && cell.isWalkable && cell.occupyingUnit == null)
            {
                return coords;
            }
            attempts--;
        }
        return null;
    }
    #endregion
 
    #region RPCs
    [ClientRpc]
    private void GameOverClientRpc(int winnerId, string reason)
    {
        ulong localId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        bool isWinner = (winnerId == 1 && localId == Player1ClientId.Value) || (winnerId == 2 && localId == Player2ClientId.Value);
        UIManager.Instance?.ShowEndGamePanel(isWinner, reason);
    }
    #endregion
}