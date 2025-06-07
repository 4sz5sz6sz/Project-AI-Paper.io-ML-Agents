using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

/// <summary>
/// 인간 플레이어의 행동을 녹화하여 모방학습용 데이터를 생성합니다.
/// </summary>
public class HumanPlayerRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    public bool isRecording = false;
    public string recordingFileName = "human_demo";

    private List<DemonstrationStep> recordedSteps = new List<DemonstrationStep>();
    private BasePlayerController playerController;
    private MapManager mapManager;
    private float recordingStartTime;
    private int stepCount = 0;

    [System.Serializable]
    public class DemonstrationStep
    {
        public float timestamp;
        public Vector2Int playerPosition;
        public Vector2Int playerDirection;
        public int actionTaken; // 0=Up, 1=Right, 2=Down, 3=Left
        public float[] observations; // 상태 관찰값들
        public float reward; // 해당 스텝에서의 보상
        public bool isTerminal; // 에피소드 종료 여부
        public string gameState; // 추가 게임 상태 정보
    }

    void Start()
    {
        playerController = GetComponent<BasePlayerController>();
        mapManager = FindFirstObjectByType<MapManager>();

        if (playerController == null)
        {
            Debug.LogError("HumanPlayerRecorder: BasePlayerController를 찾을 수 없습니다!");
            enabled = false;
        }
    }

    void Update()
    {
        if (isRecording && playerController != null)
        {
            RecordCurrentStep();
        }
        // 키보드 단축키로 녹화 제어
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ToggleRecording();
        }

        if (Input.GetKeyDown(KeyCode.F2) && !isRecording)
        {
            SaveRecording();
        }
    }

    public void StartRecording()
    {
        isRecording = true;
        recordingStartTime = Time.time;
        recordedSteps.Clear();
        stepCount = 0;
        Debug.Log("🎬 인간 플레이어 녹화 시작!");
    }

    public void StopRecording()
    {
        isRecording = false;
        Debug.Log($"🛑 녹화 종료! 총 {recordedSteps.Count}개 스텝 기록됨");
    }

    public void ToggleRecording()
    {
        if (isRecording)
            StopRecording();
        else
            StartRecording();
    }

    private void RecordCurrentStep()
    {
        // 현재 플레이어 상태 수집
        Vector2Int currentPos = new Vector2Int(
            Mathf.RoundToInt(playerController.transform.position.x),
            Mathf.RoundToInt(playerController.transform.position.y)
        );

        // 입력된 방향 감지
        int actionTaken = GetCurrentAction();

        // 관찰값 수집 (MyAgent와 동일한 방식)
        float[] observations = CollectObservations(currentPos);

        // 보상 계산 (간단한 생존 보상)
        float reward = CalculateStepReward(currentPos);

        // 데모 스텝 생성
        DemonstrationStep step = new DemonstrationStep
        {
            timestamp = Time.time - recordingStartTime,
            playerPosition = currentPos,
            playerDirection = playerController.direction,
            actionTaken = actionTaken,
            observations = observations,
            reward = reward,
            isTerminal = false, // 나중에 사망 시 true로 설정
            gameState = SerializeGameState(currentPos)
        };

        recordedSteps.Add(step);
        stepCount++;

        // 주기적으로 로그
        if (stepCount % 100 == 0)
        {
            Debug.Log($"📹 녹화 중... {stepCount}스텝 기록됨");
        }
    }

    private int GetCurrentAction()
    {
        Vector2Int currentDirection = playerController.direction;

        if (currentDirection == Vector2Int.up) return 0;
        if (currentDirection == Vector2Int.right) return 1;
        if (currentDirection == Vector2Int.down) return 2;
        if (currentDirection == Vector2Int.left) return 3;

        return -1; // 정지 상태
    }

    private float[] CollectObservations(Vector2Int playerPos)
    {
        // MyAgent와 동일한 관찰 수집 (간소화 버전)
        List<float> obs = new List<float>();

        // 기본 정보
        obs.Add(playerPos.x / 100f);
        obs.Add(playerPos.y / 100f);
        obs.Add(playerController.direction.x);
        obs.Add(playerController.direction.y);

        // 3x3 주변 영역 정보
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                Vector2Int checkPos = playerPos + new Vector2Int(dx, dy);

                if (mapManager.InBounds(checkPos))
                {
                    int tileOwner = mapManager.GetTile(checkPos);
                    int trailOwner = mapManager.GetTrail(checkPos);

                    obs.Add(tileOwner); // 타일 소유자
                    obs.Add(trailOwner); // 궤적 소유자
                }
                else
                {
                    obs.Add(-1f); // 경계 밖
                    obs.Add(-1f);
                }
            }
        }

        return obs.ToArray();
    }

    private float CalculateStepReward(Vector2Int pos)
    {
        // 기본 생존 보상
        float reward = 0.01f;

        // 내 영역에 있으면 안전 보상
        var cornerTracker = playerController.GetComponent<CornerPointTracker>();
        if (cornerTracker != null)
        {
            int myPlayerID = cornerTracker.playerId;
            if (mapManager.GetTile(pos) == myPlayerID)
            {
                reward += 0.05f;
            }
        }

        return reward;
    }

    private string SerializeGameState(Vector2Int playerPos)
    {
        // 게임 상태를 JSON 형태로 직렬화
        var gameState = new
        {
            playerPosition = playerPos,
            timestamp = Time.time,
            isInOwnTerritory = false // 계산 후 설정
        };

        return JsonUtility.ToJson(gameState);
    }

    public void SaveRecording()
    {
        if (recordedSteps.Count == 0)
        {
            Debug.LogWarning("저장할 녹화 데이터가 없습니다!");
            return;
        }

        string fileName = $"{recordingFileName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            var recordingData = new
            {
                metadata = new
                {
                    recordingDuration = recordedSteps[recordedSteps.Count - 1].timestamp,
                    totalSteps = recordedSteps.Count,
                    recordingDate = DateTime.Now.ToString(),
                    playerType = "Human"
                },
                steps = recordedSteps
            };

            string json = JsonUtility.ToJson(recordingData, true);
            File.WriteAllText(filePath, json);

            Debug.Log($"💾 녹화 데이터 저장 완료: {filePath}");
            Debug.Log($"📊 총 {recordedSteps.Count}개 스텝, {recordedSteps[recordedSteps.Count - 1].timestamp:F1}초 분량");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"녹화 데이터 저장 실패: {e.Message}");
        }
    }

    void OnGUI()
    {
        if (Application.isPlaying)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));

            GUILayout.Label($"📹 Human Player Recorder");
            GUILayout.Label($"상태: {(isRecording ? "🔴 녹화 중" : "⏹️ 정지")}");
            GUILayout.Label($"기록된 스텝: {recordedSteps.Count}");
            if (GUILayout.Button(isRecording ? "녹화 중지 (F1)" : "녹화 시작 (F1)"))
            {
                ToggleRecording();
            }

            if (!isRecording && recordedSteps.Count > 0)
            {
                if (GUILayout.Button("저장 (F2)"))
                {
                    SaveRecording();
                }
            }

            GUILayout.EndArea();
        }
    }
}
