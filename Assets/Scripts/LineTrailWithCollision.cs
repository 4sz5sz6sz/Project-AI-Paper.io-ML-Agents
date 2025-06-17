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

    private Transform playerTransform; // 자신의 Transform 사용
    public bool trailActive = false;  // 외부에서 true로 설정
    private bool collisionActive = false;
    //코너 트래커에 있는 playerId를 가져오기 위해 코너트래커를 가져오는 부분
    public CornerPointTracker cornerTracker;
    void Start()
    {
        playerTransform = transform; // 자신의 Transform 참조
        Debug.Log($"{cornerTracker.playerId}번 플레이어의 trail 위치: {playerTransform.position}");

        lineRenderer = GetComponent<LineRenderer>();
        edgeCollider = gameObject.AddComponent<EdgeCollider2D>();

        edgeCollider.isTrigger = true;

        //코너트래커 가져옴
        cornerTracker = transform.parent?.GetComponent<CornerPointTracker>();

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
        if (!trailActive) return;

        Vector3 currentPosition = playerTransform.position;
        Vector3 direction = (currentPosition - lastPosition).normalized;

        if (direction != Vector3.zero)
            lastDirection = direction; float distance = Vector3.Distance(currentPosition, lastPosition);
        if (distance >= minDistance)
        {
            lastPosition = currentPosition;

            // ✅ 오프셋 위치 계산
            Vector3 newPoint = GetOffsetPoint(currentPosition, lastDirection * -1f);

            // BasePlayerController에서 trailActive를 제어하므로 여기서는 단순히 점만 추가
            AddPoint(newPoint);
        }

        if (points.Count >= 1 && !collisionActive)
            collisionActive = true;
    }

    // 플레이어 위치에서 이동 반대 방향으로 playerHalfSize만큼 떨어진 점 반환
    Vector3 GetOffsetPoint(Vector3 origin, Vector3 oppositeDirection)
    {
        Vector3 offset = oppositeDirection.normalized * playerHalfSize;
        Vector3 result = origin + offset;
        result.z = -1f;
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
        // MapManager 기반 trail 시스템으로 변경되어 
        // OnTriggerEnter2D는 더 이상 사용하지 않습니다.
        // 충돌 감지는 BasePlayerController에서 처리됩니다.
    }

    // 궤적 초기화 메서드 추가
    public void ResetTrail()
    {
        // 모든 점들을 초기화
        points.Clear();
        colliderPoints.Clear();

        // LineRenderer의 포지션 초기화
        lineRenderer.positionCount = 0;

        // EdgeCollider2D의 포인트 초기화
        edgeCollider.points = new Vector2[0];

        // 마지막 위치 현재 플레이어 위치로 업데이트
        lastPosition = playerTransform.position;

        // 충돌 비활성화 (새로운 궤적이 생성되기 시작할 때까지)
        collisionActive = false;
    }

    // 충돌선 보이도록 그려주는함수 (디버깅용)
    // void OnDrawGizmos()
    // {
    //     if (edgeCollider == null || edgeCollider.points == null || edgeCollider.points.Length < 2)
    //         return;

    //     Gizmos.color = Color.yellow;

    //     for (int i = 0; i < edgeCollider.points.Length - 1; i++)
    //     {
    //         Vector2 p1 = edgeCollider.transform.TransformPoint(edgeCollider.points[i]);
    //         Vector2 p2 = edgeCollider.transform.TransformPoint(edgeCollider.points[i + 1]);
    //         Gizmos.DrawLine(p1, p2);
    //     }
    // }
}
