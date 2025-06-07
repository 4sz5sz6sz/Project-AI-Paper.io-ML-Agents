using UnityEngine;

public class AIPlayerController : BasePlayerController
{
    private MyAgent agent;
    // 플레이어 ID를 추가 (Inspector에서 설정하거나 코드에서 할당)
    [Tooltip("이 AI 플레이어의 고유 ID (GameController와 MapManager에서 사용)")]
    public int playerID; // <--- 이 줄을 추가합니다.

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

    // **🚨 NEW: 적 궤적을 끊었을 때 MyAgent에게 알림**
    public void NotifyEnemyKill(int killedPlayerId)
    {
        if (agent != null)
        {
            agent.NotifyEnemyKilled(killedPlayerId);
        }
    }
}