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
    void OnTriggerEnter2D(Collider2D other)
    {
        // 궤적 충돌 시스템이 MapManager 기반으로 변경되어 
        // 이제 OnTriggerEnter2D는 사용하지 않습니다.
        // 충돌 감지는 HandleMovement()에서 처리됩니다.
    }
}
