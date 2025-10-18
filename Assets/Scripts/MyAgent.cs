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

            // 벽 충돌 방지 시스템
            if (!mapManager.InBounds(nextPos))
            {
                AddReward(-0.2f); // 벽 충돌 시도 페널티

                // 안전한 방향 찾아서 강제 변경
                Vector2Int safeDirection = FindSafeDirectionFromWall(currentPos);
                if (safeDirection != Vector2Int.zero)
                {
                    newDirection = safeDirection;
                }
            }

            // 자기 궤적 충돌 방지 시스템
            if (mapManager.InBounds(nextPos))
            {
                int nextTrail = mapManager.GetTrail(nextPos);
                if (nextTrail == controller.playerID)
                {
                    // 안전한 방향 찾아서 강제 변경
                    Vector2Int safeDirection = FindSafeDirectionFromTrail(currentPos);
                    if (safeDirection != Vector2Int.zero)
                    {
                        newDirection = safeDirection;
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
    
    // 사망 유형별 보상 함수
    public void RewardKilledByWallDeath()
    {
        AddReward(-5.0f); // 벽 충돌 사망 페널티
    }

    public void RewardKilledBySelfDeath()
    {
        AddReward(-5.0f); // 자기 궤적 충돌 사망 페널티
    }

    public void RewardKilledByOthers()
    {
        AddReward(-2.5f); // 상대에게 당한 경우 페널티 (상대적으로 작음)
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
    /// 보상 함수 계산
    /// </summary>
    private void CalculateSmartRewards(Vector2Int dir, Vector2Int currentPos)
    {
        Vector2Int nextPos = currentPos + dir;

        // 0. 자기 영역 안에 너무 오래 머물면 감점
        bool currentlyInOwnTerritory = mapManager.InBounds(currentPos) &&
                                       mapManager.GetTile(currentPos) == controller.playerID;
        if (currentlyInOwnTerritory && (Time.time - trailStartTime) > 1f)
        {
            AddReward(-0.2f); // 안전지대에 너무 오래 머물면 페널티
        }

        if (mapManager.InBounds(nextPos))
        {
            int myScore = mapManager.GetOwnedTileCount(controller.playerID);
            int rank = GetMyRankAmongPlayers(myScore);

            // 1. 안전지대 진입/체류 보상
            bool isInSafeZone = mapManager.InBounds(nextPos) && mapManager.GetTile(nextPos) == controller.playerID;
            if (isInSafeZone)
            {
                AddReward(-0.1f); // 안전지대 페널티
            }

            // 2. 안전지대 이탈 보상 (적극적 플레이 장려)
            bool isLeavingSafeZone = currentlyInOwnTerritory && !isInSafeZone;
            if (isLeavingSafeZone)
            {
                AddReward(+0.3f);
            }

            // 3. 궤적 너무 길게 유지 페널티
            if (trailIsOpen && lastTrailLength > 40)
            {
                AddReward(-0.05f);
            }

            // 4. 적 근처에서 안전 귀환 성공
            float enemyDistance = EstimateNearestEnemyDistance(currentPos);
            if (enemyDistance < 3f && isInSafeZone)
            {
                AddReward(+0.1f * (1 + (4 - rank) * 0.1f));
            }

            // 5. 영역 확장/손실 보상
            int currentOwned = CountOwnedTiles(controller.playerID);
            int delta = currentOwned - prevOwnedTileCount;
            if (delta > 0)
            {
                float trailDuration = Time.time - trailStartTime;
                if (lastTrailLength > 10 && trailDuration > 5f)
                {
                    AddReward(0.5f * delta); // 큰 영역 점령
                }
                else
                {
                    AddReward(0.25f * delta); // 작은 영역 점령
                }
            }
            else if (delta < 0)
            {
                AddReward(-0.25f * Mathf.Abs(delta)); // 영역 손실
            }
            prevOwnedTileCount = currentOwned;

            // 6. 적 궤적 차단 보상
            int trailOwner = mapManager.GetTrail(nextPos);
            if (trailOwner != 0 && trailOwner != controller.playerID)
            {
                float reward = 0.25f * mapManager.GetOwnedTileCount(trailOwner);
                AddReward(reward);
            }
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
    /// 벽 충돌 회피를 위한 안전한 방향 찾기
    /// </summary>
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

    /// <summary>
    /// 자기 궤적 충돌 회피를 위한 안전한 방향 찾기
    /// </summary>
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