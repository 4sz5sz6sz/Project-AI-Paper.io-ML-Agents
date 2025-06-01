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

        // 예: 플레이어 오브젝트 비활성화
        GameObject player = FindPlayerById(playerId);
        if (player != null)
        {
            // Destroy(player);
        }

        // 점수 초기화하거나 사망 처리 추가
        SetScore(playerId, -1);
    }

    public GameObject FindPlayerById(int id)
    {
        BasePlayerController[] allPlayers = FindObjectsOfType<BasePlayerController>();
        foreach (var player in allPlayers)
        {
            if (player.GetComponent<CornerPointTracker>()?.playerId == id)
                return player.gameObject;
        }
        return null;
    }
}
