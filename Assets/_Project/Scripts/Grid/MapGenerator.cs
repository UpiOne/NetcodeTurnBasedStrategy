using System.Collections.Generic;
using UnityEngine;

public static class MapGenerator
{
    public static void GenerateMap(MapConfig config, GridManager gridManager, GameManager gameManager)
    {
        gridManager.CreateGrid(config.gridWidth, config.gridHeight);
        
        PlaceObstacles(config, gridManager);
    }

    private static void PlaceObstacles(MapConfig config, GridManager gridManager)
    {
        foreach (var obstacleConf in config.obstacleTypes)
        {
            int amount = Random.Range(obstacleConf.minAmount, obstacleConf.maxAmount + 1);
            for (int i = 0; i < amount; i++)
            {
                int attempts = 50; 
                while (attempts > 0)
                {
                    int x = Random.Range(config.obstacleZone.xMin, config.obstacleZone.xMax);
                    int y = Random.Range(config.obstacleZone.yMin, config.obstacleZone.yMax);
                    Vector2Int coords = new Vector2Int(x, y);

                    bool isInSpawnZone = false;
                    foreach (var spawnConf in config.playerSpawns)
                    {
                        if (spawnConf.spawnZone.Contains(coords))
                        {
                            isInSpawnZone = true;
                            break;
                        }
                    }

                    GridCell cell = gridManager.GetCell(coords);
                    
                    if (cell != null && cell.isWalkable && !isInSpawnZone)
                    {
                        cell.isWalkable = false;
                        Vector3 position = gridManager.GridToWorld(coords);
                        Object.Instantiate(obstacleConf.obstaclePrefab, position, Quaternion.identity);
                        break; 
                    }
                    attempts--;
                }
            }
        }
    }
}