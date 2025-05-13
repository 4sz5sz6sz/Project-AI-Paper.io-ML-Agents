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
        if (cornerPoints.Count == 0 || cornerPoints[^1] != gridPos)
        {
            cornerPoints.Add(gridPos);
            Debug.Log($"🧩 코너 추가: {gridPos}");
        }
    }

    public void FinalizePolygon()
    {
        if (cornerPoints.Count < 3)
        {
            Debug.Log("❌ 폐곡선이 아닙니다. 점 개수 부족");
            return;
        }

        Debug.Log($"🎯 폐곡선 완성 → 영역 점령 시작 (점 개수: {cornerPoints.Count})");
        mapManager.ApplyCornerArea(cornerPoints, playerId);
        Clear();
    }

    public void Clear()
    {
        Debug.Log("🧹 코너 포인트 초기화");
        cornerPoints.Clear();
    }

    public List<Vector2Int> GetPoints()
    {
        return new List<Vector2Int>(cornerPoints);
    }
}
