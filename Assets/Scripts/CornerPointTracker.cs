// CornerPointTracker.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CornerPointTracker : MonoBehaviour
{
    public List<Vector2Int> cornerPoints = new List<Vector2Int>();
    // private List<Vector2Int> cornerPoints = new List<Vector2Int>();
    MapManager mapManager;
    public int playerId = 1;
    LineRenderer lineRenderer;

    public List<Vector2Int> additionalPoints = new List<Vector2Int>(); // 추가된 꼭짓점들을 저장
    private LineRenderer additionalLineRenderer; // 추가된 꼭짓점용 라인렌더러

    void Start()
    {
        // 기존 LineRenderer 초기화
        lineRenderer = gameObject.GetComponent<LineRenderer>();
        lineRenderer.startWidth = 1.1f; // 선의 두께
        lineRenderer.endWidth = 1.1f;
        lineRenderer.positionCount = 0;
        mapManager = FindFirstObjectByType<MapManager>();
        if (mapManager == null)
        {
            Debug.LogError("MapManager를 찾을 수 없습니다. Inspector에서 할당해주세요.");
        }

        // 추가된 꼭짓점용 LineRenderer 생성
        GameObject additionalLine = new GameObject("AdditionalPointsLine");
        additionalLine.transform.SetParent(transform);
        additionalLineRenderer = additionalLine.AddComponent<LineRenderer>();
        additionalLineRenderer.startWidth = 0.5f;
        additionalLineRenderer.endWidth = 0.5f;
        additionalLineRenderer.startColor = Color.yellow; // 추가된 점은 노란색으로 표시
        additionalLineRenderer.endColor = Color.yellow;
    }

    public void AddCorner(Vector2Int gridPos)
    {
        if (cornerPoints.Count == 0 || cornerPoints[^1] != gridPos)
        {
            cornerPoints.Add(gridPos);
            // Debug.Log($"🧩 코너 추가: {gridPos}");
        }
    }

    public void FinalizePolygon()
    {
        if (cornerPoints == null || cornerPoints.Count < 2)
        {
            Debug.Log("❌ 유효하지 않은 경로: 점이 부족함");
            return;
        }

        Debug.Log($"🎯 영역 점령 시작 (점 개수: {cornerPoints.Count})");
        mapManager.ApplyCornerArea(cornerPoints, playerId);
        Clear();
    }

    // 안전 경로의 추가 꼭짓점들을 표시하는 메서드
    public void ShowAdditionalPoints(List<Vector2Int> points)
    {
        additionalPoints = points;
        if (points == null || points.Count == 0)
        {
            additionalLineRenderer.positionCount = 0;
            return;
        }

        additionalLineRenderer.positionCount = points.Count;
        Vector3[] positions = new Vector3[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            positions[i] = new Vector3(points[i].x, points[i].y, 0);
        }

        additionalLineRenderer.SetPositions(positions);
        Debug.Log($"추가된 안전 경로 꼭짓점 표시: {points.Count}개");
    }

    public void Clear()
    {
        Debug.Log("🧹 코너 포인트 초기화");
        cornerPoints.Clear();
        additionalPoints.Clear(); // 추가된 점들도 초기화
        if (additionalLineRenderer != null)
        {
            additionalLineRenderer.positionCount = 0;
        }
    }

    public List<Vector2Int> GetPoints()
    {
        return new List<Vector2Int>(cornerPoints);
    }

    // 저장된 꼭짓점을 1초 동안 검은색으로 출력
    public void DisplayCornersFor1Second()
    {
        // 제대로 작동 안됨..
        StartCoroutine(DisplayCornersCoroutine());
    }

    private IEnumerator DisplayCornersCoroutine()
    {
        //제대로 작동 안됨..
        if (cornerPoints.Count > 0)
        {
            // 폐곡선을 만들기 위해 마지막 점과 첫 점을 연결
            lineRenderer.positionCount = cornerPoints.Count + 1;

            // 모든 코너 포인트 추가
            for (int i = 0; i < cornerPoints.Count; i++)
            {
                Vector3 pointPosition = new Vector3(cornerPoints[i].x, cornerPoints[i].y, 0f);
                lineRenderer.SetPosition(i, pointPosition);
                Debug.Log($"꼭짓점 {i}: {cornerPoints[i]} -> 위치: {pointPosition}");
            }

            // 마지막에 첫 번째 점을 다시 추가하여 폐곡선 완성
            lineRenderer.SetPosition(cornerPoints.Count, new Vector3(cornerPoints[0].x, cornerPoints[0].y, 0f));

            // 선 색상과 너비 설정
            lineRenderer.startColor = Color.black;
            lineRenderer.endColor = Color.black;
            lineRenderer.startWidth = 0.2f;
            lineRenderer.endWidth = 0.2f;

            yield return new WaitForSeconds(1f);
            lineRenderer.positionCount = 0;
        }
    }
}
