using UnityEngine;
using System.Collections.Generic;

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
    private Vector3 lastDirection = Vector3.right;
    private float playerHalfSize = 0.3f;

    private Transform playerTransform;
    public bool trailActive = false;
    private bool collisionActive = false;

    public CornerPointTracker cornerTracker;

    void Start()
    {
        // 부모 오브젝트 (Player)
        playerTransform = transform.parent;

        if (playerTransform == null)
        {
            Debug.LogError("❌ 부모 오브젝트가 없습니다.");
            return;
        }

        cornerTracker = playerTransform.GetComponent<CornerPointTracker>();
        if (cornerTracker == null)
        {
            Debug.LogError("❌ CornerPointTracker 컴포넌트가 없습니다.");
            return;
        }

        SpriteRenderer sr = playerTransform.GetComponent<SpriteRenderer>();
        Color baseColor = sr != null ? sr.color : Color.red;
        float spriteWidth = sr != null ? sr.bounds.size.x : lineWidth;
        playerHalfSize = spriteWidth / 2f;

        Color lineColor = new Color(baseColor.r, baseColor.g, baseColor.b, trailAlpha);

        // LineRenderer
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            Debug.LogError("❌ LineRenderer가 없습니다.");
            return;
        }

        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = spriteWidth;
        lineRenderer.endWidth = spriteWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.useWorldSpace = true;

        // EdgeCollider2D 자식 오브젝트로 생성
        Transform trailColliderObj = transform.Find("TrailCollider");
        if (trailColliderObj == null)
        {
            GameObject child = new GameObject("TrailCollider");
            child.transform.SetParent(transform);
            child.transform.localPosition = Vector3.zero;
            trailColliderObj = child.transform;
        }

        edgeCollider = trailColliderObj.GetComponent<EdgeCollider2D>();
        if (edgeCollider == null)
        {
            edgeCollider = trailColliderObj.gameObject.AddComponent<EdgeCollider2D>();
        }

        edgeCollider.isTrigger = true;
        edgeCollider.enabled = false;

        lastPosition = playerTransform.position;

        // 📌 시작 시 첫 점 추가
        Vector3 initialPoint = GetOffsetPoint(lastPosition, -lastDirection);
        AddPoint(initialPoint);
    }

    void Update()
    {
        if (!trailActive || cornerTracker == null || MapManager.Instance == null) return;

        Vector3 currentPosition = playerTransform.position;

        Vector3 direction = (currentPosition - lastPosition).normalized;
        if (direction != Vector3.zero)
            lastDirection = direction;

        float distance = Vector3.Distance(currentPosition, lastPosition);
        if (distance >= minDistance)
        {
            lastPosition = currentPosition;

            Vector3 newPoint = GetOffsetPoint(currentPosition, -lastDirection);
            Vector2Int tilePos = new Vector2Int(Mathf.FloorToInt(newPoint.x), Mathf.FloorToInt(newPoint.y));
            int owner = MapManager.Instance.GetTile(tilePos);

            if (owner != cornerTracker.playerId)
                AddPoint(newPoint);
        }

        if (points.Count >= 1 && !collisionActive)
        {
            edgeCollider.enabled = true;
            collisionActive = true;
        }
    }

    Vector3 GetOffsetPoint(Vector3 origin, Vector3 oppositeDirection)
    {
        Vector3 offset = oppositeDirection.normalized * playerHalfSize;
        Vector3 worldPoint = origin + offset;
        worldPoint.z = 0f;
        return worldPoint;
    }

    void AddPoint(Vector3 worldPoint)
    {
        if (points.Count > 0 && Vector3.Distance(points[^1], worldPoint) < 0.01f)
            return;

        points.Add(worldPoint);
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());

        colliderPoints.Add(new Vector2(worldPoint.x, worldPoint.y));
        edgeCollider.points = colliderPoints.ToArray();
    }

    public void ResetTrail()
    {
        points.Clear();
        colliderPoints.Clear();
        lineRenderer.positionCount = 0;
        edgeCollider.points = new Vector2[0];
        edgeCollider.enabled = false;
        collisionActive = false;
        lastPosition = playerTransform.position;

        // 📌 다시 시작 시 첫 점 추가
        Vector3 initialPoint = GetOffsetPoint(lastPosition, -lastDirection);
        AddPoint(initialPoint);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && collisionActive)
        {
            Debug.Log($"{other.name} 라인에 닿았습니다!");
            ResetTrail(); // 선택적으로 리셋
        }
    }
}
