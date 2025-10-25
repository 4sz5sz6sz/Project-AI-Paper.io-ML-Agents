using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class EndSceneManager : MonoBehaviour
{
    // Unity Inspector에서 할당할 TextMeshProUGUI 컴포넌트들
    [SerializeField] private TextMeshProUGUI defeatedEnemiesText;
    [SerializeField] private TextMeshProUGUI highScoreText;
    [SerializeField] private TextMeshProUGUI deathCountText;

    void Start()
    {
        // 테스트용 로그
        Debug.Log("=====================================");
        Debug.Log("[EndSceneManager] Start() 호출됨!");
        Debug.Log("[EndSceneManager] GameObject 이름: " + gameObject.name);
        Debug.Log("[EndSceneManager] 스크립트 활성화: " + enabled);
        Debug.Log("=====================================");

        // PlayerPrefs에서 저장된 통계 불러오기
        int defeatedEnemies = PlayerPrefs.GetInt("Player1DefeatedEnemies", 0);
        int highScore = PlayerPrefs.GetInt("Player1HighScore", 0);
        int deathCount = PlayerPrefs.GetInt("Player1DeathCount", 0);

        Debug.Log($"[EndSceneManager] 통계 로드: 퇴치={defeatedEnemies}, 최고점수={highScore}, 사망={deathCount}");

        // UI 텍스트 업데이트
        if (defeatedEnemiesText != null)
        {
            defeatedEnemiesText.text = $"적 퇴치 횟수: {defeatedEnemies}";
        }
        else
        {
            Debug.LogWarning("[EndSceneManager] defeatedEnemiesText가 null입니다!");
        }
        if (highScoreText != null)
        {
            highScoreText.text = $"최고 점수: {highScore}";
        }
        else
        {
            Debug.LogWarning("[EndSceneManager] highScoreText가 null입니다!");
        }
        if (deathCountText != null)
        {
            deathCountText.text = $"사망 횟수: {deathCount}";
        }
        else
        {
            Debug.LogWarning("[EndSceneManager] deathCountText가 null입니다!");
        }

        // 버튼 존재 여부 확인
        UnityEngine.UI.Button[] buttons = FindObjectsOfType<UnityEngine.UI.Button>();
        Debug.Log($"[EndSceneManager] 발견된 버튼 개수: {buttons.Length}");
        foreach (var btn in buttons)
        {
            Debug.Log($"[EndSceneManager] 버튼 이름: {btn.name}, Interactable: {btn.interactable}");
        }
    }

    void Update()
    {
        // ESC 키로 게임 종료
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
        }
    }

    void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    // 버튼 클릭 이벤트 처리 메서드
    public void OnPlayAgainButtonClicked()
    {
        Debug.Log("=====================================");
        Debug.Log("[EndSceneManager] 다시하기 버튼 클릭됨! 메서드 호출됨!");
        Debug.Log("[EndSceneManager] GameObject: " + gameObject.name);
        Debug.Log("=====================================");
        LoadJMScene();
    }

    public void OnMainMenuButtonClicked()
    {
        Debug.Log("=====================================");
        Debug.Log("[EndSceneManager] 초기화면 버튼 클릭됨! 메서드 호출됨!");
        Debug.Log("[EndSceneManager] GameObject: " + gameObject.name);
        Debug.Log("=====================================");
        LoadStartScene();
    }

    private void LoadJMScene()
    {
        Debug.Log("[EndSceneManager] JM 씬으로 이동합니다.");
        SceneManager.LoadScene("JM");
    }

    private void LoadStartScene()
    {
        Debug.Log("[EndSceneManager] StartScene으로 이동합니다.");
        SceneManager.LoadScene("StartScene");
    }
}
