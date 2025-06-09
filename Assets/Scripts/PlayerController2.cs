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

                // 맵 경계 체크 - 경계를 벗어나면 사망
                if (!mapManager.InBounds(gridPosition))
                {
                    if (GameController.Instance != null)
                    {
                        GameController.Instance.KillPlayer(cornerTracker.playerId);
                    }
                    return; // 사망 처리 후 더 이상 진행하지 않음
                }
                int currentTile = mapManager.GetTile(gridPosition);
                bool isInsideOwnedArea = currentTile == cornerTracker.playerId;

                // 항상 궤적 충돌 체크 (내 영역 안에서도 상대방 궤적을 끊을 수 있음)
                int existingTrail = mapManager.GetTrail(gridPosition);
                if (existingTrail > 0)
                {
                    // 궤적을 밟으면 해당 궤적의 주인이 죽음
                    if (GameController.Instance != null)
                    {
                        GameController.Instance.KillPlayer(existingTrail);
                    }
                    // 궤적을 끊었으므로 해당 위치의 궤적 제거
                    mapManager.SetTrail(gridPosition, 0);
                }

                // 내 영역 밖에 있을 때만 자신의 궤적 설정
                if (!isInsideOwnedArea)
                {
                    mapManager.SetTrail(gridPosition, cornerTracker.playerId);
                }

                // 내 영역 밖으로 나갈 때 점 추가
                if (wasInsideOwnedArea && !isInsideOwnedArea)
                {
                    Vector2Int previousPos = gridPosition - direction; // 이전 위치 (내 땅)
                    cornerTracker?.AddCorner(previousPos);            // 이전 점 추가
                    cornerTracker?.AddCorner(gridPosition);
                    if (trail != null) trail.trailActive = true;
                }

                // 내 영역 안으로 들어올 때 코너 추가 및 폐곡선 검사
                if (!wasInsideOwnedArea && isInsideOwnedArea)
                {
                    cornerTracker?.AddCorner(gridPosition);
                    loopDetector?.CheckLoop(cornerTracker);
                    trail?.ResetTrail();
                    if (trail != null) trail.trailActive = false;

                    // 내 영역으로 들어올 때 내 궤적 제거
                    mapManager.ClearPlayerTrails(cornerTracker.playerId);
                }

                wasInsideOwnedArea = isInsideOwnedArea;
            }
        }
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        // 궤적 충돌 시스템이 MapManager 기반으로 변경되어 
        // 이제 OnTriggerEnter2D는 사용하지 않습니다.
        // 충돌 감지는 HandleMovement()에서 처리됩니다.
    }
}
