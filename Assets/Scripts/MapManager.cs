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
        if (cornerPoints == null || cornerPoints.Count < 2) return;

        // 시작점과 끝점 사이에 내 영역 내부의 점들을 찾아 추가
        var safeCornerPoints = CreateSafePolygon(cornerPoints, ownerValue);

        // 디버그 로그 추가
        Debug.Log($"===== Safe Corner Points (총 {safeCornerPoints.Count}개) =====");
        for (int i = 0; i < safeCornerPoints.Count; i++)
        {
            Debug.Log($"Point[{i}]: {safeCornerPoints[i]}");
            if (i < safeCornerPoints.Count - 1)
            {
                Vector2Int next = safeCornerPoints[i + 1];
                Debug.Log($"    -> 다음 점과의 차이: ({next.x - safeCornerPoints[i].x}, {next.y - safeCornerPoints[i].y})");
            }
        }
        Debug.Log("=====================================");

        // 1. 먼저 테두리 색칠
        // ColorTrailPath(safeCornerPoints, ownerValue);

        // 2. 내부 점 찾기
        Vector2Int start = FindInteriorPoint(safeCornerPoints);

        // 3. FloodFill로 내부 영역 채우기
        FloodFill(start, safeCornerPoints, ownerValue);

        if (tileRenderer != null)
        {
            tileRenderer.RedrawAllTiles();
        }
    }

    private List<Vector2Int> CreateSafePolygon(List<Vector2Int> originalPoints, int ownerValue)
    {
        if (originalPoints == null || originalPoints.Count < 2) return originalPoints;

        var result = new List<Vector2Int>();

        // 마지막 점을 제외한 모든 원본 점들을 순서대로 추가
        for (int i = 0; i < originalPoints.Count - 1; i++)
        {
            result.Add(originalPoints[i]);
        }

        // 마지막 점 추가
        Vector2Int lastPoint = originalPoints[^1];
        result.Add(lastPoint);

        // 마지막 점에서 시작점으로 가는 경로 찾기
        List<Vector2Int> safePath = FindPathThroughOwnedArea(lastPoint, originalPoints[0], ownerValue);

        if (safePath != null && safePath.Count > 2)  // 시작점과 끝점을 제외한 중간 점들만
        {
            for (int i = 1; i < safePath.Count - 1; i++)
            {
                // 중복 점 체크
                if (!result.Contains(safePath[i]))
                {
                    result.Add(safePath[i]);
                }
            }
        }

        Debug.Log("생성된 꼭짓점들 순서:");
        for (int i = 0; i < result.Count; i++)
        {
            Debug.Log($"{i}번째 점: {result[i]}");
        }

        return result;
    }

    private List<Vector2Int> FindPathThroughOwnedArea(Vector2Int start, Vector2Int end, int ownerValue)
    {
        var path = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        var parentMap = new Dictionary<Vector2Int, Vector2Int>();
        var directionMap = new Dictionary<Vector2Int, Vector2Int>();

        // 변수 추가
        bool foundPath = false;
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        queue.Enqueue(start);
        visited.Add(start);
        path.Add(start); // 시작점 추가

        // end 점은 나중에 추가하도록 변경
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            // end 점 바로 이전에 도달했는지 확인
            if (Mathf.Abs(current.x - end.x) + Mathf.Abs(current.y - end.y) == 1)
            {
                foundPath = true;
                break; // current = end 제거 (불필요)
            }

            foreach (var dir in dirs)
            {
                Vector2Int next = current + dir;

                if (!InBounds(next) || visited.Contains(next))
                    continue;

                // 내 영역인 타일만 통과
                if (GetTile(next) == ownerValue)
                {
                    queue.Enqueue(next);
                    visited.Add(next);
                    parentMap[next] = current;
                    directionMap[next] = dir;
                }
            }
        }

        if (foundPath)
        {
            // 경로를 찾은 경우에만 end 점 추가
            path.Add(end); // 끝점 추가
            Debug.Log($"경로 찾음 (총 {path.Count}개 점)");
            foreach (var point in path)
            {
                Debug.Log($"경로점: {point}");
            }
            return path;
        }

        return new List<Vector2Int>(); // 경로를 찾지 못한 경우 빈 리스트 반환
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
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        queue.Enqueue(start);
        visited.Add(start);

        // 각 타일의 네 모서리 점을 체크하기 위한 오프셋
        Vector2[] cornerOffsets = {
            new Vector2(0.1f, 0.1f),    // 좌하단
            new Vector2(0.1f, 0.9f),    // 좌상단
            new Vector2(0.9f, 0.1f),    // 우하단
            new Vector2(0.9f, 0.9f)     // 우상단
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (!InBounds(current)) continue;

            // 타일의 네 모서리를 모두 체크
            bool isInside = true;
            foreach (var offset in cornerOffsets)
            {
                Vector2 checkPoint = new Vector2(current.x + offset.x, current.y + offset.y);
                if (!IsPointInPolygon(checkPoint, polygon))
                {
                    isInside = false;
                    break;
                }
            }

            if (!isInside) continue;

            // 타일이 확실히 내부에 있다고 판단되면 색칠
            SetTile(current, ownerValue);

            // 인접 타일 체크
            foreach (var dir in dirs)
            {
                Vector2Int next = current + dir;
                if (!visited.Contains(next))
                {
                    visited.Add(next);
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

    public void ColorTrailPath(List<Vector2Int> cornerPoints, int ownerValue)
    {
        if (cornerPoints == null || cornerPoints.Count < 2) return;

        // 모든 연속된 두 점 사이의 경로를 색칠
        for (int i = 0; i < cornerPoints.Count - 1; i++)
        {
            ColorLineBetweenPoints(cornerPoints[i], cornerPoints[i + 1], ownerValue);
        }
    }

    private void ColorLineBetweenPoints(Vector2Int start, Vector2Int end, int ownerValue)
    {
        Vector2Int delta = new Vector2Int(
            Mathf.Clamp(end.x - start.x, -1, 1),
            Mathf.Clamp(end.y - start.y, -1, 1)
        );

        Vector2Int current = start;
        SetTile(current, ownerValue); // 시작점 색칠

        int iterations = 0;
        while (current != end)
        {
            iterations++;

            // 50회마다 현재 상태 출력
            if (iterations % 50 == 0)
            {
                Debug.Log($"반복 횟수: {iterations}, 현재 위치: {current}, 목표 위치: {end}, delta: {delta}");
            }

            // 100회 초과시 강제 종료
            if (iterations > 100)
            {
                Debug.LogError($"무한 루프 감지! start: {start}, end: {end}, current: {current}, delta: {delta}");
                break;
            }

            current += delta;
            SetTile(current, ownerValue);
        }
    }
}
