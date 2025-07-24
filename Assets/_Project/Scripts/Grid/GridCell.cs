using UnityEngine;

public class GridCell
{
    public Vector2Int coordinates;
    public bool isWalkable;
    public Unit occupyingUnit;
    public int gCost;
    public int hCost;
    public GridCell parent;

    public int FCost => gCost + hCost;

    public GridCell(int x, int y, bool isWalkable = true)
    {
        this.coordinates = new Vector2Int(x, y);
        this.isWalkable = isWalkable;
        this.occupyingUnit = null;
    }
    
    public void ResetPathfindingData()
    {
        gCost = int.MaxValue;
        hCost = 0;
        parent = null;
    }
}