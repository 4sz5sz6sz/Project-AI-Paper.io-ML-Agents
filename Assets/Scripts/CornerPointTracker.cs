// CornerPointTracker.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CornerPointTracker : MonoBehaviour
{
    public List<Vector2Int> cornerPoints = new List<Vector2Int>();
    // private List<Vector2Int> cornerPoints = new List<Vector2Int>();
    public MapManager mapManager;
    public int playerId = 1;
    public LineRenderer lineRenderer;

    void Start()
    {
        // LineRenderer 초기화
        lineRenderer = gameObject.GetComponent<LineRenderer>();
        lineRenderer.startWidth = 1.1f; // 선의 두께
        lineRenderer.endWidth = 1.1f;
        lineRenderer.positionCount = 0;
    }

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

    // 저장된 꼭짓점을 1초 동안 검은색으로 출력
    public void DisplayCornersFor1Second()
    {
        StartCoroutine(DisplayCornersCoroutine());
    }

    private IEnumerator DisplayCornersCoroutine()
    {
        // 저장된 꼭짓점들이 존재하면 LineRenderer로 그리기
        if (cornerPoints.Count > 0)
        {
            lineRenderer.positionCount = cornerPoints.Count;
            for (int i = 0; i < cornerPoints.Count; i++)
            {
                // (꼭짓점의 위치)를 LineRenderer에 설정
                Vector3 pointPosition = new Vector3(cornerPoints[i].x, cornerPoints[i].y, 0f);
                lineRenderer.SetPosition(i, pointPosition);
            }

            // 검은색으로 설정
            lineRenderer.startColor = Color.black;
            lineRenderer.endColor = Color.black;

            // 1초 기다리기
            yield return new WaitForSeconds(1f);

            // 1초 후 LineRenderer 초기화 (선을 제거)
            lineRenderer.positionCount = 0;
        }
    }
}
