// MapManager.cs
using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public int width = 100;
    public int height = 100;
    public int[,] tileStates;

    void Awake()
    {
        tileStates = new int[width, height];
    }

    public bool InBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    public void SetTile(Vector2Int pos, int value)
    {
        if (InBounds(pos))
            tileStates[pos.x, pos.y] = value;
    }

    public int GetTile(Vector2Int pos)
    {
        return InBounds(pos) ? tileStates[pos.x, pos.y] : -1;
    }

    public void ApplyCornerArea(List<Vector2Int> cornerPoints, int ownerValue)
    {
        if (cornerPoints == null || cornerPoints.Count < 3) return;

        Vector2 center = GetPolygonCenter(cornerPoints);
        Vector2Int start = Vector2Int.RoundToInt(center);

        FloodFill(start, cornerPoints, ownerValue);
    }

    private Vector2 GetPolygonCenter(List<Vector2Int> points)
    {
        Vector2 sum = Vector2.zero;
        foreach (var p in points)
            sum += (Vector2)p;
        return sum / points.Count;
    }

    private void FloodFill(Vector2Int start, List<Vector2Int> polygon, int ownerValue)
    {
        if (!InBounds(start)) return;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (!InBounds(current)) continue;

            Vector2 worldPos = current;
            if (!IsPointInPolygon(worldPos, polygon)) continue;

            SetTile(current, ownerValue);

            foreach (var dir in dirs)
            {
                Vector2Int next = current + dir;
                if (!visited.Contains(next))
                {
                    queue.Enqueue(next);
                    visited.Add(next);
                }
            }
        }
    }

    private bool IsPointInPolygon(Vector2 point, List<Vector2Int> polygon)
    {
        int n = polygon.Count;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];
            if ((pi.y > point.y) != (pj.y > point.y) &&
                point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}