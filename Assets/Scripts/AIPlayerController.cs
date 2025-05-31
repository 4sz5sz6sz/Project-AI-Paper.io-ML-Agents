using UnityEngine;

public class AIPlayerController : BasePlayerController
{
    private MyAgent agent;

    protected override void Start()
    {
        base.Start();
        agent = GetComponent<MyAgent>();
    }

    protected override void HandleInput()
    {
        // MyAgent에서 받은 행동을 queuedDirection에 설정
        // 이 부분은 MyAgent의 OnActionReceived에서 설정됨
    }

    public void SetDirection(Vector2Int newDirection)
    {
        if (newDirection != Vector2Int.zero && newDirection != -direction)
            queuedDirection = newDirection;
    }
}