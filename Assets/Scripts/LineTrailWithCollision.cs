using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class LineTrailWithCollision : MonoBehaviour
{
    public float minDistance = 0.1f;
    public float lineWidth = 0.2f;
    public float trailAlpha = 0.5f;

    private LineRenderer lineRenderer;
    private EdgeCollider2D edgeCollider;
    private List<Vector3> points = new List<Vector3>();
    private List<Vector2> colliderPoints = new List<Vector2>();

    private Vector3 lastPosition;
    private Vector3 lastDirection = Vector3.zero;
    private float playerHalfSize = 0.3f;

    private Transform playerTransform;
    public bool trailActive = false;
    private bool collisionActive = false;

    // 코너 트래커 (추가적인 기능을 위해)
    public CornerPointTracker cornerTracker;

    void Awake()
    {
        // Transform 및 위치 초기화
        playerTransform = transform;
        lastPosition = playerTransform.position;
        lastDirection = Vector3.right;
    }

    void Start()
    {
        // LineRenderer 및 Collider 초기화
        lineRenderer = GetComponent<LineRenderer>();
        edgeCollider = gameObject.AddComponent<EdgeCollider2D>();
        edgeCollider.isTrigger = true;

        // 코너 트래커 가져오기
        cornerTracker = transform.parent?.GetComponent<CornerPointTracker>();

        // 색상, 너비, 반투명도 설정
        SpriteRenderer sr = playerTransform.GetComponent<SpriteRenderer>();
        Color baseColor = Color.red;
        float spriteWidth = lineWidth;

        if (sr != null)
        {
            baseColor = sr.color;
            spriteWidth = sr.bounds.size.x;
            playerHalfSize = sr.bounds.size.x / 2f;
        }

        Color lineColor = new Color(baseColor.r, baseColor.g, baseColor.b, trailAlpha);
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = spriteWidth;
        lineRenderer.endWidth = spriteWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.numCornerVertices = 0;
        lineRenderer.useWorldSpace = true;
    }

    void Update()
    {
        if (!trailActive) return;

        Vector3 currentPosition = playerTransform.position;
        Vector3 direction = (currentPosition - lastPosition).normalized;

        if (direction != Vector3.zero)
            lastDirection = direction;
        float distance = Vector3.Distance(currentPosition, lastPosition);
        if (distance >= minDistance)
        {
            lastPosition = currentPosition;
            Vector3 newPoint = GetOffsetPoint(currentPosition, lastDirection * -1f);
            AddPoint(newPoint);
        }

        if (points.Count >= 1 && !collisionActive)
            collisionActive = true;
    }

    // 이동 반대 방향으로 오프셋 위치 계산
    Vector3 GetOffsetPoint(Vector3 origin, Vector3 oppositeDirection)
    {
        Vector3 offset = oppositeDirection.normalized * playerHalfSize;
        Vector3 result = origin + offset;
        result.z = -1f;
        return result;
    }

    // 궤적 점 추가
    void AddPoint(Vector3 point)
    {
        if (points.Count > 0 && Vector3.Distance(points[points.Count - 1], point) < 0.01f)
            return;

        points.Add(point);
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());

        colliderPoints.Add(new Vector2(point.x, point.y));
        edgeCollider.points = colliderPoints.ToArray();
    }

    // 궤적 초기화 및 null 체크 포함
    public void ResetTrail()
    {
        // 필드가 null인 경우 초기화
        if (playerTransform == null)
            playerTransform = transform;
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
        if (edgeCollider == null)
        {
            edgeCollider = gameObject.AddComponent<EdgeCollider2D>();
            edgeCollider.isTrigger = true;
        }
        if (points == null)
            points = new List<Vector3>();
        if (colliderPoints == null)
            colliderPoints = new List<Vector2>();

        // 기존 데이터 클리어
        points.Clear();
        colliderPoints.Clear();
        lineRenderer.positionCount = 0;
        edgeCollider.points = new Vector2[0];

        // 위치 초기화
        lastPosition = playerTransform.position;
        collisionActive = false;
    }
}
