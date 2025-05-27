// PlayerController.cs
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 100f;

    private Vector2Int gridPosition;
    private Vector2Int direction = Vector2Int.zero;
    private Vector2Int queuedDirection = Vector2Int.zero;

    private bool isMoving = false;
    private Vector3 targetPosition;

    private LineTrailWithCollision trail;
    private CornerPointTracker cornerTracker;
    private LoopDetector loopDetector;
    private MapManager mapManager;
    private bool wasInsideOwnedArea = false;

    void Start()
    {
        gridPosition = Vector2Int.RoundToInt(transform.position);
        transform.position = (Vector2)gridPosition;
        targetPosition = transform.position;

        // 컴포넌트 초기화,  FindFirstObjectByType로 바꿈. 나중에 플레이어 2명 이상이면 문제 있을수도..
        trail = FindFirstObjectByType<LineTrailWithCollision>();
        if (cornerTracker == null)
            cornerTracker = GetComponent<CornerPointTracker>();
        if (loopDetector == null)
            loopDetector = FindFirstObjectByType<LoopDetector>();
        if (mapManager == null)
            mapManager = FindFirstObjectByType<MapManager>();

        wasInsideOwnedArea = mapManager.GetTile(gridPosition) == cornerTracker.playerId;
    }

    void Update()
    {
        HandleInput();

        // 방향이 바뀔 때만 코너 저장
        if (!isMoving && queuedDirection != Vector2Int.zero && queuedDirection != -direction)
        {
            if (direction != Vector2Int.zero && queuedDirection != direction)
            {
                cornerTracker?.AddCorner(gridPosition);
                Debug.Log($"현재 코너 점 개수: {cornerTracker.GetPoints().Count}");
            }

            direction = queuedDirection;
            gridPosition += direction;
            targetPosition = new Vector3(gridPosition.x, gridPosition.y, -10f);
            isMoving = true;

            if (trail != null && !trail.trailActive)
                trail.trailActive = true;
        }

        // 이동 처리
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;

                int currentTile = mapManager.GetTile(gridPosition);
                bool isInsideOwnedArea = currentTile == cornerTracker.playerId;

                // ✅ 내 영역 밖으로 나갈 때 점 추가
                if (wasInsideOwnedArea && !isInsideOwnedArea)
                {
                    Debug.Log("📌 내 영역을 벗어남 - 점 추가");
                    cornerTracker?.AddCorner(gridPosition);
                }

                // ✅ 내 영역 안으로 들어올 때 코너 추가 및 폐곡선 검사
                if (!wasInsideOwnedArea && isInsideOwnedArea)
                {
                    Debug.Log("📌 내 영역 안으로 들어옴 - 코너 추가 및 폐곡선 검사");
                    cornerTracker?.AddCorner(gridPosition);
                    loopDetector?.CheckLoop(cornerTracker);
                    cornerTracker.DisplayCornersFor1Second();
                    trail?.ResetTrail(); // 궤적 초기화
                    trail.trailActive = false; // 궤적 그리기 비활성화
                }
                // ✅ 영역 상태 업데이트
                wasInsideOwnedArea = isInsideOwnedArea;
            }
        }
    }

    void HandleInput()
    {
        Vector2Int input = Vector2Int.zero;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            input = Vector2Int.up;
        else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            input = Vector2Int.down;
        else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            input = Vector2Int.left;
        else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            input = Vector2Int.right;

        if (input != Vector2Int.zero && input != -direction)
            queuedDirection = input;
    }
}
