using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMapConfig", menuName = "Game Config/Map Config")]
public class MapConfig : ScriptableObject
{
    [Header("Grid Settings")]
    public int gridWidth = 30;
    public int gridHeight = 30;
    
    [Header("Obstacles")]
    public List<ObstacleConfig> obstacleTypes;
    public RectInt obstacleZone;

    [Header("Player Spawns")]
    public List<PlayerSpawnConfig> playerSpawns;
}