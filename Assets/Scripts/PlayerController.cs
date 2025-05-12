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

    void Start()
    {
        gridPosition = Vector2Int.RoundToInt(transform.position);
        transform.position = (Vector2)gridPosition;
        targetPosition = transform.position;

        trail = FindObjectOfType<LineTrailWithCollision>();
        cornerTracker = GetComponent<CornerPointTracker>();
    }

    void Update()
    {
        HandleInput();

        if (!isMoving && queuedDirection != Vector2Int.zero && queuedDirection != -direction)
        {
            // ✅ 꺾임 체크 및 코너 저장
            if (direction != Vector2Int.zero && queuedDirection != direction)
            {
                if (cornerTracker != null)
                {
                    cornerTracker.AddCorner(gridPosition);
                }
            }

            direction = queuedDirection;

            gridPosition += direction;
            targetPosition = new Vector3(gridPosition.x, gridPosition.y, 0f);
            isMoving = true;

            if (trail != null && !trail.trailActive)
                trail.trailActive = true;
        }

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
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
