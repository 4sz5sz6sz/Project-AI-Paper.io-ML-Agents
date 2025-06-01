using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CornerPointTracker : MonoBehaviour
{
    public List<Vector2Int> cornerPoints = new List<Vector2Int>();
    public int playerId = 1;
    private MapManager mapManager;

    // private LineRenderer lineRenderer; // TrailDrawer에서 가져올 것
    // private LineRenderer additionalLineRenderer; // 추가 꼭짓점용
    public List<Vector2Int> additionalPoints = new List<Vector2Int>(); // 추가된 꼭짓점 저장

    void Start()
    {
        // 🎯 TrailDrawer에 있는 LineRenderer 가져오기
        /*
        Transform trailDrawer = transform.Find("TrailDrawer");
        if (trailDrawer != null)
        {
            lineRenderer = trailDrawer.GetComponent<LineRenderer>();
        }
        else
        {
            Debug.LogError("❌ TrailDrawer 오브젝트를 찾을 수 없습니다.");
        }
        */

        // MapManager 찾기
        mapManager = FindFirstObjectByType<MapManager>();
        if (mapManager == null)
        {
            Debug.LogError("❌ MapManager를 찾을 수 없습니다.");
        }

        // ✅ 추가 꼭짓점 표시용 LineRenderer 생성
        /*
        GameObject additionalLine = new GameObject("AdditionalPointsLine");
        additionalLine.transform.SetParent(transform);
        additionalLineRenderer = additionalLine.AddComponent<LineRenderer>();
        additionalLineRenderer.startWidth = 0.5f;
        additionalLineRenderer.endWidth = 0.5f;
        additionalLineRenderer.startColor = Color.yellow;
        additionalLineRenderer.endColor = Color.yellow;
        additionalLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        additionalLineRenderer.useWorldSpace = true;
        */
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
        Debug.Log($"🌀 FinalizePolygon called by player {playerId}, point count: {cornerPoints.Count}");
        mapManager.ApplyCornerArea(cornerPoints, playerId);
        Clear();
    }

    public void ShowAdditionalPoints(List<Vector2Int> points)
    {
        additionalPoints = points;

        // 시각화 비활성화
        /*
        if (points == null || points.Count == 0)
        {
            additionalLineRenderer.positionCount = 0;
            return;
        }

        additionalLineRenderer.positionCount = points.Count;
        Vector3[] positions = new Vector3[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            positions[i] = new Vector3(points[i].x, points[i].y, -5f);
        }

        additionalLineRenderer.SetPositions(positions);
        Debug.Log($"🟡 추가된 안전 경로 꼭짓점 표시: {points.Count}개");
        */
    }

    public void Clear()
    {
        Debug.Log("🧹 코너 포인트 초기화");
        cornerPoints.Clear();
        additionalPoints.Clear();
        // if (additionalLineRenderer != null)
        // {
        //     additionalLineRenderer.positionCount = 0;
        // }
    }

    public List<Vector2Int> GetPoints()
    {
        return new List<Vector2Int>(cornerPoints);
    }

    public void DisplayCornersFor1Second()
    {
        // StartCoroutine(DisplayCornersCoroutine());
    }

    /*
    private IEnumerator DisplayCornersCoroutine()
    {
        if (lineRenderer == null)
        {
            Debug.LogWarning("❗ lineRenderer가 설정되지 않았습니다.");
            yield break;
        }

        if (cornerPoints.Count > 0)
        {
            lineRenderer.positionCount = cornerPoints.Count + 1;

            for (int i = 0; i < cornerPoints.Count; i++)
            {
                Vector3 pointPosition = new Vector3(cornerPoints[i].x, cornerPoints[i].y, 0f);
                lineRenderer.SetPosition(i, pointPosition);
                Debug.Log($"◾ 꼭짓점 {i}: {cornerPoints[i]} → 위치: {pointPosition}");
            }

            lineRenderer.SetPosition(cornerPoints.Count, new Vector3(cornerPoints[0].x, cornerPoints[0].y, 0f));

            lineRenderer.startColor = Color.black;
            lineRenderer.endColor = Color.black;
            lineRenderer.startWidth = 0.2f;
            lineRenderer.endWidth = 0.2f;

            yield return new WaitForSeconds(1f);

            lineRenderer.positionCount = 0;
        }
    }
    */
}
