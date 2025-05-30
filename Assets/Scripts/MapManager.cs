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
        if (cornerPoints == null || cornerPoints.Count < 3)
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
        Vector2Int start = FindInteriorPoint(cornerPoints);
        FloodFill(start, cornerPoints, ownerValue);

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

    // 다각형 내부 판정용: 각 꼭짓점 우측 칸을 찍어 검사
    private Vector2Int FindInteriorPoint(List<Vector2Int> poly)
    {
        foreach (var p in poly)
        {
            var cand = p + Vector2Int.right;
            if (InBounds(cand) &&
                IsPointInPolygon(cand + new Vector2(0.5f, 0.5f), poly))
                return cand;
        }
        return poly[0];
    }

    // BFS FloodFill
    private void FloodFill(Vector2Int start, List<Vector2Int> poly, int ownerValue)
    {
        var queue = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        Vector2Int[] dirs = {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!InBounds(cur)) continue;
            if (!IsPointInPolygon(cur + new Vector2(0.5f, 0.5f), poly)) continue;

            SetTile(cur, ownerValue);

            foreach (var d in dirs)
            {
                var next = cur + d;
                if (visited.Add(next))
                    queue.Enqueue(next);
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
