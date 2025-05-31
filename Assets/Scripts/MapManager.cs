using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // 싱글톤 인스턴스
    public static MapManager Instance { get; private set; }
    // ─────────────────────────────────────────────────

    public int width = 100;
    public int height = 100;
    public int[,] tileStates;

    [Tooltip("TileRenderer 참조 (Inspector에서 할당)")]
    public TileRenderer tileRenderer;

    void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 맵 상태 초기화
        tileStates = new int[width, height];
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
                tileStates[x, y] = 1;
    }

    void Start()
    {
        if (tileRenderer != null)
            tileRenderer.RedrawAllTiles();
    }

    public bool InBounds(Vector2Int pos)
        => pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;

    public void SetTile(Vector2Int pos, int value)
    {
        if (InBounds(pos))
            tileStates[pos.x, pos.y] = value;
    }

    public int GetTile(Vector2Int pos)
        => InBounds(pos) ? tileStates[pos.x, pos.y] : -1;

    /// <summary>
    /// cornerPoints로 정의된 폴리곤 내부를 ownerValue로 채우고,
    /// 이번에 추가된 타일 수와
    /// 전체 초록 타일 수(afterCount)를 콘솔에 찍고,
    /// GameController.SetScore(afterCount) 호출 → UI 갱신
    /// </summary>
    public int ApplyCornerArea(List<Vector2Int> cornerPoints, int ownerValue)
    {
        // 1) 다각형 성립 불가 시
        if (cornerPoints == null || cornerPoints.Count < 2)
        {
            Debug.Log("[MapManager] ApplyCornerArea: 유효하지 않은 cornerPoints");
            int totalCount = (tileRenderer != null)
                ? tileRenderer.GetGreenCount()
                : 0;
            Debug.Log($"[MapManager] 총 초록 타일 (변화 없음): {totalCount}");
            return 0;
        }

        // 2) 채우기 전 전체 초록 개수
        int before = (tileRenderer != null)
            ? tileRenderer.GetGreenCount()
            : 0;

        // 3) FloodFill
        // 시작점과 끝점 사이에 내 영역 내부의 점들을 찾아 추가
        var safeCornerPoints = CreateSafePolygon(cornerPoints, ownerValue);
        // 내부 점 찾기
        Vector2Int start = FindInteriorPoint(safeCornerPoints);


        FloodFill(start, safeCornerPoints, ownerValue);



        // 4) 화면 갱신 + 전체 초록 개수
        int after = (tileRenderer != null)
            ? tileRenderer.RedrawAllTilesAndGetGreenCount()
            : before;

        int addedCount = after - before;

        // 5) 로그 출력
        Debug.Log($"[MapManager] 이번에 추가된 타일: {addedCount}");
        Debug.Log($"[MapManager] 전체 초록 타일: {after}");

        // 6) UI(점수) 업데이트
        if (GameController.Instance != null)
            GameController.Instance.SetScore(after);

        return addedCount;
    }

    private List<Vector2Int> CreateSafePolygon(List<Vector2Int> originalPoints, int ownerValue)
    {
        var result = new List<Vector2Int>(originalPoints);

        Vector2Int start = originalPoints[0];
        Vector2Int end = originalPoints[^1];

        List<Vector2Int> safePath = FindPathThroughOwnedArea(start, end, ownerValue);

        if (safePath != null && safePath.Count > 0)
        {
            // 코너 트래커 찾아서 추가된 점들 표시
            var cornerTracker = FindAnyObjectByType<CornerPointTracker>();
            if (cornerTracker != null)
            {
                cornerTracker.ShowAdditionalPoints(safePath);
            }

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

    // BFS FloodFill
    private void FloodFill(Vector2Int start, List<Vector2Int> poly, int ownerValue)
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
                if (!IsPointInPolygon(checkPoint, poly))
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

    // Ray-casting 점-다각형 포함 검사
    private bool IsPointInPolygon(Vector2 pt, List<Vector2Int> poly)
    {
        bool inside = false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if ((a.y > pt.y) != (b.y > pt.y) &&
                pt.x < (b.x - a.x) * (pt.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
