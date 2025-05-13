// MapManager.cs
using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public int width = 100;
    public int height = 100;
    public int[,] tileStates;

    public TileRenderer tileRenderer;

    void Awake()
    {
        tileStates = new int[width, height];

        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                tileStates[x, y] = 1;
            }
        }
    }

    void Start()
    {
        if (tileRenderer != null)
        {
            tileRenderer.RedrawAllTiles();
        }
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

        Vector2Int start = FindInteriorPoint(cornerPoints);
        FloodFill(start, cornerPoints, ownerValue);

        if (tileRenderer != null)
        {
            tileRenderer.RedrawAllTiles();
        }
    }

    private Vector2Int FindInteriorPoint(List<Vector2Int> polygon)
    {
        foreach (var p in polygon)
        {
            Vector2Int offset = new Vector2Int(1, 0);
            Vector2Int candidate = p + offset;
            if (InBounds(candidate) && IsPointInPolygon(new Vector2(candidate.x + 0.5f, candidate.y + 0.5f), polygon))
            {
                return candidate;
            }
        }
        return polygon[0];
    }

    private void FloodFill(Vector2Int start, List<Vector2Int> polygon, int ownerValue)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (!InBounds(current)) continue;
            if (!IsPointInPolygon(new Vector2(current.x + 0.5f, current.y + 0.5f), polygon)) continue;

            SetTile(current, ownerValue);

            foreach (var dir in dirs)
            {
                Vector2Int next = current + dir;
                if (visited.Add(next))
                {
                    queue.Enqueue(next);
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
