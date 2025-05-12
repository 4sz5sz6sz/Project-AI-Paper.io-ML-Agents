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

    void Start()
    {
        gridPosition = Vector2Int.RoundToInt(transform.position);
        transform.position = (Vector2)gridPosition;
        targetPosition = transform.position;

        trail = FindObjectOfType<LineTrailWithCollision>();
    }

    void Update()
    {
        HandleInput();

        // 이동 중이 아닐 때만 방향 전환 및 이동 시작
        if (!isMoving && queuedDirection != Vector2Int.zero && queuedDirection != -direction)
        {
            direction = queuedDirection;

            gridPosition += direction;
            targetPosition = new Vector3(gridPosition.x, gridPosition.y, 0f);
            isMoving = true;

            if (trail != null && !trail.trailActive)
                trail.trailActive = true;
        }

        // 이동 중일 때 MoveTowards
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
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            queuedDirection = Vector2Int.up;
        else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            queuedDirection = Vector2Int.down;
        else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            queuedDirection = Vector2Int.left;
        else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            queuedDirection = Vector2Int.right;
    }
    
}