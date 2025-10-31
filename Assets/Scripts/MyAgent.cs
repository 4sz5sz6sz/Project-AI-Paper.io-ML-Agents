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
    private const int HISTORY_SIZE = 4;

    private bool isDead = false;
    private const int MAX_STEPS_WITHOUT_PROGRESS = 500;
    private int stepsWithoutProgress = 0;
    private float previousScore = 0f;
    private Vector2Int previousPosition = Vector2Int.zero;

    // **영역 확보 추적 변수들**
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

        // Debug.Log("[MyAgent] Initialize 완료 - Camera Sensor (84x84 이미지) + Vector 8차원");
    }
    public override void OnEpisodeBegin()
    {
        // Debug.Log($"[MyAgent] Player {controller?.playerID} 에피소드 시작");

        // **상태 초기화**

        // 영역 추적 변수 초기화
        lastThreatLevel = 0f;
        lastTrailLength = 0;
        trailStartTime = 0f;
        trailIsOpen = false;
        prevOwnedTileCount = 0;

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
    }

    /// <summary>
    /// Vector 관찰 수집 (8차원)
    /// 주요 관찰은 Camera Sensor를 통한 84x84 이미지
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (controller == null || mapManager == null)
        {
            for (int i = 0; i < 8; i++) sensor.AddObservation(0f);
            return;
        }

        int agentGridX = Mathf.RoundToInt(transform.localPosition.x);
        int agentGridY = Mathf.RoundToInt(transform.localPosition.y);
        int myPlayerID = controller.playerID;

        // 1. 위치 정보 (2차원)
        sensor.AddObservation(Mathf.Clamp01(agentGridX / 100f));
        sensor.AddObservation(Mathf.Clamp01(agentGridY / 100f));
        
        // 2. 이동 방향 (2차원)
        sensor.AddObservation(controller.direction.x);
        sensor.AddObservation(controller.direction.y);
        
        // 3. 현재 점수 (1차원)
        float currentScore = gameManager?.GetScore(myPlayerID) ?? 0f;
        sensor.AddObservation(currentScore / 10000f);

        Vector2Int currentPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y)
        );

        // 4. 상태 정보 (3차원)
        bool isTrailing = mapManager.GetTrail(currentPos) == myPlayerID;
        bool isInSafeZone = mapManager.GetTile(currentPos) == myPlayerID;
        float trailDuration = isTrailing ? (Time.time - trailStartTime) / 5 : 0;

        sensor.AddObservation(isTrailing);      // 궤적 남기는 중인지
        sensor.AddObservation(isInSafeZone);    // 안전 영역에 있는지
        sensor.AddObservation(trailDuration);   // 궤적 지속 시간

        // 총 8차원 (위치2 + 방향2 + 점수1 + 상태3)
        // + Camera Sensor: 84x84 PNG 이미지로 주변 환경 시각 정보 제공
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

            Vector2Int nextPos = currentPos + newDirection;

            // 현재 자신의 영역 밖에 있는지 확인
            bool isOutsideTerritory = mapManager.GetTile(currentPos) != controller.playerID;

            if (isOutsideTerritory)
            {
                // 가장 가까운 자신의 영역 찾기
                Vector2Int nearestTerritory = FindNearestOwnTerritory(currentPos);

                // 현재 위치에서 가장 가까운 영역으로의 방향
                Vector2Int directionToTerritory = new Vector2Int(
                    Mathf.Clamp(nearestTerritory.x - currentPos.x, -1, 1),
                    Mathf.Clamp(nearestTerritory.y - currentPos.y, -1, 1)
                );

                // 선택한 방향이 영역으로 향하는 방향과 얼마나 일치하는지 계산
                Vector2 dirVector = new Vector2(newDirection.x, newDirection.y);
                Vector2 targetVector = new Vector2(directionToTerritory.x, directionToTerritory.y).normalized;
                float alignment = Vector2.Dot(dirVector, targetVector);

                // 올바른 방향으로 이동하면 보상 (1에 가까울수록 정확한 방향)
                if (alignment > 0)
                {
                    AddReward(0.05f * alignment);  // 정확한 방향일수록 더 큰 보상
                }
            }

            // === 벽 충돌 방지 시스템 (강화) ===
            if (!mapManager.InBounds(nextPos))
            {
                // 벽으로 가려는 시도에 즉각 큰 페널티
                AddReward(-2.0f);
                Debug.LogWarning($"[Safety] Player {controller.playerID} 벽 충돌 시도! 방향 강제 변경");

                // 안전한 방향 찾아서 강제 변경
                Vector2Int safeDirection = FindSafeDirectionFromWall(currentPos);
                if (safeDirection != Vector2Int.zero)
                {
                    newDirection = safeDirection;
                    nextPos = currentPos + newDirection; // nextPos 업데이트
                }
                else
                {
                    // 안전한 방향이 없으면 현재 방향 유지
                    newDirection = controller.direction;
                    nextPos = currentPos + newDirection;
                }
            }

            // === 자기 궤적 충돌 방지 시스템 (강화) ===
            if (mapManager.InBounds(nextPos))
            {
                int nextTrail = mapManager.GetTrail(nextPos);
                if (nextTrail == controller.playerID)
                {
                    // 자기 궤적으로 가려는 시도에 즉각 큰 페널티
                    AddReward(-2.0f);
                    Debug.LogWarning($"[Safety] Player {controller.playerID} 자기 궤적 충돌 시도! 방향 강제 변경");

                    // 안전한 방향 찾아서 강제 변경
                    Vector2Int safeDirection = FindSafeDirectionFromTrail(currentPos);
                    if (safeDirection != Vector2Int.zero)
                    {
                        newDirection = safeDirection;
                    }
                    else
                    {
                        // 안전한 방향이 없으면 현재 방향 유지
                        newDirection = controller.direction;
                    }
                }
            }

            // 보상 계산
            CalculateSmartRewards(newDirection, currentPos);
            controller.SetDirection(newDirection);
        }
        else
        {
            // AddReward(-1.0f); // 잘못된 행동에 페널티 (10배: -0.1f → -1.0f)
        }

        // 게임 종료 체크
        if (gameManager != null && controller != null && !isDead)
        {
            float currentScore = gameManager.GetScore(controller.playerID);

            // if (currentScore < 0)
            // {
            //     // Debug.Log($"MyAgent({controller.playerID}): 점수 기반 사망 감지 (score: {currentScore})");
            //     NotifyDeath();
            //     return;
            // }
            // if (currentScore >= 4000) // 승리
            // {
            //     AddReward(100.0f); // 10배 스케일링: 10.0f → 100.0f
            //     EndEpisode();
            //     return;
            // }
        }
    }
    
    // 사망 유형별 보상 함수 (페널티 균형 조정)
    public void RewardKilledByWallDeath()
    {
        AddReward(-5.0f); // -10.0 → -5.0 (생존 보상과 균형)
        Debug.Log($"[Death] Player {controller.playerID} 벽 충돌 사망! 페널티: -5.0");
    }

    public void RewardKilledBySelfDeath()
    {
        AddReward(-5.0f); // -10.0 → -5.0 (생존 보상과 균형)
        Debug.Log($"[Death] Player {controller.playerID} 자기 궤적 사망! 페널티: -5.0");
    }

    public void RewardKilledByOthers()
    {
        AddReward(-3.0f); // -5.0 → -3.0 (상대적으로 덜 심각)
        Debug.Log($"[Death] Player {controller.playerID} 적에게 사망! 페널티: -3.0");
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;

        int selectedAction = -1;

        // IJKL 키로 수동 제어
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

    /// <summary>
    /// 보상 함수 계산 (간소화 및 명확화)
    /// </summary>
    private void CalculateSmartRewards(Vector2Int dir, Vector2Int currentPos)
    {
        Vector2Int nextPos = currentPos + dir;

        // === 기본 생존 보상 (증가) ===
        AddReward(0.01f); // 0.001 → 0.01 (10배 증가, 생존 가치 향상)

        if (!mapManager.InBounds(nextPos))
        {
            // 벽 충돌 시도 시 페널티
            AddReward(-0.5f); // -1.0 → -0.5 (즉각 페널티는 약하게)
            return;
        }

        bool currentlyInOwnTerritory = mapManager.InBounds(currentPos) &&
                                       mapManager.GetTile(currentPos) == controller.playerID;
        bool isInSafeZone = mapManager.GetTile(nextPos) == controller.playerID;

        // === 1. 안전지대 vs 위험지대 보상 ===
        if (isInSafeZone)
        {
            // 안전지대에서 너무 오래 머물면 페널티
            int trailLength = CountTrailTiles(controller.playerID);
            if (trailLength == 0)
            {
                AddReward(-0.02f); // 궤적 없이 안전지대 배회
            }
        }
        else
        {
            // 위험 지대로 나가는 것은 긍정적 (영역 확장 기회)
            if (currentlyInOwnTerritory)
            {
                AddReward(+0.2f); // +0.1 → +0.2 (적극적 플레이 더 장려)
            }
        }

        // === 2. 자기 궤적 밟기 방지 (매우 중요!) ===
        int nextTrail = mapManager.GetTrail(nextPos);
        if (nextTrail == controller.playerID)
        {
            AddReward(-1.0f); // -2.0 → -1.0 (즉각 페널티 감소)
            return;
        }

        // === 3. 적 궤적 차단 보상 (공격적 플레이 장려) ===
        if (nextTrail != 0 && nextTrail != controller.playerID)
        {
            // 적의 궤적을 밟으면 큰 보상
            int enemyOwnedTiles = mapManager.GetOwnedTileCount(nextTrail);
            float reward = Mathf.Clamp(1.0f + enemyOwnedTiles * 0.02f, 1.0f, 10.0f); // 기본 보상 증가
            AddReward(reward);
            Debug.Log($"[Reward] Player {controller.playerID}가 Player {nextTrail}의 궤적 차단! +{reward:F2}");
        }

        // === 4. 영역 확장 보상 (가장 중요한 목표) ===
        int currentOwned = CountOwnedTiles(controller.playerID);
        int delta = currentOwned - prevOwnedTileCount;
        
        if (delta > 0)
        {
            // 영역이 늘어나면 큰 보상
            float expansionReward = Mathf.Clamp(delta * 1.0f, 0.5f, 20.0f); // 0.5 → 1.0 (2배), 최대 10 → 20
            AddReward(expansionReward);
            Debug.Log($"[Reward] Player {controller.playerID} 영역 확장! +{delta} 타일, 보상: +{expansionReward:F2}");
            
            // 궤적 닫기 성공 후 상태 리셋
            trailIsOpen = false;
            lastTrailLength = 0;
        }
        else if (delta < 0)
        {
            // 영역 손실 시 페널티
            AddReward(-0.3f * Mathf.Abs(delta)); // -0.5 → -0.3 (페널티 감소)
        }
        
        prevOwnedTileCount = currentOwned;

        // === 5. 궤적 관리 (너무 길면 위험) ===
        int currentTrailLength = CountTrailTiles(controller.playerID);
        if (currentTrailLength > 0)
        {
            if (!trailIsOpen)
            {
                trailIsOpen = true;
                trailStartTime = Time.time;
            }

            // 궤적이 너무 길어지면 점점 더 큰 페널티
            if (currentTrailLength > 30)
            {
                AddReward(-0.05f * (currentTrailLength - 30) / 10f); // -0.1 → -0.05
            }

            // 시간이 너무 오래 걸리면 추가 페널티
            float trailDuration = Time.time - trailStartTime;
            if (trailDuration > 10f)
            {
                AddReward(-0.1f); // -0.2 → -0.1
            }
        }
        
        lastTrailLength = currentTrailLength;
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
    }

    private void UpdateHistory(Vector2Int direction, Vector2Int position)
    {
        directionHistory.Enqueue(direction);
        if (directionHistory.Count > HISTORY_SIZE)
            directionHistory.Dequeue();

        positionHistory.Enqueue(position);
        if (positionHistory.Count > HISTORY_SIZE)
            positionHistory.Dequeue();
    }

    /// <summary>
    /// 벽 충돌 회피를 위한 안전한 방향 찾기 (10칸 광선 기반)
    /// </summary>
    private Vector2Int FindSafeDirectionFromWall(Vector2Int currentPos)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        Vector2Int bestDirection = Vector2Int.zero;
        int bestScore = -9999;

        foreach (var dir in directions)
        {
            int score = 0;
            bool hitWall = false;
            bool hitMyTrail = false;
            int safeDistance = 0;

            // 이 방향으로 10칸까지 광선 발사
            for (int i = 1; i <= 10; i++)
            {
                Vector2Int checkPos = currentPos + dir * i;

                // 벽 체크
                if (!mapManager.InBounds(checkPos))
                {
                    hitWall = true;
                    safeDistance = i - 1; // 벽 직전까지 안전
                    break;
                }

                // 내 궤적 체크
                if (mapManager.GetTrail(checkPos) == controller.playerID)
                {
                    hitMyTrail = true;
                    safeDistance = i - 1; // 내 궤적 직전까지 안전
                    break;
                }

                safeDistance = i; // 여기까지는 안전
            }

            // 점수 계산: 안전거리가 길수록 좋음
            score = safeDistance * 10;

            // 벽이나 내 궤적에 바로 부딪히면 큰 감점
            if (safeDistance == 0)
            {
                score = -1000;
            }

            // 추가 보너스: 끝 지점이 내 영역이면 더 안전
            if (safeDistance > 0)
            {
                Vector2Int endPos = currentPos + dir * safeDistance;
                if (mapManager.InBounds(endPos))
                {
                    int tileOwner = mapManager.GetTile(endPos);
                    if (tileOwner == controller.playerID)
                    {
                        score += 50; // 내 영역으로 돌아가는 경로
                    }
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = dir;
            }
        }

        if (bestScore == -9999)
        {
            Debug.LogError($"[Safety] Player {controller.playerID}: 모든 방향이 막힘! 현재 방향 유지");
            return controller.direction; // 최악의 경우 현재 방향
        }

        Debug.Log($"[Safety] Player {controller.playerID}: 최적 방향 {bestDirection}, 점수: {bestScore}");
        return bestDirection;
    }

    /// <summary>
    /// 자기 궤적 충돌 회피를 위한 안전한 방향 찾기 (10칸 광선 기반)
    /// </summary>
    private Vector2Int FindSafeDirectionFromTrail(Vector2Int currentPos)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        Vector2Int bestDirection = Vector2Int.zero;
        int bestScore = -9999;

        foreach (var dir in directions)
        {
            int score = 0;
            bool hitWall = false;
            bool hitMyTrail = false;
            int safeDistance = 0;

            // 이 방향으로 10칸까지 광선 발사
            for (int i = 1; i <= 10; i++)
            {
                Vector2Int checkPos = currentPos + dir * i;

                // 벽 체크
                if (!mapManager.InBounds(checkPos))
                {
                    hitWall = true;
                    safeDistance = i - 1;
                    break;
                }

                // 내 궤적 체크
                if (mapManager.GetTrail(checkPos) == controller.playerID)
                {
                    hitMyTrail = true;
                    safeDistance = i - 1;
                    break;
                }

                safeDistance = i;
            }

            // 점수 계산: 안전거리가 길수록 좋음
            score = safeDistance * 10;

            // 즉시 충돌하면 큰 감점
            if (safeDistance == 0)
            {
                score = -1000;
            }

            // 추가 보너스: 끝 지점의 안전도
            if (safeDistance > 0)
            {
                Vector2Int endPos = currentPos + dir * safeDistance;
                if (mapManager.InBounds(endPos))
                {
                    int tileOwner = mapManager.GetTile(endPos);
                    int trailOwner = mapManager.GetTrail(endPos);

                    if (tileOwner == controller.playerID)
                    {
                        score += 100; // 내 영역으로 돌아가는 경로 (최고 안전)
                    }
                    else if (tileOwner == 0)
                    {
                        score += 50; // 중립 지역 (안전)
                    }
                    else
                    {
                        score += 10; // 적 영역 (덜 선호)
                    }

                    // 적 궤적이 있으면 약간 감점
                    if (trailOwner != 0 && trailOwner != controller.playerID)
                    {
                        score -= 20;
                    }
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = dir;
            }
        }

        if (bestScore == -9999)
        {
            Debug.LogError($"[Safety] Player {controller.playerID}: 탈출 불가능! 현재 방향 유지");
            return controller.direction;
        }

        Debug.Log($"[Safety] Player {controller.playerID}: 최적 탈출 방향 {bestDirection}, 점수: {bestScore}");
        return bestDirection;
    }

    /// <summary>
    /// 180도 턴(정반대 방향) 방지: Action Masking
    /// </summary>
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
            actionMask.SetActionEnabled(0, opposite, false);
        }
    }

    /// <summary>
    /// 가장 가까운 자신의 영역 위치 찾기
    /// </summary>
    private Vector2Int FindNearestOwnTerritory(Vector2Int currentPos)
    {
        Vector2Int nearest = currentPos;
        float minDistance = float.MaxValue;

        // 적절한 탐색 범위 설정 (현재 위치에서 상하좌우 20칸)
        int searchRange = 20;
        int startX = Mathf.Max(0, currentPos.x - searchRange);
        int endX = Mathf.Min(mapManager.width - 1, currentPos.x + searchRange);
        int startY = Mathf.Max(0, currentPos.y - searchRange);
        int endY = Mathf.Min(mapManager.height - 1, currentPos.y + searchRange);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                Vector2Int checkPos = new Vector2Int(x, y);
                if (mapManager.GetTile(checkPos) == controller.playerID)
                {
                    float distance = Vector2.Distance(currentPos, checkPos);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = checkPos;
                    }
                }
            }
        }
        return nearest;
    }
}