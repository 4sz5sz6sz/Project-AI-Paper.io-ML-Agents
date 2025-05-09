using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;

    //가장 마지막으로 입력된 방향 저장.    
    private Vector2 currentDirection = Vector2.zero;


    void Update()
    {
        if(Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)){
            currentDirection = Vector2.up;
        }
        else if(Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)){
            currentDirection = Vector2.down;
        }
        else if(Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)){
            currentDirection = Vector2.left;
        }
        else if(Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)){
            currentDirection = Vector2.right;
        }

        // 방향키에 따라 빨간 네모 이동
        Vector3 move = new Vector3(currentDirection.x, currentDirection.y, 0f) * moveSpeed * Time.deltaTime;
        transform.Translate(move);
    }
}
