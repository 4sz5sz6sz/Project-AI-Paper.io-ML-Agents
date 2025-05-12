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

    public Transform playerTransform; // Inspector에서 연결
    public bool trailActive = false;  // 외부에서 true로 설정
    private bool collisionActive = false;
    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        edgeCollider = gameObject.AddComponent<EdgeCollider2D>();
        
        edgeCollider.isTrigger = true;

        // ✅ 플레이어 색상 & 너비
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

        lastPosition = playerTransform.position;
        lastDirection = Vector3.right; // 초기 방향값 (임의로 설정)
    }

    void Update()
    {
        if (!trailActive) return; // trail이 비활성화 상태라면 궤적 기록 안 함

        Vector3 currentPosition = playerTransform.position;
        Vector3 direction = (currentPosition - lastPosition).normalized;

        if (direction != Vector3.zero)
            lastDirection = direction;

        float distance = Vector3.Distance(currentPosition, lastPosition);
        if (distance >= minDistance)
        {
            lastPosition = currentPosition;
            AddPoint(GetOffsetPoint(currentPosition, lastDirection * -1f)); // 이동 반대방향에 점 찍기
        }
        if (points.Count >= 1 && !collisionActive)
        {
            Debug.Log($"ㅎㅇㅎㅇㅇ");
            collisionActive = true;
        }
    }

    // 플레이어 위치에서 이동 반대 방향으로 playerHalfSize만큼 떨어진 점 반환
    Vector3 GetOffsetPoint(Vector3 origin, Vector3 oppositeDirection)
    {
        Vector3 offset = oppositeDirection.normalized * playerHalfSize;
        Vector3 result = origin + offset;
        result.z = 0f;
        return result;
    }

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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && collisionActive)
        {
            // 궤적과 충돌 시 처리
            Debug.Log($"{other.name} 라인에 닿았습니다!");
            // Destroy(other.gameObject);
        }
    }
}
