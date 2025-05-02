using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void Update()
    {
        // 방향키 입력 받기 (상, 하, 좌, 우)
        float moveX = Input.GetAxis("Horizontal");  // 좌우 (A, D or Left Arrow, Right Arrow)
        float moveY = Input.GetAxis("Vertical");    // 상하 (W, S or Up Arrow, Down Arrow)

        // 방향키에 따라 빨간 네모 이동
        Vector3 move = new Vector3(moveX, moveY, 0f) * moveSpeed * Time.deltaTime;
        transform.Translate(move);
    }
}
