using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Vector2 currentDirection = Vector2.zero;
    private Vector2 inputDirection = Vector2.zero;

    private LineTrailWithCollision trail; // 연결될 궤적 스크립트 참조

    void Start()
    {
        // 궤적 스크립트 찾기 (씬에 하나만 있을 경우)
        trail = FindObjectOfType<LineTrailWithCollision>();
    }

    void Update()
    {
        // 입력 받기
        if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            inputDirection = Vector2.up;
        else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
            inputDirection = Vector2.down;
        else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            inputDirection = Vector2.left;
        else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            inputDirection = Vector2.right;

        // 🟢 궤적 활성화: 키 처음 눌렀을 때 한 번만 true로
        if (trail != null && !trail.trailActive && inputDirection != Vector2.zero)
        {
            trail.trailActive = true;
        }

        // 반대 방향 방지
        if (inputDirection != -currentDirection && inputDirection != Vector2.zero)
        {
            currentDirection = inputDirection;
        }
        else if (inputDirection == Vector2.zero)
        {
            currentDirection = inputDirection;
        }

        // 이동
        Vector3 move = new Vector3(currentDirection.x, currentDirection.y, 0f) * moveSpeed * Time.deltaTime;
        transform.Translate(move);
    }
}