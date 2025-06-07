using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// GAIL (Generative Adversarial Imitation Learning)을 사용하는 에이전트
/// 판별자(Discriminator)가 인간과 AI의 행동을 구별하도록 학습하면서
/// 생성자(Generator, 즉 AI 에이전트)는 더 인간다운 행동을 학습합니다.
/// </summary>
public class GAILAgent : Agent
{
    [Header("GAIL Settings")]
    public bool useGAIL = true;
    public float discriminatorWeight = 0.5f; // 판별자 보상 가중치
    public float explorationBonus = 0.1f; // 탐색 보너스

    private AIPlayerController controller;
    private MapManager mapManager;
    private GameController gameManager;

    // GAIL 관련 변수
    private DiscriminatorNetwork discriminator;
    private List<StateActionPair> expertTrajectories;
    private List<StateActionPair> agentTrajectories;

    // 행동 추적
    private Queue<StateActionPair> recentActions = new Queue<StateActionPair>();
    private const int TRAJECTORY_LENGTH = 50;

    // 인간다운 행동 패턴 감지
    private HumanBehaviorAnalyzer behaviorAnalyzer;

    [System.Serializable]
    public class StateActionPair
    {
        public float[] state;
        public int action;
        public Vector2Int position;
        public float timestamp;
        public bool isExpert; // 인간 데이터인지 여부
    }

    public override void Initialize()
    {
        controller = GetComponent<AIPlayerController>();
        mapManager = FindFirstObjectByType<MapManager>();
        gameManager = GameController.Instance;

        // 판별자 네트워크 초기화
        discriminator = new DiscriminatorNetwork();

        // 행동 분석기 초기화
        behaviorAnalyzer = new HumanBehaviorAnalyzer();

        // 전문가 궤적 로드
        LoadExpertTrajectories();

        Debug.Log("[GAILAgent] GAIL 시스템 초기화 완료");
    }

    public override void OnEpisodeBegin()
    {
        // 에이전트 궤적 리셋
        agentTrajectories = new List<StateActionPair>();
        recentActions.Clear();

        // 행동 분석기 리셋
        behaviorAnalyzer.Reset();

        Debug.Log("[GAILAgent] 새 에피소드 - GAIL 학습 시작");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (controller == null || mapManager == null)
        {
            for (int i = 0; i < 50; i++) sensor.AddObservation(0f);
            return;
        }

        Vector2Int currentPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y)
        );

        // 기본 상태 정보
        sensor.AddObservation(currentPos.x / 100f);
        sensor.AddObservation(currentPos.y / 100f);
        sensor.AddObservation(controller.direction.x);
        sensor.AddObservation(controller.direction.y);

        // 전략적 상태 정보 (인간이 고려할 만한 요소들)
        AddStrategicObservations(sensor, currentPos);

        // 인간다운 행동 패턴 관찰
        AddHumanLikeObservations(sensor, currentPos);
    }

    private void AddStrategicObservations(VectorSensor sensor, Vector2Int pos)
    {
        var cornerTracker = controller.GetComponent<CornerPointTracker>();
        int myPlayerID = cornerTracker?.playerId ?? -1;

        // 현재 상황 평가
        bool isInMyTerritory = mapManager.GetTile(pos) == myPlayerID;
        sensor.AddObservation(isInMyTerritory ? 1f : 0f);

        // 위험도 평가
        float dangerLevel = CalculateDangerLevel(pos, myPlayerID);
        sensor.AddObservation(dangerLevel);

        // 기회 평가 (확장 가능한 방향)
        float opportunityLevel = CalculateOpportunityLevel(pos);
        sensor.AddObservation(opportunityLevel);

        // 효율성 평가 (현재 궤적의 효율성)
        float efficiencyLevel = CalculateEfficiencyLevel(pos, myPlayerID);
        sensor.AddObservation(efficiencyLevel);

        // 인근 적 플레이어 정보
        AddEnemyProximityInfo(sensor, pos);
    }

    private void AddHumanLikeObservations(VectorSensor sensor, Vector2Int pos)
    {
        // 인간다운 행동 특성 관찰

        // 1. 직선 선호도 (인간은 적당한 직선을 선호)
        float straightLinePreference = behaviorAnalyzer.GetStraightLinePreference();
        sensor.AddObservation(straightLinePreference);

        // 2. 안전 우선 성향 (인간은 안전을 중시)
        float safetyOrientation = behaviorAnalyzer.GetSafetyOrientation(pos);
        sensor.AddObservation(safetyOrientation);

        // 3. 탐욕적 확장 성향 (인간은 기회를 놓치지 않음)
        float greedyExpansion = behaviorAnalyzer.GetGreedyExpansionTendency(pos);
        sensor.AddObservation(greedyExpansion);

        // 4. 패턴 변화 주기 (인간은 일정한 리듬이 있음)
        float patternVariability = behaviorAnalyzer.GetPatternVariability();
        sensor.AddObservation(patternVariability);

        // 5. 반응 시간 일관성 (인간은 일정한 반응 시간)
        float reactionConsistency = behaviorAnalyzer.GetReactionConsistency();
        sensor.AddObservation(reactionConsistency);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int actionIndex = actions.DiscreteActions[0];

        // 현재 상태-행동 쌍 기록
        Vector2Int currentPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y)
        );

        float[] currentState = GetCurrentStateVector(currentPos);

        StateActionPair saPair = new StateActionPair
        {
            state = currentState,
            action = actionIndex,
            position = currentPos,
            timestamp = Time.time,
            isExpert = false
        };

        // 궤적에 추가
        agentTrajectories.Add(saPair);
        recentActions.Enqueue(saPair);

        if (recentActions.Count > TRAJECTORY_LENGTH)
        {
            recentActions.Dequeue();
        }

        // 행동 분석기 업데이트
        behaviorAnalyzer.UpdateWithAction(saPair);

        if (useGAIL)
        {
            // GAIL 보상 계산
            float gailReward = CalculateGAILReward(saPair);
            AddReward(gailReward);
        }

        // 행동 실행
        ExecuteAction(actionIndex);
    }

    private float CalculateGAILReward(StateActionPair saPair)
    {
        // 판별자를 사용하여 인간 전문가와의 유사성 평가
        float discriminatorScore = discriminator.Evaluate(saPair);

        // 판별자가 "인간 같다"고 판단할수록 높은 보상
        float imitationReward = discriminatorScore * discriminatorWeight;

        // 추가 휴리스틱 보상
        float heuristicReward = CalculateHeuristicReward(saPair);

        // 탐색 보너스 (너무 모방에만 의존하지 않도록)
        float exploration = UnityEngine.Random.value * explorationBonus;

        return imitationReward + heuristicReward + exploration;
    }

    private float CalculateHeuristicReward(StateActionPair saPair)
    {
        float reward = 0f;

        // 인간다운 행동 패턴 보상
        if (behaviorAnalyzer.IsHumanLikeBehavior(saPair))
        {
            reward += 0.1f;
        }

        // 효율적인 움직임 보상
        if (IsEfficientMovement(saPair))
        {
            reward += 0.05f;
        }

        // 안전한 행동 보상
        if (IsSafeAction(saPair))
        {
            reward += 0.03f;
        }

        return reward;
    }

    private bool IsEfficientMovement(StateActionPair saPair)
    {
        // 직선 움직임이나 효율적인 확장 패턴 감지
        if (recentActions.Count < 3) return false;

        var recent = recentActions.ToArray();
        var lastThree = new StateActionPair[] {
            recent[recent.Length - 3],
            recent[recent.Length - 2],
            recent[recent.Length - 1]
        };

        // 직선 패턴 감지
        bool isStraightLine = (lastThree[0].action == lastThree[1].action &&
                              lastThree[1].action == lastThree[2].action);

        // 효율적인 확장 패턴 (사각형 그리기 등)
        bool isRectangularPattern = DetectRectangularPattern(lastThree);

        return isStraightLine || isRectangularPattern;
    }

    private bool DetectRectangularPattern(StateActionPair[] actions)
    {
        if (actions.Length < 3) return false;

        // 간단한 직각 패턴 감지
        int[] actionSequence = { actions[0].action, actions[1].action, actions[2].action };

        // 직각 전환 패턴들 (상→우, 우→하, 하→좌, 좌→상)
        int[][] rectangularPatterns = {
            new int[] {0, 1}, // 상→우
            new int[] {1, 2}, // 우→하  
            new int[] {2, 3}, // 하→좌
            new int[] {3, 0}  // 좌→상
        };

        for (int i = 0; i < actionSequence.Length - 1; i++)
        {
            foreach (var pattern in rectangularPatterns)
            {
                if (actionSequence[i] == pattern[0] && actionSequence[i + 1] == pattern[1])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsSafeAction(StateActionPair saPair)
    {
        Vector2Int nextPos = saPair.position + GetDirectionFromAction(saPair.action);

        // 경계 체크
        if (!mapManager.InBounds(nextPos)) return false;

        // 궤적 충돌 체크
        var cornerTracker = controller.GetComponent<CornerPointTracker>();
        int myPlayerID = cornerTracker?.playerId ?? -1;

        if (mapManager.GetTrail(nextPos) == myPlayerID) return false;

        return true;
    }

    private Vector2Int GetDirectionFromAction(int action)
    {
        switch (action)
        {
            case 0: return Vector2Int.up;
            case 1: return Vector2Int.right;
            case 2: return Vector2Int.down;
            case 3: return Vector2Int.left;
            default: return Vector2Int.zero;
        }
    }

    private float[] GetCurrentStateVector(Vector2Int pos)
    {
        List<float> state = new List<float>();

        // 기본 정보
        state.Add(pos.x / 100f);
        state.Add(pos.y / 100f);
        state.Add(controller.direction.x);
        state.Add(controller.direction.y);

        // 주변 환경 (간소화)
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                Vector2Int checkPos = pos + new Vector2Int(dx, dy);
                if (mapManager.InBounds(checkPos))
                {
                    state.Add(mapManager.GetTile(checkPos));
                    state.Add(mapManager.GetTrail(checkPos));
                }
                else
                {
                    state.Add(-1f);
                    state.Add(-1f);
                }
            }
        }

        return state.ToArray();
    }

    private void ExecuteAction(int actionIndex)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        if (actionIndex >= 0 && actionIndex < directions.Length)
        {
            controller.SetDirection(directions[actionIndex]);
        }
    }

    // 간단한 계산 함수들
    private float CalculateDangerLevel(Vector2Int pos, int playerID) => 0.5f;
    private float CalculateOpportunityLevel(Vector2Int pos) => 0.5f;
    private float CalculateEfficiencyLevel(Vector2Int pos, int playerID) => 0.5f;
    private void AddEnemyProximityInfo(VectorSensor sensor, Vector2Int pos)
    {
        for (int i = 0; i < 10; i++) sensor.AddObservation(0f);
    }

    private void LoadExpertTrajectories()
    {
        // 전문가 데이터 로드 (실제 구현에서는 파일에서 로드)
        expertTrajectories = new List<StateActionPair>();
        Debug.Log("[GAILAgent] 전문가 궤적 로드 완료");
    }

    void OnGUI()
    {
        if (Application.isPlaying && useGAIL)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 250, 140, 240, 100));

            GUILayout.Label("🎭 GAIL Learning");
            GUILayout.Label($"궤적 길이: {agentTrajectories.Count}");
            GUILayout.Label($"판별자 가중치: {discriminatorWeight:F2}");

            if (behaviorAnalyzer != null)
            {
                GUILayout.Label($"인간다움: {behaviorAnalyzer.GetHumanLikenessScore():P1}");
            }

            GUILayout.EndArea();
        }
    }
}

/// <summary>
/// 간단한 판별자 네트워크 (실제로는 신경망으로 구현)
/// </summary>
public class DiscriminatorNetwork
{
    public float Evaluate(GAILAgent.StateActionPair saPair)
    {
        // 간단한 휴리스틱 기반 판별
        // 실제로는 신경망으로 학습된 판별자 사용

        float score = 0.5f; // 기본 점수

        // 인간다운 행동 패턴 감지
        if (IsReasonableAction(saPair))
        {
            score += 0.3f;
        }

        return Mathf.Clamp01(score);
    }

    private bool IsReasonableAction(GAILAgent.StateActionPair saPair)
    {
        // 합리적인 행동인지 간단히 판단
        // (실제로는 더 복잡한 로직)
        return true;
    }
}

/// <summary>
/// 인간 행동 패턴 분석기
/// </summary>
public class HumanBehaviorAnalyzer
{
    private Queue<GAILAgent.StateActionPair> actionHistory = new Queue<GAILAgent.StateActionPair>();
    private Queue<float> reactionTimes = new Queue<float>();
    private float lastActionTime = 0f;

    public void Reset()
    {
        actionHistory.Clear();
        reactionTimes.Clear();
        lastActionTime = 0f;
    }

    public void UpdateWithAction(GAILAgent.StateActionPair action)
    {
        actionHistory.Enqueue(action);
        if (actionHistory.Count > 20) actionHistory.Dequeue();

        float reactionTime = action.timestamp - lastActionTime;
        if (lastActionTime > 0)
        {
            reactionTimes.Enqueue(reactionTime);
            if (reactionTimes.Count > 10) reactionTimes.Dequeue();
        }
        lastActionTime = action.timestamp;
    }

    public float GetStraightLinePreference()
    {
        if (actionHistory.Count < 3) return 0.5f;

        var actions = actionHistory.ToArray();
        int straightCount = 0;

        for (int i = 2; i < actions.Length; i++)
        {
            if (actions[i - 2].action == actions[i - 1].action &&
                actions[i - 1].action == actions[i].action)
            {
                straightCount++;
            }
        }

        return (float)straightCount / (actions.Length - 2);
    }

    public float GetSafetyOrientation(Vector2Int pos) => 0.7f; // 간단한 구현
    public float GetGreedyExpansionTendency(Vector2Int pos) => 0.6f;
    public float GetPatternVariability() => 0.5f;

    public float GetReactionConsistency()
    {
        if (reactionTimes.Count < 3) return 0.5f;

        float[] times = reactionTimes.ToArray();
        float variance = CalculateVariance(times);

        return 1f / (1f + variance); // 분산이 낮을수록 일관성 높음
    }

    private float CalculateVariance(float[] values)
    {
        if (values.Length == 0) return 0f;

        float mean = 0f;
        foreach (float val in values) mean += val;
        mean /= values.Length;

        float variance = 0f;
        foreach (float val in values)
        {
            variance += (val - mean) * (val - mean);
        }

        return variance / values.Length;
    }

    public bool IsHumanLikeBehavior(GAILAgent.StateActionPair action)
    {
        // 종합적인 인간다움 판단
        float straightPref = GetStraightLinePreference();
        float reactionConsist = GetReactionConsistency();

        return straightPref > 0.3f && straightPref < 0.8f && // 적당한 직선 선호
               reactionConsist > 0.4f; // 어느 정도 일관성
    }

    public float GetHumanLikenessScore()
    {
        float straight = GetStraightLinePreference();
        float reaction = GetReactionConsistency();
        float safety = GetSafetyOrientation(Vector2Int.zero);

        return (straight + reaction + safety) / 3f;
    }
}
