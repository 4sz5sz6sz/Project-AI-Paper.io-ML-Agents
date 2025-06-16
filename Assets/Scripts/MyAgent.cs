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
    };    // **이동 히스토리 추적 (간소화된 패턴 감지용)**
    private Queue<Vector2Int> directionHistory = new Queue<Vector2Int>();
    private Queue<Vector2Int> positionHistory = new Queue<Vector2Int>();
    private const int HISTORY_SIZE = 4; // 8에서 4로 줄임 - 더 유연한 이동 허용

    private bool isDead = false;
    private const int MAX_STEPS_WITHOUT_PROGRESS = 500;
    private int stepsWithoutProgress = 0;
    private float previousScore = 0f;
    private Vector2Int previousPosition = Vector2Int.zero;

    // **🚨 NEW: 영역 확보 추적 변수들**
    private float lastThreatLevel;
    private int lastTrailLength;
    private float trailStartTime;
    private bool trailIsOpen;
    private int prevOwnedTileCount;


    void Start()
    {
        if (mapManager == null)
            mapManager = MapManager.Instance;
        // if (mapManager == null)
        // Debug.LogError("MyAgent: Start()에서도 MapManager.Instance를 찾지 못했습니다!");
    }

    public override void Initialize()
    {
        controller = GetComponent<AIPlayerController>();
        gameManager = GameController.Instance;

        // Debug.Log("[MyAgent] Initialize 완료 - 🎯 3x3 중심 ULTRA 최적화 관찰 시스템 (1,319차원)");
    }
    public override void OnEpisodeBegin()
    {
        // Debug.Log($"[MyAgent] Player {controller?.playerID} 에피소드 시작");

        // **상태 초기화**

        //영역 관찰 변수 초기화 myagent보상함수와 연동할 때 필요
        lastThreatLevel = 0f;
        lastTrailLength = 0;
        trailStartTime = 0f;
        trailIsOpen = false;
        prevOwnedTileCount = 0;

        previousScore = 0f;
        stepsWithoutProgress = 0;
        isDead = false;

        // **🚨 NEW: 영역 확보 추적 변수 초기화**
        // consecutiveTerritoryGains = 0;
        // lastTerritoryTime = 0f;
        // totalTerritoryGainedThisEpisode = 0;

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

        // // 에이전트 재스폰 위치 설정
        // Vector2Int spawnPos;
        // switch (controller.playerID)
        // {
        //     case 1:
        //         spawnPos = new Vector2Int(5, 5);
        //         break;
        //     case 2:
        //         spawnPos = new Vector2Int(55, 20);
        //         break;
        //     case 3:
        //         spawnPos = new Vector2Int(45, 35);
        //         break;
        //     case 4:
        //         spawnPos = new Vector2Int(25, 25);
        //         break;
        //     default:
        //         spawnPos = new Vector2Int(25, 20); // 예외 처리용 중앙 스폰
        //         break;
        // }

        // previousPosition = spawnPos;

        // // 완전 재스폰 실행 (영토, 위치, 상태 모두 초기화)
        // if (controller != null)
        // {
        //     controller.FullRespawn(spawnPos);
        // }

        // 사망 상태 리셋
        isDead = false;
        // 보상 초기화
        SetReward(0f);
        // 추가적인 상태 안정화를 위한 지연 후 확인
        Invoke(nameof(VerifyRespawnState), 0.2f);

        // Debug.Log($"[MyAgent] Player {controller.playerID} 완전 재스폰 완료 - 위치: {spawnPos}");

        RequestDecision(); // 에이전트 결정 요청
    }

    private void VerifyRespawnState()
    {
        // 재스폰 후 상태 검증
        if (controller != null && gameManager != null)
        {
            int currentScore = gameManager.GetScore(controller.playerID);
            // Debug.Log($"[MyAgent] 재스폰 후 상태 검증 - Player {controller.playerID} 점수: {currentScore}");

            if (currentScore <= 0)
            {
                // Debug.LogWarning($"[MyAgent] Player {controller.playerID} 재스폰 후에도 점수가 {currentScore}입니다. 강제 초기화 시도...");

                // 강제로 점수 재설정
                if (mapManager != null)
                {
                    int initialScore = 10 * 10; // INITIAL_TERRITORY_SIZE * INITIAL_TERRITORY_SIZE
                    gameManager.SetScore(controller.playerID, initialScore);
                    // Debug.Log($"[MyAgent] Player {controller.playerID} 점수를 {initialScore}로 강제 설정");
                }
            }
        }
    }    // **🎯 고도로 최적화된 공정한 관찰 시스템 - 3x3 핵심 영역 중심 + 적 위협 평가**
    public override void CollectObservations(VectorSensor sensor)
    {
        if (controller == null || mapManager == null)
        {
            // 기본값으로 채워서 관찰 차원 맞추기 (45 + 625*2 + 9 + 10 + 5 + 15 = 1334차원)
            for (int i = 0; i < 1334; i++) sensor.AddObservation(0f);
            return;
        }

        int agentGridX = Mathf.RoundToInt(transform.localPosition.x);
        int agentGridY = Mathf.RoundToInt(transform.localPosition.y);
        int myPlayerID = controller.playerID;// 1. **🔥 ULTRA CRITICAL - 3x3 즉시 위험 영역 (45차원) - 가중치 15배**
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
        AddCriticalProximityObservations(sensor, agentGridX, agentGridY, myPlayerID);        // 4. 즉시 위험 감지 (10차원)
        AddImmediateDangerObservations(sensor, agentGridX, agentGridY, myPlayerID);

        // 5. **NEW: 적 위협 평가 시스템 (15차원)**
        AddEnemyThreatAssessment(sensor, agentGridX, agentGridY, myPlayerID);

        // 6. 기본 정보 (5차원)
        sensor.AddObservation(Mathf.Clamp01(agentGridX / 100f));
        sensor.AddObservation(Mathf.Clamp01(agentGridY / 100f));
        sensor.AddObservation(controller.direction.x);
        sensor.AddObservation(controller.direction.y);
        float currentScore = gameManager?.GetScore(myPlayerID) ?? 0f;
        sensor.AddObservation(currentScore / 10000f);

        // Debug.Log($"[MyAgent] 🎯 ULTRA 최적화된 관찰 완료 - 총 1334차원 (45핵심x5 + 625타일 + 625궤적 + 9근접 + 10위험 + 15적위협 + 5기본)");
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

    // **🚨 NEW: 적 위협 평가 시스템 - 적이 내 궤적을 끊기 전에 안전지대 도달 가능 여부 (15차원)**
    private void AddEnemyThreatAssessment(VectorSensor sensor, int myX, int myY, int myPlayerID)
    {
        // 모든 적 플레이어 찾기
        BasePlayerController[] allPlayers = UnityEngine.Object.FindObjectsByType<BasePlayerController>(FindObjectsSortMode.None);

        Vector2Int myPos = new Vector2Int(myX, myY);
        bool isInMyTerritory = mapManager.InBounds(myPos) && mapManager.GetTile(myPos) == myPlayerID;

        // 내가 안전지대에 있으면 위협 없음
        if (isInMyTerritory)
        {
            for (int i = 0; i < 15; i++) sensor.AddObservation(0f);
            return;
        }

        // 가장 가까운 내 영역까지의 최단 거리 계산
        int myDistanceToSafety = CalculateDistanceToMyTerritory(myPos, myPlayerID);

        // 내 현재 궤적 위치들 수집
        List<Vector2Int> myTrailPositions = GetMyTrailPositions(myPlayerID);

        float maxThreatLevel = 0f;
        Vector2Int nearestEnemyPos = Vector2Int.zero;
        float nearestEnemyDistance = 999f;
        float fastestInterceptTime = 999f;

        // 시야 제한: 25x25 영역 (시야 밖은 거리 상한 12.5로 제한)
        const int VISION_RANGE = 12; // 25x25 영역의 반경
        const float MAX_VISION_DISTANCE = 12.5f;

        foreach (var enemy in allPlayers)
        {
            if (enemy == null || enemy.gameObject == gameObject) continue;

            var enemyTracker = enemy.GetComponent<CornerPointTracker>();
            if (enemyTracker == null || enemyTracker.playerId == myPlayerID) continue;

            Vector2Int enemyPos = new Vector2Int(
                Mathf.RoundToInt(enemy.transform.position.x),
                Mathf.RoundToInt(enemy.transform.position.y)
            );

            // 적과의 거리 (시야 제한 적용)
            float distanceToEnemy = Vector2.Distance(myPos, enemyPos);

            // 시야 밖의 적은 거리를 상한값으로 제한
            bool isInVision = Mathf.Abs(enemyPos.x - myX) <= VISION_RANGE && Mathf.Abs(enemyPos.y - myY) <= VISION_RANGE;
            if (!isInVision)
            {
                distanceToEnemy = Mathf.Min(distanceToEnemy, MAX_VISION_DISTANCE);
            }

            if (distanceToEnemy < nearestEnemyDistance)
            {
                nearestEnemyDistance = distanceToEnemy;
                nearestEnemyPos = enemyPos;
            }

            // 적이 내 궤적을 끊을 수 있는 최단 시간 계산
            if (myTrailPositions.Count > 0)
            {
                float minInterceptTime = CalculateMinInterceptTime(enemyPos, myTrailPositions, isInVision);
                if (minInterceptTime < fastestInterceptTime)
                {
                    fastestInterceptTime = minInterceptTime;
                }

                // 위협 수준 계산: 적이 내 궤적을 끊기 전에 내가 안전지대에 도달 가능한가?
                float threatLevel = CalculateThreatLevel(myDistanceToSafety, minInterceptTime, distanceToEnemy);
                maxThreatLevel = Mathf.Max(maxThreatLevel, threatLevel);
            }
        }

        // 15차원 관찰 데이터 추가
        sensor.AddObservation(Mathf.Clamp01(maxThreatLevel)); // 전체 위협 수준 (0~1)
        sensor.AddObservation(Mathf.Clamp01(myDistanceToSafety / 50f)); // 안전지대까지 거리 정규화
        sensor.AddObservation(Mathf.Clamp01(nearestEnemyDistance / 50f)); // 가장 가까운 적까지 거리
        sensor.AddObservation(Mathf.Clamp01(fastestInterceptTime / 20f)); // 가장 빠른 차단 시간
        sensor.AddObservation(myTrailPositions.Count / 100f); // 내 궤적 길이 정규화

        // 4방향별 위험도 (상/우/하/좌)
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        for (int i = 0; i < 4; i++)
        {
            Vector2Int nextPos = myPos + directions[i];
            float directionThreat = CalculateDirectionThreat(nextPos, nearestEnemyPos, myPlayerID);
            sensor.AddObservation(directionThreat);
        }

        // 즉시 대피 필요성 (위험 임계점 도달 시 1.0)
        bool needImmediateRetreat = maxThreatLevel > 0.7f && myDistanceToSafety < fastestInterceptTime;
        sensor.AddObservation(needImmediateRetreat ? 1f : 0f);

        // 적의 방향성 정보 (적이 나를 향해 오고 있는가?)
        Vector2Int directionToMe = myPos - nearestEnemyPos;
        sensor.AddObservation(Mathf.Clamp(directionToMe.x / 10f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(directionToMe.y / 10f, -1f, 1f));        // 궤적 밀도 위험도 (궤적이 길수록 더 위험)
        float trailDensityRisk = myTrailPositions.Count > 10 ? 1f : myTrailPositions.Count / 10f;
        sensor.AddObservation(trailDensityRisk);

        // 안전지대 접근 각도 최적성 (직선 경로 vs 우회 경로)
        float pathOptimality = CalculatePathOptimality(myPos, myPlayerID);
        sensor.AddObservation(pathOptimality);

        // 15번째: 전체 상황 복합 위험도 (여러 요소 결합)
        float overallRisk = (maxThreatLevel + trailDensityRisk) / 2f;
        sensor.AddObservation(overallRisk);
    }

    // 내 영역까지의 최단 거리 계산 (A* 알고리즘 간소화 버전)
    private int CalculateDistanceToMyTerritory(Vector2Int startPos, int myPlayerID)
    {
        // 가장 가까운 내 영역 찾기
        int minDistance = 999;
        for (int x = Mathf.Max(0, startPos.x - 25); x <= Mathf.Min(99, startPos.x + 25); x++)
        {
            for (int y = Mathf.Max(0, startPos.y - 25); y <= Mathf.Min(99, startPos.y + 25); y++)
            {
                Vector2Int checkPos = new Vector2Int(x, y);
                if (mapManager.GetTile(checkPos) == myPlayerID)
                {
                    int distance = Mathf.Abs(startPos.x - x) + Mathf.Abs(startPos.y - y); // 맨하탄 거리
                    minDistance = Mathf.Min(minDistance, distance);
                }
            }
        }
        return minDistance;
    }

    // 내 궤적 위치들 수집
    private List<Vector2Int> GetMyTrailPositions(int myPlayerID)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        for (int x = 0; x < 100; x++)
        {
            for (int y = 0; y < 100; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (mapManager.GetTrail(pos) == myPlayerID)
                {
                    positions.Add(pos);
                }
            }
        }
        return positions;
    }
    // 적이 내 궤적을 끊을 수 있는 최단 시간 계산 (시야 제한 고려)
    private float CalculateMinInterceptTime(Vector2Int enemyPos, List<Vector2Int> myTrailPositions, bool isInVision)
    {
        float minTime = 999f;
        foreach (var trailPos in myTrailPositions)
        {
            float distance = Vector2.Distance(enemyPos, trailPos);

            // 시야 밖의 적은 거리에 불확실성 추가
            if (!isInVision)
            {
                distance = Mathf.Min(distance, 12.5f); // 최대 거리 제한
            }

            minTime = Mathf.Min(minTime, distance); // 1칸당 1턴 가정
        }
        return minTime;
    }

    // 위협 수준 계산 (0~1, 1이 최고 위험)
    private float CalculateThreatLevel(int myDistanceToSafety, float enemyInterceptTime, float enemyDistance)
    {
        // 적이 내 궤적을 끊기 전에 내가 안전지대에 도달 가능한가?
        if (myDistanceToSafety >= enemyInterceptTime)
        {
            // 위험: 적이 더 빠르게 차단 가능
            float urgency = 1f - (enemyInterceptTime - myDistanceToSafety) / 10f;
            return Mathf.Clamp01(urgency);
        }
        else
        {
            // 안전: 내가 먼저 도달 가능
            return Mathf.Clamp01(0.3f - (myDistanceToSafety - enemyInterceptTime) / 20f);
        }
    }

    // 특정 방향으로 이동 시 위험도 계산
    private float CalculateDirectionThreat(Vector2Int nextPos, Vector2Int enemyPos, int myPlayerID)
    {
        if (!mapManager.InBounds(nextPos)) return 1f; // 경계 밖은 최고 위험

        float threat = 0f;

        // 적과 가까워지면 위험 증가
        float distanceToEnemy = Vector2.Distance(nextPos, enemyPos);
        if (distanceToEnemy < 5f) threat += (5f - distanceToEnemy) / 5f * 0.5f;

        // 내 궤적이 있으면 즉시 사망
        if (mapManager.GetTrail(nextPos) == myPlayerID) threat = 1f;

        // 적의 영역이면 위험 증가
        int tileOwner = mapManager.GetTile(nextPos);
        if (tileOwner != 0 && tileOwner != myPlayerID) threat += 0.3f;

        return Mathf.Clamp01(threat);
    }

    // 안전지대로의 경로 최적성 계산
    private float CalculatePathOptimality(Vector2Int myPos, int myPlayerID)
    {
        // 직선 경로와 실제 필요 이동 비교
        int straightLineDistance = CalculateDistanceToMyTerritory(myPos, myPlayerID);

        // 장애물 회피 필요성 체크
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        int blockedDirections = 0;

        foreach (var dir in directions)
        {
            Vector2Int checkPos = myPos + dir;
            if (!mapManager.InBounds(checkPos) ||
                mapManager.GetTrail(checkPos) == myPlayerID ||
                mapManager.GetTrail(checkPos) != 0)
            {
                blockedDirections++;
            }
        }

        // 막힌 방향이 많을수록 경로가 비최적
        return 1f - (blockedDirections / 4f);
    }
    public void NotifyDeath()
    {
        if (!isDead) // 중복 호출 방지
        {
            isDead = true;
            // Debug.Log($"MyAgent({controller?.playerID}): 사망 감지됨. 즉시 재시작.");
            // 약간의 지연을 두고 에피소드 종료 (상태 안정화)
            Invoke(nameof(DelayedEndEpisode), 0.1f);
        }
    }

    // // **🚨 NEW: 영역 완성 감지 및 보상 시스템**
    // public void NotifyTerritoryCompletion(int gainedTiles)
    // {
    //     if (gainedTiles > 0)
    //     {
    //         // 📈 획득한 타일 수에 비례하는 압도적인 보상 시스템
    //         float territoryReward = gainedTiles * 1.5f; // 기본 생존 보상(0.01f) 대비 150배 강력

    //         // 🎯 대규모 영역 확보 시 추가 보너스
    //         if (gainedTiles >= 50)
    //         {
    //             territoryReward += 25.0f; // 대규모 확장 보너스
    //             // Debug.Log($"[MyAgent] 🏆 MASSIVE TERRITORY! Player {controller?.playerID}: {gainedTiles} 타일 확보 + 대규모 보너스!");
    //         }
    //         else if (gainedTiles >= 20)
    //         {
    //             territoryReward += 10.0f; // 중규모 확장 보너스
    //             // Debug.Log($"[MyAgent] 🎖️ LARGE TERRITORY! Player {controller?.playerID}: {gainedTiles} 타일 확보 + 중규모 보너스!");
    //         }
    //         else if (gainedTiles >= 10)
    //         {
    //             territoryReward += 5.0f; // 소규모 확장 보너스
    //             // Debug.Log($"[MyAgent] 🥇 GOOD TERRITORY! Player {controller?.playerID}: {gainedTiles} 타일 확보 + 소규모 보너스!");
    //         }

    //         AddReward(territoryReward);
    //         // Debug.Log($"[MyAgent] 💰 TERRITORY REWARD! Player {controller?.playerID}: " +
    //         //          $"획득 타일 {gainedTiles}개 → 보상 {territoryReward:F2}점!");

    //         // 🎯 연속 영역 확보 감지 및 추가 보상
    //         RegisterTerritoryExpansion(gainedTiles);
    //     }
    // }    // **🚨 NEW: 연속 영역 확보 추적 및 효율성 보상**
    // private int consecutiveTerritoryGains = 0;
    // private float lastTerritoryTime = 0f;
    // private int totalTerritoryGainedThisEpisode = 0;

    // // **🚨 NEW: 플레이어 ID 확인용 public 프로퍼티**
    // public int PlayerID => controller?.playerID ?? -1;

    // private void RegisterTerritoryExpansion(int gainedTiles)
    // {
    //     totalTerritoryGainedThisEpisode += gainedTiles;

    //     // 빠른 연속 영역 확보 감지 (30초 이내)
    //     if (Time.time - lastTerritoryTime < 30f)
    //     {
    //         consecutiveTerritoryGains++;

    //         // 연속 확장 보너스
    //         float consecutiveBonus = consecutiveTerritoryGains * 2.0f;
    //         AddReward(consecutiveBonus);
    //         // Debug.Log($"[MyAgent] 🔥 CONSECUTIVE EXPANSION! Player {controller?.playerID}: " +
    //         //          $"연속 {consecutiveTerritoryGains}회 → 추가 보상 {consecutiveBonus:F2}점!");
    //     }
    //     else
    //     {
    //         consecutiveTerritoryGains = 1; // 첫 번째 확장으로 초기화
    //     }

    //     lastTerritoryTime = Time.time;

    //     // 에피소드 총 영역 확보 성과 보상
    //     if (totalTerritoryGainedThisEpisode >= 100)
    //     {
    //         AddReward(15.0f); // 에피소드 내 100 타일 이상 확보 시 특별 보상
    //         // Debug.Log($"[MyAgent] 👑 EPISODE MASTER! Player {controller?.playerID}: " +
    //         //          $"총 {totalTerritoryGainedThisEpisode} 타일 확보!");
    //     }
    // }

    private void DelayedEndEpisode()
    {
        EndEpisode();
    }    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        if (controller != null && action >= 0 && action < possibleActions.Length)
        {
            Vector2Int newDirection = possibleActions[action];
            Vector2Int currentPos = new Vector2Int(
                Mathf.RoundToInt(transform.localPosition.x),
                Mathf.RoundToInt(transform.localPosition.y)
            );

            Vector2Int nextPos = currentPos + newDirection;            // **🚨 절대 벽 충돌 방지 시스템**
            if (!mapManager.InBounds(nextPos))
            {
                // 벽으로 이동하려는 시도 - 초보적 실수에 강한 페널티
                AddReward(-30.0f); // 벽 충돌 시도는 초보적 실수
                Debug.LogWarning($"[MyAgent] 🚨 벽 충돌 시도 차단! 현재: {currentPos}, 시도: {nextPos}");
                
                // 안전한 방향 찾아서 강제 변경
                Vector2Int safeDirection = FindSafeDirectionFromWall(currentPos);
                if (safeDirection != Vector2Int.zero)
                {
                    newDirection = safeDirection;
                    Debug.Log($"[MyAgent] ✅ 안전한 방향으로 변경: {safeDirection}");
                }                else
                {
                    // 모든 방향이 위험하면 현재 방향 유지 (자연스럽게 사망하도록)
                    Debug.LogError("[MyAgent] ⚠️ 모든 방향이 위험! 현재 방향 유지");
                    AddReward(-40.0f); // 벽에 몰린 상황도 어느정도 초보적 실수
                    // EndEpisode()는 호출하지 않음 - 게임 로직에서 자연스럽게 사망 처리되도록
                }
            }

            // **🚨 자기 궤적 충돌 절대 방지 시스템**
            if (mapManager.InBounds(nextPos))
            {
                int nextTrail = mapManager.GetTrail(nextPos);                if (nextTrail == controller.playerID)
                {
                    // 자기 궤적 충돌 시도 - 가장 초보적인 실수에 강한 페널티
                    AddReward(-60.0f); // 자기 궤적 충돌 시도는 가장 기본적인 실수
                    Debug.LogWarning($"[MyAgent] 💀 자기 궤적 충돌 시도 차단! 현재: {currentPos}, 시도: {nextPos}");
                    
                    // 안전한 방향 찾아서 강제 변경
                    Vector2Int safeDirection = FindSafeDirectionFromTrail(currentPos);
                    if (safeDirection != Vector2Int.zero)
                    {
                        newDirection = safeDirection;
                        Debug.Log($"[MyAgent] ✅ 궤적 회피 방향으로 변경: {safeDirection}");
                    }                    else
                    {
                        Debug.LogError("[MyAgent] 💀 자기 궤적 충돌 불가피! 현재 방향 유지");
                        AddReward(-80.0f); // 자기를 구덩이로 몰아넣은 상황에 큰 페널티
                        // EndEpisode()는 호출하지 않음 - 게임 로직에서 자연스럽게 사망 처리되도록
                        // 현재 방향을 유지하여 자연스럽게 충돌하도록 함
                    }
                }
            }

            // **🚨 위협 평가 기반 향상된 보상 시스템**
            CalculateSmartRewards(newDirection, currentPos);
            controller.SetDirection(newDirection);
        }
        else
        {
            AddReward(-1.0f); // 잘못된 행동에 페널티 (10배: -0.1f → -1.0f)
        }

        // 게임 종료 체크
        if (gameManager != null && controller != null && !isDead)
        {
            float currentScore = gameManager.GetScore(controller.playerID);

            if (currentScore < 0)
            {
                // Debug.Log($"MyAgent({controller.playerID}): 점수 기반 사망 감지 (score: {currentScore})");
                NotifyDeath();
                return;
            }
            if (currentScore >= 4000) // 승리
            {
                AddReward(100.0f); // 10배 스케일링: 10.0f → 100.0f
                EndEpisode();
                return;
            }
        }
    }    public void RewardKilledByWallDeath()
    {
        // 벽에 박기 = 매우 초보적인 실수, 큰 페널티
        AddReward(-80.0f); // 초보적 실수에 강력한 페널티
        Debug.Log("[MyAgent] 💥 벽 충돌 사망 - 초보적 실수 큰 페널티");
    }

    public void RewardKilledBySelfDeath()
    {
        // 자기 꼬리 밟기 = 가장 초보적인 실수, 가장 큰 페널티
        AddReward(-100.0f); // 가장 기본적인 실수에 최대 페널티
        Debug.Log("[MyAgent] 🐍 자기 궤적 충돌 사망 - 최대 페널티");
    }

    public void RewardKilledByOthers()
    {
        // 상대의 정교한 공격이나 전략에 당함 = 작은 페널티 (학습 기회)
        AddReward(-15.0f); // 상대방의 실력에 당한 것은 작은 페널티
        Debug.Log("[MyAgent] ⚔️ 상대방에게 사망 - 전략적 패배 작은 페널티");
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;

        int selectedAction = -1;

        // IJKL 키로 에이전트 수동 제어 (conda/ONNX 둘 다 없을 때 폴백)
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

    // **기존 보상 시스템 (백업용)**
    private void CalculateSmartRewards(Vector2Int dir, Vector2Int currentPos)
    {
        Vector2Int nextPos = currentPos + dir;

        // ✅ 7. 승부 의식 기반 보상
        int myScore = mapManager.GetOwnedTileCount(controller.playerID);
        int rank = GetMyRankAmongPlayers(myScore);


        // 0. 자기 영역 안에 너무 오래 머물면 감점
        bool currentlyInOwnTerritory = mapManager.InBounds(currentPos) &&
                                       mapManager.GetTile(currentPos) == controller.playerID;

        // 1. 위협 상황에서 귀환 성공 시 보상
        bool isInSafeZone = mapManager.InBounds(nextPos) && mapManager.GetTile(nextPos) == controller.playerID; if (isInSafeZone)
        {
            AddReward(-0.1f); // 안전지대 페널티 강화 (10배 스케일링: -0.01f → -0.1f)
        }

        if (lastThreatLevel > 0.7f && isInSafeZone)
        {
            AddReward(+0.1f); // 승부 의식 보상 (10배 스케일링: +0.01f → +0.1f)
        }

        // 2. 적극적 플레이 장려 보상
        bool isLeavingSafeZone = currentlyInOwnTerritory && !isInSafeZone;
        if (isLeavingSafeZone)
        {
            AddReward(+0.15f); // 안전지대를 벗어나는 것에 대한 보상 (10배 스케일링: +0.015f → +0.15f)
        }

        // 3. trail이 너무 길고 오래 유지되었는데 아직도 안 닫았다면 패널티
        if (trailIsOpen && lastTrailLength > 40 && (Time.time - trailStartTime) > 10f)
        {
            // AddReward(-0.0015f * (1 + (4 - rank) * 0.1f));
        }

        // 4. 적 근처에서 회피 성공했는지 체크
        float enemyDistance = EstimateNearestEnemyDistance(currentPos);
        if (enemyDistance < 3f && isInSafeZone)
        {
            AddReward(+0.1f * (1 + (4 - rank) * 0.1f)); // 10배 스케일링: +0.01f → +0.1f
        }

        // ✅ 5. 점유율 변화량 보상
        int currentOwned = CountOwnedTiles(controller.playerID);
        int delta = currentOwned - prevOwnedTileCount;
        if (delta > 0)
        {

            float trailDuration = Time.time - trailStartTime;
            if (lastTrailLength > 10 && trailDuration > 5f)
            {
                AddReward(0.1f * delta); // 점령 보상 (10배 스케일링: 0.01f → 0.1f)
            }
            else
            {
                AddReward(0.05f * delta); // 점령 보상 (10배 스케일링: 0.005f → 0.05f)
            }
        }
        else if (delta < 0)
            AddReward(-0.01f * Mathf.Abs(delta)); // 점령 손실 페널티 (10배 스케일링: -0.001f → -0.01f)
        prevOwnedTileCount = currentOwned;        // ✅ 6. 전략적 공격 보상: 적 trail 차단
        int trailOwner = mapManager.GetTrail(nextPos);
        if (trailOwner != 0 && trailOwner != controller.playerID)
        {
            // 100칸 먹은 것과 동일한 고정 보상
            float reward = 1.0f; // 0.01f * 100칸과 동일

            AddReward(reward);

            // 디버깅 로그(optional)
            // Debug.Log($"🔥 적 trail 차단! 대상 ID: {trailOwner}, 보상: {reward:F2}");
        }



        // 상태 업데이트
        lastTrailLength = CountTrailTiles(controller.playerID);
        if (!trailIsOpen && lastTrailLength > 0)
        {
            trailStartTime = Time.time;
            trailIsOpen = true;
        }
    }

    private int CountTrailTiles(int playerID)
    {
        int count = 0;
        for (int x = 0; x < 100; x++)
        {
            for (int y = 0; y < 100; y++)
            {
                if (mapManager.GetTrail(new Vector2Int(x, y)) == playerID)
                    count++;
            }
        }
        return count;
    }

    private float EstimateNearestEnemyDistance(Vector2Int myPos)
    {
        float minDist = 999f;
        BasePlayerController[] allPlayers = UnityEngine.Object.FindObjectsOfType<BasePlayerController>();

        foreach (var enemy in allPlayers)
        {
            if (enemy == null || enemy.gameObject == gameObject) continue;

            var enemyTracker = enemy.GetComponent<CornerPointTracker>();
            if (enemyTracker == null || enemyTracker.playerId == controller.playerID) continue;

            Vector2Int enemyPos = new Vector2Int(
                Mathf.RoundToInt(enemy.transform.position.x),
                Mathf.RoundToInt(enemy.transform.position.y)
            );

            float dist = Vector2.Distance(myPos, enemyPos);
            if (dist < minDist) minDist = dist;
        }

        return minDist;
    }

    private int CountOwnedTiles(int playerID)
    {
        int count = 0;
        for (int x = 0; x < mapManager.width; x++)
        {
            for (int y = 0; y < mapManager.height; y++)
            {
                if (mapManager.GetTile(new Vector2Int(x, y)) == playerID)
                    count++;
            }
        }
        return count;
    }

    private int GetTotalPlayers()
    {
        return UnityEngine.Object.FindObjectsOfType<BasePlayerController>().Length;
    }

    private int GetMyRankAmongPlayers(int myScore)
    {
        var players = UnityEngine.Object.FindObjectsOfType<BasePlayerController>();
        List<int> scores = new List<int>();

        foreach (var p in players)
        {
            scores.Add(mapManager.GetOwnedTileCount(p.GetComponent<CornerPointTracker>().playerId));
        }

        scores.Sort((a, b) => b.CompareTo(a)); // 내림차순
        return scores.IndexOf(myScore) + 1;
    }    // **히스토리 업데이트**
    private void UpdateHistory(Vector2Int direction, Vector2Int position)
    {
        directionHistory.Enqueue(direction);
        if (directionHistory.Count > HISTORY_SIZE)
            directionHistory.Dequeue();

        positionHistory.Enqueue(position);
        if (positionHistory.Count > HISTORY_SIZE)
            positionHistory.Dequeue();
    }

    // **🚨 벽 충돌 회피를 위한 안전한 방향 찾기**
    private Vector2Int FindSafeDirectionFromWall(Vector2Int currentPos)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        
        foreach (var dir in directions)
        {
            Vector2Int testPos = currentPos + dir;
            
            // 경계 내부이고 자기 궤적이 아닌 곳 찾기
            if (mapManager.InBounds(testPos) && 
                mapManager.GetTrail(testPos) != controller.playerID)
            {
                return dir; // 첫 번째 안전한 방향 반환
            }
        }
        
        return Vector2Int.zero; // 안전한 방향 없음
    }

    // **🚨 자기 궤적 충돌 회피를 위한 안전한 방향 찾기**
    private Vector2Int FindSafeDirectionFromTrail(Vector2Int currentPos)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
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
                score += 100; // 내 영역으로 이동 (가장 안전)
            else if (tileOwner == 0)
                score += 50;  // 중립 지역 (보통 안전)
            else
                score += 10;  // 상대방 영역 (덜 선호하지만 안전)
            
            // 다른 궤적이 있으면 감점
            int trailOwner = mapManager.GetTrail(testPos);
            if (trailOwner != 0 && trailOwner != controller.playerID)
                score -= 30;
            
            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = dir;
            }
        }
        
        return bestDirection;
    }

    // **✅ 효율적인 영역 확장 패턴 감지**    // 180도 턴(정반대 방향) 방지: Action Masking
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (controller == null) return;
        
        // 현재 방향
        Vector2Int currentDir = controller.direction;
        
        // 반대 방향 인덱스 계산 (0:up, 1:right, 2:down, 3:left)
        int opposite = -1;
        if (currentDir == Vector2Int.up) opposite = 2;        // up의 반대는 down
        else if (currentDir == Vector2Int.right) opposite = 3;  // right의 반대는 left
        else if (currentDir == Vector2Int.down) opposite = 0;   // down의 반대는 up
        else if (currentDir == Vector2Int.left) opposite = 1;   // left의 반대는 right
        
        if (opposite >= 0)
        {
            // 해당 방향(반대 방향) 마스킹 - 선택 불가능하게 만듦
            actionMask.SetActionEnabled(0, opposite, false);
        }
    }
}