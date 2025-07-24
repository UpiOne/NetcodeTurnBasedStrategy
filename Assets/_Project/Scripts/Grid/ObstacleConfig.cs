using UnityEngine;

[CreateAssetMenu(fileName = "NewObstacleConfig", menuName = "Game Config/Obstacle Config")]
public class ObstacleConfig : ScriptableObject
{
    public GameObject obstaclePrefab;
    public int minAmount;
    public int maxAmount;
}