// CornerPointTracker.cs
using UnityEngine;
using System.Collections.Generic;

public class CornerPointTracker : MonoBehaviour
{
    private List<Vector2Int> cornerPoints = new List<Vector2Int>();
    public MapManager mapManager;
    public int playerId = 1;

    public void AddCorner(Vector2Int gridPos)
    {
        cornerPoints.Add(gridPos);
        Debug.Log($"코너 추가: {gridPos}");
    }

    public void FinalizePolygon()
    {
        if (cornerPoints.Count < 3) return;
        mapManager.ApplyCornerArea(cornerPoints, playerId);
        Clear();
    }

    public void Clear()
    {
        cornerPoints.Clear();
    }

    public List<Vector2Int> GetPoints()
    {
        return new List<Vector2Int>(cornerPoints);
    }
} 
