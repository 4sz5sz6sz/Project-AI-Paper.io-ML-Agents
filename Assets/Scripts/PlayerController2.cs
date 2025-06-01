using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController2 : BasePlayerController
{
    protected override void HandleInput()
    {
        Vector2Int input = Vector2Int.zero;

        // Player 2 전용 입력키 (IJKL)
        if (Keyboard.current.iKey.isPressed)
            input = Vector2Int.up;
        else if (Keyboard.current.kKey.isPressed)
            input = Vector2Int.down;
        else if (Keyboard.current.jKey.isPressed)
            input = Vector2Int.left;
        else if (Keyboard.current.lKey.isPressed)
            input = Vector2Int.right;

        if (input != Vector2Int.zero && input != -direction)
            queuedDirection = input;
    }

    protected override void HandleMovement()
    {
        HandleInput();

        if (!isMoving && queuedDirection != Vector2Int.zero && queuedDirection != -direction)
        {
            if (direction != Vector2Int.zero && queuedDirection != direction && !wasInsideOwnedArea)
            {
                cornerTracker?.AddCorner(gridPosition);
                Debug.Log($"[P2] 현재 코너 점 개수: {cornerTracker.GetPoints().Count}");
            }

            direction = queuedDirection;
            gridPosition += direction;
            targetPosition = new Vector3(gridPosition.x, gridPosition.y, -10f);
            isMoving = true;

            if (trail != null && !trail.trailActive && !wasInsideOwnedArea)
                trail.trailActive = true;
        }

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;

                int currentTile = mapManager.GetTile(gridPosition);
                bool isInsideOwnedArea = currentTile == cornerTracker.playerId;

                if (wasInsideOwnedArea && !isInsideOwnedArea)
                {
                    Debug.Log("📌 [P2] 내 영역을 벗어남 - 점 추가");
                    cornerTracker?.AddCorner(gridPosition);
                    trail.trailActive = true;
                }

                if (!wasInsideOwnedArea && isInsideOwnedArea)
                {
                    Debug.Log("📌 [P2] 내 영역 안으로 들어옴 - 코너 추가 및 폐곡선 검사");
                    cornerTracker?.AddCorner(gridPosition);
                    loopDetector?.CheckLoop(cornerTracker);
                    cornerTracker.DisplayCornersFor1Second();
                    trail?.ResetTrail();
                    trail.trailActive = false;
                }

                wasInsideOwnedArea = isInsideOwnedArea;
            }
        }
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        CheckTrailCollision(other);
    }
}
