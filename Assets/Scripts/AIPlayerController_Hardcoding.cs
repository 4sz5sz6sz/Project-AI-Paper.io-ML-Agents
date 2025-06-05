using UnityEngine;

public class AIPlayerController_Hardcoding : BasePlayerController
{
    private enum AIState
    {
        Expanding,      // 영역 확장
        Returning,      // 안전 귀환
        Avoiding       // 위험 회피
    }

    private AIState currentState = AIState.Expanding;
    private int expandSteps = 0;
    private const int MAX_EXPAND_STEPS = 5;  // 최대 확장 거리

    protected override void HandleInput()
    {
        // 매 프레임마다 상태 체크 및 방향 결정
        Vector2Int input = DecideNextMove();
        
        if (input != Vector2Int.zero && input != -direction)
            queuedDirection = input;
    }

    private Vector2Int DecideNextMove()
    {
        // 1. 다른 플레이어나 궤적이 근처에 있는지 확인
        if (IsEnemyNearby())
        {
            currentState = AIState.Avoiding;
            return GetSafeDirection();
        }

        switch (currentState)
        {
            case AIState.Expanding:
                if (expandSteps >= MAX_EXPAND_STEPS)
                {
                    currentState = AIState.Returning;
                    expandSteps = 0;
                    return GetDirectionToOwnedArea();
                }
                expandSteps++;
                return GetExpandingDirection();

            case AIState.Returning:
                if (wasInsideOwnedArea)
                {
                    currentState = AIState.Expanding;
                    return GetExpandingDirection();
                }
                return GetDirectionToOwnedArea();

            case AIState.Avoiding:
                currentState = AIState.Returning;
                return GetSafeDirection();

            default:
                return Vector2Int.zero;
        }
    }

    private bool IsEnemyNearby()
    {
        // 주변 4방향 검사
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        foreach (var dir in directions)
        {
            Vector2Int checkPos = gridPosition + dir;
            Collider2D[] hits = Physics2D.OverlapCircleAll(new Vector2(checkPos.x, checkPos.y), 1f);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player") && hit.gameObject != gameObject)
                    return true;
            }
        }
        return false;
    }

    private Vector2Int GetExpandingDirection()
    {
        // 시계방향으로 확장
        if (direction == Vector2Int.up) return Vector2Int.right;
        if (direction == Vector2Int.right) return Vector2Int.down;
        if (direction == Vector2Int.down) return Vector2Int.left;
        if (direction == Vector2Int.left) return Vector2Int.up;
        return Vector2Int.right;  // 초기 방향
    }

    private Vector2Int GetDirectionToOwnedArea()
    {
        // 자신의 영역 방향으로 이동
        Vector2Int bestDir = Vector2Int.zero;
        float minDistance = float.MaxValue;

        foreach (var dir in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
        {
            Vector2Int newPos = gridPosition + dir;
            if (mapManager.GetTile(newPos) == cornerTracker.playerId)
            {
                float dist = Vector2.Distance(transform.position, new Vector2(newPos.x, newPos.y));
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestDir = dir;
                }
            }
        }

        return bestDir != Vector2Int.zero ? bestDir : GetSafeDirection();
    }

    private Vector2Int GetSafeDirection()
    {
        // 충돌이나 위험이 없는 방향 선택
        foreach (var dir in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
        {
            if (dir != -direction && IsSafeDirection(dir))
                return dir;
        }
        return direction;  // 안전한 방향이 없으면 현재 방향 유지
    }

    private bool IsSafeDirection(Vector2Int dir)
    {
        Vector2Int newPos = gridPosition + dir;
        return !Physics2D.OverlapCircle(new Vector2(newPos.x, newPos.y), 0.5f);
    }
}