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

        // 시작점과 끝점 사이에 내 영역 내부의 점들을 찾아 추가
        var safeCornerPoints = CreateSafePolygon(cornerPoints, ownerValue);

        Vector2Int start = FindInteriorPoint(safeCornerPoints);
        FloodFill(start, safeCornerPoints, ownerValue);

        if (tileRenderer != null)
        {
            tileRenderer.RedrawAllTiles();
        }
    }

    private List<Vector2Int> CreateSafePolygon(List<Vector2Int> originalPoints, int ownerValue)
    {
        var result = new List<Vector2Int>(originalPoints);

        // 시작점과 끝점
        Vector2Int start = originalPoints[0];
        Vector2Int end = originalPoints[^1];

        // 내 영역 내부에서 경로 찾기
        List<Vector2Int> safePath = FindPathThroughOwnedArea(start, end, ownerValue);

        // 찾은 경로의 점들을 결과 리스트에 추가
        if (safePath != null && safePath.Count > 0)
        {
            result.AddRange(safePath);
        }

        return result;
    }

    private List<Vector2Int> FindPathThroughOwnedArea(Vector2Int start, Vector2Int end, int ownerValue)
    {
        var path = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        var parentMap = new Dictionary<Vector2Int, Vector2Int>();
        var directionMap = new Dictionary<Vector2Int, Vector2Int>(); // 각 지점의 진행 방향 저장

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        bool foundPath = false;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == end)
            {
                foundPath = true;
                break;
            }

            foreach (var dir in dirs)
            {
                Vector2Int next = current + dir;

                if (!InBounds(next) || visited.Contains(next))
                    continue;

                if (GetTile(next) == ownerValue)
                {
                    queue.Enqueue(next);
                    visited.Add(next);
                    parentMap[next] = current;
                    directionMap[next] = dir; // 진행 방향 저장
                }
            }
        }

        if (foundPath)
        {
            Vector2Int current = end;
            Vector2Int? lastDirection = null;

            // 경로 역추적하면서 방향 전환점만 저장
            while (parentMap.ContainsKey(current))
            {
                Vector2Int currentDirection = directionMap[current];

                // 방향이 바뀌는 지점이거나 시작/끝점인 경우에만 추가
                if (lastDirection == null || currentDirection != lastDirection || current == end || parentMap[current] == start)
                {
                    path.Add(current);
                }

                lastDirection = currentDirection;
                current = parentMap[current];
            }
            path.Add(start); // 시작점 추가
            path.Reverse();

            Debug.Log($"찾은 경로의 꼭짓점 개수: {path.Count}");
            foreach (var point in path)
            {
                Debug.Log($"꼭짓점: {point}");
            }
        }

        return path;
    }

    private Vector2Int FindInteriorPoint(List<Vector2Int> polygon)
    {
        // 동서남북 모든 방향 검사
        Vector2Int[] offsets = {
            new Vector2Int(1, 0),   // 오른쪽
            new Vector2Int(-1, 0),  // 왼쪽
            new Vector2Int(0, 1),   // 위
            new Vector2Int(0, -1)   // 아래
        };

        foreach (var p in polygon)
        {
            foreach (var offset in offsets)
            {
                Vector2Int candidate = p + offset;
                if (InBounds(candidate) && IsPointInPolygon(new Vector2(candidate.x + 0.5f, candidate.y + 0.5f), polygon))
                {
                    Debug.Log($"내부 점 찾음: {candidate}, 기준점: {p}, 사용된 offset: {offset}");
                    return candidate;
                }
            }
        }

        // 모든 방향에서 실패한 경우 폴리곤의 중심점 근처 점을 시도
        Vector2 center = Vector2.zero;
        foreach (var p in polygon)
        {
            center += new Vector2(p.x, p.y);
        }
        center /= polygon.Count;

        Vector2Int centerPoint = new Vector2Int(
            Mathf.RoundToInt(center.x),
            Mathf.RoundToInt(center.y)
        );

        Debug.Log($"모든 방향 실패, 중심점 사용: {centerPoint}");
        return centerPoint;
    }

    //BFS 코드
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
