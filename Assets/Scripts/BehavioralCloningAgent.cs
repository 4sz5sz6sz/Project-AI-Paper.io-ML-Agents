using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.IO;
using System;

/// <summary>
/// 인간 데모 데이터를 사용하여 Behavioral Cloning을 수행하는 에이전트
/// </summary>
public class BehavioralCloningAgent : Agent
{
    [Header("Behavioral Cloning Settings")]
    public bool useBehavioralCloning = true;
    public string demonstrationDataPath = "";
    public float imitationWeight = 0.8f; // 모방 가중치 (0.8 = 80% 모방, 20% 탐색)

    private AIPlayerController controller;
    private MapManager mapManager;
    private GameController gameManager;

    // 로드된 데모 데이터
    private List<HumanPlayerRecorder.DemonstrationStep> demonstrationData;
    private int currentDemoIndex = 0;
    private bool isDemoLoaded = false;

    // 행동 매핑
    private Vector2Int[] actionToDirection = new Vector2Int[]
    {
        Vector2Int.up,    // 0
        Vector2Int.right, // 1
        Vector2Int.down,  // 2
        Vector2Int.left   // 3
    };

    // 성능 추적
    private float imitationAccuracy = 0f;
    private int totalPredictions = 0;
    private int correctPredictions = 0;

    public override void Initialize()
    {
        controller = GetComponent<AIPlayerController>();
        mapManager = FindFirstObjectByType<MapManager>();
        gameManager = GameController.Instance;

        // 데모 데이터 로드
        if (useBehavioralCloning && !string.IsNullOrEmpty(demonstrationDataPath))
        {
            LoadDemonstrationData();
        }

        Debug.Log("[BehavioralCloningAgent] 초기화 완료 - 모방학습 준비됨");
    }

    public override void OnEpisodeBegin()
    {
        // 데모 인덱스 리셋
        currentDemoIndex = 0;

        // 성능 추적 리셋
        totalPredictions = 0;
        correctPredictions = 0;
        imitationAccuracy = 0f;

        Debug.Log("[BehavioralCloningAgent] 새 에피소드 시작 - 모방학습 활성화");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (controller == null || mapManager == null)
        {
            // 기본값으로 채우기
            for (int i = 0; i < GetObservationSize(); i++)
            {
                sensor.AddObservation(0f);
            }
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

        // 3x3 주변 영역 상세 정보
        AddSurroundingAreaObservations(sensor, currentPos);

        // 생존 위험도 평가
        AddDangerAssessment(sensor, currentPos);

        // 목표 지향 정보 (인간이 고려할 만한 요소들)
        AddStrategicObservations(sensor, currentPos);
    }

    private void AddSurroundingAreaObservations(VectorSensor sensor, Vector2Int playerPos)
    {
        var cornerTracker = controller.GetComponent<CornerPointTracker>();
        int myPlayerID = cornerTracker?.playerId ?? -1;

        // 3x3 영역의 각 셀 분석
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                Vector2Int checkPos = playerPos + new Vector2Int(dx, dy);

                if (mapManager.InBounds(checkPos))
                {
                    int tileOwner = mapManager.GetTile(checkPos);
                    int trailOwner = mapManager.GetTrail(checkPos);

                    // 타일 소유권 (내 것=1, 중립=0, 적=−1)
                    float tileValue = (tileOwner == myPlayerID) ? 1f :
                                     (tileOwner == 0) ? 0f : -1f;
                    sensor.AddObservation(tileValue);

                    // 궤적 위험도 (내 궤적=−10, 적 궤적=−1, 없음=0)
                    float trailValue = (trailOwner == myPlayerID) ? -10f :
                                      (trailOwner == 0) ? 0f : -1f;
                    sensor.AddObservation(trailValue);
                }
                else
                {
                    sensor.AddObservation(-2f); // 경계 밖
                    sensor.AddObservation(-2f);
                }
            }
        }
    }

    private void AddDangerAssessment(VectorSensor sensor, Vector2Int playerPos)
    {
        var cornerTracker = controller.GetComponent<CornerPointTracker>();
        int myPlayerID = cornerTracker?.playerId ?? -1;

        // 4방향 각각의 즉시 위험도
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        foreach (var dir in directions)
        {
            Vector2Int nextPos = playerPos + dir;
            float danger = 0f;

            if (!mapManager.InBounds(nextPos))
            {
                danger = 1f; // 경계 = 즉시 사망
            }
            else if (mapManager.GetTrail(nextPos) == myPlayerID)
            {
                danger = 1f; // 내 궤적 = 즉시 사망
            }
            else if (mapManager.GetTrail(nextPos) != 0)
            {
                danger = 0.3f; // 다른 궤적 = 약간 위험
            }

            sensor.AddObservation(danger);
        }

        // 안전지대까지의 거리
        float distanceToSafety = CalculateDistanceToSafety(playerPos, myPlayerID);
        sensor.AddObservation(distanceToSafety / 50f); // 정규화
    }

    private void AddStrategicObservations(VectorSensor sensor, Vector2Int playerPos)
    {
        var cornerTracker = controller.GetComponent<CornerPointTracker>();
        int myPlayerID = cornerTracker?.playerId ?? -1;

        // 현재 내가 내 영역에 있는가?
        bool isInMyTerritory = mapManager.GetTile(playerPos) == myPlayerID;
        sensor.AddObservation(isInMyTerritory ? 1f : 0f);

        // 확장 가능한 방향의 수
        int expansionOpportunities = 0;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        foreach (var dir in directions)
        {
            Vector2Int checkPos = playerPos + dir;
            if (mapManager.InBounds(checkPos) &&
                mapManager.GetTile(checkPos) == 0 &&
                mapManager.GetTrail(checkPos) == 0)
            {
                expansionOpportunities++;
            }
        }
        sensor.AddObservation(expansionOpportunities / 4f);

        // 현재 궤적 길이 (위험도 지표)
        int trailLength = 0;
        for (int x = 0; x < 100; x++)
        {
            for (int y = 0; y < 100; y++)
            {
                if (mapManager.GetTrail(new Vector2Int(x, y)) == myPlayerID)
                    trailLength++;
            }
        }
        sensor.AddObservation(trailLength / 100f); // 정규화
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int actionIndex = actions.DiscreteActions[0];

        if (useBehavioralCloning && isDemoLoaded)
        {
            // 모방학습 활용
            int imitatedAction = GetImitatedAction();

            // 가중 조합: 모방 + 탐색
            int finalAction = ShouldUseImitation() ? imitatedAction : actionIndex;

            // 성능 추적
            UpdateImitationAccuracy(actionIndex, imitatedAction);

            // 행동 실행
            ExecuteAction(finalAction);
        }
        else
        {
            // 순수 강화학습
            ExecuteAction(actionIndex);
        }
    }

    private int GetImitatedAction()
    {
        if (!isDemoLoaded || demonstrationData.Count == 0)
            return 0;

        // 현재 상황과 가장 유사한 데모 스텝 찾기
        Vector2Int currentPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y)
        );

        int bestMatchIndex = FindBestMatchingDemoStep(currentPos);

        if (bestMatchIndex >= 0 && bestMatchIndex < demonstrationData.Count)
        {
            return demonstrationData[bestMatchIndex].actionTaken;
        }

        return 0; // 기본 행동
    }

    private int FindBestMatchingDemoStep(Vector2Int currentPos)
    {
        float bestSimilarity = float.MinValue;
        int bestIndex = -1;

        // 현재 상황 벡터화
        float[] currentObservations = GetCurrentObservationVector(currentPos);

        for (int i = 0; i < demonstrationData.Count; i++)
        {
            var demoStep = demonstrationData[i];

            // 위치 유사성 (가중치 높음)
            float positionSimilarity = 1f / (1f + Vector2Int.Distance(currentPos, demoStep.playerPosition));

            // 상황 유사성
            float situationSimilarity = CalculateObservationSimilarity(currentObservations, demoStep.observations);

            // 종합 유사성
            float totalSimilarity = positionSimilarity * 0.7f + situationSimilarity * 0.3f;

            if (totalSimilarity > bestSimilarity)
            {
                bestSimilarity = totalSimilarity;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private float[] GetCurrentObservationVector(Vector2Int pos)
    {
        List<float> obs = new List<float>();

        // 간단한 상황 벡터 (위치, 방향, 주변 상황)
        obs.Add(pos.x / 100f);
        obs.Add(pos.y / 100f);
        obs.Add(controller.direction.x);
        obs.Add(controller.direction.y);

        return obs.ToArray();
    }

    private float CalculateObservationSimilarity(float[] obs1, float[] obs2)
    {
        if (obs1.Length != obs2.Length) return 0f;

        float similarity = 0f;
        for (int i = 0; i < obs1.Length; i++)
        {
            similarity += 1f - Mathf.Abs(obs1[i] - obs2[i]);
        }

        return similarity / obs1.Length;
    }

    private bool ShouldUseImitation()
    {
        // 확률적으로 모방 vs 탐색 결정
        return UnityEngine.Random.value < imitationWeight;
    }

    private void UpdateImitationAccuracy(int predictedAction, int imitatedAction)
    {
        totalPredictions++;
        if (predictedAction == imitatedAction)
        {
            correctPredictions++;
        }

        imitationAccuracy = (float)correctPredictions / totalPredictions;

        // 주기적으로 성능 보고
        if (totalPredictions % 100 == 0)
        {
            Debug.Log($"[BehavioralCloning] 모방 정확도: {imitationAccuracy:P2} ({correctPredictions}/{totalPredictions})");
        }
    }

    private void ExecuteAction(int actionIndex)
    {
        if (actionIndex >= 0 && actionIndex < actionToDirection.Length)
        {
            Vector2Int newDirection = actionToDirection[actionIndex];
            controller.SetDirection(newDirection);
        }
    }

    private float CalculateDistanceToSafety(Vector2Int pos, int playerID)
    {
        float minDistance = 999f;

        // 가까운 내 영역 찾기 (최적화된 검색 범위)
        for (int x = Mathf.Max(0, pos.x - 20); x <= Mathf.Min(99, pos.x + 20); x++)
        {
            for (int y = Mathf.Max(0, pos.y - 20); y <= Mathf.Min(99, pos.y + 20); y++)
            {
                if (mapManager.GetTile(new Vector2Int(x, y)) == playerID)
                {
                    float distance = Vector2Int.Distance(pos, new Vector2Int(x, y));
                    minDistance = Mathf.Min(minDistance, distance);
                }
            }
        }

        return minDistance;
    }

    private int GetObservationSize()
    {
        // 기본 정보(4) + 3x3 영역(18) + 방향별 위험도(4) + 전략적 정보(3) = 29차원
        return 29;
    }

    private void LoadDemonstrationData()
    {
        try
        {
            if (File.Exists(demonstrationDataPath))
            {
                string jsonData = File.ReadAllText(demonstrationDataPath);

                // JSON 파싱 (간단한 형태로 가정)
                var recordingData = JsonUtility.FromJson<RecordingDataWrapper>(jsonData);
                demonstrationData = recordingData.steps;
                isDemoLoaded = true;

                Debug.Log($"✅ 데모 데이터 로드 성공: {demonstrationData.Count}개 스텝");
            }
            else
            {
                Debug.LogWarning($"⚠️ 데모 데이터 파일을 찾을 수 없음: {demonstrationDataPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 데모 데이터 로드 실패: {e.Message}");
        }
    }

    [System.Serializable]
    private class RecordingDataWrapper
    {
        public List<HumanPlayerRecorder.DemonstrationStep> steps;
    }

    void OnGUI()
    {
        if (Application.isPlaying && useBehavioralCloning)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 120));

            GUILayout.Label("🤖 Behavioral Cloning");
            GUILayout.Label($"데모 로드: {(isDemoLoaded ? "✅" : "❌")}");
            GUILayout.Label($"모방 가중치: {imitationWeight:P0}");
            GUILayout.Label($"정확도: {imitationAccuracy:P1}");
            GUILayout.Label($"예측 수: {totalPredictions}");

            GUILayout.EndArea();
        }
    }
}
