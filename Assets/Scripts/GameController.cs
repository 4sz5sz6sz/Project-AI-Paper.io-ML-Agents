using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    private Dictionary<int, int> playerScores = new();

    [SerializeField] private TextMeshProUGUI[] playerTexts;  // P1 ~ P4 UI 연결용

    // 카메라 제어 관련 변수
    private static Camera mainCamera;
    private static bool cameraFollowMode = false; // true면 특정 플레이어 추적, false면 고정
    private static int followingPlayerId = -1;


    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }
    void Start()
    {
        // 카메라 초기화
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // 메인 플레이어 찾아서 카메라 설정
        BasePlayerController[] players = FindObjectsByType<BasePlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.isMainPlayer)
            {
                if (mainCamera != null)
                {
                    mainCamera.transform.parent = player.transform;
                    mainCamera.transform.localPosition = new Vector3(0, 0, -10);
                    followingPlayerId = player.GetComponent<CornerPointTracker>()?.playerId ?? 1;
                    cameraFollowMode = true;
                }
                break;
            }
        }

        // 시작 시 초기 점수 표시
        if (MapManager.Instance != null)
            MapManager.Instance.InitializePlayerScores();
    }

    void Update()
    {
        HandleCameraControl(); // 카메라 제어 처리
    }    // 카메라 제어 입력 처리

    void SortScores()
    {
        // playerTexts가 준비되지 않았거나 최소 4개 미만일 땐 실행하지 않음 : 안전장치
        if (playerTexts == null || playerTexts.Length < 4)
            return;

        // 1) sortedScores: 플레이어 ID와 점수를 저장하는 리스트 복사
        var sortedScores = new List<KeyValuePair<int, int>>(playerScores);

        // 2) 점수 기준 내림차순 정렬
        sortedScores.Sort((a, b) => b.Value.CompareTo(a.Value));

        // 3) UI 텍스트 슬롯에 순위별로 점수 할당
        for (int i = 0; i < playerTexts.Length; i++)
        {
            if (i < sortedScores.Count)
            {
                int playerId = sortedScores[i].Key;
                int score = sortedScores[i].Value;
                playerTexts[i].text = $"P{playerId}: {score}";
            }
            else
            {
                // 점수가 없는 슬롯은 0점으로 초기화
                playerTexts[i].text = $"P{i + 1}: 0";
            }
        }
    }


    private void HandleCameraControl()
    {
        // 카메라가 없으면 다시 찾기
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return; // 여전히 없으면 종료
        }

        // 1, 2, 3, 4 키 입력으로 카메라를 특정 플레이어에게 고정
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SwitchCameraToPlayer(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SwitchCameraToPlayer(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SwitchCameraToPlayer(3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SwitchCameraToPlayer(4);
        }        // 현재 추적 중인 플레이어가 있고, 팔로우 모드라면 플레이어 상태 확인
        if (cameraFollowMode && followingPlayerId > 0)
        {
            GameObject targetPlayer = FindPlayerById(followingPlayerId);
            if (targetPlayer == null)
            {
                // 추적 중인 플레이어가 사망했으면 고정 모드로 전환
                cameraFollowMode = false;
                mainCamera.transform.parent = null;
                followingPlayerId = -1;
            }
            else if (mainCamera.transform.parent != targetPlayer.transform)
            {
                // 카메라가 올바른 플레이어에 부착되지 않은 경우에만 재부착
                mainCamera.transform.parent = targetPlayer.transform;
                mainCamera.transform.localPosition = new Vector3(0, 0, -10);
            }
        }
    }
    private static void SwitchCameraToPlayer(int playerId)
    {
        if (mainCamera == null)
        {
            return;
        }

        GameObject targetPlayer = Instance?.FindPlayerById(playerId);
        if (targetPlayer != null)
        {
            // 카메라를 해당 플레이어에게 부착
            mainCamera.transform.parent = targetPlayer.transform;
            mainCamera.transform.localPosition = new Vector3(0, 0, -10);

            followingPlayerId = playerId;
            cameraFollowMode = true;
        }
        else
        {
            // 플레이어가 없으면 현재 위치에 고정하고 팔로우 모드 해제
            if (mainCamera.transform.parent != null)
            {
                mainCamera.transform.parent = null;
            }
            cameraFollowMode = false;
            followingPlayerId = -1;
        }
    }

    public void SetScore(int playerId, int score)
    {
        playerScores[playerId] = score;
        SortScores(); // 점수 설정할 때도 정렬 수행
    }

    public int GetScore(int playerId)
    {
        return playerScores.ContainsKey(playerId) ? playerScores[playerId] : 0;
    }

    public void AddScore(int playerId, int delta)
    {
        if (!playerScores.ContainsKey(playerId))
            playerScores[playerId] = 0;

        playerScores[playerId] += delta;
        SortScores(); // 점수 변경할 때마다 정렬 수행
    }
    public void KillPlayer(int playerId, int deathType = -1)
    {
        //deathType: 1은 맵 경계 충돌, 2는 자신의 꼬리 밟음, 3은 다른 플레이어에게 궤적을 밟혀 사망, -1은 비정상 작동

        // 플레이어 오브젝트 찾기
        GameObject player = FindPlayerById(playerId);
        if (player != null)
        {
            // 현재 추적 중인 플레이어가 사망하는 경우 카메라 처리
            if (followingPlayerId == playerId && mainCamera != null)
            {
                Vector3 lastPosition = player.transform.position;

                // 카메라를 플레이어의 마지막 위치에 고정
                mainCamera.transform.parent = null; // 부모 연결 해제
                mainCamera.transform.position = new Vector3(lastPosition.x, lastPosition.y, -10f);
                cameraFollowMode = false; // 고정 모드로 전환
                followingPlayerId = -1; // 추적 대상 초기화
            }

            // MyAgent인지 확인
            MyAgent agent = player.GetComponent<MyAgent>();
            if (agent != null)
            {
                // ML-Agents인 경우: 궤적만 제거하고 영토는 유지 (재시작에서 다시 생성됨)
                if (MapManager.Instance != null)
                {
                    MapManager.Instance.ClearPlayerTrails(playerId);
                }
                switch (deathType)
                {
                    case 1:
                        // 맵 경계 충돌로 사망
                        agent.RewardKilledByWallDeath();

                        break;
                    case 2:
                        // 자신의 꼬리 밟음으로 사망
                        agent.RewardKilledBySelfDeath();

                        break;
                    case 3:
                        // 다른 플레이어에게 궤적을 밟혀 사망
                        agent.RewardKilledByOthers();

                        break;
                }
                // 즉시 사망 알림 및 재시작 (점수는 재시작에서 초기화됨)
                agent.NotifyDeath();
                // Debug.Log($"ML-Agent Player {playerId} 사망 - NotifyDeath() 호출로 즉시 재시작");
            }
            else
            {
                // 일반 플레이어인 경우: 기존처럼 처리
                // Destroy(player);

                // 사망한 플레이어의 궤적과 영토 제거
                if (MapManager.Instance != null)
                {
                    MapManager.Instance.ClearPlayerTrails(playerId);
                    MapManager.Instance.ClearPlayerTerritory(playerId);
                }

                // 점수를 -1로 설정 (사망 표시)
                // SetScore(playerId, -1);
            }
        }
    }

    public GameObject FindPlayerById(int id)
    {
        BasePlayerController[] allPlayers = FindObjectsByType<BasePlayerController>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (player.GetComponent<CornerPointTracker>()?.playerId == id)
                return player.gameObject;
        }
        return null;
    }
}
