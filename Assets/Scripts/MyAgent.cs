using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq; // for LINQ operations like .Max()

public class MyAgent : Agent
{
    private AIPlayerController controller;
    private GameController gameManager; // GameController 참조
    private MapManager mapManager;     // MapManager 참조

    private Vector2Int[] possibleActions = new Vector2Int[]
    {
        Vector2Int.up,    // 0
        Vector2Int.right, // 1
        Vector2Int.down,  // 2
        Vector2Int.left   // 3
    };

    void Start()
    {
        if (mapManager == null)
            mapManager = MapManager.Instance;
        if (mapManager == null)
            Debug.LogError("MyAgent: Start()에서도 MapManager.Instance를 찾지 못했습니다!");
    }

    // 에피소드가 시작될 때 호출됩니다. 초기화가 필요한 경우 이곳에서 처리합니다.
    public override void OnEpisodeBegin()
    {
        // 1. 게임 맵 및 점수 초기화
        if (mapManager != null)
        {
            // MapManager가 Awkake에서 맵을 초기화하므로, 여기서는 점수만 다시 초기화
            mapManager.InitializePlayerScores();
            Debug.Log("MyAgent: MapManager.InitializePlayerScores() 호출.");
        }
        else
        {
            Debug.LogError("MyAgent: MapManager 참조가 없어 맵 초기화에 실패했습니다.");
            EndEpisode();
            return;
        }

        // 2. AI의 초기 위치 설정
        // TODO: AIPlayerController에 SetStartPosition 또는 비슷한 메서드가 필요합니다.
        // 현재 GameController에 GetRandomStartPositionForAI() 같은 메서드가 없으므로,
        // 임시로 현재 위치를 유지하거나, 고정된 위치로 설정할 수 있습니다.
        // 예시: transform.localPosition = new Vector3(0, 0, 0); // 적절한 시작 위치로 변경 필요
        // AIPlayerController가 스스로 초기 위치를 설정하도록 할 수도 있습니다.
        // Debug.Log("MyAgent: 에이전트 시작 위치를 재설정하지 않습니다. AIPlayerController에 맡기거나 수동으로 설정해야 합니다.");

        // 3. 컨트롤러의 playerID가 유효한지 확인 (보상 계산 시 사용)
        if (controller == null || controller.playerID <= 0)
        {
            Debug.LogError("MyAgent: AIPlayerController 또는 playerID가 유효하지 않습니다. 학습에 문제가 발생할 수 있습니다.");
            EndEpisode();
            return;
        }

        // 보상 초기화
        SetReward(0f);
    }

    public override void Initialize()
    {
        controller = GetComponent<AIPlayerController>();
        gameManager = GameController.Instance;

    
    }

    // 에이전트가 환경에 대해 관찰하는 데이터를 수집하는 곳입니다.
    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. 자신의 현재 위치 (로컬 좌표 사용 권장)
        sensor.AddObservation(transform.localPosition);

        // 2. AI의 현재 이동 방향 (정규화된 벡터)
        if (controller != null)
        {
            // Vector2Int는 .normalized 속성이 없으므로, X, Y 값을 개별적으로 추가합니다.
            sensor.AddObservation(controller.direction.x); // <--- 이 줄을 수정
            sensor.AddObservation(controller.direction.y); // <--- 이 줄을 추가
        }
        else
        {
            // 컨트롤러가 없으면 기본값 (예: Vector2.zero)을 추가하여 오류 방지
            sensor.AddObservation(0f); // x
            sensor.AddObservation(0f); // y
        }

        // 3. 주변 타일 상태 (MapManager로부터 가져옴)
        // 에이전트 위치를 그리드 좌표로 변환 (예시: 0.5f를 더하여 타일의 중심점 사용)
        if (mapManager != null)
        {
            int agentGridX = Mathf.RoundToInt(transform.localPosition.x);
            int agentGridY = Mathf.RoundToInt(transform.localPosition.y);

            // 주변 5x5 그리드의 타일 상태를 관측
            // (좌측 하단부터 우측 상단까지 순회)
            for (int y = -2; y <= 2; y++)
            {
                for (int x = -2; x <= 2; x++)
                {
                    Vector2Int tilePos = new Vector2Int(agentGridX + x, agentGridY + y);
                    if (mapManager.InBounds(tilePos))
                    {
                        sensor.AddObservation(mapManager.GetTile(tilePos));
                    }
                    else
                    {
                        // 맵 경계 밖이면 -1 (벽 또는 접근 불가 영역)
                        sensor.AddObservation(-1f);
                    }
                }
            }
        }
        else
        {
            // MapManager가 없으면 기본값 (5x5 = 25개의 0)
            for (int i = 0; i < 25; i++)
            {
                sensor.AddObservation(0f);
            }
        }

        // 4. 자신의 점수
        if (gameManager != null && controller != null)
        {
            sensor.AddObservation(gameManager.GetScore(controller.playerID));
        }
        else
        {
            sensor.AddObservation(0f);
        }

        // 5. 게임의 전체 타일 수 (승리 조건에 활용될 수 있음)
        // MapManager에 전체 점령 가능한 타일 수를 얻는 메서드가 필요
        // 예시: sensor.AddObservation(mapManager.GetTotalClaimableTiles());

        // TODO: 상대방(다른 플레이어)의 위치, 점수 등을 추가 관측할 수 있습니다.
        // 예를 들어, 다른 플레이어의 AIPlayerController를 찾아 위치를 추가할 수 있습니다.
    }

    // 에이전트가 선택한 행동을 기반으로 보상을 계산하거나 환경을 업데이트하는 곳입니다.
    public override void OnActionReceived(ActionBuffers actions)
    {

        // Discrete action을 방향으로 변환 (0: 상, 1: 우, 2: 하, 3: 좌)
        int action = actions.DiscreteActions[0];

        // 유효한 행동인지 확인
        if (controller != null && action >= 0 && action < possibleActions.Length)
        {
            controller.SetDirection(possibleActions[action]);
        }
        else
        {
            // 유효하지 않은 행동이거나 컨트롤러가 없으면 작은 벌칙
            AddReward(-0.01f);
            if (controller == null) Debug.LogError("MyAgent: AIPlayerController가 없어 행동을 수행할 수 없습니다.");
            else Debug.LogWarning($"MyAgent: Received invalid action index: {action}");
        }

        // --- 최소한의 보상 로직 ---
        // 1. 매 스텝마다 작은 페널티를 주어 효율적인 움직임을 유도
        AddReward(-0.01f); // 이 값은 학습 진행 상황을 보며 조정 필요

        // 2. 점령한 타일 수에 비례한 보상 (GameManager의 GetScore 사용)
        if (gameManager != null && controller != null)
        {
            float currentScore = gameManager.GetScore(controller.playerID);
            // 점령 타일 수 증가에 따른 보상
            // TODO: 이전 스텝의 점수를 저장하여 변화량에 따른 보상을 주는 것이 더 좋습니다.
            // 여기서는 단순화하여 현재 점수를 반영
            // AddReward(currentScore * 0.001f); // 점수 자체가 너무 크면 보상이 커질 수 있으니 주의

            // 타일을 점령할 때마다 보상
            // SetTile이 호출될 때 MapManager에서 점수 변동이 GameController로 전달되므로,
            // MyAgent에서는 직접적인 타일 점령 보상을 추가하지 않아도 될 수 있습니다.
            // 필요하다면 MapManager나 AIPlayerController에서 MyAgent에 직접 AddReward 호출 가능
        }

        // 3. 게임 종료 및 최종 보상/벌칙
        // TODO: 게임 종료 조건을 GameManager에 명확히 정의해야 합니다.
        // 현재 GameController에 IsGameOver() 메서드가 없습니다.
        // 여기서는 임시로 '게임이 끝나지 않았다'고 가정하고, KillPlayer 호출 시 에피소드 종료
        // 또는 특정 점수/시간 초과 시 종료 로직을 추가할 수 있습니다.

        // 예시: 게임 시간이 일정 이상 지났을 때 종료 또는 모든 타일이 점령되었을 때 종료
        // if (Time.timeSinceLevelLoad > maxGameTime || mapManager.IsMapFullyClaimed()) // MapManager에 IsMapFullyClaimed() 필요
        // {
        //     EndEpisode();
        //     return;
        // }

        // AIPlayerController가 CornerPointTracker의 PlayerID를 가지고 있다고 가정
        // AI가 죽었을 경우 (예: 다른 플레이어에게 부딪히거나)
        // GameController.KillPlayer()가 호출되었을 때 에피소드를 종료하고 큰 벌칙을 줍니다.
        // 이를 위해 AIPlayerController가 죽었을 때 MyAgent에 신호를 보내거나,
        // MyAgent가 GameController의 KillPlayer 이벤트를 구독해야 합니다.
        // 현재는 직접적인 연결이 없으므로, GetScore로 확인하는 임시 로직 추가
        if (gameManager != null && controller != null)
        {
            if (gameManager.GetScore(controller.playerID) < 0) // 점수가 -1이면 죽은 것으로 간주 (KillPlayer의 로직)
            {
                SetReward(-5.0f); // 사망 시 큰 벌칙
                EndEpisode();
                Debug.Log($"MyAgent({controller.playerID}): 사망하여 에피소드 종료. 최종 보상: {GetCumulativeReward()}");
                return;
            }

            // TODO: 승리 조건 정의 및 보상
            // 예를 들어, 일정 점수 이상 획득 시 승리
            if (gameManager.GetScore(controller.playerID) >= 500) // 임시 승리 점수
            {
                SetReward(5.0f); // 승리 시 큰 보상
                EndEpisode();
                Debug.Log($"MyAgent({controller.playerID}): 승리하여 에피소드 종료. 최종 보상: {GetCumulativeReward()}");
                return;
            }

            // TODO: 다른 플레이어 점수와 비교하여 승패 판단
            // 모든 플레이어의 점수를 가져와서 비교해야 합니다.
            // 예시: 모든 플레이어 ID를 알아야 함
            // var allPlayerIDs = gameManager.GetAllPlayerIDs();
            // var maxScore = allPlayerIDs.Max(id => gameManager.GetScore(id));
            // if (gameManager.GetScore(controller.playerID) == maxScore && !mapManager.IsMapFullyClaimed())
            // {
            //     // 현재는 승리가 아니지만, 나중에 승리 조건을 추가할 수 있음
            // }
        }
    }

    // 수동으로 행동을 테스트하고 싶을 때 정의할 수 있습니다.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut.Clear(); // 이전 행동을 지웁니다.

        // WASD 키로 에이전트 수동 제어
        if (Input.GetKey(KeyCode.W)) // 위
        {
            discreteActionsOut[0] = 0;
        }
        else if (Input.GetKey(KeyCode.D)) // 오른쪽
        {
            discreteActionsOut[0] = 1;
        }
        else if (Input.GetKey(KeyCode.S)) // 아래
        {
            discreteActionsOut[0] = 2;
        }
        else if (Input.GetKey(KeyCode.A)) // 왼쪽
        {
            discreteActionsOut[0] = 3;
        }
        // 키를 누르지 않으면 행동 없음 (ML-Agents가 자동으로 처리하거나, 정지 행동을 추가할 수 있음)
    }
}