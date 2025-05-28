using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public int width = 100;
    public int height = 100;
    public int[,] tileStates;

    public TileRenderer tileRenderer;  // TileRenderer 참조

    void Awake()
    {
        tileStates = new int[width, height];

        // 예제 초기화: (0,0)~(9,9)를 1로 세팅
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

    // 코너 포인트로 이루어진 폴리곤 내부를 ownerValue로 채우고,
    // (addedCount, totalCount) 형태로
    // “추가된 개수”와 “전체 개수”를 반환
    public (int addedCount, int totalCount) ApplyCornerArea(List<Vector2Int> cornerPoints, int ownerValue)
    {
        // 1) 다각형(폐곡선)을 이루려면 최소한 꼭짓점 포인트가 3개는 있어야..
        if (cornerPoints == null || cornerPoints.Count < 3)
        {
            int totalCount = 0;
            
            if (tileRenderer != null)
            {
                totalCount = tileRenderer.GetGreenCount(); // 2) TileRenderer가 연결되어 있으면
            }
            
            return (addedCount: 0, totalCount: totalCount); // 3) 추가된 개수는 0, 전체 개수는 totalCount로 반환
        }
        // 채우기 전 전체 초록 타일 개수 구하는 로직직
        int beforeCount = 0;

        if (tileRenderer != null)
        {
            beforeCount = tileRenderer.GetGreenCount();
        }

        // 2) 내부 점 찾기
        Vector2Int start = FindInteriorPoint(cornerPoints);

        // 3) FloodFill로 내부 타일 ownerValue로 변경
        FloodFill(start, cornerPoints, ownerValue);

        // 4) 화면 갱신 & 채운 뒤 전체 초록 타일 개수
        if (tileRenderer != null)
        {
            int afterCount = tileRenderer.RedrawAllTilesAndGetGreenCount();
            int added = afterCount - beforeCount;
            Debug.Log($"[MapManager] 이번에 추가된 타일: {added}, 전체 초록 타일: {afterCount}");
            return (added, afterCount);
        }

        return (0, beforeCount);
    }

    private Vector2Int FindInteriorPoint(List<Vector2Int> polygon)
    {
        foreach (var p in polygon)
        {
            Vector2Int candidate = p + new Vector2Int(1, 0);
            if (InBounds(candidate) &&
                IsPointInPolygon(new Vector2(candidate.x + 0.5f, candidate.y + 0.5f), polygon))
                return candidate;
        }
        return polygon[0];
    }

    private void FloodFill(Vector2Int start, List<Vector2Int> polygon, int ownerValue)
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
            if (!IsPointInPolygon(new Vector2(cur.x + 0.5f, cur.y + 0.5f), polygon)) continue;

            SetTile(cur, ownerValue);

            foreach (var d in dirs)
            {
                var next = cur + d;
                if (visited.Add(next))
                    queue.Enqueue(next);
            }
        }
    }

    private bool IsPointInPolygon(Vector2 pt, List<Vector2Int> poly)
    {
        int n = poly.Count;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 pi = poly[i], pj = poly[j];
            if ((pi.y > pt.y) != (pj.y > pt.y) &&
                pt.x < (pj.x - pi.x) * (pt.y - pi.y) / (pj.y - pi.y) + pi.x)
                inside = !inside;
        }
        return inside;
    }
}
