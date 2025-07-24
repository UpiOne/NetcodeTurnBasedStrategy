using UnityEngine;

[CreateAssetMenu(fileName = "NewUnitData", menuName = "Game Data/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("Info")]
    public string unitName;
    
    [Header("Stats")]
    public int moveSpeed;      // В клетках
    public int attackRange;    // В клетках

    [Header("Visuals")]
    public GameObject unitPrefab;
}