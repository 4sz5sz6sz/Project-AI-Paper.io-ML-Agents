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

    void Start()
    {
        // LineRenderer 초기화
        lineRenderer = gameObject.GetComponent<LineRenderer>();
        lineRenderer.startWidth = 1.1f; // 선의 두께
        lineRenderer.endWidth = 1.1f;
        lineRenderer.positionCount = 0;
        mapManager = FindFirstObjectByType<MapManager>();
        if (mapManager == null)
        {
            Debug.LogError("MapManager를 찾을 수 없습니다. Inspector에서 할당해주세요.");
        }
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
