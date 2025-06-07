using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;
using System.Collections.Generic;

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

    // **이동 히스토리 추적 (직선 이동 감지용)**
    private Queue<Vector2Int> directionHistory = new Queue<Vector2Int>();
    private Queue<Vector2Int> positionHistory = new Queue<Vector2Int>();
    private const int HISTORY_SIZE = 8;

    private bool isDead = false;
    private const int MAX_STEPS_WITHOUT_PROGRESS = 500;
    private int stepsWithoutProgress = 0;
    private float previousScore = 0f;
    private Vector2Int previousPosition = Vector2Int.zero;

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

        Debug.Log("[MyAgent] Initialize 완료 - 🎯 3x3 중심 ULTRA 최적화 관찰 시스템 (1,319차원)");
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log($"[MyAgent] Player {controller?.playerID} 에피소드 시작");

        // **상태 초기화**
        previousScore = 0f;
        stepsWithoutProgress = 0;
        isDead = false;

        // **히스토리 초기화**
        directionHistory.Clear();
        positionHistory.Clear();

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
            controller.playerID == 2 ? 45 : 5,
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
    }

    // **🎯 고도로 최적화된 공정한 관찰 시스템 - 3x3 핵심 영역 중심**
    public override void CollectObservations(VectorSensor sensor)
    {
        if (controller == null || mapManager == null)
        {
            // 기본값으로 채워서 관찰 차원 맞추기 (45 + 625*2 + 9 + 10 + 5 = 1319차원)
            for (int i = 0; i < 1319; i++) sensor.AddObservation(0f);
            return;
        }

        int agentGridX = Mathf.RoundToInt(transform.localPosition.x);
        int agentGridY = Mathf.RoundToInt(transform.localPosition.y);
        int myPlayerID = controller.playerID;        // 1. **🔥 ULTRA CRITICAL - 3x3 즉시 위험 영역 (45차원) - 가중치 15배**
        // 이 정보가 생존에 가장 중요하므로 5번 반복해서 입력하여 중요도 극대화
        for (int repeat = 0; repeat < 5; repeat++)
        {
            AddUltraCritical3x3Observations(sensor, agentGridX, agentGridY, myPlayerID);
        }

        // 2. **핵심: 주변 25x25 영역 관찰 (625*2 = 1250차원)**
        const int OBSERVATION_SIZE = 25;
        int halfSize = OBSERVATION_SIZE / 2; // 12

        // 2-1. TileStates 관찰 (625차원)
        for (int dy = -halfSize; dy <= halfSize; dy++)
        {
            for (int dx = -halfSize; dx <= halfSize; dx++)
            {
                int worldX = agentGridX + dx;
                int worldY = agentGridY + dy;
                Vector2Int checkPos = new Vector2Int(worldX, worldY);

                float tileValue;
                if (!mapManager.InBounds(checkPos))
                {
                    tileValue = -10f; // 경계 밖은 매우 큰 음수 (벽 표시)
                }
                else
                {
                    int tileOwner = mapManager.GetTile(checkPos);
                    if (tileOwner == myPlayerID)
                        tileValue = 1f; // 내 영역
                    else if (tileOwner == 0)
                        tileValue = 0f; // 중립
                    else
                        tileValue = -1f; // 상대방 영역
                }
                sensor.AddObservation(tileValue);
            }
        }

        // 2-2. TrailStates 관찰 (625차원)
        for (int dy = -halfSize; dy <= halfSize; dy++)
        {
            for (int dx = -halfSize; dx <= halfSize; dx++)
            {
                int worldX = agentGridX + dx;
                int worldY = agentGridY + dy;
                Vector2Int checkPos = new Vector2Int(worldX, worldY);

                float trailValue;
                if (!mapManager.InBounds(checkPos))
                {
                    trailValue = -10f; // 경계 밖은 매우 큰 음수 (벽 표시)
                }
                else
                {
                    int trailOwner = mapManager.GetTrail(checkPos);
                    if (trailOwner == myPlayerID)
                        trailValue = 1f; // 내 궤적 (매우 위험!)
                    else if (trailOwner == 0)
                        trailValue = 0f; // 궤적 없음
                    else
                        trailValue = -1f; // 상대방 궤적
                }
                sensor.AddObservation(trailValue);
            }
        }

        // 3. **강화된 근접 3x3 영역 상세 분석 (9차원)**
        AddCriticalProximityObservations(sensor, agentGridX, agentGridY, myPlayerID);

        // 4. 즉시 위험 감지 (10차원)
        AddImmediateDangerObservations(sensor, agentGridX, agentGridY, myPlayerID);

        // 5. 기본 정보 (5차원)
        sensor.AddObservation(Mathf.Clamp01(agentGridX / 100f));
        sensor.AddObservation(Mathf.Clamp01(agentGridY / 100f));
        sensor.AddObservation(controller.direction.x);
        sensor.AddObservation(controller.direction.y);
        float currentScore = gameManager?.GetScore(myPlayerID) ?? 0f;
        sensor.AddObservation(currentScore / 10000f);

        Debug.Log($"[MyAgent] 🎯 ULTRA 최적화된 관찰 완료 - 총 1319차원 (45핵심x5 + 625타일 + 625궤적 + 9근접 + 10위험 + 5기본)");
    }

    // **🔥 ULTRA: 3x3 영역의 초고중요도 정보 (9차원) - 모델이 중요도를 확실히 인식하도록**
    private void AddUltraCritical3x3Observations(VectorSensor sensor, int myX, int myY, int myPlayerID)
    {
        // 3x3 영역을 정해진 순서로 관찰 (중앙부터 시작해서 시계방향)
        Vector2Int[] positions = {
            new Vector2Int(0, 0),   // 중앙 (현재 위치)
            new Vector2Int(0, 1),   // 위
            new Vector2Int(1, 1),   // 우상
            new Vector2Int(1, 0),   // 우
            new Vector2Int(1, -1),  // 우하
            new Vector2Int(0, -1),  // 하
            new Vector2Int(-1, -1), // 좌하
            new Vector2Int(-1, 0),  // 좌
            new Vector2Int(-1, 1)   // 좌상
        };

        foreach (var relativePos in positions)
        {
            Vector2Int checkPos = new Vector2Int(myX + relativePos.x, myY + relativePos.y);

            float ultraCriticalValue = 0f;

            if (!mapManager.InBounds(checkPos))
            {
                // 경계 = 절대 위험 (더욱 강화된 신호)
                ultraCriticalValue = -2000f;
            }
            else
            {
                int tileOwner = mapManager.GetTile(checkPos);
                int trailOwner = mapManager.GetTrail(checkPos);

                // **EXTREME 우선순위: 자기 궤적 = 즉시 사망**
                if (trailOwner == myPlayerID)
                {
                    // 모델이 절대 이 값을 무시할 수 없도록 극도로 큰 음수
                    ultraCriticalValue = -5000f;
                }
                // **2순위: 다른 궤적 = 위험 (강화)**
                else if (trailOwner != 0 && trailOwner != myPlayerID)
                {
                    ultraCriticalValue = -200f;
                }
                // **3순위: 안전 지역 구분 (강화된 양수 신호)**
                else if (tileOwner == myPlayerID)
                {
                    ultraCriticalValue = 300f; // 내 영역 - 매우 안전 (강화)
                }
                else if (tileOwner == 0)
                {
                    ultraCriticalValue = 150f; // 중립 - 확장 기회 (강화)
                }
                else
                {
                    ultraCriticalValue = -50f; // 상대방 영역 - 조금 위험 (강화)
                }
            }

            // 정규화하되 중요도를 극대화 (-5 ~ +3 범위로 강화)
            sensor.AddObservation(Mathf.Clamp(ultraCriticalValue / 1000f, -5f, 3f));
        }
    }

    // **🔥 NEW: 3x3 영역의 초고중요도 정보 (9차원)**
    private void AddSuperCritical3x3Observations(VectorSensor sensor, int myX, int myY, int myPlayerID)
    {
        // 3x3 영역을 정해진 순서로 관찰 (중앙부터 시작해서 시계방향)
        Vector2Int[] positions = {
            new Vector2Int(0, 0),   // 중앙 (현재 위치)
            new Vector2Int(0, 1),   // 위
            new Vector2Int(1, 1),   // 우상
            new Vector2Int(1, 0),   // 우
            new Vector2Int(1, -1),  // 우하
            new Vector2Int(0, -1),  // 하
            new Vector2Int(-1, -1), // 좌하
            new Vector2Int(-1, 0),  // 좌
            new Vector2Int(-1, 1)   // 좌상
        };

        foreach (var relativePos in positions)
        {
            Vector2Int checkPos = new Vector2Int(myX + relativePos.x, myY + relativePos.y);

            float superCriticalValue = 0f;

            if (!mapManager.InBounds(checkPos))
            {
                superCriticalValue = -100f; // 경계 - 절대 위험
            }
            else
            {
                int tileOwner = mapManager.GetTile(checkPos);
                int trailOwner = mapManager.GetTrail(checkPos);

                // **최우선: 자기 궤적 = 즉시 사망**
                if (trailOwner == myPlayerID)
                {
                    superCriticalValue = -1000f; // 절대 가면 안되는 곳!
                }
                // **2순위: 다른 궤적 = 위험**
                else if (trailOwner != 0 && trailOwner != myPlayerID)
                {
                    superCriticalValue = -50f;
                }
                // **3순위: 안전 지역 구분**
                else if (tileOwner == myPlayerID)
                {
                    superCriticalValue = 100f; // 내 영역 - 매우 안전
                }
                else if (tileOwner == 0)
                {
                    superCriticalValue = 50f; // 중립 - 확장 기회
                }
                else
                {
                    superCriticalValue = -10f; // 상대방 영역 - 조금 위험
                }
            }

            sensor.AddObservation(superCriticalValue / 1000f); // 정규화
        }
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
        for (int x = 0; x < 100; x++)
        {
            for (int y = 0; y < 100; y++)
            {
                if (mapManager.GetTrail(new Vector2Int(x, y)) == myPlayerID)
                    trailLength++;
            }
        }
        sensor.AddObservation(Mathf.Clamp01(trailLength / 200f)); // 정규화

        // 가장 가까운 내 궤적까지의 거리
        float closestTrailDistance = 999f;
        for (int x = 0; x < 100; x++)
        {
            for (int y = 0; y < 100; y++)
            {
                if (mapManager.GetTrail(new Vector2Int(x, y)) == myPlayerID)
                {
                    float distance = Vector2.Distance(new Vector2(myX, myY), new Vector2(x, y));
                    closestTrailDistance = Mathf.Min(closestTrailDistance, distance);
                }
            }
        }
        sensor.AddObservation(Mathf.Clamp01(closestTrailDistance / 50f)); // 정규화

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
            Vector2Int newDirection = possibleActions[action];
            Vector2Int currentPos = new Vector2Int(
                Mathf.RoundToInt(transform.localPosition.x),
                Mathf.RoundToInt(transform.localPosition.y)
            );

            // **핵심 수정: 경계 체크를 먼저 수행**
            Vector2Int nextPos = currentPos + newDirection;
            // **경계 밖으로 나가려는 시도를 강력히 차단**
            if (!mapManager.InBounds(nextPos))
            {
                Debug.LogWarning($"[MyAgent] 경계 밖 이동 시도 차단! 현재: {currentPos}, 다음: {nextPos}");
                AddReward(-5.0f); // 경계 이동 시도에 매우 큰 페널티

                // 안전한 방향으로 강제 변경
                Vector2Int safeDirection = FindSafeDirection(currentPos);
                if (safeDirection != Vector2Int.zero)
                {
                    newDirection = safeDirection;
                    Debug.Log($"[MyAgent] 안전한 방향으로 변경: {safeDirection}");
                }
                else
                {
                    // 모든 방향이 위험하면 현재 방향 유지
                    newDirection = controller.direction;
                    Debug.LogWarning("[MyAgent] 모든 방향이 위험! 현재 방향 유지");
                }
            }

            // **자기 궤적 충돌 방지 (즉시 사망 방지)**
            nextPos = currentPos + newDirection; // 방향이 변경되었을 수 있으므로 재계산
            if (mapManager.InBounds(nextPos))
            {
                int nextTrail = mapManager.GetTrail(nextPos);
                if (nextTrail == controller.playerID)
                {
                    Debug.LogWarning($"[MyAgent] 자기 궤적 충돌 시도 차단! 현재: {currentPos}, 다음: {nextPos}");
                    AddReward(-10.0f); // 자기 궤적 충돌 시도에 매우 큰 페널티

                    // 안전한 방향으로 강제 변경
                    Vector2Int safeDirection = FindSafeDirection(currentPos);
                    if (safeDirection != Vector2Int.zero)
                    {
                        newDirection = safeDirection;
                        Debug.Log($"[MyAgent] 궤적 충돌 방지를 위해 안전한 방향으로 변경: {safeDirection}");
                    }
                    else
                    {
                        // 모든 방향이 위험하면 에피소드 종료
                        Debug.LogError("[MyAgent] 모든 방향이 위험! 에피소드 종료");
                        AddReward(-20.0f);
                        NotifyDeath();
                        return;
                    }
                }
            }

            // **영역 확보 중심 보상 시스템**
            CalculateSmartRewards(newDirection, currentPos);

            controller.SetDirection(newDirection);
        }
        else
        {
            AddReward(-0.1f); // 잘못된 행동에 페널티
        }

        // 게임 종료 체크
        if (gameManager != null && controller != null && !isDead)
        {
            float currentScore = gameManager.GetScore(controller.playerID);

            if (currentScore < 0)
            {
                Debug.Log($"MyAgent({controller.playerID}): 점수 기반 사망 감지 (score: {currentScore})");
                NotifyDeath();
                return;
            }

            if (currentScore >= 500) // 승리
            {
                SetReward(50.0f);
                EndEpisode();
                return;
            }
        }
    }

    // **개선된 함수: 안전한 방향 찾기**
    private Vector2Int FindSafeDirection(Vector2Int currentPos)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        // 우선순위: 내 영역으로의 이동을 선호
        Vector2Int bestDirection = Vector2Int.zero;
        int bestScore = -999;

        foreach (var dir in directions)
        {
            Vector2Int testPos = currentPos + dir;

            // 경계 체크
            if (!mapManager.InBounds(testPos)) continue;

            // 자기 궤적 체크 (절대 피해야 함)
            if (mapManager.GetTrail(testPos) == controller.playerID) continue;

            // 안전도 점수 계산
            int score = 0;
            int tileOwner = mapManager.GetTile(testPos);

            if (tileOwner == controller.playerID)
            {
                score += 100; // 내 영역으로 이동 (가장 안전)
            }
            else if (tileOwner == 0)
            {
                score += 50; // 중립 지역 (보통 안전)
            }
            else
            {
                score += 10; // 상대방 영역 (덜 선호하지만 안전)
            }

            // 다른 궤적이 있으면 감점
            if (mapManager.GetTrail(testPos) != 0)
            {
                score -= 30;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = dir;
            }
        }

        return bestDirection;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;

        int selectedAction = -1;

        // IJKL 키로 에이전트 수동 제어
        if (Input.GetKey(KeyCode.I) || Input.GetKeyDown(KeyCode.I)) selectedAction = 0; // 위
        else if (Input.GetKey(KeyCode.L) || Input.GetKeyDown(KeyCode.L)) selectedAction = 1; // 오른쪽
        else if (Input.GetKey(KeyCode.K) || Input.GetKeyDown(KeyCode.K)) selectedAction = 2; // 아래
        else if (Input.GetKey(KeyCode.J) || Input.GetKeyDown(KeyCode.J)) selectedAction = 3; // 왼쪽

        if (selectedAction >= 0)
        {
            discreteActionsOut[0] = selectedAction;
        }
        else
        {
            // 현재 방향 유지
            Vector2Int currentDir = controller?.direction ?? Vector2Int.zero;
            if (currentDir == Vector2Int.up) discreteActionsOut[0] = 0;
            else if (currentDir == Vector2Int.right) discreteActionsOut[0] = 1;
            else if (currentDir == Vector2Int.down) discreteActionsOut[0] = 2;
            else if (currentDir == Vector2Int.left) discreteActionsOut[0] = 3;
            else discreteActionsOut[0] = 1; // 기본값: 오른쪽
        }
    }

    // **개선된 보상 시스템**
    private void CalculateSmartRewards(Vector2Int newDirection, Vector2Int currentPos)
    {
        int myPlayerID = controller.playerID;
        Vector2Int nextPos = currentPos + newDirection;

        // **히스토리 업데이트**
        UpdateHistory(newDirection, nextPos);

        // 1. 기본 생존 보상
        AddReward(0.01f);

        // 2. 경계 체크와 자기 궤적 체크는 이미 OnActionReceived에서 처리됨

        // 3. 영역 확보 보상들
        if (mapManager.InBounds(nextPos))
        {
            int nextTile = mapManager.GetTile(nextPos);
            if (nextTile == 0) // 중립 지역
            {
                AddReward(0.15f); // 새로운 땅 탐험 보상
            }
            else if (nextTile == myPlayerID) // 내 영역으로 복귀
            {
                AddReward(0.05f); // 안전한 복귀 보상
            }
        }

        // 4. 직선 이동 페널티
        if (IsStraightLineMovement())
        {
            AddReward(-0.2f);
        }

        // 5. 반복 패턴 페널티
        if (IsRepeatingPattern(newDirection))
        {
            AddReward(-0.25f);
        }
    }

    // **개선된 직선 이동 패턴 감지**
    private bool IsStraightLineMovement()
    {
        if (directionHistory.Count < 4) return false;

        Vector2Int[] directions = directionHistory.ToArray();
        Vector2Int firstDirection = directions[0];

        for (int i = 1; i < 4; i++)
        {
            if (directions[i] != firstDirection)
                return false;
        }

        return true;
    }

    // **개선된 반복 패턴 감지**
    private bool IsRepeatingPattern(Vector2Int direction)
    {
        if (directionHistory.Count < 6) return false;

        var tempHistory = new List<Vector2Int>(directionHistory);
        tempHistory.Add(direction);

        return CheckRepeatingPattern(tempHistory, 2) || CheckRepeatingPattern(tempHistory, 3);
    }

    private bool CheckRepeatingPattern(List<Vector2Int> history, int patternLength)
    {
        if (history.Count < patternLength * 2) return false;

        for (int i = 0; i < patternLength; i++)
        {
            int lastIndex = history.Count - 1 - i;
            int prevIndex = lastIndex - patternLength;

            if (prevIndex < 0 || history[lastIndex] != history[prevIndex])
                return false;
        }

        return true;
    }

    // **히스토리 업데이트**
    private void UpdateHistory(Vector2Int direction, Vector2Int position)
    {
        directionHistory.Enqueue(direction);
        if (directionHistory.Count > HISTORY_SIZE)
            directionHistory.Dequeue();

        positionHistory.Enqueue(position);
        if (positionHistory.Count > HISTORY_SIZE)
            positionHistory.Dequeue();
    }
}