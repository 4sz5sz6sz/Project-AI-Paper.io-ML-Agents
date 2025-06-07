using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Paper.io 게임의 에피소드 관리를 담당하는 중앙 관리자
/// 원작게임처럼 죽은 플레이어만 즉시 부활, 에피소드는 개별 관리
/// </summary>
public class EpisodeManager : MonoBehaviour
{
    public static EpisodeManager Instance { get; private set; }

    [Header("에피소드 종료 조건")]
    [Tooltip("특정 점수 도달 시 해당 Agent 에피소드 종료")]
    public int maxScore = 1000;

    [Tooltip("최대 에피소드 시간 (초, 0이면 무제한)")]
    public float maxEpisodeTime = 300f; // 5분

    [Tooltip("최대 스텝 수 (0이면 무제한)")]
    public int maxSteps = 10000;

    [Header("부활 설정")]
    [Tooltip("Agent 사망 시 부활 대기 시간 (초)")]
    public float respawnDelay = 0.1f;

    private List<MyAgent> allAgents = new List<MyAgent>();
    private Dictionary<int, float> agentEpisodeStartTimes = new Dictionary<int, float>();
    private Dictionary<int, int> agentStepCounts = new Dictionary<int, int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 모든 MyAgent 찾기 및 에피소드 시작 시간 등록
        RefreshAgentList();
        float currentTime = Time.time;

        foreach (var agent in allAgents)
        {
            int playerId = agent.PlayerID;
            if (playerId > 0)
            {
                agentEpisodeStartTimes[playerId] = currentTime;
                agentStepCounts[playerId] = 0;
            }
        }

        Debug.Log($"[EpisodeManager] 초기화 완료 - {allAgents.Count}개 Agent 등록");
    }

    void Update()
    {
        CheckIndividualEpisodeConditions();
    }

    /// <summary>
    /// 게임에 있는 모든 MyAgent를 다시 찾기
    /// </summary>
    public void RefreshAgentList()
    {
        allAgents.Clear();
        MyAgent[] agents = FindObjectsByType<MyAgent>(FindObjectsSortMode.None);
        allAgents.AddRange(agents);

        Debug.Log($"[EpisodeManager] Agent 목록 갱신: {allAgents.Count}개");
    }

    /// <summary>
    /// 개별 Agent의 에피소드 종료 조건 확인
    /// </summary>
    private void CheckIndividualEpisodeConditions()
    {
        var gameController = GameController.Instance;
        if (gameController == null) return;

        foreach (var agent in allAgents)
        {
            if (agent == null || agent.PlayerID <= 0) continue;

            int playerId = agent.PlayerID;

            // 1. 점수 제한 체크
            if (maxScore > 0)
            {
                int score = gameController.GetScore(playerId);
                if (score >= maxScore)
                {
                    Debug.Log($"[EpisodeManager] Player {playerId}가 목표 점수 {maxScore} 도달 - 에피소드 종료");
                    agent.EndEpisode();
                    continue;
                }
            }

            // 2. 시간 제한 체크 (개별 Agent별)
            if (maxEpisodeTime > 0 && agentEpisodeStartTimes.ContainsKey(playerId))
            {
                float agentElapsedTime = Time.time - agentEpisodeStartTimes[playerId];
                if (agentElapsedTime >= maxEpisodeTime)
                {
                    Debug.Log($"[EpisodeManager] Player {playerId} 시간 제한 {maxEpisodeTime}초 도달 - 에피소드 종료");
                    agent.EndEpisode();
                    continue;
                }
            }

            // 3. 스텝 제한 체크 (개별 Agent별)
            if (maxSteps > 0 && agentStepCounts.ContainsKey(playerId))
            {
                if (agentStepCounts[playerId] >= maxSteps)
                {
                    Debug.Log($"[EpisodeManager] Player {playerId} 스텝 제한 {maxSteps} 도달 - 에피소드 종료");
                    agent.EndEpisode();
                    continue;
                }
            }
        }
    }

    /// <summary>
    /// Agent 사망 알림 처리 - 죽은 Agent만 즉시 부활
    /// </summary>
    public void OnAgentDeath(MyAgent agent)
    {
        if (agent == null) return;

        int playerId = agent.PlayerID;
        Debug.Log($"[EpisodeManager] Player {playerId} 사망 - 즉시 개별 부활 처리");

        // 해당 Agent의 에피소드만 종료 (게임은 계속 진행)
        // MyAgent에서 자체적으로 EndEpisode() 호출함

        // 다음 에피소드를 위한 준비
        StartCoroutine(PrepareAgentRespawn(agent));
    }

    /// <summary>
    /// Agent 부활 준비
    /// </summary>
    private System.Collections.IEnumerator PrepareAgentRespawn(MyAgent agent)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (agent != null)
        {
            int playerId = agent.PlayerID;

            // 새로운 에피소드 시작 시간 기록
            agentEpisodeStartTimes[playerId] = Time.time;
            agentStepCounts[playerId] = 0;

            Debug.Log($"[EpisodeManager] Player {playerId} 부활 준비 완료 - 새 에피소드 시작");
        }
    }

    /// <summary>
    /// Agent가 스텝을 실행했을 때 호출
    /// </summary>
    public void OnAgentStep(int playerId)
    {
        if (agentStepCounts.ContainsKey(playerId))
        {
            agentStepCounts[playerId]++;
        }
        else
        {
            agentStepCounts[playerId] = 1;
        }
    }

    /// <summary>
    /// Agent가 스텝을 실행했을 때 호출 (playerId 없는 버전 - 호환성)
    /// </summary>
    public void OnAgentStep()
    {
        // 현재 활성화된 모든 Agent의 스텝 증가
        foreach (var agent in allAgents)
        {
            if (agent != null && agent.PlayerID > 0)
            {
                OnAgentStep(agent.PlayerID);
                break; // 한 번만 호출되도록
            }
        }
    }

    /// <summary>
    /// 새로운 Agent가 추가되었을 때 등록
    /// </summary>
    public void RegisterAgent(MyAgent agent)
    {
        if (agent != null && agent.PlayerID > 0)
        {
            int playerId = agent.PlayerID;
            agentEpisodeStartTimes[playerId] = Time.time;
            agentStepCounts[playerId] = 0;

            if (!allAgents.Contains(agent))
            {
                allAgents.Add(agent);
            }

            Debug.Log($"[EpisodeManager] Player {playerId} Agent 등록 완료");
        }
    }

    /// <summary>
    /// 특정 Agent의 에피소드 통계 정보
    /// </summary>
    public string GetAgentEpisodeStats(int playerId)
    {
        if (!agentEpisodeStartTimes.ContainsKey(playerId) || !agentStepCounts.ContainsKey(playerId))
        {
            return $"Player {playerId}: 데이터 없음";
        }

        float elapsedTime = Time.time - agentEpisodeStartTimes[playerId];
        int steps = agentStepCounts[playerId];

        return $"Player {playerId}: 시간 {elapsedTime:F1}s, 스텝 {steps}";
    }

    /// <summary>
    /// 모든 Agent의 에피소드 통계 정보
    /// </summary>
    public string GetAllAgentsStats()
    {
        string stats = "[EpisodeManager] 현재 상태:\n";
        foreach (var agent in allAgents)
        {
            if (agent != null && agent.PlayerID > 0)
            {
                stats += GetAgentEpisodeStats(agent.PlayerID) + "\n";
            }
        }
        return stats;
    }
}
