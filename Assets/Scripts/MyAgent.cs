using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;

public class MyAgent : Agent
{
    private AIPlayerController controller;
    private GameController gameManager;
    private MapManager mapManager;

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
    public override void Initialize()
    {
        controller = GetComponent<AIPlayerController>();
        gameManager = GameController.Instance;

        Debug.Log("[MyAgent] Initialize 완료 - 계층적 관찰 시스템 (85차원 벡터)");
    }
    public override void OnEpisodeBegin()
    {
        Debug.Log($"[MyAgent] Player {controller?.playerID} 에피소드 시작");

        if (mapManager == null)
        {
            mapManager = MapManager.Instance;
        }

        if (controller == null || controller.playerID <= 0)
        {
            Debug.LogError("MyAgent: AIPlayerController 또는 playerID가 유효하지 않습니다.");
            EndEpisode();
            return;
        }

        // 에이전트 재스폰 위치 설정
        Vector2Int spawnPos = new Vector2Int(
            controller.playerID == 2 ? 45 : 5,  // AI는 보통 player 2
            controller.playerID == 2 ? 20 : 5
        );

        // 완전 재스폰 실행 (영토, 위치, 상태 모두 초기화)
        if (controller != null)
        {
            controller.FullRespawn(spawnPos);
        }

        // 사망 상태 리셋
        isDead = false;

        // 보상 초기화
        SetReward(0f);

        // 추가적인 상태 안정화를 위한 지연 후 확인
        Invoke(nameof(VerifyRespawnState), 0.2f);

        Debug.Log($"[MyAgent] Player {controller.playerID} 완전 재스폰 완료 - 위치: {spawnPos}");
    }

    private void VerifyRespawnState()
    {
        // 재스폰 후 상태 검증
        if (controller != null && gameManager != null)
        {
            int currentScore = gameManager.GetScore(controller.playerID);
            Debug.Log($"[MyAgent] 재스폰 후 상태 검증 - Player {controller.playerID} 점수: {currentScore}");

            if (currentScore <= 0)
            {
                Debug.LogWarning($"[MyAgent] Player {controller.playerID} 재스폰 후에도 점수가 {currentScore}입니다. 강제 초기화 시도...");

                // 강제로 점수 재설정
                if (mapManager != null)
                {
                    int initialScore = 10 * 10; // INITIAL_TERRITORY_SIZE * INITIAL_TERRITORY_SIZE
                    gameManager.SetScore(controller.playerID, initialScore);
                    Debug.Log($"[MyAgent] Player {controller.playerID} 점수를 {initialScore}로 강제 설정");
                }
            }
        }
    }    // 확장된 계층적 관찰: 85차원 (Unity Inspector 설정과 일치)
    public override void CollectObservations(VectorSensor sensor)
    {
        if (controller == null || mapManager == null)
        {
            // 기본값으로 채워서 관찰 차원 맞추기
            for (int i = 0; i < 85; i++) sensor.AddObservation(0f); // 총 85차원
            return;
        }

        int agentGridX = Mathf.RoundToInt(transform.localPosition.x);
        int agentGridY = Mathf.RoundToInt(transform.localPosition.y);
        int myPlayerID = controller.playerID;

        // 1. 기본 정보 (5차원)
        sensor.AddObservation(agentGridX / 50f);      // 정규화된 X 위치
        sensor.AddObservation(agentGridY / 25f);      // 정규화된 Y 위치
        sensor.AddObservation(controller.direction.x); // 이동 방향 X
        sensor.AddObservation(controller.direction.y); // 이동 방향 Y

        // 현재 점수 (정규화)
        float currentScore = gameManager?.GetScore(myPlayerID) ?? 0f;
        sensor.AddObservation(currentScore / 1250f); // 맵 전체 타일 수로 정규화

        // 2. 로컬 5x5 관찰 (25차원) - 즉시 위험/기회
        for (int y = -2; y <= 2; y++)
        {
            for (int x = -2; x <= 2; x++)
            {
                Vector2Int tilePos = new Vector2Int(agentGridX + x, agentGridY + y);
                if (mapManager.InBounds(tilePos))
                {
                    int tileOwner = mapManager.GetTile(tilePos);
                    // 상대방 영역(-1), 중립(0), 내 영역(1)로 정규화
                    float normalizedTile = (tileOwner == myPlayerID) ? 1f :
                                         (tileOwner == 0) ? 0f : -1f;
                    sensor.AddObservation(normalizedTile);
                }
                else
                {
                    sensor.AddObservation(-1f); // 경계 밖
                }
            }
        }

        // 3. 추가 정보 (1차원) - 위험도
        float dangerLevel = CalculateLocalDanger(agentGridX, agentGridY, myPlayerID);
        sensor.AddObservation(dangerLevel);

        // 4. 전략적 관찰 (39차원) - 방향별 기회 + 경계 거리 + 영역 정보
        AddStrategicObservations(sensor, agentGridX, agentGridY, myPlayerID);

        // 5. 상대방 정보 (15차원) - 최대 3명 상대방
        AddOpponentObservations(sensor, agentGridX, agentGridY, myPlayerID);

        Debug.Log($"[MyAgent] 관찰 완료 - 총 85차원 (5기본 + 25로컬 + 1위험 + 39전략 + 15상대방)");
    }

    private float CalculateLocalDanger(int myX, int myY, int myPlayerID)
    {
        // 주변 5x5 영역에서 내 궤적의 위험도 계산
        float danger = 0f;
        int dangerCount = 0;

        for (int y = -2; y <= 2; y++)
        {
            for (int x = -2; x <= 2; x++)
            {
                Vector2Int checkPos = new Vector2Int(myX + x, myY + y);
                if (mapManager.InBounds(checkPos))
                {
                    int trail = mapManager.GetTrail(checkPos);
                    if (trail == myPlayerID)
                    {
                        // 내 궤적이 가까이 있으면 위험도 증가
                        float distance = Mathf.Sqrt(x * x + y * y);
                        danger += 1f / (distance + 0.1f); // 거리 역수
                        dangerCount++;
                    }
                }
            }
        }        return dangerCount > 0 ? Mathf.Clamp01(danger / 10f) : 0f;
    }

    private void AddStrategicObservations(VectorSensor sensor, int myX, int myY, int myPlayerID)
    {
        // 방향별 기회 분석 (상/하/좌/우/대각선 8방향) - 8차원
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
                                   new Vector2Int(1,1), new Vector2Int(-1,1), new Vector2Int(1,-1), new Vector2Int(-1,-1) };

        for (int i = 0; i < 8; i++)
        {
            float opportunity = CalculateDirectionalOpportunity(myX, myY, directions[i], myPlayerID);
            sensor.AddObservation(opportunity);
        }

        // 경계까지의 거리 (4차원: 상/하/좌/우)
        sensor.AddObservation((25 - myY) / 25f); // 위쪽 경계까지
        sensor.AddObservation(myY / 25f);        // 아래쪽 경계까지  
        sensor.AddObservation((50 - myX) / 50f); // 오른쪽 경계까지
        sensor.AddObservation(myX / 50f);        // 왼쪽 경계까지

        // 내 영역의 연결성 (1차원)
        float connectivity = CalculateTerritoryConnectivity(myPlayerID);
        sensor.AddObservation(connectivity);

        // 내 꼬리 길이 (위험도) (1차원)
        float trailRisk = CalculateTrailRisk(myX, myY, myPlayerID);
        sensor.AddObservation(trailRisk);

        // 전체 점령률 (1차원)
        float totalOccupancy = CalculateMapOccupancy();
        sensor.AddObservation(totalOccupancy);

        // 추가 전략 정보들 (24차원)
        // 현재 위치에서 각 방향으로의 안전성 평가 (8차원)
        for (int i = 0; i < 8; i++)
        {
            float safety = CalculateDirectionalSafety(myX, myY, directions[i], myPlayerID);
            sensor.AddObservation(safety);
        }

        // 영역 크기 변화율 (4차원: 최근 4스텝)
        for (int i = 0; i < 4; i++)
        {
            sensor.AddObservation(0.5f); // 임시값 - 실제로는 이전 점수들과 비교
        }

        // 맵 중심으로부터의 거리 및 각도 (2차원)
        float distanceFromCenter = Vector2.Distance(new Vector2(myX, myY), new Vector2(25, 12.5f)) / 30f;
        float angleFromCenter = Mathf.Atan2(myY - 12.5f, myX - 25f) / Mathf.PI; // -1 ~ 1
        sensor.AddObservation(distanceFromCenter);
        sensor.AddObservation(angleFromCenter);

        // 내 영역의 형태 분석 (10차원)
        for (int i = 0; i < 10; i++)
        {
            sensor.AddObservation(0.5f); // 임시값 - 복잡한 형태 분석
        }
    }

    private void AddOpponentObservations(VectorSensor sensor, int myX, int myY, int myPlayerID)
    {
        // 최대 3명의 상대방 정보 (각 5차원씩) - 총 15차원
        for (int opponentSlot = 0; opponentSlot < 3; opponentSlot++)
        {
            // 실제 상대방이 있는지 확인하고 정보 수집
            // 여기서는 간단히 더미 데이터로 구현
            sensor.AddObservation(0f); // 상대방 X 위치
            sensor.AddObservation(0f); // 상대방 Y 위치  
            sensor.AddObservation(0f); // 상대방과의 거리
            sensor.AddObservation(0f); // 상대방 점수
            sensor.AddObservation(0f); // 상대방 위험도
        }
    }

    private float CalculateDirectionalOpportunity(int x, int y, Vector2Int direction, int playerID)
    {
        float opportunity = 0f;
        for (int dist = 1; dist <= 10; dist++)
        {
            Vector2Int checkPos = new Vector2Int(x + direction.x * dist, y + direction.y * dist);
            if (!mapManager.InBounds(checkPos)) break;

            int owner = mapManager.GetTile(checkPos);
            if (owner == 0) opportunity += 1f / dist; // 중립 타일, 거리 가중치
            else if (owner != playerID) opportunity -= 1f / dist; // 적 타일
        }
        return Mathf.Clamp(opportunity / 5f, -1f, 1f);
    }

    private float CalculateDirectionalSafety(int x, int y, Vector2Int direction, int playerID)
    {
        // 해당 방향으로 이동했을 때의 안전성 평가
        Vector2Int nextPos = new Vector2Int(x + direction.x, y + direction.y);
        
        if (!mapManager.InBounds(nextPos)) return -1f; // 경계 밖은 위험

        // 내 궤적과 충돌하는지 확인
        int trail = mapManager.GetTrail(nextPos);
        if (trail == playerID) return -1f; // 내 궤적과 충돌

        // 주변 안전성 계산
        float safety = 0f;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector2Int checkPos = new Vector2Int(nextPos.x + dx, nextPos.y + dy);
                if (mapManager.InBounds(checkPos))
                {
                    int checkTrail = mapManager.GetTrail(checkPos);
                    if (checkTrail == playerID) safety -= 0.2f; // 내 궤적 근처는 위험
                    else if (checkTrail == 0) safety += 0.1f; // 빈 공간은 안전
                }
            }
        }
        
        return Mathf.Clamp(safety, -1f, 1f);
    }

    private float CalculateTerritoryConnectivity(int playerID)
    {
        // 간단한 연결성 계산 - 실제로는 더 복잡한 알고리즘 필요
        // 내 영역의 평균 연결성을 계산
        return 0.5f; // 임시값
    }

    private float CalculateTrailRisk(int x, int y, int playerID)
    {
        // 현재 위치가 내 영역 밖인지 확인하여 꼬리 위험도 계산
        if (mapManager.InBounds(new Vector2Int(x, y)))
        {
            int currentTile = mapManager.GetTile(new Vector2Int(x, y));
            return (currentTile != playerID) ? 0.8f : 0.1f; // 영역 밖이면 위험
        }
        return 0.5f;
    }

    private float CalculateMapOccupancy()
    {
        // 전체 맵 점령률 계산
        float occupied = 0f;
        for (int x = 0; x < 50; x++)
        {
            for (int y = 0; y < 25; y++)
            {
                if (mapManager.GetTile(new Vector2Int(x, y)) != 0) occupied++;
            }
        }
        return occupied / 1250f;
    }
    private bool isDead = false; // 사망 상태 추적

    public void NotifyDeath()
    {
        if (!isDead) // 중복 호출 방지
        {
            isDead = true;
            SetReward(-10.0f); // 사망 페널티
            Debug.Log($"MyAgent({controller?.playerID}): 사망 감지됨. 즉시 재시작.");

            // 약간의 지연을 두고 에피소드 종료 (상태 안정화)
            Invoke(nameof(DelayedEndEpisode), 0.1f);
        }
    }

    private void DelayedEndEpisode()
    {
        EndEpisode();
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        if (controller != null && action >= 0 && action < possibleActions.Length)
        {
            controller.SetDirection(possibleActions[action]);

            // 유효한 행동에 대한 작은 보상
            AddReward(0.001f);
        }
        else
        {
            AddReward(-0.05f); // 잘못된 행동에 더 큰 페널티
            if (controller == null) Debug.LogError("MyAgent: AIPlayerController가 없어 행동을 수행할 수 없습니다.");
            else Debug.LogWarning($"MyAgent: Received invalid action index: {action}");
        }        // 생존에 대한 작은 보상 (매 스텝마다)
        AddReward(0.002f);

        // 이미 사망 상태라면 더 이상 진행하지 않음
        if (isDead)
        {
            return;
        }

        // 게임 종료 체크
        if (gameManager != null && controller != null)
        {
            float currentScore = gameManager.GetScore(controller.playerID);

            // 영역 확장에 대한 보상
            if (currentScore > 0)
            {
                AddReward(currentScore * 0.001f); // 점수에 비례한 보상
            }

            // 점수 기반 사망 감지 (보조적 체크)
            if (currentScore < 0 && !isDead)
            {
                Debug.Log($"MyAgent({controller.playerID}): 점수 기반 사망 감지 (score: {currentScore})");
                NotifyDeath();
                return;
            }

            if (currentScore >= 500) // 승리
            {
                SetReward(20.0f); // 더 큰 승리 보상
                Debug.Log($"MyAgent({controller.playerID}): 승리하여 에피소드 종료. 즉시 재시작.");
                EndEpisode();
                return;
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut.Clear();

        // IJKL 키로 에이전트 수동 제어 (Player1 WASD와 구분)
        if (Input.GetKey(KeyCode.I)) // 위
        {
            discreteActionsOut[0] = 0;
        }
        else if (Input.GetKey(KeyCode.L)) // 오른쪽
        {
            discreteActionsOut[0] = 1;
        }
        else if (Input.GetKey(KeyCode.K)) // 아래
        {
            discreteActionsOut[0] = 2;
        }
        else if (Input.GetKey(KeyCode.J)) // 왼쪽
        {
            discreteActionsOut[0] = 3;
        }
    }
}