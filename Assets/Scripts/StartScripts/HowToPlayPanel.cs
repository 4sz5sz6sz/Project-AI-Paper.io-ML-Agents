using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 사용

/// <summary>
/// 게임 설명 패널 컨트롤러
/// Start 버튼을 누르기 전 또는 'H' 키를 눌러 게임 방법을 확인할 수 있습니다.
/// </summary>
public class HowToPlayPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject howToPlayPanel; // 설명 패널
    public Button closeButton; // 닫기 버튼
    public Button howToPlayButton; // "게임 방법" 버튼 (선택사항)

    [Header("Font Settings (Optional)")]
    public TMP_FontAsset koreanFont; // 한글 폰트 (TextMeshPro)

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.H; // H 키로 열고 닫기
    public bool showOnStart = true; // 게임 시작 시 자동으로 표시

    void Start()
    {
        // 닫기 버튼에 이벤트 연결
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }

        // "게임 방법" 버튼이 있으면 이벤트 연결
        if (howToPlayButton != null)
        {
            howToPlayButton.onClick.AddListener(OpenPanel);
        }

        // 한글 폰트 적용
        if (koreanFont != null && howToPlayPanel != null)
        {
            ApplyKoreanFont();
        }

        // 시작 시 패널 표시 여부 설정
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(showOnStart);
        }
    }

    /// <summary>
    /// 패널 내 모든 TextMeshPro 텍스트에 한글 폰트 적용
    /// </summary>
    void ApplyKoreanFont()
    {
        TextMeshProUGUI[] textComponents = howToPlayPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in textComponents)
        {
            text.font = koreanFont;
        }
    }

    void Update()
    {
        // H 키로 토글
        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }

        // ESC 키 처리
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (howToPlayPanel != null && howToPlayPanel.activeSelf)
            {
                // 패널이 열려있으면 닫기
                ClosePanel();
            }
            else
            {
                // 패널이 닫혀있으면 게임 종료
                QuitGame();
            }
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

    public void OpenPanel()
    {
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(true);
        }
    }

    public void ClosePanel()
    {
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(false);
        }
    }

    public void TogglePanel()
    {
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(!howToPlayPanel.activeSelf);
        }
    }
}
