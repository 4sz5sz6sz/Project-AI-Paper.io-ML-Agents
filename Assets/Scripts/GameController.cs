using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    private Dictionary<int, int> playerScores = new();

    [SerializeField] private TextMeshProUGUI[] playerTexts;  // P1 ~ P4 UI 연결용

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        // 시작 시 초기 점수 표시
        if (MapManager.Instance != null)
            MapManager.Instance.InitializePlayerScores();
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
        Debug.Log($"💀 플레이어 {playerId}가 사망했습니다.");

        // 플레이어 오브젝트 찾기
        GameObject player = FindPlayerById(playerId);
        if (player != null)
        {
            BasePlayerController playerController = player.GetComponent<BasePlayerController>();

            // 메인 플레이어(카메라를 가진 플레이어)인지 확인
            if (playerController != null && playerController.isMainPlayer)
            {
                // 카메라를 플레이어의 마지막 위치에 고정
                var camera = Camera.main;
                if (camera != null)
                {
                    camera.transform.parent = null; // 부모 연결 해제
                    camera.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
                    Debug.Log($"📷 카메라를 플레이어 {playerId}의 마지막 위치에 고정: {camera.transform.position}");
                }
            }

            // 플레이어 오브젝트 파괴
            Destroy(player);
        }

        // 점수 초기화하거나 사망 처리 추가
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
