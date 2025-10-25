using UnityEngine;
using UnityEngine.SceneManagement; 
using System.Collections;

public class ButtonController : MonoBehaviour
{
    private LiveBackgroundManager backgroundManager;

    void Start()
    {
        // LiveBackgroundManager 찾기
        backgroundManager = FindObjectOfType<LiveBackgroundManager>();
    }

    public void OnButtonClicked()
    {
        // LiveBackgroundManager가 있으면 그쪽에서 처리
        if (backgroundManager != null)
        {
            backgroundManager.StartGame();
        }
        else
        {
            // 없으면 그냥 씬 전환
            SceneManager.LoadScene("JM");
        }
    }
}

