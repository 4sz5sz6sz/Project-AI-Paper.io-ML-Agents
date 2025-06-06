using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{    // ─────────────────────────────────────────────────
    // 싱글톤 인스턴스
    public static MapManager Instance { get; private set; }
    // ─────────────────────────────────────────────────

    public int width = 100;
    public int height = 100;
    public int[,] tileStates;
    public int[,] trailStates; // 궤적 정보 저장 (0: 궤적 없음, 1~N: 플레이어 ID)

    [Tooltip("TileRenderer 참조 (Inspector에서 할당)")]
    public TileRenderer tileRenderer;

    private const int INITIAL_TERRITORY_SIZE = 10;  // 초기 영역 크기

    void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;        // 맵 상태 초기화
        tileStates = new int[width, height];
        trailStates = new int[width, height]; // 궤적 상태도 초기화

        // Start()로 이동하여 플레이어들이 모두 생성된 후 영역 초기화
    }

    void Start()
    {
        // 모든 플레이어 찾기
        BasePlayerController[] players = FindObjectsByType<BasePlayerController>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            Vector2Int playerPos = Vector2Int.RoundToInt(player.transform.position);
            InitializePlayerTerritory(playerPos, player.GetComponent<CornerPointTracker>()?.playerId ?? 1);
        }

        // TileRenderer 업데이트
        if (tileRenderer != null)
            tileRenderer.RedrawAllTiles();
    }

    private void InitializePlayerTerritory(Vector2Int center, int playerId)
    {
        int halfSize = INITIAL_TERRITORY_SIZE / 2;
        int startX = Mathf.Clamp(center.x - halfSize, 0, width - INITIAL_TERRITORY_SIZE);
        int startY = Mathf.Clamp(center.y - halfSize, 0, height - INITIAL_TERRITORY_SIZE);

        Debug.Log($"플레이어 {playerId}의 초기 영역 설정: 중심점({center.x}, {center.y})");

        for (int x = startX; x < startX + INITIAL_TERRITORY_SIZE; x++)
        {
            for (int y = startY; y < startY + INITIAL_TERRITORY_SIZE; y++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    tileStates[x, y] = playerId;
                }
            }
        }
    }

    public void InitializePlayerScores()
    {
        Dictionary<int, int> scoreMap = new Dictionary<int, int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int owner = tileStates[x, y];
                if (owner >= 1)
                {
                    if (!scoreMap.ContainsKey(owner))
                        scoreMap[owner] = 0;
                    scoreMap[owner]++;
                }
            }
        }

        foreach (var kvp in scoreMap)
        {
            int playerId = kvp.Key;
            int count = kvp.Value;
            GameController.Instance?.SetScore(playerId, count); // 점수 직접 설정
            Debug.Log($"[초기 점수] 플레이어 {playerId}: {count}");
        }
    }

    public bool InBounds(Vector2Int pos)
        => pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;

    public void SetTile(Vector2Int pos, int newOwner)
    {
        if (!InBounds(pos)) return;

        int prevOwner = tileStates[pos.x, pos.y];
        if (prevOwner == newOwner) return;  // 변화 없음

        tileStates[pos.x, pos.y] = newOwner;

        // 점수 처리
        if (GameController.Instance != null)
        {
            if (prevOwner >= 1)
                GameController.Instance.AddScore(prevOwner, -1); // 뺏김
            if (newOwner >= 1)
                GameController.Instance.AddScore(newOwner, +1);  // 점령
        }
    }

    public int GetTile(Vector2Int pos)
        => InBounds(pos) ? tileStates[pos.x, pos.y] : -1;

    // ═══════════════════════════════════════════════════════════
    // 궤적(Trail) 관련 메서드들
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 특정 위치에 궤적을 설정합니다.
    /// </summary>
    /// <param name="pos">위치</param>
    /// <param name="playerId">플레이어 ID (0이면 궤적 제거)</param>
    public void SetTrail(Vector2Int pos, int playerId)
    {
        if (!InBounds(pos)) return;
        trailStates[pos.x, pos.y] = playerId;
    }

    /// <summary>
    /// 특정 위치의 궤적 정보를 가져옵니다.
    /// </summary>
    /// <param name="pos">위치</param>
    /// <returns>플레이어 ID (0이면 궤적 없음, -1이면 맵 범위 밖)</returns>
    public int GetTrail(Vector2Int pos)
        => InBounds(pos) ? trailStates[pos.x, pos.y] : -1;    /// <summary>
                                                              /// 특정 플레이어의 모든 궤적을 제거합니다.
                                                              /// </summary>
                                                              /// <param name="playerId">제거할 플레이어 ID</param>
    public void ClearPlayerTrails(int playerId)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (trailStates[x, y] == playerId)
                {
                    trailStates[x, y] = 0;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════

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

        // 내부 점 찾기 및 채우기 (먼저 실행)
        Vector2Int start = FindInteriorPoint(safeCornerPoints);
        FloodFill(start, safeCornerPoints, ownerValue);

        // 경계선 색칠 (나중에 실행)
        PaintBoundary(safeCornerPoints, ownerValue);



        // 4) 화면 갱신 + 플레이어 영역 개수 
        int after = 0;
        if (tileRenderer != null)
        {
            tileRenderer.RedrawAllTiles(); // 화면은 전체 갱신
            after = tileRenderer.GetPlayerTileCount(ownerValue); // 각 플레이어 타일 개수만 계산
        }
        else
        {
            after = before;
        }

        int addedCount = after - before;

        // 5) 로그 출력
        Debug.Log($"[MapManager] 이번에 추가된 타일: {addedCount}");
        Debug.Log($"[MapManager] 전체 초록 타일: {after}");

        // 6) UI(점수) 업데이트
        if (GameController.Instance != null)
            // GameController.Instance.SetScore(after);
            //점수를 업데이트하는 부분, SetScore(플레이어id, 점수)
            Debug.Log($"점수 갱신 시도: 플레이어 {ownerValue}, 점수 {after}");
        GameController.Instance.SetScore(ownerValue, after);
        return addedCount;
    }

    private void PaintBoundary(List<Vector2Int> points, int ownerValue)
    {
        if (points == null || points.Count < 2) return;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2Int start = points[i];
            Vector2Int end = points[i + 1];
            foreach (var p in GetLinePoints(start, end))
            {
                SetTile(p, ownerValue);
            }
        }
    }

    private IEnumerable<Vector2Int> GetLinePoints(Vector2Int p1, Vector2Int p2)
    {
        int x0 = p1.x, y0 = p1.y;
        int x1 = p2.x, y1 = p2.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            yield return new Vector2Int(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private List<Vector2Int> CreateSafePolygon(List<Vector2Int> originalPoints, int ownerValue)
    {
        var result = new List<Vector2Int>();

        // 1. 외부 경로 추가 (출발 -> 도착)
        result.AddRange(originalPoints);

        // 2. 내부 경로 찾기 (도착 -> 출발)
        Vector2Int start = originalPoints[^1];  // 도착점
        Vector2Int end = originalPoints[0];     // 출발점

        List<Vector2Int> safePath = FindPathThroughOwnedArea(start, end, ownerValue);

        if (safePath != null && safePath.Count > 0)
        {
            // 코너 트래커에 추가된 점들 표시
            var cornerTracker = FindAnyObjectByType<CornerPointTracker>();
            if (cornerTracker != null)
            {
                cornerTracker.ShowAdditionalPoints(safePath);
            }

            // 3. 내부 경로 추가 (도착 -> 출발)
            result.AddRange(safePath);
        }

        // 디버그 로그
        // Debug.Log("완성된 폐곡선 경로:");
        // for (int i = 0; i < result.Count; i++)
        // {
        //     Debug.Log($"점 {i}: {result[i]} " + 
        //         (i < originalPoints.Count ? "(외부 경로)" : "(내부 경로)"));
        // }

        return result;
    }

    private List<Vector2Int> FindPathThroughOwnedArea(Vector2Int start, Vector2Int end, int ownerValue)
    {
        var path = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        var parentMap = new Dictionary<Vector2Int, Vector2Int>();
        var directionMap = new Dictionary<Vector2Int, Vector2Int>(); // 각 지점의 진행 방향 저장

        // Debug.Log($"경로 탐색 시작: {start} → {end}, 소유자: {ownerValue}");
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

                if (GetTile(next) == ownerValue || end == next) // 소유 영역이거나 도착점인 경우
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
                    // Debug.Log($"내부 점 찾음: {candidate}, 기준점: {p}, 사용된 offset: {offset}");
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
