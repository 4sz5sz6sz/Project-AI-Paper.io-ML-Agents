using TMPro;
using UnityEngine;
using System.Collections.Generic;

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

        if (playerTexts != null && playerId >= 1 && playerId <= playerTexts.Length)
        {
            playerTexts[playerId - 1].text = $"P{playerId}: {score}";
        }
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

        if (playerTexts != null && playerId >= 1 && playerId <= playerTexts.Length)
        {
            playerTexts[playerId - 1].text = $"P{playerId}: {playerScores[playerId]}";
        }
    }
    public void KillPlayer(int playerId)
    {
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

            // 플레이어 오브젝트 파괴
            Destroy(player);

            // 사망한 플레이어의 궤적 제거
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ClearPlayerTrails(playerId);
                // 영토도 제거
                MapManager.Instance.ClearPlayerTerritory(playerId);
            }
        }

        // 점수를 -1로 설정 (사망 표시)
        SetScore(playerId, -1);
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
