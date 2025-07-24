using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public interface IUIManager
{
    event Action<bool, string> OnEndGamePanelShown;
    void ShowEndGamePanel(bool isWinner, string reason);
}

public class UIManager : MonoBehaviour, IUIManager
{
    public static UIManager Instance { get; private set; }

    #region Inspector Fields
    [Header("Text Elements")]
    [Tooltip("Текст таймера хода")] [SerializeField] private TextMeshProUGUI turnTimerText;
    [Tooltip("Текст информации о ходе")] [SerializeField] private TextMeshProUGUI turnInfoText;

    [Header("Action Indicators")]
    [Tooltip("Иконка действия перемещения")] [SerializeField] private Image moveActionIcon;
    [Tooltip("Иконка действия атаки")] [SerializeField] private Image attackActionIcon;

    [Header("Colors")]
    [Tooltip("Цвет доступного действия")] [SerializeField] private Color actionAvailableColor = Color.white;
    [Tooltip("Цвет использованного действия")] [SerializeField] private Color actionUsedColor = Color.gray;

    [Header("End Game UI")]
    [Tooltip("Панель конца игры")] [SerializeField] private GameObject endGamePanel;
    [Tooltip("Текст конца игры")] [SerializeField] private TextMeshProUGUI endGameText;
    #endregion

    #region Events
    public event Action<bool, string> OnEndGamePanelShown;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }
    #endregion

    #region Public API
    public void UpdateTurnTimer(float time)
    {
        if (turnTimerText != null)
        {
            turnTimerText.text = Mathf.CeilToInt(time).ToString();
        }
    }

    public void UpdateTurnInfo(int turnNumber, int playerId)
    {
        if (turnInfoText != null)
        {
            turnInfoText.text = $"Ход {turnNumber}: Игрок {playerId}";
        }
    }

    public void SetMoveActionAvailable(bool available)
    {
        if (moveActionIcon != null)
        {
            moveActionIcon.color = available ? actionAvailableColor : actionUsedColor;
        }
    }

    public void SetAttackActionAvailable(bool available)
    {
        if (attackActionIcon != null)
        {
            attackActionIcon.color = available ? actionAvailableColor : actionUsedColor;
        }
    }

    public void UpdateActionIcons(bool isMyTurn, bool canMove, bool canAttack)
    {
        Color moveColor = (isMyTurn && canMove) ? actionAvailableColor : actionUsedColor;
        Color attackColor = (isMyTurn && canAttack) ? actionAvailableColor : actionUsedColor;
        if (moveActionIcon != null)
        {
            moveActionIcon.color = moveColor;
        }
        if (attackActionIcon != null)
        {
            attackActionIcon.color = attackColor;
        }
    }

    public void ShowEndGamePanel(bool isWinner, string reason)
    {
        if (endGamePanel != null) endGamePanel.SetActive(true);
        if (endGameText != null)
        {
            if (isWinner)
                endGameText.text = $"Победа! {reason}";
            else
                endGameText.text = $"Поражение! {reason}";
        }
        OnEndGamePanelShown?.Invoke(isWinner, reason);
    }
    #endregion
}