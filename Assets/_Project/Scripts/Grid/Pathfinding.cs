using System.Collections.Generic;
using UnityEngine;

public static class Pathfinding
{
    private const int MOVE_STRAIGHT_COST = 10;
    private const int MOVE_DIAGONAL_COST = 14;

    public static List<GridCell> FindPath(GridCell startCell, GridCell endCell, Dictionary<Vector2Int, GridCell> grid, Unit unitToMove)
    {

        List<GridCell> openList = new List<GridCell> { startCell };
        HashSet<GridCell> closedSet = new HashSet<GridCell>();
        
        foreach (var cell in grid.Values)
        {
            cell.ResetPathfindingData();
        }

        startCell.gCost = 0;
        startCell.hCost = CalculateDistanceCost(startCell, endCell);
        
        while (openList.Count > 0)
        {
            GridCell currentCell = GetLowestFCostCell(openList);
            
            if (currentCell == endCell)
            {
                return CalculatePath(endCell);
            }
            
            openList.Remove(currentCell);
            closedSet.Add(currentCell);
            
            foreach (GridCell neighbourCell in GetNeighbours(currentCell, grid))
            {
                if (closedSet.Contains(neighbourCell))
                {
                    continue;
                }
                if (!neighbourCell.isWalkable)
                {
                    continue;
                }
                if (neighbourCell.occupyingUnit != null && neighbourCell != endCell)
                {
                    continue;
                }
                
                int tentativeGCost = currentCell.gCost + CalculateDistanceCost(currentCell, neighbourCell);
                
                if (tentativeGCost < neighbourCell.gCost)
                {
                    neighbourCell.parent = currentCell;
                    neighbourCell.gCost = tentativeGCost;
                    neighbourCell.hCost = CalculateDistanceCost(neighbourCell, endCell);
                    
                    if (!openList.Contains(neighbourCell))
                    {
                        openList.Add(neighbourCell);
                    }
                }
            }
        }
        
        return null;
    }

    private static List<GridCell> CalculatePath(GridCell endCell)
    {
        List<GridCell> path = new List<GridCell>();
        path.Add(endCell);
        GridCell currentCell = endCell;
        while (currentCell.parent != null)
        {
            path.Add(currentCell.parent);
            currentCell = currentCell.parent;
        }
        path.Reverse();
        return path;
    }

    private static int CalculateDistanceCost(GridCell a, GridCell b)
    {
        int xDistance = Mathf.Abs(a.coordinates.x - b.coordinates.x);
        int yDistance = Mathf.Abs(a.coordinates.y - b.coordinates.y);
        int remaining = Mathf.Abs(xDistance - yDistance);
        return MOVE_DIAGONAL_COST * Mathf.Min(xDistance, yDistance) + MOVE_STRAIGHT_COST * remaining;
    }

    private static GridCell GetLowestFCostCell(List<GridCell> cellList)
    {
        GridCell lowestFCostCell = cellList[0];
        for (int i = 1; i < cellList.Count; i++)
        {
            if (cellList[i].FCost < lowestFCostCell.FCost)
            {
                lowestFCostCell = cellList[i];
            }
        }
        return lowestFCostCell;
    }

    private static List<GridCell> GetNeighbours(GridCell currentCell, Dictionary<Vector2Int, GridCell> grid)
    {
        List<GridCell> neighbours = new List<GridCell>();
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                Vector2Int neighbourCoords = currentCell.coordinates + new Vector2Int(x, y);
                if (grid.TryGetValue(neighbourCoords, out GridCell neighbour))
                {
                    neighbours.Add(neighbour);
                }
            }
        }
        return neighbours;
    }
}