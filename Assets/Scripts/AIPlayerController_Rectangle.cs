using UnityEngine;

public class AIPlayerController_Rectangle : BasePlayerController
{
    private enum AIState
    {
        MovingRight,
        MovingUp,
        MovingLeft,
        MovingDown,
        Returning
    }

    private AIState currentState = AIState.MovingRight;
    private int moveSteps = 0;
    private const int HORIZONTAL_STEPS = 10;  // 가로 이동 거리
    private const int VERTICAL_STEPS = 8;     // 세로 이동 거리
    private const int WALL_MARGIN = 3;        // 벽과의 최소 거리

    protected override void HandleInput()
    {
        Vector2Int input = DecideNextMove();
        // 90도 회전 제약 검사
        if (input != Vector2Int.zero && IsValidTurn(input))
        {
            queuedDirection = input;
        }

        Debug.Log($"현재 상태: {currentState}, 이동 스텝: {moveSteps}, 방향: {queuedDirection}");
    }

    private bool IsValidTurn(Vector2Int newDirection)
    {
        // 현재 방향과 같으면 허용
        if (newDirection == direction) return true;
        
        // 반대 방향이면 불허
        if (newDirection == -direction) return false;
        
        // 90도 회전만 허용
        return Vector2.Dot(new Vector2(direction.x, direction.y), 
                          new Vector2(newDirection.x, newDirection.y)) == 0;
    }

    private bool IsSafePosition(Vector2Int pos)
    {
        // 맵 경계 체크
        if (pos.x <= WALL_MARGIN || pos.x >= mapManager.width - WALL_MARGIN ||
            pos.y <= WALL_MARGIN || pos.y >= mapManager.height - WALL_MARGIN)
        {
            return false;
        }
        return true;
    }

    private bool IsNearTopWall(Vector2Int pos) => pos.y >= mapManager.height - WALL_MARGIN;
    private bool IsNearBottomWall(Vector2Int pos) => pos.y <= WALL_MARGIN;
    private bool IsNearLeftWall(Vector2Int pos) => pos.x <= WALL_MARGIN;
    private bool IsNearRightWall(Vector2Int pos) => pos.x >= mapManager.width - WALL_MARGIN;

    private Vector2Int GetSafeDirection()
    {
        Vector2Int nextPos = gridPosition + direction;

        // 현재 방향이 위험한지 확인
        bool isDangerous = false;

        if (direction == Vector2Int.up && IsNearTopWall(nextPos))
            isDangerous = true;
        else if (direction == Vector2Int.down && IsNearBottomWall(nextPos))
            isDangerous = true;
        else if (direction == Vector2Int.left && IsNearLeftWall(nextPos))
            isDangerous = true;
        else if (direction == Vector2Int.right && IsNearRightWall(nextPos))
            isDangerous = true;

        if (!isDangerous)
            return direction;  // 현재 방향이 안전하면 유지

        // 벽 근처에서 안전한 방향 선택
        if (IsNearTopWall(nextPos))
        {
            return IsNearRightWall(nextPos) ? Vector2Int.left : Vector2Int.right;
        }
        if (IsNearBottomWall(nextPos))
        {
            return IsNearLeftWall(nextPos) ? Vector2Int.right : Vector2Int.left;
        }
        if (IsNearLeftWall(nextPos))
        {
            return IsNearTopWall(nextPos) ? Vector2Int.down : Vector2Int.up;
        }
        if (IsNearRightWall(nextPos))
        {
            return IsNearBottomWall(nextPos) ? Vector2Int.up : Vector2Int.down;
        }

        return direction;
    }

    private Vector2Int DecideNextMove()
    {
        // 안전한 방향 확인
        Vector2Int safeDir = GetSafeDirection();
        if (safeDir != direction)
        {
            // 방향 전환이 필요한 경우
            currentState = GetNewState(safeDir);
            moveSteps = 0;
            return safeDir;
        }

        switch (currentState)
        {
            case AIState.MovingRight:
                if (moveSteps >= HORIZONTAL_STEPS)
                {
                    currentState = AIState.MovingUp;
                    moveSteps = 0;
                    return Vector2Int.up;
                }
                moveSteps++;
                return Vector2Int.right;

            case AIState.MovingUp:
                if (moveSteps >= VERTICAL_STEPS || !IsSafePosition(gridPosition + Vector2Int.up))
                {
                    currentState = AIState.MovingLeft;
                    moveSteps = 0;
                    return Vector2Int.left;  // 90도 회전: 위 -> 왼쪽
                }
                moveSteps++;
                return Vector2Int.up;

            case AIState.MovingLeft:
                if (moveSteps >= HORIZONTAL_STEPS || !IsSafePosition(gridPosition + Vector2Int.left))
                {
                    currentState = AIState.MovingDown;
                    moveSteps = 0;
                    return Vector2Int.down;  // 90도 회전: 왼쪽 -> 아래
                }
                moveSteps++;
                return Vector2Int.left;

            case AIState.MovingDown:
                if (moveSteps >= VERTICAL_STEPS || !IsSafePosition(gridPosition + Vector2Int.down))
                {
                    currentState = AIState.Returning;
                    moveSteps = 0;
                    return GetSafeReturnDirection();  // 귀환 모드로 전환
                }
                moveSteps++;
                return Vector2Int.down;

            case AIState.Returning:
                if (wasInsideOwnedArea)
                {
                    currentState = AIState.MovingRight;  // 새로운 사각형 시작
                    moveSteps = 0;
                    return Vector2Int.right;
                }
                return GetSafeReturnDirection();
        }

        return Vector2Int.zero;
    }

    private AIState GetNewState(Vector2Int newDir)
    {
        if (newDir == Vector2Int.right) return AIState.MovingRight;
        if (newDir == Vector2Int.up) return AIState.MovingUp;
        if (newDir == Vector2Int.left) return AIState.MovingLeft;
        if (newDir == Vector2Int.down) return AIState.MovingDown;
        return currentState;
    }

    private Vector2Int GetSafeReturnDirection()
    {
        // 현재 방향에서 90도로만 회전하며 안전한 귀환 경로 찾기
        Vector2Int[] possibleTurns = GetValidTurns(direction);
        
        foreach (var dir in possibleTurns)
        {
            Vector2Int newPos = gridPosition + dir;
            if (IsSafePosition(newPos) && mapManager.GetTile(newPos) == cornerTracker.playerId)
            {
                return dir;
            }
        }

        // 안전한 90도 회전 방향 찾기
        foreach (var dir in possibleTurns)
        {
            if (IsSafePosition(gridPosition + dir))
            {
                return dir;
            }
        }

        return direction; // 안전한 방향이 없으면 현재 방향 유지
    }

    private Vector2Int[] GetValidTurns(Vector2Int currentDir)
    {
        if (currentDir == Vector2Int.right || currentDir == Vector2Int.left)
            return new[] { Vector2Int.up, Vector2Int.down };
        else
            return new[] { Vector2Int.right, Vector2Int.left };
    }
}