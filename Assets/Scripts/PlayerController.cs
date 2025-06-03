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

    // HandleMovement() 오버라이드 제거: 부모의 공통 이동 로직 사용

    void OnTriggerEnter2D(Collider2D other)
    {
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
