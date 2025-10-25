using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    private Dictionary<int, int> playerScores = new();
    private Color highlightColor = Color.red; // 캐릭터 점수 색상
    private Color defaultColor = Color.black; // 기본 텍스트 색상
    private int myPlayerId = 1; // 내 캐릭터

    [Header("ScoreSound")]
    AudioSource audioSource;
    public AudioClip getSound;
    public AudioClip BGM;

    [SerializeField] private TextMeshProUGUI[] playerTexts;  // P1 ~ P4 UI 연결용
    [SerializeField] private TextMeshProUGUI timerText;      // 타이머 UI 연결용

    // 카메라 제어 관련 변수
    private static Camera mainCamera;
    private static bool cameraFollowMode = false; // true면 특정 플레이어 추적, false면 고정
    private static int followingPlayerId = -1;

    // 게임 타이머 관련 변수
    private const float WINNING_TIME_LIMIT = 60f; // 1등 달성 시 타이머: 60초(1분)
    private const float TIMER_ACTIVATION_DELAY = 10f; // 게임 시작 후 10초 후 타이머 활성화
    private const float WARNING_TIME = 10f; // 10초 이하일 때 빨간색 표시
    private float gameTimer;
    private float gameStartTime; // 게임 시작 시간
    private bool isWinningTimerActive = false; // 플레이어가 1등일 때 타이머 활성화
    private bool hasWonOnce = false; // 한 번이라도 승리한 적이 있는지 체크 (0초 멈춤용)

    // 플레이어 1 통계 관련 변수
    private int player1DefeatedEnemiesCount = 0; // 플레이어 1의 적 퇴치 횟수
    private int player1DeathCount = 0;           // 플레이어 1의 사망 횟수
    private int player1HighScore = 0;            // 플레이어 1의 최고 점수


    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
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

        // 게임 상태 초기화 (새 게임 시작 시)
        isWinningTimerActive = false;
        hasWonOnce = false;
        player1DefeatedEnemiesCount = 0;
        player1DeathCount = 0;
        player1HighScore = 0;

        audioSource.clip = BGM;
        audioSource.loop = true;
        audioSource.Play();

        // 게임 시작 시간 기록
        gameStartTime = Time.time;

        // 게임 타이머 초기화 (처음엔 0으로 설정하여 타이머를 숨김)
        gameTimer = 0f;

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
        UpdateGameTimer(); // 게임 타이머 업데이트
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

        // 3) 1등 플레이어 체크 (내 플레이어가 1등인지 확인)
        bool playerIsFirst = sortedScores.Count > 0 && sortedScores[0].Key == myPlayerId;
        
        // 게임 시작 후 10초가 지났는지 확인
        bool canActivateTimer = (Time.time - gameStartTime) >= TIMER_ACTIVATION_DELAY;
        
        // 플레이어가 1등을 달성했을 때 (처음 1등이거나 다시 1등으로 돌아왔을 때)
        // 단, 게임 시작 후 10초가 지나고, 한 번도 승리하지 않은 경우에만 타이머 시작
        if (playerIsFirst && !isWinningTimerActive && canActivateTimer && !hasWonOnce)
        {
            isWinningTimerActive = true;
            gameTimer = WINNING_TIME_LIMIT; // 1분(60초) 타이머 시작
            Debug.Log($"플레이어가 1등 달성! 1분 타이머 시작! (현재 경과시간: {Time.time - gameStartTime:F1}초)");
        }
        // 1등을 빼앗겼을 때 타이머 비활성화 및 숨김 (단, 타이머가 아직 0보다 클 때만)
        else if (!playerIsFirst && isWinningTimerActive && gameTimer > 0f)
        {
            isWinningTimerActive = false;
            gameTimer = 0f; // 타이머를 0으로 설정하여 숨김
            Debug.Log("1등을 빼앗김! 타이머 숨김");
        }

        // 4) UI 텍스트 슬롯에 순위별로 점수 할당
        for (int i = 0; i < playerTexts.Length; i++)
        {
            // 색상 초기화
            playerTexts[i].color = defaultColor;
            playerTexts[i].fontStyle = FontStyles.Normal;

            if (i < sortedScores.Count)
            {
                int playerId = sortedScores[i].Key;
                int score = sortedScores[i].Value;

                playerTexts[i].text = $"P{playerId}: {score}";

                // 내 플레이어라면 강조
                if (playerId == myPlayerId)
                {
                    playerTexts[i].color = highlightColor;
                    playerTexts[i].fontStyle = FontStyles.Bold;
                }
            }
            else
            {
                // 점수 없는 슬롯
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
        if(playerId == myPlayerId)
        {
            audioSource.PlayOneShot(getSound);
            // 플레이어 1의 최고 점수 업데이트
            if (playerScores[playerId] > player1HighScore)
            {
                player1HighScore = playerScores[playerId];
            }
        }
        SortScores(); // 점수 변경할 때마다 정렬 수행
    }
    public void KillPlayer(int playerId, int deathType = -1, int killerId = -1)
    {
        //deathType: 1은 맵 경계 충돌, 2는 자신의 꼬리 밟음, 3는 다른 플레이어에게 궤적을 밟혀 사망, -1은 비정상 작동
        //killerId: 처치한 플레이어의 ID (deathType == 3일 때만 유효)

        // 플레이어 1이 다른 플레이어를 처치한 경우
        if (killerId == myPlayerId && deathType == 3)
        {
            IncrementPlayer1DefeatedEnemies();
            Debug.Log($"[GameController] 플레이어 1이 플레이어 {playerId}를 처치했습니다!");
        }

        // 플레이어 1이 사망한 경우 통계 업데이트
        if (playerId == myPlayerId)
        {
            IncrementPlayer1DeathCount();
        }

        // 처치한 플레이어에게 100점 추가 (deathType == 3일 때만, 즉 다른 플레이어를 처치했을 때)
        if (killerId > 0 && deathType == 3)
        {
            AddScore(killerId, 100);
            Debug.Log($"[GameController] 플레이어 {killerId}가 플레이어 {playerId}를 처치하여 100점 획득!");
        }

        // 플레이어 1이 다른 플레이어를 처치한 경우 (궤적을 끊어서 처치)
        // 주의: deathType == 3은 다른 플레이어에게 궤적을 밟혀 사망한 경우이므로
        // 이 경우 처치한 플레이어는 게임 로직에서 별도로 추적해야 합니다.
        // 임시로 이를 추적하기 위해 다른 플레이어 사망 시 플레이어 1이 살아있는지 체크

        // 플레이어 오브젝트 찾기
        GameObject player = FindPlayerById(playerId);
        if (player != null)
        {
            // 현재 추적 중인 플레이어가 사망하는 경우 카메라 처리
            if (followingPlayerId == playerId && mainCamera != null)
            {
                if (playerId == 1) // 플레이어 1이 죽은 경우
                {
                    // 잠시 후에 자동으로 다시 플레이어 1을 따라가도록 설정
                    Invoke("ReattachCameraToPlayer1", 1.0f);
                }
                else // 다른 플레이어가 죽은 경우 기존 로직 유지
                {
                    Vector3 lastPosition = player.transform.position;
                    mainCamera.transform.parent = null;
                    mainCamera.transform.position = new Vector3(lastPosition.x, lastPosition.y, -10f);
                    cameraFollowMode = false;
                    followingPlayerId = -1;
                }
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

    private void ReattachCameraToPlayer1()
    {
        GameObject player1 = FindPlayerById(1);
        if (player1 != null)
        {
            followingPlayerId = 1;
            cameraFollowMode = true;
            mainCamera.transform.parent = player1.transform;
            mainCamera.transform.localPosition = new Vector3(0, 0, -10f);
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

    /// <summary>
    /// 플레이어 1의 적 퇴치 횟수를 증가시킵니다.
    /// </summary>
    public void IncrementPlayer1DefeatedEnemies()
    {
        player1DefeatedEnemiesCount++;
        Debug.Log($"Player 1 defeated an enemy. Total: {player1DefeatedEnemiesCount}");
    }

    /// <summary>
    /// 플레이어 1의 사망 횟수를 증가시킵니다.
    /// </summary>
    public void IncrementPlayer1DeathCount()
    {
        player1DeathCount++;
        Debug.Log($"Player 1 died. Total: {player1DeathCount}");
    }

    /// <summary>
    /// 게임 타이머 업데이트 (Time: 01:00 형식으로 표시)
    /// </summary>
    private void UpdateGameTimer()
    {
        // 플레이어가 1등일 때만 1분(60초) 타이머 감소
        if (isWinningTimerActive)
        {
            gameTimer -= Time.deltaTime;
            
            Debug.Log($"[GameController] 타이머 진행 중: {gameTimer:F2}초");
            
            // 1분 타이머가 다 지났으면 (0초에서 멈춤)
            if (gameTimer <= 0f)
            {
                gameTimer = 0f;
                isWinningTimerActive = false; // 타이머 비활성화
                hasWonOnce = true; // 승리 플래그 설정 (재시작 방지)
                
                Debug.Log("=====================================");
                Debug.Log("[GameController] 1분 타이머 종료! 승리! EndScene으로 전환합니다.");
                Debug.Log("=====================================");
                
                // EndScene으로 전환
                LoadEndScene();
            }
        }

        // 타이머 UI 업데이트 (게임 타이머가 0보다 클 때만 표시)
        if (timerText != null)
        {
            if (gameTimer > 0f)
            {
                // 타이머가 활성화되어 있을 때만 표시
                int minutes = Mathf.FloorToInt(gameTimer / 60f);
                int seconds = Mathf.FloorToInt(gameTimer % 60f);
                timerText.text = $"Time: {minutes:00}:{seconds:00}";
                
                // 10초 이하면 빨간색으로 표시
                if (gameTimer <= WARNING_TIME)
                {
                    timerText.color = Color.red;
                }
                else
                {
                    timerText.color = Color.black;
                }
            }
            else
            {
                // 타이머가 0이면 숨김
                timerText.text = "";
            }
        }
    }

    /// <summary>
    /// EndScene으로 전환하는 메서드
    /// </summary>
    private void LoadEndScene()
    {
        Debug.Log("[GameController] LoadEndScene 메서드 호출됨!");
        
        // 플레이어 1의 통계를 PlayerPrefs에 저장
        Debug.Log($"[GameController] 통계 저장: 퇴치={player1DefeatedEnemiesCount}, 최고점수={player1HighScore}, 사망={player1DeathCount}");
        
        PlayerPrefs.SetInt("Player1DefeatedEnemies", player1DefeatedEnemiesCount);
        PlayerPrefs.SetInt("Player1HighScore", player1HighScore);
        PlayerPrefs.SetInt("Player1DeathCount", player1DeathCount);
        PlayerPrefs.Save();
        
        Debug.Log("[GameController] EndScene 로드 시도 중...");
        SceneManager.LoadScene("EndScene");
    }
}
