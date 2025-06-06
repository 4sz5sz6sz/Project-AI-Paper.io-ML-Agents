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

        Debug.Log("[MyAgent] Initialize 완료 - 확장된 관찰 시스템 (700+ 차원 벡터)");
    }
    public override void OnEpisodeBegin()
    {
        Debug.Log($"[MyAgent] Player {controller?.playerID} 에피소드 시작");

        // **상태 초기화**
        previousScore = 0f;
        stepsWithoutProgress = 0;
        isDead = false;

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

        previousPosition = spawnPos;

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
    }    // 확장된 고해상도 관찰: 700+ 차원 (Unity Inspector 설정 필요)
    public override void CollectObservations(VectorSensor sensor)
    {
        if (controller == null || mapManager == null)
        {
            // 기본값으로 채워서 관찰 차원 맞추기 (총 1328차원)
            for (int i = 0; i < 1328; i++) sensor.AddObservation(0f);
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

        // 2. 전체 맵 관찰 (50x25 = 1250차원) - 모든 타일 상태
        for (int y = 0; y < 25; y++)
        {
            for (int x = 0; x < 50; x++)
            {
                Vector2Int tilePos = new Vector2Int(x, y);
                int tileOwner = mapManager.GetTile(tilePos);
                // 상대방 영역(-1), 중립(0), 내 영역(1)로 정규화
                float normalizedTile = (tileOwner == myPlayerID) ? 1f :
                                     (tileOwner == 0) ? 0f : -1f;
                sensor.AddObservation(normalizedTile);
            }
        }

        // 3. **중요!** 근접 3x3 영역 (9차원) - 생존에 핵심적인 정보
        AddCriticalProximityObservations(sensor, agentGridX, agentGridY, myPlayerID);

        // 4. 즉시 위험 감지 (10차원) - 꼬리 충돌 방지
        AddImmediateDangerObservations(sensor, agentGridX, agentGridY, myPlayerID);

        // 5. 전략적 관찰 (39차원) - 방향별 기회 + 경계 거리 + 영역 정보
        AddStrategicObservations(sensor, agentGridX, agentGridY, myPlayerID);

        // 6. 상대방 정보 (15차원) - 최대 3명 상대방
        AddOpponentObservations(sensor, agentGridX, agentGridY, myPlayerID);

        Debug.Log($"[MyAgent] 관찰 완료 - 총 1328차원 (5기본 + 1250전체맵 + 9근접 + 10위험 + 39전략 + 15상대방)");
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
        }
        return dangerCount > 0 ? Mathf.Clamp01(danger / 10f) : 0f;
    }

    // **중요!** 근접 3x3 영역 - 생존에 가장 핵심적인 정보 (9차원)
    private void AddCriticalProximityObservations(VectorSensor sensor, int myX, int myY, int myPlayerID)
    {
        // 3x3 영역의 각 타일을 개별적으로 관찰 (생존에 직접적 영향)
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector2Int checkPos = new Vector2Int(myX + x, myY + y);

                float criticalValue = 0f;

                if (!mapManager.InBounds(checkPos))
                {
                    criticalValue = -2f; // 경계 밖 - 매우 위험
                }
                else
                {
                    // 타일 소유권 확인
                    int tileOwner = mapManager.GetTile(checkPos);
                    int trailOwner = mapManager.GetTrail(checkPos);

                    // **생존 핵심 로직**: 내 궤적이 있으면 절대 위험
                    if (trailOwner == myPlayerID)
                    {
                        criticalValue = -3f; // 내 궤적 - 절대 가면 안됨!
                    }
                    else if (trailOwner != 0 && trailOwner != myPlayerID)
                    {
                        criticalValue = -1f; // 다른 플레이어 궤적 - 위험
                    }
                    else if (tileOwner == myPlayerID)
                    {
                        criticalValue = 2f; // 내 영역 - 안전
                    }
                    else if (tileOwner == 0)
                    {
                        criticalValue = 1f; // 중립 - 확장 기회
                    }
                    else
                    {
                        criticalValue = -0.5f; // 다른 플레이어 영역
                    }
                }

                sensor.AddObservation(criticalValue);
            }
        }
    }

    // 즉시 위험 감지 - 다음 스텝에서 일어날 수 있는 모든 위험 (10차원)
    private void AddImmediateDangerObservations(VectorSensor sensor, int myX, int myY, int myPlayerID)
    {
        // 4방향 이동 시 즉시 위험도
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        for (int i = 0; i < 4; i++)
        {
            Vector2Int nextPos = new Vector2Int(myX + directions[i].x, myY + directions[i].y);

            float immediateRisk = 0f;

            if (!mapManager.InBounds(nextPos))
            {
                immediateRisk = 1f; // 경계로 이동 - 즉시 사망
            }
            else
            {
                int trail = mapManager.GetTrail(nextPos);
                if (trail == myPlayerID)
                {
                    immediateRisk = 1f; // 내 궤적으로 이동 - 즉시 사망
                }
                else if (trail != 0)
                {
                    immediateRisk = 0.8f; // 다른 궤적으로 이동 - 위험
                }
                else
                {
                    // 안전한 이동이지만 주변 확인
                    int nearbyTrails = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            Vector2Int checkPos = new Vector2Int(nextPos.x + dx, nextPos.y + dy);
                            if (mapManager.InBounds(checkPos))
                            {
                                int checkTrail = mapManager.GetTrail(checkPos);
                                if (checkTrail == myPlayerID) nearbyTrails++;
                            }
                        }
                    }
                    immediateRisk = Mathf.Clamp01(nearbyTrails / 8f); // 주변 궤적 밀도
                }
            }

            sensor.AddObservation(immediateRisk);
        }

        // 추가 위험 지표들 (6차원)

        // 현재 위치가 내 영역인지
        Vector2Int currentPos = new Vector2Int(myX, myY);
        bool inMyTerritory = mapManager.InBounds(currentPos) &&
                           mapManager.GetTile(currentPos) == myPlayerID;
        sensor.AddObservation(inMyTerritory ? 0f : 1f); // 영역 밖이면 위험

        // 내 궤적의 총 길이 (위험도 증가)
        int trailLength = 0;
        for (int x = 0; x < 50; x++)
        {
            for (int y = 0; y < 25; y++)
            {
                if (mapManager.GetTrail(new Vector2Int(x, y)) == myPlayerID)
                    trailLength++;
            }
        }
        sensor.AddObservation(Mathf.Clamp01(trailLength / 100f)); // 정규화

        // 가장 가까운 내 궤적까지의 거리
        float closestTrailDistance = 999f;
        for (int x = 0; x < 50; x++)
        {
            for (int y = 0; y < 25; y++)
            {
                if (mapManager.GetTrail(new Vector2Int(x, y)) == myPlayerID)
                {
                    float distance = Vector2.Distance(new Vector2(myX, myY), new Vector2(x, y));
                    closestTrailDistance = Mathf.Min(closestTrailDistance, distance);
                }
            }
        }
        sensor.AddObservation(Mathf.Clamp01(closestTrailDistance / 20f)); // 정규화

        // 현재 방향으로 계속 가면 위험한지
        Vector2Int currentDirection = controller.direction;
        Vector2Int projectedPos = new Vector2Int(myX + currentDirection.x, myY + currentDirection.y);
        float projectedRisk = 0f;
        if (!mapManager.InBounds(projectedPos) ||
            mapManager.GetTrail(projectedPos) == myPlayerID)
        {
            projectedRisk = 1f;
        }
        sensor.AddObservation(projectedRisk);

        // 탈출 가능한 방향의 수
        int escapePaths = 0;
        foreach (var dir in directions)
        {
            Vector2Int escapePos = new Vector2Int(myX + dir.x, myY + dir.y);
            if (mapManager.InBounds(escapePos) &&
                mapManager.GetTrail(escapePos) != myPlayerID)
            {
                escapePaths++;
            }
        }
        sensor.AddObservation(escapePaths / 4f); // 0~1로 정규화

        // 주변 8방향 중 안전한 곳의 비율
        int safeCells = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int checkPos = new Vector2Int(myX + dx, myY + dy);
                if (mapManager.InBounds(checkPos) &&
                    mapManager.GetTrail(checkPos) != myPlayerID)
                {
                    safeCells++;
                }
            }
        }
        sensor.AddObservation(safeCells / 8f); // 0~1로 정규화
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
    private float previousScore = 0f;
    private Vector2Int previousPosition;
    private int stepsWithoutProgress = 0;
    private const int MAX_STEPS_WITHOUT_PROGRESS = 100;

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        if (controller != null && action >= 0 && action < possibleActions.Length)
        {
            Vector2Int newDirection = possibleActions[action];
            Vector2Int currentPos = new Vector2Int(
                Mathf.RoundToInt(transform.localPosition.x),
                Mathf.RoundToInt(transform.localPosition.y)
            );

            // **영역 확보 중심 보상 시스템**
            CalculateSmartRewards(newDirection, currentPos);

            controller.SetDirection(newDirection);
        }
        else
        {
            AddReward(-0.1f); // 잘못된 행동에 더 큰 페널티
            if (controller == null) Debug.LogError("MyAgent: AIPlayerController가 없어 행동을 수행할 수 없습니다.");
            else Debug.LogWarning($"MyAgent: Received invalid action index: {action}");
        }

        // 이미 사망 상태라면 더 이상 진행하지 않음
        if (isDead)
        {
            return;
        }

        // 게임 종료 체크
        if (gameManager != null && controller != null)
        {
            float currentScore = gameManager.GetScore(controller.playerID);

            // 점수 기반 사망 감지 (보조적 체크)
            if (currentScore < 0 && !isDead)
            {
                Debug.Log($"MyAgent({controller.playerID}): 점수 기반 사망 감지 (score: {currentScore})");
                NotifyDeath();
                return;
            }

            if (currentScore >= 500) // 승리
            {
                SetReward(50.0f); // 승리 보상 증대
                Debug.Log($"MyAgent({controller.playerID}): 승리하여 에피소드 종료.");
                EndEpisode();
                return;
            }

            // 진전 없이 너무 오래 걸리면 페널티
            if (currentScore == previousScore)
            {
                stepsWithoutProgress++;
                if (stepsWithoutProgress > MAX_STEPS_WITHOUT_PROGRESS)
                {
                    AddReward(-0.5f); // 진전 없음 페널티
                    stepsWithoutProgress = 0;
                }
            }
            else
            {
                stepsWithoutProgress = 0;
            }

            previousScore = currentScore;
        }
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;

        // **개선된 휴리스틱 - 더 반응성 좋게**
        int selectedAction = -1;

        // IJKL 키로 에이전트 수동 제어 (Player1 WASD와 구분)
        // Input.GetKey 대신 Input.GetKeyDown도 함께 확인
        if (Input.GetKey(KeyCode.I) || Input.GetKeyDown(KeyCode.I)) // 위
        {
            selectedAction = 0;
            Debug.Log("[Heuristic] I키 입력 - 위쪽 이동");
        }
        else if (Input.GetKey(KeyCode.L) || Input.GetKeyDown(KeyCode.L)) // 오른쪽
        {
            selectedAction = 1;
            Debug.Log("[Heuristic] L키 입력 - 오른쪽 이동");
        }
        else if (Input.GetKey(KeyCode.K) || Input.GetKeyDown(KeyCode.K)) // 아래
        {
            selectedAction = 2;
            Debug.Log("[Heuristic] K키 입력 - 아래쪽 이동");
        }
        else if (Input.GetKey(KeyCode.J) || Input.GetKeyDown(KeyCode.J)) // 왼쪽
        {
            selectedAction = 3;
            Debug.Log("[Heuristic] J키 입력 - 왼쪽 이동");
        }

        // 액션 설정
        if (selectedAction >= 0)
        {
            discreteActionsOut[0] = selectedAction;
            Debug.Log($"[Heuristic] 최종 액션: {selectedAction} ({(selectedAction == 0 ? "위" : selectedAction == 1 ? "오른쪽" : selectedAction == 2 ? "아래" : "왼쪽")})");
        }
        else
        {
            // 키 입력이 없으면 현재 방향 유지 또는 랜덤
            Vector2Int currentDir = controller?.direction ?? Vector2Int.zero;
            if (currentDir == Vector2Int.up) discreteActionsOut[0] = 0;
            else if (currentDir == Vector2Int.right) discreteActionsOut[0] = 1;
            else if (currentDir == Vector2Int.down) discreteActionsOut[0] = 2;
            else if (currentDir == Vector2Int.left) discreteActionsOut[0] = 3;
            else discreteActionsOut[0] = 1; // 기본값: 오른쪽
        }
    }

    // **영역 확보 중심 스마트 보상 시스템**
    private void CalculateSmartRewards(Vector2Int newDirection, Vector2Int currentPos)
    {
        int myPlayerID = controller.playerID;
        Vector2Int nextPos = currentPos + newDirection;

        // 1. 기본 생존 보상 (매우 작게)
        AddReward(0.001f);

        if (!mapManager.InBounds(nextPos))
        {
            AddReward(-1.0f); // 경계 충돌 강력한 페널티
            return;
        }

        // 2. 자기 궤적 충돌 방지 보상
        int nextTrail = mapManager.GetTrail(nextPos);
        if (nextTrail == myPlayerID)
        {
            AddReward(-2.0f); // 자기 궤적 충돌 강력한 페널티
            return;
        }

        // 3. **영역 확보 전략 보상**
        int nextTile = mapManager.GetTile(nextPos);
        int currentTile = mapManager.GetTile(currentPos);

        bool wasInMyTerritory = (currentTile == myPlayerID);
        bool willBeInMyTerritory = (nextTile == myPlayerID);

        // 영역 밖에서 이동 중일 때 - 루프 완성을 장려
        if (!wasInMyTerritory && !willBeInMyTerritory)
        {
            // 내 영역으로 돌아가는 방향이면 큰 보상
            if (IsMovingTowardsMyTerritory(nextPos, myPlayerID))
            {
                AddReward(0.5f); // 영역으로 돌아가는 행동 장려
            }

            // 새로운 영역을 포함할 수 있는 움직임 보상
            float areaGainPotential = CalculateAreaGainPotential(currentPos, nextPos, myPlayerID);
            AddReward(areaGainPotential * 0.3f);
        }

        // 영역 밖으로 나가는 것은 전략적으로만 허용
        if (wasInMyTerritory && !willBeInMyTerritory)
        {
            // 영역 확장 가능성이 있을 때만 보상
            if (HasExpansionOpportunity(nextPos, myPlayerID))
            {
                AddReward(0.2f); // 전략적 확장 보상
            }
            else
            {
                AddReward(-0.1f); // 의미없는 영역 이탈 페널티
            }
        }

        // 4. **중립 지역 점령 보상**
        if (nextTile == 0) // 중립 지역
        {
            AddReward(0.1f); // 새로운 땅 탐험 보상
        }

        // 5. **루프 완성 감지 및 보상**
        if (!wasInMyTerritory && willBeInMyTerritory)
        {
            // 루프를 완성하여 영역으로 돌아옴 - 큰 보상!
            float loopSize = EstimateLoopSize(currentPos, myPlayerID);
            AddReward(loopSize * 2.0f); // 루프 크기에 비례한 보상
        }

        // 6. **효율성 보상** - 직선만 가는 것 방지
        if (IsStraightLineMovement())
        {
            AddReward(-0.05f); // 직선 이동 페널티
        }

        // 7. **다양성 보상** - 같은 패턴 반복 방지
        if (IsRepeatingPattern(newDirection))
        {
            AddReward(-0.1f); // 패턴 반복 페널티
        }
    }

    // 내 영역 방향으로 이동하는지 확인
    private bool IsMovingTowardsMyTerritory(Vector2Int pos, int playerID)
    {
        // 주변 5x5에서 가장 가까운 내 영역 찾기
        float minDistance = float.MaxValue;
        for (int x = -10; x <= 10; x++)
        {
            for (int y = -10; y <= 10; y++)
            {
                Vector2Int checkPos = pos + new Vector2Int(x, y);
                if (mapManager.InBounds(checkPos) && mapManager.GetTile(checkPos) == playerID)
                {
                    float distance = Vector2.Distance(pos, checkPos);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }
            }
        }
        return minDistance <= 15f; // 가까운 거리에 내 영역이 있음
    }

    // 영역 확장 가능성 계산
    private float CalculateAreaGainPotential(Vector2Int from, Vector2Int to, int playerID)
    {
        // 현재 궤적으로 둘러쌀 수 있는 중립 영역 계산
        int neutralTiles = 0;
        int totalTiles = 0;

        // 예상 루프 영역 확인 (간단한 사각형 근사)
        int minX = Mathf.Min(from.x, to.x) - 3;
        int maxX = Mathf.Max(from.x, to.x) + 3;
        int minY = Mathf.Min(from.y, to.y) - 3;
        int maxY = Mathf.Max(from.y, to.y) + 3;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int checkPos = new Vector2Int(x, y);
                if (mapManager.InBounds(checkPos))
                {
                    totalTiles++;
                    if (mapManager.GetTile(checkPos) == 0)
                        neutralTiles++;
                }
            }
        }

        return totalTiles > 0 ? (float)neutralTiles / totalTiles : 0f;
    }

    // 확장 기회가 있는지 확인
    private bool HasExpansionOpportunity(Vector2Int pos, int playerID)
    {
        // 주변에 중립 지역이 충분히 있는지 확인
        int neutralCount = 0;
        for (int x = -3; x <= 3; x++)
        {
            for (int y = -3; y <= 3; y++)
            {
                Vector2Int checkPos = pos + new Vector2Int(x, y);
                if (mapManager.InBounds(checkPos) && mapManager.GetTile(checkPos) == 0)
                    neutralCount++;
            }
        }
        return neutralCount >= 10; // 주변에 중립 지역이 10개 이상
    }

    // 루프 크기 예측
    private float EstimateLoopSize(Vector2Int pos, int playerID)
    {
        // 간단한 루프 크기 예측 - 실제로는 더 복잡한 계산 필요
        return Mathf.Clamp(Vector2.Distance(pos, previousPosition) / 10f, 0.1f, 5.0f);
    }

    // 직선 이동 패턴 감지
    private bool IsStraightLineMovement()
    {
        // 이전 3번의 이동이 모두 같은 방향인지 확인
        // 실제 구현에서는 이동 히스토리를 저장해야 함
        return false; // 간단화
    }

    // 반복 패턴 감지
    private bool IsRepeatingPattern(Vector2Int direction)
    {
        // 같은 행동이 계속 반복되는지 확인
        // 실제 구현에서는 행동 히스토리를 저장해야 함
        return false; // 간단화
    }
}