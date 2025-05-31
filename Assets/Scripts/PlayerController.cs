// PlayerController.cs
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : BasePlayerController
{
    protected override void HandleInput()
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

    protected override void HandleMovement()
    {
        HandleInput();

        // 방향이 바뀔 때만 코너 저장
        if (!isMoving && queuedDirection != Vector2Int.zero && queuedDirection != -direction)
        {
            // 수정된 부분: 내 영역 밖에 있을 때만 코너 저장
            if (direction != Vector2Int.zero && queuedDirection != direction && !wasInsideOwnedArea)
            {
                cornerTracker?.AddCorner(gridPosition);
                Debug.Log($"현재 코너 점 개수: {cornerTracker.GetPoints().Count}");
            }

            direction = queuedDirection;
            gridPosition += direction;
            targetPosition = new Vector3(gridPosition.x, gridPosition.y, -10f);
            isMoving = true;

            // 내 영역 밖에 있을 때만 궤적 활성화
            if (trail != null && !trail.trailActive && !wasInsideOwnedArea)
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

                // 내 영역 밖으로 나갈 때 점 추가
                if (wasInsideOwnedArea && !isInsideOwnedArea)
                {
                    Debug.Log("📌 내 영역을 벗어남 - 점 추가");
                    cornerTracker?.AddCorner(gridPosition);
                    trail.trailActive = true;
                }

                // 내 영역 안으로 들어올 때 코너 추가 및 폐곡선 검사
                if (!wasInsideOwnedArea && isInsideOwnedArea)
                {
                    Debug.Log("📌 내 영역 안으로 들어옴 - 코너 추가 및 폐곡선 검사");
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
}
