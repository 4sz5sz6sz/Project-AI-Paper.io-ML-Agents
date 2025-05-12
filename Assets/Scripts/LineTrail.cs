using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class LineTrail : MonoBehaviour
{
    public float minDistance = 0.1f;     // 일정 거리 이상 이동했을 때만 포인트 추가
    public float lineWidthMultiplier = 1f;  // 플레이어 크기에 비례하여 선 너비 조정 (배율)

    private LineRenderer lineRenderer;
    private List<Vector3> points = new List<Vector3>();
    private Vector3 lastPosition;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;

        // 🎨 플레이어 색상 + 알파 0.5 적용
        Color lineColor = Color.white; // 기본 fallback 색상

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color playerColor = sr.color;
            lineColor = new Color(playerColor.r, playerColor.g, playerColor.b, 0.5f);
        }

        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        // 🎯 기타 설정
        float spriteWidth = 0.2f;
        if (sr != null)
            spriteWidth = sr.bounds.size.x;

        lineRenderer.startWidth = spriteWidth;
        lineRenderer.endWidth = spriteWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.numCornerVertices = 0;
        lineRenderer.useWorldSpace = true;

        lastPosition = transform.position;
        AddPoint(lastPosition);
    }


    void Update()
    {
        if (Vector3.Distance(transform.position, lastPosition) >= minDistance)
        {
            lastPosition = transform.position;
            AddPoint(lastPosition);
        }
    }

    void AddPoint(Vector3 point)
    {
        point.z = 0f;
        points.Add(point);
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());
    }
}