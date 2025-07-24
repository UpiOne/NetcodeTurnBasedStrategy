using System;
using Unity.Netcode;
using UnityEngine;

public enum ActionType { Move, Attack }

public interface ITurnManager
{
    event Action<int, int> OnTurnStarted; // turn, playerId
    event Action<int, int> OnTurnEnded;   // turn, playerId
    event Action<int> OnPlayerChanged;    // playerId
    int CurrentTurnValue { get; }
    int CurrentPlayerIdValue { get; }
    void StartGame();
    void EndTurnButton();
    void OnActionTaken(ActionType action);
}

public class TurnManager : NetworkBehaviour, ITurnManager
{
    public static TurnManager Instance { get; private set; }

    #region Inspector Fields
    [Header("Turn Settings")]
    [Tooltip("Время на ход (секунд)")]
    [SerializeField] private float turnTime = 60f;
    [Tooltip("Ход, на котором ничья")]
    [SerializeField] private int drawRuleTurn = 15;
    #endregion

    #region Network Variables
    public NetworkVariable<int> CurrentTurn { get; private set; } = new NetworkVariable<int>(0);
    public NetworkVariable<int> CurrentPlayerId { get; private set; } = new NetworkVariable<int>(1);
    public NetworkVariable<float> TurnTimer { get; private set; } = new NetworkVariable<float>(60f);
    public NetworkVariable<bool> CanMove { get; private set; } = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> CanAttack { get; private set; } = new NetworkVariable<bool>(true);
    #endregion

    #region Properties
    public int CurrentTurnValue => CurrentTurn.Value;
    public int CurrentPlayerIdValue => CurrentPlayerId.Value;
    #endregion

    #region Events
    public event Action<int, int> OnTurnStarted;
    public event Action<int, int> OnTurnEnded;
    public event Action<int> OnPlayerChanged;
    #endregion

    #region Private Fields
    private bool _isGameStarted = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Instance = this;
    }
    #endregion

    #region Network Spawn
    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            GameManager.Instance.Player1ClientId.OnValueChanged += (prev, next) => UpdateActionIconsForClient();
            GameManager.Instance.Player2ClientId.OnValueChanged += (prev, next) => UpdateActionIconsForClient();
            CurrentTurn.OnValueChanged += (prev, next) => {
                UIManager.Instance?.UpdateTurnInfo(next, CurrentPlayerId.Value);
                UpdateActionIconsForClient();
            };
            CurrentPlayerId.OnValueChanged += (prev, next) => {
                UIManager.Instance?.UpdateTurnInfo(CurrentTurn.Value, next);
                UpdateActionIconsForClient();
            };
            CanMove.OnValueChanged += (prev, next) => {
                UIManager.Instance?.SetMoveActionAvailable(next);
                UpdateActionIconsForClient();
            };
            CanAttack.OnValueChanged += (prev, next) => {
                UIManager.Instance?.SetAttackActionAvailable(next);
                UpdateActionIconsForClient();
            };
            TurnTimer.OnValueChanged += (prev, next) => UIManager.Instance?.UpdateTurnTimer(next);
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateTurnInfo(CurrentTurn.Value, CurrentPlayerId.Value);
                UIManager.Instance.SetMoveActionAvailable(CanMove.Value);
                UIManager.Instance.SetAttackActionAvailable(CanAttack.Value);
                UIManager.Instance.UpdateTurnTimer(TurnTimer.Value);
                UpdateActionIconsForClient();
            }
        }
    }
    #endregion

    #region Update Loops
    private void Update()
    {
        if (!IsServer || !_isGameStarted) return;
        TurnTimer.Value -= Time.deltaTime;
        if (TurnTimer.Value <= 0)
        {
            EndTurn();
        }
    }

    private void LateUpdate()
    {
        if (IsClient && _isGameStarted && UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnTimer(TurnTimer.Value);
        }
    }
    #endregion

    #region Turn Logic
    public void StartGame()
    {
        if (!IsServer) return;
        _isGameStarted = true;
        CurrentTurn.Value = 0;
        CurrentPlayerId.Value = 1;
        StartNewTurn();
    }

    private void StartNewTurn()
    {
        if (!IsServer) return;
        CurrentTurn.Value++;
        CanMove.Value = true;
        CanAttack.Value = true;
        TurnTimer.Value = turnTime;
        if (CurrentTurn.Value >= drawRuleTurn)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                int p1 = gm.Player1UnitsCount;
                int p2 = gm.Player2UnitsCount;
                if (p1 == p2 && p1 > 0)
                {
                    gm.GrantInfiniteMoveSpeedToAllUnits();
                }
            }
        }
        OnTurnStarted?.Invoke(CurrentTurn.Value, CurrentPlayerId.Value);
    }

    public void EndTurnButton()
    {
        if (GameManager.Instance == null) return;
        ulong localId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        bool isMyTurn = (CurrentPlayerId.Value == 1 && localId == GameManager.Instance.Player1ClientId.Value)
            || (CurrentPlayerId.Value == 2 && localId == GameManager.Instance.Player2ClientId.Value);
        if (isMyTurn)
        {
            RequestEndTurn();
        }
    }

    private void EndTurn()
    {
        if (!IsServer) return;
        OnTurnEnded?.Invoke(CurrentTurn.Value, CurrentPlayerId.Value);
        if (CurrentTurn.Value >= drawRuleTurn)
        {
            GameManager.Instance?.CheckForGameEndByTurnLimit();
        }
        CurrentPlayerId.Value = (CurrentPlayerId.Value == 1) ? 2 : 1;
        OnPlayerChanged?.Invoke(CurrentPlayerId.Value);
        StartNewTurn();
    }
    #endregion

    #region Action Logic
    public void OnActionTaken(ActionType action)
    {
        if (!IsServer) return;
        if (action == ActionType.Move)
        {
            if (!CanMove.Value) return;
            CanMove.Value = false;
        }
        else if (action == ActionType.Attack)
        {
            if (!CanAttack.Value) return;
            CanAttack.Value = false;
        }
        if (!CanMove.Value && !CanAttack.Value)
        {
            EndTurn();
        }
    }
    #endregion

    #region End Turn RPC
    public void RequestEndTurn()
    {
        EndTurnServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void EndTurnServerRpc(ServerRpcParams rpcParams = default)
    {
        if (GameManager.Instance.IsItThatPlayersTurn(rpcParams.Receive.SenderClientId, CurrentPlayerId.Value))
        {
            EndTurn();
        }
    }
    #endregion

    #region Helpers
    private int GetLocalPlayerId()
    {
        if (GameManager.Instance == null) return -1;
        var localId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        if (GameManager.Instance.IsItThatPlayersTurn(localId, 1)) return 1;
        if (GameManager.Instance.IsItThatPlayersTurn(localId, 2)) return 2;
        return -1;
    }

    private void UpdateActionIconsForClient()
    {
        if (GameManager.Instance == null) return;
        ulong localId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        bool isMyTurn = (CurrentPlayerId.Value == 1 && localId == GameManager.Instance.Player1ClientId.Value)
            || (CurrentPlayerId.Value == 2 && localId == GameManager.Instance.Player2ClientId.Value);
        UIManager.Instance?.UpdateActionIcons(isMyTurn, CanMove.Value, CanAttack.Value);
    }
    #endregion
}
