using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;


public class MyAgent : Agent
{
    private AIPlayerController controller;
    private Vector2Int[] possibleActions = new Vector2Int[]
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    // 에피소드가 시작될 때 호출됩니다. 초기화가 필요한 경우 이곳에서 처리합니다.
    public override void OnEpisodeBegin()
    {
        // 1. 플레이어의 위치 초기화
        // 2. AI의 초기 위치 설정
        // 3. 게임 맵 리셋 (전략적인 맵 초기화)
        // 예시: 플레이어와 AI의 시작 위치를 랜덤하게 설정하고, 맵을 초기화.
    }

    public override void Initialize()
    {
        controller = GetComponent<AIPlayerController>();
    }

    // 에이전트가 환경에 대해 관찰하는 데이터를 수집하는 곳입니다.
    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. 플레이어의 현재 위치
        // 2. AI의 위치
        // 3. 점령된 영역 정보
        // 4. 장애물이나 상대방의 위치
        // 예시: 플레이어의 좌표, AI의 좌표, 점령한 땅의 영역을 센서에 추가.

        // 관찰할 데이터 추가
        // 1. 현재 위치
        sensor.AddObservation(transform.position);

        // 2. 현재 방향
        sensor.AddObservation(controller.direction);

        // 3. 주변 타일 상태
        // ... 더 많은 관찰 데이터 추가
    }

    // 에이전트가 선택한 행동을 기반으로 보상을 계산하거나 환경을 업데이트하는 곳입니다.
    public override void OnActionReceived(ActionBuffers actions)
    {
        // 1. 행동에 따른 플레이어 이동 (상, 하, 좌, 우 등)
        // 2. 플레이어가 점령한 영역을 업데이트
        // 3. 점령된 땅에 대한 보상 처리
        // 예시: 행동에 따라 플레이어 이동, 점령한 땅의 면적에 따른 보상 제공.

        // Discrete action을 방향으로 변환
        int action = actions.DiscreteActions[0];
        if (action < possibleActions.Length)
        {
            controller.SetDirection(possibleActions[action]);
        }
    }

    // 수동으로 행동을 테스트하고 싶을 때 정의할 수 있습니다.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // 1. 키보드나 마우스를 이용해 수동으로 행동 결정
        // 2. 상, 하, 좌, 우 이동 조작
        // 예시: WASD 키로 플레이어 이동, 마우스 클릭으로 AI의 행동 조작.
    }
}
