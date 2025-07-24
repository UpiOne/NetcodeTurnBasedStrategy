using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerSpawnConfig
{
    public int playerId;
    public List<UnitData> startingArmy;
    public RectInt spawnZone;
}