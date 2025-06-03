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

    // // HandleMovement() 오버라이드 제거: 부모의 공통 이동 로직 사용
    // protected override void HandleMovement()
    // {
    //     HandleInput();

    //     // 방향이 바뀔 때만 코너 저장
    //     if (!isMoving && queuedDirection != Vector2Int.zero && queuedDirection != -direction)
    //     {
    //         // 수정된 부분: 내 영역 밖에 있을 때만 코너 저장
    //         if (direction != Vector2Int.zero && queuedDirection != direction && !wasInsideOwnedArea)
    //         {
    //             cornerTracker?.AddCorner(gridPosition);
    //             Debug.Log($"현재 코너 점 개수: {cornerTracker.GetPoints().Count}");
    //         }

    //         direction = queuedDirection;
    //         gridPosition += direction;
    //         targetPosition = new Vector3(gridPosition.x, gridPosition.y, -2f);
    //         isMoving = true;

    //         // 내 영역 밖에 있을 때만 궤적 활성화
    //         if (trail != null && !trail.trailActive && !wasInsideOwnedArea)
    //             trail.trailActive = true;
    //     }

    //     // 이동 처리
    //     if (isMoving)
    //     {
    //         transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

    //         if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
    //         {
    //             transform.position = targetPosition;
    //             isMoving = false;

    //             int currentTile = mapManager.GetTile(gridPosition);
    //             bool isInsideOwnedArea = currentTile == cornerTracker.playerId;

    //             // 내 영역 밖으로 나갈 때 두 점 추가
    //             if (wasInsideOwnedArea && !isInsideOwnedArea)
    //             {
    //                 Debug.Log("📌 내 영역을 벗어남 - 이전 점과 현재 점 추가");
    //                 Vector2Int previousPos = gridPosition - direction; // 이전 위치 (내 땅)
    //                 cornerTracker?.AddCorner(previousPos);            // 이전 점 추가
    //                 cornerTracker?.AddCorner(gridPosition);           // 현재 점 추가
    //                 Debug.Log($"추가된 점들: 이전=({previousPos.x}, {previousPos.y}), 현재=({gridPosition.x}, {gridPosition.y})");
    //                 trail.trailActive = true;
    //             }

    //             // 내 영역 안으로 들어올 때 코너 추가 및 폐곡선 검사
    //             if (!wasInsideOwnedArea && isInsideOwnedArea)
    //             {
    //                 Debug.Log("📌 내 영역 안으로 들어옴 - 코너 추가 및 폐곡선 검사");
    //                 cornerTracker?.AddCorner(gridPosition);
    //                 loopDetector?.CheckLoop(cornerTracker);
    //                 // cornerTracker.DisplayCornersFor1Second();
    //                 trail?.ResetTrail();
    //                 trail.trailActive = false;
    //             }

    //             wasInsideOwnedArea = isInsideOwnedArea;
    //         }
    //     }
    // }

    void OnTriggerEnter2D(Collider2D other)
    {
        float distance = Vector3.Distance(transform.position, other.transform.position);
        if (distance > 1f) return; // 너무 멀리 떨어진 오브젝트는 무시

        Debug.Log("✅ 트리거 작동함!");

        // 충돌된 오브젝트 이름 출력
        Debug.Log($"충돌된 오브젝트 이름: {other.gameObject.name}");

        // 태그 검사
        if (other.CompareTag("Player"))
        {
            Debug.Log("🎯 Player 태그를 가진 오브젝트와 충돌함!");
        }

        // LineTrailWithCollision 컴포넌트가 있는지 확인
        var trail = other.GetComponent<LineTrailWithCollision>();
        if (trail != null)
        {
            Debug.Log($"📏 충돌한 오브젝트에 LineTrail 있음. OwnerId: {trail.cornerTracker?.playerId}");
        }
        else
        {
            Debug.Log("❌ 충돌한 오브젝트에는 LineTrailWithCollision이 없음");
        }

        // 실제 충돌 로직 실행
        CheckTrailCollision(other);
    }
}
